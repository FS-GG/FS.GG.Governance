# Contract — Host Surface Changes & Byte-Identity Anchor

## Public surface growth (per host — `Loop.fsi` / `Interpreter.fsi`; re-bless surface baselines)
- `ArtifactKind` gains `CostBudgetArtifact`, `ProvenanceArtifact`.
- `RunRequest` gains `CostBudgetOut : string` (default `readiness/cost-budget.json`) and
  `ProvenanceOut : string` (default `readiness/provenance.json`).
- `Model` gains `CacheDecisionReport option` and `AuditSnapshot option` carriers.
- `kindOf : Gate -> CommandKind` (pure) is declared public for test exercise.
- `Interpreter.Ports` gains `senseEnvironment : unit -> EnvironmentClass` and
  `senseBuilder : unit -> BuilderIdentity` (both normalized).
- `.fsproj`: + ProjectReference `CostBudget`, `CommandKind`, `CostBudgetJson`, `ProvenanceJson`, `Provenance`.

No change to: `parse` flags' existing semantics (`verify` still rejects `--mode`; `ship` keeps `--mode`/`--profile`),
`exitCode`, `Verdict`/`ExitCodeBasis`, or the existing `WriteArtifact`/`PersistStore` ports.

## Byte-identity anchor (the non-negotiable, SC-004)
- `verify.json` (`fsgg.verify/v1`) and `audit.json` (`fsgg.audit/v2`) and the route/ship goldens stay
  **byte-identical** to their frozen pre-wiring baselines for identical repository state.
- This holds because (research.md D1): the default `budgetFor Standard Verify/Gate` ceiling is `Medium`, and every
  frozen golden fixture's must-recompute gates fit `Medium` — proven by a **fixture-budget invariant** test run
  before wiring. If any fixture holds an over-ceiling must-recompute gate, it is surfaced and escalated as a real
  behavioral change, not absorbed.

## Deferred / out of scope (bounded, per Development Workflow)
- `fsgg release` cost-budgeting — the separate deferred F26 release host-wiring thread; not wired here.
- `route` / `cache` / `explain` / `evidence` hosts — not budget-bearing gate executors; untouched.
- No new pure core, report object, verdict, exit-code scheme, or external dependency.
