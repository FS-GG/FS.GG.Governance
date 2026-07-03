module FS.GG.Governance.ShipCommand.Tests.ExecutionTests

open System.IO
open Expecto
open FsCheck
open FsCheck.FSharp
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.ShipCommand
open FS.GG.Governance.ShipCommand.Tests.Support

// F052 US1/US2/US4 — the genuinely-new behavior: `fsgg ship` runs its selected command-gates through the
// injected F051 port, a PASSING gate is relocated out of the blockers and the verdict/exit reflect it, a
// FAILING gate stays a blocker, a REUSABLE gate is not spawned a second time, and totality is inherited from
// the F051 port (a missing executable is a recorded sentinel, never a throw). Driven through a deterministic
// fake port over an in-memory catalog and a REAL writable temp store (Principle V; no `Synthetic` outcome).

[<Tests>]
let tests =
    testList
        "Execution"
        [ test "US1: a selected blocking gate that EXITS 0 is relocated to Passing ⇒ verdict Pass / exit 0 (SC-002)" {
              // The src change selects block-on-ship gates that would normally BLOCK under gate/standard; with
              // a real passing run they relocate to Passing and the verdict flips to Pass.
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let req = requestFor Loop.DefaultRange Loop.Text
              let cap = newCapture ()
              let model = Interpreter.run (fakePortsExec validCatalog git fakeSensor absentStoreReader fakeExecPortPass cap req) req

              Expect.equal (Option.get model.Decision).Verdict Pass "every selected gate passed ⇒ Pass"
              Expect.isEmpty (Option.get model.Decision).Blockers "the passing gates are relocated out of Blockers"
              Expect.equal model.Exit Loop.Success "a clean run exits 0"
              let audit = writtenAudit cap |> Option.map snd |> Option.defaultValue ""
              Expect.stringContains audit "\"disposition\":\"executed\"" "the gates are reported executed"
              Expect.stringContains audit "\"passed\":true" "with a passing outcome"
          }

          test "US1: a selected blocking gate that EXITS non-zero stays a Blocker ⇒ verdict Fail / exit 1 (SC-001)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let req = requestFor Loop.DefaultRange Loop.Text
              let cap = newCapture ()
              // The default fake exec port exits 1 (fail).
              let model = Interpreter.run (fakePortsExec validCatalog git fakeSensor absentStoreReader fakeExecPort cap req) req

              Expect.equal (Option.get model.Decision).Verdict Fail "a failing blocking gate ⇒ Fail"
              Expect.isNonEmpty (Option.get model.Decision).Blockers "the failing gate stays a Blocker"
              Expect.equal model.Exit Loop.Blocked "a blocked verdict exits 1"
              let audit = writtenAudit cap |> Option.map snd |> Option.defaultValue ""
              Expect.stringContains audit "\"passed\":false" "the failing outcome is reported"
          }

          test "US2: a reusable gate is NOT spawned a second time (the cache payoff, SC-003)" {
              withTempRepo (fun dir ->
                  let counter = { Calls = 0 }
                  let port = countingExecPort counter 0 // exit 0 (pass)
                  let req =
                      match Loop.parse [ "ship"; "--repo"; dir; "--since"; "HEAD~1"; "--persist-store" ] with
                      | Ok r -> r
                      | Error e -> failtestf "parse failed: %A" e

                  let ports = { Interpreter.realPorts req.Repo with Execute = port }

                  // Run 1 (empty store): the selected command-gates are executed once each and captured.
                  Interpreter.run ports req |> ignore
                  let afterRun1 = counter.Calls
                  Expect.isGreaterThan afterRun1 0 "run 1 executes the selected command-gates"

                  // Run 2 (same world, store from run 1): the gates are REUSABLE ⇒ NOT spawned again.
                  let model2 = Interpreter.run ports req
                  Expect.equal counter.Calls afterRun1 "run 2 spawns NO new process (the gates are reused)"

                  let audit2 = File.ReadAllText req.AuditOut
                  Expect.stringContains audit2 "\"disposition\":\"reused\"" "run 2 reports the gates as reused"
                  // The reused outcome contributes to the verdict on identical terms (exit 0 ⇒ pass ⇒ relocated).
                  Expect.equal (Option.get model2.Decision).Verdict Pass "the reused passing outcome drives the verdict")
          }

          test "US4: a MISSING executable is the F051 sentinel outcome (treated as a failed gate), never a throw" {
              // A real gate whose command does not exist: the merged F051 realPort records `startFailureExitCode`
              // (127) and the gate fails — no exception, no hang. Reaches the REAL port at the edge (D8).
              withTempRepo (fun dir ->
                  // Rewrite tooling so the selected gates' command is a missing executable.
                  let missingTooling =
                      """schemaVersion: 1
commands:
  - id: dotnet-format
    command: "fsgg-no-such-tool-xyz"
    timeout: 30
    environment: local-or-ci
  - id: dotnet-build
    command: "fsgg-no-such-tool-xyz"
    timeout: 30
    environment: local-or-ci
  - id: dotnet-audit
    command: "fsgg-no-such-tool-xyz"
    timeout: 30
    environment: local-or-ci
environmentClasses:
  - local
  - ci
"""
                  writeFile dir ".fsgg/tooling.yml" missingTooling

                  let req =
                      match Loop.parse [ "ship"; "--repo"; dir; "--since"; "HEAD~1" ] with
                      | Ok r -> r
                      | Error e -> failtestf "parse failed: %A" e

                  // The REAL execution port (no override) — exercises the genuine process-spawn edge.
                  let model = Interpreter.run (Interpreter.realPorts req.Repo) req

                  Expect.equal (Option.get model.Decision).Verdict Fail "a missing-executable gate fails the verdict"
                  Expect.equal model.Exit Loop.Blocked "and the run is blocked (exit 1), no crash"
                  let audit = File.ReadAllText req.AuditOut
                  Expect.stringContains audit "\"passed\":false" "the sentinel outcome is recorded as a failed gate"
                  Expect.stringContains audit "127" "the start-failure sentinel exit code is surfaced")
          } ]

// ── applyExecution invariants (D3) — the pure verdict relocation, no I/O ──

[<Tests>]
let applyExecutionTests =
    let decisionFixture () =
        let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
        let candidates = candidatesOf git defaultOpts
        let _, decision = resultAndDecisionOf validCatalog candidates Gate Standard
        let gateIds =
            (decision.Blockers @ decision.Warnings @ decision.Passing)
            |> List.choose (fun i ->
                match i.Id with
                | GateItem g -> Some g
                | FindingItem _ -> None)
        decision, gateIds

    testList
        "ApplyExecution"
        [ test "relocating ALL passing gates clears the blockers ⇒ Pass / Clean" {
              let decision, gateIds = decisionFixture ()
              Expect.equal decision.Verdict Fail "the fixture is a fail (base-blocking gates)"
              Expect.isNonEmpty decision.Blockers "with blockers"

              let relocated = Loop.applyExecution (Set.ofList gateIds) decision
              Expect.isEmpty relocated.Blockers "all passing gates relocated out of Blockers"
              Expect.equal relocated.Verdict Pass "no blocker remains ⇒ Pass"
              Expect.equal relocated.ExitCodeBasis Clean "⇒ Clean basis"
          }

          test "relocating NO gates is the identity (a failing gate keeps its rollup treatment, FR-005/FR-006)" {
              let decision, _ = decisionFixture ()
              let relocated = Loop.applyExecution Set.empty decision
              Expect.equal relocated decision "an empty passing set leaves the decision unchanged"
          }

          test "relocation never MOVES a finding and never CREATES a blocker (FR-006)" {
              let decision, gateIds = decisionFixture ()

              let isFinding (i: EnforcedItem) =
                  match i.Id with
                  | FindingItem _ -> true
                  | GateItem _ -> false

              let findingsBefore = (decision.Blockers @ decision.Warnings) |> List.filter isFinding

              // Relocate an arbitrary subset of the passing gate ids.
              let relocated = Loop.applyExecution (Set.ofList gateIds) decision

              // Blockers can only SHRINK (a passing gate cannot create a blocker).
              Expect.isLessThanOrEqual (List.length relocated.Blockers) (List.length decision.Blockers) "blockers only shrink"
              // Findings that were blockers/warnings are untouched (never relocated).
              let findingsAfter = (relocated.Blockers @ relocated.Warnings) |> List.filter isFinding
              Expect.equal findingsAfter findingsBefore "findings are never relocated"
          }

          testProperty "applyExecution can only CLEAR blockers, never add one (FsCheck over arbitrary id subsets)"
          <| fun (pick: bool list) ->
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let candidates = candidatesOf git defaultOpts
              let _, decision = resultAndDecisionOf validCatalog candidates Gate Standard

              let gateIds =
                  decision.Blockers
                  |> List.choose (fun i ->
                      match i.Id with
                      | GateItem g -> Some g
                      | FindingItem _ -> None)

              // Choose an arbitrary subset of the blocking gate ids from the FsCheck bool list.
              let chosen =
                  gateIds
                  |> List.mapi (fun idx g -> idx, g)
                  |> List.filter (fun (idx, _) -> idx < List.length pick && pick.[idx])
                  |> List.map snd
                  |> Set.ofList

              let relocated = Loop.applyExecution chosen decision
              // The relocated blocker set is a SUBSET of the original (never grows).
              List.length relocated.Blockers <= List.length decision.Blockers ]
