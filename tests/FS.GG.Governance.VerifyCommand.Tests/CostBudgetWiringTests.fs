module FS.GG.Governance.VerifyCommand.Tests.CostBudgetWiringTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.GateRun.Model
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.CostBudget.Model
open FS.GG.Governance.CommandKind.Model
open FS.GG.Governance.GateRun
open FS.GG.Governance.VerifyCommand
open FS.GG.Governance.VerifyCommand.Tests.Support

// F25 host wiring (064) — the budget filter, the two deterministic sidecars, kinded-run recording, and the
// byte-identity anchor, exercised end-to-end through the REAL `Interpreter.run` over the REAL F25 cores (only
// the edge ports are faked). verify is fixed at `RunMode.Verify` (High ceiling); `--profile Light` floors the
// budget to `Cheap`, deferring an expensive must-recompute gate.

let private srcScope = Loop.DefaultRange

let private runProfile (g) (profile: Profile) =
    let cap = newCapture ()
    let req = { requestForProfile Loop.DefaultRange Loop.Json profile with Repo = "." }
    let model = Interpreter.run (fakePortsExec validCatalog g fakeSensor absentStoreReader fakeExecPortPass cap) req
    model, cap

let private outcomeFor (model: Loop.Model) (gid: string) : GateOutcome option =
    model.Outcomes |> List.tryPick (fun (g, o) -> if gateIdValue g = gid then Some o else None)

[<Tests>]
let tests =
    testList
        "CostBudgetWiring (064)"
        [
          // ── Phase 2 (T008): the byte-identity safety anchor, proven up front ──
          test "fixture-budget invariant: every frozen-golden must-recompute gate fits the Medium ceiling" {
              // budgetFor Standard Verify = Medium. The frozen verify.json / route goldens are all backed by the
              // src-change selection (format(cheap) + build(medium)); over the empty store every selected gate is
              // must-recompute, so the default budget must defer NOTHING there — else an existing golden would
              // change and that would be a real behavioral change to escalate, not re-bless (research D1, SC-004).
              // (The work-change selection brings in audit(High), which the default Medium budget DOES defer —
              // correct new behavior, exercised by the deferral tests below; it backs no frozen golden.)
              let gates = selectedGatesFor validCatalog (candidatesOf gitSrcChange defaultOpts)
              let deferred = budgetDeferredIds gates Verify Standard

              for gate in gates do
                  if Set.contains (gateIdValue gate.Id) deferred then
                      failtestf
                          "fixture-budget invariant broken: gate %s (cost %A) is deferred under the default Medium ceiling — escalate as a real behavioral change, do not re-bless"
                          (gateIdValue gate.Id)
                          gate.Cost
          }

          // ── Phase 3 (US1, T010/T011): tight-budget deferral is observable, named, and never passed ──
          test "verify --profile Light defers the over-budget medium gate; the cheap gate still runs" {
              // src change selects format(cheap) + build(medium). Light ⇒ Cheap ceiling: format fits and runs,
              // build is over-budget and is deferred — not executed, not passed (SC-002).
              let model, cap = runProfile gitSrcChange Light

              match outcomeFor model "package-api:format" with
              | Some o ->
                  Expect.isTrue
                      (match o.Disposition with
                       | Executed _ -> true
                       | _ -> false)
                      "the cheap in-budget gate runs"
              | None -> failtest "expected a format outcome"

              match outcomeFor model "package-api:build" with
              | Some o ->
                  Expect.equal o.Disposition NotExecuted "the over-budget medium gate is deferred, not executed"
                  Expect.isFalse (isPassing o.Disposition) "a deferred gate is NEVER reported as passed (SC-002)"
              | None -> failtest "expected a build outcome (recorded, not dropped)"

              // The deferral is named in cost-budget.json with the gate and the overBudget decision.
              match writtenCostBudget cap with
              | Some doc ->
                  Expect.stringContains doc "fsgg.cost-budget/v1" "the cost-budget sidecar carries its schema id"
                  Expect.stringContains doc "overBudget" "the deferred gate is recorded overBudget (named, never silent)"
                  Expect.stringContains doc "package-api:build" "the over-budget gate is named"
              | None -> failtest "expected a cost-budget.json sidecar write"
          }

          test "verify --profile Light defers the High-cost workflow audit gate" {
              let model, cap = runProfile gitWorkChange Light

              match outcomeFor model "workflow:audit" with
              | Some o ->
                  Expect.equal o.Disposition NotExecuted "the High-cost audit gate is deferred under Light"
                  Expect.isFalse (isPassing o.Disposition) "deferred ⇒ never passed"
              | None -> failtest "expected an audit outcome"

              match writtenCostBudget cap with
              | Some doc -> Expect.stringContains doc "workflow:audit" "the deferred audit gate is named in the sidecar"
              | None -> failtest "expected a cost-budget.json sidecar"
          }

          // ── Phase 4 (US2, T015): kinded-run recording — kindOf total; identity is duration-invariant ──
          test "kindOf is total over the selected gates and runIdentity ignores sensed duration (FR-004)" {
              let gates = selectedGatesFor validCatalog (candidatesOf gitSrcChange defaultOpts)
              // Total: kindOf returns a kind for every gate (no throw, no silent drop).
              for gate in gates do
                  let k = Loop.kindOf gate
                  Expect.isTrue (List.contains k [ Build; Test; Pack; TemplateInstantiation; GitDiff; PackageInspection; VisualCapture ]) "kindOf yields a closed-taxonomy kind"

              // Two runs of one gate differing ONLY in sensed duration share a run identity.
              let gate = List.head gates
              let tooling = (factsOf validCatalog).Tooling
              let cmd =
                  tooling
                  |> Option.map (fun t -> Plan.commandFor "." t gate)
                  |> function
                      | Some(Ok c) -> c
                      | other -> failtestf "expected a resolvable command for the gate, got %A" other
              let baseRecord = FS.GG.Governance.GateExecution.Interpreter.senseExecution (fakeExecPortExiting 0) cmd
              let run1 = { Kind = Loop.kindOf gate; Record = baseRecord }
              let run2 = { Kind = Loop.kindOf gate; Record = { baseRecord with Duration = SensedDuration 999L } }
              Expect.equal (FS.GG.Governance.CommandKind.Audit.runIdentity run1) (FS.GG.Governance.CommandKind.Audit.runIdentity run2) "duration never participates in the run identity"
          }

          // ── Phase 4 (US2, T017): both sidecars deterministic across re-runs; no clock/abs-path/user leakage ──
          test "both sidecars are byte-identical across two runs over an unchanged tree" {
              let _, a = runProfile gitSrcChange Strict
              let _, b = runProfile gitSrcChange Strict

              Expect.equal (writtenCostBudget a) (writtenCostBudget b) "cost-budget.json byte-identical across runs"
              Expect.equal (writtenProvenance a) (writtenProvenance b) "provenance.json byte-identical across runs"

              match writtenProvenance a with
              | Some doc ->
                  Expect.stringContains doc "fsgg.provenance/v1" "provenance carries its schema id"
                  Expect.isFalse (doc.Contains "/home/") "no absolute path leaks into provenance.json"
              | None -> failtest "expected a provenance.json sidecar"
          }

          // ── Phase 4 (US2, T018): the existing verify.json stays byte-identical; sidecars are additive ──
          test "verify.json is byte-identical with wiring active; the two sidecars are written beside it" {
              let cap = newCapture ()
              let req = requestForProfile srcScope Loop.Json Strict
              let snap = Some(snapshotOf gitSrcChange defaultOpts)
              Interpreter.run (fakePortsExec validCatalog gitSrcChange fakeSensor absentStoreReader fakeExecPortFail cap) req |> ignore

              let candidates = candidatesOf gitSrcChange defaultOpts
              match writtenVerify cap with
              | Some(_, doc) -> Expect.equal doc (verifyExpected validCatalog candidates Strict snap) "verify.json bytes unchanged (sidecars never fold in)"
              | None -> failtest "expected a verify.json write"

              Expect.isSome (writtenCostBudget cap) "cost-budget.json written beside verify.json"
              Expect.isSome (writtenProvenance cap) "provenance.json written beside verify.json"
          }

          // ── Phase 4 (US2, T018): the empty selection still produces well-formed empty-array sidecars ──
          test "nothing-to-verify still writes well-formed empty sidecars" {
              let cap = newCapture ()
              let req = requestFor Loop.DefaultRange Loop.Json
              Interpreter.run (fakePortsExec emptyCatalog gitEmpty fakeSensor absentStoreReader fakeExecPortPass cap) req |> ignore

              match writtenCostBudget cap with
              | Some doc ->
                  Expect.stringContains doc "fsgg.cost-budget/v1" "empty cost-budget sidecar still carries its schema"
                  Expect.stringContains doc "\"decisions\":[]" "no decisions ⇒ empty array"
              | None -> failtest "expected an (empty) cost-budget.json"

              match writtenProvenance cap with
              | Some doc -> Expect.stringContains doc "fsgg.provenance/v1" "empty provenance sidecar still carries its schema"
              | None -> failtest "expected an (empty) provenance.json"
          }

          // ── Phase 4 (US2, T019): cost/cache findings are advisory and live only in the sidecar ──
          test "cost/cache findings enforce as Advisory and never enter verify.json (FR-008)" {
              let model, cap = runProfile gitSrcChange Light
              let report = Option.get model.CacheDecision
              let findings = FS.GG.Governance.CostBudget.Findings.cacheFindings report (fun _ -> FS.GG.Governance.CostBudget.Findings.Real)

              for f in findings do
                  let decision = FS.GG.Governance.CostBudget.Findings.enforce Verify Light f
                  Expect.equal decision.EffectiveSeverity Advisory "a cost/cache finding is advisory — never a blocker"

              // The findings live in cost-budget.json, not verify.json.
              match writtenVerify cap, writtenCostBudget cap with
              | Some(_, vdoc), Some _ -> Expect.isFalse (vdoc.Contains "syntheticTaint") "findings never leak into verify.json"
              | _ -> failtest "expected both verify.json and cost-budget.json"
          }

          // ── Phase 5 (US3, T025): missing/unreadable store ⇒ safe failure, well-formed sidecars, no fake reuse ──
          test "an unreadable store still writes well-formed sidecars with no fabricated reuse" {
              let cap = newCapture ()
              let req = requestForProfile Loop.DefaultRange Loop.Json Standard
              // SYNTHETIC: a malformed store reader (disclosed) — the degrade-to-empty probe; product-local only.
              let model = Interpreter.run (fakePortsExec validCatalog gitSrcChange fakeSensor malformedStoreReader fakeExecPortPass cap) req

              Expect.isTrue model.StoreDegraded "an unreadable store degrades (input, not tool defect)"

              match writtenCostBudget cap with
              | Some doc ->
                  Expect.stringContains doc "fsgg.cost-budget/v1" "cost-budget sidecar well-formed under degrade"
                  Expect.isFalse (doc.Contains "\"reuse\"") "no gate is fabricated as reusable under a missing store"
              | None -> failtest "expected a cost-budget.json under degrade"

              Expect.isSome (writtenProvenance cap) "provenance sidecar still written under degrade"
          } ]
