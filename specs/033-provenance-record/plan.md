# Implementation Plan: Provenance Core

**Branch**: `033-provenance-record` | **Date**: 2026-06-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/033-provenance-record/spec.md`

## Summary

Land **Phase 11 (Cost, Cache, and Provenance)** row 5 — *"Include source commit, base/head, rule hash,
generator version, artifact digests, command records, environment class, and builder identity in provenance"*
(design `docs/initial-implementation-plan.md`; Phase-11 exit criterion: *"Audit records are sufficient to
explain builds, tests, packs, template instantiation, git diffs, package inspection, and visual capture"*).
Continuing this repo's maintainer-confirmed **pure-core-first** rhythm (F015–F032 each landed a pure, total,
deterministic core before any host edge consumed it), this row delivers a single new pure core,
**`FS.GG.Governance.Provenance`**, that answers one question deterministically: *"Given the already-sensed
facts of a build, what is the complete, typed provenance value of it; which of its facts are reproducible
versus sensed; and what is its stable canonical identity?"*

It performs **no command execution** (spawns no process, captures no bytes), reads **no clock / filesystem /
git / environment / network**, computes **no digest from raw bytes** (digests and revisions are supplied),
persists **no artifact**, renders **no JSON / audit.json / attestation document**, performs **no attestation /
signing**, and adds **no CLI**. Its sole outputs are the typed `Provenance` value and its `ProvenanceIdentity`.

The core provides (full vocabulary in [data-model.md](./data-model.md)):

- **`Provenance`** — one flat closed record carrying **all eight** declared facts of a build: the source
  commit, the base and head revisions, the rule hash, the generator version, the artifact digests, the
  command records, the environment class, and the builder identity. No declared fact is dropped, stringly-typed
  away, or optional-by-omission (FR-001).
- **`ProvenanceIdentity`** = `ProvenanceIdentity of string` — a byte-stable canonical identity computed
  **only** over the reproducible facts, suitable for an audit field (FR-006).
- **`Provenance.build`** — the single, pure, **total** assembly point: takes the nine supplied facts (curried
  in the design row's field order — base and head are two revisions) and produces a complete `Provenance`,
  defined for every well-typed input (no command records, no covered artifacts, equal base/head, and
  failed/timed-out embedded command records all yield ordinary complete values — FR-004).
- **`Provenance.canonicalId`** — the pure, total canonical identity over the reproducible facts; it folds each
  embedded command record's **reproducible** identity via F032's `CommandRecord.canonicalId` (never its sensed
  duration), so two builds differing only in command durations share an identity, while any reproducible
  difference changes it (FR-006/FR-007).
- **`Provenance.identityValue`** — unwrap a `ProvenanceIdentity` to its canonical string (storage, tests).

The **sensed / non-deterministic** metadata (the embedded command records' durations) is carried **structurally
apart** and reachable via `provenance.CommandRecords.[i].Duration` — the F032 record holds its sensed
`Duration` in a distinct field from its `Reproducible` facts (F032 D2), so it is structurally impossible for a
duration to enter the provenance identity (FR-005, SC-003). The spec's optional wall-clock timestamp is **not**
carried by this row (the design lists the eight facts and no timestamp — Spec Assumptions).

**Plan-time reconciliations (maintainer to confirm):**

- **D1 — New pure core referencing three sibling cores (Tier 1, no new package).** A new packable library
  `src/FS.GG.Governance.Provenance`, referencing **`FS.GG.Governance.FreshnessKey`** (for the verbatim F029
  `RuleHash` / `GeneratorVersion` / `ArtifactHash` / `Revision`), **`FS.GG.Governance.CommandRecord`** (for the
  verbatim F032 `CommandRecord` plus its public `canonicalId` / `identityValue`), and **`FS.GG.Governance.Config`**
  (for the verbatim F014 `EnvironmentClass`). This is the **first core to reference more than one sibling core**
  — every prior core (F029, F030, F031, F032) referenced only `Config`. The multi-reference is *mandated* by
  FR-010 (reuse the existing typed facts verbatim where one maps to a declared provenance fact) and is the
  spec's stated expectation (*"the established rhythm suggests direct references"*). It introduces **no new
  third-party `PackageReference`** (FR-013): BCL + `FSharp.Core` only. Dependency direction stays one-way —
  `Provenance → { FreshnessKey, CommandRecord, Config }`, all three of which are themselves pure config/vocab
  cores (no git / filesystem / host), so nothing impure is transitively pulled in; the transitive `YamlDotNet`
  arriving via `Config` is unused. Every merged core / host stays untouched.
- **D2 — Reuse `Revision` for the source commit (only `BuilderIdentity` is genuinely new).** Base and head are
  **F029 `Revision`** verbatim (mandated — Spec Key Entities). The **source commit is also a resolved
  revision**, so it reuses the same `Revision` newtype (the spec's leading candidate). Each of the three
  revisions is a **distinct tagged segment** in the identity (`src` / `base` / `head`), so the same revision
  string in two fields yields different identity segments (injective across fields). The **only** genuinely new
  vocabulary is `BuilderIdentity of string` — a minimal opaque comparable token (the F029 opaque-token
  discipline) supplied by the edge. *Alternative rejected:* a dedicated `SourceCommit` newtype — it would add
  vocabulary for a value the design already treats as a revision, and `Revision` already carries the right
  opaque-comparable semantics.
- **D3 — Sensed / reproducible split is inherited structurally from F032, not re-modeled.** Provenance is a
  flat record of eight facts; its only sensed metadata lives **inside** the embedded `CommandRecord`s (their
  `Duration` fields). Because `canonicalId` folds each command record via F032's `CommandRecord.canonicalId`
  (which reads only `record.Reproducible` and never `record.Duration` — F032 D2), the durations are
  *structurally* excluded from the provenance identity while remaining reachable as sensed metadata via the
  carried records (FR-005, SC-003). No new `Sensed*`/`Reproducible*` wrapper is introduced at the provenance
  level — there is no provenance-level sensed fact (no wall-clock timestamp this row).
- **D4 — Artifact digests are a SET; command records are an ORDER-PRESERVING sequence.** The artifact digests
  are an `ArtifactHash list` compared as a **set** in the identity (deduped, ordinal-sorted — the established
  F029/F032 covered-artifacts discipline; FR-003, FR-008). The command records are a `CommandRecord list`
  carried **whole and in order**, and their identity contribution is **order-significant** (each contributes
  its F032 `canonicalId`, rendered in the given order, not sorted or deduped). *Rationale:* the order of
  command runs is itself reproducible provenance (a build runs build → test → pack), and FR-008 mandates set
  discipline **only** for the artifact digests (*"concretely the artifact digests"*); this mirrors F032's
  decision to keep *arguments* order-significant. *Alternative rejected:* treating the command records as a set
  (dedup/sort by identity) — it would silently collapse a genuinely-repeated run and discard meaningful run
  order, and the spec only requires set treatment for the artifact digests. The spec's Assumptions explicitly
  defer this choice to the plan; the contract it fixes (each record carried whole; contributes its reproducible
  identity) is honored either way.
- **D5 — Canonical identity is the F029/F032 tagged, length-prefixed, injective string discipline.**
  `canonicalId` renders the reproducible facts to a byte-stable `ProvenanceIdentity` string using the
  established encoding (`contracts/provenance-identity-format.md`): each field is a tagged, length-prefixed
  segment so no value can masquerade as another field (injective across fields). The artifact-digest segment
  is a **set** (deduped, ordinal-sorted); the command-records segment is an **ordered** list whose each entry
  is the **length-prefixed full F032 canonical-id string** (so the embedded newlines/`:`/`;`/`=` inside a
  command id can never bleed across the boundary). The environment class renders via the same total four-token
  map F029 uses internally (`local` / `ci` / `localOrCi` / `release`) — replicated locally as a small total
  match (F029's `environmentToken` is an internal helper, not public, so it is convention-reused, not
  called). `build` and `canonicalId` are pure list / string operations; **no hashing** (FR-011).

This row renders **no JSON / artifact**, computes **no digest from bytes**, performs **no sensing / timing /
persistence / provenance-document rendering / attestation / signing / severity / enforcement / freshness / ship
verdict**, and adds **no CLI**. The merged cores and their `surface/*.surface.txt` baselines are **untouched**;
`dotnet build` / `dotnet test` over the existing projects stays unchanged, and the new project + its test
project are purely additive (SC-007).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (repo standard; `Nullable=enable`, `TreatWarningsAsErrors=true`
inherited from `Directory.Build.props`). One new `src/` library with two curated `.fsi` files, plus one new
test project.

**Primary Dependencies**: **`FS.GG.Governance.FreshnessKey`** (F029 `RuleHash` / `GeneratorVersion` /
`ArtifactHash` / `Revision`), **`FS.GG.Governance.CommandRecord`** (F032 `CommandRecord` + public
`canonicalId` / `identityValue`), and **`FS.GG.Governance.Config`** (F014 `EnvironmentClass`) — all reused
verbatim (FR-010). **No new third-party `PackageReference`** (FR-013): the projection is plain `List` / `string`
building + `FSharp.Core`; the transitive `YamlDotNet` arriving via `Config` is unused. Test frameworks already
on the central feed (`Directory.Packages.props`): **Expecto**, **Expecto.FsCheck**, **FsCheck**,
**Microsoft.NET.Test.Sdk**, **YoloDev.Expecto.TestSdk**.

**Storage**: None. No database, no files, no runtime storage — the build facts are in-value inputs. The only
test-side I/O is the surface-drift baseline read (and its `BLESS_SURFACE=1` write), the established pattern.

**Testing**: Expecto + FsCheck, exercising the **public** surface (`Provenance.build` / `canonicalId` /
`identityValue`) over real, literally-constructible facts (Principle V — every value is a genuine typed value
built from literals, including real F032 `CommandRecord`s assembled via `CommandRecord.build`; no mock, no
process spawn, matching the spec's "no host required"). Concerns: (1) **the value carries all eight facts
verbatim and readably**, with the command records carried whole and the artifact digests reported as a set,
across the no-records / no-artifacts / equal-base-head / failed-or-timed-out-record edge cases (SC-001, SC-002);
(2) **the embedded durations are reachable as sensed metadata and excluded from identity** (SC-003); (3)
**duration-only difference ⇒ equal identity; any reproducible difference ⇒ different identity** (SC-004); (4)
**determinism + artifact-digest order/dup invariance** of build and identity, and **command-record order
significance** (SC-005); (5) **purity** under changed cwd / time / filesystem (SC-006); (6) **surface drift +
scope hygiene** (Principle II, SC-007). The identity laws (duration-invariance, per-field sensitivity,
artifact-set order/dup invariance, command-order significance) and totality are FsCheck properties; the
field-carriage and edge cases are example tests, including a byte-exact worked-example identity pinned to
`contracts/provenance-identity-format.md` and the FSI proof.

**Target Platform**: Developer / CI .NET SDK running `dotnet test`. No host, no CLI, no OS-specific surface.

**Project Type**: A new pure-core F# library + its test project. No host, no CLI, no MVU.

**Performance Goals**: N/A. The contract is **determinism and totality**, not latency; a provenance holds a
modest number of artifact digests and command records (Spec Assumptions).

**Constraints**: Pure / total / deterministic (FR-009): reads no clock, filesystem, git, environment, or
network, spawns no process, hashes no bytes; identical supplied facts always yield an identical provenance and
identical canonical identity; reordering or duplicating the artifact digests never changes the identity, while
command-record order is significant. The sensed durations live inside the embedded F032 records and are
structurally excluded from the identity (FR-005). The merged cores and baselines are not modified
(FR-010 / SC-007).

**Scale/Scope**: One new `src/` library (`Provenance` — `Model.fsi/fs` + `Provenance.fsi/fs`); one new test
project; one new surface baseline `surface/FS.GG.Governance.Provenance.surface.txt`; two solution entries; a
short `scripts/prelude.fsx` FSI section (design-first proof, Principle I); the `CLAUDE.md` plan pointer. Zero
changes to existing `src/`, `surface/`, or merged test projects.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | The public surface is drafted as `Model.fsi` + `Provenance.fsi` and exercised in `scripts/prelude.fsx` (a new F033 section) before any `.fs` body exists; semantic tests call the public functions, never private helpers. |
| II. Visibility in `.fsi` | **PASS** | Two curated `.fsi` files are the sole public-surface declaration; the `.fs` files carry no access modifiers. A new `surface/FS.GG.Governance.Provenance.surface.txt` baseline is added and guarded by a reflective `SurfaceDrift` test (the F029–F032 precedent), with the `BLESS_SURFACE=1` re-bless path. |
| III. Idiomatic Simplicity | **PASS — load-bearing** | Plain records, one new single-case newtype (`BuilderIdentity`) + one identity newtype (`ProvenanceIdentity`), and `List.map` / `List.distinct` / `List.sortWith` + `sprintf` segment building. No SRTP, reflection (outside the surface test), custom operators, type providers, or non-trivial CEs. The sensed/reproducible split is inherited from the embedded F032 record (D3), not re-modeled. |
| IV. Elmish/MVU is the boundary for stateful/I/O | **N/A** | No state, no I/O, no workflow — pure total functions over supplied values. Like F019 `Route`, F029 `FreshnessKey`, F030 `EvidenceReuse`, F031 `RouteExplain`, F032 `CommandRecord`, this is a pure projection needing no MVU ceremony. The *actual* sensing (resolving the commit / base / head, hashing artifacts, running commands, reading builder identity) is the later host edge (Principle IV; the F016 git-sensing and future command-execution precedents), explicitly out of scope. |
| V. Test Evidence Is Mandatory | **PASS** | Every input is a real, literally-constructible typed value (including real F032 `CommandRecord`s); no process is spawned and no mock is used (the facts the host would sense are supplied as literals — the spec's contract). Tests fail before the implementation matches the contract and pass after. No mocks ⇒ no `Synthetic` disclosure needed. |
| VI. Observability & Safe Failure | **PASS** | The functions are total: no exception, no swallowed failure, no silent truncation. A build with no command records / no artifacts / equal base-head / a failed-or-timed-out embedded record is an ordinary complete value (FR-004, Edge cases); empty-string facts are literal values that each encode to a distinct identity segment, never colliding with absence (FR-011, Edge cases). |
| Change Classification | **Tier 1 (contracted change — new public API)** | Adds a new public module/assembly and a new surface baseline ⇒ full chain: spec, plan, `.fsi`, baseline, tests. **No new third-party dependency.** No existing public API, baseline, or merged behavior is altered (F029 / F032 / F014 vocabulary consumed verbatim, none modified). |
| Engineering Constraints | **PASS** | F#/.NET `net10.0`; no new third-party `PackageReference` (FR-013); references only sibling pure cores (`FreshnessKey`, `CommandRecord`, `Config`) — no git / filesystem scanning / Snapshot / host / CLI (the "core stays minimal" constraint holds because all three references are themselves pure vocab cores). No rendering package IDs/paths/templates assumed — inputs are product-neutral build facts supplied by the caller. Pack output + structured-logging TODOs unaffected (no runtime/host code). |

**Gate result: PASS — no unjustified violations. Complexity Tracking is empty.** Principle IV is the only
N/A (no stateful/I/O workflow); I, II, III, V, VI all have concrete targets and pass. The one notable structural
departure — referencing three sibling cores instead of `Config` alone (D1) — is mandated by the verbatim-reuse
requirement FR-010 and pulls in nothing impure, so it is not a complexity violation.

## Project Structure

### Documentation (this feature)

```text
specs/033-provenance-record/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 — decisions D1–D5 + the build/identity semantics facts
├── data-model.md        # Phase 1 — the eight facts, BuilderIdentity, Provenance, ProvenanceIdentity
├── quickstart.md        # Phase 1 — how to build, FSI-exercise, test, and re-bless the surface
├── contracts/           # Phase 1 — the contracts this row commits
│   ├── provenance-api.md             # the public function signatures + their laws
│   └── provenance-identity-format.md # the canonical-identity byte encoding (F029/F032 discipline)
├── checklists/
│   └── requirements.md  # spec quality checklist (already present)
└── tasks.md             # Phase 2 — /speckit-tasks output (NOT created here)
```

### Source Code / deliverable layout (repository root)

```text
src/FS.GG.Governance.Provenance/                     # NEW — the pure provenance core
├── Model.fsi                                         # NEW — BuilderIdentity, ProvenanceIdentity, Provenance (sole public surface; reuses F029/F032/F014 vocab)
├── Model.fs                                          # NEW — the type bodies (no access modifiers)
├── Provenance.fsi                                    # NEW — build / canonicalId / identityValue signatures + laws
├── Provenance.fs                                     # NEW — pure assembly + canonical-identity rendering
└── FS.GG.Governance.Provenance.fsproj                # NEW — references ../FreshnessKey, ../CommandRecord, ../Config; no new package

tests/FS.GG.Governance.Provenance.Tests/             # NEW — the semantic tests
├── Support.fs                                         # NEW — literal fact builders (incl. real CommandRecords) + FsCheck generators + repoRoot
├── ProvenanceTests.fs                                # NEW — all eight facts carried verbatim; records whole; artifact digests as a set; no-records/no-artifacts/equal-base-head/failed-record edge cases (SC-001/SC-002)
├── IdentityTests.fs                                  # NEW — durations excluded + reachable; duration-only ⇒ equal id; any reproducible change ⇒ different id; worked-example byte match (SC-003/SC-004)
├── DeterminismTests.fs                               # NEW — build/identity twice equality + artifact-digest order&dup invariance; command-record order significance (SC-005)
├── PurityTests.fs                                    # NEW — provenance + identity identical across cwd/time/fs changes (SC-006)
├── SurfaceDriftTests.fs                              # NEW — baseline equality + scope-hygiene (FreshnessKey/CommandRecord/Config/BCL/FSharp.Core only) (SC-007)
├── Main.fs                                           # NEW — Expecto entry point
└── FS.GG.Governance.Provenance.Tests.fsproj          # NEW — references the core + its three deps + test frameworks

surface/FS.GG.Governance.Provenance.surface.txt       # NEW — committed public-surface baseline (Principle II)
FS.GG.Governance.sln                                 # CHANGED — add the new library + test project
scripts/prelude.fsx                                  # CHANGED — add the F033 design-first FSI section
CLAUDE.md                                            # CHANGED — SPECKIT plan pointer → this plan

# Deliberately UNCHANGED:
src/** (existing), surface/** (existing)             # no merged core/.fsi/surface-baseline changes (FR-010)
tests/** (existing projects)                         # untouched; the new project is purely additive
```

**Structure Decision**: A **new pure-core library** mirroring F032 `CommandRecord` (a `Model` file of types +
a same-named operations file, a byte-stable canonical identity in the F029/F032 tagged/length-prefixed/injective
discipline). It differs from prior cores only in referencing **three** sibling pure cores instead of `Config`
alone (D1) — the minimum needed to reuse the F029 / F032 / F014 vocabulary verbatim per FR-010 — and stays free
of the git-sensing / host assemblies. It adds the *value and vocabulary* the later host edge (the actual
sensing) and the later audit / attestation rows will populate, read, and sign. Tier 1 (new public surface +
baseline), no new third-party dependency.

## Complexity Tracking

> No Constitution violations to justify — this section is intentionally empty. (The three-core reference of D1
> is mandated by FR-010 and pulls in nothing impure; it is not a violation.)

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
