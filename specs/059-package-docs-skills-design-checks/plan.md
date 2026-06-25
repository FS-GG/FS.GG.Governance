# Implementation Plan: Package / Docs / Skills / Design Deterministic Checks (F24)

**Branch**: `059-package-docs-skills-design-checks` | **Date**: 2026-06-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/059-package-docs-skills-design-checks/spec.md`

## Summary

F23 made each product surface *declarable, routable, classifiable, and cost-tiered* and produced, as a known
non-error state, a **declared evidence tag with no check behind it** (F23 FR-016, surfaced as
`ProductClassification.TierIsDeclared = false`). This row supplies the missing checks: four **independent,
composable adapter rule packs** — package/API, docs/examples, skills, design/rendering — that consume the
F23 `ProductSurfaceReport` plus the surface's declared `EvidenceTag` and emit deterministic findings tied to
that tag, so a declared evidence tag is no longer an empty promise but an executable check that produces real
evidence.

The work **composes the established leaf precedent** rather than inventing a parallel engine. Each domain
mirrors the F046/F053/F054 split exactly (`FreshnessSensing` / `ReleaseRules` / `ReleaseFactsSensing`):

1. A **pure rule pack** — `Model` (the domain's closed fact + finding vocabulary) + a pure, total
   `evaluate : SurfaceCheckRequest -> <Domain>Facts -> SurfaceFinding list`. Facts are *supplied*, never
   sensed here; identical input yields byte-identical findings (FR-010, SC-005). This is the
   F014/F015/F017/F031 leaf precedent named in the spec.
2. A **host sensor** — an `Interpreter` edge with an injected port that reads the real source (the `.fsi`
   file, the FSI transcript, the docs link target, the skill manifest, the design catalog) via `System.IO`
   only, catches every exception, and produces the plain `<Domain>Facts`. The only place a
   filesystem/registry/rendering dependency lives (FR-007, SC-004). This is the "Host sensor" half of the
   roadmap's "pure adapters plus Host sensors."

A small shared core, **`FS.GG.Governance.SurfaceChecks`**, owns the cross-domain vocabulary used by all four
packs — the `SurfaceFinding` shape (with its `BaseSeverity`, advisory flag, bound `EvidenceTag`, and exact
location) and the `SurfaceCheckRequest` derived from one F23 `ProductClassification`. The pure `Composition`
dispatcher — which, given a `ProductSurfaceReport` and the per-domain sensed facts, runs **every applicable**
pack and aggregates findings deterministically and order-independently (FR-008, SC-008) — lives in a separate
tiny project **`FS.GG.Governance.SurfaceChecks.Dispatch`**, because its `DomainFactBundle` references the four
domain `Model` types while the four domain libraries reference `SurfaceChecks` for the `SurfaceFinding` shape:
keeping both in one project would be a project-level reference cycle (the data-model's recorded micro-decision,
resolved to the split). The shared core carries `Model` only; `Dispatch` references the core + the four domain
libraries. No pack depends on another; the dispatcher depends on all of them.

**Advisory is free, not new machinery.** A judgement-heavy check sets `BaseSeverity = Advisory`;
`FS.GG.Governance.Enforcement.deriveEffectiveSeverity` already guarantees a base-Advisory finding never
escalates to Blocking under any mode or profile (FR-011, SC-006). Promotion is the existing
`FS.GG.Governance.AdvisoryPromotion` and is **not** invoked in this row. Evidence is produced and bound to
the surface's `EvidenceTag` through the existing `EvidenceCapture` / `EvidenceReuse` machinery (FR-009,
SC-007). The F018/F023 enforcement truth table is **reused verbatim** — this row supplies new findings and
evidence and nothing else (FR-014).

**Confirmed planning decisions** (full rationale in [research.md](./research.md)):

1. **Leaf sensing pattern, not the Kernel Adapter SPI (D1).** The checks are leaf rule packs + host sensors
   in the F046/F053/F054 lineage, producing `SurfaceFinding`s consumed by F023 enforcement — *not* kernel
   `Adapter<'fact,'artifact,'change>` values feeding the F04 `Kernel`. The existing kernel-side
   `Adapters.DesignSystem` (F11) stays as-is and is **not** extended here; F24's `DesignChecks` is the
   host-side deterministic check, the same way `ReleaseRules` is leaf-side. This keeps the kernel out of the
   route/verify host and honors "pure adapters plus Host sensors."
2. **One library per domain, bundling Model + pure pack + Interpreter sensor (D2).** Mirrors
   `ReleaseFactsSensing` (Model.fsi / pure `Sensing` / impure `Interpreter` in one project). Four domain
   libraries (`PackageChecks`, `DocsChecks`, `SkillChecks`, `DesignChecks`) + the shared `SurfaceChecks`
   core (`Model`) + the `SurfaceChecks.Dispatch` composition project — six `src` libraries (the core and the
   dispatcher are split to break the `core ↔ domains` reference cycle, see Summary). Each domain library
   carries no cross-domain reference (FR-008); each `Interpreter` is the sole I/O seam (FR-007).
3. **Reuse F23/F017/F023 unchanged (D3).** No new `SurfaceClass`, no new `capabilities.yml` schema version,
   no new `capabilities.yml` field, no new `DiagnosticId`, no change to the enforcement truth table. The
   packs read the *already-classified* `ProductSurfaceReport` and the *already-declared* `EvidenceTag`
   (FR-013, FR-014).
4. **Surfaced additively through `fsgg verify`, byte-identical when empty (D8).** The pre-PR host
   (`VerifyCommand`) gains an edge step that, after classification, runs the applicable sensors+packs and
   folds the `SurfaceFinding`s into its result and into `verify.json` **only when non-empty** — the same
   additive discipline F23/F052 used for `productSurfaces` (empty ⇒ byte-identical output, every existing
   golden untouched). Enforcement, exit codes, and the truth table are unchanged. A domain whose surface is
   not declared/routed runs no sensor and emits nothing (FR-015, edge "subset of domains declared").

## Technical Context

**Language/Version**: F# on .NET `net10.0` (`Directory.Build.props`: `TargetFramework=net10.0`,
`TreatWarningsAsErrors=true`, `Nullable=enable`, `GenerateDocumentationFile=true`).

**Primary Dependencies**: FSharp.Core 10.1.301 only for the pure packs and the shared core. The host sensors
use **BCL `System.IO`** for file reads and **`System.Text.Json`** (`Utf8JsonWriter`/`JsonDocument`, already
in use by `RouteJson`/`VerifyJson`) for catalog/manifest parsing — **no new package**. The package-domain
FSI-transcript sensor shells the F# Interactive runner through the existing
`FS.GG.Governance.GateExecution` process port (the same `ExecutionPort` `VerifyCommand` already injects) —
**no new dependency**, no `FSharp.Compiler.Service`. Project references reused verbatim: `Config`
(`TypedFacts`, `Surface`, `EvidenceTag`, `SurfaceClass`), `ProductSurfaces` (`ProductSurfaceReport`,
`ProductClassification`), `Enforcement` (`Severity`, `Maturity`, `deriveEffectiveSeverity`),
`EvidenceCapture`/`EvidenceReuse` (evidence tie), `GateExecution` (transcript execution port). **No new
dependency anywhere.**

**Storage**: None new — pure evaluation over caller-supplied sensed facts; the only writes are the existing
`verify.json` artifact (additive `surfaceChecks` section) and the existing evidence-reuse store. No database,
no network, no registry.

**Testing**: Expecto 10.2.3 + Expecto.FsCheck/FsCheck 2.16.6 (repo standard). One new test project per new
library; real fixture directories under each (a real `.fsi` pair for baseline drift, a real FSI transcript
that compiles/evaluates, real docs files with live/dead links, a real skill manifest + mirror, real
token/capture/contrast/control catalog files). Pure-pack tests feed real sensed facts and assert findings;
sensor tests read real on-disk fixtures through the real port; a determinism test per domain asserts
byte-identical findings on repeated runs; a composition test routes a single change across package+docs+skill
and asserts three independent findings; an advisory test asserts a base-Advisory finding never blocks across
the enforcement matrix; one real-filesystem `fsgg verify` end-to-end proves the additive surfacing. FSI
semantic tests load the public surface, not internals (Constitution I).

**Target Platform**: Cross-platform .NET libraries + the existing `fsgg verify` CLI executable
(Linux/macOS/Windows); standalone (no monorepo) and monorepo usage (FR-016).

**Project Type**: Capability-domain check expansion — a shared pure core (`Model`) plus a pure dispatcher
(`Composition`), four new pure-pack+sensor libraries, one extended host command; single-solution F# layout.

**Performance Goals**: Not a hot path for the pure packs (pure per-surface evaluation, sub-millisecond).
Sensor cost is dominated by the real reads each domain's cost tier already budgets (F23): docs/skill/design
are structural reads; the package FSI-transcript sensor is the expensive one (compile+evaluate) and runs at
its declared tier only. No performance-driven mutation.

**Constraints**: Deterministic, byte-identical findings and evidence for identical sensed facts (no
timestamps / abs-paths / usernames / order-dependence; stable path normalization and ordering — FR-010,
SC-005). Pure packs and the shared core carry **zero** rendering/UI/registry/filesystem dependency; the real
read lives only in each `Interpreter` (FR-007, SC-004). Input-vs-tool-defect diagnostics preserved: a missing
baseline, an unlocatable transcript, an unreadable docs source, an absent design catalog each produce a clear
input diagnostic naming the source, never a fabricated pass and never swallowed (FR-012, Constitution VI).
Standalone with no monorepo dependency (FR-016). Enforcement truth table and `capabilities.yml` schema
untouched (FR-013, FR-014).

**Scale/Scope**: 6 new `src` libraries (`SurfaceChecks` shared core (`Model`) + `SurfaceChecks.Dispatch`
(`Composition`) + `PackageChecks` / `DocsChecks` / `SkillChecks` / `DesignChecks`) + 5 new test projects
(`SurfaceChecks.Tests` covers both the core and the dispatcher); 1 extended host (`VerifyCommand` + its tests,
and `VerifyJson` additive section); 6 new committed surface baselines; new fixtures per domain; no schema or
enforcement change. P1 = package; P2 = docs + skill; P3 = design + advisory guarantee.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

- **I. Spec → FSI → Semantic Tests → Implementation** — PASS. Every new public module (`SurfaceChecks.Model`,
  the four domain `Model`/`evaluate`/`Interpreter` surfaces) is drafted as `.fsi` and exercised through the
  packed/loaded public surface before the `.fs` bodies exist (the F053–F057 precedent). Semantic tests call
  `evaluate`, the sensor `senseX`, and `Composition.run` — never internals.
- **II. Visibility Lives in `.fsi`** — PASS. Every new public module ships a curated `.fsi`; `.fs` bodies
  carry no access modifiers. Six new committed surface baselines (`SurfaceChecks` + `SurfaceChecks.Dispatch` + four domains);
  `VerifyJson` baseline updates only if its signature changes (the additive section is emitted by a new
  overload, mirroring `ofRouteResultWithProductSurfaces`, so the existing signature stays byte-identical).
- **III. Idiomatic Simplicity** — PASS. Closed DUs, plain records, pipelines, exhaustive matches; no
  SRTP/reflection/type-providers/custom CEs/non-trivial active patterns; no new dependency. Any local
  mutation in a JSON writer follows the disclosed `RouteJson`/`VerifyJson` precedent with a one-line reason.
- **IV. Elmish/MVU Is the Boundary** — PASS. The pure packs (`evaluate`) and the shared `Composition` are
  pure, total leaves — no MVU ceremony (the F046/F053 precedent). Each host sensor is an edge `Interpreter`
  with an injected port (`Read… : unit -> Result<…,string>`), the F054 `ReleaseFactsSensing` shape; I/O is
  represented as a port and executed only at the edge. `VerifyCommand` keeps its existing MVU boundary; the
  new sensors are invoked at its `Interpreter`, never inside a pure `update`.
- **V. Test Evidence Is Mandatory** — PASS. Tests fail-before/pass-after against real on-disk fixtures and
  real upstream cores (`Config`/`ProductSurfaces`/`Enforcement` never mocked). The package transcript sensor
  runs a real FSI process through the real execution port. No synthetic evidence is anticipated; any would be
  disclosed at the use site, carry `Synthetic` in the test name, and be listed in the PR.
- **VI. Observability and Safe Failure** — PASS. Each sensor distinguishes missing/malformed **input**
  (absent `.fsi` baseline → first-run generation note; unlocatable transcript; unreadable docs source; absent
  design catalog) from a **tool defect**, naming the offending source (FR-012). No swallowed errors; no
  fabricated pass; a domain with no declared surface emits nothing (FR-015), never a silent gap reported as
  success.

**Change Classification: Tier 1 (contracted change)** — adds new public API surface (six new libraries) and
extends a host's observable output (`verify.json` additive section). Requires the full chain: spec, plan,
`.fsi` for every new module, six new surface-area baselines, test evidence, and documentation of the
additive `verify.json` section. It adds **no** dependency, **no** `capabilities.yml` schema change, and **no**
enforcement-truth-table change, so the migration surface is limited to the additive `verify.json` section
(documented in `contracts/verify-json-surfacechecks.md`).

**Result: PASS — no violations. Complexity Tracking is empty.**

## Project Structure

### Documentation (this feature)

```text
specs/059-package-docs-skills-design-checks/
├── plan.md                              # This file (/speckit-plan output)
├── research.md                          # Phase 0 — D1..D8 decisions
├── data-model.md                        # Phase 1 — shared core + four domain vocabularies
├── quickstart.md                        # Phase 1 — per-domain validation scenarios
├── contracts/                           # Phase 1
│   ├── surface-check-finding.md         #   shared SurfaceFinding + SurfaceCheckRequest + Composition contract
│   ├── package-checks.md                #   .fsi baseline drift + FSI transcript facts/sensor/evaluate
│   ├── docs-checks.md                   #   link + reference currency facts/sensor/evaluate
│   ├── skill-checks.md                  #   path contract + task list + mirror facts/sensor/evaluate
│   ├── design-checks.md                 #   token/capture/contrast/control facts/sensor/evaluate (no-render fence)
│   └── verify-json-surfacechecks.md     #   additive verify.json `surfaceChecks` section (byte-identical when empty)
└── tasks.md                             # Phase 2 (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
├── FS.GG.Governance.SurfaceChecks/                 # NEW — shared core (pure, Model only)
│   ├── Model.fsi / Model.fs                        #   SurfaceFinding, FindingClass, SurfaceCheckRequest,
│   │                                               #     CheckDomain, location/evidence-tag binding, tokens
│   └── FS.GG.Governance.SurfaceChecks.fsproj       #   refs: Config, Enforcement
├── FS.GG.Governance.SurfaceChecks.Dispatch/        # NEW — pure dispatcher (split to break the core↔domains cycle)
│   ├── Composition.fsi / Composition.fs            #   run : ProductSurfaceReport -> DomainFactBundle ->
│   │                                               #     SurfaceFinding list (applicable packs, order-independent)
│   └── FS.GG.Governance.SurfaceChecks.Dispatch.fsproj  # refs: SurfaceChecks, Config, ProductSurfaces, + four domains
├── FS.GG.Governance.PackageChecks/                 # NEW (P1) — pure pack + sensor
│   ├── Model.fsi / Model.fs                        #   PackageFacts: FsiBaselineFact, TranscriptFact
│   ├── PackageChecks.fsi / PackageChecks.fs        #   evaluate (pure): baseline drift + transcript findings
│   ├── Interpreter.fsi / Interpreter.fs            #   sensor: read .fsi + run transcripts via ExecutionPort
│   └── FS.GG.Governance.PackageChecks.fsproj       #   refs: SurfaceChecks, Config, GateExecution
├── FS.GG.Governance.DocsChecks/                    # NEW (P2)
│   ├── Model.fsi / Model.fs                        #   DocsFacts: LinkFact, ReferenceFact
│   ├── DocsChecks.fsi / DocsChecks.fs              #   evaluate (pure): link + reference currency findings
│   ├── Interpreter.fsi / Interpreter.fs            #   sensor: read docs sources, resolve link/anchor targets
│   └── FS.GG.Governance.DocsChecks.fsproj          #   refs: SurfaceChecks, Config
├── FS.GG.Governance.SkillChecks/                   # NEW (P2)
│   ├── Model.fsi / Model.fs                        #   SkillFacts: PathContractFact, TaskListFact, MirrorFact
│   ├── SkillChecks.fsi / SkillChecks.fs            #   evaluate (pure): path/task-list/mirror findings
│   ├── Interpreter.fsi / Interpreter.fs            #   sensor: read skill manifest + mirror
│   └── FS.GG.Governance.SkillChecks.fsproj         #   refs: SurfaceChecks, Config
├── FS.GG.Governance.DesignChecks/                  # NEW (P3) — fenced: pure pack carries NO render dep
│   ├── Model.fsi / Model.fs                        #   DesignFacts: TokenFact, CaptureFact, ContrastFact, ControlFact
│   ├── DesignChecks.fsi / DesignChecks.fs          #   evaluate (pure): resolution findings, no I/O
│   ├── Interpreter.fsi / Interpreter.fs            #   sensor: read token/capture/contrast/control catalogs
│   └── FS.GG.Governance.DesignChecks.fsproj        #   refs: SurfaceChecks, Config   (NO rendering/UI ref)
└── FS.GG.Governance.VerifyCommand/                 # EXTEND (additive surfacing, D8)
    ├── Loop.fs                                     #   thread SurfaceFinding list through Model + render
    └── Interpreter.fs                              #   run applicable sensors+packs at the edge; fold findings
src/FS.GG.Governance.VerifyJson/
    └── VerifyJson.fsi / VerifyJson.fs              #   new overload emits additive `surfaceChecks` (non-empty only)

tests/
├── FS.GG.Governance.SurfaceChecks.Tests/           # NEW — Composition order-independence + advisory matrix + standalone/reuse guards
│                                                   #   (refs SurfaceChecks + SurfaceChecks.Dispatch + four domains)
├── FS.GG.Governance.PackageChecks.Tests/           # NEW — baseline drift / first-run / transcript / determinism
│   └── fixtures/                                   #   committed .fsi baseline + changed surface; passing+broken transcript
├── FS.GG.Governance.DocsChecks.Tests/              # NEW — live/dead link, present/removed reference, determinism
│   └── fixtures/
├── FS.GG.Governance.SkillChecks.Tests/             # NEW — path contract, task list, mirror present/absent/drifted
│   └── fixtures/
├── FS.GG.Governance.DesignChecks.Tests/            # NEW — token/capture/contrast/control resolve+fail; no-render inspection
│   └── fixtures/
└── FS.GG.Governance.VerifyCommand.Tests/           # EXTEND — real-filesystem `fsgg verify` end-to-end + empty=byte-identical

surface/
├── FS.GG.Governance.SurfaceChecks.surface.txt          # NEW
├── FS.GG.Governance.SurfaceChecks.Dispatch.surface.txt # NEW
├── FS.GG.Governance.PackageChecks.surface.txt          # NEW
├── FS.GG.Governance.DocsChecks.surface.txt             # NEW
├── FS.GG.Governance.SkillChecks.surface.txt            # NEW
├── FS.GG.Governance.DesignChecks.surface.txt           # NEW
└── FS.GG.Governance.VerifyJson.surface.txt             # EDIT only if the new overload changes the committed surface

FS.GG.Governance.sln                                # EDIT — add 6 src + 5 test projects
```

**Structure Decision**: Compose, don't fork. The feature **consumes** F23's `ProductSurfaceReport` and the
F017 `Findings` / F023 `Enforcement` machinery rather than introducing a parallel classifier or enforcement
path. Each domain is one leaf library bundling its closed fact vocabulary, its pure `evaluate` rule pack, and
its edge `Interpreter` sensor — the exact `ReleaseFactsSensing` decomposition — so the four packs are
independent and composable (FR-008) and every real read is fenced into one `Interpreter` module per domain
(FR-007). The shared `SurfaceChecks` core holds only the cross-domain finding vocabulary; the pure
`Composition` dispatcher lives in the adjacent `SurfaceChecks.Dispatch` project (split to break the
`core ↔ domains` reference cycle). Surfacing reuses the existing `fsgg verify` host additively, exactly the
deterministic-JSON, byte-identical-when-empty precedent F23/F052 set — no new command and no enforcement
change.

## Complexity Tracking

> No Constitution Check violations. This section is intentionally empty.

## Implementation status (2026-06-25)

Implemented and verified against the build + real on-disk fixtures (78 F24 tests green, plus the VerifyJson
embed tests; `dotnet build FS.GG.Governance.sln` clean; six new surface baselines blessed):

- **Six `src` libraries** stand built and acyclic: `SurfaceChecks` (Model), `SurfaceChecks.Dispatch`
  (Composition), and the four domain packs `PackageChecks` / `DocsChecks` / `SkillChecks` / `DesignChecks`,
  each bundling `Model` + pure `evaluate` + edge `Interpreter` (the F054 `ReleaseFactsSensing` shape).
- **All four deterministic checks** produce `SurfaceFinding`s bound to the surface's declared `EvidenceTag`,
  sorted/normalized for byte-identical output; the package transcript sensor shells **real `dotnet fsi`**
  through the existing F051 `GateExecution.ExecutionPort` (no `FSharp.Compiler.Service`).
- **Composition** dispatches order-independently; **advisory** base-severity never escalates across the full
  `(RunMode, Profile)` matrix (real `Enforcement`, never mocked); the **render fence**, **neutrality**,
  **standalone read-scope**, and **no-new-vocabulary** guards all pass.
- **`VerifyJson.ofVerifyDecisionWithSurfaceChecks`** emits the additive `surfaceChecks` section, byte-identical
  to `ofVerifyDecision` when empty (the 44 existing `VerifyCommand` tests stay green).

**Deferred (not yet wired):** the `fsgg verify` host edge step (`VerifyCommand` Loop/Interpreter — tasks
T048/T045/T052). Surfacing through the command requires an async edge sense-step at both verify-doc
projection points plus folding F24 findings into the `Ship.rollup` decision; it was deferred to avoid
destabilizing the load-bearing command and is a well-scoped follow-up. The JSON contract it would emit is
already implemented and tested at the `VerifyJson` layer. See tasks.md §"Delivery status" for the full
breakdown and the documented contract resolutions (`ListTranscripts` / `ReadDescriptor` port readers,
`DocsFacts.Examples`, and the `ofVerifyDecisionWithSurfaceChecks` naming).
