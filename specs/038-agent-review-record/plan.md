# Implementation Plan: Agent-Review Record — Auditable Review-Record Core

**Branch**: `038-agent-review-record` | **Date**: 2026-06-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/038-agent-review-record/spec.md`

## Summary

Land **Phase 12 (Agent-Reviewed Rule Guardrails)**'s **fourth** line — *"Record review requests, response digests,
model identity, prompt identity, artifact digests, and final verdict"* (design `docs/initial-implementation-plan.md`;
`docs/initial-design.md`, *Optional agent-reviewed constraints*, the **Auditability** row). F035
(`FS.GG.Governance.AgentReviewKey`), F036 (`FS.GG.Governance.VerdictReuse`), and F037
(`FS.GG.Governance.PromptIsolation`) landed the phase's first three lines. This row delivers the design's
**auditability** guardrail as a pure core: a typed value that captures one completed agent review as an immutable,
auditable record — the F037 review request, the supplied response digest, the F035 model identity, the F035 prompt
identity, the F029 reviewed-artifact digests, and the final recorded verdict — and derives a deterministic,
byte-stable, **injective** record identity over its reproducible facts.

Continuing this repo's maintainer-confirmed **pure-core-first** rhythm (F015–F037 each landed a pure, total,
deterministic core before any host edge consumed it), this row delivers a single new packable pure core,
**`FS.GG.Governance.ReviewRecord`** — the **agent-review analogue of F032 `CommandRecord` / F033 `Provenance`**: a
record-building core that assembles a complete typed record from already-sensed facts and derives a canonical
identity over its **reproducible** facts, holding sensed metadata structurally apart (the F032/F033 honesty
boundary).

| Phase-12 row | Core | Question it answers |
|---|---|---|
| 1 — cache key (F035) | `AgentReviewKey` | *Under what identity is a verdict cached?* |
| 2 — invalidation (F036) | `VerdictReuse` | *Is a cached verdict still valid, and if not, why?* |
| 3 — prompt isolation (F037) | `PromptIsolation` | *How is the request shaped so the artifact is data, not an instruction?* |
| **4 — review record (this row)** | **`ReviewRecord`** | ***What was this completed review — request, judge, artifacts, response, verdict — for the audit trail?*** |

The core performs **no model / agent invocation**, reads **no clock / filesystem / git / environment / network**,
computes **no hash from raw bytes** (every digest is a supplied token), runs **no actual review**, performs **no
cache lookup / verdict invalidation** (F035 / F036, consumed by neighbours, not by this core), makes **no
advisory-vs-blocking promotion** (the fifth row), defines **no calibration** (the sixth row), performs **no
persistence / JSON projection**, and adds **no CLI**. Its sole outputs are the typed `ReviewRecord` value and its
deterministic `RecordIdentity`.

The core provides (full vocabulary in [data-model.md](./data-model.md); the identity format in
[contracts/review-record-identity-format.md](./contracts/review-record-identity-format.md); the signatures + laws
in [contracts/review-record-api.md](./contracts/review-record-api.md)):

- **`ResponseDigest`** = `ResponseDigest of string` — the supplied hash of the reviewer's response; opaque,
  carries no response bytes (FR-001, research D2/D3).
- **`RecordedVerdict`** = `RecordedVerdict of string` — the final verdict, an **opaque recorded fact** never
  interpreted, compared, thresholded, or promoted (FR-007, research D3).
- **`RecordIdentity`** = `RecordIdentity of string` — the byte-stable canonical identity (mirrors F032
  `CommandIdentity` / F033 `ProvenanceIdentity`) (FR-003).
- **`ReproducibleFacts`** = `{ Request: ReviewRequest; Model: ModelId; ModelVersion: ModelVersion; PromptHash:
  ReviewerPromptHash; ReviewedArtifacts: ArtifactHash list; ResponseDigest: ResponseDigest; Verdict:
  RecordedVerdict }` — the **six** named audit facts (FR-001), the sole input to `canonicalId` (research D2/D5).
- **`ReviewRecord`** = `{ Reproducible: ReproducibleFacts; Sensed: SensedMetadatum list }` — the complete record;
  the F034 sensed metadata is held structurally apart and excluded from identity (FR-004, research D6).
- **`ReviewRecord.build`** — the total curried assembly of the six facts + sensed metadata (sensed last — the F032
  convention) (FR-002, research D7).
- **`ReviewRecord.canonicalId`** — the pure, total, injective render over the reproducible facts; the `req` segment
  is F037's own injective rendering of the embedded request (FR-003, research D5).
- **`ReviewRecord.identityValue`** — the `RecordIdentity` unwrapper.

The record reuses **F037 `ReviewRequest`**, **F035 `ModelId`/`ModelVersion`/`ReviewerPromptHash`**, **F029
`ArtifactHash`**, and **F034 `SensedMetadatum`** verbatim (research D2), introducing only the three new types
above. The merged cores (including F029 / F032 / F033 / F034 / F035 / F037) and their `surface/*.surface.txt`
baselines are **untouched**; `dotnet build` / `dotnet test` over existing projects stays unchanged, and the new
project + its test project are purely additive (SC-006).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (repo standard; `Nullable=enable`, `TreatWarningsAsErrors=true`
inherited from `Directory.Build.props`). One new `src/` library with two curated `.fsi` files, plus one new test
project.

**Primary Dependencies**: Two `ProjectReference`s — **`FS.GG.Governance.PromptIsolation`** (F037), to reuse
**`ReviewRequest`** + **`PromptIsolation.render`** directly and the F035 **`ModelId`/`ModelVersion`/
`ReviewerPromptHash`** and F029 **`ArtifactHash`** transitively (F037 → F035 → F029); and
**`FS.GG.Governance.SensedMetadata`** (F034), to reuse **`SensedMetadatum`** + `markTimestamp`/`markDuration`. This
is the F033 multi-sibling-reference shape (research D1). **No new third-party `PackageReference`** (FR-009): the
build and identity derivation are plain `System.Text`/BCL string building + `FSharp.Core` + the reused vocabulary.
Test frameworks already on the central feed (`Directory.Packages.props`): **Expecto**, **Expecto.FsCheck**,
**FsCheck**, **Microsoft.NET.Test.Sdk**, **YoloDev.Expecto.TestSdk**.

**Storage**: None. No database, no files, no runtime storage — the record and its identity are in-value results of
supplied data. The only test-side I/O is the surface-drift baseline read (and its `BLESS_SURFACE=1` write), the
established pattern.

**Testing**: Expecto + FsCheck, exercising the **public** surface (`ReviewRecord.build` / `canonicalId` /
`identityValue` and the `Model` types) over real, literally-constructible values (Principle V — every value is a
genuine typed token: real F037 `ReviewRequest`, real F035 `ModelId`/`ModelVersion`/`ReviewerPromptHash`, real F029
`ArtifactHash`, real F034 `SensedMetadatum`, and literal digest/verdict strings; no mock, no clock read, no model
invoked, no file read, no bytes hashed). Concerns: (1) **faithful capture** — all six facts read back exactly as
supplied, none dropped/altered/invented; zero-artifact / empty-digest / empty-verdict / empty-sensed records valid
(SC-001, US1); (2) **identity injectivity** — identical reproducible facts ⇒ byte-identical identity, any single
differing reproducible fact ⇒ different identity (SC-002, US2); (3) **sensed boundary** — records differing only in
`Sensed` ⇒ identical identity (SC-003, US2); (4) **no raw bytes** — only digests + the F037-bounded request, no
raw response/artifact content form (SC-004, US3); (5) **artifact set** — reordered/duplicated digests ⇒ identical
identity (D4); (6) **determinism / purity** under changed cwd / time / filesystem (SC-005); (7) **surface drift +
scope hygiene** — the assembly references only `PromptIsolation`/`SensedMetadata` (+ allowed transitive cores)
(Principle II, SC-006). Faithful-carriage, totality, determinism, injectivity, set-invariance, and sensed-exclusion
laws are FsCheck properties; the identity format and edge cases are example tests pinned to
[contracts/review-record-identity-format.md](./contracts/review-record-identity-format.md), plus the FSI proof.

**Target Platform**: Developer / CI .NET SDK running `dotnet test`. No host, no CLI, no OS-specific surface.

**Project Type**: A new pure-core F# library + its test project. No host, no CLI, no MVU.

**Performance Goals**: N/A. The contract is **faithful capture, totality, determinism, and injectivity**, not
latency; a record holds a modest number of supplied facts (Spec Assumptions: *"Determinism is the contract, not
performance"*).

**Constraints**: Pure / total / deterministic (FR-005): reads no clock, filesystem, git, environment, or network;
invokes no model / agent; computes no hash from raw bytes (digests are supplied tokens); measures no elapsed time;
spawns no process; persists nothing. The identity excludes sensed metadata (FR-004). Identical supplied inputs
always yield an identical record and an identical identity. The merged cores and baselines are not modified (FR-009
/ SC-006).

**Scale/Scope**: One new `src/` library (`ReviewRecord` — `Model.fsi/fs` + `ReviewRecord.fsi/fs`); one new test
project; one new surface baseline `surface/FS.GG.Governance.ReviewRecord.surface.txt`; two solution entries; a
short `scripts/prelude.fsx` FSI section (design-first proof, Principle I); the `CLAUDE.md` plan pointer. Zero
changes to existing `src/`, `surface/`, or merged test projects.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | The public surface is drafted as `Model.fsi` + `ReviewRecord.fsi` and exercised in `scripts/prelude.fsx` (a new F038 section) before any `.fs` body exists; semantic tests call the public functions, never private helpers. |
| II. Visibility in `.fsi` | **PASS** | Two curated `.fsi` files are the sole public-surface declaration; the `.fs` files carry no access modifiers. A new `surface/FS.GG.Governance.ReviewRecord.surface.txt` baseline is added and guarded by a reflective `SurfaceDrift` test (the F029–F037 precedent), with the `BLESS_SURFACE=1` re-bless path. |
| III. Idiomatic Simplicity | **PASS** | Plain records + three single-case newtypes; `System.Text.StringBuilder` length-prefixed string building (the F035 `seg`/`artSegment` idiom verbatim) + `PromptIsolation.render` reused for the request segment. No SRTP, reflection (outside the surface test), custom operators, type providers, or non-trivial CEs. All reused vocabulary is opened, not re-modeled (research D2). |
| IV. Elmish/MVU is the boundary for stateful/I/O | **N/A** | No state, no I/O, no workflow — pure total functions over supplied values. Like F032/F033/F035/F037, this is a pure record/identity core needing no MVU ceremony. The *actual* review (sending a request to a model, receiving a verdict), computing the response digest from bytes, and sensing the review's timestamp are a later host edge (Principle IV), explicitly out of scope. |
| V. Test Evidence Is Mandatory | **PASS** | Every input is a real, literally-constructible typed value (real F037 `ReviewRequest`, real F035/F029/F034 vocabulary, literal digest/verdict strings); no clock read, no model invoked, no file read, no bytes hashed, no mock used. Tests fail before the implementation matches the contract and pass after. No mocks ⇒ no `Synthetic` disclosure needed. |
| VI. Observability & Safe Failure | **PASS** | The functions are total: no exception, no swallowed failure, no silent drop. A zero-artifact request, an empty response digest, an empty verdict, an empty sensed list, and digests/verdicts containing tag/separator/fence characters are all ordinary complete values, rendered unambiguously by length prefix (Edge Cases). |
| Change Classification | **Tier 1 (contracted change — new public API)** | Adds a new public module/assembly and a new surface baseline ⇒ full chain: spec, plan, `.fsi`, baseline, tests. **No new third-party dependency.** No existing public API, baseline, or merged behavior is altered (F037 `ReviewRequest`, F035 model/prompt vocabulary, F029 `ArtifactHash`, F034 `SensedMetadatum` consumed verbatim, not modified). |
| Engineering Constraints | **PASS** | F#/.NET `net10.0`; no new third-party `PackageReference` (FR-009); references only the sibling pure cores `PromptIsolation` (F037) and `SensedMetadata` (F034) — and their transitive cores `AgentReviewKey`/`FreshnessKey`/`CommandRecord`/`Config` — no git / filesystem scanning / host / CLI. No rendering package IDs/paths/templates assumed — inputs are product-neutral supplied values. Pack output + structured-logging TODOs unaffected (no runtime/host code). |

**Gate result: PASS — no unjustified violations. Complexity Tracking is empty.** Principle IV is the only N/A (no
stateful/I/O workflow); I, II, III, V, VI all have concrete targets and pass. The two sibling references (research
D1) are the F033 multi-sibling-reference precedent, mandated by the verbatim-reuse requirement FR-006 and pulling
in nothing impure, so they are not a complexity violation. Carrying sensed metadata (research D6) reuses F034 to
make the F032/F033 honesty boundary demonstrable, not added cleverness.

## Project Structure

### Documentation (this feature)

```text
specs/038-agent-review-record/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 — decisions D1–D7 + the capture/identity/sensed-boundary facts
├── data-model.md        # Phase 1 — ResponseDigest, RecordedVerdict, RecordIdentity, ReproducibleFacts,
│                        #            ReviewRecord (reuses F037 ReviewRequest, F035 model/prompt, F029
│                        #            ArtifactHash, F034 SensedMetadatum)
├── quickstart.md        # Phase 1 — how to build, FSI-exercise, test, and re-bless the surface
├── contracts/           # Phase 1 — the contracts this row commits
│   ├── review-record-api.md             # the public function signatures + their laws + the scope guard
│   └── review-record-identity-format.md # the canonical injective, length-prefixed identity (the F032/F035 discipline)
└── tasks.md             # Phase 2 — /speckit-tasks output (NOT created here)
```

### Source Code / deliverable layout (repository root)

```text
src/FS.GG.Governance.ReviewRecord/                    # NEW — the pure auditable review-record core
├── Model.fsi                                          # NEW — ResponseDigest, RecordedVerdict, RecordIdentity,
│                                                      #       ReproducibleFacts, ReviewRecord (sole public surface;
│                                                      #       reuses F037 ReviewRequest, F035 model/prompt, F029
│                                                      #       ArtifactHash, F034 SensedMetadatum verbatim)
├── Model.fs                                           # NEW — the matching type defns (no access modifiers)
├── ReviewRecord.fsi                                   # NEW — build / canonicalId / identityValue (sole operations surface)
├── ReviewRecord.fs                                    # NEW — the pure, total assembly + injective identity bodies
└── FS.GG.Governance.ReviewRecord.fsproj              # NEW — packable; references PromptIsolation + SensedMetadata; BCL + FSharp.Core

tests/FS.GG.Governance.ReviewRecord.Tests/            # NEW — semantic tests over the PUBLIC surface (Expecto + FsCheck)
├── Support.fs                                          # NEW — real literal builders + FsCheck generators (no mocks)
├── CaptureTests.fs                                     # NEW — US1: all six facts read back; zero-artifact/empty cases (SC-001)
├── IdentityTests.fs                                    # NEW — US2: identical facts ⇒ equal id; any differing fact ⇒ different id (SC-002)
├── SensedBoundaryTests.fs                              # NEW — US2: records differing only in Sensed ⇒ equal id (SC-003)
├── IdentityFormatTests.fs                              # NEW — worked-example pins to review-record-identity-format.md
├── ArtifactSetTests.fs                                 # NEW — D4: reordered/duplicated digests ⇒ equal id
├── NoBytesTests.fs                                     # NEW — US3: only digests + F037-bounded request; no raw-content form (SC-004)
├── DeterminismTests.fs                                 # NEW — byte-equal repeat build+id; purity under changed cwd/time/fs (SC-005)
├── SurfaceDriftTests.fs                                # NEW — Principle II surface baseline + PromptIsolation/SensedMetadata scope guard
├── Main.fs                                             # NEW — Expecto entry point
└── FS.GG.Governance.ReviewRecord.Tests.fsproj          # NEW — references ReviewRecord (+ siblings for builders); test packages

surface/FS.GG.Governance.ReviewRecord.surface.txt      # NEW — Tier-1 public-surface baseline (BLESS_SURFACE=1 generated)
scripts/prelude.fsx                                     # EDIT — append a short F038 FSI section (design-first proof)
FS.GG.Governance.sln                                    # EDIT — add the two new projects
CLAUDE.md                                               # EDIT — point the SPECKIT plan reference at this plan
```

**Structure Decision**: One new pure-core F# library `src/FS.GG.Governance.ReviewRecord` (the established
one-new-minimal-core-per-row rhythm, research D1), compiled `Model → ReviewRecord`, referencing the sibling pure
cores `PromptIsolation` (F037) and `SensedMetadata` (F034) — which provide `ReviewRequest` + `render` and
`SensedMetadatum` directly, and F035's model/prompt vocabulary + F029's `ArtifactHash` transitively (FR-006). This
is the F033 multi-sibling-reference shape, specialised from *build provenance* to *review record*. A sibling test
project exercises the public surface with real literal values. The library is additive: no existing `src/`,
`surface/`, or merged test project changes.

## Complexity Tracking

> No Constitution Check violations. This section is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
