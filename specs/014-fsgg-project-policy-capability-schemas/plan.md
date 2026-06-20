# Implementation Plan: `.fsgg` Project, Policy, Capability, and Tooling Schemas

**Branch**: `014-fsgg-project-policy-capability-schemas` (active spec; git branch currently `main`) | **Date**: 2026-06-20 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/014-fsgg-project-policy-capability-schemas/spec.md`

## Summary

Deliver the **source-of-truth schemas** for the four versioned `.fsgg` files —
`project.yml`, `policy.yml`, `capabilities.yml`, and `tooling.yml` — together with
strict parsing, deterministic diagnostics, deterministic path normalization,
cross-reference validation, surface classification, and the conversion of valid
declarations into **typed, YAML-free, product-neutral facts**. The feature stops at the
typed facts: git/CI sensing, path-to-capability routing, the gate registry, profile
enforcement, and the `ship` command are later Phase-2 features that *consume* these facts
(FR-016).

The work lands as a new, optional, **light configuration library**
`FS.GG.Governance.Config` plus its test project. The library has two halves split across
the Constitution's MVU/I-O boundary (Principle IV):

- a **pure validation core** (`Schema.validate : RawSource -> Validation`) that parses,
  validates strictly, normalizes paths, resolves cross-references, classifies surfaces,
  and emits either typed facts or ordered diagnostics — performing no I/O and never
  throwing; and
- a **thin edge loader** (`Loader`) that reads a `.fsgg` directory into a `RawSource`
  (distinguishing *absent* from *present-but-invalid*, FR-015) through an injected file
  reader port, then hands it to the pure core.

YAML is read into a generic node tree with **YamlDotNet** (the design doc's recommended
dependency); every strictness rule — unknown fields, duplicate ids, `schemaVersion`
range, path escape, dangling references — is our own code over the node tree, not
YamlDotNet's lenient object binding. The dependency is isolated to this library; Kernel
and Host stay BCL-only.

**Confirmed during planning:**

- **YAML parsing**: YamlDotNet in parse-to-node mode only; all strictness is hand-written.
- **Required files**: `project.yml` and `capabilities.yml` are required for a minimally
  valid declaration; `policy.yml` and `tooling.yml` are optional but fully validated when
  present (FR-015 distinguishes *absent-optional* from *present-but-invalid*).

## Technical Context

**Language/Version**: F# on .NET, `net10.0` from `Directory.Build.props`.

**Primary Dependencies**: One new runtime `PackageReference` — **YamlDotNet** — pinned
centrally in `Directory.Packages.props` and referenced only by the new
`FS.GG.Governance.Config` project. Used in parse-to-node mode (`YamlStream` /
`YamlDocument` / `YamlNode`) only; the typed model and all validation are hand-written F#.
No object-graph deserialization (that path is lenient and cannot reject unknown fields).
The library does **not** reference the Kernel: it produces its own config-domain typed
facts that later features bridge into kernel facts, keeping "the Kernel never sees YAML or
product vocabulary" (spec Layering assumption, constitution operating rule). Test-only
packages remain the centrally pinned Expecto/FsCheck/VSTest set already in
`Directory.Packages.props`.

**Storage**: Read-only with respect to the governed repository. The loader reads the four
`.fsgg/*.yml` files from a caller-supplied directory and writes nothing. No mutation of
governed trees; no temp files. The pure core touches no filesystem at all.

**Testing**: `dotnet test` (Expecto via VSTest). Tests exercise the public library surface
(not private helpers): the pure `validate` over real fixture YAML strings; the `Loader`
edge over real fixture directories on the actual filesystem; determinism (validate twice →
byte-identical typed facts and diagnostics); order-independence (FsCheck permutation of
authored domains/surfaces/checks/commands → identical typed facts); one malformed fixture
per diagnostic id; one accepted fixture per MVP surface class; surface-drift check against
`surface/FS.GG.Governance.Config.surface.txt`; and an FSI/prelude transcript that loads the
built library and validates a fixture product.

**Target Platform**: Cross-platform .NET library; validated on the Linux dev host.

**Project Type**: Optional packable F# class library plus one test project — the same shape
as the existing adapter libraries.

**Performance Goals**: Deterministic, bounded validation rather than throughput. A single
`validate` reads each file's node tree once, normalizes each path once, and produces
byte-for-byte stable typed facts and diagnostics for identical input (SC-002). No wall-clock
or environment-derived fields enter the typed facts.

**Constraints**: Strict parsing only — unknown fields, missing required fields, malformed
values, duplicate ids, out-of-root paths, and dangling references are failures, never
partial acceptance or silent correction (FR-006). Deterministic ordering of every emitted
list (domains, surfaces, checks, commands, diagnostics). No raw YAML text and no
product-specific vocabulary in the typed facts (FR-010, SC-005). Pure validation core with
no I/O; file reading isolated behind the loader's injected port (Principle IV). Out of scope
held firm: no routing, no git/CI facts, no gate registry, no enforcement, no `ship`
(FR-016).

**Scale/Scope**: One new production project (`src/FS.GG.Governance.Config`) and one test
project (`tests/FS.GG.Governance.Config.Tests`). Public modules are `Model`, `Schema`, and
`Loader`, each with a curated `.fsi` and a single combined surface baseline. Four file
schemas at `schemaVersion: 1`. Five MVP surface classes (routine/unmanaged, governed root,
protected package/API, generated view, release surface). One diagnostic id per malformed
class named in the spec.

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.0.0. Re-checked after Phase 1
design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | [`contracts/Model.fsi`](./contracts/Model.fsi), [`contracts/Schema.fsi`](./contracts/Schema.fsi), [`contracts/Loader.fsi`](./contracts/Loader.fsi), and [`contracts/fsgg-schema.md`](./contracts/fsgg-schema.md) define the public surface and the YAML authoring contract before implementation. `tasks.md` must order `.fsi` → FSI/prelude sketch → semantic tests → implementation → surface baseline. |
| II. Visibility lives in `.fsi` + surface baseline | **PASS** | `Model.fsi`, `Schema.fsi`, `Loader.fsi` are the sole public surface; `.fs` files carry no top-level access modifiers. Add `surface/FS.GG.Governance.Config.surface.txt` and a surface-drift test. |
| III. Idiomatic simplicity | **PASS** | Plain records/DUs, a hand-written validation walk over a YAML node tree, list folds for ordering. No SRTP, reflection, type providers, custom operators, or non-trivial computation expressions. YamlDotNet is used only as a node reader, not via clever binding. Any `mutable` accumulator in a normalization/validation fold is disclosed at the use site. |
| IV. Elmish/MVU boundary | **PASS** | The validation core is a pure total function and needs no MVU ceremony. The only I/O — reading the four `.fsgg` files and distinguishing absent vs present — is isolated at the edge: `Loader` takes an injected `FileReader` port (I/O represented as a supplied function), assembles a `RawSource` value, and calls the pure `validate`. Interpretation happens only at the edge (the library allowance in Principle IV). [`research.md`](./research.md) D3 records why a full Elmish `Program` is unnecessary here. |
| V. Test evidence mandatory | **PASS** | Tests run against real fixture YAML and real fixture directories on the actual filesystem; determinism and order-independence are property-tested; each diagnostic id and each surface class has a real fixture. No synthetic evidence is anticipated (no agent, network, or clock); if any appears it carries `Synthetic` in the name and a use-site disclosure. |
| VI. Observability & safe failure | **PASS** | Every diagnostic carries a stable id, the offending file, a locating reference, and a fix hint (FR-013). The result distinguishes *file absent* from *file present but invalid* (FR-015) and a malformed-input finding from a tool defect (a tool defect surfaces as a test failure / fail-fast, never as a `Diagnostic`). No silent correction or partial acceptance. |
| Change Classification | **Tier 1** | New public, packable surface (config schemas, validation, typed facts), new public `.fsi`, new surface baseline, and a new runtime dependency — consistent with the spec's stated tier for `014`. |
| Engineering Constraints | **PASS** | `net10.0`; `FS.GG.Governance.*` identity; one-way dependency direction (Config → YamlDotNet + FSharp.Core only; Kernel/Host/adapters/CLI unaffected and do not reference Config in this feature). The new dependency states need, pin, and owner below; the kernel stays BCL-only, honoring the constraint that the core rule/evidence library takes no such dependency. |

**New-dependency justification (Engineering Constraints):** **YamlDotNet** —
*Need*: `.fsgg` files are authored in YAML (spec assumption); a correct YAML reader is
required and hand-rolling the full spec is out of proportion. *Scope*: parse-to-node only,
isolated to `FS.GG.Governance.Config`; Kernel/Host stay BCL-only. *Pin*: centrally in
`Directory.Packages.props` (`16.3.0`). *Owner*: `FS.GG.Governance.Config` maintainer.
*Strictness*: not delegated to the library — unknown-field/duplicate-id/version/path/
dangling-reference rules are our own code over the node tree.

**Gate result: PASS — no unjustified violations. Complexity Tracking remains empty.**

## Project Structure

### Documentation (this feature)

```text
specs/014-fsgg-project-policy-capability-schemas/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   ├── Model.fsi        # typed facts + diagnostics public surface
│   ├── Schema.fsi       # pure validate + RawSource
│   ├── Loader.fsi       # edge loader (FileReader port + interpreter)
│   └── fsgg-schema.md   # the four YAML file schemas (authoring contract)
├── readiness/           # FSI transcripts + SC-006 traceability note (created during tasks)
└── tasks.md             # Created by /speckit-tasks, NOT by this command
```

### Source Code (repository root)

```text
src/FS.GG.Governance.Config/                      # NEW optional configuration library
├── FS.GG.Governance.Config.fsproj                # references YamlDotNet only
├── Model.fsi                                     # = contracts/Model.fsi
├── Model.fs                                      # typed-fact records/DUs, diagnostic ids, ordering
├── Schema.fsi                                    # = contracts/Schema.fsi
├── Schema.fs                                     # pure parse → validate → normalize → classify
└── Loader.fsi                                    # = contracts/Loader.fsi
└── Loader.fs                                     # FileReader port + filesystem interpreter + load

tests/FS.GG.Governance.Config.Tests/             # NEW semantic tests
├── FS.GG.Governance.Config.Tests.fsproj
├── fixtures/                                     # valid all-four, per-diagnostic, per-surface-class products
│   ├── valid-complete/.fsgg/{project,policy,capabilities,tooling}.yml
│   ├── malformed-*/...                           # one defect per fixture
│   └── surface-*/...                             # one MVP surface class per fixture
├── SchemaTests.fs                                # pure validate: success + typed-fact assertions
├── DiagnosticTests.fs                            # one malformed fixture per diagnostic id
├── DeterminismTests.fs                           # validate twice → identical; FsCheck order-independence
├── SurfaceClassTests.fs                          # each MVP surface class classifies correctly
├── LoaderTests.fs                                # edge over real fixture dirs; absent vs invalid
├── SurfaceDriftTests.fs                          # baseline drift check
└── Main.fs

scripts/prelude.fsx                               # extend with a Config validate sketch
surface/FS.GG.Governance.Config.surface.txt       # NEW public surface baseline
FS.GG.Governance.sln                              # add Config project and Config test project
Directory.Packages.props                          # add YamlDotNet PackageVersion
```

**Structure Decision**: a new `FS.GG.Governance.Config` class library, sibling to the
Kernel/Host/adapters, chosen as the spec's "light configuration library" home. It depends
only on YamlDotNet (+ FSharp.Core) and is referenced by no existing project in this feature,
keeping the new dependency isolated and the dependency direction one-way. Splitting `Schema`
(pure) from `Loader` (edge) places the Constitution's MVU/I-O boundary exactly at the
filesystem read, so the entire validation contract is testable as a pure function over
fixture strings while the loader is exercised over real directories.

## Complexity Tracking

> No unjustified Constitution Check violations.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| - | - | - |
