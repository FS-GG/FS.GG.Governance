// Pure handoff parse + version-check (F081, US2). Visibility lives in Reader.fsi (Principle II).
// BCL-only: System.Text.Json `JsonDocument` (research D2). PURE and TOTAL — never throws
// (Constitution VI): malformed / missing-required / unknown-major / declared-autoSynthetic each
// becomes a distinct, descriptive `Diagnostic`.

namespace FS.GG.Governance.Adapters.SddHandoff

open System.Text.Json
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Adapters.SddHandoff.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Reader =

    type HandoffRead =
        { Source: string
          Json: string }

    // ── Small total JsonElement accessors (no throw — kind-checked) ──

    let private tryProp (e: JsonElement) (name: string) : JsonElement option =
        match e.TryGetProperty name with
        | true, v -> Some v
        | false, _ -> None

    let private asString (e: JsonElement) : string option =
        if e.ValueKind = JsonValueKind.String then Option.ofObj (e.GetString()) else None

    let private asBool (e: JsonElement) : bool option =
        match e.ValueKind with
        | JsonValueKind.True -> Some true
        | JsonValueKind.False -> Some false
        | _ -> None

    let private asInt (e: JsonElement) : int option =
        if e.ValueKind = JsonValueKind.Number then
            match e.TryGetInt32() with
            | true, n -> Some n
            | false, _ -> None
        else
            None

    // The declared-state token map (kernel tokens + SDD `deferred`/`accepted-deferral`). A declared
    // `autoSynthetic` is its OWN distinct rejection (research D4), never generic Malformed.
    let private parseDeclaredState (token: string) : Result<DeclaredState, DiagnosticCause> =
        match token with
        | "pending" -> Ok Pending
        | "real" -> Ok Real
        | "synthetic" -> Ok Synthetic
        | "failed" -> Ok Failed
        | "skipped" -> Ok Skipped
        | "deferred" -> Ok Deferred
        | "accepted-deferral" -> Ok AcceptedDeferral
        | "autoSynthetic" -> Error AutoSyntheticDeclared
        | _ -> Error Malformed

    // The MAJOR component of a semver string (text before the first `.`), or None if absent/non-numeric.
    let private majorOf (version: string) : int option =
        let head = version.Split('.') |> Array.tryHead
        match head with
        | Some h ->
            match System.Int32.TryParse h with
            | true, n -> Some n
            | false, _ -> None
        | None -> None

    let parse (read: HandoffRead) : Result<Handoff, Diagnostic> =
        let diag cause message : Diagnostic =
            { Cause = cause; Source = read.Source; Message = message }

        let malformed msg = Error(diag Malformed msg)

        // Distinct, descriptive messages per cause (SC-004).
        try
            use doc = JsonDocument.Parse read.Json
            let root = doc.RootElement

            if root.ValueKind <> JsonValueKind.Object then
                malformed "handoff document is not a JSON object"
            else

            // contractVersion (required) + major version pin (FR-002).
            match tryProp root "contractVersion" |> Option.bind asString with
            | None -> malformed "handoff is missing the required string field 'contractVersion'"
            | Some contractVersion ->

            match majorOf contractVersion with
            | None -> malformed (sprintf "handoff 'contractVersion' is not a recognizable semver: '%s'" contractVersion)
            | Some major when major <> supportedContractMajor ->
                Error(
                    diag
                        VersionMismatch
                        (sprintf
                            "handoff 'contractVersion' major %d is unsupported (this consumer pins major %d): '%s'"
                            major
                            supportedContractMajor
                            contractVersion)
                )
            | Some _ ->

            let schemaVersion =
                tryProp root "schemaVersion" |> Option.bind asInt |> Option.defaultValue 1

            // evidence (required object).
            match tryProp root "evidence" with
            | None -> malformed "handoff is missing the required 'evidence' block"
            | Some evidence when evidence.ValueKind <> JsonValueKind.Object ->
                malformed "handoff 'evidence' is not a JSON object"
            | Some evidence ->

            // evidence.nodes (required array). Each node: id (string), state (token), stale?, rationale?.
            let nodesEl = tryProp evidence "nodes"

            match nodesEl with
            | None -> malformed "handoff 'evidence.nodes' is missing"
            | Some nodesEl when nodesEl.ValueKind <> JsonValueKind.Array ->
                malformed "handoff 'evidence.nodes' is not an array"
            | Some nodesEl ->

            // Fold nodes, surfacing the FIRST node-level diagnostic (autoSynthetic / malformed token).
            let parsedNodes =
                nodesEl.EnumerateArray()
                |> Seq.fold
                    (fun (acc: Result<DeclaredNode list, Diagnostic>) nodeEl ->
                        acc
                        |> Result.bind (fun ns ->
                            match tryProp nodeEl "id" |> Option.bind asString with
                            | None -> malformed "an 'evidence.nodes[]' entry is missing the required string 'id'"
                            | Some id ->
                                match tryProp nodeEl "state" |> Option.bind asString with
                                | None ->
                                    malformed (sprintf "evidence node '%s' is missing the required string 'state'" id)
                                | Some stateToken ->
                                    match parseDeclaredState stateToken with
                                    | Error AutoSyntheticDeclared ->
                                        Error(
                                            diag
                                                AutoSyntheticDeclared
                                                (sprintf
                                                    "evidence node '%s' declares state 'autoSynthetic', which is computed-only and never a valid declared input (FR-005)"
                                                    id)
                                        )
                                    | Error _ ->
                                        malformed (
                                            sprintf "evidence node '%s' declares an unknown state token '%s'" id stateToken
                                        )
                                    | Ok state ->
                                        let stale =
                                            tryProp nodeEl "stale" |> Option.bind asBool |> Option.defaultValue false

                                        let rationale = tryProp nodeEl "rationale" |> Option.bind asString

                                        // Prepend (O(1)) and reverse once after the fold (#56/C1f) instead of
                                        // `ns @ [node]` per element, which was O(n²). Order is restored below.
                                        Ok({ Id = id; State = state; Stale = stale; Rationale = rationale } :: ns)))
                    (Ok [])

            match parsedNodes with
            | Error d -> Error d
            | Ok nodesReversed ->

            // Restore source order once (#56/C1f): the fold above prepended for O(n) accumulation.
            let nodes = List.rev nodesReversed

            // evidence.dependencies (optional array of 2-string tuples). Parsed STRICTLY, like nodes
            // above: a present-but-malformed edge is REJECTED as Malformed, never silently dropped
            // (ADPT-2). AutoSynthetic taint flows ALONG these edges, so a dropped edge could leave a
            // downstream verdict resting on a synthetic node un-tainted — a taint fail-open. An absent
            // `dependencies` field (or an explicit `null`) is fine (Ok []) since it is optional and
            // carries no edges to drop; a present *value* must be a well-formed [from, to] list.
            let parsedDeps =
                match tryProp evidence "dependencies" with
                | None -> Ok []
                | Some depsEl when depsEl.ValueKind = JsonValueKind.Null -> Ok []
                | Some depsEl when depsEl.ValueKind <> JsonValueKind.Array ->
                    malformed "handoff 'evidence.dependencies' is present but is not an array"
                | Some depsEl ->
                    depsEl.EnumerateArray()
                    |> Seq.fold
                        (fun (acc: Result<(string * string) list, Diagnostic>) pair ->
                            acc
                            |> Result.bind (fun ds ->
                                if pair.ValueKind <> JsonValueKind.Array then
                                    malformed "an 'evidence.dependencies[]' entry is not a 2-element array"
                                else
                                    // Do NOT Seq.choose here: a non-string element must fail the edge,
                                    // not be silently skipped into a shorter (and possibly matching) list.
                                    match pair.EnumerateArray() |> Seq.map asString |> Seq.toList with
                                    | [ Some a; Some b ] -> Ok((a, b) :: ds)
                                    | _ ->
                                        malformed
                                            "an 'evidence.dependencies[]' entry is not a pair of strings [from, to]"))
                        (Ok [])
                    // Prepended for O(n); restore source order once (mirrors the nodes fold above).
                    |> Result.map List.rev

            match parsedDeps with
            | Error d -> Error d
            | Ok deps ->

            // readiness (optional object).
            let readiness =
                match tryProp root "readiness" with
                | Some r when r.ValueKind = JsonValueKind.Object ->
                    let str name = tryProp r name |> Option.bind asString |> Option.defaultValue ""

                    let blocking =
                        match tryProp r "blockingDiagnosticIds" with
                        | Some b when b.ValueKind = JsonValueKind.Array ->
                            b.EnumerateArray() |> Seq.choose asString |> Seq.toList
                        | _ -> []

                    let counts =
                        match tryProp r "counts" with
                        | Some c when c.ValueKind = JsonValueKind.Object ->
                            c.EnumerateObject()
                            |> Seq.choose (fun p -> asInt p.Value |> Option.map (fun n -> p.Name, n))
                            |> Seq.toList
                        | _ -> []

                    let perViewState =
                        match tryProp r "perViewState" with
                        | Some p when p.ValueKind = JsonValueKind.Object ->
                            p.EnumerateObject()
                            |> Seq.choose (fun e -> asString e.Value |> Option.map (fun v -> e.Name, v))
                            |> Seq.toList
                        | _ -> []

                    Some
                        { ShipDisposition = str "shipDisposition"
                          VerificationReadiness = str "verificationReadiness"
                          BlockingDiagnosticIds = blocking
                          Counts = counts
                          PerViewState = perViewState }
                | _ -> None

            // governedReferences (optional array; routing enrichment only — FR-010).
            let governedReferences =
                match tryProp root "governedReferences" with
                | Some g when g.ValueKind = JsonValueKind.Array ->
                    g.EnumerateArray()
                    |> Seq.choose (fun refEl ->
                        if refEl.ValueKind = JsonValueKind.Object then
                            let workItem = tryProp refEl "workItem" |> Option.bind asString |> Option.defaultValue ""

                            let paths =
                                match tryProp refEl "paths" with
                                | Some p when p.ValueKind = JsonValueKind.Array ->
                                    p.EnumerateArray()
                                    |> Seq.choose asString
                                    |> Seq.map normalizePath
                                    |> Seq.toList
                                | _ -> []

                            Some { WorkItem = workItem; Paths = paths }
                        else
                            None)
                    |> Seq.toList
                | _ -> []

            Ok
                { ContractVersion = contractVersion
                  SchemaVersion = schemaVersion
                  Evidence = { Nodes = nodes; Dependencies = deps }
                  Readiness = readiness
                  GovernedReferences = governedReferences }
        with ex ->
            // Any parse failure (malformed JSON, unexpected shape) is a descriptive Malformed diagnostic,
            // never a throw (Constitution VI).
            malformed ("handoff JSON could not be parsed: " + ex.Message)
