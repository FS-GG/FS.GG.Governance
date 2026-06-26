# Phase 0 Research — Shared test-support library

All NEEDS CLARIFICATION items from the plan's Technical Context are resolved here. Each
decision records what was chosen, why, and what was rejected. Counts were verified against
the working tree at branch `main` (tip `48c8cfc`), not estimated.

## D1 — Linking mechanism: referenced library vs. `Directory.Build.props` `Compile` link

**Decision.** A **`<ProjectReference>` to a test-only F# class library**
(`FS.GG.Governance.Tests.Common`); test projects `open` its modules and delete their local
copies. **Not** a root `Directory.Build.props` `<Compile Include="…/Shared.fs" Link="…"/>`
shared-source link.

**Rationale.**
- FR-001 says the helpers are *referenced rather than copied*; FR-009 requires *a signature
  boundary that exposes exactly the shared helpers and nothing more*. A real library with a
  curated `.fsi` delivers both; a shared-`Compile` link gives neither (`.fsi`-per-file
  linking is awkward and the source recompiles into every consumer, so there is no single
  compiled definition — contradicting FR-003/SC-004).
- The design report itself says consumers "`open` it" — a referenced assembly.
- A `Directory.Build.props` `Compile Include` would force the shared file into **every**
  project in the tree (including the 10 with no `Support.fs` and all `src` projects),
  violating FR-008 (test-only). A `ProjectReference` is opt-in per project.
- It matches the established repo pattern: every shared leaf in Phase A
  (`JsonText`/`JsonTokens`/`JsonWriters`) is a referenced library with an `.fsi`.

**Alternatives rejected.**
- *Shared `Compile Include` via `Directory.Build.props`* — rejected per above (recompiles
  per consumer ⇒ no single definition; leaks into every project; no clean `.fsi` boundary).
- *A NuGet-packed shared package* — rejected: `IsPackable=false`, this never leaves the repo;
  packing test-only helpers adds release surface for zero benefit.

## D2 — One library with five modules, or a tiered split

**Decision.** **One** library, `FS.GG.Governance.Tests.Common`, exposing five modules behind
one `.fsi`: `RepositoryHelpers`, `FakePorts`, `CatalogFixtures`, `SnapshotHelpers`,
`CaptureHelpers` — exactly as FR-002 and the spec's Key Entities name them.

**Rationale.** The spec authoritatively names a single aggregating library and its five
groups (FR-002; "Aggregates the five helper groups behind a signature boundary"). One
library means one `<ProjectReference>` line per migrated project — the simplest, least
error-prone sweep, and the smallest diff per commit (FR-007/SC-006).

**Known cost (accepted).** Because `FakePorts`/`CatalogFixtures` construct typed port and
catalog values, the library must `ProjectReference` the union of `src` cores those fakes
touch (≈ the command-suite core set: `Config`, `Snapshot`, `GateExecution`, `GateRun`,
`FreshnessSensing`, `FreshnessResolution`, `CacheEligibility`, `EvidenceReuse`,
`EvidenceReuseStore`, `CommandRecord`, `EvidenceCapture`, …). Every test project that
references `Tests.Common` therefore transitively references that union, even a tiny leaf
suite that only wanted `findRepoRoot`. This is **inert**: test projects are not packable, are
not on the release graph, and carry no dependency scope-guard (unlike the `src` leaves), so a
wider transitive reference set costs only marginal build time, not correctness or contract
purity. `ProjectReference` brings assemblies, **not** `open`s, so no consumer gains an
ambiguous name.

**Alternatives rejected / deferred.**
- *Tiered split* — a dependency-free core (`RepositoryHelpers`+`CaptureHelpers`, referenced by
  all 68) plus a heavier `CommandFixtures` (`CatalogFixtures`+`FakePorts`, referenced only by
  the command/host suites). This better respects dependency-minimization, but **contradicts
  FR-002's single named library**. Recorded as the natural future refactor **if** the fat
  transitive set ever becomes a measurable build-time problem; out of scope here.

## D3 — Surface boundary, baseline, and the library's own test project

**Decision.**
- `Tests.Common` gets `TestsCommon.fsi` (curated, `.fsi`-first) + `TestsCommon.fs` (no access
  modifiers), exposing exactly the five modules' shared members.
- A blessed baseline `surface/FS.GG.Governance.Tests.Common.surface.txt`, validated by a
  reflective `SurfaceBaselineTests` in a new minimal `FS.GG.Governance.Tests.Common.Tests`
  project (blessed via `BLESS_SURFACE=1 dotnet test`), mirroring the sibling leaf-test
  convention verbatim (e.g. `JsonText.Tests/SurfaceBaselineTests.fs`).
- That same `.Tests` project carries a **scope-guard test** asserting **no `src/*.fsproj`
  references `FS.GG.Governance.Tests.Common`** (FR-008), plus a couple of smoke tests
  exercising `RepositoryHelpers`/`CaptureHelpers` directly so the library has real-evidence
  coverage independent of the migrated suites.

**Rationale.** Constitution Principle II + Engineering Constraints require a curated `.fsi`
and a surface baseline for every public module; the repo enforces this with a per-project
reflective drift test. The new library is no exception even though it is test-only. The
scope-guard test makes FR-008 a tested invariant rather than a convention.

**Test-count accounting.** SC-001 ("total test count identical to baseline") is about the
**migration** not losing tests: every *migrated* project keeps its exact per-project count.
The new `Tests.Common.Tests` is **additive** and expected — exactly as Phase A's full-suite
count moved `2237 → 2259` solely because of its three new leaf-test projects. The plan and
quickstart state this explicitly so the additive delta is not mistaken for drift.

## D4 — `findRepoRoot` variants and divergent fixtures

**Decision.** The shared `RepositoryHelpers.findRepoRoot` walks parent directories for the
repo marker checking **both** `FS.GG.Governance.sln` **and** `FS.GG.Governance.slnx` (the
superset variant) — 17 `Support.fs` files already use the `sln||slnx` form, the rest check
`.sln` only. The superset is behaviour-identical in this tree (only `.sln` exists today) and
safe for either.

Genuinely divergent "copies" that are **not** byte-identical (e.g. a suite-specific catalog
variant, VerifyJson-style local writers) **stay local** and are **not** forced into the
shared surface — the byte-identity of that suite's goldens is the guard (spec Edge Cases;
mirrors Phase A keeping `dispositionToken`/`writeCauseValue` local). Only the genuinely
duplicated helpers move (FR-006, FR-010).

**Rationale.** A superset locator is the one place a tiny behaviour-preserving generalization
is warranted (it strictly widens acceptance and matches the majority variant). Everything
else moves **only** if byte-identical; the golden/snapshot diff is the arbiter, never a
visual "looks the same."

## D5 — Migration ordering and "green at every commit"

**Decision.** Land the library + migrate **one** project (US1, proves the architecture) →
migrate the **three command suites** as the first real batch (US2, largest measured win,
FR-005) → **sweep** the remaining `Support.fs` files in small batches (US3). Each commit
removes one concern (or one suite) and is gated on a **full green suite with unchanged
per-project counts and byte-identical goldens** before the next (FR-007, SC-006).

**Rationale.** One concern / one batch per commit means any failure isolates its own cause —
the same discipline Phase A used. The command suites first because they carry the ~42%
byte-identical bulk and are the proving ground for the full sweep.

## D6 — No MVU boundary; no new packages

**Decision.** The library models **no** Elmish/MVU workflow and adds **no** third-party
`PackageReference`. The fakes are inert port *values* (functions/records the consuming suites
drive); `SnapshotHelpers` is a pure temp-dir + real-`git` builder with no owned state. The
library needs no `Expecto`/`Microsoft.NET.Test.Sdk` (those live only in its `.Tests` project).

**Rationale.** Principle IV applies to features with multi-step state / I-O *workflow*;
moving already-inert helper definitions between assemblies introduces none. Keeping the
library package-free preserves dependency-minimalism (`Directory.Packages.props` unchanged).

---

### Resolved unknowns

| Unknown (Technical Context) | Resolution |
|---|---|
| Link mechanism (project ref vs shared compile) | **D1** — `ProjectReference` to a test-only library |
| One library vs. tiered | **D2** — one library, five modules (per FR-002); tiered deferred |
| Surface baseline / `.fsi` / own test project | **D3** — curated `.fsi` + blessed baseline + minimal `.Tests` (surface + scope guard + smoke) |
| `findRepoRoot` variant to share | **D4** — `sln||slnx` superset; divergent fixtures stay local |
| Ordering / green-at-every-commit | **D5** — one project → 3 command suites → sweep |
| MVU? new packages? | **D6** — neither |
