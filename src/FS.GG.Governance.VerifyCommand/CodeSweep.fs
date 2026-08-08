// #390 — the production caller #368 never had.
//
// `CodeChecks.analyze` shipped complete and tested under FS-GG/FS.GG.Governance#368, and #385 composed it
// into the published F# constitution profile as `fsharp:idiomatic-simplicity`. Nothing in `src/` invoked it,
// so the published gate was STRUCTURALLY INERT: green because no code path could look, not because the
// repository was clean. This module is the missing call site, reached from `Interpreter.senseSurfacesReal`
// exactly where #366's `FSharpSurface.evaluate` and #369's `FSharpEffectBoundary.evaluate` are reached.
//
// APPLICABILITY IS DECLARED, NOT ASSUMED — the #369 posture. `CodeChecks.analyzeDocument` type-checks a
// document through the reference set its caller supplies; it never discovers project files or assemblies. The
// declared `references` map below binds each selected source to its repository-relative assembly paths, so a
// multi-project repository can opt into real compiler analysis without giving this I/O-free pack a repository
// discovery seam. The pack is applicable exactly where a repository DECLARES the sources it wants analysed, in
// `.fsgg/fsharp-simplicity.json`, the same shape `.fsgg/fsharp-surface.json` already has for #366's policy.
// No declaration ⇒ no documents ⇒ no findings, and nothing is silently skipped: the absence is the
// repository's own recorded choice.
//
// FAIL CLOSED on a malformed declaration. Every parse defect raises `PolicyError`, which `sense` reifies as
// an `Error` and `senseSurfacesReal` turns into a Blocking input-state finding — never an empty pass. The
// exception is CONFINED to this module's parser: nothing throws out of `sense` or `findings`.
//
// INTERNAL on purpose: `VerifyCommand`'s public surface is exactly `Loop` + `Interpreter` + the three 076 fold
// seams (`SurfaceDriftTests`), and this is a private sense edge, not a new seam.

namespace FS.GG.Governance.VerifyCommand

open System
open System.IO
open System.Text.Json
open FS.GG.Governance.Config.Model

module internal CodeSweep =

    module SC = FS.GG.Governance.SurfaceChecks.Model
    module CP = FS.GG.Governance.SurfaceChecks.Profile
    module CM = FS.GG.Governance.CodeChecks.Model

    /// The repository's own declaration of this pack's scope. Absent ⇒ the pack is not applicable here.
    [<Literal>]
    let PolicyPath = ".fsgg/fsharp-simplicity.json"

    /// The surface id every finding of this pack carries — the `fsharp-*` naming #366 and #369 already use.
    [<Literal>]
    let SurfaceName = "fsharp-idiomatic-simplicity"

    let request: SC.SurfaceCheckRequest =
        { Domain = SC.DesignDomain
          Surface = SurfaceId SurfaceName
          Class = DesignSurface
          Path = normalizePath PolicyPath
          EvidenceTag = None }

    /// A defect in the declaration. Raised only by the parser below and caught only by `sense`.
    exception private PolicyError of string

    // ── Declaration parsing (fail-closed; every unreadable or unrecognised token raises) ──────────────

    /// `JsonElement.GetString()` is typed `string | null`; on a `JsonValueKind.String` element it never is.
    /// Normalize once, here, so every caller below stays non-nullable without a cast at each use site.
    let private text (element: JsonElement) : string =
        element.GetString() |> Option.ofObj |> Option.defaultValue ""

    let private prop (element: JsonElement) (name: string) : JsonElement option =
        match element.TryGetProperty name with
        | true, value when value.ValueKind <> JsonValueKind.Null -> Some value
        | _ -> None

    let private stringList (element: JsonElement) (name: string) : string list =
        match prop element name with
        | None -> []
        | Some value when value.ValueKind = JsonValueKind.Array ->
            value.EnumerateArray()
            |> Seq.map (fun item ->
                if item.ValueKind = JsonValueKind.String then
                    text item
                else
                    raise (PolicyError(sprintf "'%s' must contain only strings; found %O" name item.ValueKind)))
            |> Seq.toList
        | Some value -> raise (PolicyError(sprintf "'%s' must be an array; found %O" name value.ValueKind))

    let private optionalInt (element: JsonElement) (name: string) : int option =
        match prop element name with
        | None -> None
        | Some value when value.ValueKind = JsonValueKind.Number ->
            match value.TryGetInt32() with
            | true, number -> Some number
            | _ -> raise (PolicyError(sprintf "'%s' must be a 32-bit integer" name))
        | Some value -> raise (PolicyError(sprintf "'%s' must be a number; found %O" name value.ValueKind))

    let private requiredString (element: JsonElement) (name: string) : string =
        match prop element name with
        | Some value when value.ValueKind = JsonValueKind.String -> text value
        | Some value -> raise (PolicyError(sprintf "'%s' must be a string; found %O" name value.ValueKind))
        | None -> raise (PolicyError(sprintf "'%s' is required" name))

    let private optionalString (element: JsonElement) (name: string) : string =
        match prop element name with
        | None -> ""
        | Some value when value.ValueKind = JsonValueKind.String -> text value
        | Some value -> raise (PolicyError(sprintf "'%s' must be a string; found %O" name value.ValueKind))

    let private objects (element: JsonElement) (name: string) : JsonElement list =
        match prop element name with
        | None -> []
        | Some value when value.ValueKind = JsonValueKind.Array ->
            value.EnumerateArray()
            |> Seq.map (fun item ->
                if item.ValueKind = JsonValueKind.Object then
                    item
                else
                    raise (PolicyError(sprintf "'%s' must contain only objects; found %O" name item.ValueKind)))
            |> Seq.toList
        | Some value -> raise (PolicyError(sprintf "'%s' must be an array; found %O" name value.ValueKind))

    let private reason (element: JsonElement) : CM.JustificationReason =
        match requiredString element "reason" with
        | "measured" -> CM.Measured
        | "interoperability" -> CM.Interoperability
        | other ->
            raise (
                PolicyError(sprintf "unknown justification reason '%s' (expected 'measured' or 'interoperability')" other)
            )

    let private justification (element: JsonElement) : CM.ComplexityJustification =
        { Path = requiredString element "path"
          Symbol = requiredString element "symbol"
          Head = optionalString element "head"
          SourceDigest = optionalString element "sourceDigest"
          SimplerAlternative = optionalString element "simplerAlternative"
          Reason = reason element
          Evidence = optionalString element "evidence" }

    let private approvedPrimitive (element: JsonElement) : CM.ApprovedPrimitive =
        { Capability = requiredString element "capability"
          ApprovedSymbols = stringList element "approvedSymbols"
          CandidateSymbols = stringList element "candidateSymbols" }

    let private thresholds (root: JsonElement) : CM.ReviewThresholds =
        match prop root "thresholds" with
        | None ->
            { ModuleLines = None
              TypeLines = None
              MemberLines = None
              DependencyFanOut = None }
        | Some value when value.ValueKind = JsonValueKind.Object ->
            { ModuleLines = optionalInt value "moduleLines"
              TypeLines = optionalInt value "typeLines"
              MemberLines = optionalInt value "memberLines"
              DependencyFanOut = optionalInt value "dependencyFanOut" }
        | Some value -> raise (PolicyError(sprintf "'thresholds' must be an object; found %O" value.ValueKind))

    // ── Source expansion ─────────────────────────────────────────────────────────────────────────────
    // A declared entry is either one repo-relative `.fs` file or a directory prefix ending in `/`, which
    // contributes every `.fs` under it in sorted order. `obj/` and `bin/` outputs are excluded — they are
    // build products, not authored sources. A declared file that is not on disk is an ERROR, never a
    // silent omission: a policy that names a source the sweep cannot read has not been satisfied.

    let private isBuildOutput (relative: string) =
        relative.Split('/')
        |> Array.exists (fun segment ->
            String.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase)
            || String.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase))

    /// A generated document by naming convention (`*.g.fs`). `CodeChecks.analyze` drops these itself; the
    /// flag is carried so the pack — not this caller — remains the authority on what it analyses.
    let private isGenerated (relative: string) =
        relative.EndsWith(".g.fs", StringComparison.OrdinalIgnoreCase)

    // A DECLARATION GOVERNS ITS OWN REPOSITORY, AND THE PARSER ENFORCES THAT.
    //
    // Round 1 of independent review measured the hole this closes. Stripping leading `.`/`/` characters
    // and then `Path.Combine`-ing is NOT containment: it touches only the START of the entry, so a
    // MID-path `..` survives it and walks straight out of the tree. `"src/../../outside.fs"` was accepted
    // and analysed, and the DIRECTORY form was worse — `"src/../../outdir/"` yielded findings whose
    // `file` read `outdir/Out1.fs`, a governed-LOOKING path naming a file that is not in the repository,
    // so nothing downstream could see that the escape had happened.
    //
    // #366's and #369's sweeps cannot do this: they enumerate outward from `repo` and never resolve a
    // caller-supplied path. This pack takes declared paths, so it owns the check they never needed.
    //
    // The check is CANONICAL, not textual: resolve against the governed root and require the result to
    // stay under it. A rejection is the same `PolicyError` every other declaration defect raises, so it
    // fails closed through the existing Blocking input-state finding rather than silently dropping the entry.

    /// The governed root, canonicalized once and terminated with a separator, so a prefix test cannot
    /// match a SIBLING whose name merely starts with the root's (`/w/repo` must not admit `/w/repo-other`).
    let private governedRoot (repo: string) =
        let full = Path.GetFullPath repo

        if full.EndsWith(string Path.DirectorySeparatorChar, StringComparison.Ordinal) then
            full
        else
            full + string Path.DirectorySeparatorChar

    /// Resolve a declared entry against the governed root and REFUSE anything that lands outside it.
    /// Returns the canonical absolute path so the caller does not resolve it a second time (and cannot
    /// resolve it differently). `entry` is the declaration AS WRITTEN and is used only for the diagnostic.
    let private contained (repo: string) (entry: string) (relative: string) : string =
        let root = governedRoot repo
        let candidate = Path.GetFullPath(Path.Combine(root, relative))

        let terminated =
            if candidate.EndsWith(string Path.DirectorySeparatorChar, StringComparison.Ordinal) then
                candidate
            else
                candidate + string Path.DirectorySeparatorChar

        if not (terminated.StartsWith(root, StringComparison.Ordinal)) then
            raise (
                PolicyError(
                    sprintf
                        "declared source '%s' resolves outside the repository; a declaration governs this repository's own sources only"
                        entry
                )
            )

        candidate

    // NORMALIZE THE WHOLE `./` SEGMENT, SEPARATOR RUN AND ALL (review round 2, F3).
    //
    // Round 1 replaced `TrimStart('.', '/')` with a fixed 2-character `./` strip, which was wrong in BOTH
    // directions and regressed two shapes that round 1's own comment claimed to fix:
    //
    //   `.//src/Dirty.fs`  a valid in-repo relative path. Stripping exactly two characters left
    //                      `/src/Dirty.fs`, which `contained` then refused as absolute — a FALSE REFUSAL
    //                      whose diagnostic quoted `/src/Dirty.fs`, a path the author never wrote and
    //                      byte-identical to the message for a genuinely absolute declaration.
    //   `/`                left untouched, so it reached the directory arm, `TrimEnd('/')` made it empty,
    //                      and `Path.Combine(root, "")` resolved to the repository root: the whole tree
    //                      swept SILENTLY — precisely the reinterpretation this widening set out to remove.
    //
    // Neither was a containment escape (the first fails closed, the second stays inside the repo), but a
    // false refusal on valid input and a silent reinterpretation are both defects in their own right.
    //
    // So: consume `./` as a SEGMENT — the dot plus the separator run behind it — and leave a leading
    // separator the author actually wrote in place, where `expand` refuses it explicitly.
    let rec private stripLeadingCurrent (value: string) =
        if value.StartsWith("./", StringComparison.Ordinal) then
            stripLeadingCurrent ((value.Substring 1).TrimStart('/'))
        else
            value

    // EVERY DIAGNOSTIC QUOTES THE ENTRY AS WRITTEN. Normalization is an internal step; quoting its OUTPUT
    // is how round 1 told an operator their declaration was `/src/Dirty.fs` when they had written
    // `.//src/Dirty.fs`, and made a false refusal indistinguishable from a correct one.
    let private expand (repo: string) (entry: string) : string list =
        let normalized = entry.Replace('\\', '/') |> stripLeadingCurrent

        if normalized = "" then
            raise (PolicyError(sprintf "declared source '%s' does not name a source" entry))
        elif normalized.StartsWith("/", StringComparison.Ordinal) then
            // Absolute, as written — including a bare `/`. Refused, never reinterpreted as the repo root.
            raise (
                PolicyError(
                    sprintf
                        "declared source '%s' is an absolute path; a declaration names this repository's own sources, relative to its root"
                        entry
                )
            )
        elif normalized.EndsWith("/", StringComparison.Ordinal) then
            let directory = contained repo entry (normalized.TrimEnd('/'))

            if not (Directory.Exists directory) then
                raise (PolicyError(sprintf "declared source directory '%s' does not exist" entry))

            Directory.EnumerateFiles(directory, "*.fs", SearchOption.AllDirectories)
            |> Seq.map (fun path -> Path.GetRelativePath(repo, path).Replace('\\', '/'))
            |> Seq.filter (isBuildOutput >> not)
            |> Seq.toList
        elif not (normalized.EndsWith(".fs", StringComparison.OrdinalIgnoreCase)) then
            raise (
                PolicyError(
                    sprintf
                        "declared source '%s' is neither an F# implementation file nor a directory prefix ending in '/'"
                        entry
                )
            )
        elif not (File.Exists(contained repo entry normalized)) then
            raise (PolicyError(sprintf "declared source '%s' does not exist" entry))
        else
            [ normalized ]

    let private referenceMap (repo: string) (root: JsonElement) : Map<string, string list> =
        match prop root "references" with
        | None -> Map.empty
        | Some value when value.ValueKind = JsonValueKind.Object ->
            value.EnumerateObject()
            |> Seq.map (fun property ->
                let references =
                    if property.Value.ValueKind <> JsonValueKind.Array then
                        raise (PolicyError(sprintf "references for '%s' must be an array; found %O" property.Name property.Value.ValueKind))

                    property.Value.EnumerateArray()
                    |> Seq.map (fun item ->
                        if item.ValueKind <> JsonValueKind.String then
                            raise (PolicyError(sprintf "references for '%s' must contain only strings; found %O" property.Name item.ValueKind))

                        let declared = text item
                        let normalized = declared.Replace('\\', '/') |> stripLeadingCurrent

                        if normalized = "" || normalized.StartsWith("/", StringComparison.Ordinal) then
                            raise (PolicyError(sprintf "declared reference '%s' must name a repository-relative assembly path" declared))

                        let full = contained repo declared normalized

                        if not (File.Exists full) then
                            raise (PolicyError(sprintf "declared reference '%s' does not exist" declared))

                        full)
                    |> Seq.toList

                normalizePath property.Name, references)
            |> Map.ofSeq
        | Some value -> raise (PolicyError(sprintf "'references' must be an object; found %O" value.ValueKind))

    let private documents (repo: string) (entries: string list) (references: Map<string, string list>) : CM.SourceDocument list =
        entries
        |> List.collect (expand repo)
        |> List.distinct
        |> List.sort
        |> List.map (fun relative ->
            { Path = relative
              Source = File.ReadAllText(Path.Combine(repo, relative))
              IsGenerated = isGenerated relative
              References = references |> Map.tryFind relative |> Option.defaultValue [] })

    /// Read the repository's declaration. `Ok None` means the pack is not applicable here (no policy file,
    /// or a policy that declares no sources — an empty declaration is still a declaration).
    let sense (repo: string) : Result<CM.AnalysisRequest option, string> =
        try
            let path = Path.Combine(repo, PolicyPath)

            if not (File.Exists path) then
                Ok None
            else
                use document = JsonDocument.Parse(File.ReadAllText path)
                let root = document.RootElement

                if root.ValueKind <> JsonValueKind.Object then
                    Error(sprintf "%s must contain a JSON object; found %O" PolicyPath root.ValueKind)
                else
                    match stringList root "sources" with
                    | [] -> Ok None
                    | sources ->
                        let references = referenceMap repo root
                        let documents = documents repo sources references
                        let selected = documents |> List.map _.Path |> Set.ofList
                        let undeclared = references |> Map.keys |> Seq.filter (fun path -> not (Set.contains path selected)) |> Seq.toList

                        if not undeclared.IsEmpty then
                            Error(sprintf "references declare source(s) outside 'sources': %s" (String.concat ", " undeclared))
                        else
                            Ok(
                                Some
                                    { Head = optionalString root "head"
                                      Documents = documents
                                      PureDomainPrefixes = stringList root "pureDomainPrefixes"
                                      Thresholds = thresholds root
                                      Justifications = objects root "justifications" |> List.map justification
                                      ApprovedPrimitives =
                                        objects root "approvedPrimitives" |> List.map approvedPrimitive }
                            )
        with
        | PolicyError detail -> Error(sprintf "%s is malformed: %s" PolicyPath detail)
        | ex -> Error(sprintf "%s could not be read: %s" PolicyPath ex.Message)

    /// The pack's contribution to the `fsgg verify` surface sweep. Every finding is normalized through
    /// `Profile.findingOf`, so it carries the composed profile's declared maturity for #368 and reaches
    /// `Enforcement.deriveEffectiveSeverity` through the same fold as every other surface finding.
    let findings (repo: string) : Result<SC.SurfaceFinding list, string> =
        match sense repo with
        | Error failure -> Error failure
        | Ok None -> Ok []
        | Ok(Some analysis) ->
            try
                let report =
                    FS.GG.Governance.CodeChecks.CodeChecks.analyze analysis |> Async.RunSynchronously

                report.Findings
                |> List.map (fun finding ->
                    CP.findingOf
                        CP.IdiomaticSimplicity
                        (CM.findingIdToken finding.Id)
                        request
                        (normalizePath finding.Path)
                        finding.Symbol
                        finding.Message)
                |> Ok
            with ex ->
                Error(sprintf "F# idiomatic-simplicity analysis threw: %s" ex.Message)
