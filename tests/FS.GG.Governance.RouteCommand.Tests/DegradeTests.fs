module FS.GG.Governance.RouteCommand.Tests.DegradeTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.FreshnessSensing
open FS.GG.Governance.RouteCommand
open FS.GG.Governance.RouteCommand.Tests.Support

// US3 (honest degradation) for `fsgg route` (FR-010/FR-011, SC-006; L2/L3): a sense `Error` substitutes an
// empty `SensedFacts` (every gate ⇒ notEvaluated) and a malformed store substitutes `EvidenceReuse.empty`
// (every gate ⇒ mustRecompute noPriorEvidence). Neither fails the command, neither changes the exit code,
// neither hides a gate or fabricates `reusable`; each appends a NON-FATAL cache note naming the missing/
// malformed input distinctly from a fatal tool defect (Principle VI).

let private toSelected (git) (req: Loop.RunRequest) =
    let snap = snapshotOf git defaultOpts
    let m0, _ = Loop.init req
    let m1, _ = Loop.update (Loop.Sensed(Ok snap)) m0
    let m2, _ = Loop.update (Loop.Loaded(Valid(factsOf validCatalog))) m1
    snap, m2

let private driveToDone (m: Loop.Model) =
    // Two writes then Emitted (route writes gates + route).
    let m1, _ = Loop.update (Loop.Wrote(Loop.GatesArtifact, Ok())) m
    let m2, _ = Loop.update (Loop.Wrote(Loop.RouteArtifact, Ok())) m1
    let m3, _ = Loop.update Loop.Emitted m2
    m3

[<Tests>]
let tests =
    testList
        "Degrade"
        [ test "FreshnessSensed Error ⇒ empty SensedFacts (every gate notEvaluated) + cache note, NO fail, exit 0 (L2/L3)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let req = requestFor Loop.DefaultRange Loop.Text
              let _, m2 = toSelected git req
              Expect.isNonEmpty m2.SelectedGates "the src change selects gates"

              // Degrade: the sense fails; the store arrives Ok.
              let m3, _ = Loop.update (Loop.FreshnessSensed(Error "synthetic sense failure")) m2
              let m4, _ = Loop.update (Loop.StoreLoaded(Ok EvidenceReuse.empty)) m3
              Expect.equal m4.Phase Loop.Projected "the document still projects (no fail)"

              let routeDoc = Option.get m4.RouteDoc
              Expect.stringContains routeDoc "\"cacheEligibilityEvaluated\":true" "still an evaluated section"
              Expect.isFalse (routeDoc.Contains "\"kind\":\"reusable\"") "no gate is fabricated reusable"
              // Every selected gate is notEvaluated (unresolved ⇒ dropped from the report).
              for g in m2.SelectedGates do
                  Expect.stringContains routeDoc (gateIdValue g.Id) "the gate is still named in route.json"
              Expect.stringContains routeDoc "notEvaluated" "the affected gates render notEvaluated"

              // A non-fatal cache note names the MISSING input (unsensed facts), distinct from a defect (O1).
              Expect.isNonEmpty m4.CacheNotes "a non-fatal cache note is recorded"
              Expect.stringContains (String.concat " " m4.CacheNotes) "could not be sensed" "the note names the unsensed input"

              let mDone = driveToDone m4
              Expect.equal mDone.Exit Loop.Success "degrade never changes the exit code (route always 0)"
          }

          test "StoreLoaded Error ⇒ EvidenceReuse.empty (every gate mustRecompute noPriorEvidence) + cache note, exit 0 (L2/L4)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let req = requestFor Loop.DefaultRange Loop.Text
              let snap, m2 = toSelected git req
              let baseHead = baseHeadOfSnap (Some snap)
              let sensed = match FreshnessSensing.senseFreshness fakeSensor m2.SelectedGates baseHead with Ok s -> s | Error e -> failtestf "%s" e

              // Degrade: facts sense Ok; the store is malformed.
              let m3, _ = Loop.update (Loop.FreshnessSensed(Ok sensed)) m2
              let m4, _ = Loop.update (Loop.StoreLoaded(Error "synthetic malformed store")) m3
              Expect.equal m4.Phase Loop.Projected "the document still projects (no fail)"

              let routeDoc = Option.get m4.RouteDoc
              Expect.isFalse (routeDoc.Contains "\"kind\":\"reusable\"") "an unreadable store ⇒ never reusable (recompute by default)"
              Expect.stringContains routeDoc "noPriorEvidence" "every gate recompute-by-default with noPriorEvidence"

              Expect.isNonEmpty m4.CacheNotes "a non-fatal cache note is recorded"
              Expect.stringContains (String.concat " " m4.CacheNotes) "unreadable" "the note names the malformed store input"

              let mDone = driveToDone m4
              Expect.equal mDone.Exit Loop.Success "a malformed store never changes the exit code"
          }

          test "the interpreter degrades end-to-end over faked degrade ports — still writes, exit 0 (L2)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let req = requestFor Loop.DefaultRange Loop.Text
              let cap = newCapture ()
              // A malformed store reader (present-but-unreadable) drives the degrade through the real edge.
              let model = Interpreter.run (fakePortsWith validCatalog git fakeSensor malformedStoreReader cap req) req

              Expect.equal model.Exit Loop.Success "degraded store ⇒ still exits 0"
              Expect.isNonEmpty model.CacheNotes "a cache note surfaced"
              Expect.isSome (writtenOf cap Loop.RouteArtifact) "route.json still written under degrade"
          } ]
