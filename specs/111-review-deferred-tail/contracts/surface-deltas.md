# Contract: Surface-baseline deltas (the authoritative Tier-1 checklist)

This feature is **Tier-1 for exactly five surface moves** and **Tier-2 everywhere else**. The
api-compat / surface-drift gate must show *these deltas and no others* (spec FR-016, SC-001). Each
row is the acceptance contract for its PR: the `.fsi` before/after and the direction of the surface
change (all shrink the legal-value set except the one intentional widen, C6).

| # | Module (`.fsi`) | Change | Direction | PR |
|---|---|---|---|---|
| C1 | `GateRun/Model.fsi` | `GateDisposition.Executed`/`.Reused` gain `(ExitCode, bool)` payload; `GateOutcome` drops its `ExitCode`/`Passed` optionals (keeps `{ GateId; Disposition }`); add `isPassing` | **shrink** (illegal exit-less-Executed removed) | US1 |
| C2 | `GateRun/Plan.fsi` | `commandFor : … -> GateCommand option` → `… -> Result<GateCommand, NoCommand>` + new `NoCommand` DU | **refine** (option → typed result) | US1 |
| C3 | `Snapshot/Snapshot.fsi` | `RawSensing.RepoOk: bool` → `RawSensing.RepoState: RepoState` + new `RepoState` DU; `assemble` doc updated | **refine** (bool → 3-state) | US2 |
| C4 | `Calibration/Model.fsi` | `ComparisonSample` loses `Agreement` field; the now-dead `AgreementClassification` DU is removed too (it was used only by that field — `ObservedAgreement` is `AgreementLevel`, unaffected) | **shrink** | US2 |
| C5 | `ValidationMatrix/Matrix.fsi` | `decideMatrix` loses `boundary` parameter | **shrink** | US2 |
| C6 | `Kernel/Verdict.fsi` | **add** `val combineReasons : …` (promote existing private fn) | **widen** (intentional, A6) | US4 |

## Per-delta acceptance

- **C1 (`GateDisposition` payload)**: after the reshape, constructing an `Executed`/`Reused` outcome
  without an exit code MUST fail to compile (the case demands `(ExitCode, bool)`). The
  `disposition`/`exitCode`/`passed` JSON and the human "executed/reused/not-executed … passed/failed"
  strings MUST be byte-identical to `main` for every real gate outcome. `JsonTokens.dispositionToken`
  keeps its `GateDisposition -> string` signature (body-only change). Baseline: only `GateRun/Model`
  moves.
- **C2 (`NoCommand`)**: `commandFor` MUST return a distinct case for each of the three current
  `None` sites; at least one caller (diagnostic path) MUST surface the reason. Baseline: only
  `GateRun/Plan` moves.
- **C3 (`RepoState`)**: the git-unavailable snapshot produced via
  `assemble { raw with RepoState = GitUnavailable }` MUST equal the current hand-rolled record
  field-for-field (diagnostic id `GitUnavailable`, op `RepoCheck.Token`, same message, `Range=None`,
  empty working tree, same digest order). Baseline: only `Snapshot/Snapshot` (and `Interpreter` is a
  `.fs`-only edit) move.
- **C4 (`Agreement` drop)**: the calibration `decide` output MUST be identical for every fixture;
  the field is provably unread (`decide` reads only the sample count and the evidence-level
  `ObservedAgreement`). Removing the field leaves `AgreementClassification` dead, so it is removed in
  the same PR. Baseline: only `Calibration/Model` moves.
- **C5 (`boundary` drop)**: `decideMatrix`'s `MatrixPlan` output MUST be identical; all callers drop
  the argument. Baseline: only `ValidationMatrix/Matrix` moves.
- **C6 (`combineReasons` export)**: `Route.stakesOf` MUST produce byte-identical stakes strings via
  `Verdict.combineReasons`; the only new public symbol is `combineReasons`. Baseline: only
  `Kernel/Verdict` moves.

## Non-deltas (MUST NOT move the surface)

B5 (`Schema.fs` `finish` — `private`), A1/A4 (dedup — internal), A6 `mkFinding`/`safe`/`valuesFor`/
`sha256Hex`/`buildGate` (helpers stay non-exported in their new home), C1a/C1b (removals of already
non-exported or to-be-removed symbols — DocsChecks `ExampleFact` vocab and VerifyCommand
`SurfacesPending` leave *their* baselines but MUST NOT touch any *other* module), C1g/C2f/C2g
(cosmetic/docs). Any surface move on a module not in the C1–C6 table is a defect.

> **Gate command** (same one CI runs): regenerate the surface baseline for the touched module and
> diff — the diff MUST match the row above and nothing else. The dependency-fence suite
> (`tests/FS.GG.Governance.DependencyFences.Tests`) MUST stay green on the A1/A6 PRs.
