# Implementation Plan: Sensed-Metadata Marking Core

**Branch**: `034-sensed-metadata-marking` | **Date**: 2026-06-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/034-sensed-metadata-marking/spec.md`

## Summary

Land **Phase 11 (Cost, Cache, and Provenance)** row 6 — its **sixth and final** line — *"Mark wall-clock
timestamps and durations as sensed or non-deterministic metadata when included in deterministic reports"*
(design `docs/initial-implementation-plan.md`; Phase-11 exit criterion: *"audit records are sufficient to
explain builds, tests, packs, … — including how long things took and when they ran, honestly flagged"*).
Continuing this repo's maintainer-confirmed **pure-core-first** rhythm (F015–F033 each landed a pure, total,
deterministic core before any host edge consumed it), this row delivers a single new pure core,
**`FS.GG.Governance.SensedMetadata`**, that answers one question deterministically: *"Given an already-measured
wall-clock timestamp or duration, what is the typed sensed-metadatum value that marks it sensed by
construction, and what is its unambiguously-flagged, byte-stable rendering for inclusion in a deterministic
report?"*

The **structural half** of the honesty rule already exists — F032 holds a command run's measured `Duration` in
a distinct `SensedDuration` field apart from its reproducible facts, and F033 folds only each command record's
reproducible identity into the provenance canonical identity, so durations are *structurally* excluded from
identity. What is **missing** is the **presentation half**: a single, shared, deterministic way to *surface* a
timestamp or duration inside a deterministic report **with an explicit "sensed / non-deterministic" marker**, so
every later rendering row (a provenance document, an `audit.json` that embeds provenance, a route report with
per-gate timing) flags sensed metadata the *same* way. This row delivers exactly that primitive — and nothing
more.

It reads **no clock**, performs **no timing** (the timestamp and duration are supplied, already-measured
values), computes **no digest from raw bytes**, reads **no filesystem / git / environment / network**, persists
**no artifact**, renders **no complete report document** (no `audit.json`, no provenance document, no route
report — only the marked rendering of an individual sensed metadatum and the section that groups them), performs
**no attestation / signing**, and adds **no CLI**. Its sole outputs are the typed sensed-metadata value(s) and
their flagged rendering.

The core provides (full vocabulary in [data-model.md](./data-model.md); the rendering bytes in
[contracts/sensed-metadata-format.md](./contracts/sensed-metadata-format.md)):

- **`SensedMetadatum`** — one flat closed record carrying a sensed value's **label** and its **value**, where
  the value is a closed two-case `SensedValue` (a wall-clock **timestamp** or a **duration**). **The type *is*
  the flag**: every `SensedMetadatum` is sensed by construction — there is no representation of a marked
  timestamp or duration that is reproducible (FR-001).
- **`SensedValue`** = `TimestampValue of SensedTimestamp | DurationValue of SensedDuration` — the carried value;
  the *kind* is intrinsic to the case. `SensedTimestamp of string` is the **only genuinely new vocabulary** (a
  minimal opaque, comparable wall-clock-instant token — the F029 opaque-token discipline — supplied by the edge,
  never clocked); the duration reuses **F032's `SensedDuration` verbatim** (FR-008).
- **`SensedKind`** = `TimestampKind | DurationKind` — the closed, readable kind enum the design names (FR-001),
  with `kindToken : SensedKind -> string` (`"timestamp"` / `"duration"`), a total injective wire token.
- **`SensedRendering`** = `SensedRendering of string` — the byte-stable, unambiguously-flagged rendering (mirrors
  F032's `CommandIdentity` and F033's `ProvenanceIdentity` newtype).
- **`SensedMetadata.markDuration` / `markTimestamp`** — the two total constructors that mark an already-measured
  duration / timestamp as a sensed metadatum, each with its label. Neither reads a clock or measures elapsed time
  (FR-002).
- **`SensedMetadata.kindOf`** — total: the closed `SensedKind` of a metadatum (its label and value are read
  directly off the record / by matching `SensedValue`).
- **`SensedMetadata.render`** — the pure, total render of a single sensed metadatum: an explicit `!sensed!`
  marker, the kind, the label, and the value, in the F029/F032/F033 tagged, length-prefixed, **injective**
  discipline. The `!…!` marker form is reserved and is **never** produced by a reproducible field tag, so the
  rendering is unmistakably distinguishable from a reproducible field (FR-003) and **unspoofable by its data**
  (FR-004).
- **`SensedMetadata.renderSection`** — renders a list of sensed metadata as **one clearly-marked
  `!sensed-section!`** that is cleanly separable from a report's reproducible bytes (FR-005). The empty list is
  an ordinary value (`!sensed-section!=0;`), not an error (Edge cases).
- **`SensedMetadata.renderingValue`** — unwrap a `SensedRendering` to its canonical string.

**Identity-neutrality (FR-006)** is structural and demonstrable: this core computes **no** reproducible identity
and references **no** identity-computing core — its sensed rendering is a standalone value that no
reproducible-identity computation reads. The F032/F033 honesty boundary (sensed metadata excluded from identity)
now holds **at the rendering layer**: a report's reproducible bytes/identity and its sensed section are cleanly
separable.

**Plan-time reconciliations (maintainer to confirm):**

- **D1 — New pure core referencing exactly one sibling core (Tier 1, no new package).** A new packable library
  `src/FS.GG.Governance.SensedMetadata` (`Model.fsi/fs` + `SensedMetadata.fsi/fs`), referencing **only
  `FS.GG.Governance.CommandRecord`** to reuse F032's `SensedDuration` verbatim (FR-008). It introduces **no new
  third-party `PackageReference`** (FR-011): BCL + `FSharp.Core` only. The reference is one-way
  (`SensedMetadata → CommandRecord → Config`); `CommandRecord` is itself a pure vocab core, so nothing impure is
  transitively pulled in (the `YamlDotNet` arriving via the transitive `Config` is unused). Every merged core /
  host stays untouched. *(Contrast: F033 referenced three cores; this row needs only the one that owns
  `SensedDuration`.)*
- **D2 — The duration reuses F032's `SensedDuration` verbatim; only `SensedTimestamp` is genuinely new.**
  FR-008 mandates reusing F032's `SensedDuration` (no local alias — the established direct-reference rhythm). The
  **only** new vocabulary is `SensedTimestamp of string` (no timestamp type exists yet — F032 carries a duration
  but no timestamp, F033 carries no timestamp), plus the `SensedMetadatum` / `SensedValue` / `SensedKind` /
  `SensedLabel` / `SensedRendering` vocabulary this row owns. *Alternative rejected:* a thin local duration alias
  — it would duplicate vocabulary FR-008 says to reuse verbatim.
- **D3 — The kind is modeled as a closed two-case `SensedValue` DU (the value carries the kind), not a tag +
  payload.** `SensedValue = TimestampValue of SensedTimestamp | DurationValue of SensedDuration` makes the type
  *the* flag: there is no way to carry a timestamp or duration through this vocabulary without it being sensed,
  and no reproducible variant exists (FR-001). A separate closed `SensedKind` enum + `kindOf` gives the readable
  kind and the rendering's token. *Alternative rejected:* a single record with a `Kind` field + an untyped value
  — it would allow a kind/value mismatch and weaken "sensed by construction."
- **D4 — The rendering is the F029/F032/F033 tagged, length-prefixed, injective string discipline, with a
  reserved `!…!` sensed marker.** A single metadatum renders as
  `!sensed!=<kindToken>;<labelLen>:<label>;<valueLen>:<value>`; a group renders as one counted, order-preserving
  `!sensed-section!=<count>;<len>:<rendering>;…` section. The `!…!` marker form is **reserved** — no reproducible
  field tag (F029/F032/F033 all use lowercase-letter tags like `src`/`exe`/`rule` before `=`) ever begins with
  `!` — so a sensed rendering is unmistakably distinct from a reproducible field (FR-003). Every label/value is
  length-prefixed, so content containing `!sensed!`, `;`, `:`, or `=` is read by length and cannot masquerade
  (FR-004). An empty label is `0:` (distinct, unambiguous); an empty list is `!sensed-section!=0;` (ordinary
  value). *Alternative rejected:* a free-form `"sensed: label=value"` string — it would be spoofable by data and
  inconsistent with the established encoding.
- **D5 — Identity-neutrality is structural, not a computed law of this core.** This core owns no reproducible
  identity (FR-006: *"this core neither computes nor alters any reproducible identity"*). It guarantees only that
  sensed metadata are a separable partition: the sensed rendering is a standalone value, and the library
  references no identity-computing core (scope-guarded). Tests demonstrate the property self-containedly — a
  report modeled as `(reproducibleBytes, sensedSection)` keeps its `reproducibleBytes` byte-identical regardless
  of which/how-many sensed metadata populate the section — i.e. the F032/F033 honesty boundary now at the
  rendering layer. *(Optional stronger evidence, maintainer's call: a test-only reference to
  `FS.GG.Governance.Provenance` asserting `Provenance.canonicalId` is unchanged when sensed metadata are rendered
  alongside it. Deferred — the self-contained demonstration is sufficient and avoids test coupling.)*
- **D6 — Value text is rendered verbatim from the supplied measured value; this core neither rounds nor
  re-scales.** A `SensedDuration`'s `int64` nanoseconds render as their decimal form (including `0`); a
  `SensedTimestamp`'s opaque string renders verbatim (Edge cases: zero-length duration, long-fraction/large
  magnitude). The length prefix carries whatever bytes the edge supplied.

This row renders **no JSON / artifact / complete report document**, computes **no digest from bytes**, performs
**no sensing / timing / persistence / attestation / signing**, and adds **no CLI**. The merged cores (including
F032) and their `surface/*.surface.txt` baselines are **untouched**; `dotnet build` / `dotnet test` over the
existing projects stays unchanged, and the new project + its test project are purely additive (SC-006).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (repo standard; `Nullable=enable`, `TreatWarningsAsErrors=true`
inherited from `Directory.Build.props`). One new `src/` library with two curated `.fsi` files, plus one new test
project.

**Primary Dependencies**: **`FS.GG.Governance.CommandRecord`** (F032 `SensedDuration`, reused verbatim —
FR-008). **No new third-party `PackageReference`** (FR-011): the marking and rendering are plain `string`
building + `FSharp.Core`; the transitive `YamlDotNet` arriving via `Config` (through `CommandRecord`) is unused.
Test frameworks already on the central feed (`Directory.Packages.props`): **Expecto**, **Expecto.FsCheck**,
**FsCheck**, **Microsoft.NET.Test.Sdk**, **YoloDev.Expecto.TestSdk**.

**Storage**: None. No database, no files, no runtime storage — the timestamp/duration/label are in-value inputs.
The only test-side I/O is the surface-drift baseline read (and its `BLESS_SURFACE=1` write), the established
pattern.

**Testing**: Expecto + FsCheck, exercising the **public** surface (`SensedMetadata.markDuration` /
`markTimestamp` / `kindOf` / `kindToken` / `render` / `renderSection` / `renderingValue`) over real,
literally-constructible values (Principle V — every value is a genuine typed value built from literals,
including real F032 `SensedDuration`s; no mock, no clock read, no process spawn). Concerns: (1) **marking carries
kind/label/value readably and is sensed by construction** across the zero-duration / empty-label / same-label
different-kind edges (SC-001); (2) **rendering carries the label, value, and an explicit, unmistakable sensed
marker, distinguishable from a reproducible field, and unspoofable by data** — including values whose text
contains the marker's characters (SC-002, FR-004); (3) **a group renders as one separable `!sensed-section!`**,
empty list included (SC-004, FR-005); (4) **determinism** — marking/rendering the same value twice is
byte-equal (SC-004); (5) **purity** under changed cwd / time / filesystem (SC-005); (6) **identity-neutrality**
— a report's reproducible bytes are unchanged regardless of its sensed section (SC-003); (7) **surface drift +
scope hygiene** (Principle II, SC-006). The unspoofability/injectivity, determinism, purity, and totality laws
are FsCheck properties; the field-carriage and edge cases are example tests, including a byte-exact
worked-example rendering pinned to `contracts/sensed-metadata-format.md` and the FSI proof.

**Target Platform**: Developer / CI .NET SDK running `dotnet test`. No host, no CLI, no OS-specific surface.

**Project Type**: A new pure-core F# library + its test project. No host, no CLI, no MVU.

**Performance Goals**: N/A. The contract is **determinism and totality**, not latency; a report holds a modest
number of sensed metadata (Spec Assumptions).

**Constraints**: Pure / total / deterministic (FR-007): reads no clock, filesystem, git, environment, or
network; measures no elapsed time, spawns no process, hashes no bytes; identical supplied values always yield an
identical marked value and identical rendering. Marking a value sensed contributes nothing to any reproducible
identity (FR-006). The merged cores and baselines are not modified (FR-008 / SC-006).

**Scale/Scope**: One new `src/` library (`SensedMetadata` — `Model.fsi/fs` + `SensedMetadata.fsi/fs`); one new
test project; one new surface baseline `surface/FS.GG.Governance.SensedMetadata.surface.txt`; two solution
entries; a short `scripts/prelude.fsx` FSI section (design-first proof, Principle I); the `CLAUDE.md` plan
pointer. Zero changes to existing `src/`, `surface/`, or merged test projects.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | The public surface is drafted as `Model.fsi` + `SensedMetadata.fsi` and exercised in `scripts/prelude.fsx` (a new F034 section) before any `.fs` body exists; semantic tests call the public functions, never private helpers. |
| II. Visibility in `.fsi` | **PASS** | Two curated `.fsi` files are the sole public-surface declaration; the `.fs` files carry no access modifiers. A new `surface/FS.GG.Governance.SensedMetadata.surface.txt` baseline is added and guarded by a reflective `SurfaceDrift` test (the F029–F033 precedent), with the `BLESS_SURFACE=1` re-bless path. |
| III. Idiomatic Simplicity | **PASS — load-bearing** | Plain records and closed DUs, minimal new newtypes (`SensedLabel`, `SensedTimestamp`, `SensedRendering`), and `sprintf` / length-prefixed segment building with `List.map` + `String.concat`. No SRTP, reflection (outside the surface test), custom operators, type providers, or non-trivial CEs. The duration is reused verbatim from F032 (D2), not re-modeled. |
| IV. Elmish/MVU is the boundary for stateful/I/O | **N/A** | No state, no I/O, no workflow — pure total functions over supplied values. Like F019 `Route`, F029 `FreshnessKey`, F030–F033, this is a pure projection needing no MVU ceremony. The *actual* sensing (reading the clock, measuring elapsed time) is the later host edge (Principle IV; the F016 git-sensing and future command-execution precedents — F032 already models the duration as a *supplied* `SensedDuration`), explicitly out of scope. |
| V. Test Evidence Is Mandatory | **PASS** | Every input is a real, literally-constructible typed value (including real F032 `SensedDuration`s); no clock is read, no process spawned, no mock used (the values the host would sense are supplied as literals — the spec's contract). Tests fail before the implementation matches the contract and pass after. No mocks ⇒ no `Synthetic` disclosure needed. |
| VI. Observability & Safe Failure | **PASS** | The functions are total: no exception, no swallowed failure, no silent truncation. A zero-length duration, an empty label, a same-label/different-kind pair, an empty section, and a value whose text contains the marker characters are all ordinary complete values (FR-004, Edge cases); empty-string tokens are literal values that each encode to a distinct, unambiguous segment, never colliding with absence or with the marker. |
| Change Classification | **Tier 1 (contracted change — new public API)** | Adds a new public module/assembly and a new surface baseline ⇒ full chain: spec, plan, `.fsi`, baseline, tests. **No new third-party dependency.** No existing public API, baseline, or merged behavior is altered (F032 `SensedDuration` consumed verbatim, not modified). |
| Engineering Constraints | **PASS** | F#/.NET `net10.0`; no new third-party `PackageReference` (FR-011); references only the sibling pure core `CommandRecord` (which owns `SensedDuration`) — no git / filesystem scanning / Snapshot / host / CLI. No rendering package IDs/paths/templates assumed — inputs are product-neutral measured values supplied by the caller. Pack output + structured-logging TODOs unaffected (no runtime/host code). |

**Gate result: PASS — no unjustified violations. Complexity Tracking is empty.** Principle IV is the only N/A
(no stateful/I/O workflow); I, II, III, V, VI all have concrete targets and pass. The single sibling reference
(D1) is mandated by the verbatim-reuse requirement FR-008 and pulls in nothing impure, so it is not a complexity
violation.

## Project Structure

### Documentation (this feature)

```text
specs/034-sensed-metadata-marking/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 — decisions D1–D6 + the marking/rendering semantics facts
├── data-model.md        # Phase 1 — SensedLabel, SensedTimestamp, SensedKind, SensedValue, SensedMetadatum, SensedRendering
├── quickstart.md        # Phase 1 — how to build, FSI-exercise, test, and re-bless the surface
├── contracts/           # Phase 1 — the contracts this row commits
│   ├── sensed-metadata-api.md         # the public function signatures + their laws
│   └── sensed-metadata-format.md      # the flagged-rendering byte encoding (F029/F032/F033 discipline)
├── checklists/
│   └── requirements.md  # spec quality checklist (if present)
└── tasks.md             # Phase 2 — /speckit-tasks output (NOT created here)
```

### Source Code / deliverable layout (repository root)

```text
src/FS.GG.Governance.SensedMetadata/                  # NEW — the pure sensed-metadata marking + rendering core
├── Model.fsi                                          # NEW — SensedLabel, SensedTimestamp, SensedKind, SensedValue,
│                                                      #       SensedMetadatum, SensedRendering (sole public surface;
│                                                      #       reuses F032 SensedDuration verbatim)
├── Model.fs                                           # NEW — the matching record/DU/newtype definitions (no access modifiers)
├── SensedMetadata.fsi                                 # NEW — markDuration/markTimestamp/kindOf/kindToken/render/
│                                                      #       renderSection/renderingValue (sole operations surface)
├── SensedMetadata.fs                                  # NEW — the pure, total marking + length-prefixed rendering bodies
└── FS.GG.Governance.SensedMetadata.fsproj            # NEW — packable; references ONLY CommandRecord; BCL + FSharp.Core

tests/FS.GG.Governance.SensedMetadata.Tests/          # NEW — semantic tests over the PUBLIC surface (Expecto + FsCheck)
├── Support.fs                                          # NEW — real literal builders + FsCheck generators (no mocks)
├── MarkingTests.fs                                     # NEW — US1: kind/label/value carriage, sensed-by-construction (SC-001)
├── RenderingTests.fs                                   # NEW — US2: marker present, distinguishable, unspoofable, section (SC-002/004)
├── DeterminismTests.fs                                 # NEW — US3: byte-equality on repeat marking + rendering (SC-004)
├── PurityTests.fs                                      # NEW — US3: identical under changed cwd/time/fs; identity-neutrality (SC-003/005)
├── SurfaceDriftTests.fs                                # NEW — Principle II surface baseline + CommandRecord-only scope guard
├── Main.fs                                             # NEW — Expecto entry point
└── FS.GG.Governance.SensedMetadata.Tests.fsproj       # NEW — references SensedMetadata + CommandRecord; test packages

surface/FS.GG.Governance.SensedMetadata.surface.txt    # NEW — Tier-1 public-surface baseline (BLESS_SURFACE=1 generated)
scripts/prelude.fsx                                     # EDIT — append a short F034 FSI section (design-first proof)
FS.GG.Governance.sln                                   # EDIT — add the two new projects
CLAUDE.md                                               # EDIT — point the SPECKIT plan reference at this plan
```

**Structure Decision**: One new pure-core F# library `src/FS.GG.Governance.SensedMetadata` (the established
one-new-minimal-core-per-row rhythm, D1), compiled `Model → SensedMetadata`, referencing only the sibling pure
core `CommandRecord` that owns F032's `SensedDuration` (FR-008). A sibling test project exercises the public
surface with real literal values. The library is additive: no existing `src/`, `surface/`, or merged test
project changes.

## Complexity Tracking

> No Constitution Check violations. This section is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
