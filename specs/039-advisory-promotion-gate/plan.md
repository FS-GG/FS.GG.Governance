# Implementation Plan: Advisory-to-Blocking Promotion Gate — the Single-Sample-Noise Guardrail

**Branch**: `039-advisory-promotion-gate` | **Date**: 2026-06-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/039-advisory-promotion-gate/spec.md`

## Summary

Land **Phase 12 (Agent-Reviewed Rule Guardrails)**'s **fifth** line — *"Keep agent-reviewed findings advisory until
deterministic backing evidence, repeated-review confidence thresholds, or explicit human sign-off exists"* (design
`docs/initial-implementation-plan.md`; `docs/initial-design.md`, *Optional agent-reviewed constraints*, the
**single-sample-noise** row). F035 (`FS.GG.Governance.AgentReviewKey`), F036 (`FS.GG.Governance.VerdictReuse`), F037
(`FS.GG.Governance.PromptIsolation`), and F038 (`FS.GG.Governance.ReviewRecord`) landed the phase's first four lines.
This row delivers the design's **single-sample-noise** guardrail as a pure **decision** core: a typed
promotion-decision value and a single total, deterministic function that decides whether an agent-reviewed finding
may be promoted from *advisory* to *eligible to block* — where the **only** three permitted promotion bases are
deterministic backing evidence, a repeated-review confidence threshold being met, or explicit human sign-off — and
which **defaults to advisory** whenever none holds.

Continuing this repo's maintainer-confirmed **pure-core-first** rhythm (F015–F038 each landed a pure, total,
deterministic core before any host edge consumed it), this row delivers a single new packable pure core,
**`FS.GG.Governance.AdvisoryPromotion`** — the **agent-review analogue of F023 `deriveEffectiveSeverity` / F030
`decide` / F036 `lookup`**: a *decision* core (not a record core) that maps supplied facts to a named outcome and
derives **no** byte-stable identity.

| Phase-12 row | Core | Question it answers |
|---|---|---|
| 1 — cache key (F035) | `AgentReviewKey` | *Under what identity is a verdict cached?* |
| 2 — invalidation (F036) | `VerdictReuse` | *Is a cached verdict still valid, and if not, why?* |
| 3 — prompt isolation (F037) | `PromptIsolation` | *How is the request shaped so the artifact is data, not an instruction?* |
| 4 — review record (F038) | `ReviewRecord` | *What was this completed review, for the audit trail?* |
| **5 — advisory promotion (this row)** | **`AdvisoryPromotion`** | ***May this agent-reviewed finding be promoted from advisory to block-eligible — and on which permitted basis?*** |

The core invokes **no model / agent / network**, reads **no clock / filesystem / git / environment**, computes **no
hash from raw bytes**, runs **no actual review**, makes **no cache lookup / verdict invalidation** (F035 / F036),
builds **no review record** (F038, consumed conceptually as an opaque finding, not produced), produces /
interprets / re-scores **no verdict** (FR-007), defines **no judge-vs-human calibration** (the sixth row), derives
**no effective severity / enforcement verdict** (F023 / F024), performs **no persistence / JSON projection**, and
adds **no CLI**. Its sole output is the typed `PromotionDecision` value.

The core provides (full vocabulary in [data-model.md](./data-model.md); the signatures + laws in
[contracts/advisory-promotion-api.md](./contracts/advisory-promotion-api.md)):

- **`PromotionBasis`** = `DeterministicBackingEvidence | RepeatedReviewConfidence | HumanSignOff` — the **closed
  three-value** vocabulary of permitted bases, and no others; the model's own self-confidence is not a case (FR-002).
- **`ConfirmationCount`** = `ConfirmationCount of int` and **`ConfidenceThreshold`** = `ConfidenceThreshold of int`
  — the two supplied confidence inputs (FR-004).
- **`SignOff`** = `SignOff of string` — the opaque human-sign-off marker; presence is the basis, never parsed or
  dereferenced (FR-002, research D3).
- **`AdvisoryReason`** = `NoPermittedBasis | ConfidenceBelowThreshold of ConfirmationCount * ConfidenceThreshold` —
  the no-hide attribution carried by a *stays advisory* outcome (FR-005, research D5).
- **`PromotionFacts`** = `{ BackingEvidence: EvidenceRef option; Confirmations: ConfirmationCount; ConfidenceThreshold:
  ConfidenceThreshold; SignOff: SignOff option }` — the supplied levers, the sole input to `decide` (research D2/D4).
- **`PromotionDecision`** = `StaysAdvisory of AdvisoryReason | EligibleToBlock of PromotionBasis * PromotionBasis
  list` — the two-outcome gate verdict; *eligible to block* is **non-empty by construction** (head + tail), so an
  empty-basis promotion is unrepresentable (FR-001, research D6).
- **`AdvisoryPromotion.decide`** — the single total, deterministic decision over `PromotionFacts` (FR-003).
- **`AdvisoryPromotion.satisfiedBases`** / **`signOffValue`** / **`confirmationValue`** / **`thresholdValue`** — the
  small projection/unwrap helpers for audit and tests.

The core reuses **F030 `EvidenceRef`** (`FS.GG.Governance.EvidenceReuse.Model`) verbatim for the
deterministic-backing-evidence basis — the precedent the spec names (FR-009, research D3) — introducing only the
minimal new vocabulary the row needs (the promotion bases, the confidence inputs, the sign-off marker, the advisory
reason, the promotion facts, the promotion decision). FR-009's SHOULD to reuse the established advisory/blocking
vocabulary (F023 `Severity = Advisory | Blocking`) is **deliberately not taken** for the outcome: an *eligible to
block* decision is necessary-not-sufficient and is **not** a `Blocking` severity (FR-008), so reusing `Severity`
would erase the eligibility/blocking distinction this row exists to preserve — hence the distinct `StaysAdvisory |
EligibleToBlock` outcome (research D1 / D7). The merged cores and their `surface/*.surface.txt` baselines
are **untouched**; `dotnet build` / `dotnet test` over existing projects stays unchanged, and the new project + its
test project are purely additive (SC-007).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (repo standard; `Nullable=enable`, `TreatWarningsAsErrors=true` inherited
from `Directory.Build.props`). One new `src/` library with two curated `.fsi` files, plus one new test project.

**Primary Dependencies**: One `ProjectReference` — **`FS.GG.Governance.EvidenceReuse`** (F030), to reuse the opaque
**`EvidenceRef`** token verbatim for the backing-evidence basis (research D3); F029 `FreshnessKey` / F014 `Config`
arrive transitively through it but are unused by this core. **No new third-party `PackageReference`** (FR-011): the
decision is plain pattern matching + `FSharp.Core` + the one reused token. Test frameworks already on the central
feed (`Directory.Packages.props`): **Expecto**, **Expecto.FsCheck**, **FsCheck**, **Microsoft.NET.Test.Sdk**,
**YoloDev.Expecto.TestSdk**.

**Storage**: None. No database, no files, no runtime storage — the decision is an in-value result of supplied data.
The only test-side I/O is the surface-drift baseline read (and its `BLESS_SURFACE=1` write), the established pattern.

**Testing**: Expecto + FsCheck, exercising the **public** surface (`AdvisoryPromotion.decide` / `satisfiedBases` /
unwrappers and the `Model` types) over real, literally-constructible values (Principle V — every value is a genuine
typed token: a real F030 `EvidenceRef`, literal counts/thresholds, a literal `SignOff`; no mock, no clock read, no
model invoked, no file read, no bytes hashed). Concerns: (1) **advisory by default** — no basis ⇒ `StaysAdvisory
NoPermittedBasis`; confidence-below-threshold ⇒ `StaysAdvisory (ConfidenceBelowThreshold …)`; self-confidence never
promotes (SC-001, US1); (2) **eligible on a permitted basis, all named** — one basis ⇒ `EligibleToBlock` naming it;
two or three ⇒ all named, in fixed order (SC-002, US2); (3) **inclusive `>=` with the no-single-sample floor** — the
confidence basis is satisfied exactly when `count >= threshold && count >= 2`, verified across counts below, equal
to, and above the threshold, and never for a lone review (SC-003, edge cases); (4) **totality** — a decision is
returned and never throws across the full cross-product of basis presence/absence and any non-negative count against
any threshold (SC-004, US3); (5) **determinism / purity** — equal facts ⇒ equal decision under changed cwd / time /
filesystem, no I/O (SC-005, US3); (6) **necessary-not-sufficient** — an `EligibleToBlock` value carries no blocking
action and no calibration claim (SC-006, US3); (7) **non-empty eligibility** — `EligibleToBlock` is unrepresentable
with an empty basis set (FR-001); (8) **surface drift + scope hygiene** — the assembly references only
`EvidenceReuse` (+ allowed transitive cores) (Principle II, SC-007). Advisory-default, totality, determinism,
all-named, and the confidence comparator laws are FsCheck properties; the worked examples are pinned to
[contracts/advisory-promotion-api.md](./contracts/advisory-promotion-api.md), plus the FSI proof.

**Target Platform**: Developer / CI .NET SDK running `dotnet test`. No host, no CLI, no OS-specific surface.

**Project Type**: A new pure-core F# library + its test project. No host, no CLI, no MVU.

**Performance Goals**: N/A. The contract is **advisory-by-default safety, totality, determinism, and the correct
three-basis logic**, not latency; the decision is a small computation over a handful of supplied facts (Spec
Assumptions: *"Determinism is the contract, not performance"*).

**Constraints**: Pure / total / deterministic (FR-006): reads no clock, filesystem, git, environment, or network;
invokes no model / agent; computes no hash from raw bytes; runs no review; makes no cache-key / verdict-store /
lookup / invalidation operation; builds no review record; measures no elapsed time; spawns no process; persists
nothing. Treats the finding's verdict as an opaque fact — never produced, interpreted, compared, re-scored, or
thresholded (FR-007). An *eligible to block* decision is necessary-not-sufficient: it carries no blocking action and
asserts no calibration (FR-008). Identical supplied inputs always yield an identical decision. The merged cores and
baselines are not modified (FR-009 / SC-007).

**Scale/Scope**: One new `src/` library (`AdvisoryPromotion` — `Model.fsi/fs` + `AdvisoryPromotion.fsi/fs`); one new
test project; one new surface baseline `surface/FS.GG.Governance.AdvisoryPromotion.surface.txt`; two solution
entries; a short `scripts/prelude.fsx` FSI section (design-first proof, Principle I); the `CLAUDE.md` plan pointer.
Zero changes to existing `src/`, `surface/`, or merged test projects.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | The public surface is drafted as `Model.fsi` + `AdvisoryPromotion.fsi` and exercised in `scripts/prelude.fsx` (a new F039 section) before any `.fs` body exists; semantic tests call the public functions, never private helpers. |
| II. Visibility in `.fsi` | **PASS** | Two curated `.fsi` files are the sole public-surface declaration; the `.fs` files carry no access modifiers, and the `confidenceMet` helper stays unexposed by its absence from the `.fsi`. A new `surface/FS.GG.Governance.AdvisoryPromotion.surface.txt` baseline is added and guarded by a reflective `SurfaceDrift` test (the F029–F038 precedent), with the `BLESS_SURFACE=1` re-bless path. |
| III. Idiomatic Simplicity | **PASS** | Plain records + single-case newtypes + two small unions; the decision is a `[ … ]` list comprehension of satisfied bases + one `match` (no SRTP, reflection outside the surface test, custom operators, type providers, or non-trivial CEs). The one reused token (`EvidenceRef`) is opened, not re-modeled (research D3). |
| IV. Elmish/MVU is the boundary for stateful/I/O | **N/A** | No state, no I/O, no workflow — a pure total decision over supplied values. Like F023/F030/F036, this is a pure decision core needing no MVU ceremony. The *actual* review, sensing whether deterministic evidence exists, counting independent reviews, and capturing a human sign-off are a later host edge (Principle IV), explicitly out of scope. |
| V. Test Evidence Is Mandatory | **PASS** | Every input is a real, literally-constructible typed value (a real F030 `EvidenceRef`, literal counts/thresholds, a literal `SignOff`); no clock read, no model invoked, no file read, no bytes hashed, no mock used. Tests fail before the implementation matches the contract and pass after. No mocks ⇒ no `Synthetic` disclosure needed. |
| VI. Observability & Safe Failure | **PASS** | The function is total: no exception, no swallowed failure, no silent drop. Every combination — no basis, confidence below/at/above threshold, a lone review, a zero/absent count, one/two/three bases at once — is an ordinary named decision (Edge Cases), and the *stays advisory* outcome always names its reason (the no-hide rule). |
| Change Classification | **Tier 1 (contracted change — new public API)** | Adds a new public module/assembly and a new surface baseline ⇒ full chain: spec, plan, `.fsi`, baseline, tests. **No new third-party dependency.** No existing public API, baseline, or merged behavior is altered (F030 `EvidenceRef` consumed verbatim, not modified). |
| Engineering Constraints | **PASS** | F#/.NET `net10.0`; no new third-party `PackageReference` (FR-011); references only the sibling pure core `EvidenceReuse` (F030) — and its transitive cores `FreshnessKey` / `Config`, unused here — no git / filesystem scanning / host / CLI. No rendering package IDs/paths/templates assumed — inputs are product-neutral supplied values. Pack output + structured-logging TODOs unaffected (no runtime/host code). |

**Gate result: PASS — no unjustified violations. Complexity Tracking is empty.** Principle IV is the only N/A (no
stateful/I/O workflow); I, II, III, V, VI all have concrete targets and pass. The single sibling reference (research
D3) reuses the F030 `EvidenceRef` token the spec names, pulls in nothing impure, and is the only cross-core
coupling; minting a fourth redundant token was the considered alternative (research D3).

## Project Structure

### Documentation (this feature)

```text
specs/039-advisory-promotion-gate/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 — decisions D1–D7 + the advisory-default / three-basis / no-hide facts
├── data-model.md        # Phase 1 — PromotionBasis, ConfirmationCount, ConfidenceThreshold, SignOff,
│                        #            AdvisoryReason, PromotionFacts, PromotionDecision (reuses F030 EvidenceRef)
├── quickstart.md        # Phase 1 — how to build, FSI-exercise, test, and re-bless the surface
├── contracts/           # Phase 1 — the contracts this row commits
│   └── advisory-promotion-api.md         # the public signatures + their laws (advisory-default, all-named, comparator) + the scope guard
├── checklists/          # (present) spec-quality checklist
└── tasks.md             # Phase 2 — /speckit-tasks output (NOT created here)
```

### Source Code / deliverable layout (repository root)

```text
src/FS.GG.Governance.AdvisoryPromotion/                # NEW — the pure advisory-promotion decision core
├── Model.fsi                                          # NEW — PromotionBasis, ConfirmationCount, ConfidenceThreshold,
│                                                      #       SignOff, AdvisoryReason, PromotionFacts, PromotionDecision
│                                                      #       (sole public surface; reuses F030 EvidenceRef verbatim)
├── Model.fs                                           # NEW — the matching type defns (no access modifiers)
├── AdvisoryPromotion.fsi                              # NEW — decide / satisfiedBases / signOffValue / confirmationValue / thresholdValue
├── AdvisoryPromotion.fs                               # NEW — the pure, total decision body + the confidenceMet helper (private by omission)
└── FS.GG.Governance.AdvisoryPromotion.fsproj          # NEW — packable; references EvidenceReuse; BCL + FSharp.Core

tests/FS.GG.Governance.AdvisoryPromotion.Tests/        # NEW — semantic tests over the PUBLIC surface (Expecto + FsCheck)
├── Support.fs                                          # NEW — real literal builders + FsCheck generators (no mocks)
├── AdvisoryDefaultTests.fs                             # NEW — US1: no basis ⇒ advisory; below-threshold ⇒ ConfidenceBelowThreshold; self-confidence never promotes (SC-001)
├── EligibilityTests.fs                                 # NEW — US2: one basis ⇒ eligible naming it; two/three ⇒ all named, fixed order (SC-002)
├── ConfidenceComparatorTests.fs                       # NEW — SC-003: count vs threshold below/equal/above + the no-single-sample floor
├── TotalityTests.fs                                    # NEW — US3: a decision always returned, never throws, across the cross-product (SC-004)
├── DeterminismTests.fs                                 # NEW — US3: equal facts ⇒ equal decision under changed cwd/time/fs; no I/O (SC-005)
├── NecessaryNotSufficientTests.fs                      # NEW — US3: EligibleToBlock carries no blocking action / no calibration claim (SC-006)
├── NonEmptyEligibilityTests.fs                         # NEW — FR-001: EligibleToBlock is unrepresentable with an empty basis set
├── SurfaceDriftTests.fs                                # NEW — Principle II surface baseline + EvidenceReuse-only scope guard
├── Main.fs                                             # NEW — Expecto entry point
└── FS.GG.Governance.AdvisoryPromotion.Tests.fsproj     # NEW — references AdvisoryPromotion (+ EvidenceReuse for the token); test packages

surface/FS.GG.Governance.AdvisoryPromotion.surface.txt  # NEW — Tier-1 public-surface baseline (BLESS_SURFACE=1 generated)
scripts/prelude.fsx                                     # EDIT — append a short F039 FSI section (design-first proof)
FS.GG.Governance.sln                                    # EDIT — add the two new projects
CLAUDE.md                                               # EDIT — point the SPECKIT plan reference at this plan
```

**Structure Decision**: One new pure-core F# library `src/FS.GG.Governance.AdvisoryPromotion` (the established
one-new-minimal-core-per-row rhythm, research D1), compiled `Model → AdvisoryPromotion`, referencing the sibling
pure core `EvidenceReuse` (F030) only to reuse the opaque `EvidenceRef` token for the backing-evidence basis
(research D3). This is the F036 single-sibling-reference shape, specialised from *verdict reuse* to *advisory
promotion*. A sibling test project exercises the public surface with real literal values. The library is additive:
no existing `src/`, `surface/`, or merged test project changes.

## Complexity Tracking

> No Constitution Check violations. This section is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
