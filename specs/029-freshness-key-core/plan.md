# Implementation Plan: Freshness Key Computation Core

**Branch**: `029-freshness-key-core` | **Date**: 2026-06-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/029-freshness-key-core/spec.md`

## Summary

Open **Phase 11 (Cost, Cache, and Provenance)** with its first checkbox — *"Define freshness keys over rule
hash, artifact hash, command version, generator version, base/head, environment class, and output digest"* —
the decision every prior gate/route/audit row explicitly **carried inputs for but deferred to "Phase 11."**
Continuing this repo's maintainer-confirmed **pure-core-first** rhythm (F015–F025 each landed a pure, total,
deterministic core before a host edge consumed it), this row delivers a single new pure core,
**`FS.GG.Governance.FreshnessKey`**, that answers one question deterministically: *"are these two runs
working against the same world, so may one reuse the other's recorded evidence?"*

The core provides:

- **`FreshnessInputs`** — a closed typed value naming every input that, if changed, invalidates prior
  evidence: the **rule hash**, the set of **covered artifact hashes**, the optional **command version**, the
  **generator version**, the **base** and **head** revisions, the **environment class**, and the **carried
  gate identity** (check / domain / command). It reuses the F014 typed-fact newtypes (`CheckId`, `DomainId`,
  `CommandId`, `EnvironmentClass`) verbatim — the very newtypes the F018 gate `FreshnessKey` is built from —
  and introduces four thin opaque-string newtypes for the Phase-11 additions.
- **`FreshnessKey.compute : FreshnessInputs -> Key`** — a pure, total function rendering the inputs into a
  deterministic, byte-stable, **injective** canonical key (a tagged, length-prefixed string; covered
  artifacts deduped + ordinally sorted so order and duplication never matter). No hashing, no I/O, no clock.
- **`FreshnessKey.matches`** and **`FreshnessKey.diff`** — the total reuse predicate ("all inputs agree")
  and the no-hide explainer (the closed list of differing input categories), the literal foundation of the
  later "cache reusable evidence only when all freshness inputs match" row.

**Plan-time reconciliations (maintainer to confirm):**

- **D1 — New pure core, Config-only dependency (Tier 1).** A new packable library
  `src/FS.GG.Governance.FreshnessKey`, referencing **only** `FS.GG.Governance.Config`. It does **not**
  reference Gates, Snapshot, or any host/edge assembly — it reuses the F014 newtypes the gate identity is
  *built from* rather than the F018 record wrapper, keeping the core minimal (Constitution Engineering
  Constraint: "the rule/evidence helper core MUST NOT depend on git, filesystem scanning"). New `.fsi` +
  new `surface/*.surface.txt` baseline ⇒ **Tier 1**, but **no new third-party `PackageReference`**.
- **D2 — The canonical string *is* the key (no digest).** The key is a deterministic, tagged,
  length-prefixed canonical string, not a SHA digest. Length-prefixing makes it injective across categories
  (FR-006) and the structure keeps it inspectable (FR-007) — a cryptographic digest would add opacity and
  buy nothing for a handful of short inputs. BCL string building only (FR-013).
- **D3 — Base/head use a local `Revision` newtype, not Snapshot's `CommitId`.** To avoid coupling the pure
  key core to the git-sensing `Snapshot` assembly, base/head are a local opaque `Revision = Revision of
  string`. The later edge that assembles `FreshnessInputs` maps `Snapshot.Model.CommitId` → `Revision`. (The
  spec flagged this for the plan; recorded in research D3.)
- **D4 — "Output digest" is out of scope (a verification companion, not a lookup input).** The Phase-11
  plan line lists "output digest" among freshness-key concerns, but an output digest is a *result* of
  running a gate, not an *input* that decides reuse. It belongs to the later cache-write/verify row, not to
  this input-identity key. (Spec Assumptions; research D4.)
- **D5 — Cost is not a freshness input.** The F018 carried key also includes `Cost`, but cost does not
  affect whether evidence is reusable (a reclassified-cost gate with identical inputs/outputs is still
  reusable). `FreshnessInputs` therefore carries Check / Domain / Command / Environment from the carried
  identity and **omits Cost** — consistent with the spec's enumerated category list. (Research D5.)

This row **computes no cache lookup, storage, eviction, or reuse side effect**; it computes no ship verdict,
persists no artifact, and adds no CLI. The merged cores and their `surface/*.txt` baselines are
**untouched**; `dotnet build` / `dotnet test` over the existing projects stays unchanged, and the new
project + its test project are purely additive.

## Technical Context

**Language/Version**: F# on .NET `net10.0` (repo standard; `Nullable=enable`,
`TreatWarningsAsErrors=true` inherited from `Directory.Build.props`). One new `src/` library with a curated
`.fsi`, plus one new test project.

**Primary Dependencies**: **`FS.GG.Governance.Config`** only — for the typed-fact newtypes `CheckId`,
`DomainId`, `CommandId`, and `EnvironmentClass` (`Config.Model`), reused verbatim (FR-009). These are the
exact newtypes the F018 gate `FreshnessKey` is assembled from, so the freshness inputs speak the gate's
vocabulary without referencing the Gates assembly. **No new third-party `PackageReference`** (FR-013): the
canonical-key rendering is BCL string building (`System.Text`/`String`) + `FSharp.Core` only. Test
frameworks already on the central feed (`Directory.Packages.props`): **Expecto**, **Expecto.FsCheck**,
**FsCheck**, **Microsoft.NET.Test.Sdk**, **YoloDev.Expecto.TestSdk**.

**Storage**: None. No database, no files, no runtime storage. The core is a pure value transform; the only
test-side I/O is the surface-drift baseline read (and its `BLESS_SURFACE=1` write), the established pattern.

**Testing**: Expecto + FsCheck, exercising the **public** surface (`FreshnessKey.compute` / `matches` /
`diff` / `value`) over real, literally-constructible `FreshnessInputs` (Principle V — no mocks, no private
helpers). Concerns: (1) **determinism / order-and-dup invariance** (SC-001/SC-002), (2)
**single-field distinction** for every input category (SC-003), (3) **cross-category injectivity** (SC-004),
(4) **inspection / matches⇔diff=[]** (SC-005), (5) **purity** under changed cwd/time/filesystem (SC-006),
(6) **totality** over the degenerate cases (FR-011), (7) **surface drift + scope hygiene** (Principle II,
SC-007). Determinism/injectivity/totality are FsCheck properties; the rest are example tests.

**Target Platform**: Developer/CI .NET SDK running `dotnet test`. No host, no OS-specific surface.

**Project Type**: A new pure-core F# library + its test project. No host, no CLI, no MVU.

**Performance Goals**: N/A. The contract is **determinism and byte-stability**, not latency; the input set
is a handful of short strings per gate.

**Constraints**: Pure / total / deterministic (FR-002/FR-003/FR-008): reads no clock, filesystem, git,
environment, or network; identical inputs always render the identical key regardless of evaluation time,
machine, process, or input order; covered artifacts compared as a **set** (dedup + ordinal sort, FR-004);
the encoding is **injective across categories** (length-prefixed tags, FR-006); `\n`-joined, UTF-8, no
trailing newline. `None` (absent command/version) is distinct from any present value (FR-011). The merged
cores and baselines are not modified (FR-009/SC-007).

**Scale/Scope**: One new `src/` library (`FreshnessKey` — `Model.fsi/fs` + `FreshnessKey.fsi/fs`); one new
test project; one new surface baseline `surface/FS.GG.Governance.FreshnessKey.surface.txt`; two solution
entries; a short `scripts/prelude.fsx` FSI section (design-first proof, Principle I); a `README.md`/plan
pointer. Zero changes to existing `src/`, `surface/`, or merged test projects.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | The public surface is drafted as `Model.fsi` + `FreshnessKey.fsi` and exercised in `scripts/prelude.fsx` (a new F029 section) before any `.fs` body exists; semantic tests call the packed public functions, never private helpers. |
| II. Visibility in `.fsi` | **PASS** | Two curated `.fsi` files are the sole public-surface declaration; the `.fs` files carry no access modifiers. A new `surface/FS.GG.Governance.FreshnessKey.surface.txt` baseline is added and guarded by a reflective `SurfaceDrift` test (the AuditJson/Gates precedent), with the `BLESS_SURFACE=1` re-bless path. |
| III. Idiomatic Simplicity | **PASS — load-bearing** | Plain records, single-case newtypes, one closed DU (`InputCategory`), list dedup+sort, and `String`/`StringBuilder` concatenation. No SRTP, reflection (outside the surface test), custom operators, type providers, or non-trivial CEs. The canonical-string key (D2) is the plainest thing that is deterministic, injective, and inspectable. |
| IV. Elmish/MVU is the boundary for stateful/I/O | **N/A** | No state, no I/O, no workflow — three pure total functions. Like F018 `Gates`, F019 `Route`, F023 `Enforcement`, this is a pure projection that needs no MVU ceremony. |
| V. Test Evidence Is Mandatory | **PASS** | Every input is a real, literally-constructible typed value driven through the genuine public functions (the F025 `Support.fs` real-chain precedent). Tests fail before the implementation matches the contract and pass after. No mocks ⇒ no `Synthetic` disclosure needed. |
| VI. Observability & Safe Failure | **PASS** | `diff` is the observability surface: a non-match is never an opaque "they differ" — it names the exact differing input categories (the no-hide requirement, FR-007). The functions are total: no exception, no swallowed failure, no silent truncation (a degenerate input is an ordinary value, FR-011). |
| Change Classification | **Tier 1 (contracted change — new public API)** | Adds a new public module/assembly and a new surface baseline ⇒ full chain: spec, plan, `.fsi`, baseline, tests. **No new third-party dependency.** No existing public API, baseline, or merged behavior is altered (the cores are consumed/!referenced verbatim; only Config is referenced). |
| Engineering Constraints | **PASS** | F#/.NET `net10.0`; no new third-party `PackageReference` (FR-013); references only `Config`, honoring "the rule/evidence helper core stays minimal — MUST NOT depend on git/filesystem scanning" (the local `Revision` newtype, D3, exists precisely to avoid the Snapshot/git assembly). No rendering package IDs/paths/templates assumed — the inputs are opaque ids/hashes supplied by the caller. Pack output + structured-logging TODOs unaffected (no runtime/host code). |

**Gate result: PASS — no unjustified violations. Complexity Tracking is empty.** Principle IV is the only
N/A (no stateful/I/O workflow); I, II, III, V, VI all have concrete targets and pass.

## Project Structure

### Documentation (this feature)

```text
specs/029-freshness-key-core/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 — decisions D1–D5 + the input-category & encoding facts
├── data-model.md        # Phase 1 — FreshnessInputs, Key, InputCategory, the new newtypes
├── quickstart.md        # Phase 1 — how to build, FSI-exercise, test, and re-bless the surface
├── contracts/           # Phase 1 — the contracts this row commits
│   ├── freshness-key-format.md   # the canonical key encoding (tags, length-prefix, set/option rules, order)
│   └── freshness-key-api.md      # the public function signatures + their laws
├── checklists/
│   └── requirements.md  # spec quality checklist (already present)
└── tasks.md             # Phase 2 — /speckit-tasks output (NOT created here)
```

### Source Code / deliverable layout (repository root)

```text
src/FS.GG.Governance.FreshnessKey/                 # NEW — the pure freshness-key core
├── Model.fsi                                       # NEW — FreshnessInputs, Key, InputCategory, newtypes (sole public surface)
├── Model.fs                                        # NEW — the type bodies (no access modifiers)
├── FreshnessKey.fsi                                # NEW — compute / matches / diff / value signatures + laws
├── FreshnessKey.fs                                 # NEW — pure canonical rendering + comparison
└── FS.GG.Governance.FreshnessKey.fsproj            # NEW — references ONLY ../FS.GG.Governance.Config; no new package

tests/FS.GG.Governance.FreshnessKey.Tests/          # NEW — the semantic tests
├── Support.fs                                       # NEW — real FreshnessInputs builders + FsCheck generators + repoRoot
├── DeterminismTests.fs                              # NEW — compute-twice byte-equality + order/dup invariance (SC-001/002)
├── DistinctionTests.fs                              # NEW — single-field change ⇒ key differs & ¬matches, every category (SC-003)
├── InjectivityTests.fs                              # NEW — cross-category value moves change the key (SC-004)
├── InspectionTests.fs                               # NEW — diff locates the differing category; matches ⇔ diff = [] (SC-005)
├── PurityTests.fs                                   # NEW — key identical across cwd/time/fs changes (SC-006)
├── TotalityTests.fs                                 # NEW — empty artifact set, None command/version, base=head, empty strings (FR-011)
├── SurfaceDriftTests.fs                             # NEW — baseline equality + scope-hygiene (Config/BCL/FSharp.Core only) (SC-007)
├── Main.fs                                          # NEW — Expecto entry point
└── FS.GG.Governance.FreshnessKey.Tests.fsproj       # NEW — references the core + Config + test frameworks

surface/FS.GG.Governance.FreshnessKey.surface.txt   # NEW — committed public-surface baseline (Principle II)
FS.GG.Governance.sln                                # CHANGED — add the new library + test project
scripts/prelude.fsx                                 # CHANGED — add the F029 design-first FSI section
README.md                                           # CHANGED — short pointer to the new core (if it lists cores)

# Deliberately UNCHANGED:
src/** (existing), surface/** (existing)            # no merged core/.fsi/surface-baseline changes (FR-009)
tests/** (existing projects)                        # untouched; the new project is purely additive
```

**Structure Decision**: A **new pure-core library** mirroring F018 `Gates` / F019 `Route` (a `Model` file
of types + a same-named operations file), the established Phase-pattern for a deterministic projection. It
references **only `Config`** (D1) so the freshness core stays minimal and free of the git-sensing assembly,
reusing the F014 newtypes the gate identity is built from. Tier 1 (new public surface + baseline), no new
third-party dependency.

## Complexity Tracking

> No Constitution violations to justify — this section is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
