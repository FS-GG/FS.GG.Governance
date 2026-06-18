# Implementation Plan: The Design-System Adapter — A Second, Unrelated Domain Adopts The Kernel From Fixtures

**Branch**: `011-adapter-designsystem` | **Date**: 2026-06-18 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/011-adapter-designsystem/spec.md`

## Summary

Add the **design-system adapter** (F11) as a **new pure project** — `FS.GG.Governance.Adapters.DesignSystem` —
that depends on the already-shipped F09 SPI (and through it the kernel, F01–F07) and **nothing else** (never
F10). It is the **second concrete production adapter** and **domain #2 of Milestone M3 — the adoption bar**:
it governs adherence to a **design language** (the worked example is Ant Design) by supplying — through the
F09 `Adapter<'fact,'artifact,'change>` SPI — **only** its own five components and getting inference,
three-valued verdicts, the reified `Check` algebra and its interpreters, `CheckTier` arbitration, the
evidence/taint DAG, JSON explanation and contract, severity, and routing/run-modes **for free**. It is
**pure** — values and total folds, no state, no I/O, no `Model`/`Msg`/`Effect`, no interpreter — so
Constitution **Principle IV is N/A**, exactly as for F01–F07, F09, and F10.

The thesis of this feature is **generality by difference, not by a second copy**. Where the Spec Kit adapter
(F10) governs a **staged lifecycle** — phases-as-facts, a `whenPhase` guard, a single merge fence, a
constitution dial — the design-system adapter governs a **flat design surface** with **no lifecycle at all**:
no `Phase`, no `whenPhase`, no merge fence, no dial. It shares **none** of F10's vocabulary or shape and
references F10 not at all. That two domains this different plug into one unchanged kernel is the evidence the
abstraction sits at the right altitude.

It introduces two modules, in compile order:

1. **`DesignSystem` — the domain vocabulary, the artifact map, the probes, the kernel wiring.** The closed
   `DesignSystemFact` union (`PolicySelected`, `DesignRule`, `SurfaceObservation` carrying a sensed boolean,
   `MeasurementState` carrying an authored `EvidenceState`, `VerdictRestsOn`, `ArtifactPresent`, plus the
   `DesignGov of RuleOutcome` embed case the F04 `Bridge` uses), the `DesignArtifactRef` enumeration
   (`TokenDocument`, `GeneratedTokenSurface`, `RenderedCapture`, `InteractionStateSpec`, `PagePatternSpec`),
   the `DesignChange` shape (a `Set<DesignArtifactRef>` of touched surfaces — **no phase**), the
   `DesignSystem.toRef` artifact mapping, the `DesignSystem.identify` fact identity, the `DesignSystem.bridge`
   kernel wiring, and the declared probes `DesignSystem.surfaceMatches`/`contrastMeets`/… read against a
   **fixture token tree** as data. There is **no phase guard** — the keystone *absence* that proves
   adoption did not copy domain #1.

2. **`Catalog` — the tiered rule catalog, the high-stakes fences, the adapter.** The catalog is a list of
   reified `CheckRule`s, each carrying a `CheckTier` and a `Severity` and rendering to a sentence: the
   **deterministic blocking** checks (`tokenDrift`, `contrastPolicy`, `tokenSurfaceGate`), the
   **deterministic advisory** checks (`spacingScale`, `controlHeightDefaults`, `intentCoverage`,
   `visualStateResolution`), the **`AgentReviewed` advisory** judgement checks via the `Opaque` hatch
   carrying a `Question` (`renderedMatchesIntent`, `fourValues`, `pagePatternCorrect`, `colourInformational`,
   `motionRestraint`, `elevationLayering`), one **deterministic blocking** evidence-honesty check
   (`evidenceMeasured`, the F05-taint realization — a deterministic verdict may not rest on a synthetic /
   unmeasured input), and one **`HumanOnly` blocking** check (`adoptNewPolicy`). `tokenSurfaceFence` is the
   single F07 `Fence` naming the public token surface. `adapter judge` assembles the one `Adapter` value:
   the five SPI components plus the `bridge`.

The keystone behaviours, each a direct translation of `docs/governance-design/adapters.md` ("Design-system
adapter"): **(1)** a second, unrelated domain adopts the kernel by supplying **only** the five SPI components
plus the F04 `Bridge` wiring — no phases, no `whenPhase`, no merge fence, none of domain #1's machinery
(US1); **(2)** the catalog assigns each rule a `CheckTier` (*who decides*) and a `Severity` (*whether it
blocks*) — the deterministic, contract-bearing token/contrast/surface checks (and the always-blocking
`evidenceMeasured`) are the few that **block**; the visual-judgement rules use the `Opaque` hatch, which keeps
them out of the `Deterministic` tier and routes them to an agent whose prompt is the rule's `Question`;
adopting a **new** design policy is `HumanOnly` (US2); **(3)** the adapter runs entirely against a **fixture
token tree** — a few JSON/RON files, no rendering dependency — and **no rendering vocabulary leaks into the
generic kernel or SPI** (US3); **(4)** every deterministic rule **renders to a sentence and hashes** stably,
so what the published contract advertises is byte-for-byte what `eval` enforces, and the F04 agent-review
cache key does not move under commutative re-ordering (US4); and **(5)** the adapter **lifts unchanged** into
a project coproduct and composes alongside the Spec Kit adapter (F10) at one root — standalone and lifted
`(verdict, provenance)` are identical (the F09 faithful-lift guarantee), the two unrelated domains coexisting
without either knowing about the other (US5).

The feature **depends on the SPI only** (a single `ProjectReference` to `FS.GG.Governance.Adapters.Spi`,
and through it the kernel) and adds **no new `PackageReference`** — BCL + `FSharp.Core` + SPI + kernel, **no
rendering library** (FR-016). It is exercised through its built public surface — two curated `.fsi` contracts,
[`contracts/DesignSystem.fsi`](./contracts/DesignSystem.fsi) and [`contracts/Catalog.fsi`](./contracts/Catalog.fsi)
— with a **new** surface-area baseline (`surface/FS.GG.Governance.Adapters.DesignSystem.surface.txt`) and a
DesignSystem-side surface-drift / dependency-hygiene test that asserts the adapter references only
BCL/`FSharp.Core`/Spi/Kernel **and not F10** (FR-017). The standalone-vs-lifted faithful-lift guarantee
(FR-014) is proven by composing the adapter alongside the **real Spec Kit adapter (F10)** at a test root
(F11's test project may reference F10; the shipped library never does).

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (inherited from `Directory.Build.props`).

**Primary Dependencies**: **None new** (no `PackageReference`). `FS.GG.Governance.Adapters.DesignSystem` takes
a single `ProjectReference` on `FS.GG.Governance.Adapters.Spi` (which references the kernel) and uses only
`FSharp.Core` + the BCL. There is **no rendering library, no I/O, no `System.IO`, no serializer** — the
adapter is pure values and folds over a fixture token tree supplied as facts. Test project only: Expecto +
FsCheck, already pinned centrally; the **test** project additionally references `FS.GG.Governance.Adapters.SpecKit`
(F10) solely to prove cross-domain composition — the shipped adapter does not.

**Storage**: N/A. Pure value/fold layer — no state, no persistence. Sensing a live design system (reading the
token tree, capturing rendered output, computing contrast ratios, hashing artifact content) into
`DesignSystemFact`s is the F08 effects shell / F12 CLI's job, not this feature (FR-015); tests feed facts
drawn from a small fixture token tree directly.

**Testing**: `dotnet test`. Tests exercise the **public** surface through the built library and
`scripts/prelude.fsx` (Principle I). Targeted tests for: the adapter supplying exactly the five components and
reusing 100% of kernel facilities — no inference/arbitration/evidence/render/hash/explain/severity/routing
code, no artifact-authoring operation, and **no phase/lifecycle/`whenPhase`/merge-fence machinery copied from
F10**, in the adapter module (SC-001); the **tier split** — the deterministic token-drift / contrast-policy /
token-surface-gate rules produce definite verdicts and are `Blocking`, the `Opaque` judgement rules stay out
of the `Deterministic` tier and route to an agent with the rule's `Question`, and `adoptNewPolicy`
(`HumanOnly`) never resolves deterministically (SC-002); the **fixture token tree** — the full catalog
evaluates and explains with no rendering library on the path, and the kernel/SPI surfaces carry **zero**
rendering/token/colour/layout vocabulary (SC-003); **render-and-hash** — every rule renders to a non-empty
sentence whose `Check.explain` root verdict equals `eval`, the published `Statement` equals `Check.render`,
and a deterministic rule's hash is invariant under commutative re-ordering (SC-004/SC-005); the **faithful
lift** — for 100% of the catalog the lifted rule's `(verdict, provenance)`, render, hash, and reads equal the
standalone original when composed alongside the real F10 adapter at a root (SC-006); the **adoption bar** —
two unrelated domains coexist, neither references the other, dropping one `Lifted` removes it cleanly (SC-007);
and the new DesignSystem surface-drift + dependency-hygiene baseline (DesignSystem → BCL/FSharp.Core/Spi/Kernel
only, **not F10**, SC-008).

**Target Platform**: cross-platform .NET library (Linux dev host).

**Project Type**: **library** — a new pure adapter library `FS.GG.Governance.Adapters.DesignSystem` (+ its
test project), separate from and depending on the pure SPI (FR-016), a sibling of (never a dependant on) the
F10 Spec Kit adapter.

**Performance Goals**: correctness, determinism, and faithful lifting — not throughput. Every function is a
**total** value/fold; no measured hot path. The evidence taint closure is the kernel's existing
least-fixed-point (F05), unchanged.

**Constraints**: the adapter is **pure & total** — no I/O, no state, no `Model`/`Msg`/`Effect`, no interpreter
(FR-015, Principle IV N/A). It supplies **exactly the five SPI components** + the F04 `Bridge` wiring and
**nothing more** (FR-003); inference, arbitration, evidence, render/hash/explain, severity, and routing are
all reused (SC-001). It is an **observer, not an author** — no operation generates, edits, renders, or
rewrites a design artifact (FR-004). It does **not copy domain #1's shape** — no `Phase`, no `whenPhase`, no
merge fence, no reference to F10 (FR-005). The default posture is **advisory**; only the deterministic
token/contrast/surface rules, the always-blocking `evidenceMeasured`, and the `HumanOnly` `adoptNewPolicy`
are `Blocking` (FR-009). **No rendering vocabulary** appears in the kernel or SPI (FR-011). The adapter
**lifts unchanged** into a coproduct — standalone and lifted `(verdict, provenance)` identical (FR-014, the
F09 guarantee). **Zero new dependency, no rendering library**; dependency direction is adapter → SPI → kernel,
never the reverse and never adapter → adapter (FR-016, SC-008).

**Scale/Scope**: one new project with two public modules — `DesignSystem` (the vocabulary
`DesignArtifactRef`/`DesignSystemFact`/`DesignChange`, `DesignSystem.toRef`/`identify`/`bridge`/the
`surfaceMatches`/`contrastMeets`/… probes/`probes`) and `Catalog` (the fourteen named rules + `evidenceMeasured`,
`catalog`, `tokenSurfaceFence`, `fences`, `adapter`). The concrete `ProjectFact` coproduct that carries both
this adapter and the Spec Kit adapter for the faithful-lift proof lives in the **test project**, not the
shipped library.

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.0.0. Re-checked after Phase 1 design —
still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | The two `contracts/*.fsi` are drafted first; the FSI sketch extends `scripts/prelude.fsx` (quickstart) authoring the design-system adapter, feeding it fixture-drawn `DesignSystemFact`s, evaluating the tiered catalog, and lifting alongside F10; semantic tests against the public surface precede the `.fs` bodies. `tasks.md` will order accordingly. |
| II. Visibility lives in `.fsi` + surface baseline | **PASS** | Two curated `.fsi` are the sole surface; the `.fs` carry no `private`/`internal`/`public` on top-level bindings. A **new** baseline `surface/FS.GG.Governance.Adapters.DesignSystem.surface.txt` plus a DesignSystem-side reflective drift test and a dependency-hygiene test (DesignSystem → BCL/FSharp.Core/Spi/Kernel only, **not F10**) (FR-017). |
| III. Idiomatic simplicity | **PASS** | Plain DUs + records + total folds. The catalog is fourteen `CheckRule.rule`/`asking`/`blocking` constructions; `adapter` is a plain record literal; `evidenceMeasured`/`tokenDrift` reuse the kernel's F05 `Evidence.build`/`effective` and F03 `Check.allOf`/`probe` — no bespoke engine. The `SurfaceObservation` probe key (`probe * subject`) collapses the boolean deterministic observations into one parametric fact case, avoiding case-explosion. No custom operators, no SRTP, no reflection (outside the surface test), no type providers. The generic `'fact`/`'artifact`/`'change` parameters appear only via the F09 `Adapter<…>` the module instantiates. **No `RequireQualifiedAccess` is needed** — unlike F10, no case names collide (a noted *difference*, D4). |
| IV. Elmish/MVU boundary | **N/A — PASS** | A **pure** value/fold layer: no multi-step state, no I/O, no retries, no interaction. No `Model`/`Msg`/`Effect`/interpreter (FR-015). **Sensing** a live design system into `DesignSystemFact`s (reading the token tree, capturing rendered output, computing contrast, hashing artifact content) and **wiring** the adapter into a running loop is the already-shipped F08 effects shell + the F12 CLI, not this feature — exactly as Principle IV exempts "a single rule evaluation, an explanation formatter." |
| V. Test evidence mandatory; prefer real | **PASS** | Tests feed **real** `DesignSystemFact`s drawn from a real fixture token tree through the **built** `FS.GG.Governance.Adapters.DesignSystem` + Spi + Kernel libraries and assert real verdicts, provenance, render, hash, and reads — the deterministic-engine "prefer real evaluation over fixtures" path. The fixture token tree is the domain's real input, not a mock. The faithful-lift proof composes against the **real F10 adapter** (not a synthetic toy) — a stronger composition test than F10's, which used a synthetic second domain. No mocks are needed — the layer is pure. |
| VI. Observability & safe failure | **PASS** | Every function is **total**: a missing component does not compile (the `Adapter` record, F09 FR-014); a probe over an **absent** fixture artifact reports `Unknown` (undecided), never a silent `Met` (edge case "contrast fixture missing"); `evidenceMeasured` distinguishes a malformed graph (`Evidence.build` error → `Unmet` with the `GraphError`) from a real synthetic taint, and a definite failing check (`Unmet`) from an undecided one (`Unknown`, Principle VI). No silent failure: lifting cannot change a verdict (SC-006). |
| Change Classification | **Tier 1** | New project + new public API surface (two modules) + a new dependency *direction* (DesignSystem → Spi) + a new surface baseline; full artifact chain (spec, plan, two `.fsi`, baseline, tests, docs) (FR-017). |
| Engineering Constraints | **PASS** | `net10.0`; new `FS.GG.Governance.Adapters.DesignSystem` library under `src/`; `.fsi` per public module; new surface baseline; **zero new `PackageReference`**, **no rendering library** (ProjectReference to the Spi + `FSharp.Core`/BCL only) (FR-016). Governance-may-inspect / project-must-never-require holds — DesignSystem depends on Spi/Kernel, not the reverse; it does **not** own design-system product identity, controls, themes, or token authoring (it observes a design language, it does not author one). **Domain #2 of M3**; no packing action (the kernel packs at F06; the CLI tool packs at F12). |

**Gate result: PASS — no unjustified violations. Complexity Tracking left empty (the `SurfaceObservation`
parametric fact key, the `Evidence`-based taint reuse, and the `Opaque`-routes-to-agent hatch are ordinary,
constitution-sanctioned F# idioms, not waived complexity).**

Decisions touched by this feature (roadmap §F11): F11 **locks no roadmap decision**; it **realizes** the
design fixed by `docs/governance-design/adapters.md` (the design-system adapter, its `DesignArtifactRef`
vocabulary, the `surfaceMatches`/`contrastMeets` probes, the tiered rule catalog with its blocking/advisory
severities, the `Opaque`-routes-to-agent mechanism, and the fixture-based testing). It consumes **F09**
(`Adapter<'fact,'artifact,'change>`, `Lift`, `Composition` — it is authored against the SPI record and lifts
unchanged into a coproduct), **F07** (`Fence`/`Route`/`RunMode` — `tokenSurfaceFence` is an F07 `Fence`, the
advisory/blocking split is `Route.route … mode`), **F05** (`EvidenceState`/`Evidence.build`/`Evidence.effective`
— `MeasurementState`/`VerdictRestsOn` run unchanged), **F04** (`CheckRule`/`CheckTier`/`Severity`/`Bridge`/
`rule`/`asking`/`blocking`/`toRule`), **F03** (`Check`/`Probe`/`ArtifactRef`/`Outcome` — its probes), and
through them F01–F02 (`Rule`/`FactSet`/`FixedPoint`, `Verdict`). It is composed alongside **F10** (the Spec Kit
adapter — at a root authored by **F12**, never by this feature; this feature's *test* root proves the
composition) and **wired** by **F12** (the CLI's project-level composition root and the sensing of a live
design system). It is run by **F08** (the effects shell, already shipped). It depends on **F09 only** — never
on F10 (the two adapters are independent siblings, FR-005/FR-016). **Out of scope** (deferred): the CLI command
surface, the project-level composition-root *wiring* that assembles F10 and F11, and the **sensing** of a live
design system (F12); the effects edge (F08, shipped); and any change to the kernel or the SPI (this feature
adds adapter code only).

## Project Structure

### Documentation (this feature)

```text
specs/011-adapter-designsystem/
├── plan.md              # This file
├── research.md          # Phase 0 — engineering decisions D1–D9
├── data-model.md        # Phase 1 — the vocabulary, the probes, the catalog, the fence, the laws & invariants
├── quickstart.md        # Phase 1 — FSI sketch (author the adapter, govern fixture facts, route by tier, lift with F10) + validation
├── checklists/          # Pre-existing checklist(s) from /speckit-checklist
├── contracts/
│   ├── DesignSystem.fsi # Phase 1 — the vocabulary + DesignArtifactRef + toRef/identify/bridge/probes (NO whenPhase)
│   └── Catalog.fsi      # Phase 1 — the tiered rule catalog + tokenSurfaceFence + adapter
└── tasks.md             # Phase 2 — created by /speckit-tasks (NOT here)
```

### Source Code (repository root)

```text
src/FS.GG.Governance.Adapters.DesignSystem/                  # NEW project — the pure design-system adapter (FR-016)
├── FS.GG.Governance.Adapters.DesignSystem.fsproj            # ProjectReference → Spi; ZERO PackageReference; no rendering lib; net10.0
├── DesignSystem.fsi                                         # = contracts/DesignSystem.fsi (NEW) — vocabulary + artifact map + probes + wiring
├── DesignSystem.fs                                          # implementation against the stable signature (NEW)
├── Catalog.fsi                                              # = contracts/Catalog.fsi (NEW) — tiered catalog + fence + adapter
└── Catalog.fs                                               # implementation against the stable signature (NEW)

src/FS.GG.Governance.Adapters.Spi/                           # UNCHANGED (F09) — DesignSystem depends on it, never the reverse
src/FS.GG.Governance.Adapters.SpecKit/                       # UNCHANGED (F10) — a SIBLING; DesignSystem never references it
src/FS.GG.Governance.Kernel/                                 # UNCHANGED (F01–F07)

tests/FS.GG.Governance.Adapters.DesignSystem.Tests/          # NEW test project
├── FS.GG.Governance.Adapters.DesignSystem.Tests.fsproj      # Expecto + FsCheck; ProjectReference → DesignSystem (+ SpecKit, for the lift proof only)
├── fixtures/                                                 # NEW: the fixture token tree (JSON/RON — token document, generated surface, interaction-state & page-pattern specs)
├── DesignSystemTests.fs                                     # NEW: five-component/observer-only + no-F10-shape (SC-001); fixture probes (SC-003); render/explain (SC-004)
├── CatalogTests.fs                                          # NEW: tier split + routing (SC-002); evidence/taint via F05 (SC-003 taint); commutative-hash (SC-005)
├── LiftTests.fs                                             # NEW: faithful lift — compose alongside the REAL F10 adapter; standalone == lifted (SC-006); adoption bar (SC-007)
├── SurfaceDriftTests.fs                                     # NEW: DesignSystem surface baseline + deps = BCL/FSharp.Core/Spi/Kernel only, NOT F10 (SC-008)
└── Main.fs                                                  # NEW: Expecto entry point

scripts/prelude.fsx                                          # extend: an F11 sketch (author the adapter, govern fixture facts, route by tier, lift with F10)
surface/FS.GG.Governance.Adapters.DesignSystem.surface.txt   # NEW baseline for the DesignSystem public surface (blessed at impl time)
FS.GG.Governance.sln                                         # add the two new projects
```

**Structure Decision**: a **new project**, `FS.GG.Governance.Adapters.DesignSystem`, not an addition to the
kernel, the SPI, or the Spec Kit adapter — the spec (FR-016) and the roadmap (§3: each concrete adapter
depends on Spi, never the reverse, and never on another adapter) both require it to be **separate from and
dependent on** the SPI. Inside it, two modules in compile order **`DesignSystem` → `Catalog`**: `Catalog`
references `DesignSystem`'s vocabulary/`bridge`/`probes` and the F09 `Adapter<…>`. The concrete `ProjectFact`
coproduct for the faithful-lift proof lives in the **test project**, which is the **only** place F11 references
F10 (the shipped adapter never does — the dependency-hygiene test enforces this). The `surface/`, `scripts/`,
and central build-props scaffolding from F01/F09/F10 is reused; a new per-module baseline is added for
DesignSystem. This feature is **domain #2 of M3**; it carries no packing/milestone action — the kernel already
packs (F06) and the CLI tool packs later (F12).

## Complexity Tracking

> No unjustified Constitution Check violations. The `SurfaceObservation (probe * subject * met)` parametric
> fact (which collapses the boolean deterministic observations into one case), the `Evidence`-based
> synthetic-taint reuse in `evidenceMeasured`, and the `Opaque`-routes-to-agent hatch are ordinary,
> constitution-sanctioned F# idioms; the generic type parameters appear only through the F09 `Adapter<…>` the
> module instantiates. The deliberate **absence** of `RequireQualifiedAccess`, `Phase`, `whenPhase`, a merge
> fence, and a dial (all present in F10) is the feature's thesis, not a simplification debt. No entries
> required.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
