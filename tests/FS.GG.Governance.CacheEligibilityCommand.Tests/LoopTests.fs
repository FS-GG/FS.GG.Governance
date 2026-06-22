module FS.GG.Governance.CacheEligibilityCommand.Tests.LoopTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.FreshnessResolution
open FS.GG.Governance.CacheEligibility
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.CacheEligibilityJson
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.CacheEligibilityCommand
open FS.GG.Governance.CacheEligibilityCommand.Tests.Support

// T011 (US1, SC-001/SC-002) — the pure init/update pipeline tail: emitted effects + the computed cache
// document = a genuine CacheEligibilityJson.ofReport (L6), in GateId order, with the right verdicts.

let private gateA = mkGate "build" "format" Cheap LocalOrCi (Some(CommandId "dotnet-format"))
let private gateB = mkGate "build" "tests" Medium LocalOrCi (Some(CommandId "dotnet-build"))
let private req = requestFor Loop.DefaultRange Loop.Human

[<Tests>]
let tests =
    testList
        "Loop"
        [ test "init: Since/DefaultRange emit SenseScope; ExplicitPaths emit LoadCatalog (no git)" {
              let _, defEff = Loop.init (requestFor Loop.DefaultRange Loop.Human)
              Expect.equal defEff [ Loop.SenseScope Loop.DefaultRange ] "DefaultRange senses scope first"

              let _, expEff = Loop.init (requestFor (Loop.ExplicitPaths [ gp "src/A.fs" ]) Loop.Human)
              Expect.equal expEff [ Loop.LoadCatalog "." ] "ExplicitPaths loads the catalog directly"
          }

          test "selected gates → SenseFreshness + LoadStore are emitted together" {
              // From the Selected phase, the first message that completes BOTH sensed+store triggers writes;
              // the selection itself emits SenseFreshness + LoadStore (verified via the catalog path in
              // InterpreterTests). Here we drive the projection tail directly.
              let model = selectedModel [ gateA; gateB ] req
              let sensed = fullSensed [ gateA; gateB ]
              let store = EvidenceReuse.empty
              let m1, e1 = Loop.update (Loop.FreshnessSensed(Ok sensed)) model
              Expect.isEmpty e1 "freshness alone does not write (store not yet loaded)"
              let _, e2 = Loop.update (Loop.StoreLoaded(Ok store)) m1

              match e2 with
              | [ Loop.WriteArtifact(Loop.CacheArtifact, p1, _); Loop.WriteArtifact(Loop.UnresolvedArtifact, p2, _) ] ->
                  Expect.equal p1 req.CacheOut "cache written to CacheOut"
                  Expect.equal p2 req.UnresolvedOut "sidecar written to UnresolvedOut"
              | other -> failtestf "expected two WriteArtifact effects, got %A" other
          }

          test "empty store ⇒ every resolved gate mustRecompute NoPriorEvidence; cache doc = ofReport (L6/L7)" {
              let gates = [ gateA; gateB ]
              let sensed = fullSensed gates
              let store = EvidenceReuse.empty
              let _, effs = driveProjection (selectedModel gates req) sensed store

              let written =
                  effs
                  |> List.tryPick (function
                      | Loop.WriteArtifact(Loop.CacheArtifact, _, c) -> Some c
                      | _ -> None)

              Expect.equal written (Some(expectedCacheDoc gates sensed store)) "cache doc = genuine ofReport"

              // Verify the verdicts are NoPriorEvidence for both gates.
              let report = FreshnessResolution.resolve gates sensed
              let cands = FreshnessResolution.entries report |> List.choose FreshnessResolution.candidate
              let verdicts = CacheEligibility.evaluate cands store |> CacheEligibility.entries

              for e in verdicts do
                  Expect.equal (CacheEligibility.recomputeCause e.Verdict) (Some NoPriorEvidence) "noPriorEvidence with empty store"
          }

          test "a matching store ⇒ that gate reusable with its evidence ref (US1 AS1, SC-002)" {
              let gates = [ gateA; gateB ]
              let sensed = fullSensed gates
              let store = storeMakingReusable [ gateA ] sensed (fun _ -> "ev-A")
              let _, effs = driveProjection (selectedModel gates req) sensed store

              let written =
                  effs
                  |> List.pick (function
                      | Loop.WriteArtifact(Loop.CacheArtifact, _, c) -> Some c
                      | _ -> None)

              Expect.equal written (expectedCacheDoc gates sensed store) "cache doc = genuine ofReport"

              let report = FreshnessResolution.resolve gates sensed
              let cands = FreshnessResolution.entries report |> List.choose FreshnessResolution.candidate
              let verdicts = CacheEligibility.evaluate cands store |> CacheEligibility.entries

              let aVerdict = verdicts |> List.find (fun e -> e.Gate = gateA.Id)
              Expect.equal (CacheEligibility.reusableEvidence aVerdict.Verdict) (Some(EvidenceRef "ev-A")) "gate A reusable with ev-A"
              let bVerdict = verdicts |> List.find (fun e -> e.Gate = gateB.Id)
              Expect.isFalse (CacheEligibility.isReusable bVerdict.Verdict) "gate B (no matching entry) must recompute"
          }

          test "entries appear in GateId order regardless of selection order (SC-001)" {
              let gates = [ gateB; gateA ] // supplied out of order
              let sensed = fullSensed gates
              let _, effs = driveProjection (selectedModel gates req) sensed EvidenceReuse.empty

              let cacheDoc =
                  effs
                  |> List.pick (function
                      | Loop.WriteArtifact(Loop.CacheArtifact, _, c) -> Some c
                      | _ -> None)
              // build:format sorts before build:tests ordinally
              let idxFormat = cacheDoc.IndexOf "build:format"
              let idxTests = cacheDoc.IndexOf "build:tests"
              Expect.isLessThan idxFormat idxTests "entries in GateId ordinal order"
          }

          test "empty selection ⇒ a valid empty-entry cache doc, no unresolved (US1 AS3)" {
              let sensed = fullSensed []
              let _, effs = driveProjection (selectedModel [] req) sensed EvidenceReuse.empty

              let cacheDoc =
                  effs
                  |> List.pick (function
                      | Loop.WriteArtifact(Loop.CacheArtifact, _, c) -> Some c
                      | _ -> None)

              Expect.equal cacheDoc (CacheEligibilityJson.ofReport (CacheEligibility.evaluate [] EvidenceReuse.empty)) "empty report projection"
          } ]
