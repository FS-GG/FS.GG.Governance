# Implementation Plan: The Effects Edge — Sense → Plan → Act, with Nondeterminism Reified as Evidence

**Branch**: `008-effects-interpreter` | **Date**: 2026-06-18 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/008-effects-interpreter/spec.md`

## Summary

Add the **effects shell** (F08) as a **new project** — `FS.GG.Governance.Host` — that sits at
the edge of the already-shipped pure kernel (F01–F07) and closes the governance loop:
**sense** facts from real artifacts, **plan** by running the pure kernel over them, and **act**
on the resulting effects (dispatch agent reviews, freeze verdicts), turning every real-world
result back into an event the pure core consumes. This is the **Elmish/MVU boundary feature** —
Constitution **Principle IV applies for the first time** — and it **completes the effects half
of Milestone M2**.

It introduces two modules, one per side of the boundary:

1. **`Loop` — the pure core.** A local MVU/effect algebra (no Elmish runtime — research D2):
   a `Model<'fact>` (the durable loop state — sensed facts, pending reviews, the acted-on
   `Route`, disclosures, failures), a `Msg<'fact>` (every effect result, success or failure,
   plus internal transitions), an `Effect` (the I/O the loop *requests* — `ReadArtifact`,
   `LoadReview`, `DispatchReview`, `RecordVerdict`, `EmitOutput`), an `init`, and a **pure,
   total `update`**. `update` runs the F01 fixed point over the F04-bridged rules to *plan* and
   emits effects to *act* — but performs **no I/O** and **never throws** (FR-002). It locks
   **decision #2** (the configurable `AcceptancePolicy` that decides whether a stochastic
   verdict is trustworthy enough to **freeze** — `accept` is a pure fold) and the **decision #3**
   instruction/data isolation *as a type* (the `ReviewTask` carries the rule's `Instruction`
   and the untrusted artifact `Data` as **separate fields** the loop never merges).

2. **`Interpreter` — the edge.** The injected ports (`ArtifactReader`, `Judge`, `ReviewStore`,
   `OutputSink`, bundled as `Ports`) and the one impure function, `run`, that drives the loop
   from `init` to **quiescence**: it executes each `Effect` against the ports, **reifies every
   result — including every failure — as a `Msg`** (FR-004/FR-012), and feeds it back into the
   pure `update`. The judge and store are **injected**, so the whole loop is exercised against a
   **real filesystem fixture** with a **fake judge** and **no real network or agent** (FR-017,
   SC-009).

The keystone behaviour is that **the hard, untrustworthy part of governance — I/O and a
stochastic AI judge — is made observable and safe by reifying it as data**. A stochastic verdict
that meets the `AcceptancePolicy` is **frozen as a `RecordedReview`** keyed by the **F04
content-hash cache key** (`CheckRule.cacheKey` — judge identity + check hash + artifact hashes +
prompt hash); on a re-run the loop **loads it from the store and short-circuits without
dispatching** (FR-008) — nondeterminism enters once, is recorded, and is thereafter reproducible
and free. Acting on an F07 `Route`, the loop enforces blocking gates **only** at `Gate` mode,
recomputed from the base (FR-011).

The feature **depends on the kernel only** (a single `ProjectReference` to
`FS.GG.Governance.Kernel`) and adds **no new `PackageReference`**: sensing uses `System.IO`, the
F06 emit reuses the kernel's `System.Text.Json`-backed `Json.*`, and the MVU algebra is plain
F#. It is **domain-neutral and ships no adapter** (F09–F11 deferred): the kernel catalog,
bridge, fences, run mode, acceptance policy, and the lifts that turn sensed content into `'fact`
are all supplied to the loop as a caller-provided `LoopConfig<'change,'fact>` — so F08 provides
the generic effects edge that F12 (the CLI) wires concrete ports into. The public surface is two
curated `.fsi` contracts — [`contracts/Loop.fsi`](./contracts/Loop.fsi) and
[`contracts/Interpreter.fsi`](./contracts/Interpreter.fsi) — with a **new** surface-area
baseline (`surface/FS.GG.Governance.Host.surface.txt`) and a Host-side surface-drift /
dependency-hygiene test (FR-018).

This feature **locks decision #2 and decision #3** and **opens decision #5** (a cost/latency
budget for reviews plus a judge-vs-human meta-validation loop) as a tracked deferral. The
`TODO(STRUCTURED_LOGGING)` constitution item is resolved for F08 by an ADR
([`docs/decisions/0001-structured-logging.md`](../../docs/decisions/0001-structured-logging.md)):
**no logging dependency** — observability is the `Model`'s `Failures`/`Disclosures` values plus
the host's own `OutputSink` (research D8).

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (inherited from `Directory.Build.props`).

**Primary Dependencies**: **None new** (no `PackageReference`). `FS.GG.Governance.Host` takes a
single `ProjectReference` on `FS.GG.Governance.Kernel` and uses the BCL only: `System.IO` for the
filesystem sensing/store at the edge, and the kernel's `Json.*` (shared-framework
`System.Text.Json`) for the F06 edge outputs. The MVU/effect algebra is plain F# — **no Elmish
package** (research D2). Test project only: Expecto + FsCheck, already pinned centrally.

**Storage**: At the **edge only** — the injected `ReviewStore` persists `RecordedReview` values
(the frozen evidence) keyed by the F04 cache key; tests back it with a **real local-filesystem
store fixture** under a temp directory, and `ArtifactReader` reads a **real filesystem tree** of
governed artifacts. The pure `Loop` performs no storage of any kind (FR-002). No database, no
network, no real agent (SC-009).

**Testing**: `dotnet test`. Two-sided per Principle IV (FR-016, SC-010): **pure transition
tests** assert `(Model, Msg) ⇒` next `Model` + effects with **zero** I/O (no file, process,
network, clock, agent) — exercising the public `Loop` surface through the built library /
`scripts/prelude.fsx`; **interpreter tests** drive `run` against a **real filesystem fixture**
and a **fake judge** + real-fs store, asserting the loop senses, dispatches, and freezes
correctly and that a re-run hits the cache (zero dispatches). FsCheck properties for
**order-independence** (permuting independent effect completion ⇒ identical final `Model`,
SC-007), **idempotency** (re-applying the same result `Msg` records no duplicate verdict/fact,
SC-007), and **safe-failure totality** (no driven input throws or yields a malformed `Model`,
SC-006). Targeted tests for: the freeze-then-cache-hit round trip and each cache-key ingredient
forcing a fresh dispatch (SC-003); the acceptance-policy gate — below-policy ⇒ `StayPending`,
nothing recorded (SC-004); instruction/data isolation — an injection-laden artifact leaves the
instruction channel byte-for-byte identical (SC-005); the `Gate`-recompute-from-base guarantee
(SC-008); and the new Host surface-drift + dependency-hygiene baseline (FR-018, SC-009).

**Target Platform**: cross-platform .NET library (Linux dev host).

**Project Type**: **library** — a new effects-shell library `FS.GG.Governance.Host` (+ its test
project), separate from and depending on the pure kernel (FR-017).

**Performance Goals**: correctness, determinism, and safe failure — not throughput. The loop
runs the kernel fixed point once per planning round; rounds are bounded by the (finite) review
set, so the loop terminates. No measured hot path. Every pure function is **total** (FR-002).

**Constraints**: `update` is **pure & total** — no filesystem, network, process, clock, or agent
call inside it, and it never throws (FR-002, SC-001); all I/O is **reified as `Effect`** and
executed **only** in `Interpreter.run` (FR-003). Every effect result, **including every
failure**, re-enters as a `Msg` — the interpreter never throws out of itself (FR-004/FR-012,
SC-006). A stochastic verdict is **frozen only when the `AcceptancePolicy` is met** (FR-009,
SC-004); the reviewer **instruction is isolated from untrusted artifact data** by type (FR-010,
SC-005). A recorded verdict matching the cache key **short-circuits dispatch** (FR-008, SC-003).
Blocking gates are enforced **only** at `Gate`, recomputed from base (FR-011, SC-008). The loop
is **idempotent and order-independent** (FR-014, SC-007). **Zero new dependency**; judge and
store are **injected ports** — no real network/agent in the suite (FR-017, SC-009).

**Scale/Scope**: one new project with two public modules — `Loop` (the pure MVU core:
`Model`/`Msg`/`Effect` + value types `ArtifactContent`, `JudgeVerdict`, `ReviewTask`,
`ReviewDispatch`, `AcceptancePolicy`, `Acceptance`, `Disclosure`, `Failure`, `Output`, `Phase`,
`LoopConfig`; the folds `defaultPolicy`/`samplesFor`/`accept`/`init`/`update`) and `Interpreter`
(the edge: ports `ArtifactReader`/`Judge`/`ReviewStore`/`OutputSink`/`Ports`; `step`/`run`).

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.0.0. Re-checked after Phase 1
design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | The two `contracts/*.fsi` are drafted first; the FSI sketch extends `scripts/prelude.fsx` (quickstart) driving `init`/`update` and a `run` against a temp fixture; semantic tests against the public surface precede the `.fs` bodies. `tasks.md` will order accordingly. |
| II. Visibility lives in `.fsi` + surface baseline | **PASS** | Two curated `.fsi` are the sole surface; the `.fs` carry no `private`/`internal`/`public` on top-level bindings. A **new** baseline `surface/FS.GG.Governance.Host.surface.txt` plus a Host-side reflective drift test (V13) and a dependency-hygiene test (V14: Host → BCL/FSharp.Core/Kernel only) (FR-018). |
| III. Idiomatic simplicity | **PASS** | Plain records/DUs + a local MVU loop. `accept` is a small total fold; `update` is a `match` over `Msg` that calls the existing pure kernel (`CheckRule.toRule`, `FixedPoint.evaluate`) — no new evaluation logic (FR-006). No custom operators, no SRTP, no reflection (outside tests), no type providers; the only computation expression used is `result`/`option` (permitted). A **local effect algebra instead of the Elmish package** is the constitution-sanctioned shape for a library and is justified in research D2 (keeps deps zero). The generic `'change`/`'fact` parameters are the kernel's house style. |
| IV. Elmish/MVU boundary | **APPLIES — PASS** | This is the boundary feature. The `.fsi` exposes `Model`/`Msg`/`Effect`/`init`/`update` + an edge interpreter (`run`); `update` is pure and total, I/O is `Effect` data, interpretation happens **only** in `Interpreter` (FR-001–FR-004). Both sides are tested: pure transition tests + a real-filesystem interpreter test + an FSI transcript (FR-016, SC-010). |
| V. Test evidence mandatory; prefer real | **PASS** | Real filesystem fixture for sensing and the store; the **only** fake is the stochastic `Judge` — a real agent cannot be a reproducible oracle (spec Assumptions, Principle V). The fake judge carries the `Synthetic` token where used and is disclosed; the store and reads are real evidence. |
| VI. Observability & safe failure | **PASS** | Every failure is reified as a `Failure`/`Msg` (FR-012); the interpreter never throws out of itself (SC-006); `Failure` cases **distinguish absent/bad input** (`ArtifactUnavailable`, `ReviewDispatchFailed`, `ReviewStoreUnavailable`) from a tool defect (which would surface as a test failure, not a `Failure` value). Disclosures are observable values; no silent verdict flip (FR-013). The logging dependency question is resolved by an ADR (research D8). |
| Change Classification | **Tier 1** | New project + new public API surface (two modules) + a new dependency *direction* (Host → Kernel) + a new surface baseline; full artifact chain (spec, plan, two `.fsi`, baseline, tests, docs, ADR) (FR-018). |
| Engineering Constraints | **PASS** | `net10.0`; new `FS.GG.Governance.Host` library under `src/`; `.fsi` per public module; new surface baseline; **zero new `PackageReference`** (ProjectReference to the kernel + BCL `System.IO`/`System.Text.Json`); no rendering/domain vocabulary (the change is the generic `'change`; `'fact` is the kernel's). Governance-may-inspect / project-must-never-require holds — Host depends on Kernel, not the reverse. ADR records the structured-logging decision (constitution `TODO(STRUCTURED_LOGGING)`). **Completes M2**; no packing action (the kernel packs at F06; the CLI tool packs at F12 — research D7). |

**Gate result: PASS — no unjustified violations. Complexity Tracking left empty (the local MVU
algebra, the generic `'change`/`'fact` parameters, and the injected-port records are ordinary,
constitution-sanctioned F# idioms, not waived complexity).**

Decisions locked / touched by this feature (roadmap §F08): **locks decision #2** (the
configurable `AcceptancePolicy`: a below-policy stochastic verdict is **never** frozen, stays
`Uncertain`/pending, and the default — `SingleSample` — is explicit and deterministic) and
**decision #3** (the `ReviewTask` carries the reviewer `Instruction` and the untrusted artifact
`Data` as **separate fields**, so a malicious artifact cannot become instruction). It **opens
decision #5** (cost/latency budget + judge-vs-human meta-validation) as a tracked deferral, not
implemented here. It consumes **F07** (`Route`/`route` — acted on at `Gate`), **F06** (`Json.*`
explanation/contract + `Freshness` — emitted at the edge), **F04** (`toRule`, `NeedsReview`/
`RecordedReview`, `cacheKey`, `JudgeId`, `Bridge`), and through them the whole pure kernel
(F01–F03, F05). It is consumed by **F12** (the CLI wires concrete ports into `run`) and reused by
**F13** (external validation). **Out of scope** (deferred): the adapter SPI / composition root
(F09), the concrete Spec Kit / design-system adapters (F10/F11), the CLI command surface (F12),
and decision #5 (the review budget + meta-validation loop).

## Project Structure

### Documentation (this feature)

```text
specs/008-effects-interpreter/
├── plan.md              # This file
├── research.md          # Phase 0 — engineering decisions D1–D8
├── data-model.md        # Phase 1 — MVU types, loop lifecycle, policy/isolation/cache rules, invariants
├── quickstart.md        # Phase 1 — FSI sketch (init/update + run over a temp fixture) + validation V48–V60
├── contracts/
│   ├── Loop.fsi         # Phase 1 — the pure MVU core (Model/Msg/Effect/init/update + value types)
│   └── Interpreter.fsi  # Phase 1 — the edge: injected ports + run
└── tasks.md             # Phase 2 — created by /speckit-tasks (NOT here)
```

### Source Code (repository root)

```text
src/FS.GG.Governance.Host/                 # NEW project — the effects shell (FR-017)
├── FS.GG.Governance.Host.fsproj           # ProjectReference → Kernel; ZERO PackageReference; net10.0
├── Loop.fsi                               # = contracts/Loop.fsi (NEW) — the pure MVU core
├── Loop.fs                                # implementation against the stable signature (NEW)
├── Interpreter.fsi                        # = contracts/Interpreter.fsi (NEW) — the edge ports + run
└── Interpreter.fs                         # implementation against the stable signature (NEW)

src/FS.GG.Governance.Kernel/               # UNCHANGED (F01–F07) — Host depends on it, never the reverse

tests/FS.GG.Governance.Host.Tests/         # NEW test project
├── FS.GG.Governance.Host.Tests.fsproj     # Expecto + FsCheck; ProjectReference → Host
├── LoopTests.fs                           # NEW: pure transition tests — (Model,Msg) ⇒ Model+effects, zero I/O (V48–V52)
├── InterpreterTests.fs                    # NEW: run vs a REAL temp fixture + fake judge; freeze→cache-hit; safe failure (V53–V58)
├── SurfaceDriftTests.fs                   # NEW: V13 Host surface baseline; V14 Host deps = BCL/FSharp.Core/Kernel only
└── Main.fs                                # NEW: Expecto entry point

docs/decisions/
└── 0001-structured-logging.md             # NEW ADR — resolves TODO(STRUCTURED_LOGGING) for F08 (research D8)

scripts/prelude.fsx                        # extend: an F08 sketch (init/update assertions + a run over a temp dir)
surface/FS.GG.Governance.Host.surface.txt  # NEW baseline for the Host public surface (blessed at impl time)
FS.GG.Governance.sln                       # add the two new projects
fixtures/                                   # NEW (optional): a tiny governed-artifact tree for InterpreterTests
```

**Structure Decision**: a **new project**, `FS.GG.Governance.Host`, not an addition to the
kernel — the spec (FR-017) and the roadmap (§3: `FS.GG.Governance.Host` is the effects shell that
depends on Kernel) both require the impure shell to be **separate from and dependent on** the pure
kernel, never the reverse. Inside it, two modules in compile order **`Loop` → `Interpreter`**:
`Interpreter` references `Loop`'s `Effect`/`Msg`/`Model`/`LoopConfig`/`Output` and the kernel.
The kernel's V12 BCL-only hygiene test is unaffected (the kernel gains no reference); a **new**
V14 Host-hygiene test asserts Host references only BCL/FSharp.Core/Kernel (no Elmish, no heavy
dep — research D2/D6). The `surface/`, `scripts/`, and central build-props scaffolding from F01
is reused; a new per-module baseline is added for Host. This feature **completes M2**; it carries
no packing/milestone action — the kernel already packs (F06) and the CLI tool packs later (F12).

## Complexity Tracking

> No unjustified Constitution Check violations. The local MVU/effect algebra (instead of the
> Elmish package) is the constitution-sanctioned shape for a library and keeps dependencies at
> zero (research D2); the generic `'change`/`'fact` parameters and the injected-port records are
> the kernel's established domain-neutrality idiom. No entries required.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
