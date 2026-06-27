<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the roadmap at
docs/initial-implementation-plan.md. The most recently DELIVERED feature is
specs/075-command-host-skeleton/ (Phase B of the architecture/quality/
de-duplication roadmap — ✅ DELIVERED). It introduced one new pure leaf library,
`FS.GG.Governance.CommandHost` (under `src/`, `.fsi`-first, with a surface-area
baseline + reflective drift test + a scope-guard asserting no `Host`/`Cli`/
`*Command` edge), holding the genuinely-shared MVU command-host skeleton the seven
command `Loop.fs` hosts used to hand-copy. MOVED (all gated by byte-identical
goldens): `under`, `revOfCommit`, `baseHeadOf` (decomposed to the snapshot
diff-range so the leaf takes NO host `Model`), `emptySensedFacts`, `describeInvalid`,
`persistedContent`, the superset `GateClassification`, the parameterized
`executionPlan` (FR-006 — Route passes `BudgetFold = None`; Ship/Verify pass a
budget-fold closure capturing their own profile/mode), and the Verify↔Ship `kindOf`
(re-exported per host to preserve surface), `kindedRunsOf`, `buildSnapshot` (both
decomposed). STAYED LOCAL (FR-008 — textually identical but TYPE-divergent on each
host's own `Model`/`Effect`, the `dispositionToken`/`CaptureHelpers` precedent):
`fail`, `tryExecute`, `awaitingPersist`, and `exitCode`+`ExitDecision` (the canonical
superset `ExitDecision`+`exitCode` is built and unit-tested in the leaf, but host
ADOPTION — a six-host public-`Loop.fsi` surface cascade + interpreter/Cli/test ripple
— is deferred as a bounded follow-up); plus Refresh's `RefreshOutcome`-typed
`fail`/`exitCode`, Release's `buildSnapshot` (different input type), and
`cacheReportOf` (single site). Route gains one blessed new edge to `CostBudget`
(the shared superset `GateClassification.Deferred of BudgetReason` +
`executionPlan`'s `CacheDecisionReport` return); a pure core, graph stays acyclic.
Net host reduction ≈ −318 LOC; every command/projection golden + snapshot
byte-identical; full suite green save the pre-existing Cli `dotnet pack` timeout
flake; +10 additive `CommandHost.Tests`. See research.md §D9 for the recorded
divergences. The prior DELIVERED feature is specs/074-shared-test-library/ (Phase
D): one test-only library `FS.GG.Governance.Tests.Common` (under `tests/`,
`IsPackable=false`, referenced by test projects via ProjectReference, NOT by any
`src` project — a tested scope guard), aggregating the genuinely-shared test helpers
behind one curated `.fsi` in FOUR modules (`RepositoryHelpers`, `CatalogFixtures`,
`FakePorts`, `SnapshotHelpers`); the sketched fifth `CaptureHelpers` proved
unshareable and stayed local per FR-006 (net test-support reduction ≈ −1,200 LOC).
The prior DELIVERED feature is specs/073-kernel-json-consolidation/ (Phase A): the
`FS.GG.Governance.JsonText`/`JsonTokens`/`JsonWriters` pure leaves placed BELOW
everything. Past feature plans live under specs/.
<!-- SPECKIT END -->
