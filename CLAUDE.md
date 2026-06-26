<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the roadmap at
docs/initial-implementation-plan.md. The most recently DELIVERED feature is
specs/074-shared-test-library/ (Phase D of the architecture/quality/
de-duplication roadmap — ✅ DELIVERED). It introduced one test-only library,
`FS.GG.Governance.Tests.Common` (under `tests/`, `IsPackable=false`, referenced
by test projects via ProjectReference, NOT by any `src` project — a tested scope
guard), aggregating the genuinely-shared test helpers behind one curated `.fsi`
in FOUR modules: `RepositoryHelpers` (the 60-copy `findRepoRoot`, shared as the
`sln||slnx` superset, + `repoRoot`), `CatalogFixtures` (project/policy/tooling
YAML + valid/empty/invalid catalogs + `readerOf`/`factsOf`), `FakePorts`
(git/exec/sensor fakes), and `SnapshotHelpers` (real-`git` temp-repo builders +
the real-core snapshot/gate/evidence expectation builders). The sketched fifth
group `CaptureHelpers` proved unshareable — each command suite's `Capture`/
`capturingSink`/`capturingWriter` is parametrised by THAT command's own
`Loop.ArtifactKind`/`Interpreter.OutputSink`, so it stays local per FR-006. The
three command suites consume the library via thin re-exports through their own
`Support`; the 56 leaf suites had their `findRepoRoot` copies swept. Net
test-support reduction ≈ −1,200 LOC; per-project test counts identical to
baseline, every golden/snapshot byte-identical (2259 → 2265, the +6 being the
additive `Tests.Common.Tests` harness only). The prior DELIVERED feature is
specs/073-kernel-json-consolidation/ (Phase A): the `FS.GG.Governance.JsonText`/
`JsonTokens`/`JsonWriters` pure leaves placed BELOW everything. Past feature
plans live under specs/.
<!-- SPECKIT END -->
