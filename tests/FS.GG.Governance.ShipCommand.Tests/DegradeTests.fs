module FS.GG.Governance.ShipCommand.Tests.DegradeTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.FreshnessSensing
open FS.GG.Governance.ShipCommand
open FS.GG.Governance.ShipCommand.Tests.Support

// US3 (honest degradation) for `fsgg ship` (FR-009 ∧ FR-011, SC-006; L2/L3): a sense `Error` substitutes an
// empty `SensedFacts` (every gate ⇒ notEvaluated) and a malformed store substitutes `EvidenceReuse.empty`
// (every gate ⇒ mustRecompute noPriorEvidence). Neither fails the command nor changes the exit code; the
// ship verdict / partition / `ExitCodeBasis` are UNCHANGED under both degrade paths (degrade never perturbs
// the merge decision). Each appends a NON-FATAL cache note naming the missing/malformed input (Principle VI).

let private toSelected (git) (req: Loop.RunRequest) =
    let snap = snapshotOf git defaultOpts
    let m0, _ = Loop.init req
    let m1, _ = Loop.update (Loop.Sensed(Ok snap)) m0
    let m2, _ = Loop.update (Loop.Loaded(Valid(factsOf validCatalog))) m1
    snap, m2

let private driveToDone (m: Loop.Model) =
    let m1, _ = Loop.update (Loop.Wrote(Loop.AuditArtifact, Ok())) m
    let m2, _ = Loop.update Loop.Emitted m1
    m2

[<Tests>]
let tests =
    testList
        "Degrade"
        [ test "FreshnessSensed Error ⇒ notEvaluated gates + note; verdict/partition/basis/exit UNCHANGED (FR-009 ∧ FR-011)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let req = requestFor Loop.DefaultRange Loop.Text
              let _, m2 = toSelected git req
              let decision = Option.get m2.Decision
              Expect.equal decision.Verdict Fail "the base-blocking change fails (decided BEFORE the cache senses)"

              let m3, _ = Loop.update (Loop.FreshnessSensed(Error "synthetic sense failure")) m2
              let m4raw, e4 = Loop.update (Loop.StoreLoaded(Ok EvidenceReuse.empty)) m3
              let m4, _ = runExecuteEffect fakeExecPort m4raw e4
              Expect.equal m4.Phase Loop.Rolled "the document still projects (no fail)"

              let auditDoc = Option.get m4.AuditDoc
              Expect.isFalse (auditDoc.Contains "\"kind\":\"reusable\"") "no gate fabricated reusable"
              Expect.stringContains auditDoc "notEvaluated" "the affected gates render notEvaluated"
              Expect.isNonEmpty m4.CacheNotes "a non-fatal cache note is recorded"
              Expect.stringContains (String.concat " " m4.CacheNotes) "could not be sensed" "the note names the unsensed input (distinct from a defect)"

              // The merge decision is byte-unchanged: same verdict, same partition.
              Expect.equal (Option.get m4.Decision) decision "the ShipDecision is untouched by the degrade"
              let mDone = driveToDone m4
              Expect.equal mDone.Exit Loop.Blocked "exit follows the verdict (Blocked), unchanged by the degrade"
          }

          test "StoreLoaded Error ⇒ recompute-by-default + note; verdict/partition/basis/exit UNCHANGED (L2/L4)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let req = requestFor Loop.DefaultRange Loop.Text
              let snap, m2 = toSelected git req
              let decision = Option.get m2.Decision
              let baseHead = baseHeadOfSnap (Some snap)
              let sensed = match FreshnessSensing.senseFreshness fakeSensor m2.SelectedGates baseHead with Ok s -> s | Error e -> failtestf "%s" e

              let m3, _ = Loop.update (Loop.FreshnessSensed(Ok sensed)) m2
              let m4raw, e4 = Loop.update (Loop.StoreLoaded(Error "synthetic malformed store")) m3
              let m4, _ = runExecuteEffect fakeExecPort m4raw e4
              Expect.equal m4.Phase Loop.Rolled "the document still projects (no fail)"

              let auditDoc = Option.get m4.AuditDoc
              Expect.isFalse (auditDoc.Contains "\"kind\":\"reusable\"") "an unreadable store ⇒ never reusable"
              Expect.stringContains auditDoc "noPriorEvidence" "every gate recompute-by-default with noPriorEvidence"
              Expect.isNonEmpty m4.CacheNotes "a non-fatal cache note is recorded"
              Expect.stringContains (String.concat " " m4.CacheNotes) "unreadable" "the note names the malformed store input"

              Expect.equal (Option.get m4.Decision) decision "the ShipDecision is untouched by the degrade"
              let mDone = driveToDone m4
              Expect.equal mDone.Exit Loop.Blocked "exit follows the verdict (Blocked), unchanged by the degrade"
          }

          test "the interpreter degrades end-to-end over a malformed store — still writes, verdict/exit unchanged (L2)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let req = requestFor Loop.DefaultRange Loop.Text
              let cap = newCapture ()
              let model = Interpreter.run (fakePortsWith validCatalog git fakeSensor malformedStoreReader cap req) req

              Expect.equal model.Exit Loop.Blocked "degraded store ⇒ verdict-driven exit unchanged (Blocked)"
              Expect.isNonEmpty model.CacheNotes "a cache note surfaced"
              Expect.isSome (writtenAudit cap) "audit.json still written under degrade"
          } ]
