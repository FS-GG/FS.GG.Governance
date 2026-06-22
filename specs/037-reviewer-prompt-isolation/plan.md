# Implementation Plan: Reviewer-Prompt Isolation — Governed-Artifact-as-Data Core

**Branch**: `037-reviewer-prompt-isolation` | **Date**: 2026-06-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/037-reviewer-prompt-isolation/spec.md`

## Summary

Land **Phase 12 (Agent-Reviewed Rule Guardrails)**'s **third** line — *"Separate governed artifact content from
reviewer instructions and pass it as bounded data or digests"* (design `docs/initial-implementation-plan.md`;
`docs/initial-design.md`, *Optional agent-reviewed constraints*). F035 (`FS.GG.Governance.AgentReviewKey`) landed
the phase's first line (the agent-review cache key) and F036 (`FS.GG.Governance.VerdictReuse`) landed the second
(the verdict store + invalidation decision). This row delivers the design's **prompt-injection** guardrail as a
pure core: a typed value that structurally keeps trusted **reviewer instructions** and untrusted
**governed-artifact content** in *separate channels*, carries each artifact only as a **bounded excerpt or a
digest**, and renders the two channels with an **injective, unspoofable data fence** — the F029 / F032 / F035
tagged, length-prefixed encoding discipline, applied to the *prompt* rather than to a *key*.

Continuing this repo's maintainer-confirmed **pure-core-first** rhythm (F015–F036 each landed a pure, total,
deterministic core before any host edge consumed it), this row delivers a single new packable pure core,
**`FS.GG.Governance.PromptIsolation`**, that answers one question deterministically over supplied values: *"Given
trusted reviewer instructions and a sequence of governed artifacts (each already captured as a bounded excerpt or
supplied as a digest), how is the review request shaped and rendered so the artifact is always data and can never
masquerade as an instruction?"*

This is the **structural sibling of F035** (both are Phase-12 cores reusing F029's `ArtifactHash`), specialised to
*prompt shape* rather than *cache identity*:

| Phase-12 row | Core | Question it answers |
|---|---|---|
| 1 — cache key (F035) | `AgentReviewKey` | *Under what identity is a verdict cached?* |
| 2 — invalidation (F036) | `VerdictReuse` | *Is a cached verdict still valid, and if not, why?* |
| **3 — prompt isolation (this row)** | **`PromptIsolation`** | ***How is the request shaped so the artifact is data, not an instruction?*** |

The core performs **no model / agent invocation**, reads **no clock / filesystem / git / environment / network**,
computes **no hash from raw bytes** (digests are supplied tokens), runs **no actual review**, produces **no
verdict / cache key / cached-verdict store** (F035 / F036, consumed by neighbours, not by this core), writes **no
review record / provenance** (the fourth row), makes **no advisory-vs-blocking promotion** (the fifth row),
defines **no calibration** (the sixth row), performs **no persistence**, and adds **no CLI**. Its sole outputs are
the typed `ReviewRequest` value and its deterministic `RenderedPrompt`.

The core provides (full vocabulary in [data-model.md](./data-model.md); the rendered-fence format in
[contracts/render-format.md](./contracts/render-format.md); the signatures + laws in
[contracts/prompt-isolation-api.md](./contracts/prompt-isolation-api.md)):

- **`SizeBound`** = `SizeBound of int` — the declared maximum size of a bounded excerpt, in **characters** (UTF-16
  code units / .NET `String.Length`); a supplied non-negative bound (a negative bound is clamped to zero so
  capture stays total) (FR-003, research D4).
- **`Truncation`** = `Whole | Truncated` — whether an excerpt was truncated to its bound. Never silent (Principle
  VI, FR-003).
- **`BoundedExcerpt`** — an **abstract** type (no public representation) whose *only* constructor is the smart
  constructor `excerpt`, which captures supplied content into the declared bound and marks its truncation status.
  Abstraction is what makes "no excerpt exceeds its bound" and "no form carries raw, unbounded content" hold **by
  construction** (FR-002, FR-003, research D3).
- **`ArtifactPayload`** = `Excerpt of BoundedExcerpt | DigestOnly of ArtifactHash` — the **closed two-form**
  carrier of one artifact in the data channel: a bounded excerpt (content as data, within bound) or a digest only
  (the supplied F029 `ArtifactHash`, carrying no bytes). There is no third, unbounded form (FR-002, research D2).
- **`ReviewRequest`** = `{ Instructions: QuestionText; Artifacts: ArtifactPayload list }` — the assembled value
  pairing the **trusted instruction channel** (reused F035 `QuestionText`) with the **ordered data channel** (the
  artifact payloads, order- and duplicate-preserving). The two channels are different shapes; there is no
  constructor placing artifact content into the instruction field — separation **by construction** (FR-001,
  research D1/D2).
- **`RenderedPrompt`** = `RenderedPrompt of string` — the deterministic, byte-stable, injective serialization of a
  `ReviewRequest`, with an explicit, unspoofable fence between the instruction channel and the data channel (the
  F029 / F032 / F035 tagged, length-prefixed discipline) (FR-005, research D5).
- **`PromptIsolation.assemble`** — the total assembly of a `ReviewRequest` from instructions + an artifact
  sequence, total over the empty sequence and all boundary-length / empty content (FR-004).
- **`PromptIsolation.render`** — the single pure, total, injective render to a `RenderedPrompt` (FR-005, FR-006).
- **`Model.excerpt` / `excerptContent` / `excerptBound` / `excerptTruncation`** — the bounded-capture constructor
  and the excerpt accessors (the abstract type's companion surface).
- **`PromptIsolation.renderedValue`** — the `RenderedPrompt` unwrapper (for handoff / messages / tests).

Separation is **exactly** the typed channel split (no constructor crosses it); bounding is **exactly** the
abstract excerpt + its smart constructor (no over-bound or unbounded form exists); the fence is **exactly** the
F035 length-prefixed encoding, applied per channel and per payload (research D2/D5). The merged cores (including
F029 / F035 / F036) and their `surface/*.surface.txt` baselines are **untouched**; `dotnet build` / `dotnet test`
over existing projects stays unchanged, and the new project + its test project are purely additive (SC-006).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (repo standard; `Nullable=enable`, `TreatWarningsAsErrors=true`
inherited from `Directory.Build.props`). One new `src/` library with two curated `.fsi` files, plus one new test
project.

**Primary Dependencies**: **`FS.GG.Governance.AgentReviewKey`** (F035), referenced to reuse **`QuestionText`** (the
reviewer-instruction channel, FR-007) directly and **`ArtifactHash`** (the digest-only form, FR-007) transitively
through F035's own `FreshnessKey` reference — the same single-sibling-reference shape F036 used (research D1). **No
new third-party `PackageReference`** (FR-010): the assembly and render are plain `System.Text`/BCL string building
+ `FSharp.Core` only. Test frameworks already on the central feed (`Directory.Packages.props`): **Expecto**,
**Expecto.FsCheck**, **FsCheck**, **Microsoft.NET.Test.Sdk**, **YoloDev.Expecto.TestSdk**.

**Storage**: None. No database, no files, no runtime storage — the review request and its rendering are in-value
results of supplied data. The only test-side I/O is the surface-drift baseline read (and its `BLESS_SURFACE=1`
write), the established pattern.

**Testing**: Expecto + FsCheck, exercising the **public** surface (`PromptIsolation.assemble` / `render` /
`renderedValue` and `Model.excerpt` / `excerptContent` / `excerptBound` / `excerptTruncation`) over real,
literally-constructible values (Principle V — every value is a genuine typed token built from literals: real F035
`QuestionText`, real F029 `ArtifactHash`, and literal content strings; no mock, no clock read, no model invoked,
no file read, no bytes hashed). Concerns: (1) **channel separation** — assembled instruction channel is exactly
the supplied instructions and the data channel is exactly the supplied payloads, including when content imitates
an instruction (SC-001, US1); (2) **bounded capture** — at/under bound carried whole + not-truncated, over bound
truncated to the bound + marked truncated, no over-bound excerpt, digest-only carries no bytes (SC-002, US2); (3)
**injective fence** — content containing the fence markers / separators / tag characters / instruction-imitating
text stays in the data channel and cannot terminate it, forge a field, or open the instruction channel (SC-003,
US3); (4) **determinism** — repeated assemble+render byte-identical, order-preserving, duplicate-preserving
(SC-004); (5) **purity** under changed cwd / time / filesystem (SC-005); (6) **surface drift + scope hygiene** —
the assembly references only `AgentReviewKey` / `FreshnessKey` / `Config` (transitive) / BCL / `FSharp.Core`
(Principle II, SC-006). Bounding, totality, determinism, injectivity, and order-preservation laws are FsCheck
properties; the render format and edge cases are example tests pinned to
[contracts/render-format.md](./contracts/render-format.md), plus the FSI proof.

**Target Platform**: Developer / CI .NET SDK running `dotnet test`. No host, no CLI, no OS-specific surface.

**Project Type**: A new pure-core F# library + its test project. No host, no CLI, no MVU.

**Performance Goals**: N/A. The contract is **determinism, totality, bounding, and injectivity**, not latency; a
review request holds a modest number of bounded artifact payloads (Spec Assumptions: *"Determinism is the
contract, not performance"*).

**Constraints**: Pure / total / deterministic (FR-006): reads no clock, filesystem, git, environment, or network;
invokes no model / agent; computes no hash or key bytes from raw bytes (digests are supplied tokens); measures no
elapsed time; spawns no process; persists nothing. Every excerpt is within its declared bound with explicit
truncation status; no form carries raw, unbounded content (FR-002, FR-003). Identical supplied inputs always
yield an identical assembled request and an identical rendering. The merged cores and baselines are not modified
(FR-010 / SC-006).

**Scale/Scope**: One new `src/` library (`PromptIsolation` — `Model.fsi/fs` + `PromptIsolation.fsi/fs`); one new
test project; one new surface baseline `surface/FS.GG.Governance.PromptIsolation.surface.txt`; two solution
entries; a short `scripts/prelude.fsx` FSI section (design-first proof, Principle I); the `CLAUDE.md` plan
pointer. Zero changes to existing `src/`, `surface/`, or merged test projects.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | The public surface is drafted as `Model.fsi` + `PromptIsolation.fsi` and exercised in `scripts/prelude.fsx` (a new F037 section) before any `.fs` body exists; semantic tests call the public functions, never private helpers. |
| II. Visibility in `.fsi` | **PASS** | Two curated `.fsi` files are the sole public-surface declaration; the `.fs` files carry no access modifiers. `BoundedExcerpt` is declared **abstract** in the `.fsi` (representation hidden) so bounding is enforced by construction. A new `surface/FS.GG.Governance.PromptIsolation.surface.txt` baseline is added and guarded by a reflective `SurfaceDrift` test (the F029–F036 precedent), with the `BLESS_SURFACE=1` re-bless path. |
| III. Idiomatic Simplicity | **PASS** | Plain records + closed DUs + one abstract type with a smart constructor; `System.Text.StringBuilder` length-prefixed string building (the F035 idiom verbatim). No SRTP, reflection (outside the surface test), custom operators, type providers, or non-trivial CEs. `QuestionText` / `ArtifactHash` are reused from F035 / F029 (D2), not re-modeled. |
| IV. Elmish/MVU is the boundary for stateful/I/O | **N/A** | No state, no I/O, no workflow — pure total functions over supplied values. Like F019/F029/F035/F036, this is a pure shaping/rendering needing no MVU ceremony. The *actual* review (sending the rendered prompt to a model, receiving a verdict), reading an artifact from disk, and computing its digest are a later host edge (Principle IV), explicitly out of scope. |
| V. Test Evidence Is Mandatory | **PASS** | Every input is a real, literally-constructible typed value (real F035 `QuestionText`, real F029 `ArtifactHash`, literal content strings); no clock read, no model invoked, no file read, no bytes hashed, no mock used. Tests fail before the implementation matches the contract and pass after. No mocks ⇒ no `Synthetic` disclosure needed. |
| VI. Observability & Safe Failure | **PASS** | The functions are total: no exception, no swallowed failure, no silent truncation — truncation is always explicitly marked `Truncated` (FR-003). An empty excerpt, a zero bound, a zero-artifact request, an empty/unusual digest, and content imitating an instruction are all ordinary complete values rendered unambiguously; the empty-excerpt, digest-only, and absent-artifact cases are each distinct in the rendering (Edge Cases). |
| Change Classification | **Tier 1 (contracted change — new public API)** | Adds a new public module/assembly and a new surface baseline ⇒ full chain: spec, plan, `.fsi`, baseline, tests. **No new third-party dependency.** No existing public API, baseline, or merged behavior is altered (F035 `QuestionText` / F029 `ArtifactHash` consumed verbatim, not modified). |
| Engineering Constraints | **PASS** | F#/.NET `net10.0`; no new third-party `PackageReference` (FR-010); references only the sibling pure core `AgentReviewKey` (which provides `QuestionText` and, transitively, `ArtifactHash`) — no git / filesystem scanning / Snapshot / host / CLI. No rendering package IDs/paths/templates assumed — inputs are product-neutral supplied values. Pack output + structured-logging TODOs unaffected (no runtime/host code). |

**Gate result: PASS — no unjustified violations. Complexity Tracking is empty.** Principle IV is the only N/A
(no stateful/I/O workflow); I, II, III, V, VI all have concrete targets and pass. The single sibling reference
(D1) is mandated by the verbatim-reuse requirement FR-007 and pulls in nothing impure, so it is not a complexity
violation. `BoundedExcerpt`'s abstraction (D3) is the minimal mechanism that makes FR-002/FR-003 structural rather
than conventional, not added cleverness.

## Project Structure

### Documentation (this feature)

```text
specs/037-reviewer-prompt-isolation/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 — decisions D1–D6 + the separation/bounding/fence facts
├── data-model.md        # Phase 1 — SizeBound, Truncation, BoundedExcerpt (abstract), ArtifactPayload,
│                        #            ReviewRequest, RenderedPrompt (reuses F035 QuestionText, F029 ArtifactHash)
├── quickstart.md        # Phase 1 — how to build, FSI-exercise, test, and re-bless the surface
├── contracts/           # Phase 1 — the contracts this row commits
│   ├── prompt-isolation-api.md   # the public function signatures + their laws + the scope guard
│   └── render-format.md          # the canonical injective, length-prefixed render (the F035 discipline)
└── tasks.md             # Phase 2 — /speckit-tasks output (NOT created here)
```

### Source Code / deliverable layout (repository root)

```text
src/FS.GG.Governance.PromptIsolation/                 # NEW — the pure prompt-isolation core
├── Model.fsi                                          # NEW — SizeBound, Truncation, BoundedExcerpt (abstract) +
│                                                      #       excerpt/accessors, ArtifactPayload, ReviewRequest,
│                                                      #       RenderedPrompt (sole public surface; reuses F035
│                                                      #       QuestionText + F029 ArtifactHash verbatim)
├── Model.fs                                           # NEW — the matching type defns + bounded-capture body (no access modifiers)
├── PromptIsolation.fsi                                # NEW — assemble / render / renderedValue (sole operations surface)
├── PromptIsolation.fs                                 # NEW — the pure, total assembly + injective render bodies
└── FS.GG.Governance.PromptIsolation.fsproj           # NEW — packable; references ONLY AgentReviewKey; BCL + FSharp.Core

tests/FS.GG.Governance.PromptIsolation.Tests/         # NEW — semantic tests over the PUBLIC surface (Expecto + FsCheck)
├── Support.fs                                          # NEW — real literal builders + FsCheck generators (no mocks)
├── ChannelSeparationTests.fs                           # NEW — US1: instruction vs data channel; instruction-imitating content stays data (SC-001)
├── BoundedCaptureTests.fs                              # NEW — US2: at/under/over bound, marked truncation, no over-bound, digest-only no bytes (SC-002)
├── RenderFenceTests.fs                                 # NEW — US3: injective fence; markers/separators/forged instructions read as data (SC-003)
├── DeterminismTests.fs                                 # NEW — US3: byte-equal repeat render; order/duplicate preserved (SC-004)
├── PurityTests.fs                                      # NEW — SC-005: identical under changed cwd/time/fs
├── EdgeCaseTests.fs                                    # NEW — empty excerpt, zero bound, zero artifacts, empty digest (Edge Cases)
├── SurfaceDriftTests.fs                                # NEW — Principle II surface baseline + AgentReviewKey-only scope guard
├── Main.fs                                             # NEW — Expecto entry point
└── FS.GG.Governance.PromptIsolation.Tests.fsproj       # NEW — references PromptIsolation + AgentReviewKey; test packages

surface/FS.GG.Governance.PromptIsolation.surface.txt   # NEW — Tier-1 public-surface baseline (BLESS_SURFACE=1 generated)
scripts/prelude.fsx                                     # EDIT — append a short F037 FSI section (design-first proof)
FS.GG.Governance.sln                                    # EDIT — add the two new projects
CLAUDE.md                                               # EDIT — point the SPECKIT plan reference at this plan
```

**Structure Decision**: One new pure-core F# library `src/FS.GG.Governance.PromptIsolation` (the established
one-new-minimal-core-per-row rhythm, D1), compiled `Model → PromptIsolation`, referencing only the sibling pure
core `AgentReviewKey` (F035) — which provides `QuestionText` directly and `ArtifactHash` transitively (FR-007).
A sibling test project exercises the public surface with real literal values. The library is additive: no existing
`src/`, `surface/`, or merged test project changes.

## Complexity Tracking

> No Constitution Check violations. This section is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
