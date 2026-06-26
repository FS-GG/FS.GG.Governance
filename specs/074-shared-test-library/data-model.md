# Phase 1 Data Model — Shared test-support library

This feature is a **behaviour-preserving consolidation**; it introduces no new domain data.
The "entities" below are the library's five helper groups (the shared surface) and the
migration-state of a consuming project (the acceptance signal). Field/relationship details
for the helpers live in [contracts/TestsCommon.fsi](./contracts/TestsCommon.fsi); the
acceptance invariants in [contracts/migration-acceptance.md](./contracts/migration-acceptance.md).

## Entity: `FS.GG.Governance.Tests.Common` (the library)

The single test-only assembly aggregating the five helper groups behind one curated `.fsi`.

| Attribute | Value |
|---|---|
| Location | `tests/FS.GG.Governance.Tests.Common/` |
| Packable | `false` (test-only; FR-008) |
| Public surface | `TestsCommon.fsi` — the five modules below, nothing more (FR-009) |
| Surface baseline | `surface/FS.GG.Governance.Tests.Common.surface.txt` (reflective drift test) |
| `src` references | union of cores the fakes/fixtures construct (enumerated at impl from the 3 command suites' `open` sets) |
| `src` consumers | **none** — invariant, scope-guard tested (FR-008) |
| third-party packages | **none** added (`Directory.Packages.props` unchanged) |

### Component: `RepositoryHelpers`
- **Purpose**: locate the repository root and related path helpers (today's per-project
  `findRepoRoot`, copied in 68 files).
- **Key members**: `findRepoRoot : DirectoryInfo|null -> string` (walks parents for
  `FS.GG.Governance.sln` **or** `.slnx`, fails fast if absent — research D4); `repoRoot : string`.
- **Dependencies**: `System.IO` only (dependency-free).
- **Validation**: `failwith` when no marker found, identical to the copies.

### Component: `FakePorts`
- **Purpose**: real-`git` `ProcessStartInfo` helper + git/exec/sensor port fakes used by the
  command and adapter suites (`GitPort`, `ExecutionPort`, `FreshnessSensor`,
  `Loader.FileReader`, `Ports`, counting/throwing variants).
- **Dependencies**: the typed-port owners (`Config`, `Snapshot`, `GateExecution`, `GateRun`,
  `FreshnessSensing`, `FreshnessResolution`, `CacheEligibility`, `EvidenceReuse`,
  `EvidenceReuseStore`, `CommandRecord`, `EvidenceCapture`, …).
- **Note**: `SYNTHETIC:`-tagged fakes (e.g. fixed env/builder senses) move **verbatim** with
  their disclosure comments intact (Principle V).

### Component: `CatalogFixtures`
- **Purpose**: the shared YAML catalog inputs — `projectYml`/`policyYml`/`toolingYml` and the
  `validCatalog`/`emptyCatalog`/`invalidCatalog` builders (~387 LOC across the 3 suites).
- **Dependencies**: the YAML string literals need none; `readerOf` returns `Loader.FileReader`
  (`Config`).
- **Boundary**: suite-specific catalog *variants* that are not byte-identical stay local (D4).

### Component: `SnapshotHelpers`
- **Purpose**: temp-repo + file-writing snapshot builders (`writeFile`, `withTempRepo`,
  snapshot constructors) that drive **real** `git` for end-to-end proofs.
- **Dependencies**: `System.IO`/`System.Diagnostics` + `Snapshot` types; **real** `git`
  process (Principle V) — no owned durable state.

### Component: `CaptureHelpers`
- **Purpose**: stdout/stderr/exit-code capture utilities used to assert against goldens
  (capturing `OutputSink`/`ArtifactWriter`, redirect-and-collect helpers).
- **Dependencies**: `System.IO` + the sink/writer port types.

## Entity: `FS.GG.Governance.Tests.Common.Tests` (the library's own suite)

| Attribute | Value |
|---|---|
| Location | `tests/FS.GG.Governance.Tests.Common.Tests/` |
| Holds | `SurfaceBaselineTests` (drift + **no-`src`-reference** scope guard) + `SmokeTests` |
| Test-count effect | **additive** and expected (like Phase A's `2237 → 2259`), never counted as drift |

## Entity: Migrated test project (state, not data)

An existing `*.Tests` project moving through the sweep. Its **state transition** is the
acceptance signal, not a stored value.

```
                migrate (add ProjectReference + delete redundant local copies, same commit)
   [unmigrated] ───────────────────────────────────────────────────────────────► [migrated]
   local copies        guard: full suite green ∧ per-project test count unchanged          references Tests.Common
   present             ∧ every golden/snapshot byte-identical                              redundant copies gone;
                                                                                           genuine per-suite variants kept
```

**Transition guard (must all hold to accept the migration — FR-003/FR-004/FR-007):**
1. Project compiles with the `ProjectReference` added **and** local duplicates deleted in the
   **same** change (no project ever compiles with both — spec Edge Case "name collisions").
2. Per-project test count == pre-migration baseline for that project (drift ⇒ reject &
   investigate; a lost/duplicated/renamed test).
3. Every golden and snapshot fixture byte-identical (drift ⇒ the shared fixture diverged from
   the local one ⇒ keep the local one local).
4. Exactly **one** compiled definition of each migrated helper remains for that project
   (no shadowing local copy).

**Terminal invariant after the sweep (FR-010 / SC-004):** the only remaining definitions of
the previously-duplicated helpers (`findRepoRoot`, the git `ProcessStartInfo` helper, the
catalog fixtures) are in `Tests.Common`, except explicitly-documented per-project variants.

## Out-of-scope (no model change)

Production (`src`) projects, MVU host boundaries, projection contracts, deterministic-JSON
output, test coverage authoring, and the other roadmap phases (B/C/E). Projects without a
`Support.fs` (10 of 78) are not required to migrate.
