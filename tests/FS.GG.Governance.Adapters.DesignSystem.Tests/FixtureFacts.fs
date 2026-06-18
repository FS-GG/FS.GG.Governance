module FS.GG.Governance.Adapters.DesignSystem.Tests.FixtureFacts

open System.IO
open System.Text.Json
open FS.GG.Governance.Kernel
open FS.GG.Governance.Adapters.DesignSystem

// TEST-only helper that reads the fixture token tree (a few JSON files under fixtures/) and
// lifts it to DesignSystemFacts. This is the domain's REAL input (Principle V), and the
// substrate every catalog evaluation/explanation test runs against. It uses the BCL
// System.Text.Json only (no new package, no rendering library) — this is the test harness,
// not the adapter. SENSING a live design system into facts is the F08 effects shell / F12
// CLI's job, NOT this feature (FR-015); here the fixtures encode the sensed observations as
// data directly.

let fixturesDir = Path.Combine(System.AppContext.BaseDirectory, "fixtures")

let artifactOfName (name: string) : DesignArtifactRef =
    match name with
    | "token-document" -> TokenDocument
    | "generated-token-surface" -> GeneratedTokenSurface
    | "rendered-capture" -> RenderedCapture
    | "interaction-state-spec" -> InteractionStateSpec
    | "page-pattern-spec" -> PagePatternSpec
    | other -> failwithf "unknown fixture artifact '%s'" other

let stateOfName (name: string) : EvidenceState =
    match name with
    | "Pending" -> Pending
    | "Real" -> Real
    | "Synthetic" -> Synthetic
    | "Failed" -> Failed
    | "Skipped" -> Skipped
    | other -> failwithf "unknown / non-authored evidence state '%s'" other

let private readDoc (file: string) : JsonElement =
    let path = Path.Combine(fixturesDir, file)
    use doc = JsonDocument.Parse(File.ReadAllText path)
    doc.RootElement.Clone()

/// A non-null string from a JSON element (the fixtures never carry JSON null here).
let private str (e: JsonElement) : string =
    match e.GetString() with
    | null -> failwith "fixture string value was null"
    | s -> s

/// Presence + the sensed observations declared on one artifact surface file.
let private surfaceFacts (subject: DesignArtifactRef) (file: string) : DesignSystemFact list =
    let root = readDoc file

    [ match root.TryGetProperty "present" with
      | true, p when p.GetBoolean() -> ArtifactPresent subject
      | _ -> ()

      match root.TryGetProperty "observations" with
      | true, obs ->
          for o in obs.EnumerateObject() do
              SurfaceObservation(o.Name, subject, o.Value.GetBoolean())
      | _ -> () ]

/// The F05 evidence pair (measurements + verdict-rests-on edges) declared on the generated
/// token surface file.
let private evidenceFacts (file: string) : DesignSystemFact list =
    let root = readDoc file

    [ match root.TryGetProperty "measurements" with
      | true, ms ->
          for m in ms.EnumerateArray() do
              MeasurementState(str (m.GetProperty "id"), stateOfName (str (m.GetProperty "state")))
      | _ -> ()

      match root.TryGetProperty "verdictRestsOn" with
      | true, vs ->
          for v in vs.EnumerateArray() do
              VerdictRestsOn(str (v.GetProperty "verdict"), str (v.GetProperty "measurement"))
      | _ -> () ]

let private policyFacts (file: string) : DesignSystemFact list =
    let root = readDoc file

    [ match root.TryGetProperty "policy" with
      | true, p -> PolicySelected(str p)
      | _ -> ()

      match root.TryGetProperty "designRules" with
      | true, rs ->
          for r in rs.EnumerateArray() do
              DesignRule(str r)
      | _ -> () ]

/// A supplied DesignSystemFact with the adapter's own identity (provenance empty — asserted).
let fact (v: DesignSystemFact) : FactAssertion<DesignSystemFact> =
    { Id = DesignSystem.identify v; Value = v; Provenance = [] }

/// The conforming fixture token tree, lifted to a fact set — every deterministic observation
/// is `true` and every measurement is `Real`, so the deterministic catalog passes. The real
/// input the full-catalog evaluation/explanation tests run against (SC-003).
let conformingFacts: FactSet<DesignSystemFact> =
    [ yield! policyFacts "policy.json"
      yield! surfaceFacts TokenDocument "token-document.json"
      yield! surfaceFacts GeneratedTokenSurface "generated-token-surface.json"
      yield! surfaceFacts InteractionStateSpec "interaction-state-spec.json"
      yield! surfaceFacts PagePatternSpec "page-pattern-spec.json"
      yield! evidenceFacts "generated-token-surface.json" ]
    |> List.map fact
