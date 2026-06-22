# Implementation Plan: Agent-Reviewed Verdict Store & Invalidation Decision Core

**Branch**: `036-agent-verdict-invalidation` | **Date**: 2026-06-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/036-agent-verdict-invalidation/spec.md`

## Summary

Land **Phase 12 (Agent-Reviewed Rule Guardrails)**'s **second** line — *"Invalidate cached verdicts when judge
identity or prompt identity changes"* (design `docs/initial-implementation-plan.md`). F035
(`FS.GG.Governance.AgentReviewKey`) already landed the phase's **first** line as a pure core: the typed
`AgentReviewInputs` over the seven judge / prompt / check / artifact inputs, the byte-stable injective `CacheKey`,
and `compute` / `matches` / `diff`. This row adds the **verdict store + lookup / invalidation decision** that
consumes F035's `matches` / `diff` **verbatim** — exactly as F030 (`EvidenceReuse`) added the store + reuse
decision that consumed F029's `matches` / `diff`.

This is the **direct analogue of F030 `EvidenceReuse`**, specialised to agent-reviewed verdicts:

| Phase 11 (deterministic evidence) | Phase 12 (agent-reviewed verdicts) |
|---|---|
| F029 `FreshnessKey` — `FreshnessInputs`, `matches`, `diff` | F035 `AgentReviewKey` — `AgentReviewInputs`, `matches`, `diff` |
| F030 `EvidenceReuse` — `ReuseStore`, `decide`, `record` | **F036 `VerdictReuse` — `VerdictStore`, `lookup`, `record`** |
| `EvidenceRef` (opaque, edge-minted) | `VerdictRef` (opaque, edge-minted) |
| `Reuse` / `Recompute (NoPriorEvidence \| InputsChanged)` | `Valid` / `Invalidated (NoCachedVerdict \| InputsChanged)` |

Continuing this repo's maintainer-confirmed **pure-core-first** rhythm (F015–F035 each landed a pure, total,
deterministic core before any host edge consumed it), this row delivers a single new packable pure core,
**`FS.GG.Governance.VerdictReuse`**, that answers one operational question deterministically: *"Given a new
agent-review request's `AgentReviewInputs` and a store of previously cached verdicts, is a cached verdict still
**valid** for reuse — and when it is **not**, **why** was it invalidated (which judge / prompt / check / artifact
identity changed, or no cached verdict for this work)?"*

The core performs **no persistence** (no filesystem/database read or write), **no eviction/expiry/size limit**,
computes **no key bytes** itself (F035 owns the key), invokes **no model / agent**, reads **no clock / filesystem
/ git / environment / network**, carries **no verdict content** (the verdict reference is an opaque token), runs
**no actual review**, makes **no advisory-vs-blocking promotion** decision, and adds **no CLI**. Its sole outputs
are the lookup decision value and the new verdict-store value.

The core provides (full vocabulary in [data-model.md](./data-model.md); the decision tables in
[contracts/lookup-decision-semantics.md](./contracts/lookup-decision-semantics.md); the signatures + laws in
[contracts/verdict-store-api.md](./contracts/verdict-store-api.md)):

- **`VerdictRef`** = `VerdictRef of string` — a thin, opaque, comparable newtype: a handle to an
  already-cached agent-reviewed verdict, minted at the edge and supplied as data. The core never parses,
  validates, produces, or dereferences it, and never reads its advisory/blocking content (FR-001, research D3 —
  the F030 `EvidenceRef` precedent).
- **`CachedVerdict`** = `{ Inputs: AgentReviewInputs; Verdict: VerdictRef }` — one cached entry: the F035 seven-
  input identity a verdict was produced under, paired with its opaque reference (FR-001).
- **`VerdictStore`** = `VerdictStore of CachedVerdict list` — the immutable, in-value collection of cached
  entries (newest-first by `record` convention); not a live cache, connection, or file (FR-002, research D4).
- **`InvalidationCause`** = `NoCachedVerdict | InputsChanged of ReviewInput list` — the no-hide explanation
  (FR-006): either "no cached verdict for the request's work" or the **non-empty** list of differing F035
  `ReviewInput`s (which never contains `CheckHashInput` — the work key, equal by construction; research D5).
- **`LookupDecision`** = `Valid of VerdictRef | Invalidated of InvalidationCause` — the total result of `lookup`
  (FR-003).
- **`IdentityGroup`** = `JudgeIdentity | PromptIdentity | CheckArtifactIdentity` with
  **`inputGroup : ReviewInput -> IdentityGroup`** — the total projection that attributes each differing input to
  its identity group, so a judge change and a prompt change are each *visible as such* (FR-006, SC-002, US2).
  The analogue of F035's `inputToken`.
- **`VerdictReuse.lookup`** — the single pure, total decision: `Valid ref` **iff** some cached entry
  `AgentReviewKey.matches` the request on **every** one of the seven inputs (FR-004, the dual of "invalidate when
  judge or prompt identity changes"); otherwise `Invalidated cause` with a located cause (FR-006).
- **`VerdictReuse.record`** — the pure, total, de-duplicating insert (most-recent-wins, no mutation): refreshes a
  matching entry rather than accumulating duplicates (FR-007, FR-008).
- **`VerdictReuse.empty` / `entries` / `referenceValue`** — the empty store, the entries accessor, and the
  `VerdictRef` unwrapper (for inspection / messages / tests).

Validity is **exactly** F035 `matches`; the explanation is **exactly** F035 `diff`, grouped by `inputGroup`
(research D2). The merged cores (including F035) and their `surface/*.surface.txt` baselines are **untouched**;
`dotnet build` / `dotnet test` over existing projects stays unchanged, and the new project + its test project are
purely additive (SC-007).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (repo standard; `Nullable=enable`, `TreatWarningsAsErrors=true`
inherited from `Directory.Build.props`). One new `src/` library with two curated `.fsi` files, plus one new test
project.

**Primary Dependencies**: **`FS.GG.Governance.AgentReviewKey`** (F035), referenced to reuse `AgentReviewInputs`,
`ReviewInput`, `AgentReviewKey.matches`, and `AgentReviewKey.diff` **verbatim** (FR-010, research D1/D2). The
F029 `RuleHash`/`ArtifactHash` and F014 typed facts arrive **transitively through** F035 — this core names them
only via `request.Check` field equality (the work key) and never adds a direct `FreshnessKey`/`Config` project
reference. **No new third-party `PackageReference`** (FR-014): the decision is plain list/`option` handling +
`FSharp.Core` only. Test frameworks already on the central feed (`Directory.Packages.props`): **Expecto**,
**Expecto.FsCheck**, **FsCheck**, **Microsoft.NET.Test.Sdk**, **YoloDev.Expecto.TestSdk**.

**Storage**: None. No database, no files, no runtime storage — the verdict store is an in-value `VerdictStore`
handed in and returned. The only test-side I/O is the surface-drift baseline read (and its `BLESS_SURFACE=1`
write), the established pattern.

**Testing**: Expecto + FsCheck, exercising the **public** surface (`VerdictReuse.lookup` / `record` / `empty` /
`entries` / `referenceValue` and `Model.inputGroup`) over real, literally-constructible values (Principle V —
every value is a genuine typed token built from literals, including real F035 `AgentReviewInputs` over real F029
`RuleHash`/`ArtifactHash`s and literal `VerdictRef`s; no mock, no clock read, no model invoked, no process
spawned). Concerns: (1) **Valid iff all seven inputs match** and **single-field change ⇒ Invalidated**, for every
F035 input (SC-001); (2) **judge / prompt change ⇒ Invalidated, attributed to the right group** via `inputGroup`
(SC-002); (3) **every Invalidated carries a located, non-empty cause** — `NoCachedVerdict` vs `InputsChanged`
(SC-003); (4) **determinism** — repeated `lookup` byte-identical, artifact reorder/dup invariant (SC-004); (5)
**record→lookup, refresh/de-dup most-recent-wins, independence, no mutation** (SC-005); (6) **purity** under
changed cwd / time / filesystem (SC-006); (7) **surface drift + scope hygiene** — the assembly references only
`AgentReviewKey` / `FreshnessKey` / `Config` (transitive) / BCL / `FSharp.Core` (Principle II, SC-007). The
match/diff-reuse, determinism, purity, set-semantics, refresh, and totality laws are FsCheck properties; the
decision tables and edge cases are example tests pinned to
[contracts/lookup-decision-semantics.md](./contracts/lookup-decision-semantics.md), plus the FSI proof.

**Target Platform**: Developer / CI .NET SDK running `dotnet test`. No host, no CLI, no OS-specific surface.

**Project Type**: A new pure-core F# library + its test project. No host, no CLI, no MVU.

**Performance Goals**: N/A. The contract is **determinism and totality**, not latency; a verdict store holds a
modest number of cached verdicts per check (Spec Assumptions: *"Determinism is the contract, not performance"*).

**Constraints**: Pure / total / deterministic (FR-009): reads no clock, filesystem, git, environment, or network;
invokes no model / agent; computes no hash or key bytes from raw bytes; measures no elapsed time; spawns no
process; captures no bytes. Carries no verdict content and performs no persistence/eviction/expiry/promotion
(FR-011). Identical request + identical store always yields the identical decision; identical starting store +
identical recording sequence always yields an equivalent store. The merged cores and baselines are not modified
(FR-010 / SC-007).

**Scale/Scope**: One new `src/` library (`VerdictReuse` — `Model.fsi/fs` + `VerdictReuse.fsi/fs`); one new test
project; one new surface baseline `surface/FS.GG.Governance.VerdictReuse.surface.txt`; two solution entries; a
short `scripts/prelude.fsx` FSI section (design-first proof, Principle I); the `CLAUDE.md` plan pointer. Zero
changes to existing `src/`, `surface/`, or merged test projects.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | The public surface is drafted as `Model.fsi` + `VerdictReuse.fsi` and exercised in `scripts/prelude.fsx` (a new F036 section) before any `.fs` body exists; semantic tests call the public functions, never private helpers. |
| II. Visibility in `.fsi` | **PASS** | Two curated `.fsi` files are the sole public-surface declaration; the `.fs` files carry no access modifiers. A new `surface/FS.GG.Governance.VerdictReuse.surface.txt` baseline is added and guarded by a reflective `SurfaceDrift` test (the F029–F035 precedent), with the `BLESS_SURFACE=1` re-bless path. |
| III. Idiomatic Simplicity | **PASS** | Plain records + closed DUs and BCL list/`option` handling (`List.tryFind` head-first, `List.filter` to de-dup, cons to prepend) — the F030 shape verbatim. No SRTP, reflection (outside the surface test), custom operators, type providers, or non-trivial CEs. `AgentReviewInputs` / `ReviewInput` / `matches` / `diff` are reused from F035 (D2), not re-modeled. |
| IV. Elmish/MVU is the boundary for stateful/I/O | **N/A** | No state, no I/O, no workflow — pure total functions over a supplied store value. Like F019/F029/F030/F035, this is a pure decision needing no MVU ceremony. The *actual* review (sending the prompt to a model, receiving a verdict) and the *persistence* of the store are a later host edge (Principle IV), explicitly out of scope. |
| V. Test Evidence Is Mandatory | **PASS** | Every input is a real, literally-constructible typed value (real F035 `AgentReviewInputs`, literal `VerdictRef`s); no clock read, no model invoked, no process spawned, no mock used. Tests fail before the implementation matches the contract and pass after. No mocks ⇒ no `Synthetic` disclosure needed. |
| VI. Observability & Safe Failure | **PASS** | The functions are total: no exception, no swallowed failure, no silent truncation. An empty store, an empty/unusual `VerdictRef`, a several-inputs-differ request, and a no-entry-for-this-work request are all ordinary complete values; every `Invalidated` carries a present, non-ambiguous cause (FR-006, FR-012), distinguishing "no cached verdict for this work" from "the identity changed" (the genuine-defect-vs-missing-input distinction). |
| Change Classification | **Tier 1 (contracted change — new public API)** | Adds a new public module/assembly and a new surface baseline ⇒ full chain: spec, plan, `.fsi`, baseline, tests. **No new third-party dependency.** No existing public API, baseline, or merged behavior is altered (F035 `AgentReviewInputs`/`matches`/`diff` consumed verbatim, not modified). |
| Engineering Constraints | **PASS** | F#/.NET `net10.0`; no new third-party `PackageReference` (FR-014); references only the sibling pure core `AgentReviewKey` (which owns the input vocabulary and `matches`/`diff`) — no git / filesystem scanning / Snapshot / host / CLI. No rendering package IDs/paths/templates assumed — inputs are product-neutral supplied values. Pack output + structured-logging TODOs unaffected (no runtime/host code). |

**Gate result: PASS — no unjustified violations. Complexity Tracking is empty.** Principle IV is the only N/A
(no stateful/I/O workflow); I, II, III, V, VI all have concrete targets and pass. The single sibling reference
(D1) is mandated by the verbatim-reuse requirement FR-010 and pulls in nothing impure, so it is not a complexity
violation.

## Project Structure

### Documentation (this feature)

```text
specs/036-agent-verdict-invalidation/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 — decisions D1–D6 + the lookup/record semantics facts
├── data-model.md        # Phase 1 — VerdictRef, CachedVerdict, VerdictStore, InvalidationCause,
│                        #            LookupDecision, IdentityGroup, inputGroup (reuses F035 AgentReviewInputs/ReviewInput)
├── quickstart.md        # Phase 1 — how to build, FSI-exercise, test, and re-bless the surface
├── contracts/           # Phase 1 — the contracts this row commits
│   ├── verdict-store-api.md           # the public function signatures + their laws + inputGroup table
│   └── lookup-decision-semantics.md   # the lookup/record decision tables (the F030 discipline)
├── checklists/
│   └── requirements.md  # spec quality checklist (if present)
└── tasks.md             # Phase 2 — /speckit-tasks output (NOT created here)
```

### Source Code / deliverable layout (repository root)

```text
src/FS.GG.Governance.VerdictReuse/                    # NEW — the pure verdict store + invalidation decision core
├── Model.fsi                                          # NEW — VerdictRef, CachedVerdict, VerdictStore,
│                                                      #       InvalidationCause, LookupDecision, IdentityGroup,
│                                                      #       inputGroup (sole public surface; reuses F035
│                                                      #       AgentReviewInputs/ReviewInput verbatim)
├── Model.fs                                           # NEW — the matching newtype/record/DU definitions (no access modifiers)
├── VerdictReuse.fsi                                   # NEW — empty / record / lookup / entries / referenceValue (sole operations surface)
├── VerdictReuse.fs                                    # NEW — the pure, total lookup/record bodies
└── FS.GG.Governance.VerdictReuse.fsproj              # NEW — packable; references ONLY AgentReviewKey; BCL + FSharp.Core

tests/FS.GG.Governance.VerdictReuse.Tests/            # NEW — semantic tests over the PUBLIC surface (Expecto + FsCheck)
├── Support.fs                                          # NEW — real literal builders + FsCheck generators (no mocks)
├── LookupDecisionTests.fs                              # NEW — US1: Valid iff all seven match; single-field change ⇒ Invalidated (SC-001)
├── ExplanationTests.fs                                 # NEW — US2: located cause; NoCachedVerdict vs InputsChanged; group attribution (SC-002, SC-003)
├── EmptyStoreTests.fs                                  # NEW — edge: empty store ⇒ Invalidated NoCachedVerdict (SC-001/SC-003)
├── RecordTests.fs                                      # NEW — US3: record→lookup, refresh/de-dup, independence, no mutation (SC-005)
├── DeterminismTests.fs                                 # NEW — US1/US3: byte-equal repeat lookup; artifact reorder/dup invariant (SC-004)
├── PurityTests.fs                                      # NEW — SC-006: identical under changed cwd/time/fs
├── SurfaceDriftTests.fs                                # NEW — Principle II surface baseline + AgentReviewKey-only scope guard
├── Main.fs                                             # NEW — Expecto entry point
└── FS.GG.Governance.VerdictReuse.Tests.fsproj          # NEW — references VerdictReuse + AgentReviewKey; test packages

surface/FS.GG.Governance.VerdictReuse.surface.txt      # NEW — Tier-1 public-surface baseline (BLESS_SURFACE=1 generated)
scripts/prelude.fsx                                     # EDIT — append a short F036 FSI section (design-first proof)
FS.GG.Governance.sln                                   # EDIT — add the two new projects
CLAUDE.md                                               # EDIT — point the SPECKIT plan reference at this plan
```

**Structure Decision**: One new pure-core F# library `src/FS.GG.Governance.VerdictReuse` (the established
one-new-minimal-core-per-row rhythm, D1), compiled `Model → VerdictReuse`, referencing only the sibling pure core
`AgentReviewKey` (F035) that owns `AgentReviewInputs`, `ReviewInput`, `matches`, and `diff` (FR-010). A sibling
test project exercises the public surface with real literal values. The library is additive: no existing `src/`,
`surface/`, or merged test project changes.

## Complexity Tracking

> No Constitution Check violations. This section is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
