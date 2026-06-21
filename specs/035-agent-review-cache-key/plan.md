# Implementation Plan: Agent-Review Verdict Cache-Key Core

**Branch**: `035-agent-review-cache-key` | **Date**: 2026-06-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/035-agent-review-cache-key/spec.md`

## Summary

Open **Phase 12 (Agent-Reviewed Rule Guardrails)** with its **first** line — *"Cache agent-reviewed verdicts by
model id, model version, reviewer prompt hash, model configuration, check hash, artifact hashes, and question
text"* (design `docs/initial-implementation-plan.md`; phase purpose: *"allow judgement-heavy checks without
treating uncalibrated agent output as deterministic proof"*). Continuing this repo's maintainer-confirmed
**pure-core-first** rhythm (F015–F034 each landed a pure, total, deterministic core before any host edge consumed
it), and sliced exactly as the Phase-11 cache pair was (F029 `FreshnessKey` defined the deterministic-evidence
key before F030 `EvidenceReuse` consumed it), this row delivers a single new pure core,
**`FS.GG.Governance.AgentReviewKey`**, that answers one question deterministically: *"Given the seven
already-formed identity tokens an agent-reviewed verdict depends on, what is the byte-stable, injective cache key
under which that verdict is cached and found again, and exactly which inputs differ between two such requests?"*

This is the **direct analogue of F029 `FreshnessKey`**, specialised to agent-reviewed verdicts. F029 keyed
deterministic-evidence reuse over its closed input set with `compute` / `matches` / `diff`; this row keys an
**agent-reviewed verdict** over the seven judge / prompt / check / artifact inputs the design names, with the
**same shape**. A later row (Phase 12's second line, *"Invalidate cached verdicts when judge identity or prompt
identity changes"*) is the F030 analogue that adds the verdict store + invalidation decision that consumes this
key.

The core reads **no clock**, invokes **no model / agent / network**, computes **no hash from raw bytes** (the
hashes are supplied, already-computed tokens), reads **no filesystem / git / environment**, persists **no
artifact**, carries **no cached verdict** and runs **no cache store / lookup / invalidation operation**, makes
**no advisory-vs-blocking promotion decision**, and adds **no CLI**. Its sole outputs are the typed cache-key
inputs, the computed key value, the `matches` predicate, and the `diff` explanation.

The core provides (full vocabulary in [data-model.md](./data-model.md); the key bytes in
[contracts/agent-review-key-format.md](./contracts/agent-review-key-format.md); the signatures + laws in
[contracts/agent-review-key-api.md](./contracts/agent-review-key-api.md)):

- **`AgentReviewInputs`** — one flat closed record carrying the **seven** inputs an agent-reviewed verdict
  depends on: model id, model version, reviewer prompt hash, model configuration, check hash, the **set** of
  reviewed-artifact hashes, and the question text. Each is an already-formed opaque token supplied by the edge
  (FR-001).
- **Five new opaque newtypes** — `ModelId`, `ModelVersion`, `ReviewerPromptHash`, `ModelConfig`, `QuestionText`
  (each `of string`), because no type for any of these exists yet (research D2). The **check hash** reuses F029's
  **`RuleHash`** verbatim and the **reviewed-artifact hashes** reuse F029's **`ArtifactHash`** verbatim (FR-008,
  research D2) — so the core references exactly one sibling core, `FS.GG.Governance.FreshnessKey` (research D1).
- **`CacheKey`** = `CacheKey of string` — the deterministic, byte-stable, injective canonical key computed over
  the seven inputs (named `CacheKey`, **not** `Key`, to avoid collision with F029's `Key` brought in by the
  `open` — research D3).
- **`ReviewInput`** — the closed seven-case enumeration of comparable inputs returned by `diff`, with
  `inputToken : ReviewInput -> string` (`"modelId"` / `"checkHash"` / …), a total injective readable token
  (research D3), deliberately distinct from the terse internal encoding tags.
- **`AgentReviewKey.compute`** — the total, pure projection of `AgentReviewInputs` to its canonical `CacheKey`
  in the F029/F032/F033 tagged, length-prefixed, **injective** discipline; reviewed-artifact hashes keyed as a
  **set** (deduped, ordinal-sorted) so order and duplication never change the key (FR-002, FR-003, FR-006).
- **`AgentReviewKey.matches`** — the total cache-hit predicate, defined as `compute a = compute b` so the
  predicate and the key can never disagree (FR-004).
- **`AgentReviewKey.diff`** — the total no-hide explainer: exactly the `ReviewInput`s whose values differ between
  two input sets, in fixed encoding order; empty iff `matches` (FR-005). The observable face of *"a judge or
  prompt change invalidates prior cached verdicts."*
- **`AgentReviewKey.value`** — unwrap a `CacheKey` to its canonical string.

This row carries and stores **no cached verdict** and runs **no cache store / lookup / invalidation operation**
(Phase 12 row 2), does **not** separate governed artifact content from reviewer instructions (row 3), does
**not** record review requests / response digests (row 4), promotes **no** finding from advisory to blocking
(row 5), and defines **no** judge-vs-human calibration (row 6). It invokes no model, computes no digest from raw
bytes, performs no persistence, and adds no CLI. The merged cores and their `surface/*.surface.txt` baselines are
**untouched**; `dotnet build` / `dotnet test` over existing projects stays unchanged, and the new project + its
test project are purely additive (SC-007).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (repo standard; `Nullable=enable`, `TreatWarningsAsErrors=true`
inherited from `Directory.Build.props`). One new `src/` library with two curated `.fsi` files, plus one new test
project.

**Primary Dependencies**: **`FS.GG.Governance.FreshnessKey`** (F029), referenced to reuse `RuleHash` (check hash)
and `ArtifactHash` (reviewed-artifact hashes) verbatim — FR-008, research D1/D2. **No new third-party
`PackageReference`** (FR-011): the keying is plain `string` building + `FSharp.Core` only; the transitive
`YamlDotNet` arriving via `Config` (through `FreshnessKey`) is unused. Test frameworks already on the central
feed (`Directory.Packages.props`): **Expecto**, **Expecto.FsCheck**, **FsCheck**, **Microsoft.NET.Test.Sdk**,
**YoloDev.Expecto.TestSdk**.

**Storage**: None. No database, no files, no runtime storage — the seven tokens are in-value inputs. The only
test-side I/O is the surface-drift baseline read (and its `BLESS_SURFACE=1` write), the established pattern.

**Testing**: Expecto + FsCheck, exercising the **public** surface (`AgentReviewKey.compute` / `matches` / `diff`
/ `value` and `Model.inputToken`) over real, literally-constructible values (Principle V — every value is a
genuine typed token built from literals, including real F029 `RuleHash`/`ArtifactHash`s; no mock, no clock read,
no model invoked, no process spawned). Concerns: (1) **the key incorporates all seven inputs and changes when any
single one changes** (SC-001); (2) **reviewed-artifact hashes are keyed as a set** — order- and
duplicate-insensitive, empty set distinct (SC-002); (3) **`diff` names exactly the differing inputs and no
others** (SC-003); (4) **determinism** — key/`matches`/`diff` byte-identical on repeat (SC-004); (5) **injective,
unspoofable encoding** — separator-bearing tokens, empty tokens, and a token equal to another input's text never
collide or spoof a boundary (SC-005); (6) **purity** under changed cwd / time / filesystem (SC-006); (7)
**surface drift + scope hygiene** — the assembly references only `FreshnessKey` / `Config` / BCL / `FSharp.Core`
(Principle II, SC-007). The injectivity, determinism, purity, set-semantics, and totality laws are FsCheck
properties; the field-carriage and edge cases are example tests, including a byte-exact worked-example key pinned
to `contracts/agent-review-key-format.md` and the FSI proof.

**Target Platform**: Developer / CI .NET SDK running `dotnet test`. No host, no CLI, no OS-specific surface.

**Project Type**: A new pure-core F# library + its test project. No host, no CLI, no MVU.

**Performance Goals**: N/A. The contract is **determinism and totality**, not latency; a review cache holds a
modest number of keyed verdicts (Spec Assumptions: *"Determinism is the contract, not performance"*).

**Constraints**: Pure / total / deterministic (FR-007): reads no clock, filesystem, git, environment, or
network; invokes no model / agent, computes no hash from raw bytes, measures no elapsed time, spawns no process,
captures no bytes; identical supplied inputs always yield an identical key, comparison, and difference. Carries no
verdict and runs no cache operation (FR-009). The merged cores and baselines are not modified (FR-008 / SC-007).

**Scale/Scope**: One new `src/` library (`AgentReviewKey` — `Model.fsi/fs` + `AgentReviewKey.fsi/fs`); one new
test project; one new surface baseline `surface/FS.GG.Governance.AgentReviewKey.surface.txt`; two solution
entries; a short `scripts/prelude.fsx` FSI section (design-first proof, Principle I); the `CLAUDE.md` plan
pointer. Zero changes to existing `src/`, `surface/`, or merged test projects.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | The public surface is drafted as `Model.fsi` + `AgentReviewKey.fsi` and exercised in `scripts/prelude.fsx` (a new F035 section) before any `.fs` body exists; semantic tests call the public functions, never private helpers. |
| II. Visibility in `.fsi` | **PASS** | Two curated `.fsi` files are the sole public-surface declaration; the `.fs` files carry no access modifiers. A new `surface/FS.GG.Governance.AgentReviewKey.surface.txt` baseline is added and guarded by a reflective `SurfaceDrift` test (the F029–F034 precedent), with the `BLESS_SURFACE=1` re-bless path. |
| III. Idiomatic Simplicity | **PASS — load-bearing** | Plain record + closed DU, five minimal new newtypes, and BCL string building (the length-prefixed segments via `System.Text.StringBuilder`, per task T012) joined with `\n`. No SRTP, reflection (outside the surface test), custom operators, type providers, or non-trivial CEs. `RuleHash` / `ArtifactHash` are reused verbatim from F029 (D2), not re-modeled. |
| IV. Elmish/MVU is the boundary for stateful/I/O | **N/A** | No state, no I/O, no workflow — pure total functions over supplied tokens. Like F019 `Route`, F029 `FreshnessKey`, F030–F034, this is a pure projection needing no MVU ceremony. The *actual* review (sending the prompt to a model, receiving a verdict) and the production of these digests are a later host edge (Principle IV), explicitly out of scope. |
| V. Test Evidence Is Mandatory | **PASS** | Every input is a real, literally-constructible typed token (including real F029 `RuleHash`/`ArtifactHash`s); no clock read, no model invoked, no process spawned, no mock used. Tests fail before the implementation matches the contract and pass after. No mocks ⇒ no `Synthetic` disclosure needed. |
| VI. Observability & Safe Failure | **PASS** | The functions are total: no exception, no swallowed failure, no silent truncation. An empty artifact set, an empty token (empty question / config), a token equal to another input's text, a token containing the encoding's separator/tag characters, and a several-inputs-differ pair are all ordinary complete values (FR-003, Edge cases); empty-string tokens are literal values that each encode to a distinct, unambiguous segment, never colliding with absence or another field. |
| Change Classification | **Tier 1 (contracted change — new public API)** | Adds a new public module/assembly and a new surface baseline ⇒ full chain: spec, plan, `.fsi`, baseline, tests. **No new third-party dependency.** No existing public API, baseline, or merged behavior is altered (F029 `RuleHash`/`ArtifactHash` consumed verbatim, not modified). |
| Engineering Constraints | **PASS** | F#/.NET `net10.0`; no new third-party `PackageReference` (FR-011); references only the sibling pure core `FreshnessKey` (which owns `RuleHash`/`ArtifactHash`) — no git / filesystem scanning / Snapshot / host / CLI. No rendering package IDs/paths/templates assumed — inputs are product-neutral supplied tokens. Pack output + structured-logging TODOs unaffected (no runtime/host code). |

**Gate result: PASS — no unjustified violations. Complexity Tracking is empty.** Principle IV is the only N/A
(no stateful/I/O workflow); I, II, III, V, VI all have concrete targets and pass. The single sibling reference
(D1) is mandated by the verbatim-reuse requirement FR-008 and pulls in nothing impure, so it is not a complexity
violation.

## Project Structure

### Documentation (this feature)

```text
specs/035-agent-review-cache-key/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 — decisions D1–D6 + the keying semantics facts
├── data-model.md        # Phase 1 — ModelId, ModelVersion, ReviewerPromptHash, ModelConfig, QuestionText,
│                        #            AgentReviewInputs, CacheKey, ReviewInput (reuses F029 RuleHash/ArtifactHash)
├── quickstart.md        # Phase 1 — how to build, FSI-exercise, test, and re-bless the surface
├── contracts/           # Phase 1 — the contracts this row commits
│   ├── agent-review-key-api.md       # the public function signatures + their laws + inputToken table
│   └── agent-review-key-format.md    # the canonical cache-key byte encoding (F029/F032/F033 discipline)
├── checklists/
│   └── requirements.md  # spec quality checklist (if present)
└── tasks.md             # Phase 2 — /speckit-tasks output (NOT created here)
```

### Source Code / deliverable layout (repository root)

```text
src/FS.GG.Governance.AgentReviewKey/                  # NEW — the pure agent-review verdict cache-key core
├── Model.fsi                                          # NEW — ModelId, ModelVersion, ReviewerPromptHash,
│                                                      #       ModelConfig, QuestionText, AgentReviewInputs,
│                                                      #       CacheKey, ReviewInput, inputToken (sole public
│                                                      #       surface; reuses F029 RuleHash/ArtifactHash verbatim)
├── Model.fs                                           # NEW — the matching newtype/record/DU definitions (no access modifiers)
├── AgentReviewKey.fsi                                 # NEW — compute / matches / diff / value (sole operations surface)
├── AgentReviewKey.fs                                  # NEW — the pure, total length-prefixed keying bodies
└── FS.GG.Governance.AgentReviewKey.fsproj            # NEW — packable; references ONLY FreshnessKey; BCL + FSharp.Core

tests/FS.GG.Governance.AgentReviewKey.Tests/          # NEW — semantic tests over the PUBLIC surface (Expecto + FsCheck)
├── Support.fs                                          # NEW — real literal builders + FsCheck generators (no mocks)
├── ComputeTests.fs                                     # NEW — US1: seven-input carriage, single-field distinction (SC-001)
├── SetSemanticsTests.fs                                # NEW — US3: artifact set order/dup-insensitivity, empty set (SC-002)
├── DiffTests.fs                                        # NEW — US2: matches IFF all equal; diff names exactly the differing inputs (SC-003)
├── InjectivityTests.fs                                 # NEW — US1/US2: cross-input injectivity, unspoofable encoding (SC-005)
├── DeterminismTests.fs                                 # NEW — US3: byte-equality on repeat compute/matches/diff (SC-004)
├── PurityTests.fs                                      # NEW — US3: identical under changed cwd/time/fs (SC-006)
├── SurfaceDriftTests.fs                                # NEW — Principle II surface baseline + FreshnessKey-only scope guard
├── Main.fs                                             # NEW — Expecto entry point
└── FS.GG.Governance.AgentReviewKey.Tests.fsproj        # NEW — references AgentReviewKey + FreshnessKey; test packages

surface/FS.GG.Governance.AgentReviewKey.surface.txt    # NEW — Tier-1 public-surface baseline (BLESS_SURFACE=1 generated)
scripts/prelude.fsx                                     # EDIT — append a short F035 FSI section (design-first proof)
FS.GG.Governance.sln                                   # EDIT — add the two new projects
CLAUDE.md                                               # EDIT — point the SPECKIT plan reference at this plan
```

**Structure Decision**: One new pure-core F# library `src/FS.GG.Governance.AgentReviewKey` (the established
one-new-minimal-core-per-row rhythm, D1), compiled `Model → AgentReviewKey`, referencing only the sibling pure
core `FreshnessKey` that owns F029's `RuleHash` and `ArtifactHash` (FR-008). A sibling test project exercises the
public surface with real literal tokens. The library is additive: no existing `src/`, `surface/`, or merged test
project changes.

## Complexity Tracking

> No Constitution Check violations. This section is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
