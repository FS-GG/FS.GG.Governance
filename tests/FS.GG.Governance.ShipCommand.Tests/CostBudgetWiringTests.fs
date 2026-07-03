module FS.GG.Governance.ShipCommand.Tests.CostBudgetWiringTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.GateRun
open FS.GG.Governance.GateRun.Model
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.CostBudget.Model
open FS.GG.Governance.CommandKind.Model
open FS.GG.Governance.ShipCommand
open FS.GG.Governance.ShipCommand.Tests.Support

// F25 host wiring (064) — the budget filter, the two deterministic sidecars, kinded-run recording, and the
// byte-identity anchor, exercised end-to-end through the REAL `Interpreter.run` over the REAL F25 cores (only
// the edge ports are faked). ship threads (request.Profile, request.Mode); `--mode inner` floors the budget to
// `Cheap` (the merge-boundary deferral probe).

let private runLevers (g) (mode: RunMode) (profile: Profile) =
    let cap = newCapture ()
    let req = requestForLevers Loop.DefaultRange Loop.Json mode profile
    let model = Interpreter.run (fakePortsExec validCatalog g fakeSensor absentStoreReader fakeExecPortPass cap req) req
    model, cap

let private outcomeFor (model: Loop.Model) (gid: string) : GateOutcome option =
    model.Outcomes |> List.tryPick (fun (g, o) -> if gateIdValue g = gid then Some o else None)

[<Tests>]
let tests =
    testList
        "CostBudgetWiring (064)"
        [
          // ── Phase 2 (T009): the byte-identity safety anchor, proven up front ──
          test "fixture-budget invariant: every frozen-golden must-recompute gate fits the Medium ceiling" {
              // budgetFor Standard Gate = Medium. The frozen audit.json / ship goldens are all backed by the
              // src-change selection (format(cheap) + build(medium)); over the empty store every selected gate is
              // must-recompute, so the default budget must defer NOTHING there — else an existing golden would
              // change (research D1, SC-004). The work-change selection's audit(High) IS deferred under the
              // default budget (correct new behavior, exercised below); it backs no frozen golden.
              let gates = selectedGatesFor validCatalog (candidatesOf gitSrcChange defaultOpts)
              let deferred = budgetDeferredIds gates Gate Standard

              for gate in gates do
                  if Set.contains (gateIdValue gate.Id) deferred then
                      failtestf
                          "fixture-budget invariant broken: gate %s (cost %A) is deferred under the default Medium ceiling — escalate as a real behavioral change, do not re-bless"
                          (gateIdValue gate.Id)
                          gate.Cost
          }

          // ── Phase 3 (US1, T011): tight-budget deferral at the (profile, mode) merge boundary ──
          test "ship --mode inner defers the over-budget medium gate; the cheap gate still runs" {
              // src change selects format(cheap) + build(medium). Inner ⇒ Cheap ceiling: format fits and runs,
              // build is over-budget and is deferred (Skipped) — not executed, not passed (SC-002).
              let model, cap = runLevers gitSrcChange Inner Standard

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

              match writtenCostBudget cap with
              | Some doc ->
                  Expect.stringContains doc "fsgg.cost-budget/v1" "the cost-budget sidecar carries its schema id"
                  Expect.stringContains doc "overBudget" "the deferred gate is recorded overBudget (named, never silent)"
                  Expect.stringContains doc "package-api:build" "the over-budget gate is named"
              | None -> failtest "expected a cost-budget.json sidecar write"
          }

          // ── Phase 4 (US2, T015): kinded-run recording — kindOf total; identity is duration-invariant ──
          test "kindOf is total over the selected gates and runIdentity ignores sensed duration (FR-004)" {
              let gates = selectedGatesFor validCatalog (candidatesOf gitSrcChange defaultOpts)

              for gate in gates do
                  let k = Loop.kindOf gate
                  Expect.isTrue (List.contains k [ Build; Test; Pack; TemplateInstantiation; GitDiff; PackageInspection; VisualCapture ]) "kindOf yields a closed-taxonomy kind"

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
              let _, a = runLevers gitSrcChange Gate Strict
              let _, b = runLevers gitSrcChange Gate Strict

              Expect.equal (writtenCostBudget a) (writtenCostBudget b) "cost-budget.json byte-identical across runs"
              Expect.equal (writtenProvenance a) (writtenProvenance b) "provenance.json byte-identical across runs"

              match writtenProvenance a with
              | Some doc ->
                  Expect.stringContains doc "fsgg.provenance/v1" "provenance carries its schema id"
                  Expect.isFalse (doc.Contains "/home/") "no absolute path leaks into provenance.json"
              | None -> failtest "expected a provenance.json sidecar"
          }

          // ── Phase 4 (US2, T018): the existing audit.json stays byte-identical; sidecars are additive ──
          test "audit.json is byte-identical with wiring active; the two sidecars are written beside it" {
              let cap = newCapture ()
              let req = requestForLevers Loop.DefaultRange Loop.Json Gate Standard
              let snap = Some(snapshotOf gitSrcChange defaultOpts)
              Interpreter.run (fakePortsExec validCatalog gitSrcChange fakeSensor absentStoreReader fakeExecPort cap req) req |> ignore

              let candidates = candidatesOf gitSrcChange defaultOpts
              match writtenAudit cap with
              | Some(_, doc) -> Expect.equal doc (auditExpected validCatalog candidates Gate Standard snap) "audit.json bytes unchanged (sidecars never fold in)"
              | None -> failtest "expected an audit.json write"

              Expect.isSome (writtenCostBudget cap) "cost-budget.json written beside audit.json"
              Expect.isSome (writtenProvenance cap) "provenance.json written beside audit.json"
          }

          // ── Phase 4 (US2, T019): cost/cache findings are advisory and live only in the sidecar ──
          test "cost/cache findings enforce as Advisory and never enter audit.json (FR-008)" {
              let model, cap = runLevers gitSrcChange Inner Standard
              let report = Option.get model.CacheDecision
              let findings = FS.GG.Governance.CostBudget.Findings.cacheFindings report (fun _ -> FS.GG.Governance.CostBudget.Findings.Real)

              for f in findings do
                  let decision = FS.GG.Governance.CostBudget.Findings.enforce Gate Standard f
                  Expect.equal decision.EffectiveSeverity Advisory "a cost/cache finding is advisory — never a blocker"

              match writtenAudit cap, writtenCostBudget cap with
              | Some(_, adoc), Some _ -> Expect.isFalse (adoc.Contains "syntheticTaint") "findings never leak into audit.json"
              | _ -> failtest "expected both audit.json and cost-budget.json"
          }

          // ── Phase 5 (US3, T025): missing/unreadable store ⇒ safe failure, well-formed sidecars, no fake reuse ──
          test "an unreadable store still writes well-formed sidecars with no fabricated reuse" {
              let cap = newCapture ()
              let req = requestForLevers Loop.DefaultRange Loop.Json Gate Standard
              // SYNTHETIC: a malformed store reader (disclosed) — the degrade-to-empty probe; product-local only.
              let model = Interpreter.run (fakePortsExec validCatalog gitSrcChange fakeSensor malformedStoreReader fakeExecPort cap req) req

              Expect.isTrue model.StoreDegraded "an unreadable store degrades (input, not tool defect)"

              match writtenCostBudget cap with
              | Some doc ->
                  Expect.stringContains doc "fsgg.cost-budget/v1" "cost-budget sidecar well-formed under degrade"
                  Expect.isFalse (doc.Contains "\"reuse\"") "no gate is fabricated as reusable under a missing store"
              | None -> failtest "expected a cost-budget.json under degrade"

              Expect.isSome (writtenProvenance cap) "provenance sidecar still written under degrade"
          } ]
