# Quickstart / Validation: Dry-run / simulated governance gate

Per-user-story validation. Build once, then run the checks. All commands from repo root.

```sh
dotnet build FS.GG.Governance.sln -c Release
```

## US1 — Simulated verdict, no execution, no writes (P1)

**Parse (RED→GREEN)** — `tests/FS.GG.Governance.ShipCommand.Tests/ParseTests.fs`:
- `Loop.parse [ "ship"; "--repo"; d; "--dry-run" ]` ⇒ `Ok` with `req.DryRun = true`.
- `--dry-run` composes with `--since`/`--paths`/`--mode`/`--profile`/`--json`.

**No-writes + no-execution** (faked ports, `Support.fs` `Capture`):
- Run `Interpreter.run ports { req with DryRun = true }` on a repo that would normally fail a gate.
- Assert `cap.Writes = []` (SC-003) and that the fake `ExecutionPort` was **never** invoked
  (spy count 0) — every gate outcome is `NotExecuted`.
- Assert the printed summary shows a simulated verdict.

**Determinism** (SC-005): run twice on identical inputs ⇒ byte-identical stdout.

**Expected**: a verdict prints; the working tree is unchanged; no gate command ran.

## US2 — Handoff sufficiency, esp. against a surface bump (P2)

`tests/FS.GG.Governance.ReferenceGateSet.Tests` (drives `Loader.loadAndValidate
"samples/sdd-reference-gate-set"` → route → `Simulate.assemble`):
- **Required-absent**: feed a handoff that omits a signal a reference-set gate requires ⇒ that
  gate classifies `RequiredAbsent`, `Sufficiency.RequiredAbsentCount > 0`, and it is **not**
  presented as a clean Pass (SC-002 / FR-011).
- **Not-required vs satisfied**: a gate the profile doesn't require ⇒ `NotRequired`; a required
  signal the handoff carries ⇒ `RequiredSatisfied`.
- **Stricter profile**: same inputs under `--profile strict` surface at least the strict profile's
  additional requirements (SC-006).
- **All-absent**: a handoff carrying no required signal ⇒ `AllNotEvaluated = true`, surfaced
  explicitly.

**Expected**: the required-but-absent gaps are named; absence is never rendered as Pass.

## US3 — Machine-readable simulated document (P3)

`--dry-run --json`:
- `schemaVersion = "fsgg.audit.dryrun/v1"`; the string `fsgg.audit/v2` never appears (G1).
- `simulated: true` present (G2); `sufficiency` block present.
- Byte-identical across re-runs (G3).

**Real-audit invariant (G5 / regression)**: the existing ShipCommand byte-identical tests still
pass — `AuditJson.ofShipDecision` output is unchanged, proving dry-run added a *separate*
projection rather than altering the real `audit.json` contract.

## Edge cases

- **Malformed / version-mismatch handoff** ⇒ appears in `handoffDiagnostics`; no bare Pass (FR-008).
- **No handoff present** ⇒ explicit empty-input / nothing-to-evaluate signal, not an implicit Pass.
- **Unknown flag** (e.g. `--dry-runn`) ⇒ `UsageError`, writes nothing.

## Surface / gate checks

```sh
dotnet test tests/FS.GG.Governance.ShipCommand.Tests
dotnet test tests/FS.GG.Governance.ReferenceGateSet.Tests
```

- `SurfaceDriftTests` baselines updated for `RunRequest.DryRun` and the two new modules
  (`Simulate`, `SimulateProjection`) — the only intended surface deltas.
- api-compat / surface-drift gate green with the updated baselines; no other surface moved.
