<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the roadmap at
docs/initial-implementation-plan.md. There is no IN-PROGRESS feature. The most recently
DELIVERED feature is specs/081-sdd-handoff-consumer/ (✅ DELIVERED): a Tier 1 feature shipping the
SDD→Governance handoff CONSUMER (ADR-0002 queued work) so a produced
`readiness/<id>/governance-handoff.json` actually drives `route`/`ship`/`verify` verdicts — the
handoff is no longer inert. A new pure leaf library `FS.GG.Governance.Adapters.SddHandoff` (five
`.fsi`-curated modules `Model`/`Reader`/`Mapping`/`Readiness`/`Consumer`; BCL `System.Text.Json`
only, ZERO new package, NO SDD `ProjectReference` — SC-006, verified by a reflective hygiene
guard): `Reader.parse` version-pins the contract major (1.x; an unknown major / malformed /
missing-required / declared-`autoSynthetic` each ⇒ a DISTINCT typed `Diagnostic`, never a throw —
the `autoSynthetic` token is its OWN `AutoSyntheticDeclared` cause, not generic `Malformed`);
`Mapping` maps `evidence.nodes[].state` → `Kernel.EvidenceState` token-for-token row-for-row
(ADR-0002), `deferred`/`accepted-deferral` → `Skipped` (FR-004), a `stale` node → underlying state
PLUS a `StaleEvidence` diagnostic (FR-006), then `Evidence.build`+`Evidence.effective` for the
taint closure (a `Real` node resting on `Synthetic` ⇒ `AutoSynthetic` ⇒ blocking-capable);
`Readiness.toGate` projects `readiness.*` into a first-class `Gate` (`BlockOnShip` when the
disposition is non-shippable OR `blockingDiagnosticIds` non-empty, else advisory `Warn`);
`Consumer.consume` composes per-document into typed `Gate` + pre-selected `SelectedGate` entries
(evidence + readiness + a blocking `integrity` gate for a bad document — NO mapped gate for that
document, FR-011), aggregated in `<id>` then `GateId` order. **Research D3 pivot**: the verdict
hosts form their verdict via the Config→Gates→Routing→Route→Ship pipeline, NOT the kernel `Adapter`
rule catalogs, so the consumer integrates via GATES, not the kernel SPI. Each of the three host
loops (`RouteCommand`/`ShipCommand`/`VerifyCommand`) gains the SAME additive MVU edge (research D6):
an `Effect.LoadHandoffs`, a `Msg.HandoffsLoaded`, an `Interpreter.Ports.Handoffs: string ->
Reader.HandoffRead list` port (locate `readiness/<id>/governance-handoff.json` in stable ordinal
`<id>` order — the ONLY I/O; total+safe ⇒ `[]` on any error), and a pure post-`Route.select` fold
that unions `Consumer.consume`'s gates into the registry + `RouteResult.SelectedGates` BEFORE
`Ship.rollup` — GUARDED so absent handoff ⇒ identity fold ⇒ byte-identical output (FR-001, SC-003).
`LoadHandoffs` is emitted FIRST in `init` so `HandoffsLoaded` folds before `Loaded(Valid)` (the
breadth-first driver). ADR 0002 + the handoff tutorial moved with the code in lockstep (FR-014/
FR-015 — queue item #4 resolved toward the gate binding; readiness row = "first-class
gate-registry entry"). **Recorded deviations**: (1) the three host surface-drift hygiene guards
were relaxed to PERMIT the one `Adapters.SddHandoff` edge while still forbidding every other
adapter (+ kernel/host/cli) — the intended host→consumer edge (research D6); (2) diagnostics ride
the integrity gate's description (one verdict mechanism, research D5), so no separate diagnostics
channel was threaded into host `Model`s (keeps the no-op byte-identical). Verified: library
31/31; RouteCommand 85/85 (handoff gates in gates.json/route.json + the real `Handoffs` port's
stable `<id>` ordering & empty-repo `[]`), ShipCommand 101/101 (SC-001: satisfied⇒Pass vs
failing⇒Fail with the blocking evidence gate in `Blockers`, through the REAL pipeline),
VerifyCommand 81/81 (SC-005: a blocking readiness gate is verify-blocking under `Strict`), Host
18/18 — all green; three host surface baselines re-blessed ADDITIVELY + the new library baseline;
the absent-handoff no-op keeps every existing host golden byte-identical. (CLI 49/51: the 2 are
the pre-existing `dotnet pack` local-feed MSBuild-node flake, out-of-scope.) Scope: 1 new library
(10 files) + 1 new test project + additive edits to the three hosts (`Loop`/`Interpreter` `.fsi`+
`.fs`) + their test wiring + ADR/tutorial + `.sln`; no production `src/` core touched. Unblocks
Coordination board P3 (Governance actually fires, and the handoff is consumed). The prior
DELIVERED feature is specs/079-reference-gate-set/ (✅ DELIVERED): a
Tier 2 data+test+docs feature publishing a single curated, populated reference `.fsgg`
gate set at `samples/sdd-reference-gate-set/.fsgg/` (full four-file project/policy/
capabilities/tooling set, alongside the SDD reference worked-example) plus a co-located
adopter `README.md` and an `adopter-onboarding.md` cross-link. It declares three
first-class checks — `build`, `test`, and an in-process `evidence`-integrity check (the
last bound to `build-evidence` = `"dotnet fsi build.fsx -- evidence"`) — each bound to a
declared `tooling.yml` command and routed through declared `build`/`test`/`evidence`
domains + path-map (`src/**`+`*.sln`→build, `tests/**`→test, `build.fsx`→evidence), with
`light` as the non-blocking default profile. `build`/`test` carry `block-on-ship`,
`evidence` carries `warn` (advisory everywhere — the first-touch posture). A new
`FS.GG.Governance.ReferenceGateSet.Tests` regression guard (Expecto+YoloDev like the
repo's other 80 test projects — **deviation from the spec's xUnit wording**, recorded:
xUnit is absent from central `Directory.Packages.props`; the framework is incidental and
the binding G1–G7 assertions hold regardless) loads the on-disk artifact through the
EXISTING Config→Gates→Routing→Route→Enforcement pipeline (real evidence — no synthetic
facts, no mocks) and freezes its invariants G1–G7: loads `Valid` with 0 diagnostics,
exactly 3 gates `build:build`/`test:test`/`evidence:evidence` with no dangling/orphan
refs (0 orphan commands), `defaultProfile: light`, and — on a failing change at
`RunMode.Verify` (the one mode where `block-on-ship` is Light-advisory yet
Strict-blocking, research D5) — every selected gate `Advisory` under `Light` yet ≥1
`Blocking` under `Strict` (the deliberate ratchet), with `evidence` advisory under both.
No new public F# surface, no `.fsi`/baseline change (Tier 2). Verified: the guard runs
8/8 green; a copy-unedited reuse check loads `Valid` (3 checks, `light`) in a fresh dir
(FR-009/SC-005); and three sanity mutations (empty checks → 5 fail, break a command ref →
load `Invalid`, 8 fail, flip `defaultProfile` off `light` → G5 fails) each turn the guard
red, reverting clean (SC-007). Scope: 4 YAML + README + guard project (3 files) + docs
cross-link + `.sln` registration; no `src/` production code. Unblocks Coordination board
P4 (Templates overlay copies it unedited). The prior
DELIVERED feature is specs/078-fix-scaffold-build-test/ (✅ DELIVERED): a
Tier 2 test-support-only fix bounding the SDD reference-provider worked-example
real-evidence `dotnet build` (`Support.dotnetBuild`) so the suite never hangs. It adds a
reusable `Support.runBounded exe args workingDir budget : BuildAttempt` primitive —
async `OutputDataReceived`/`ErrorDataReceived` capture + `BeginOutputReadLine` AFTER
`Start` (defeats the `ReadToEnd()` deadlock, D1), `WaitForExit(int budget.TotalMilliseconds)`,
and on overrun `Kill(entireProcessTree=true)` + a bounded `WaitForExit(killDrainMargin)`
drain → a new `BuildAttempt.TimedOut of budget*partialOutput` named-skip case (a
start-failure still ⇒ `SdkMissing`, a clean exit ⇒ `Built`). `dotnetBuild` becomes a thin
wrapper passing `-maxcpucount:1 --disable-build-servers` (collapses the MSBuild worker
fan-out, FR-007) under `buildBudget` (default 120 s, override `FSGG_BUILD_BUDGET_SECONDS`;
absent/garbage/≤0 ⇒ default, never unbounded). The heavyweight build is gated behind
`realEvidenceEnabled ()` (`FSGG_REAL_EVIDENCE=1` OR a truthy `CI` — set and, trimmed +
case-insensitive, not `""`/`0`/`false`); the default run emits a named opt-out skip and
does NOT scaffold or build (worked-example project 5.3 s ≪ 30 s, SC-002). The build
test's match is total/ordered so a non-zero `Built` still FAILS (FR-003/SC-003) and the
three skips — opt-out / `PREREQUISITE:` missing-SDK / `BUDGET EXCEEDED:` timeout — are
textually distinct (FR-004/FR-009). A real forced-stall test
(`BoundedBuildTests.fs`, additive `+1`) kills a real OS sleeper (`sleep` on Unix /
`ping` on Windows) under a 500 ms budget and asserts `TimedOut`, return within
budget+`boundAssertionMargin`, and the spawned tree gone. Validated both lanes:
`FSGG_REAL_EVIDENCE=1` and `CI=true` each run the real build green (11/11). Scope: ONLY
`Support.fs` + the build test in `WorkedExampleTests.fs` + new `BoundedBuildTests.fs` +
the `.fsproj` `<Compile>` line; the two non-build worked-example tests and the committed
manifest golden stay byte-identical, no production code / `.fsi` / golden / surface
baseline touched (FR-008/SC-005, diff confirmed empty). The prior DELIVERED feature is
specs/077-cli-decomposition/ (Phase E of the
architecture/quality/de-duplication roadmap — ✅ DELIVERED): a Tier 1 structural
split of the optional CLI into three new `.fsi`-curated sibling modules inside the
existing `FS.GG.Governance.Cli` project (no new project/dependency, FR-008) —
`CliRender` (the pure `CommandResult`→text/JSON projection relocated verbatim from
`Cli.fs`: the four entry points + every `*Json`/`*Text` sub-writer + the render-only
formatting helpers; `exitCode`+`stableStrings` stay public in `module Cli` and are
called qualified), `ArtifactReading` (the impure spec-kit/design path resolution +
file/dir reads + regex task/dep parsers + fact extraction + snapshot assembly,
`optionsFor`/`readArtifact`/`loadSnapshot` public, `loadSnapshot` uses
`Path.GetFullPath` not `Program.fullPath`), and `ReviewStore`
(`loadReview`/`saveReview` + the hidden store-root/sanitize/verdict helpers and the
`review-store-unavailable` short-circuit). Compile order
`Cli→CliRender→ArtifactReading→ReviewStore→Program`; `runHost`/`main` reduced to thin
orchestration with NO inline `File.`/`Directory.` bodies (SC-004), the budget folds +
the `review-dispatch-failed` branch + the read-only watch/tui block left unchanged
(research D4, verified by an empty `+`/`-` diff on those functions). One concern per
commit (FR-009; three commits), every CLI text/JSON transcript byte-identical at each
(SC-001 — `route`/`explain`/`contract`/`evidence` × text/json + the `bogus`
usage-error all diff-empty), the four render `val`s relocated out of `Cli.fsi` with
call sites updated in-commit (research D3), and the name-level surface baseline +
in-test `generatedSurface` literal grown ADDITIVELY by exactly three lines
(`module CliRender`/`ArtifactReading`/`ReviewStore`; FR-011). Per-file: `Cli.fs`
829→557 (−272), `Program.fs` 673→251 (−422); new modules `CliRender.fs` 282,
`ArtifactReading.fs` 370, `ReviewStore.fs` 74 (~694 LOC relocated). **SC-005 deviation
(recorded, flagged):** the spec's "~200 LOC" headline tracks only the dominant render
extraction and undercounts the two edge extractions (~694 actual); the binding
SC-001/002/003/004 hold regardless (research D7). Suite: the full `dotnet test
FS.GG.Governance.sln` COMPILED the whole solution and ran 13 test projects green with
0 failures (Kernel 73, SurfaceChecks 18, … + the CLI suite 50/50 and neighbors Host
18/RouteCommand 83/VerifyJson 30/VerifyCommand 79); the unrelated SDD
`fs-gg-fullstack` template-generation integration test (pathologically slow in this
environment) and the pre-existing CLI `dotnet pack` timeout flake are out-of-scope.
The prior DELIVERED feature is specs/076-verify-module-split/ (Phase C of the
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
