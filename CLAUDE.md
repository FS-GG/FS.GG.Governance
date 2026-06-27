<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the roadmap at
docs/initial-implementation-plan.md. There is no IN-PROGRESS feature. The most
recently DELIVERED feature is specs/076-verify-module-split/ (Phase C of the
architecture/quality/de-duplication roadmap — ✅ DELIVERED): it split the two Verify
god modules along their feature seams into new additively-public, `.fsi`-curated
sibling modules — host: `SurfaceFold`/`ViewCurrencyFold`/`ReleasePreview` (the third
decomposes `previewOf`→`previewOf'` so the fold stays host-`Model`-free, one-line
`Loop` wrapper); projection: `Core`/`SurfaceChecks`/`ReleaseReadiness`/`GeneratedViews`
+ a thin composing `VerifyJson` entry (`Core.writeCore` is 4-arg — the
surfaceChecks/releaseReadiness/generatedViews tail moved up into one hidden
`writeDocument` composer; `dispositionToken`'s `not-executed` divergence stays local in
`Core`, FR-009). One seam per commit (FR-010); every command/projection golden +
snapshot byte-identical at every commit; `Loop.fsi`/`VerifyJson.fsi` byte-identical
(four entry points + `schemaVersion` frozen). The two surface-drift baselines were
re-blessed ADDITIVELY once (Tier 1; existing `Loop`/`VerifyJson` blocks byte-identical)
and +4 structural seam scope-guard tests added (VerifyJson.Tests 28→30,
VerifyCommand.Tests 77→79). Per-file shrink: `Loop.fs` 1,009→946 (−63), `VerifyJson.fs`
582→122 (−460). **SC-001 deviation (recorded, flagged):** the T024 concrete ≤800-LOC
bar on `Loop.fs` is NOT met (946) — the three contracted folds (research D2) are small,
and reaching ≤800 would require extracting pipeline bodies D2 keeps in `Loop` and
widening the additive 3-module re-bless (against FR-004); honored the contracted scope
rather than gaming the number (a larger Tier-1 follow-up could extract more if ≤800 is
required). The `GateRunHost` route→ship→verify unification is recorded as ADR 0003 =
DEFER (route/ship/verify left unchanged, verified by empty `git diff --stat`). The prior
DELIVERED feature is specs/075-command-host-skeleton/ (Phase B of the architecture/quality/
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
