# Implementation Plan: SDD First-Class Reference Integration (Template + Tutorials)

**Branch**: `072-sdd-first-class-integration` | **Date**: 2026-06-26 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/072-sdd-first-class-integration/spec.md`

## Summary

Make FS.GG.SDD a **first-class citizen inside Governance as a reference integration**
on top of the unchanged 071 template-provider seam. The deliverable is three things,
none of which touch the generic seam core: (1) a concrete **reference template
provider** — a clearly-separated, non-packable example that conforms to the 071
`TemplateProvider` contract and *describes* a minimal but **buildable** F#/.NET runtime
skeleton (source project, test project, package/manifest, entry point); (2) a **layered
end-to-end worked example** delivered as an automated check that takes an empty temp
directory through a documented lifecycle-layer precondition (sibling-owned) and then the
runtime layer via the seam, then `dotnet build`s the result and asserts a deterministic
manifest golden; and (3) three audience-targeted **tutorials** (adopter onboarding,
provider author, SDD↔Governance handoff) anchored to that executable example.

Technical approach (research [D1](./research.md)–[D8](./research.md)): the reference
provider is a pure value (`val provider : TemplateProvider`) in a new `samples/` library
with its **own** curated `.fsi` + **own additive** surface baseline — the generic-core
baselines (`Scaffold`, `ScaffoldManifestJson`) stay byte-identical (SC-006). Its `Emit`
is pure and deterministic (no clock/guid/env), and the emitted skeleton's dependency
closure is **FSharp.Core only**, so `dotnet build` succeeds on the first attempt,
offline, every time (SC-002/SC-003). The worked example reuses 071's `Loop`/`Interpreter`
MVU verbatim; the only new I/O is test-edge process execution (`dotnet build`) and a
disclosed lifecycle-precondition stand-in. A committed, BLESS-regenerated manifest golden
is asserted by the test and embedded/linked by the tutorials, so any provider/seam change
fails the build before a tutorial can silently rot (FR-008, SC-005). Production wiring of
the seam into `fsgg-sdd init` remains owned by the sibling `FS.GG.SDD` repo (FR-013, 071
[D0](../071-runtime-project-templates/research.md)).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (constitution Engineering Constraints).

**Primary Dependencies**: `FS.GG.Governance.Scaffold` (the 071 seam — provider contract +
MVU + edge) and `FS.GG.Governance.ScaffoldManifestJson` (the 071 manifest projection),
both consumed unchanged. The reference-provider library references **only**
`FS.GG.Governance.Scaffold` + `FSharp.Core`. **No** new third-party `PackageReference`;
**no** git/FAKE/FS-scan/rendering dependency anywhere. The e2e test additionally shells
out to the **.NET SDK** (`dotnet build`) — a test-time toolchain prerequisite, not a
library dependency.

**Storage**: Files only, at the test edge — the 071 interpreter writes the provider-
emitted skeleton atomically under an operator-chosen temp target; the test seeds a small
lifecycle-precondition stand-in and runs `dotnet build` over the result. The reference
provider and the manifest projection touch no storage.

**Testing**: Expecto (repo standard). Worked-example tests against a **real** temp
directory that run the seam, assert the runtime skeleton appears, `dotnet build` it
(real evidence), and assert the manifest golden; failure-path tests (contract mismatch
FR-011, no-provider parity FR-010, collision); a surface-drift test for the reference
provider's **own** `.fsi` baseline plus an assertion that the **core** baselines are
unchanged (SC-006).

**Target Platform**: Cross-platform .NET CLI/library (Linux/macOS/Windows). The emitted
skeleton is SDK-only and toolchain-portable.

**Project Type**: Single repository of F# library projects (no web/mobile split). One new
`samples/` library + one new `tests/` project + `docs/tutorials/`.

**Performance Goals**: Not a hot path. Constraints are determinism and onboarding time:
byte-identical manifest on re-run (SC-003); a newcomer reaches a buildable governed
product in under 15 minutes (SC-001).

**Constraints**: Generic-core public surface unchanged (SC-006); reference provider is a
clearly-separated, non-packable example (FR-002); emitted skeleton builds first-attempt,
offline, deterministically (SC-002/SC-003); no-provider path reproduces today's behavior
with zero difference (SC-007); every tutorial step anchored to an e2e-test assertion
(FR-008, SC-005); production `fsgg-sdd init` wiring stays sibling-owned (FR-013).

**Scale/Scope**: One new `samples/` project, one new `tests/` project, one new additive
surface baseline, one golden fixture, three tutorial pages. **Zero** edits to the generic
seam core or its baselines.

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.0.0. Re-checked after Phase 1
design — still passing; no violations, Complexity Tracking empty.*

- **I. Spec → FSI → Semantic Tests → Implementation**: PASS. The reference provider's
  `provider` value is drafted/exercised in FSI (`scripts/prelude.fsx`) before the `.fs`
  body, and the worked example exercises it through the packed `Scaffold` surface a host
  would use (see [quickstart.md](./quickstart.md)).
- **II. Visibility lives in `.fsi`**: PASS. The reference provider ships a curated
  `SddReferenceProvider.fsi` and its **own** `surface/*.surface.txt` baseline + drift
  test; the `.fs` carries no access modifiers. The generic-core baselines are **not**
  touched (research [D1](./research.md), SC-006).
- **III. Idiomatic Simplicity**: PASS. The provider is plain data — a record value and a
  pure `Emit` returning a fixed `ProviderEmission`. No SRTP/reflection/type-providers/
  custom operators (reflection lives only in the surface-drift test). Emitted file
  contents are literal strings.
- **IV. Elmish/MVU boundary**: PASS by **reuse**. The stateful/I/O scaffold workflow is
  071's `Loop` (pure) + `Interpreter` (edge), used unchanged. This feature adds **no** new
  product workflow; the only new I/O (`dotnet build`, fixture seeding) is test-edge process
  execution, not a shipped stateful surface (research [D7](./research.md)).
- **V. Test Evidence Is Mandatory**: PASS — and **stronger** than 071: the provider
  content is now **real and buildable** (a `dotnet build` over a real temp dir), not a
  fake. The only disclosed stand-in is the lifecycle-layer precondition (sibling-owned
  `fsgg-sdd init` output), seeded as a literal fixture with a `// SYNTHETIC:`/precondition
  note at the use site (research [D4](./research.md)).
- **VI. Observability and Safe Failure**: PASS. The missing-toolchain edge case yields an
  actionable prerequisite diagnostic distinguishable from a tool defect (test skips with a
  named rationale when no SDK; the tutorial states the prerequisite); contract-mismatch and
  collision remain the seam's explicit, named refusals (research [D3](./research.md)).
- **Change Classification**: **Tier 1** — adds a new reference-provider public surface and
  new sample/test projects. Requires the full chain: spec, plan, `.fsi`, an **additive**
  surface baseline, test evidence, docs. The generic-core baselines stay untouched
  (SC-006). Declared accordingly (spec Assumptions).
- **Engineering Constraints**: PASS. net10.0; `.fsi` per public module; additive surface
  baseline for the new module; dependency-minimalism (reference provider →
  `Scaffold` + `FSharp.Core` only; emitted skeleton → `FSharp.Core` only; no
  git/FS-scan/FAKE/rendering); `FS.GG.Governance.*` identity; the sample is
  `IsPackable=false` (not published — research [D1](./research.md)).
- **Genericity (operating rule)**: PASS — this is the feature's whole point. **All**
  provider-, package-, target-, and layout-specific knowledge lives in the `samples/`
  example; the generic seam core gains **none** (FR-002, SC-006).

**Result**: No violations. Complexity Tracking below is intentionally empty.

## Project Structure

### Documentation (this feature)

```text
specs/072-sdd-first-class-integration/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions D0–D8
├── data-model.md        # Phase 1 — entities & the emitted-skeleton shape
├── quickstart.md        # Phase 1 — runnable validation guide
├── contracts/           # Phase 1
│   └── reference-provider.md   # the concrete emission + golden-manifest anchor (over 071's provider-contract)
├── checklists/
│   └── requirements.md  # (created by /speckit-checklist; NOT here)
└── tasks.md             # Phase 2 — created by /speckit-tasks (NOT here)
```

### Source Code (repository root)

```text
samples/                                                  # NEW top-level — clearly separates example from product (FR-002)
└── FS.GG.Governance.Sample.SddReferenceProvider/         # NEW — the reference template provider (example, non-packable)
    ├── SddReferenceProvider.fsi / .fs                    #   val provider: TemplateProvider — describes a buildable F#/.NET skeleton; pure Emit
    └── FS.GG.Governance.Sample.SddReferenceProvider.fsproj   # references Scaffold + FSharp.Core; IsPackable=false

tests/
└── FS.GG.Governance.Sample.SddReferenceProvider.Tests/   # NEW — the layered worked example + e2e build check
    ├── Support.fs            #   lifecycle-precondition stand-in (disclosed), temp dirs, `dotnet build` runner, golden path
    ├── WorkedExampleTests.fs #   empty dir → lifecycle layer → seam → runtime skeleton appears, BUILDS, manifest == golden (US1)
    ├── FailurePathTests.fs   #   contract-mismatch refusal (FR-011), no-provider parity (FR-010), collision refusal
    ├── SurfaceDriftTests.fs  #   reference provider's OWN baseline; ASSERTS the two core baselines are byte-identical (SC-006)
    └── Main.fs

surface/
└── FS.GG.Governance.Sample.SddReferenceProvider.surface.txt   # NEW (additive) baseline; core baselines untouched

fixtures/
└── sdd-reference/
    └── scaffold-manifest.golden.json                     # NEW — deterministic manifest the tutorial shows & the test asserts (BLESS-regenerated)

docs/tutorials/                                           # NEW — anchored to the executable example (FR-008)
├── adopter-onboarding.md          # FR-005 / US1 — empty dir → scaffold → govern → verify → ship
├── provider-author.md             # FR-006 / US2 — clone the reference, author & register your own provider, no tool change
└── sdd-governance-handoff.md      # FR-007 / US3 — readiness → routing/evidence/enforcement, mapping per ADR 0002
```

**Structure Decision**: Single-repo F# library layout (the repo's only shape). The
reference provider lives under a **new top-level `samples/`** directory — not `src/` — to
make the example/product separation structural and obvious (FR-002, SC-006), mirroring the
constitution's "rendering is one external customer, not this tool's internal shape" stance.
It is a library with a curated `.fsi` and its **own additive** surface baseline (Principle
II: a faithful clone target), but `IsPackable=false` (it is an example, not a published
package — research [D1](./research.md)). Tests live under `tests/` per repo convention; the
surface-drift test's `findRepoRoot` walk makes location flexible. Both new projects are
added to `FS.GG.Governance.sln`. No CLI subcommand or `Program.fs` is added to the product —
production host wiring stays in `../FS.GG.SDD` (FR-013, 071 D0).

## Deferred / Out of Scope (tracked)

Per spec and 071 research [D0](../071-runtime-project-templates/research.md), these are
explicit, bounded follow-ups — **not** silent omissions (constitution: intentional deferral
MUST be explicit in the plan):

- **Production host wiring** of the seam into `fsgg-sdd init` (provider-selection flag,
  lifecycle-skeleton-first ordering, exit-code mapping, manifest persistence) — sibling
  `FS.GG.SDD` repo (FR-013). This feature ships only the Governance-side reference + docs.
- **Provider discovery/resolution** (registry, assembly loading) — host concern; the worked
  example selects the resolved reference value directly (071 D0/D1).
- **A `governance-handoff.json` consumer** (reader/parser, evidence adapter) — ADR 0002's
  queued Governance-side work; the handoff tutorial is **explanatory/cross-referential**
  only and ships no consumer code here (research [D8](./research.md)).
- **Running** the emitted skeleton's tests (`dotnet test` with an external framework) — the
  e2e check asserts `dotnet build` only, to keep first-attempt success offline-deterministic
  (research [D2](./research.md)); the emitted test project builds but is not executed.
- **The lifecycle skeleton itself** (`.fsgg/`/`work/`/`readiness/`) — authored by
  sibling-owned `fsgg-sdd init`; the example seeds a disclosed minimal stand-in to
  demonstrate layering and reserved-path avoidance only (research [D4](./research.md)).

## Complexity Tracking

> No constitution violations. No entries.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| — | — | — |
