# Implementation Plan: The Spec Kit Adapter — Governance Dogfoods This Repo's Own Workflow As Data

**Branch**: `010-adapter-speckit` | **Date**: 2026-06-18 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/010-adapter-speckit/spec.md`

## Summary

Add the **Spec Kit adapter** (F10) as a **new pure project** — `FS.GG.Governance.Adapters.SpecKit` —
that depends on the already-shipped F09 SPI (and through it the kernel, F01–F07) and **nothing else**.
It is the **first concrete production adapter** and **domain #1 of Milestone M3 — the adoption bar**:
it governs **this repository's own** Spec Kit workflow
(`constitution → specify → clarify → plan → tasks → analyze → implement → merge`) by supplying — through
the F09 `Adapter<'fact,'artifact,'change>` SPI — **only** its own five components and getting inference,
three-valued verdicts, the reified `Check` algebra and its interpreters, `CheckTier` arbitration, the
evidence/taint DAG, JSON explanation and contract, severity, and routing/run-modes **for free**. It is
**pure** — values and total folds, no state, no I/O, no `Model`/`Msg`/`Effect`, no interpreter — so
Constitution **Principle IV is N/A**, exactly as for F01–F07 and F09.

It introduces two modules, in compile order:

1. **`SpecKit` — the domain vocabulary, the phase guard, the artifact map, the kernel wiring.** The
   closed `SpecKitFact` union (`PhaseReached`, `ArtifactPresent`, `TaskState` carrying an authored
   `EvidenceState`, `TaskDependsOn`, `SkillBound`, `ConstitutionArea`, plus the `SpecKitGov of
   RuleOutcome` embed case the F04 `Bridge` uses), the ordered `Phase` and the `SpecKitArtifact`
   enumerations, the `SpecKitChange` shape, the `SpecKit.toRef` artifact mapping, the `SpecKit.identify`
   fact identity, the `SpecKit.bridge` kernel wiring, the declared `SpecKit.probes`, and the keystone
   **`whenPhase`** phase guard. `whenPhase required check` is `Implies (phaseAtLeast required, check)`
   over an atomic "phase ≥ required" probe — **reusing the kernel's `Implies`, not new logic**: before
   the phase the antecedent reports `Unmet`, so the implication is **vacuously satisfied** (a definite
   `Pass` — the F09 inertness mechanism, never `Fail`/`Uncertain`); at or after, the antecedent is `Met`
   and the implication reduces to the check's own verdict. It is **reified-ness preserving**, so a
   guarded reified check stays `Deterministic`-eligible and a guarded `Opaque` stays `AgentReviewed`.

2. **`Catalog` — the reified rule catalog, the constitution dial, the merge fence, the adapter.** The
   monolithic `analyze` pass becomes a **catalog of reified `CheckRule`s**, each carrying a `CheckTier`
   and a `Severity` and rendering to a sentence: the **deterministic** checks (`tasksGraphWellFormed`,
   `constitutionComplete`, `contractsCurrent`, `evidenceNotSynthetic`, `fencedSurfacesVerified`), the
   **`AgentReviewed`** judgement checks (`planSatisfiesSpec`, `tasksCompleteOrdered`, each over an
   `Opaque` check carrying a `Question`), and the **`HumanOnly`** `featureInScope`. The
   **`ConstitutionDial`** is the constitution-authored configuration of which rules block at merge
   (`BlockingAtMerge`) and which earlier phases opt into a hard-stop (`EarlyFences`) — the
   "enforcement ↔ light" dial as **data**, so the blocking set is what the constitution authored, not a
   fixed list. **`mergeFence`** is the single F07 `Fence` tripping at `Phase.Merge`. **`adapter judge
   dial`** assembles the one `Adapter` value: the five SPI components (the dial-promoted catalog, the
   dial's fences) plus the `bridge`.

The keystone behaviours, each a direct translation of `docs/governance-design/speckit-in-the-system.md`:
**(1)** the stateless kernel governs the stateful lifecycle by treating the current phase as a
*supplied* `PhaseReached` fact and guarding rules with `whenPhase`; **(2)** the always-blocking `analyze`
gates become a catalog of reified rules that each declare their tier/severity and explain themselves;
**(3)** **nothing blocks before merge** (the inner loop runs `Inner`/`Sandbox` — advisory across all
tiers), and **merge is the single fence** that flips to `Gate`, recomputes from base, and lets the
`Blocking` rules bite; **(4)** the **constitution is the dial** — `ConstitutionDial` authors the fences
and severities, and the Constitution Check just verifies the dial was filled in honestly. The
`tasks.deps.yml` topology becomes `TaskDependsOn` facts and the synthetic-taint model runs the
**kernel's F05** `Evidence.build`/`Evidence.effective` over them — `EvidenceGraph` is a **derivation**,
not a bespoke engine (US5).

The feature **depends on the SPI only** (a single `ProjectReference` to `FS.GG.Governance.Adapters.Spi`,
and through it the kernel) and adds **no new `PackageReference`** — BCL + `FSharp.Core` + SPI + kernel
(FR-016). It is exercised through its built public surface — two curated `.fsi` contracts,
[`contracts/SpecKit.fsi`](./contracts/SpecKit.fsi) and [`contracts/Catalog.fsi`](./contracts/Catalog.fsi)
— with a **new** surface-area baseline (`surface/FS.GG.Governance.Adapters.SpecKit.surface.txt`) and a
SpecKit-side surface-drift / dependency-hygiene test (FR-017). The standalone-vs-lifted faithful-lift
guarantee (FR-014) is proven by composing the adapter with a second neutral toy domain at a test root.

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (inherited from `Directory.Build.props`).

**Primary Dependencies**: **None new** (no `PackageReference`). `FS.GG.Governance.Adapters.SpecKit` takes
a single `ProjectReference` on `FS.GG.Governance.Adapters.Spi` (which references the kernel) and uses only
`FSharp.Core` + the BCL. There is **no I/O, no `System.IO`, no serializer** — the adapter is pure values
and folds. Test project only: Expecto + FsCheck, already pinned centrally.

**Storage**: N/A. Pure value/fold layer — no state, no persistence. Sensing the live repository (reading
`.specify/feature.json`, parsing `tasks.md`/`tasks.deps.yml`, hashing artifact content) into `SpecKitFact`s
is the F08 effects shell / F12 CLI's job, not this feature (FR-015); tests feed facts directly.

**Testing**: `dotnet test`. Tests exercise the **public** surface through the built library and
`scripts/prelude.fsx` (Principle I). Targeted tests for: the adapter supplying exactly the five
components and reusing 100% of kernel facilities — no inference/arbitration/evidence/render/hash/explain/
severity/routing code, and no artifact-authoring operation, in the adapter module (SC-001); the
**phase guard** — a `whenPhase P` rule is a definite not-applicable (vacuous `Pass`) for a supplied phase
before `P` and contributes its check at/after `P` (SC-002); the **inner-loop-vs-merge** distinction —
every catalog rule is advisory in `Inner`/`Sandbox` (a failing deterministic check reports, the route
never blocks) and the `Blocking` rules flip the route to a blocking gate at `Phase.Merge` in `Gate`
(SC-003); the **evidence/taint** — `AutoSynthetic` propagates down a `TaskDependsOn` chain via the
kernel's `Evidence.effective` and `evidenceNotSynthetic` is a blocking failure at merge that no flag
flips (SC-004); the **constitution dial** — the Constitution Check is advisory inner / blocking at merge,
and the blocking set varies with the dial (SC-005); each catalog rule **renders and explains** through
`Check.render`/`Check.explain` (SC-006); the **faithful lift** — for 100% of the catalog the lifted
rule's `(verdict, provenance)` equals the standalone original when composed at a root (SC-007, the F09
guarantee); and the new SpecKit surface-drift + dependency-hygiene baseline (SpecKit → BCL/FSharp.Core/
Spi/Kernel only, FR-016/FR-017, SC-008).

**Target Platform**: cross-platform .NET library (Linux dev host).

**Project Type**: **library** — a new pure adapter library `FS.GG.Governance.Adapters.SpecKit` (+ its
test project), separate from and depending on the pure SPI (FR-016).

**Performance Goals**: correctness, determinism, and faithful lifting — not throughput. Every function is
a **total** value/fold; no measured hot path. The evidence taint closure is the kernel's existing
least-fixed-point (F05), unchanged.

**Constraints**: the adapter is **pure & total** — no I/O, no state, no `Model`/`Msg`/`Effect`, no
interpreter (FR-015, Principle IV N/A). It supplies **exactly the five SPI components** + the F04 `Bridge`
wiring and **nothing more** (FR-003); inference, arbitration, evidence, render/hash/explain, severity, and
routing/run-modes are all reused (SC-001). It is an **observer, not an author** — no operation generates or
mutates a Spec Kit artifact (FR-004). The default posture is **advisory in the inner loop**; **merge is the
single fence** (FR-008/FR-009). The `evidenceNotSynthetic` verdict is non-negotiable — no flag flips it
(FR-013). The adapter **lifts unchanged** into a coproduct — standalone and lifted `(verdict, provenance)`
identical (FR-014, the F09 guarantee). **Zero new dependency**; dependency direction is adapter → SPI →
kernel, never the reverse (FR-016, SC-008).

**Scale/Scope**: one new project with two public modules — `SpecKit` (the vocabulary `Phase`/
`SpecKitArtifact`/`SpecKitFact`/`SpecKitChange`, `Phase.rank`/`reached`, `SpecKit.toRef`/`identify`/
`bridge`/`whenPhase`/`probes`) and `Catalog` (the eight named rules + `catalog`, `ConstitutionDial`,
`mergeFence`, `defaultDial`, `fences`, `adapter`). The concrete `ProjectFact` coproduct and a second
unrelated example adapter for the faithful-lift proof live in the **test project**, not the shipped library.

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.0.0. Re-checked after Phase 1 design —
still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | The two `contracts/*.fsi` are drafted first; the FSI sketch extends `scripts/prelude.fsx` (quickstart) authoring the Spec Kit adapter, feeding it synthetic `SpecKitFact`s, and routing inner-loop vs merge; semantic tests against the public surface precede the `.fs` bodies. `tasks.md` will order accordingly. |
| II. Visibility lives in `.fsi` + surface baseline | **PASS** | Two curated `.fsi` are the sole surface; the `.fs` carry no `private`/`internal`/`public` on top-level bindings. A **new** baseline `surface/FS.GG.Governance.Adapters.SpecKit.surface.txt` plus a SpecKit-side reflective drift test and a dependency-hygiene test (SpecKit → BCL/FSharp.Core/Spi/Kernel only) (FR-017). |
| III. Idiomatic simplicity | **PASS** | Plain DUs + records + total folds. `whenPhase` is one `Implies` over an atomic probe; the catalog is eight `CheckRule.rule`/`asking`/`blocking` constructions; `adapter` is a `List.map` promotion + a fence list. `RequireQualifiedAccess` on `Phase`/`SpecKitArtifact` resolves the three colliding case names (`Constitution`/`Tasks`) — an ordinary idiom, not waived complexity. No custom operators, no SRTP, no reflection (outside the surface test), no type providers. The generic `'fact`/`'artifact`/`'change` parameters appear only via the F09 `Adapter<…>` the module instantiates. |
| IV. Elmish/MVU boundary | **N/A — PASS** | A **pure** value/fold layer: no multi-step state, no I/O, no retries, no interaction. No `Model`/`Msg`/`Effect`/interpreter (FR-015). **Sensing** the live repository into `SpecKitFact`s and **wiring** the adapter into a running loop is the already-shipped F08 effects shell + the F12 CLI, not this feature — exactly as Principle IV exempts "a single rule evaluation, an explanation formatter." |
| V. Test evidence mandatory; prefer real | **PASS** | Tests feed **real** `SpecKitFact`s through the **built** `FS.GG.Governance.Adapters.SpecKit` + Spi + Kernel libraries and assert real verdicts, provenance, render, hash, and routes — the deterministic-engine "prefer real evaluation over fixtures" path. The Spec Kit domain is the **real adopter** under test (not synthetic); the **second** example domain used only for the faithful-lift composition proof is a synthetic example domain — disclosed at its definition, `Synthetic` token in the test names that assert via it, listed in the PR description (Principle V). No mocks are needed — the layer is pure. |
| VI. Observability & safe failure | **PASS** | Every function is **total**: a missing component does not compile (the `Adapter` record, F09 FR-014); a phase-guarded rule before its phase is a definite not-applicable (vacuous `Pass`), never a throw; `evidenceNotSynthetic` distinguishes a malformed graph (`Evidence.build` error → `Unmet` with the `GraphError`) from a real synthetic taint, and a definite failing check from an undecided one (`Unmet` vs `Unknown`, Principle VI). No silent failure: lifting cannot change a verdict (SC-007). |
| Change Classification | **Tier 1** | New project + new public API surface (two modules) + a new dependency *direction* (SpecKit → Spi) + a new surface baseline; full artifact chain (spec, plan, two `.fsi`, baseline, tests, docs) (FR-017). |
| Engineering Constraints | **PASS** | `net10.0`; new `FS.GG.Governance.Adapters.SpecKit` library under `src/`; `.fsi` per public module; new surface baseline; **zero new `PackageReference`** (ProjectReference to the Spi + `FSharp.Core`/BCL only) (FR-016). Governance-may-inspect / project-must-never-require holds — SpecKit depends on Spi/Kernel, not the reverse, and this repo governs itself with the same standard Spec Kit it offers others. **Domain #1 of M3**; no packing action (the kernel packs at F06; the CLI tool packs at F12). |

**Gate result: PASS — no unjustified violations. Complexity Tracking left empty (the `RequireQualifiedAccess`
attributes, the `Implies`-based phase guard, and the dial-as-data are ordinary, constitution-sanctioned F#
idioms, not waived complexity).**

Decisions touched by this feature (roadmap §F10): F10 **locks no roadmap decision**; it **realizes** the
design fixed by `docs/governance-design/speckit-in-the-system.md` (the spec-kit adapter, phase checks as
reified rules, run-modes mapped to phases, the constitution as the dial, the evidence model). It consumes
**F09** (`Adapter<'fact,'artifact,'change>`, `Lift`, `Composition` — it is authored against the SPI record
and lifts unchanged into a coproduct), **F07** (`Fence`/`Route`/`RunMode` — `mergeFence` is an F07 `Fence`,
the inner-loop-vs-merge mapping is `Route.route … mode`), **F05** (`EvidenceState`/`Evidence.build`/
`Evidence.effective` — `TaskState`/`TaskDependsOn` run unchanged), **F04** (`CheckRule`/`CheckTier`/
`Severity`/`Bridge`/`rule`/`asking`/`blocking`/`toRule`), **F03** (`Check`/`Probe`/`ArtifactRef`/`Outcome`/
`Implies` — its probes and the phase guard), and through them F01–F02 (`Rule`/`FactSet`/`FixedPoint`,
`Verdict`). It is composed alongside **F11** (the design-system adapter) and **wired** by **F12** (the CLI's
project-level composition root and the sensing of the live repository). It is run by **F08** (the effects
shell, already shipped). **Out of scope** (deferred): the concrete design-system adapter (F11); the CLI
command surface, the project-level composition-root *wiring*, and the **sensing** of the live repository
(F12); the effects edge (F08, shipped); and any change to the kernel or the SPI (this feature adds adapter
code only).

## Project Structure

### Documentation (this feature)

```text
specs/010-adapter-speckit/
├── plan.md              # This file
├── research.md          # Phase 0 — engineering decisions D1–D9
├── data-model.md        # Phase 1 — the vocabulary, the phase guard, the catalog, the dial, the laws & invariants
├── quickstart.md        # Phase 1 — FSI sketch (author the adapter, govern synthetic facts, route inner vs merge) + validation
├── contracts/
│   ├── SpecKit.fsi      # Phase 1 — the vocabulary + Phase + toRef/identify/bridge/whenPhase/probes
│   └── Catalog.fsi      # Phase 1 — the rule catalog + ConstitutionDial + mergeFence + adapter
└── tasks.md             # Phase 2 — created by /speckit-tasks (NOT here)
```

### Source Code (repository root)

```text
src/FS.GG.Governance.Adapters.SpecKit/                  # NEW project — the pure Spec Kit adapter (FR-016)
├── FS.GG.Governance.Adapters.SpecKit.fsproj            # ProjectReference → Spi; ZERO PackageReference; net10.0
├── SpecKit.fsi                                          # = contracts/SpecKit.fsi (NEW) — vocabulary + phase guard + wiring
├── SpecKit.fs                                           # implementation against the stable signature (NEW)
├── Catalog.fsi                                          # = contracts/Catalog.fsi (NEW) — catalog + dial + fence + adapter
└── Catalog.fs                                           # implementation against the stable signature (NEW)

src/FS.GG.Governance.Adapters.Spi/                       # UNCHANGED (F09) — SpecKit depends on it, never the reverse
src/FS.GG.Governance.Kernel/                             # UNCHANGED (F01–F07)

tests/FS.GG.Governance.Adapters.SpecKit.Tests/          # NEW test project
├── FS.GG.Governance.Adapters.SpecKit.Tests.fsproj      # Expecto + FsCheck; ProjectReference → SpecKit
├── SpecKitTests.fs                                      # NEW: five-component/observer-only (SC-001); phase guard (SC-002); render/explain (SC-006)
├── CatalogTests.fs                                      # NEW: inner-loop-vs-merge routing (SC-003); evidence/taint + evidenceNotSynthetic (SC-004); constitution dial (SC-005)
├── LiftTests.fs                                         # NEW: faithful lift — compose with a second SYNTHETIC toy domain; standalone == lifted (SC-007)
├── SurfaceDriftTests.fs                                 # NEW: SpecKit surface baseline + deps = BCL/FSharp.Core/Spi/Kernel only (SC-008)
└── Main.fs                                              # NEW: Expecto entry point

scripts/prelude.fsx                                      # extend: an F10 sketch (author the adapter, govern synthetic facts, route inner vs merge)
surface/FS.GG.Governance.Adapters.SpecKit.surface.txt    # NEW baseline for the SpecKit public surface (blessed at impl time)
FS.GG.Governance.sln                                     # add the two new projects
```

**Structure Decision**: a **new project**, `FS.GG.Governance.Adapters.SpecKit`, not an addition to the
kernel or the SPI — the spec (FR-016) and the roadmap (§3: `FS.GG.Governance.Adapters.SpecKit … depends on
Spi (F10)`) both require each concrete adapter to be **separate from and dependent on** the SPI, never the
reverse. Inside it, two modules in compile order **`SpecKit` → `Catalog`**: `Catalog` references `SpecKit`'s
vocabulary/`whenPhase`/`bridge` and the F09 `Adapter<…>`. The concrete `ProjectFact` coproduct and the
second unrelated example adapter for the faithful-lift proof live in the **test project** (the standalone
adopter under test is the *real* Spec Kit adapter; only the second composition partner is synthetic,
disclosed per Principle V). The `surface/`, `scripts/`, and central build-props scaffolding from F01/F09 is
reused; a new per-module baseline is added for SpecKit. This feature is **domain #1 of M3**; it carries no
packing/milestone action — the kernel already packs (F06) and the CLI tool packs later (F12).

## Complexity Tracking

> No unjustified Constitution Check violations. The `RequireQualifiedAccess` attributes (resolving the
> `Constitution`/`Tasks` case-name collisions between `Phase` and `SpecKitArtifact`), the `Implies`-based
> `whenPhase` guard, and the constitution-dial-as-data are ordinary, constitution-sanctioned F# idioms;
> the generic type parameters appear only through the F09 `Adapter<…>` the module instantiates. No entries
> required.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
