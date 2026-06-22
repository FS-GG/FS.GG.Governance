module FS.GG.Governance.FreshnessResolution.Tests.CandidateBridgeTests

open Expecto
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.CacheEligibility
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.FreshnessResolution
open FS.GG.Governance.FreshnessResolution.Model
open FS.GG.Governance.FreshnessResolution.Tests.Support

// User Story 1 — the F041 BRIDGE (SC-007, FR-004, L-candidate / L-recompute-safe). `candidate` of a `Resolved`
// entry is `Some { Gate; Inputs }` accepted by the GENUINE `CacheEligibility.evaluate` WITHOUT adaptation;
// `candidate` of an `Unresolved` entry is `None` (recompute-safe by construction — no path from `Unresolved` to
// a candidate). A `Resolved` outcome is necessary-not-sufficient: the entry holds only `Gate` + `Outcome`.

[<Tests>]
let tests =
    testList
        "CandidateBridge"
        [ test "candidate of a Resolved entry is Some { Gate = entry.Gate; Inputs = resolved FreshnessInputs }" {
              match FreshnessResolution.entries (FreshnessResolution.resolve [ gBuildTests ] fullSensed) with
              | [ e ] ->
                  match e.Outcome with
                  | Resolved i -> Expect.equal (FreshnessResolution.candidate e) (Some { Gate = e.Gate; Inputs = i }) "candidate pairs the entry's gate with its resolved inputs"
                  | Unresolved facts -> failtestf "expected Resolved, got Unresolved %A" facts
              | other -> failtestf "expected one entry, got %d" (List.length other)
          }

          test "resolved candidates are accepted by real CacheEligibility.evaluate without adaptation ⇒ one verdict per resolved gate (SC-007)" {
              let gates = [ gBuildTests; gLintStyle; gDocsCheck ] // all fully sensed by fullSensed
              let report = FreshnessResolution.resolve gates fullSensed
              let cands = FreshnessResolution.entries report |> List.choose FreshnessResolution.candidate
              Expect.equal (List.length cands) 3 "all three gates resolve to candidates"
              // Feed straight into F041 over a real (empty) store — no prior evidence ⇒ MustRecompute per gate.
              let cacheEntries = CacheEligibility.entries (cacheReport cands EvidenceReuse.empty)
              Expect.equal (List.length cacheEntries) 3 "F041 produces exactly one verdict per resolved candidate"
          }

          test "a recorded resolved candidate is deemed Reusable by F041 (the bridge round-trips to a real verdict)" {
              let report = FreshnessResolution.resolve [ gBuildTests ] fullSensed
              let cands = FreshnessResolution.entries report |> List.choose FreshnessResolution.candidate

              match cands with
              | [ c ] ->
                  // Record the resolved inputs under an opaque handle, then evaluate the SAME candidate.
                  let store = storeOf [ c.Inputs, EvidenceRef "ev-A" ]

                  match CacheEligibility.entries (cacheReport cands store) with
                  | [ e ] -> Expect.isTrue (CacheEligibility.isReusable e.Verdict) "an exact-match resolved candidate is Reusable in F041"
                  | other -> failtestf "expected one verdict, got %d" (List.length other)
              | other -> failtestf "expected one candidate, got %d" (List.length other)
          }

          test "candidate of an Unresolved entry is None — recompute-safe by construction (FR-004)" {
              // Drop every repo-wide fact ⇒ gBuildTests is Unresolved.
              let unsensed =
                  { fullSensed with
                      RuleHash = None
                      GeneratorVersion = None
                      Base = None
                      Head = None }

              match FreshnessResolution.entries (FreshnessResolution.resolve [ gBuildTests ] unsensed) with
              | [ e ] ->
                  Expect.isFalse (FreshnessResolution.isResolved e.Outcome) "the gate is Unresolved"
                  Expect.equal (FreshnessResolution.candidate e) None "Unresolved ⇒ no candidate"
              | other -> failtestf "expected one entry, got %d" (List.length other)
          }

          testPropertyWithConfig fscheckConfig "choose candidate over a mixed report has exactly as many elements as there are Resolved entries (FR-004)"
          <| fun (gs: Gate list) (s: SensedFacts) ->
              let es = FreshnessResolution.entries (FreshnessResolution.resolve gs s)
              let cands = es |> List.choose FreshnessResolution.candidate
              let resolvedCount = es |> List.filter (fun e -> FreshnessResolution.isResolved e.Outcome) |> List.length
              List.length cands = resolvedCount

          testPropertyWithConfig fscheckConfig "every produced candidate is accepted by F041 ⇒ one verdict per candidate (no adaptation)"
          <| fun (gs: Gate list) (s: SensedFacts) ->
              let cands = FreshnessResolution.entries (FreshnessResolution.resolve gs s) |> List.choose FreshnessResolution.candidate
              let cacheEntries = CacheEligibility.entries (cacheReport cands EvidenceReuse.empty)
              List.length cacheEntries = List.length cands

          testPropertyWithConfig fscheckConfig "candidate is None for exactly the Unresolved entries"
          <| fun (gs: Gate list) (s: SensedFacts) ->
              FreshnessResolution.entries (FreshnessResolution.resolve gs s)
              |> List.forall (fun e ->
                  match e.Outcome with
                  | Resolved _ -> (FreshnessResolution.candidate e).IsSome
                  | Unresolved _ -> (FreshnessResolution.candidate e).IsNone) ]
