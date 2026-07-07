# Phase 0 Research: Dry-run / simulated governance gate

All decisions resolved against ground-truth signatures read from the repo. No open
NEEDS CLARIFICATION remains.

## R1 — Integration seam: where does the `--dry-run` lever live?

**Decision**: A boolean flag `--dry-run` on the existing **`fsgg ship`** command
(`ShipCommand.Loop`).

**Rationale**: Ship already owns the exact pipeline a dry-run previews —
`route → Ship.rollup → AuditJson/HumanText projections`. The cost is minimal and local:
- one `bool` field on the hidden `ParseAcc` + `emptyAcc`,
- one `bool` field on `RunRequest` (the only `.fsi` surface move),
- one flag arm `| "--dry-run" :: more -> go { acc with DryRun = true } more` (a boolean flag,
  recognized inside `parse` so a typo writes no artifact — the repo convention),
- one branch in the pure `update` that withholds the write/persist effects and routes to a
  simulated evaluation.

**Alternatives considered**:
- *New `Cli` subcommand* — rejected: the `fsgg-governance` `Cli` exe has **no ship path** and a
  different `ExitDecision`/`CommandPayload` vocabulary; adding ship semantics there is a poor fit.
- *New dedicated Exe* — rejected: heaviest option (new `.fsproj`, `Program.fs`, duplicated port
  wiring and baselines) for what is a mode of an existing command.

## R2 — The "no runtime / no execution" model

**Decision**: In dry-run, the pure `update` does **not** emit the `ExecuteGates` effect; every
selected gate is assigned `GateDisposition.NotExecuted`. It also withholds `WriteArtifact` and
`PersistStore`.

**Rationale**: The `ExecutionPort` (`GateCommand -> ExecutionOutcome`) is the *sole* process seam;
not emitting `ExecuteGates` means no `dotnet test`/tool is ever spawned — that is precisely the
"works without the full runtime installed" property. `GateRun.Model.GateDisposition.NotExecuted`
already models "a selected gate that did not run", and `AuditJson`/`HumanText` already project it,
so the reused projections need no change. Suppressing writes is likewise a pure `update` decision
(withhold the effect), so `Interpreter.Ports` is untouched — no fake-port surface grows.

**Alternatives considered**:
- *Inject a stub `ExecutionPort` that returns exit 0* — rejected: it would fabricate a *passing
  execution*, which is a lie (the gate did not run); `NotExecuted` is the truthful state and keeps
  absence visible (FR-011).

## R3 — Handoff-sufficiency semantics (US2)

**Decision**: A pure classifier `Simulate.classify` maps the **selected gates** (from routing) and
the **consumed handoff** (`SddHandoff.Consumer.consume`, giving `Gates`/`Selected`/`Diagnostics`)
into three buckets per gate:
- **required-satisfied** — the policy required a signal the handoff carries,
- **required-absent** — the policy required a signal the handoff does **not** carry (the
  would-be-`notEvaluated` gap; this is the FS.GG.Audio failure mode),
- **not-required** — the policy did not require this for the chosen profile.

**Rationale**: `Consumer.consume` already surfaces the handoff's declared nodes and its
`Diagnostic`s (`VersionMismatch`/`Malformed`/`StaleEvidence`/`AutoSyntheticDeclared`); the gap
between *selected* gates and *satisfied* gates is exactly "required-but-absent". Classifying makes
the absence a first-class, named output rather than an empty blocker list. The classifier is total
and pure (no I/O), driven entirely by already-parsed values.

**Alternatives considered**:
- *Only report a Pass/Fail verdict* — rejected: a verdict alone can read green while the handoff is
  empty; the sufficiency breakdown is the sharper half of issue #101.

## R4 — Simulated marker & keeping real `audit.json` byte-identical

**Decision**: The dry-run's machine-readable document is a **new projection** with:
- top-level `simulated: true`,
- schema id `schemaVersion: "fsgg.audit.dryrun/v1"` (distinct from the real `fsgg.audit/v2`),
- a `sufficiency` block (R3),
- the same recognizable `verdict`/`blockers`/`warnings`/`passing` shape as the real audit.

`AuditJson.ofShipDecision` is **not modified**; the simulated projection lives in a new pure
`SimulateProjection` module. The human projection reuses `HumanText`/`ReportView` plus a
prominent `SIMULATED (dry-run) — not a real gate result` banner.

**Rationale**: A distinct schema id makes it structurally impossible for a consumer to mistake the
simulated document for a real audit (FR-006/SC-004), while the shared key layout keeps it readable
by the same tooling (US3). Leaving `ofShipDecision` untouched guarantees the real `audit.json`
contract is byte-identical (a pinned test), satisfying the Tier-1 "no unintended surface move"
rule. The new projection is additive Tier-1 surface with its own baseline.

**Alternatives considered**:
- *Add a `simulated` key to `AuditJson.ofShipDecision`* — rejected: would either change the real
  audit output (contract break) or require a conditional that risks drift; a separate projection is
  cleaner and fences the contract.

## R5 — "Sample policy" default & scope reality

**Decision**: MVP dry-run evaluates the **repo it is run in** (its sensed `.fsgg` policy + handoffs
via the existing ports), not an arbitrary detached file. The "sample policy" for demonstration and
tests is the bundled **`samples/sdd-reference-gate-set`**, loaded through the public
`Loader.loadAndValidate` pipeline the `ReferenceGateSet.Tests` already exercise.

**Rationale**: There is **no embedded/default-policy loader** in `Config`; a policy is a `.fsgg`
directory path. The core value of issue #101 — *preview the verdict and check handoff sufficiency
without executing gate tooling* — is fully delivered by running against the current repo with
execution suppressed. Pointing dry-run at a fully-detached handoff file with a bundled policy and
zero repo context is a **larger, separable increment** (it needs a policy-resolution story that
doesn't exist yet); the spec's Assumptions already record explicit-file input as a
convenience-not-MVP. Keeping MVP repo-scoped is what lets this land green now.

**Alternatives considered**:
- *Embed the reference gate set as a compiled-in default policy* — deferred: introduces a
  packaging/embedding decision and a new resolution path; out of scope for the previewing MVP and
  best tracked as a follow-up if consumers ask for detached-file evaluation.
