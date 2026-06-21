# Implementation Plan: Evidence-Reuse Decision Core

**Branch**: `030-evidence-reuse-decision` | **Date**: 2026-06-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/030-evidence-reuse-decision/spec.md`

## Summary

Land **Phase 11 (Cost, Cache, and Provenance)** row 2 — *"Cache reusable evidence only when all freshness
inputs match"* — the row F029's plan named as the one its `matches`/`diff` were *"the literal foundation
of."* Continuing this repo's maintainer-confirmed **pure-core-first** rhythm (F015–F029 each landed a pure,
total, deterministic core before any host edge consumed it), this row delivers a single new pure core,
**`FS.GG.Governance.EvidenceReuse`**, that answers one operational question deterministically: *"Given the
evidence I have already recorded, may I reuse any of it for this run — and if not, exactly which input
changed?"*

The core provides:

- **`EvidenceRef`** — a thin opaque-string newtype standing for *already-recorded* evidence (a handle the
  edge mints; this core never parses, validates, produces, or dereferences it), mirroring F029's `Revision`.
- **`RecordedEvidence`** — a closed record pairing one F029 `FreshnessInputs` value (the world the evidence
  was recorded against) with its `EvidenceRef`.
- **`ReuseStore`** — an immutable single-case value (`ReuseStore of RecordedEvidence list`) holding the
  recorded entries. **Not** a live cache, connection, or file — a value handed in and returned, exactly as
  F029's inputs are values handed in.
- **`ReuseDecision`** = `Reuse of EvidenceRef | Recompute of RecomputeCause`, and
  **`RecomputeCause`** = `NoPriorEvidence | InputsChanged of InputCategory list` — the no-hide explanation
  (FR-006), expressed in F029's `InputCategory` vocabulary.
- **`EvidenceReuse.decide : FreshnessInputs -> ReuseStore -> ReuseDecision`** — the pure, total reuse
  decision: *Reuse* iff some recorded entry F029-`matches` the candidate on **every** category, else
  *Recompute* with a located cause.
- **`EvidenceReuse.record : FreshnessInputs -> EvidenceRef -> ReuseStore -> ReuseStore`** — the pure, total,
  **de-duplicating** insert (refresh-on-match, most-recent-wins), plus `empty`, `entries`, and
  `referenceValue` for construction/inspection.

**Plan-time reconciliations (maintainer to confirm):**

- **D1 — New pure core, FreshnessKey-only dependency (Tier 1).** A new packable library
  `src/FS.GG.Governance.EvidenceReuse`, referencing **only** `FS.GG.Governance.FreshnessKey`. It reuses
  F029's `FreshnessInputs`, `matches`, `diff`, and `InputCategory` verbatim and consumes the F014 newtypes
  transitively *through* F029 (it never references Config directly, never references Gates/Snapshot or any
  host/edge assembly). New `.fsi` + new `surface/*.surface.txt` baseline ⇒ **Tier 1**, but **no new
  third-party `PackageReference`** (Constitution Engineering Constraint: the rule/evidence helper core stays
  minimal). The dependency direction stays one-way: `EvidenceReuse → FreshnessKey → Config`.
- **D2 — Reuse is exactly F029 `matches`; the explanation is exactly F029 `diff`.** "Cache reusable
  evidence only when all freshness inputs match" is implemented as *Reuse* iff some entry `matches` the
  candidate — no new partial/fuzzy match notion (FR-004). The `InputsChanged` cause carries `diff` against
  the relevant prior entry (FR-006). F029 is consumed verbatim; nothing in F029/Config is modified.
- **D3 — Opaque evidence reference newtype.** `EvidenceRef = EvidenceRef of string`, a thin opaque-string
  newtype (the F029 `Revision` precedent). The core treats it as an opaque, comparable token: it is carried
  back on *Reuse* and never interpreted (FR-001).
- **D4 — The reuse store is an ordered list, newest-first; representation is deliberately plain.** Modeled
  as `ReuseStore of RecordedEvidence list` rather than a `Map` keyed by F029 `Key`: determinism (not lookup
  latency) is the contract, the store is small, and a list keeps the core inspectable and free of a
  "compute-the-key-at-insert" coupling. `record` **prepends** and filters prior full-matches, so the head is
  the most recent and there is at most one entry per matching-input class (FR-008); `decide` scans head-first
  so "most-recent wins" is deterministic even for a hand-built store with duplicates (Edge: multiple
  matches).
- **D5 — "Prior evidence for the candidate's work" = same F018 `GateId` (Check + Domain).** Resolving the
  spec's deferred "which prior entry's diff to surface" question: when no entry fully matches, the cause is
  `InputsChanged (diff candidate e)` for the most-recent entry `e` whose **Check and Domain equal the
  candidate's** (the `GateId = "<domain>:<checkId>"` identity the whole catalog is keyed by); if no entry
  shares the candidate's `GateId`, the cause is `NoPriorEvidence`. Because that branch is reached only when
  no entry fully matches, the surfaced `diff` is always non-empty and never lists Check/Domain — it names
  exactly the non-identity categories that changed (FR-006, the no-hide rule).
- **D6 — "Output digest" remains out of scope (inherited from F029 D4).** The Phase-11 plan line lists
  "output digest" among freshness concerns, but it is a *result* of running a gate (a write/verify concern),
  not an *input* that decides reuse. It belongs to the later cache-write/verify row, not to this reuse
  decision. (Spec Assumptions.)

This row **computes no persistence** (no filesystem/database read or write), **no eviction / expiry / size
limit**, performs **no output-digest verification**, runs **no gate**, computes **no ship verdict**,
persists **no artifact**, and adds **no CLI**. The merged cores and their `surface/*.txt` baselines are
**untouched**; `dotnet build` / `dotnet test` over the existing projects stays unchanged, and the new
project + its test project are purely additive.

## Technical Context

**Language/Version**: F# on .NET `net10.0` (repo standard; `Nullable=enable`,
`TreatWarningsAsErrors=true` inherited from `Directory.Build.props`). One new `src/` library with a curated
`.fsi`, plus one new test project.

**Primary Dependencies**: **`FS.GG.Governance.FreshnessKey`** only — for `FreshnessInputs`, `matches`,
`diff`, and `InputCategory` (`FreshnessKey.Model` + the `FreshnessKey` operations module), reused verbatim
(FR-010). The F014 typed-fact newtypes (`CheckId`/`DomainId`/…) arrive **transitively through F029** and are
only ever touched as `FreshnessInputs` fields via structural equality — this core names no Config type and
references no Config project directly. **No new third-party `PackageReference`** (FR-014): the decision is
plain `List` filtering/scanning + `FSharp.Core` only. Test frameworks already on the central feed
(`Directory.Packages.props`): **Expecto**, **Expecto.FsCheck**, **FsCheck**, **Microsoft.NET.Test.Sdk**,
**YoloDev.Expecto.TestSdk**.

**Storage**: None. No database, no files, no runtime storage — the `ReuseStore` is an in-value collection,
not a persisted cache. The only test-side I/O is the surface-drift baseline read (and its `BLESS_SURFACE=1`
write), the established pattern.

**Testing**: Expecto + FsCheck, exercising the **public** surface (`EvidenceReuse.decide` / `record` /
`empty` / `entries` / `referenceValue`) over real, literally-constructible `FreshnessInputs`,
`RecordedEvidence`, and `ReuseStore` values (Principle V — no mocks, no private helpers). Concerns: (1)
**reuse iff all inputs match / single-field-change ⇒ recompute** for every F029 category (SC-001), (2)
**determinism + order/dup invariance** (SC-002), (3) **every recompute carries a located cause** —
`NoPriorEvidence` vs `InputsChanged` with the right categories (SC-003), (4) **empty store ⇒
`NoPriorEvidence`** (SC-004), (5) **record→reuse, refresh/de-dup most-recent-wins, independent entries,
replay determinism** (SC-005), (6) **purity** under changed cwd/time/filesystem (SC-006), (7) **surface
drift + scope hygiene** (Principle II, SC-007). Reuse-iff-match, determinism, and totality are FsCheck
properties; the rest are example tests.

**Target Platform**: Developer/CI .NET SDK running `dotnet test`. No host, no OS-specific surface.

**Project Type**: A new pure-core F# library + its test project. No host, no CLI, no MVU.

**Performance Goals**: N/A. The contract is **determinism and totality**, not latency; a store holds a
modest number of recorded entries per gate.

**Constraints**: Pure / total / deterministic (FR-003/FR-009): reads no clock, filesystem, git, environment,
or network; identical candidate + identical store always yields the identical decision; identical starting
store + identical recording sequence always yields an equivalent store (same decisions for all candidates).
Reuse is exactly F029 `matches` (FR-004); covered-artifact order/duplication never changes a decision
(inherited from F029). Every *Recompute* carries a located, non-ambiguous cause (FR-006). `record` does not
mutate its input store and holds at most one entry per matching-input class (FR-007/FR-008). The merged
cores and baselines are not modified (FR-010/SC-007).

**Scale/Scope**: One new `src/` library (`EvidenceReuse` — `Model.fsi/fs` + `EvidenceReuse.fsi/fs`); one new
test project; one new surface baseline `surface/FS.GG.Governance.EvidenceReuse.surface.txt`; two solution
entries; a short `scripts/prelude.fsx` FSI section (design-first proof, Principle I); a `README.md` cores
pointer; the `CLAUDE.md` plan pointer. Zero changes to existing `src/`, `surface/`, or merged test projects.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | The public surface is drafted as `Model.fsi` + `EvidenceReuse.fsi` and exercised in `scripts/prelude.fsx` (a new F030 section) before any `.fs` body exists; semantic tests call the packed public functions, never private helpers. |
| II. Visibility in `.fsi` | **PASS** | Two curated `.fsi` files are the sole public-surface declaration; the `.fs` files carry no access modifiers. A new `surface/FS.GG.Governance.EvidenceReuse.surface.txt` baseline is added and guarded by a reflective `SurfaceDrift` test (the F029/AuditJson precedent), with the `BLESS_SURFACE=1` re-bless path. |
| III. Idiomatic Simplicity | **PASS — load-bearing** | Plain records, single-case newtypes, two small closed DUs (`ReuseDecision`, `RecomputeCause`), and `List.tryFind`/`List.filter`/cons. No SRTP, reflection (outside the surface test), custom operators, type providers, or non-trivial CEs. The list-backed store (D4) is the plainest representation that is deterministic and inspectable. |
| IV. Elmish/MVU is the boundary for stateful/I/O | **N/A** | No state, no I/O, no workflow — pure total functions over supplied values. The `ReuseStore` is a value transformed by `record`, not a stateful store (no persistence, no effects). Like F018 `Gates`, F019 `Route`, F029 `FreshnessKey`, this is a pure projection that needs no MVU ceremony. |
| V. Test Evidence Is Mandatory | **PASS** | Every input is a real, literally-constructible typed value driven through the genuine public functions (the F029 `Support.fs` real-chain precedent — reusing its `baseInputs`/`allCategories` table). Tests fail before the implementation matches the contract and pass after. No mocks ⇒ no `Synthetic` disclosure needed. |
| VI. Observability & Safe Failure | **PASS** | `RecomputeCause` is the observability surface: a non-reuse is never an opaque "no" — it is either `NoPriorEvidence` or `InputsChanged` naming the exact differing categories (the no-hide requirement, FR-006). The functions are total: no exception, no swallowed failure, no silent truncation (an empty store / empty reference is an ordinary value, FR-012). |
| Change Classification | **Tier 1 (contracted change — new public API)** | Adds a new public module/assembly and a new surface baseline ⇒ full chain: spec, plan, `.fsi`, baseline, tests. **No new third-party dependency.** No existing public API, baseline, or merged behavior is altered (F029/Config are consumed verbatim; only FreshnessKey is referenced). |
| Engineering Constraints | **PASS** | F#/.NET `net10.0`; no new third-party `PackageReference` (FR-014); references only `FreshnessKey`, honoring "the rule/evidence helper core stays minimal — MUST NOT depend on git/filesystem scanning" (no Snapshot/git reference; `EvidenceRef`/`Revision` are opaque edge-supplied tokens). No rendering package IDs/paths/templates assumed — the inputs are opaque ids/hashes/refs supplied by the caller. Pack output + structured-logging TODOs unaffected (no runtime/host code). |

**Gate result: PASS — no unjustified violations. Complexity Tracking is empty.** Principle IV is the only
N/A (no stateful/I/O workflow); I, II, III, V, VI all have concrete targets and pass.

## Project Structure

### Documentation (this feature)

```text
specs/030-evidence-reuse-decision/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 — decisions D1–D6 + the decision/record semantics facts
├── data-model.md        # Phase 1 — EvidenceRef, RecordedEvidence, ReuseStore, ReuseDecision, RecomputeCause
├── quickstart.md        # Phase 1 — how to build, FSI-exercise, test, and re-bless the surface
├── contracts/           # Phase 1 — the contracts this row commits
│   ├── evidence-reuse-api.md         # the public function signatures + their laws
│   └── reuse-decision-semantics.md   # the decide/record decision tables (match, cause selection, dedup)
├── checklists/
│   └── requirements.md  # spec quality checklist (already present)
└── tasks.md             # Phase 2 — /speckit-tasks output (NOT created here)
```

### Source Code / deliverable layout (repository root)

```text
src/FS.GG.Governance.EvidenceReuse/                 # NEW — the pure evidence-reuse decision core
├── Model.fsi                                        # NEW — EvidenceRef, RecordedEvidence, ReuseStore, ReuseDecision, RecomputeCause (sole public surface)
├── Model.fs                                         # NEW — the type bodies (no access modifiers)
├── EvidenceReuse.fsi                                # NEW — empty / record / decide / entries / referenceValue signatures + laws
├── EvidenceReuse.fs                                 # NEW — pure decision + de-duplicating record
└── FS.GG.Governance.EvidenceReuse.fsproj            # NEW — references ONLY ../FS.GG.Governance.FreshnessKey; no new package

tests/FS.GG.Governance.EvidenceReuse.Tests/          # NEW — the semantic tests
├── Support.fs                                        # NEW — real FreshnessInputs/EvidenceRef/ReuseStore builders + FsCheck generators + repoRoot (reusing F029's baseInputs/allCategories shape)
├── ReuseDecisionTests.fs                             # NEW — full match ⇒ Reuse(ref); single-field change ⇒ Recompute, every category (SC-001)
├── DeterminismTests.fs                               # NEW — decide-twice equality + covered-artifact order/dup invariance of the decision (SC-002)
├── ExplanationTests.fs                               # NEW — Recompute always carries a located cause; NoPriorEvidence vs InputsChanged categories (SC-003)
├── RecordTests.fs                                    # NEW — record→reuse, refresh/de-dup most-recent-wins, independent entries, replay determinism (SC-005)
├── EmptyStoreTests.fs                                # NEW — empty store ⇒ Recompute NoPriorEvidence for any candidate (SC-004)
├── PurityTests.fs                                    # NEW — decisions/records identical across cwd/time/fs changes (SC-006)
├── SurfaceDriftTests.fs                              # NEW — baseline equality + scope-hygiene (FreshnessKey/Config/BCL/FSharp.Core only) (SC-007)
├── Main.fs                                           # NEW — Expecto entry point
└── FS.GG.Governance.EvidenceReuse.Tests.fsproj        # NEW — references the core + FreshnessKey + test frameworks

surface/FS.GG.Governance.EvidenceReuse.surface.txt    # NEW — committed public-surface baseline (Principle II)
FS.GG.Governance.sln                                 # CHANGED — add the new library + test project
scripts/prelude.fsx                                  # CHANGED — add the F030 design-first FSI section
README.md                                            # CHANGED — short pointer to the new core in the cores list
CLAUDE.md                                            # CHANGED — SPECKIT plan pointer → this plan

# Deliberately UNCHANGED:
src/** (existing), surface/** (existing)             # no merged core/.fsi/surface-baseline changes (FR-010)
tests/** (existing projects)                         # untouched; the new project is purely additive
```

**Structure Decision**: A **new pure-core library** mirroring F029 `FreshnessKey` / F018 `Gates` (a `Model`
file of types + a same-named operations file), the established Phase-pattern for a deterministic projection.
It references **only `FreshnessKey`** (D1) so the reuse core stays minimal and free of the git-sensing
assemblies, reusing F029's freshness vocabulary verbatim. Tier 1 (new public surface + baseline), no new
third-party dependency.

## Complexity Tracking

> No Constitution violations to justify — this section is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
