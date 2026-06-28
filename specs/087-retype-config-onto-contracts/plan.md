# Implementation Plan: Re-type Config loader/schema onto FS.GG.Contracts

**Branch**: `087-retype-config-onto-contracts` | **Date**: 2026-06-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/087-retype-config-onto-contracts/spec.md`

## Summary

Make `FS.GG.Governance.Config` a **consumer** of the org-shared schema-authority package
`FS.GG.Contracts` (`Fsgg.Schemas`, pinned `fsgg-contracts@1.0.1`, caps=2) instead of a
second source of truth. The single thing genuinely duplicated today is the set of per-file
supported `.fsgg` `schemaVersion` constants — `Schema.supportedVersionFor` hard-codes
`capabilities = 2`, `project/policy/tooling = 1`. The change replaces those literals with the
package constants `Fsgg.Schemas.{governanceVersion, policyVersion, capabilitiesVersion,
toolingVersion}`, so the version numbers are single-sourced across the org and a future SDD
bump flows in by re-pinning the package rather than by editing Governance code.

**Key discovery (constrains scope — see [research.md](./research.md) D1):** Governance's
`Model` record/DU surface (`ProjectFacts`, `PolicyFacts`, `CapabilityFacts`, `ToolingFacts`,
their parts, the identity newtypes, the `Cost`/`Maturity`/`SurfaceClass`/`GeneratedProductTier`/
`EnvironmentClass` enums, and the `Diagnostic`/`DiagnosticId`/`Validation` model) are **derived
typed-fact and diagnostic types with no `Fsgg.Schemas` equivalent**. The Contracts record
shapes (`ProjectSchema`, `ProvidersSchema`, `AgentsSchema`, `GovernanceHandoffSchema`, …)
describe *raw on-disk artifacts authored/emitted elsewhere*, not Governance's *post-validation
facts*. So FR-003 resolves to "no record shape is duplicated → nothing but the four version
constants is single-sourced," and FR-004's Governance-owned types all stay local. This is the
same shape as the SDD#9 / Templates#13 re-typings: adopt the shared constants/records that
genuinely overlap, keep repo-specific derived types.

Consequently the public Config surface is **byte-identical** — the constants are consumed
*inside* `Schema.fs`; `supportedVersionFor` still returns Governance's own `SchemaVersion`
newtype. No `.fsi` changes, no re-export, FR-009's preferred outcome achieved for free. The
delivered work is: dependency plumbing (PackageReference + central version + lockfile
regeneration) and the constant swap, proven by an unchanged build + full test suite +
byte-identical surface baseline and goldens.

**Implementation status:** the working tree already contains the mechanical change
(`Directory.Packages.local.props` central version, the `FS.GG.Contracts` PackageReference in
`FS.GG.Governance.Config.fsproj`, `open Fsgg` + the constant swap in `Schema.fs`, and the
regenerated `packages.lock.json`). This plan documents the design behind that change and the
verification chain that still gates it; tasks/implement complete the evidence.

## Technical Context

**Language/Version**: F# on .NET `net10.0` (FSharp.Core 10.1.301)

**Primary Dependencies**: `FS.GG.Contracts` 1.0.1 (new — BCL-only, FSharp.Core only; the
schema-authority package), `YamlDotNet` 16.3.0 (existing, parse-to-node validation). Central
versions in `Directory.Packages.local.props`; locked restore via committed `packages.lock.json`.

**Storage**: N/A — Config reads `.fsgg/{governance,policy,capabilities,tooling}.yml` from disk
through the `Loader` `FileReader` port; no database.

**Testing**: Expecto. Full suite is the delivery gate via `dotnet fsi build.fsx test`.
Config-specific tests in `tests/FS.GG.Governance.Config.Tests` (validation, diagnostics,
determinism property tests, version handling). Surface drift via `SurfaceDriftTests` /
`dotnet fsi pack-and-apicheck.fsx` against `surface/FS.GG.Governance.Config.surface.txt`.

**Target Platform**: Linux/CI build host; cross-platform .NET library consumed by 50+ sibling
Governance projects.

**Project Type**: Single F# class library within a 162-project solution (`FS.GG.Governance.sln`).

**Performance Goals**: None changed. Validation is the same pure parse-to-node pass; reading
a compile-time constant has zero runtime cost vs. a literal.

**Constraints**: Zero observable-behavior change (identical typed facts, identical diagnostics,
preserved determinism, byte-identical downstream goldens/snapshots). Restore must succeed in
**locked mode** from the org GitHub Packages feed at the pinned `1.0.1`. Public Config surface
must not break the 50+ downstream consumers.

**Scale/Scope**: One library re-typed; one function body (`supportedVersionFor`) changed; one
new dependency edge; lockfile(s) regenerated. No new modules, no new public symbols.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Change Classification (Tier 1).** Declared Tier 1 in the spec because it **introduces a new
  dependency** (`FS.GG.Contracts`). That alone is Tier 1 per the constitution, independent of
  behavior. Full artifact chain carried: spec ✅, plan ✅, `.fsi` review (result: no change
  needed — see below), surface-area baseline review (result: byte-identical), test evidence
  (full suite + parity checks). **PASS.**
- **I. Spec → FSI → Semantic Tests → Implementation.** No new public surface is designed, so
  there is no new FSI shape to draft. The existing FSI/prelude exercises `supportedVersionFor`
  and `validate` unchanged; the constant swap is validated by the existing semantic tests
  (version-handling + unsupported-`capabilities` diagnostic). **PASS.**
- **II. Visibility in `.fsi`, not `.fs`.** No `.fsi` edits; `Schema.fs` adds only `open Fsgg`
  and swaps literals for `Schemas.*` reads inside the existing `supportedVersionFor` body — no
  new top-level binding, no access modifier introduced. Surface baseline stays byte-identical
  (a Tier-1 requirement: a Tier-1 change that *fails* to keep `.fsi`/baselines correct is a
  defect — here they are correct because the surface does not move). **PASS.**
- **III. Idiomatic Simplicity.** Plainer after the change: four literals become four named
  constant reads. No custom operators, SRTP, reflection, type providers, or non-trivial CEs
  introduced. **PASS.**
- **IV. Elmish/MVU boundary.** Unchanged. The pure core (`Schema.validate`) stays pure and
  total; the I/O edge (`Loader`) keeps its `FileReader` port and signatures (FR-007). The
  feature touches a pure constant resolution only. **PASS.**
- **V. Test Evidence.** Behavior is asserted unchanged by tests that already exist and stay
  green; the version-source change is guarded by a no-local-literal check (SC-004) and the
  unsupported-version diagnostic test (FR-005). No synthetic evidence introduced. **PASS.**
- **VI. Observability & Safe Failure.** A missing feed/credential or pin mismatch is a
  build/restore failure surfaced loudly before any behavior question (spec edge case) — no
  silent fallback to old local types. Unchanged validation diagnostics keep distinguishing
  tool defect from malformed input. **PASS.**
- **Engineering Constraints.** F# on `net10.0` ✅. New dependency states need (single-source
  schema versions), version-pinning strategy (registry pin `fsgg-contracts@1.0.1`, central
  `PackageVersion`, locked `packages.lock.json`), and maintenance owner (FS.GG.SDD owns the
  package; Governance owns the pin bump) ✅ — satisfying the "each new dependency states need,
  pin, owner" rule. `FS.GG.Contracts` is BCL-only (FSharp.Core), so it does **not** drag in
  FAKE/git/Skia/NuGet-publishing/etc. forbidden to the core ✅. No rendering identity assumed ✅.

**No violations. Complexity Tracking table is empty (nothing to justify).**

## Project Structure

### Documentation (this feature)

```text
specs/087-retype-config-onto-contracts/
├── plan.md              # This file (/speckit-plan output)
├── spec.md              # Feature spec (already present)
├── research.md          # Phase 0 output — boundary & restore decisions
├── data-model.md        # Phase 1 output — shared-vs-local type boundary
├── quickstart.md        # Phase 1 output — parity verification guide
├── contracts/
│   └── fsgg-contracts-consumption.md   # Phase 1 output — the consumed contract surface
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/FS.GG.Governance.Config/
├── FS.GG.Governance.Config.fsproj   # + <PackageReference Include="FS.GG.Contracts" />   (done)
├── Model.fsi / Model.fs             # UNCHANGED — Governance-owned facts/enums/diagnostics (FR-004)
├── Schema.fsi                       # UNCHANGED — public surface does not move (FR-009)
├── Schema.fs                        # + open Fsgg; supportedVersionFor reads Schemas.* (done)
├── Loader.fsi / Loader.fs           # UNCHANGED — I/O edge signature preserved (FR-007)
└── packages.lock.json               # regenerated: adds FS.GG.Contracts + FSharp.Core (done)

Directory.Packages.local.props       # + <PackageVersion Include="FS.GG.Contracts" 1.0.1 /> (done)
surface/FS.GG.Governance.Config.surface.txt   # MUST stay byte-identical (verification, FR-009)

tests/FS.GG.Governance.Config.Tests/ # parity gate: validation, diagnostics, determinism,
                                     #   unsupported-version, no-local-literal guard
```

**Structure Decision**: Single-project library change. No new projects, modules, or files. The
only edited production files are `FS.GG.Governance.Config.fsproj`, `Schema.fs`,
`Directory.Packages.local.props`, and regenerated `packages.lock.json` across the dependency
closure (locked-restore requires every transitively-affected project's lockfile to reflect the
new graph — hence the broad `packages.lock.json` churn already in the working tree).

## Complexity Tracking

> No Constitution Check violations — table intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| — | — | — |
