# Implementation Plan: Command-Record Core

**Branch**: `032-command-records` | **Date**: 2026-06-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/032-command-records/spec.md`

## Implementation Progress

**Status: 🟢 COMPLETE** — all phases landed; 24/24 semantic tests green; full solution green (no regression);
surface baseline committed; FSI proof matches the contract byte-for-byte.

| Phase | Scope | Status |
|---|---|---|
| 1 — Setup | New library + test project + solution entries | 🟢 Done (T001–T003) |
| 2 — Foundational | `Model.fsi`/`CommandRecord.fsi`, stubs, FSI proof, Support + Main | 🟢 Done (T004–T009) |
| 3 — US1 (P1) | `build` — complete ten-fact record, total over edge cases | 🟢 Done (T010–T011) |
| 4 — US2 (P1) | `canonicalId`/`identityValue` — sensed split + byte-stable identity | 🟢 Done (T012–T013) |
| 5 — US3 (P2) | Determinism + purity (env-delta order/dup invariance, arg-order significance) | 🟢 Done (T014–T015) |
| 6 — Cross-cutting | Surface baseline + scope-hygiene guard | 🟢 Done (T016–T017); 🟡 T018 README pointer **skipped** (list frozen at F18) |
| 7 — Validation | Suite + full-solution no-regression + FSI + quickstart walk | 🟢 Done (T019–T022) |

**Verification evidence (Principle V):**

- 🟢 `dotnet build src/FS.GG.Governance.CommandRecord` — clean under `TreatWarningsAsErrors`.
- 🟢 `dotnet test tests/FS.GG.Governance.CommandRecord.Tests` — **24 passed, 0 failed** (RecordTests,
  IdentityTests, DeterminismTests, PurityTests, SurfaceDriftTests; FsCheck laws + example/edge tests).
- 🟢 `dotnet test FS.GG.Governance.sln` — full solution green; **no** existing `src/**` or `surface/**` diff
  (only the new `CommandRecord` core + its `surface/FS.GG.Governance.CommandRecord.surface.txt` are added).
- 🟢 `dotnet fsi scripts/prelude.fsx` — the F032 section's worked-example identity equals
  `contracts/command-record-identity-format.md` byte-for-byte; duration-invariance / per-field-sensitivity /
  env-delta order-dup-invariance / captured-output disambiguation all print the expected results.
- 🟢 Surface baseline blessed and re-verified WITHOUT `BLESS_SURFACE` — exactly the two modules
  (`Model`, `CommandRecord`), three vals, declared types; Config/BCL/FSharp.Core-only scope guard passes.

> **Plan-time vs implementation note (honest deviation):** the canonical-identity format contract's field
> table describes each env-delta class entry as outer length-prefixed (`<len>:<entry>`), but its **worked
> example** renders the entry directly after the count (`env+=1;n:2:CI|v:1:1`, no outer prefix). The worked
> example is the concrete, testable golden block (pinned by `IdentityTests` and the FSI proof), so the
> implementation matches it exactly; per-entry injectivity is still guaranteed by the entries' internal
> length prefixes (`n:<len>:…|v:<len>:…`) plus the distinct class tags. No behavior is ambiguous.

## Summary

Land **Phase 11 (Cost, Cache, and Provenance)** row 4 — *"Record command runs with executable, arguments,
working directory, environment delta, timeout, exit code, stdout digest, stderr digest, captured output path,
and duration"* (design `docs/initial-implementation-plan.md`, Phase-11 exit criterion: *"Audit records are
sufficient to explain builds, tests, packs, template instantiation, git diffs, package inspection, and visual
capture"*). Continuing this repo's maintainer-confirmed **pure-core-first** rhythm (F015–F031 each landed a
pure, total, deterministic core before any host edge consumed it), this row delivers a single new pure core,
**`FS.GG.Governance.CommandRecord`**, that answers one question deterministically: *"Given the already-sensed
facts of a command run, what is the complete typed record of it, which of its facts are reproducible versus
sensed, and what is its stable canonical identity?"*

It performs **no command execution** (spawns no process, captures no bytes), reads **no clock / filesystem /
git / environment / network**, computes **no digest from raw bytes** (digests are supplied), persists **no
artifact**, renders **no JSON / audit.json**, builds **no provenance / attestation** (that is Phase-11 row 5),
and adds **no CLI**. Its sole outputs are the typed command-record value and its canonical identity.

The core provides (full vocabulary in [data-model.md](./data-model.md)):

- **The ten typed run facts** — `Executable`, `Argument` (an ordered list), `WorkingDirectory`,
  `EnvironmentDelta` (the `Added` / `Changed` / `Removed` partition), the reused F014 `TimeoutLimit`,
  `ExitCode`, `OutputDigest` (stdout and stderr, opaque, supplied), `CapturedOutput`
  (`CapturedAt of CapturedOutputPath | NoCapturedOutput`), and the sensed `SensedDuration`.
- **`ReproducibleFacts`** — a closed record carrying the **nine reproducible** facts (everything except
  duration); it is the addressable "the reproducible part of the run" value and the sole input to identity.
- **`CommandRecord`** = `{ Reproducible: ReproducibleFacts; Duration: SensedDuration }` — the complete record,
  with the sensed duration held **structurally apart** from the reproducible facts (FR-004, US2/3): a
  consumer reads `record.Duration` as explicitly-sensed metadata and `record.Reproducible` as the
  reproducible part; the duration can never be silently folded into identity.
- **`CommandIdentity`** = `CommandIdentity of string` — a byte-stable canonical identity computed **only** over
  `record.Reproducible`, order-independent over the environment delta, suitable for an audit field.
- **`CommandRecord.build`** — the single, pure, **total** assembly point: takes the ten supplied facts and
  produces a complete `CommandRecord` (grouping the nine reproducible facts), defined for every well-typed
  input (failed, timed-out, argument-less, empty-delta runs all yield ordinary records).
- **`CommandRecord.canonicalId`** — the pure, total canonical identity over the reproducible facts (excludes
  duration); two runs differing only in duration share it, any reproducible difference changes it.
- **`CommandRecord.identityValue`** — unwrap a `CommandIdentity` to its canonical string (storage, tests).

**Plan-time reconciliations (maintainer to confirm):**

- **D1 — New pure core, `Config`-only dependency (Tier 1).** A new packable library
  `src/FS.GG.Governance.CommandRecord`, referencing **only `FS.GG.Governance.Config`** — exactly the shape of
  F029 `FreshnessKey`. The one verbatim F014 reuse that fits a declared field is **`TimeoutLimit`** (F014's
  `TimeoutLimit of seconds: int`) for the *timeout* fact. New `.fsi` + new `surface/*.surface.txt` baseline ⇒
  **Tier 1**, but **no new third-party `PackageReference`** (FR-013): BCL + `FSharp.Core` only. It references
  **no** Snapshot / Gates / Route / FreshnessKey / EvidenceReuse / host / edge assembly. The transitive
  `YamlDotNet` that arrives via `Config` is unused here. Dependency direction stays one-way:
  `CommandRecord → Config`; every merged core/host stays untouched.
- **D2 — Reproducible / sensed split is structural, not a discipline.** `CommandRecord` is
  `{ Reproducible: ReproducibleFacts; Duration: SensedDuration }`. The sensed duration is a *separate field of
  a distinct type*, so "the duration is marked sensed, distinguishable from the reproducible facts, and
  excluded from identity" (FR-004, FR-005, US2 scenario 3) is enforced by the **type shape**, not by trusting
  `canonicalId` to skip a field. `canonicalId` takes the whole record but reads only `record.Reproducible` —
  it is structurally impossible for duration to enter the identity. The `SensedDuration` *name itself* flags
  the non-determinism at every use site.
- **D3 — Digests and duration are opaque supplied values; no F029 cross-reference.** stdout/stderr digests are
  carried as a local `OutputDigest of string` opaque newtype — the **F029 `RuleHash`/`ArtifactHash`
  opaque-token discipline applied locally**, mirroring how F029 introduced its *own* `Revision` rather than
  referencing the git-sensing `Snapshot` (research D3 of F029). This core therefore does **not** reference
  `FreshnessKey` for a digest type. Duration is `SensedDuration of nanoseconds: int64` — a pure, comparable,
  deterministic measure (no `float`, no `DateTime`, no clock); modeling it as nanoseconds is a precision-safe
  "opaque measure" (Spec Assumptions). No wall-clock timestamp is carried (the design row lists *duration*).
- **D4 — Environment delta is a three-class partition; a changed var carries old+new.**
  `EnvironmentDelta = { Added: AddedVar list; Changed: ChangedVar list; Removed: RemovedVar list }` where
  `AddedVar = { Name; Value }`, `ChangedVar = { Name; Old; New }`, `RemovedVar = { Name; Old }`
  (`Name`/value via `EnvVarName`/`EnvVarValue` newtypes). A changed variable appears **once**, in `Changed`,
  carrying both its baseline (`Old`) and run (`New`) value — never decomposed into a `Removed` + `Added` pair
  (FR-002, SC-002, Edge cases). Empty lists in any class are ordinary values, not errors.
- **D5 — Captured-output path is a closed two-case outcome.** `CapturedOutput = CapturedAt of
  CapturedOutputPath | NoCapturedOutput` (FR-011): absence is an explicit, locatable case, never an empty
  string that could collide with a real path, and it **participates in the canonical identity unambiguously**
  (the two cases encode to distinct, length-prefixed segments).
- **D6 — Canonical identity is the F029 tagged, length-prefixed, injective string discipline.**
  `canonicalId` renders `record.Reproducible` to a byte-stable `CommandIdentity` string using the established
  F029 encoding (`contracts/command-record-identity-format.md`): each field is a tagged, length-prefixed
  segment so no value can masquerade as another field (injective across categories). **Arguments are encoded
  in order** (order is significant — `["-a";"-b"]` ≠ `["-b";"-a"]`). Each **environment-delta class is encoded
  as a SET** — entries rendered to a canonical per-entry string, deduplicated, ordinal-sorted — so supplying
  the delta in a different order or with duplicates never changes the identity (FR-007, the F029
  set-discipline). Duration is **not** in the rendering (FR-005). `build` and `canonicalId` are pure list/
  string operations; no hashing.

This row renders **no JSON / artifact**, computes **no digest from bytes**, performs **no execution / timing /
persistence / provenance / attestation / severity / enforcement / freshness / ship verdict**, and adds **no
CLI**. The merged cores and their `surface/*.surface.txt` baselines are **untouched**; `dotnet build` /
`dotnet test` over the existing projects stays unchanged, and the new project + its test project are purely
additive.

## Technical Context

**Language/Version**: F# on .NET `net10.0` (repo standard; `Nullable=enable`, `TreatWarningsAsErrors=true`
inherited from `Directory.Build.props`). One new `src/` library with two curated `.fsi` files, plus one new
test project.

**Primary Dependencies**: **`FS.GG.Governance.Config`** only — for the verbatim F014 `TimeoutLimit` newtype
(FR-009). No other project is referenced (no Gates/Route/Snapshot/FreshnessKey/EvidenceReuse, no host/edge).
**No new third-party `PackageReference`** (FR-013): the projection is plain `List`/`string` building +
`FSharp.Core`; the transitive `YamlDotNet` arriving via `Config` is unused. Test frameworks already on the
central feed (`Directory.Packages.props`): **Expecto**, **Expecto.FsCheck**, **FsCheck**,
**Microsoft.NET.Test.Sdk**, **YoloDev.Expecto.TestSdk**.

**Storage**: None. No database, no files, no runtime storage — the run facts are in-value inputs. The only
test-side I/O is the surface-drift baseline read (and its `BLESS_SURFACE=1` write), the established pattern.

**Testing**: Expecto + FsCheck, exercising the **public** surface (`CommandRecord.build` / `canonicalId` /
`identityValue`) over real, literally-constructible facts (Principle V — every value is a genuine typed value
built from literals, never a mock; no process is spawned, matching the spec's "no host required"). Concerns:
(1) **the record carries all ten facts verbatim and readably**, across failed/timed-out/argument-less/
empty-delta runs (SC-001); (2) **the env delta reports added/changed/removed as distinct classes, a change
counted once** (SC-002); (3) **duration is reachable as sensed metadata and excluded from identity** (SC-003);
(4) **duration-only difference ⇒ equal identity; any reproducible difference ⇒ different identity** (SC-004);
(5) **determinism + env-delta order/dup invariance** of build and identity (SC-005); (6) **purity** under
changed cwd/time/filesystem (SC-006); (7) **surface drift + scope hygiene** (Principle II, SC-007). The
identity laws (duration-invariance, per-field sensitivity, order/dup invariance) and totality are FsCheck
properties; the field-carriage and edge cases are example tests.

**Target Platform**: Developer/CI .NET SDK running `dotnet test`. No host, no CLI, no OS-specific surface.

**Project Type**: A new pure-core F# library + its test project. No host, no CLI, no MVU.

**Performance Goals**: N/A. The contract is **determinism and totality**, not latency; a record holds a modest
number of arguments and environment-delta entries (Spec Assumptions).

**Constraints**: Pure / total / deterministic (FR-003/FR-008): reads no clock, filesystem, git, environment,
or network, spawns no process, hashes no bytes; identical supplied facts always yield an identical record and
identical canonical identity; reordering or duplicating the environment-delta entries never changes the
identity (arguments, by contrast, are order-significant). The duration is structurally separated and excluded
from identity (FR-004/FR-005). The merged cores and baselines are not modified (FR-009/SC-007).

**Scale/Scope**: One new `src/` library (`CommandRecord` — `Model.fsi/fs` + `CommandRecord.fsi/fs`); one new
test project; one new surface baseline `surface/FS.GG.Governance.CommandRecord.surface.txt`; two solution
entries; a short `scripts/prelude.fsx` FSI section (design-first proof, Principle I); a `README.md` cores
pointer; the `CLAUDE.md` plan pointer. Zero changes to existing `src/`, `surface/`, or merged test projects.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | The public surface is drafted as `Model.fsi` + `CommandRecord.fsi` and exercised in `scripts/prelude.fsx` (a new F032 section) before any `.fs` body exists; semantic tests call the public functions, never private helpers. |
| II. Visibility in `.fsi` | **PASS** | Two curated `.fsi` files are the sole public-surface declaration; the `.fs` files carry no access modifiers. A new `surface/FS.GG.Governance.CommandRecord.surface.txt` baseline is added and guarded by a reflective `SurfaceDrift` test (the F029/F030/F031 precedent), with the `BLESS_SURFACE=1` re-bless path. |
| III. Idiomatic Simplicity | **PASS — load-bearing** | Plain records, one small closed DU (`CapturedOutput`), and `List.map`/`List.distinct`/`List.sortWith` + `StringBuilder`/`sprintf` segment building. No SRTP, reflection (outside the surface test), custom operators, type providers, or non-trivial CEs. The reproducible/sensed split is a plain nested record (D2). |
| IV. Elmish/MVU is the boundary for stateful/I/O | **N/A** | No state, no I/O, no workflow — pure total functions over supplied values. Like F019 `Route`, F029 `FreshnessKey`, F030 `EvidenceReuse`, F031 `RouteExplain`, this is a pure projection needing no MVU ceremony. The *actual* command execution/sensing is the later host edge (Principle IV, the F016 git-sensing precedent), explicitly out of scope. |
| V. Test Evidence Is Mandatory | **PASS** | Every input is a real, literally-constructible typed value; no process is spawned and no mock is used (the facts the host would sense are supplied as literals — the spec's contract). Tests fail before the implementation matches the contract and pass after. No mocks ⇒ no `Synthetic` disclosure needed. |
| VI. Observability & Safe Failure | **PASS** | The function is total: no exception, no swallowed failure, no silent truncation. A failed/timed-out run is an ordinary complete record (FR-003, Edge cases); an absent captured-output path is the explicit `NoCapturedOutput`, never a silent empty string (FR-011); an empty argument list / empty delta is an ordinary value. |
| Change Classification | **Tier 1 (contracted change — new public API)** | Adds a new public module/assembly and a new surface baseline ⇒ full chain: spec, plan, `.fsi`, baseline, tests. **No new third-party dependency.** No existing public API, baseline, or merged behavior is altered (Config consumed verbatim; F014 `TimeoutLimit` reused, not modified). |
| Engineering Constraints | **PASS** | F#/.NET `net10.0`; no new third-party `PackageReference` (FR-013); references only `Config` (for `TimeoutLimit`), honoring "the helper core stays minimal — MUST NOT depend on git/filesystem scanning" (no Snapshot/git reference; no host/CLI). No rendering package IDs/paths/templates assumed — inputs are product-neutral run facts supplied by the caller. Pack output + structured-logging TODOs unaffected (no runtime/host code). |

**Gate result: PASS — no unjustified violations. Complexity Tracking is empty.** Principle IV is the only
N/A (no stateful/I/O workflow); I, II, III, V, VI all have concrete targets and pass.

## Project Structure

### Documentation (this feature)

```text
specs/032-command-records/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 — decisions D1–D6 + the build/identity semantics facts
├── data-model.md        # Phase 1 — the ten facts, ReproducibleFacts, CommandRecord, CommandIdentity
├── quickstart.md        # Phase 1 — how to build, FSI-exercise, test, and re-bless the surface
├── contracts/           # Phase 1 — the contracts this row commits
│   ├── command-record-api.md             # the public function signatures + their laws
│   └── command-record-identity-format.md # the canonical-identity byte encoding (F029 discipline)
├── checklists/
│   └── requirements.md  # spec quality checklist (already present)
└── tasks.md             # Phase 2 — /speckit-tasks output (NOT created here)
```

### Source Code / deliverable layout (repository root)

```text
src/FS.GG.Governance.CommandRecord/                  # NEW — the pure command-record core
├── Model.fsi                                         # NEW — the ten facts, EnvironmentDelta, ReproducibleFacts, CommandRecord, CommandIdentity (sole public surface)
├── Model.fs                                          # NEW — the type bodies (no access modifiers)
├── CommandRecord.fsi                                 # NEW — build / canonicalId / identityValue signatures + laws
├── CommandRecord.fs                                  # NEW — pure assembly + canonical-identity rendering
└── FS.GG.Governance.CommandRecord.fsproj             # NEW — references ../FS.GG.Governance.Config; no new package

tests/FS.GG.Governance.CommandRecord.Tests/           # NEW — the semantic tests
├── Support.fs                                         # NEW — literal fact builders + FsCheck generators + repoRoot
├── RecordTests.fs                                     # NEW — all ten facts carried verbatim; env-delta three-class partition; failed/timed-out/argless/empty-delta edge cases (SC-001/SC-002)
├── IdentityTests.fs                                   # NEW — duration excluded + reachable; duration-only ⇒ equal id; any reproducible change ⇒ different id; captured-output presence/absence distinct (SC-003/SC-004)
├── DeterminismTests.fs                               # NEW — build/identity twice equality + env-delta order&dup invariance; argument order significance (SC-005)
├── PurityTests.fs                                     # NEW — record + identity identical across cwd/time/fs changes (SC-006)
├── SurfaceDriftTests.fs                              # NEW — baseline equality + scope-hygiene (Config/BCL/FSharp.Core only) (SC-007)
├── Main.fs                                           # NEW — Expecto entry point
└── FS.GG.Governance.CommandRecord.Tests.fsproj        # NEW — references the core + Config + test frameworks

surface/FS.GG.Governance.CommandRecord.surface.txt    # NEW — committed public-surface baseline (Principle II)
FS.GG.Governance.sln                                 # CHANGED — add the new library + test project
scripts/prelude.fsx                                  # CHANGED — add the F032 design-first FSI section
README.md                                            # CHANGED — short pointer to the new core in the cores list
CLAUDE.md                                            # CHANGED — SPECKIT plan pointer → this plan

# Deliberately UNCHANGED:
src/** (existing), surface/** (existing)             # no merged core/.fsi/surface-baseline changes (FR-009)
tests/** (existing projects)                         # untouched; the new project is purely additive
```

**Structure Decision**: A **new pure-core library** mirroring F029 `FreshnessKey` (a `Model` file of types +
a same-named operations file, a single `Config` reference for verbatim F014 reuse, a byte-stable canonical
identity in the F029 tagged/length-prefixed/injective discipline). It stays free of the git-sensing/host
assemblies and adds the *value and vocabulary* the later host edge (the actual execution/sensing) and the
later provenance row (Phase-11 row 5) will populate and read. Tier 1 (new public surface + baseline), no new
third-party dependency.

## Complexity Tracking

> No Constitution violations to justify — this section is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
