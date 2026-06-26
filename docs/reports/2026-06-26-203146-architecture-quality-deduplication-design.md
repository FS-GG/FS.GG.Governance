# Architecture, code-quality, and de-duplication analysis & design

**Timestamp:** 2026-06-26T20:31:46+02:00
**Author:** Claude (Opus 4.8)
**Status:** Analysis + design + roadmap, ready for feature scheduling
**Scope:** Whole-repo architecture / smell / duplication / god-module review of
`FS.GG.Governance` `src` (75 projects, ~24.6K LOC) and `tests` (75 projects,
~54K LOC), with a phased remediation roadmap. The only hard constraint on the
remediation work is that the full test suite must continue to pass; radical
reorganization is permitted where it is justified below.

## Executive summary

The macro-architecture is sound and must be preserved: one-concern-per-project
microprojects, a pure-core / impure-host split, `.fsi` signature boundaries,
Elmish/MVU (`Model`/`Msg`/`Effect`/`init`/`update`) command hosts, and
deterministic JSON as the machine contract. None of those decisions is the
source of the debt found here.

The debt is **missing shared leaf libraries**, not structural rot. Because every
concern lives in its own project and there is no `Kernel`-level "common" layer
for cross-cutting helpers, the same code was *copied* where it should have been
*referenced*. Four concrete clusters account for the bulk of it:

1. **JSON emit duplication** — the canonical `writeToString` exists in `Kernel`
   but is not exported, so it is re-implemented **14 times**; closed-enum token
   helpers and sub-object writers are copied across the 12 `*Json` projections.
2. **Command-host skeleton duplication** — the 8 MVU `Loop.fs` hosts copy a
   shared skeleton (`under`, `exitCode`, `fail`, `emptySensedFacts`,
   `executionPlan`, `tryExecute`, …); `VerifyCommand/Loop.fs` (1,119 LOC) and its
   projection twin `VerifyJson.fs` (626 LOC) have grown into god modules.
3. **Test-support duplication** — there is *no* shared test library; all 75 test
   projects hand-roll `Support.fs` (11,845 LOC total), with `findRepoRoot`
   copied into **68** files and ~42% byte-identical content across the largest
   command suites.
4. **CLI responsibility mixing** — parse + render + artifact-I/O are interleaved
   in `Cli/Cli.fs` and `Cli/Program.fs`.

Remediation is low-risk because the output contracts are pinned by golden and
snapshot tests: the extractions below are verifiable byte-for-byte. Estimated
mechanical reduction is **~1,700–4,200 LOC** (src + tests), dominated by the
test-support library.

### On "radical reorganization"

A genuinely radical option — collapsing the 75 microprojects into a handful of
larger assemblies — was considered and is **rejected**. The microproject
boundaries carry real value: they enforce the pure-core/impure-host split at the
*assembly* level (a pure projection physically cannot take a host dependency),
they give each `.fsi` a minimal surface, and they keep the dependency graph
acyclic and legible. Collapsing them would trade a legible graph for a smaller
project count and would weaken the very boundaries that keep the cores pure.

The justified change is the **opposite** of collapsing: *add* a small number of
shared leaf libraries that the microprojects reference, and split the two god
modules into smaller ones. That removes duplication while strengthening — not
eroding — the existing boundaries. The one place where a more aggressive
consolidation is on the table is the command-host family (Finding 2), and that
is argued explicitly there.

---

## Findings

All counts below were verified directly against the working tree at
`fc845ae`, not estimated.

### Finding 1 — `writeToString` duplicated 14× because the canonical one is hidden (🔴 high value, low risk)

`Kernel/Json.fs:23` defines the canonical deterministic-JSON helper:

```fsharp
let writeToString (emit: Utf8JsonWriter -> unit) : string =
    use stream = new MemoryStream()
    use writer = new Utf8JsonWriter(stream)
    emit writer
    writer.Flush()
    Encoding.UTF8.GetString(stream.ToArray())
```

But `Kernel/Json.fsi:30` exposes `module Json =` **without exporting
`writeToString`** — so it is effectively private. The measured consequences:

- **14 copies** of the identical 6-line function across `src`
  (`VerifyJson`, `RouteJson`, `AuditJson`, `ReleaseJson`, `EvidenceJson`,
  `GatesJson`, `CacheEligibilityJson`, `RefreshJson`, `CostBudgetJson`,
  `ProvenanceJson`, `ScaffoldManifestJson`, `AttestationJson`, …).
- Only **1 of 12** `*Json` projects references `Kernel` at all — the dependency
  edge needed to reuse it does not even exist.
- Several copies *cite* the Kernel precedent in a comment
  (`RouteJson.fs:40`, "the `Json.fs` `writeToString` precedent") and then
  reimplement it anyway.

Layered on top of that, the projection modules copy:

- **Closed-enum token helpers** — `costToken` (3×), `maturityToken` (4×),
  `severityToken` (4×), `environmentToken` (3×), `dispositionToken` (3×),
  `basisToken` (3×), `profileToken` (2×).
- **Sub-object writers** — `writeCause` (6×: `VerifyJson:99`, `RouteJson:157`,
  `AuditJson:139`, `CacheEligibilityJson:56`, `CostBudgetJson:68`,
  `EvidenceJson:87`), `verdictByGate` (3×), `outcomeByGate` (3×),
  `writeExecution` (3× near-identical), `writeEnforcement` (2×),
  nullable-field writers (2×).
- `VerifyJson.fs:360-473` grows a **parallel `rr`-prefixed copy** of
  ReleaseJson's token logic rather than depending on it.

**Estimated reduction:** ~300 src LOC, and a single source of truth for emit
determinism (today a determinism fix must be applied in 14 places).

### Finding 2 — Command-host `Loop.fs` family: copied skeleton + two god modules (🔴 high value, medium risk)

The 8 MVU hosts (`VerifyCommand`, `ShipCommand`, `RouteCommand`,
`RefreshCommand`, `CacheEligibilityCommand`, `ReleaseCommand`, `EvidenceCommand`,
`Host` — 4,844 LOC) share a copied skeleton. Verified verbatim/near-verbatim
duplicates:

| Helper | Copies | Representative sites |
|---|---|---|
| `under repo rel` | **6 (confirmed)** | `VerifyCommand/Loop.fs:218`, `ShipCommand:200`, `RouteCommand:173`, `RefreshCommand:118`, `CacheEligibilityCommand:148`, `ReleaseCommand:136` |
| `exitCode` mapper | 6 | `VerifyCommand:176`, `ShipCommand:157`, `RouteCommand:135`, … |
| `fail` / `describeInvalid` | 6 / 4 | `VerifyCommand:350` / `:357`, … |
| `emptySensedFacts` | **3 (confirmed)** | Verify / Ship / Route |
| `revOfCommit` + `baseHeadOf` | 4 | Verify / Ship / Route / CacheEligibility |
| `persistedContent` + `awaitingPersist` | 3 | Verify / Ship / Route |
| `cacheReportOf` | 4 | Verify / Ship / Route / CacheEligibility |
| `GateClassification` DU | 3 | Verify / Ship / Route |
| `executionPlan` (~75 LOC) | 3 near-identical | `VerifyCommand:584`, `ShipCommand:494`, `RouteCommand:337` |
| `tryExecute`, `buildSnapshot`, `kindedRunsOf`, `kindOf` | 2–3 each | Verify ↔ Ship |

**God module — `VerifyCommand/Loop.fs` (1,119 LOC).** It carries seven
responsibilities in one `update` loop (cache-eligibility + gate execution +
cost-budget + provenance + release-readiness preview + surface checks +
generated-view currency), a four-way sensed-facts join, and four parallel
"notes" accumulators (`CurrencyNotes`, `Diagnostics`, `ViewCurrencyFindings`,
`SurfaceFindings`). `ShipCommand/Loop.fs` (931 LOC) is the same minus
release/surface. Its projection twin **`VerifyJson.fs` (626 LOC)** layers four
features behind four entry points (`ofVerifyDecision`,
`…WithSurfaceChecks`, `…WithPreview`, `…WithGeneratedViews`).

**Two-part fix.** (a) Extract a `CommandHost` leaf for the verbatim skeleton
helpers and a parameterized `executionPlan`. (b) Split Verify's *optional*
feature layers (release-preview, surface-fold, view-currency-fold) out of the
core loop so the base verify host shrinks back toward Route's size, and split
`VerifyJson` along the same four feature seams.

**Justified semi-radical option:** the `route → ship → verify` trio is the same
pipeline with progressively more folds. A single parameterized
`GateRunHost` core (taking a per-command record of optional folds) could replace
three near-identical `executionPlan`/`tryExecute`/projection skeletons. This is
more than a helper extraction, so it is gated behind the cheaper extraction in
the roadmap (Phase B before Phase C) and should only proceed if Phase B's
golden-diff stays byte-identical.

**Estimated reduction:** ~400–500 LOC from the skeleton extraction; the god-module
split is clarity-dominated (~150 LOC) but is the larger maintainability win.

### Finding 3 — Test `Support.fs`: 11,845 LOC, ~42% copy-paste, no shared library (🔴 highest maintenance value, low risk)

There is **no shared test-support project**; all 75 test projects hand-roll
`Support.fs`. Verified:

- **`findRepoRoot` copied in 68 files** — essentially every test project.
- **7 copies** of the real-`git` `ProcessStartInfo` helper.
- The three largest command suites (`VerifyCommand.Tests` 857,
  `ShipCommand.Tests` 769, `RouteCommand.Tests` 723 = 2,349 LOC) are
  **~42% byte-identical**: YAML catalog fixtures
  (`projectYml`/`policyYml`/`toolingYml`/`validCatalog`/`emptyCatalog`/
  `invalidCatalog`, ~387 LOC), git/exec/sensor port fakes (~270 LOC),
  `writeFile`/`withTempRepo`/snapshot builders (~270 LOC).
- No `Directory.Build.props` shared `Compile Include` linking — nothing is
  shared today.

This is the single largest maintenance liability: a one-line catalog-fixture
change currently requires synchronized edits across 4+ files, and a repo-root
helper change touches 68.

**Fix:** add a `FS.GG.Governance.Tests.Common` library
(`RepositoryHelpers`, `CatalogFixtures`, `FakePorts`, `SnapshotHelpers`,
`CaptureHelpers`); have test projects `open` it and delete the local copies.

**Estimated reduction:** conservatively **1,000+ LOC** from the four command
suites alone; up to ~3,500 across the full suite.

### Finding 4 — CLI mixes parse + render + I/O (🟡 medium value, medium risk)

`Cli/Cli.fs` (829) and `Cli/Program.fs` (673) dispatch cleanly (a single
recursive `parseOptions`, no per-subcommand boilerplate) but conflate concerns:

- `Cli.fs:501-700` does *all* text + JSON rendering inside the module that also
  parses (`renderText`, `renderJson`, parse-error and failure rendering).
- `Program.fs` scatters artifact reading / path resolution / JSON extraction
  across `readArtifact` (103-124), `designFactsFromFile` (259-292),
  `specKitFacts` (211-248).
- `runHost` (`Program.fs:473-531`, 58 LOC) couples budget tracking + effect
  dispatch + message routing + review-store I/O.

**Fix:** split out `CliRender.fs`, `ArtifactReading.fs`, and a `ReviewStore`
module; keep `Program.fs` as thin port orchestration.

**Estimated reduction:** ~200 LOC moved (clarity-dominated).

---

## Design — target shared-leaf topology

The remediation introduces **three** shared leaf libraries and splits **two** god
modules. The leaves are pure (or test-only) and sit below the existing
projection/host layers, so the dependency graph stays acyclic and the
pure-core/impure-host split is preserved.

```
Kernel/Json.fs        ── export writeToString in Json.fsi (no new project)
Kernel.JsonTokens     ── NEW pure leaf: closed-enum token helpers
Kernel.JsonWriters    ── NEW pure leaf: writeCause / verdictByGate /
                          outcomeByGate / writeExecution / writeEnforcement /
                          nullable-field writers
   ▲ referenced by ──── the 12 *Json projections

CommandHost           ── NEW pure leaf: under / exitCode / fail /
                          describeInvalid / emptySensedFacts / baseHeadOf /
                          persistedContent / awaitingPersist / cacheReportOf /
                          GateClassification / buildSnapshot / kindedRunsOf /
                          tryExecute / executionPlan (parameterized)
   ▲ referenced by ──── the 8 command Loop.fs hosts

FS.GG.Governance.Tests.Common  ── NEW test-only library:
                          RepositoryHelpers / CatalogFixtures / FakePorts /
                          SnapshotHelpers / CaptureHelpers
   ▲ referenced by ──── all 75 test projects
```

Design rules for the extraction:

- **Byte-identical output is the acceptance test.** Every projection/host change
  must leave the golden and snapshot fixtures untouched. If a golden moves, the
  extraction changed behaviour and must be revisited.
- **Leaves stay pure.** `Kernel.JsonTokens`/`JsonWriters`/`CommandHost` take no
  host dependency; they depend only on the already-shared domain types.
- **`.fsi` first.** Each new leaf gets a signature file that exposes exactly the
  helpers being shared and nothing more, matching the repo's existing discipline.
- **One concern moved at a time** so each commit's test run isolates the cause of
  any golden drift.

---

## Roadmap

Phases are independent and individually shippable; within a phase, keep the full
suite green at every commit. Ordered by value-to-risk, the two lowest-risk /
highest-value phases (A, D) come first.

### Phase A — Kernel JSON consolidation (low risk, ~300 LOC)
1. Export `writeToString` from `Kernel/Json.fsi`; add `Kernel` references to the
   11 `*Json` projects that lack them; delete the 13 local copies.
2. Add `Kernel.JsonTokens` (`.fsi` + `.fs`) with the seven token helpers; replace
   in-module copies, including VerifyJson's `rr`-prefixed duplicates.
3. Add `Kernel.JsonWriters` with the shared sub-object writers; replace copies.
4. Acceptance: all `*Json.Tests` golden snapshots byte-identical; full suite green.

### Phase B — CommandHost skeleton extraction (medium risk, ~400–500 LOC)
1. Add `CommandHost` (`.fsi` + `.fs`) with the verbatim helpers
   (`under`, `exitCode` (with optional `Blocked`), `fail`, `describeInvalid`,
   `emptySensedFacts`, `baseHeadOf`, `persistedContent`, `awaitingPersist`,
   `cacheReportOf`, `GateClassification`, `buildSnapshot`, `kindedRunsOf`,
   `tryExecute`).
2. Parameterize `executionPlan` (per-command fold record) and move it to
   `CommandHost`; have Route/Ship/Verify call it.
3. Acceptance: every command `route.json`/`audit.json`/`verify.json` golden
   byte-identical; full suite green.

### Phase C — God-module split (medium/high risk, clarity-dominated)
1. Split `VerifyCommand/Loop.fs` optional layers (release-preview, surface-fold,
   view-currency-fold) into sibling modules; core loop shrinks toward Route size.
2. Split `VerifyJson.fs` along its four feature seams (`Core`, `SurfaceChecks`,
   `ReleaseReadiness`, `GeneratedViews`) with a thin composing entry module.
3. **Decision gate:** evaluate the semi-radical `GateRunHost` unification of
   `route/ship/verify` only if Phase B's golden diff was clean. Justify or drop
   in an ADR under `docs/decisions/`.
4. Acceptance: all command + projection goldens byte-identical; full suite green.

### Phase D — Shared test library (low risk, ~1,000–3,500 LOC) — can run in parallel with A
1. Create `FS.GG.Governance.Tests.Common` with `RepositoryHelpers`,
   `CatalogFixtures`, `FakePorts`, `SnapshotHelpers`, `CaptureHelpers`.
2. Migrate the four command suites first (largest win), then sweep the remaining
   71 `Support.fs` files, deleting local `findRepoRoot`/git/fixture copies.
3. Acceptance: full suite green with identical test counts (no tests lost in the
   move).

### Phase E — CLI decomposition (medium risk, ~200 LOC)
1. Extract `CliRender.fs` (all `render*` out of `Cli.fs`).
2. Extract `ArtifactReading.fs` (`readArtifact`/`designFactsFromFile`/path logic)
   and a `ReviewStore` module; reduce `runHost` to thin orchestration.
3. Acceptance: CLI smoke transcripts unchanged; full suite green.

### Suggested sequencing

`A` and `D` first (independent, lowest risk, highest immediate value) → `B` →
`C` (gated on B) → `E`. Each phase is a candidate Spec Kit feature; per the repo
constitution, land each through a feature spec with its own readiness evidence
and a clean full-suite run.

## Reduction summary

| Phase | Area | Risk | Est. LOC removed |
|---|---|---|---|
| A | Kernel JSON (`writeToString` + tokens + writers) | Low | ~300 |
| B | CommandHost skeleton | Medium | ~400–500 |
| C | VerifyCommand/VerifyJson split (+ optional GateRunHost) | Med/High | ~150 (clarity) |
| D | Shared test library | Low | ~1,000–3,500 |
| E | CLI render/IO split | Medium | ~200 (moved) |
| **Total** | | | **~1,700–4,200** |

## Explicitly out of scope (do not change)

The microproject decomposition, MVU host boundaries, pure-core/impure-host
assembly split, `.fsi` signature-first discipline, and deterministic-JSON
contract are working as intended. The duplication is a symptom of missing shared
leaves, and Phases A–E add exactly the leaves the architecture already implies —
without collapsing any existing boundary.
