// #390 — the production caller #370 never had.
//
// `EvidenceBoundary.evaluate` shipped complete and tested under FS-GG/FS.GG.Governance#370, and #385 composed
// it into the published F# constitution profile as `fsharp:evidence-boundary`. Nothing in `src/` invoked it,
// so — exactly like #368's pack — the published gate was green because no code path could look. This module
// is the missing call site, reached from `Interpreter.senseSurfacesReal`.
//
// APPLICABILITY IS DECLARED, NOT ASSUMED. `EvidenceBoundary.evaluate` is fail-closed by construction: over an
// EMPTY request it still emits three `evidence.real-boundary-required` findings, because semantic-regression,
// boundary-fixture and golden-or-schema evidence are unconditionally required of a subject that HAS an
// evidence obligation. Swept unconditionally, every repository would acquire three findings for having no
// obligation at all — the pack would be measuring absence, not compliance. The obligation is therefore
// declared, in `.fsgg/evidence-boundary.json`, mirroring the record shapes `EvidenceBoundary` already
// defines. No declaration ⇒ no obligation ⇒ no findings.
//
// FAIL CLOSED on a malformed declaration: every parse defect raises `PolicyError`, reified by `sense` as an
// `Error` and by `senseSurfacesReal` as a Blocking input-state finding — never an empty pass. The exception
// is CONFINED to this module's parser: nothing throws out of `sense` or `findings`.
//
// INTERNAL on purpose: this is a private sense edge, not a new `VerifyCommand` seam.

namespace FS.GG.Governance.VerifyCommand

open System
open System.IO
open System.Text.Json
open FS.GG.Governance.Config.Model

module internal EvidenceSweep =

    module SC = FS.GG.Governance.SurfaceChecks.Model
    module CP = FS.GG.Governance.SurfaceChecks.Profile
    module EB = FS.GG.Governance.DesignChecks.EvidenceBoundary

    /// The repository's own declaration of this pack's obligation. Absent ⇒ the pack is not applicable here.
    [<Literal>]
    let PolicyPath = ".fsgg/evidence-boundary.json"

    [<Literal>]
    let SurfaceName = "fsharp-evidence-boundary"

    let request: SC.SurfaceCheckRequest =
        { Domain = SC.DesignDomain
          Surface = SurfaceId SurfaceName
          Class = DesignSurface
          Path = normalizePath PolicyPath
          EvidenceTag = None }

    /// A defect in the declaration. Raised only by the parser below and caught only by `sense`.
    exception private PolicyError of string

    // ── Declaration parsing (fail-closed) ────────────────────────────────────────────────────────────

    /// `JsonElement.GetString()` is typed `string | null`; on a `JsonValueKind.String` element it never is.
    /// Normalize once, here, so every caller below stays non-nullable without a cast at each use site.
    let private text (element: JsonElement) : string =
        element.GetString() |> Option.ofObj |> Option.defaultValue ""

    let private prop (element: JsonElement) (name: string) : JsonElement option =
        match element.TryGetProperty name with
        | true, value when value.ValueKind <> JsonValueKind.Null -> Some value
        | _ -> None

    let private flag (element: JsonElement) (name: string) : bool =
        match prop element name with
        | None -> false
        | Some value when value.ValueKind = JsonValueKind.True -> true
        | Some value when value.ValueKind = JsonValueKind.False -> false
        | Some value -> raise (PolicyError(sprintf "'%s' must be a boolean; found %O" name value.ValueKind))

    let private optionalString (element: JsonElement) (name: string) : string =
        match prop element name with
        | None -> ""
        | Some value when value.ValueKind = JsonValueKind.String -> text value
        | Some value -> raise (PolicyError(sprintf "'%s' must be a string; found %O" name value.ValueKind))

    let private optionalStringOption (element: JsonElement) (name: string) : string option =
        match prop element name with
        | None -> None
        | Some value when value.ValueKind = JsonValueKind.String -> Some(text value)
        | Some value -> raise (PolicyError(sprintf "'%s' must be a string; found %O" name value.ValueKind))

    let private optionalInt (element: JsonElement) (name: string) : int =
        match prop element name with
        | None -> 0
        | Some value when value.ValueKind = JsonValueKind.Number ->
            match value.TryGetInt32() with
            | true, number -> number
            | _ -> raise (PolicyError(sprintf "'%s' must be a 32-bit integer" name))
        | Some value -> raise (PolicyError(sprintf "'%s' must be a number; found %O" name value.ValueKind))

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

    let private requiredString (element: JsonElement) (name: string) : string =
        match prop element name with
        | Some value when value.ValueKind = JsonValueKind.String -> text value
        | Some value -> raise (PolicyError(sprintf "'%s' must be a string; found %O" name value.ValueKind))
        | None -> raise (PolicyError(sprintf "'%s' is required on every evidence record" name))

    let private kind (element: JsonElement) : EB.EvidenceKind =
        match requiredString element "kind" with
        | "semantic-regression" -> EB.SemanticRegression
        | "boundary-fixture" -> EB.BoundaryFixture
        | "golden-or-schema" -> EB.GoldenOrSchema
        | "production-journey" -> EB.ProductionJourney
        | other -> raise (PolicyError(sprintf "unknown evidence kind '%s'" other))

    let private observation (element: JsonElement) : EB.Observation =
        match requiredString element "observation" with
        | "dispatch-only" -> EB.DispatchOnly
        | "observed-outcome" -> EB.ObservedOutcome
        | "malformed-input" -> EB.MalformedInput
        | "unknown-input" -> EB.UnknownInput
        | "partial-write" -> EB.PartialWrite
        | "degraded" -> EB.Degraded
        | other -> raise (PolicyError(sprintf "unknown evidence observation '%s'" other))

    // `representedSeam` is deliberately NOT required here. A synthetic record that names no seam is exactly
    // what `evidence.synthetic-undisclosed` exists to report, so rejecting it at the parser would hide the
    // pack's own rule behind an input-state error.
    let private provenance (element: JsonElement) : EB.Provenance =
        match requiredString element "provenance" with
        | "real" -> EB.Real
        | "synthetic" -> EB.Synthetic(optionalString element "representedSeam")
        | other ->
            raise (PolicyError(sprintf "unknown evidence provenance '%s' (expected 'real' or 'synthetic')" other))

    let private record (element: JsonElement) : EB.EvidenceRecord =
        { Subject = optionalString element "subject"
          Kind = kind element
          Provenance = provenance element
          Command = optionalString element "command"
          ExitCode = optionalInt element "exitCode"
          SourceDigest = optionalString element "sourceDigest"
          Fresh = flag element "fresh"
          Observation = observation element }

    let private artifact (element: JsonElement) : EB.GeneratedArtifact =
        { Path = optionalString element "path"
          Source = optionalStringOption element "source"
          RegenerationDeterministic = flag element "regenerationDeterministic"
          Consumer = optionalStringOption element "consumer"
          HasGoldenOrSchema = flag element "hasGoldenOrSchema" }

    let private mitigation (element: JsonElement) : EB.Mitigation =
        { Claim = optionalString element "claim"
          ProducerClasses = stringList element "producerClasses"
          ReintroducedByMutation = stringList element "reintroducedByMutation" }

    let private render (root: JsonElement) : EB.RenderEvidence option =
        match prop root "render" with
        | None -> None
        | Some value when value.ValueKind = JsonValueKind.Object ->
            Some
                { Fixture = optionalString value "fixture"
                  Executed = flag value "executed"
                  ByteReproducible = flag value "byteReproducible"
                  SemanticReceiptStable = flag value "semanticReceiptStable" }
        | Some value -> raise (PolicyError(sprintf "'render' must be an object; found %O" value.ValueKind))

    /// Read the repository's declared evidence obligation. `Ok None` ⇒ the pack is not applicable here.
    let sense (repo: string) : Result<EB.Request option, string> =
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
                    Ok(
                        Some
                            { RequiresProductionJourney = flag root "requiresProductionJourney"
                              RequiresObservedOutcome = flag root "requiresObservedOutcome"
                              OptionalIntegrations = stringList root "optionalIntegrations"
                              Evidence = objects root "evidence" |> List.map record
                              GeneratedArtifacts = objects root "generatedArtifacts" |> List.map artifact
                              Mitigations = objects root "mitigations" |> List.map mitigation
                              Render = render root }
                    )
        with
        | PolicyError detail -> Error(sprintf "%s is malformed: %s" PolicyPath detail)
        | ex -> Error(sprintf "%s could not be read: %s" PolicyPath ex.Message)

    /// The pack's contribution to the `fsgg verify` surface sweep, normalized through `Profile.findingOf` so
    /// it carries #370's declared maturity and reaches `Enforcement.deriveEffectiveSeverity` through the
    /// same fold as every other surface finding.
    let findings (repo: string) : Result<SC.SurfaceFinding list, string> =
        match sense repo with
        | Error failure -> Error failure
        | Ok None -> Ok []
        | Ok(Some obligation) ->
            try
                EB.evaluate obligation
                |> List.map (fun finding ->
                    CP.findingOf
                        CP.EvidenceBoundary
                        finding.Code
                        request
                        (normalizePath PolicyPath)
                        finding.Subject
                        finding.Correction)
                |> Ok
            with ex ->
                Error(sprintf "evidence-boundary evaluation threw: %s" ex.Message)
