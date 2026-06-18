# Implementation Plan: The Adapter SPI & Composition Root — A Domain Plugs In By Supplying Only Its Own Vocabulary

**Branch**: `009-adapter-spi` | **Date**: 2026-06-18 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/009-adapter-spi/spec.md`

## Summary

Add the **adapter SPI** and the **composition root** (F09) as a **new pure project** —
`FS.GG.Governance.Adapters.Spi` — that depends on the already-shipped kernel (F01–F07) and
nothing else. It defines the thin, total contract a domain implements to be governed (it supplies
**only** its own vocabulary — a closed `'fact` union, an artifact mapping, probes, a rule catalog,
and fences) and the generic machinery that **lifts** each domain's rules into one closed coproduct
and **composes** several adapters at a single reviewable root. It **starts Milestone M3 — the
adoption bar** ("the kernel is a library, not a platform"). It is **pure** — values and total
folds, no state, no I/O, no `Model`/`Msg`/`Effect`, no interpreter — so Constitution **Principle
IV is N/A**, exactly as for F01–F07.

It introduces two modules, in compile order:

1. **`Adapter` — the SPI.** A single total record `Adapter<'fact,'artifact,'change>` bundling the
   **five** domain-supplied components: the closed fact union (the `'fact` type parameter, named by
   its `Identify`), the artifact mapping (`ToRef: 'artifact -> ArtifactRef`), the declared
   `Probes`, the `Rules` catalog (each an F04 `CheckRule<'fact>`), and the `Fences`. It carries the
   F04 `Bridge<'fact>` as **kernel wiring** — not new cross-cutting code, the kernel's own contract
   the domain fills in (two one-liners: how the domain-neutral `RuleOutcome` embeds in `'fact` and
   how an artifact content hash is read from facts). Because it is a **record**, an adapter that
   omits a component **does not compile** — adoption is total, never silently partial (FR-014).
   The module also ships the **lift combinators** (a `Lift` module): `Lift.check` /
   `Lift.checkRule` (one argument — a prism `'big -> 'small option`; **contravariant**, because a
   `Check` only *reads* facts, so `render`/`hash`/`reads` are **invariant** under lifting — the
   cache key does not move), `Lift.rule` (two arguments — prism + injection, **invariant**, for the
   executable kernel `Rule` whose provenance the lift preserves), and `Lift.fence` (one argument —
   contravariant on the change channel). These are the semantics-preserving fact mappings (FR-004).

2. **`Composition` — the composition root machinery.** Generic helpers that assemble independent
   adapters into one catalog the **unchanged** kernel evaluates: `lift` (bridge each adapter's
   catalog into the coproduct via `Lift.checkRule` + contramap its fences), `compose` (concatenate
   the lifted catalogs with the small, named set of cross-domain `Implies` rules authored over the
   coproduct, and union the fences **deduped by name**), and the thin `toRules` reuse that turns the
   composed `CheckRule<'project>` catalog into executable `Rule<'project>` via the **unchanged**
   `CheckRule.toRule`. The concrete `ProjectFact` coproduct, its single-case active patterns, its
   `inject` helpers, and the project `Identify`/`Bridge` are **authored by the consumer at the one
   root** (in the test example adapters here; by F12 for a real project) — F09 ships the *generic*
   machinery, not a fixed coproduct (FR-003/FR-012).

The keystone behaviour is that **adoption is cheap and local, and composition is deterministic and
order-independent.** A domain author hands over the five components and gets inference, arbitration,
evidence, rendering, hashing, explanation, severity, and run-modes **for free** — the adapter
contains **none** of them (FR-002). A rule authored over one domain, once lifted, evaluates to the
**identical** verdict and provenance it produced standalone (FR-004): the lift re-targets the fact
channel only, and because `Check.contramapFacts` keeps each probe's `Name`/`Reads`/`Args` and the
combinator structure, `Check.render` and `Check.hash` are **byte-for-byte invariant** — the F04
agent-review cache key is stable across lifting. Cross-domain coupling is written **once** at the
root as an `Implies` over the coproduct's facts; its **deterministic, order-independent precedence
is not new code** — it is the kernel's own confluent least fixed point (the Datalog guarantee,
F01) plus F07's forbid-trumps-permit `Route` (a blocking result always wins; default
allow-unless-fenced). Removing one adapter is dropping one `Lifted` from the list: the kernel and
every remaining adapter are untouched, and a cross-domain rule naming the gone domain becomes
**inert** (its antecedent probe reads facts that are never present and reports `Unmet` — a definite
"not applicable" — so the `Implies` is vacuously satisfied, never an error) (FR-009).

The feature **depends on the kernel only** (a single `ProjectReference` to
`FS.GG.Governance.Kernel`) and adds **no new `PackageReference`** — BCL + `FSharp.Core` + kernel
(FR-015). It **ships no concrete production adapter** (F10/F11 deliver those); generality is proven
by **two unrelated, neutral example adapters** authored in the test suite and disclosed as
synthetic example domains (Principle V). The public surface is two curated `.fsi` contracts —
[`contracts/Adapter.fsi`](./contracts/Adapter.fsi) and
[`contracts/Composition.fsi`](./contracts/Composition.fsi) — with a **new** surface-area baseline
(`surface/FS.GG.Governance.Adapters.Spi.surface.txt`) and a Spi-side surface-drift /
dependency-hygiene test (FR-016).

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (inherited from `Directory.Build.props`).

**Primary Dependencies**: **None new** (no `PackageReference`). `FS.GG.Governance.Adapters.Spi`
takes a single `ProjectReference` on `FS.GG.Governance.Kernel` and uses only `FSharp.Core` + the
BCL. There is **no I/O, no `System.IO`, no serializer** — the SPI and root are pure values and
folds. Test project only: Expecto + FsCheck, already pinned centrally.

**Storage**: N/A. Pure value/fold layer — no state, no persistence, no cache of its own (the F04
content-hash cache key is reused unchanged; recording/loading is the F08 edge's job, not F09's).

**Testing**: `dotnet test`. Tests exercise the **public** surface through the built library and
`scripts/prelude.fsx` (Principle I). Targeted tests for: the five-part contract being total (a
missing component does not compile — a `[<Literal>]`/comment-documented compile guard) and an
example adapter governing itself using only kernel facilities (SC-001); **faithful lifting** — for
100% of an example adapter's rules, the lifted rule's `(verdict, provenance)` and its
`Check.render`/`Check.hash` are byte-for-byte identical to the standalone original (SC-002);
**lifted `Opaque`/`AgentReviewed`** staying out of `Deterministic` and routing to review (US2-3).
FsCheck **property** tests for order-independence (every permutation of adapter-composition order
and rule order ⇒ identical least fixed point and identical merged `Route`/verdict, SC-003/SC-007)
and for the cross-domain precedence (a blocking result wins regardless of position). The
**removal/boundary** test (compose ≥2, drop one, the kernel + remaining adapter(s) evaluate
unchanged and the cross-domain rule goes inert, SC-004); **two unrelated** example adapters that
compose with zero cross-copying (SC-005); and the new Spi surface-drift + dependency-hygiene
baseline (Spi → BCL/FSharp.Core/Kernel only, FR-016, SC-008).

**Target Platform**: cross-platform .NET library (Linux dev host).

**Project Type**: **library** — a new pure SPI/composition library `FS.GG.Governance.Adapters.Spi`
(+ its test project), separate from and depending on the pure kernel (FR-015).

**Performance Goals**: correctness, determinism, and faithful lifting — not throughput. Every
function is a **total** value/fold; the lift is O(facts) per rule application and adds no rounds to
the kernel's fixed point. No measured hot path.

**Constraints**: the SPI and root are **pure & total** — no I/O, no state, no `Model`/`Msg`/
`Effect`, no interpreter (FR-013, Principle IV N/A). The lift is **semantics-preserving** — a
lifted rule's verdict and provenance equal the standalone original's, and `Check.render`/`hash` are
invariant (FR-004, SC-002). Composition adds **no new evaluation or precedence logic**: the
composed catalog runs through the **unchanged** `CheckRule.toRule` + `FixedPoint.evaluate`, and
cross-domain precedence is the kernel's confluent LFP + F07 `Route` (FR-005/FR-007, SC-006). The
merged verdict and the least fixed point are **order-independent** (FR-008, SC-003). Adapters are
**independent** — each references only the SPI and the kernel, never another adapter (FR-006). The
composed fence set is the **deduped-by-name union** (FR-011). **Zero new dependency**; the kernel
does **not** reference the Spi (dependency direction: adapters → kernel) (FR-015, SC-008).

**Scale/Scope**: one new project with two public modules — `Adapter` (the `Adapter<'fact,'artifact,
'change>` SPI record + the `Lift` combinators `check`/`checkRule`/`rule`/`fence` + the thin
standalone reuse `Adapter.toRules`) and `Composition` (the value types `Lifted<'project,'change>`
and `Composed<'project,'change>`; the folds `lift`/`compose`/`toRules`). The concrete coproduct and
two example adapters live in the **test project**, not the shipped library.

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.0.0. Re-checked after Phase 1
design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | The two `contracts/*.fsi` are drafted first; the FSI sketch extends `scripts/prelude.fsx` (quickstart) authoring a toy adapter, lifting it, and composing two; semantic tests against the public surface precede the `.fs` bodies. `tasks.md` will order accordingly. |
| II. Visibility lives in `.fsi` + surface baseline | **PASS** | Two curated `.fsi` are the sole surface; the `.fs` carry no `private`/`internal`/`public` on top-level bindings. A **new** baseline `surface/FS.GG.Governance.Adapters.Spi.surface.txt` plus a Spi-side reflective drift test and a dependency-hygiene test (Spi → BCL/FSharp.Core/Kernel only) (FR-016). |
| III. Idiomatic simplicity | **PASS** | Plain records/DUs + total folds. `Lift.check`/`checkRule`/`fence` are one-argument contravariant maps; `Lift.rule` is a two-argument map preserving provenance; `compose` is `List.collect` + a dedup-by-name `List.fold`. No custom operators, no SRTP, no reflection (outside the surface test), no type providers. The **single-case active patterns** the root uses to lift (`(\|Design\|_\|)`) are explicitly permitted by Principle III ("active patterns beyond single-case"); these are single-case, so no justification is owed. The generic `'fact`/`'artifact`/`'change`/`'project` parameters are the kernel's house style (`Check<'fact>`, `Fence<'change>`, `EvidenceGraph<'id>`). |
| IV. Elmish/MVU boundary | **N/A — PASS** | A **pure** value/fold layer: no multi-step state, no I/O, no retries, no interaction. No `Model`/`Msg`/`Effect`/interpreter (FR-013). Wiring a *composed* catalog into a running loop is the already-shipped F08 effects shell + the F12 CLI, not this feature — exactly as Principle IV exempts "a single rule evaluation, an explanation formatter." |
| V. Test evidence mandatory; prefer real | **PASS** | Tests feed **real** facts and rules through the **built** library and assert derived facts, provenance, render, hash, and routes — the deterministic-engine "prefer real evaluation over fixtures" path. The two example adapters are **synthetic example domains** (illustrative, not real adopters): each is disclosed at its definition, carries the `Synthetic` token in the test names that assert via it, and is listed in the PR description (Principle V). No mocks are needed — the layer is pure. |
| VI. Observability & safe failure | **PASS** | Every function is **total**: an absent component does not compile (FR-014, a typed boundary error, not a silent partial adoption); a cross-domain rule whose domain is absent goes **inert** (its antecedent reports `Unmet`), never throws (FR-009). The `Outcome.Unmet` "definite not-applicable" vs `Unknown` "undecided" distinction (F03) is what keeps an absent-domain antecedent inert rather than blocking — malformed/absent input is distinguishable from a real failing check (Principle VI). No silent failure: lifting cannot change a verdict (SC-002). |
| Change Classification | **Tier 1** | New project + new public API surface (two modules) + a new dependency *direction* (Spi → Kernel) + a new surface baseline; full artifact chain (spec, plan, two `.fsi`, baseline, tests, docs) (FR-016). |
| Engineering Constraints | **PASS** | `net10.0`; new `FS.GG.Governance.Adapters.Spi` library under `src/`; `.fsi` per public module; new surface baseline; **zero new `PackageReference`** (ProjectReference to the kernel + `FSharp.Core`/BCL only) (FR-015). No rendering/domain vocabulary — the SPI is generic over `'fact`/`'artifact`/`'change`; the example adapters are neutral toy domains, not rendering. Governance-may-inspect / project-must-never-require holds — Spi depends on Kernel, not the reverse. **Starts M3**; no packing action (the kernel packs at F06; the CLI tool packs at F12). |

**Gate result: PASS — no unjustified violations. Complexity Tracking left empty (the generic type
parameters, the single-case active patterns the root uses, and the contravariant lift combinators
are ordinary, constitution-sanctioned F# idioms, not waived complexity).**

Decisions touched by this feature (roadmap §F09): F09 **locks no roadmap decision** (#1 was F04,
#2/#3 F08, #4 F07); it **realizes the closed-coproduct trade** stated in
`docs/governance-design/theory-and-composition.md` (Data Types à la Carte, kept *closed* — a
single reviewable root; adding an interpreter stays trivial, adding a domain is one central root
edit; open third-party plug-in extensibility is **out of scope**). It consumes **F04** (`CheckRule
<'fact>`: `Check` + `CheckTier` + `Severity` + `Bridge`, `toRule`, `RuleOutcome`/`NeedsReview`/
`RecordedReview`, `cacheKey`), **F05** (the evidence/taint model carried unchanged through the
coproduct), **F03** (`Check`/`Probe`/`ArtifactRef`/`Outcome` — the lift maps a probe's `Eval`
channel), **F07** (`Route`/`route`/`Fence` — the cross-domain precedence reused), and through them
F01–F02 (`Rule`/`FactSet`/`FixedPoint`, `Verdict`). It is consumed by **F10** (the Spec Kit
adapter), **F11** (the design-system adapter), and **F12** (the CLI wires a *composed* catalog and
authors the real project root). It is reused alongside **F08** (the effects shell runs a composed
catalog). **Out of scope** (deferred): the concrete adapters (F10/F11), the CLI command surface and
real project-root wiring (F12), open third-party plug-in extensibility, and the future *rule-set
analysis* interpreter (the SMT-style "can this ever pass / is this rule shadowed" fold the
inspectable algebra reserves a slot for).

## Project Structure

### Documentation (this feature)

```text
specs/009-adapter-spi/
├── plan.md              # This file
├── research.md          # Phase 0 — engineering decisions D1–D9
├── data-model.md        # Phase 1 — the SPI record, lift combinators, compose, lifting & composition laws, invariants
├── quickstart.md        # Phase 1 — FSI sketch (author a toy adapter, lift it, compose two) + validation V61–V72
├── contracts/
│   ├── Adapter.fsi      # Phase 1 — the SPI: Adapter<'fact,'artifact,'change> + the Lift combinators + standalone reuse
│   └── Composition.fsi  # Phase 1 — the root: Lifted/Composed + lift/compose/toRules (generic over 'project)
└── tasks.md             # Phase 2 — created by /speckit-tasks (NOT here)
```

### Source Code (repository root)

```text
src/FS.GG.Governance.Adapters.Spi/                  # NEW project — the pure SPI + composition root (FR-015)
├── FS.GG.Governance.Adapters.Spi.fsproj            # ProjectReference → Kernel; ZERO PackageReference; net10.0
├── Adapter.fsi                                      # = contracts/Adapter.fsi (NEW) — the SPI + Lift combinators
├── Adapter.fs                                        # implementation against the stable signature (NEW)
├── Composition.fsi                                   # = contracts/Composition.fsi (NEW) — the root machinery
└── Composition.fs                                    # implementation against the stable signature (NEW)

src/FS.GG.Governance.Kernel/                         # UNCHANGED (F01–F07) — Spi depends on it, never the reverse

tests/FS.GG.Governance.Adapters.Spi.Tests/          # NEW test project
├── FS.GG.Governance.Adapters.Spi.Tests.fsproj      # Expecto + FsCheck; ProjectReference → Spi
├── ExampleAdapters.fs                               # NEW: two UNRELATED neutral toy adapters + the ProjectFact coproduct (SYNTHETIC, disclosed)
├── AdapterTests.fs                                  # NEW: five-part contract; standalone governs-itself; faithful lift (verdict+provenance+render+hash) (V61–V65)
├── CompositionTests.fs                              # NEW: lift+compose; cross-domain Implies; order-independence; removal/boundary; two-unrelated (V66–V71)
├── SurfaceDriftTests.fs                             # NEW: Spi surface baseline + Spi deps = BCL/FSharp.Core/Kernel only (V72)
└── Main.fs                                          # NEW: Expecto entry point

scripts/prelude.fsx                                  # extend: an F09 sketch (author a toy adapter, lift, compose two)
surface/FS.GG.Governance.Adapters.Spi.surface.txt   # NEW baseline for the Spi public surface (blessed at impl time)
FS.GG.Governance.sln                                 # add the two new projects
```

**Structure Decision**: a **new project**, `FS.GG.Governance.Adapters.Spi`, not an addition to the
kernel — the spec (FR-015) and the roadmap (§3: `FS.GG.Governance.Adapters.Spi  pure; depends on
Kernel`) both require the SPI/root to be **separate from and dependent on** the pure kernel, never
the reverse, so each concrete adapter (F10/F11) and the CLI (F12) depend on the **Spi**, not on the
kernel directly for composition. Inside it, two modules in compile order **`Adapter` →
`Composition`**: `Composition` references `Adapter`'s `Adapter<…>`/`Lift` and the kernel. The
concrete `ProjectFact` coproduct lives in the **test project** (and in F12 for real), because the
root is per-project and authored by the consumer (FR-003/FR-012) — F09 ships the generic machinery.
The kernel's BCL-only hygiene test is unaffected (the kernel gains no reference); a **new** Spi
hygiene test asserts Spi references only BCL/FSharp.Core/Kernel. The `surface/`, `scripts/`, and
central build-props scaffolding from F01 is reused; a new per-module baseline is added for Spi. This
feature **starts M3**; it carries no packing/milestone action — the kernel already packs (F06) and
the CLI tool packs later (F12).

## Complexity Tracking

> No unjustified Constitution Check violations. The generic `'fact`/`'artifact`/`'change`/`'project`
> parameters and the injected-port-free pure folds are the kernel's established domain-neutrality
> idiom; the single-case active patterns the root uses are explicitly permitted by Principle III.
> No entries required.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
