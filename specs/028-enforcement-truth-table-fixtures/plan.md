# Implementation Plan: Golden Enforcement Truth-Table Fixtures

**Branch**: `028-enforcement-truth-table-fixtures` | **Date**: 2026-06-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/028-enforcement-truth-table-fixtures/spec.md`

## Summary

Close the two remaining `🟡` rows of **Phase 5 (Route Parity, Profiles, and Enforcement Fixtures)** by
producing **golden, committed, drift-guarded fixtures** for the enforcement decision surface — adding **no
new enforcement semantics, no CLI, and no audit schema**. The deliverable is a *coverage and evidence*
slice over the already-merged pure cores:

- **P1 — the golden truth table.** A deterministic, byte-stable, committed **Markdown** document
  enumerating the complete primary cross-product of the four enforcement dials — base severity (2) ×
  maturity (5) × run mode (6) × profile (4) = **240 rows** — each row pinning the dial inputs, the derived
  effective severity, and the reason text exactly as F023 `Enforcement.deriveEffectiveSeverity` returns
  them. A second, small **route-class** section (sourced from F015 `Routing` / F017 `Findings`) shows the
  routine-vs-fenced-vs-unknown-governed-path dimension the plan row requires. A **drift-guard test**
  regenerates both from the live cores and asserts byte-equality with the committed copy.
- **P2 — representative `audit.json` snapshots.** For each combination where a *single dial flips a
  finding between blocking and non-blocking*, a committed `audit.json` document produced verbatim by F025
  `AuditJson.ofShipDecision` over a real F024 `ShipDecision`, guarded by byte-equality snapshot tests.

**Plan-time reconciliations confirmed with the maintainer (2026-06-21):**

- **D1 — Generator home & tier (Tier 2).** The generator is **test-support code inside a new test
  project** `tests/FS.GG.Governance.EnforcementFixtures.Tests`. It adds **no `src/` module, no `.fsi`, and
  no `surface/*.surface.txt` baseline** — consistent with the spec's framing (a coverage/evidence
  deliverable that computes no new semantics) and Constitution Principle III (the plainest thing that
  works; a public module nothing consumes would be surface for its own sake). This is a **Tier 2** change:
  spec + tests, no public-API surface moved. FR-011's "covered by the `.fsi`/surface-baseline rules *if it
  is public*" is satisfied vacuously — the helper is not public.
- **D2 — Artifact home (top-level `fixtures/`).** The committed goldens live under the repo's existing
  `fixtures/` directory at `fixtures/enforcement/` — durable, reviewable independent of the spec folder,
  and a stable path for the drift guard. The truth table is `fixtures/enforcement/truth-table.md`; the
  snapshots are `fixtures/enforcement/audit-snapshots/<scenario>.audit.json`.
- **D3 — Table format (Markdown pipe table).** The truth table is a GitHub-renderable `| … |` table — most
  human-auditable in a PR diff (SC-005) and openable directly on GitHub. The byte cost of padding is
  accepted; determinism is unaffected (fixed column order, fixed lexical row order, `\n` newlines, no
  trailing whitespace).

This row **re-derives, re-classifies, re-partitions, re-orders, and re-serializes nothing**: every
outcome comes verbatim from F014/F015/F017/F023/F024/F025. The merged cores and their `surface/*.txt`
baselines are **untouched**; `dotnet build` / `dotnet test` over the existing projects stays byte-for-byte
unchanged, and the new test project is purely additive.

## Technical Context

**Language/Version**: F# on .NET `net10.0` (repo standard). No new language surface in `src/`; the only new
code is F# **test-support + tests** in one new test project.

**Primary Dependencies**: The merged cores, **consumed not modified** — F023 `Enforcement`
(`deriveEffectiveSeverity`, the `RunMode`/`Profile`/`Severity` vocabulary, `runModeOrdinal`), F024 `Ship`
(`rollup`, `ShipDecision`), F025 `AuditJson` (`ofShipDecision`, `schemaVersion`), F015 `Routing` (`route`,
`RouteReport`), F017 `Findings` (`findUnknownGovernedPaths`, `FindingReport`), and F014 `Config.Model`
(`Maturity`, `SurfaceClass`, `GovernedPath`, the typed-fact vocabulary). Test frameworks already on the
central feed: **Expecto** with the VSTest adapters. **FsCheck is deliberately NOT referenced** — the
truth table is an *exhaustive* enumeration of the closed dial cross-product, not a sampled property, so
the F025-style property generators add no value here; determinism is asserted by regenerating twice and
comparing (T012). **No new `PackageReference`** (FR-011): rendering uses BCL string building; snapshot
bytes come from F025's shared-framework `System.Text.Json` projection.

**Storage**: Committed text fixtures under `fixtures/enforcement/` (a Markdown table + a small set of
`*.audit.json` files). No database, no runtime storage. The drift guard reads these committed files via the
established `findRepoRoot (DirectoryInfo AppContext.BaseDirectory)` walk-up pattern (Support.fs precedent).

**Testing**: Expecto tests in the new project, exercising the **public** surfaces of the merged cores
(never private helpers — Principle V, Principle I's "FSI is the honest audience" applied through the packed
public API). Three test concerns: (1) **completeness/shape** — the generated table has exactly one row per
combination, no missing/duplicate (SC-001); (2) **drift guard** — regenerate and assert byte-equality with
the committed table and each snapshot, with a `BLESS_FIXTURES=1` re-bless path mirroring the existing
`BLESS_SURFACE=1` convention (SC-002, SC-003); (3) **coverage** — every blocking-altering dial is
represented by ≥1 snapshot, and each relaxed-blocker snapshot carries both base and effective severity
(SC-004). Determinism is asserted by regenerating twice and comparing (SC-002).

**Target Platform**: Developer/CI .NET SDK running `dotnet test`. No host, no OS-specific surface.

**Project Type**: Test + committed fixtures (a coverage/evidence deliverable). No `src/` project is added
or changed.

**Performance Goals**: N/A. The contract is **determinism and byte-stability**, not latency. The full
cross-product is 240 rows + a handful of snapshots — trivial to generate in-test.

**Constraints**: Pure/total/deterministic generation (FR-004): no clock, host path, environment, or
enumeration-order influence; fixed dial-iteration order; `\n` newlines; no trailing whitespace; UTF-8
without BOM. Reason text is part of the pinned outcome (Edge: reason-text stability) — the guard compares
it byte-for-byte. Adding a future dial value grows the table and **fails the guard until re-blessed**
(FR-012, Edge: cross-product growth). The merged cores and their baselines are not modified (FR-011).

**Scale/Scope**: One new test project; one committed Markdown truth table (240 primary rows + a route-class
section); one `audit-snapshots/` directory of representative `*.audit.json` files (one per blocking-altering
lever, see contracts); the drift-guard + completeness + coverage tests; one solution entry; a short README
pointer to the new fixtures. Zero changes to existing `src/`, `surface/`, or merged test projects.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS (adapted; no new public surface)** | No new `.fs`/`.fsi` public module is authored, so there is no FSI-first signature to draft. The discipline is honored in its consuming form: the fixtures are generated **only through the already-FSI-validated public surfaces** of F023/F024/F025/F015/F017 (the same API a human exercises in `scripts/prelude.fsx`), never private helpers. The tests are the semantic tests for *those* surfaces over the full cross-product. |
| II. Visibility in `.fsi` | **N/A** | No `src/` module is added or changed; no `.fsi` to curate and no surface baseline to move. The generator is internal test-support code in a test assembly (which, like every `*.Tests` project, has no curated `.fsi`). |
| III. Idiomatic Simplicity | **PASS — load-bearing** | The Tier-2 choice (D1) *is* the simplicity principle applied: a plain in-test generator (enumerate the closed dial lists, call the cores, render strings) over a public module nothing else consumes. Plain `for`/list comprehension enumeration; no SRTP, reflection (beyond the existing surface-drift idiom, which this row does **not** add), custom operators, or non-trivial CEs. |
| IV. Elmish/MVU is the boundary for stateful/I/O | **N/A** | No stateful or I/O-bearing workflow. Generation is a pure fold over finite closed enumerations; the only I/O is the test reading committed files to compare bytes (and the bless path writing them) — ordinary test-fixture I/O at the edge, not a modeled workflow. |
| V. Test Evidence Is Mandatory | **PASS** | Evidence is the whole deliverable: drift-guard tests fail before the committed fixtures match the live cores and pass after; completeness/coverage tests fail if a combination is missing or a blocking-altering lever is unrepresented. All inputs are **real** typed values driven through the genuine cores (the F025 `Support.fs` real-chain precedent) — no mocks, no synthetic evidence, so no `Synthetic` disclosure is needed. |
| VI. Observability & Safe Failure | **PASS** | The guard fails **loudly with a readable diff** identifying exactly which row/snapshot drifted (FR-006, SC-003), and distinguishes an intended change (re-bless with `BLESS_FIXTURES=1`) from an accidental regression (the failure message names the bless path). No silent truncation: the completeness test counts rows against the product of dial cardinalities, so an under-covered table fails rather than passing quietly. |
| Change Classification | **Tier 2 (internal change — coverage/evidence)** | No public API surface is added, removed, or modified; no new dependency; no inter-project/package contract change; no merged-spec behavior altered (the cores are consumed verbatim). Per the Constitution's Tier-2 definition this requires spec + tests and leaves `.fsi`/baselines untouched — which is exactly this row. (Confirmed reconciliation D1.) |
| Engineering Constraints | **PASS** | F#/.NET `net10.0`; no new third-party `PackageReference` (FR-011); no rendering package IDs/paths/templates assumed (the fixtures are this tool's own enforcement vocabulary only); the core libraries are untouched, honoring "the rule/evidence helper core stays minimal." Pack output and structured-logging TODOs are unaffected (no runtime/host code added). |

**Gate result: PASS — no unjustified violations. Complexity Tracking is empty.** The N/A rows record that
the F#-public-surface principles (II, IV) have no applicable target in a Tier-2 fixtures+tests row that
adds no `src/` code; I, III, V, and VI are honored in their fixtures-appropriate form.

## Project Structure

### Documentation (this feature)

```text
specs/028-enforcement-truth-table-fixtures/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 — the plan-time reconciliations (D1–D3) + dial-enumeration facts
├── data-model.md        # Phase 1 — the fixture entities: truth-table row, route-class row, snapshot scenario
├── quickstart.md        # Phase 1 — how to generate, bless, and validate the fixtures
├── contracts/           # Phase 1 — the byte-level contracts this row commits
│   ├── truth-table-format.md        # column set, dial-token spelling, row order, newline/encoding rules
│   └── audit-snapshot-set.md        # the named blocking-altering scenarios + each snapshot's source decision
├── checklists/
│   └── requirements.md  # (if present) spec quality checklist
└── tasks.md             # Phase 2 — /speckit-tasks output (NOT created here)
```

### Source Code / deliverable layout (repository root)

```text
fixtures/enforcement/                              # NEW — the committed golden artifacts
├── truth-table.md                                 # NEW — 240-row primary cross-product + route-class section
└── audit-snapshots/                               # NEW — representative blocking-altering audit.json docs
    ├── maturity-withholds-observe.audit.json      #   maturity (observe) withholds blocking (no-hide warning)
    ├── maturity-withholds-warn.audit.json         #   maturity (warn) withholds blocking
    ├── base-advisory-stays-advisory.audit.json    #   base-advisory never escalates
    ├── profile-relaxes-blocker.audit.json         #   a looser profile relaxes a base-blocker to a warning
    ├── profile-tightens-to-block.audit.json       #   a stricter profile pulls the floor down so it blocks
    ├── mode-below-floor.audit.json                #   run mode does NOT reach the blocking boundary (warning)
    └── mode-reaches-floor.audit.json              #   run mode reaches the blocking boundary (blocker)

tests/FS.GG.Governance.EnforcementFixtures.Tests/  # NEW — the generator + the drift/completeness/coverage guards
├── Generator.fs                                    # NEW — pure rendering over the merged cores (truth table + snapshots)
├── TruthTableTests.fs                              # NEW — completeness/shape (SC-001) + drift guard (SC-002/SC-003)
├── RouteClassTests.fs                              # NEW — route-class section sourced from F015/F017 (FR-003)
├── AuditSnapshotTests.fs                           # NEW — per-scenario byte-equality + coverage (FR-007..FR-010)
├── Support.fs                                      # NEW — repo-root walk-up + real input builders (F025 precedent)
├── Main.fs                                         # NEW — Expecto entry point
└── FS.GG.Governance.EnforcementFixtures.Tests.fsproj  # NEW — references the consumed cores + test frameworks

FS.GG.Governance.sln                               # CHANGED — add the new test project
README.md                                          # CHANGED — short pointer to fixtures/enforcement/

# Deliberately UNCHANGED:
src/** , surface/**                                # no core/.fsi/surface-baseline changes (Tier 2, FR-011)
tests/** (existing projects)                       # untouched; the new project is purely additive
```

**Structure Decision**: A **Tier-2 fixtures-and-tests** deliverable, not a new `src/` project. The
generator is internal test-support code in one new test project (`EnforcementFixtures.Tests`) that consumes
the merged public cores; the durable goldens live under the repo's existing top-level `fixtures/` directory
so they are reviewable and drift-guardable independent of the spec folder. The drift guard follows the
existing `BLESS_SURFACE=1`-style re-bless idiom (here `BLESS_FIXTURES=1`) and the `findRepoRoot` path
resolution already used by `AuditJson.Tests/Support.fs`. No merged core, `.fsi`, or surface baseline is
touched.

## Complexity Tracking

> No Constitution violations to justify — this section is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
