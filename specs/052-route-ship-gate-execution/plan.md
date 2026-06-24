# Implementation Plan: Execute Selected Gates In `fsgg route` And `fsgg ship`, Capture Their Evidence, And Persist The Grown Reuse Store

**Branch**: `052-route-ship-gate-execution` | **Date**: 2026-06-24 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/052-route-ship-gate-execution/spec.md`

## Summary

Every pure core on the evidence-reuse path is merged, and F051 added the one impure capability the chain was
missing: `FS.GG.Governance.GateExecution` (`senseExecution realPort <command>` runs one gate to a complete
F032 `CommandRecord`). But `fsgg route` and `fsgg ship` still **never run a gate** — they sense freshness, load
the reuse store read-only, evaluate cache eligibility (F046), and re-persist a pruned/retained copy, yet
because nothing grows the store, every gate reports `mustRecompute / noPriorEvidence` forever and the ship
verdict never benefits from a real run.

This row is the missing wire. It teaches **both** host commands to, for each selected gate the cache marks
`mustRecompute` that declares a command: derive the gate's command-to-run from its **declared** command spec,
run it once through the injected F051 port, assemble its record, derive an `EvidenceRef` (F049 `referenceOf`),
fold it into the store (F049 `capture`), and persist the **grown** store (F047/F048 prune → retain →
serialise → write). A selected gate the cache marks `reusable` is **skipped**: its prior outcome is recovered
from the store and reused. Two load-bearing consequences (maintainer-confirmed, a deliberate departure from
the cache-only rows): a gate's real pass/fail now **gates the `fsgg ship` verdict and exit code** via the
**existing** F023/F024 enforcement, and `fsgg route` runs the same gates but **stays advisory** (always exits
0, reports execution as information on `route.json`).

The whole feature is **host wiring that composes merged cores verbatim** — F051 (run), F050 (record), F049
(capture), F046 (eligibility), F047/F048 (persist) — plus three small, genuinely new pure pieces the wiring
needs and no merged core supplies (resolved in [research.md](./research.md)):

1. **Derive a `GateCommand` from a declared command spec** — a pure POSIX-style argv split of the
   `CommandSpec.Command` line into `Executable` + ordered `Argument list`, with the working directory set to
   the governed repo root, an **empty** environment delta (the declared `EnvironmentClass` is a where-it-runs
   declaration, not an env mutation — FR-002), `NoCapturedOutput`, and the declared `Timeout` (D1).
2. **Recover a reusable gate's prior exit code** — the F049 `EvidenceRef` is the **full structured
   F032 canonical-identity string**, not a hash; it embeds `exit=<value>` recoverably. A pure parser reads the
   prior `ExitCode` from a reusable gate's stored reference; on any non-canonical reference the gate is
   conservatively **recomputed** (D2, FR-004) — so reuse never invents a verdict.
3. **Gate the ship verdict by pass/fail without editing a frozen core** — `Ship.rollup` derives severity from
   maturity only and has no seam for pass/fail. The host calls `Ship.rollup` **verbatim** (all selected gates
   + findings), then applies one pure post-processing step: a **passing** command-gate is **relocated** from
   `Blockers`/`Warnings` into `Passing` and the verdict/exit recomputed from the (now-possibly-empty)
   blocker set. A **failing** command-gate stays exactly where the existing rules placed it; a gate with no
   declared command keeps its current treatment. This reuses every F023/F024 severity decision unchanged and
   introduces no new severity scheme (D3, FR-006).

The new shared vocabulary and these pure helpers live in a small new library
`FS.GG.Governance.GateRun`, layered on the merged thread (the constitution's "heavier capabilities layer on
top, not into the core"). `RouteCommand` and `ShipCommand` gain an injected `ExecutionPort` in their `Ports`
record, a new execute Effect/Msg pair on their existing MVU loop, and the document/verdict wiring;
`RouteJson`/`AuditJson` gain an **optional** per-gate execution-outcome embed (default empty ⇒ byte-identical
to today, exactly as F045 added the cache embed). The committed contracts live in [contracts/](./contracts/);
the entities and flow in [data-model.md](./data-model.md); the build/exercise/test walkthrough in
[quickstart.md](./quickstart.md); the resolved decisions in [research.md](./research.md).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (repo standard; `Nullable=enable`, `TreatWarningsAsErrors=true`,
`WarnOn=3390;1182` from `Directory.Build.props`). This row adds one small new pure library
(`FS.GG.Governance.GateRun`) and edits the two existing host-command projects (`RouteCommand`, `ShipCommand`)
and the two document emitters (`RouteJson`, `AuditJson`). No new command, no new schema.

**Primary Dependencies**: `ProjectReference`s only; **no new third-party `PackageReference`** (FR-017). The
new `GateRun` library references the merged cores it composes the vocabulary of — `FS.GG.Governance.GateExecution`
(F051 `GateCommand`), `FS.GG.Governance.CommandRecord` (F032 `Executable`/`Argument`/`ExitCode`/identity
format), `FS.GG.Governance.EvidenceReuse` (F030 `EvidenceRef`), `FS.GG.Governance.Config` (F014 `CommandSpec`/
`ToolingFacts`/`TimeoutLimit`/`EnvironmentClass`), and `FS.GG.Governance.Gates` (F018 `Gate`/`GatePrerequisite`/
`CommandId`). `RouteCommand`/`ShipCommand` additionally already reference F046 `FreshnessSensing`/F041
`CacheEligibility`, F049 `EvidenceCapture`, F047 `EvidenceReuseStore`, and (ship) F024 `Ship`/F023
`Enforcement` — all already on their graph. The process is spawned **only** through the already-merged F051
`realPort` (BCL `System.Diagnostics.Process`), injected into the commands' `Ports`. Test frameworks unchanged
(Expecto, Expecto.FsCheck, FsCheck, Microsoft.NET.Test.Sdk, YoloDev.Expecto.TestSdk).

**Storage**: The reuse store at the conventional path (`<repo>/readiness/evidence-reuse.json`,
`fsgg.evidence-reuse-store/v1`). This row **reads** it (F046 `loadStore`, already wired), **grows** it (F049
`capture` of each executed gate), and **persists** it (F047/F048 `prune` → `retain defaultRetentionBound` →
`serialise` → write, already wired). **No schema version bump** (FR-010): the store fields are unchanged; the
prior exit code is recovered from the existing `evidence` field's canonical-identity string, not a new field.
`route.json` and `audit.json` gain an **additive optional** per-gate `execution` object (no `schemaVersion`
bump, the F045 cache-embed precedent).

**Testing**: Expecto + FsCheck. New `FS.GG.Governance.GateRun.Tests` covers the three pure helpers (argv lex
incl. quotes/escapes/empty; `GateCommand` derivation incl. the no-command ⇒ `None` and empty-env-delta cases;
`priorExitOf` round-trip against a **real** `referenceOf` of a `senseExecution` record, and `None` on a
non-canonical reference). The existing `FS.GG.Governance.RouteCommand.Tests` and
`FS.GG.Governance.ShipCommand.Tests` gain execution scenarios driven through a **deterministic fake
`ExecutionPort`** over **real temp-script fixtures** (one exits 0, one exits non-zero, one missing executable,
one sleeps past a short timeout — reaching the real F051 port only where an edge test needs it, mirroring
F051's discipline) with a **writable temp store**: assert the executed/reused/not-executed disposition per
gate, the captured grown store is persisted (pruned + retained), the second run **reuses** (no second spawn),
and (ship) the verdict + exit code track real pass/fail. The command tests **recompute** their expected
`route.json`/`audit.json` live (FR-009 / Assumptions); `RouteJson`/`AuditJson` own goldens stay byte-stable
because the new embed defaults to empty. Output digests under test derive from **real captured bytes** (never
`Synthetic` literals). New surface baseline `surface/FS.GG.Governance.GateRun.surface.txt`; updated baselines
for `RouteJson`/`AuditJson` (the additive embed) and `RouteCommand`/`ShipCommand` (the `Ports` field). No
network, no governed repository for the semantic tests (SC-007 carried forward).

**Target Platform**: Developer / CI .NET SDK running `dotnet test` on Linux. Gate processes are ordinary child
processes spawned by the merged F051 `realPort`; the real temp-script fixtures are `/bin/sh` scripts (the F051
/ Snapshot precedent). No OS-specific surface in `GateRun` or the command wiring — the platform-specific detail
is confined to the test fixtures.

**Project Type**: Host wiring over a small new pure library. **Principle IV applies and is satisfied by the
commands' existing MVU boundary**: the new gate execution is one more injected effect (`ExecutionPort` in the
`Ports` record), one more pure `Effect` requested by `update` and run only at the interpreter edge, and one
more `Msg` carrying results back. No new Elmish `Program` is introduced; the established `RouteCommand`/
`ShipCommand` `Loop`/`Interpreter` split absorbs the wiring. `GateRun` itself is pure (argv lex, command
derivation, prior-exit recovery, the verdict relocation) — pure given the injected port, exactly like the
merged `senseExecution`.

**Performance Goals**: N/A. The added cost is, per selected must-recompute command-gate, one child-process
spawn + one F050 record assembly (already F051's cost), plus one F049 capture and one store persist per run
(already F047's cost). Reuse **removes** cost on repeat runs (a `reusable` gate is not spawned — the headline
payoff, US2/SC-003). The single timing contract is inherited from F051: a timed-out gate returns within a
bounded time of its limit and never hangs (FR-011).

**Constraints**: Spawn-once-per-recompute (FR-001): each selected must-recompute command-gate is run exactly
once via `senseExecution`; reusable gates and no-command gates spawn nothing (FR-003, FR-005). Declared-inputs
only (FR-002): the command-to-run is derived solely from the gate's declared `CommandSpec` (command line →
executable + ordered args, declared timeout, working dir = repo root, **empty** env delta); no fabricated
command, no ambient-env diff, no altered timeout. Verbatim composition (FR-001, FR-010, FR-015): run/record/
capture/persist are the merged F051/F050/F049/F047 calls unchanged — the row dereferences no opaque reference
(except the **declared-format** prior-exit parse, D2), recomputes no freshness key/digest, and invents no
record/outcome/store shape. Reuse needs a recoverable outcome (FR-004): a `reusable` gate whose stored
reference is non-canonical (prior exit not recoverable) is conservatively recomputed, never treated as passed.
Existing enforcement only (FR-006, FR-007): pass/fail **gates whether a command-gate is counted** at its
**existing** effective severity (relocation of passing gates to `Passing`); no new severity scheme/mode/profile/
rule, and a reused gate contributes its prior outcome on identical terms. Advisory route (FR-008): route runs
gates and reports per-gate execution + executed-vs-reused, but always exits 0 and decides no merge. Additive
documents (FR-009): every non-execution field of `route.json`/`audit.json` is byte-identical to today; the only
changes are the per-gate `execution` embed and the ship verdict changes that follow directly from real pass/
fail. Totality (FR-011): a missing executable / start failure / timeout is the F051 recorded sentinel outcome,
never a throw; the command never hangs. Full-fidelity capture (FR-012): output of any size/byte-content is
digested in full by the merged F050 digest the F051 port applies. Honest degradation (FR-013): absent store ⇒
empty (all execute); unreadable store ⇒ empty + surfaced; persist failure ⇒ surfaced; none crash or change the
already-computed verdict/exit. Determinism (FR-014): two runs of the same deterministic gate over the same
world yield a byte-identical `canonicalId` (duration excluded) so the second reuses; the persisted store is
deterministic and bounded. No frozen-core edit / no schema bump (FR-017): no edit to F023/F024/F041–F051 cores,
the F045 embed, or the F049/F050/F051 surfaces; no new dependency; `RouteJson`/`AuditJson`/`RouteCommand`/
`ShipCommand` are the host seams this row legitimately extends.

**Scale/Scope**: Additive + host wiring. **New**: `src/FS.GG.Governance.GateRun/` (`Model.fsi/.fs`,
`Plan.fsi/.fs`, `.fsproj`), `tests/FS.GG.Governance.GateRun.Tests/`, `surface/FS.GG.Governance.GateRun.surface.txt`,
two `.sln` entries, a `scripts/prelude.fsx` section, the `CLAUDE.md` plan pointer. **Edited (host seams)**:
`RouteCommand` (`Loop` + `Interpreter`: `Ports.Execute`, execute Effect/Msg, capture + persist-grown-store
wiring, per-gate outcome on `route.json`), `ShipCommand` (same + the pure verdict-relocation), `RouteJson`/
`AuditJson` (additive optional `execution` embed), and their surface baselines. **Untouched (frozen)**: F051
`GateExecution`, F050 `ExecutionRecord`, F049 `EvidenceCapture`, F047/F048 `EvidenceReuseStore`, F046
`FreshnessSensing`/F041 `CacheEligibility`, F032 `CommandRecord`, F030 `EvidenceReuse`, F024 `Ship`, F023
`Enforcement`, the F045 cache embed, the `fsgg.evidence-reuse-store/v1` schema, and every other core/golden.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — still PASS.*

| Principle | Status | Justification |
|-----------|--------|---------------|
| I. Spec → FSI → Semantic Tests → Implementation | PASS | FSI-first is satisfied by committing `GateRun` `contracts/Model.fsi` + `contracts/Plan.fsi` and the host-seam deltas (`contracts/host-wiring.md`) **before any `.fs` body**, and writing public-surface semantic tests that fail before implementation: the three pure helpers through the packed `GateRun` surface, and the route/ship commands end-to-end through a fake `ExecutionPort` + real temp-script fixtures + a writable temp store. The `scripts/prelude.fsx` F052 section is the runnable honest-audience transcript (a two-run route+ship demo: first run executes & grows the store, second run reuses), not the design sketch. |
| II. Visibility lives in `.fsi` | PASS | Every new public symbol is declared in the curated `GateRun/Model.fsi` (`GateDisposition`, `GateOutcome`) and `GateRun/Plan.fsi` (`lexCommandLine`, `commandFor`, `priorExitOf`, `passed`); the new `Ports.Execute` field is declared in each command's `Interpreter.fsi`; the new `execution`-embed parameters are declared in `RouteJson.fsi`/`AuditJson.fsi`; the ship verdict-relocation helper is declared in `ShipCommand`'s `.fsi`. The `.fs` files carry no `private`/`internal`/`public` modifiers (the argv lexer and the relocation's inner helpers live unexported, kept off-surface by absence from the `.fsi`). A new `surface/FS.GG.Governance.GateRun.surface.txt` baseline plus updated `RouteJson`/`AuditJson`/`RouteCommand`/`ShipCommand` baselines are guarded by the existing reflective drift test. |
| III. Idiomatic Simplicity | PASS | The plainest F#: `commandFor` is a `match` on the gate's `RequiresCommand` prerequisite + a catalog lookup + the argv lex; `priorExitOf` is a `String.split`-and-find over the documented canonical-identity format (F032 `command-record-identity-format.md`); the verdict relocation is a `List.partition` keyed by a `Set<GateId>` plus the **same** one-line verdict rule re-applied. Run/record/capture/persist are existing calls. The argv lexer is a small explicit character scanner (quotes + backslash escapes) — disclosed `mutable` index/accumulator confined to it (`// mutable: single-pass argv scan`), the constitution's sanctioned hot-loop use; no custom operators, SRTP, reflection (outside tests), type providers, recursion-for-state, or non-trivial CEs. |
| IV. Elmish/MVU boundary | PASS (extends the commands' existing boundary) | `RouteCommand`/`ShipCommand` are already MVU (`Loop.init`/`update` pure, `Effect`/`Msg` data, `Interpreter` ports). Gate execution is added **the same way the existing freshness-sense and store-load effects are**: I/O is data (the injected `ExecutionPort` in `Ports`), `update` requests it as a pure `Effect` (`ExecuteGates`) and never spawns a process itself, and interpretation (`senseExecution port`) happens **only at the interpreter edge**, feeding a `Msg` (`GatesExecuted`) back into the pure `update` that folds capture and projects documents. Both sides are tested: pure `update` transitions (given the execute msg, assert the grown store + projected documents + ship verdict) and the interpreter against real child processes (real temp scripts). `GateRun` is pure given the port. |
| V. Test Evidence | PASS | Semantic tests fail before the wiring exists and pass after, driving the public FSI surfaces against **real** captured bytes, **real** F032 identity, **real** F049 capture/persist round-trips, **real** child processes (the edge fixtures), and a **real** writable store — reaching no network and no governed repository (SC-007). This row **removes** the last synthetic on the verdict path: the ship verdict was previously selection-only; it is now driven by a real run, and the reuse path is proven by a genuine second run that does not spawn. No `Synthetic` outcome literals; the fake `ExecutionPort` is a deterministic double over real `byte[]` (the real port is also exercised), not a stand-in for unavailable evidence. |
| VI. Observability & Safe Failure | PASS | Totality is inherited from F051 and **preserved**: a missing executable / start failure / timeout is the recorded sentinel outcome (named `startFailureExitCode`/`timeoutExitCode`), surfaced in the command summary and the document, never a swallowed exception or a hang (FR-011, FR-016). Store read/persist failures degrade explicitly (absent ⇒ empty, unreadable ⇒ empty + surfaced, persist failure ⇒ surfaced) and never lose the run's already-computed verdict (FR-013) — the established command behavior, carried forward. A reusable gate with a non-recoverable prior outcome is recomputed rather than silently passed (FR-004) — failing safe, distinguishing "no recoverable evidence" from "passed". |

**Change Classification**: **Tier 1 (contracted change)** — adds public API surface (the new `GateRun` library
with a surface baseline; new `Ports.Execute` fields; new `execution`-embed parameters on `RouteJson`/
`AuditJson`; the ship relocation helper) and alters observable behavior covered by existing specs (the
`fsgg ship` verdict/exit code now reflect real gate runs; `route.json`/`audit.json` carry a per-gate execution
embed). Requires the full artifact chain: spec, plan, `.fsi` updates, surface-baseline updates, test evidence,
and the document changes. **No** new third-party dependency; **no** schema version bump; **no** edit to any
frozen merged core or its golden baseline beyond the route/ship command tests that legitimately recompute their
expected documents.

**Engineering Constraints**: net10.0 ✅; each new/edited public module carries a curated `.fsi` ✅; surface
baselines added/updated ✅; no new third-party dependency ✅ (BCL via the already-merged F051 port +
FSharp.Core, over the already-on-graph F051→F050→F032→F014 chain and the existing command graph);
`FS.GG.Governance.*` namespace ✅; existing packages' pack output unaffected ✅; one-way operating rule
unaffected — the command runs *whatever executable the gate's declared spec supplies*, assuming no rendering
package IDs, template names, or layout ✅. No violations → **Complexity Tracking is empty**.

## Project Structure

### Documentation (this feature)

```text
specs/052-route-ship-gate-execution/
├── plan.md              # This file (/speckit-plan command output)
├── spec.md              # Feature specification (input)
├── research.md          # Phase 0 output — the resolved decisions (D1–D9)
├── data-model.md        # Phase 1 output — entities, dispositions, the run/capture/verdict flow
├── quickstart.md        # Phase 1 output — build/exercise/test walkthrough (two-run reuse demo)
├── contracts/
│   ├── Model.fsi          # NEW GateRun surface — GateDisposition, GateOutcome
│   ├── Plan.fsi           # NEW GateRun surface — lexCommandLine, commandFor, priorExitOf, passed
│   └── host-wiring.md     # the host-seam deltas: Ports.Execute, the ExecuteGates Effect/GatesExecuted
│                          # Msg, the RouteJson/AuditJson execution-embed params, the ship relocation
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/FS.GG.Governance.GateRun/                          # NEW (this row) — pure helpers the wiring needs
├── Model.fsi          # curated surface: GateDisposition (Executed|Reused|NotExecuted), GateOutcome
├── Model.fs           # the two domain declarations
├── Plan.fsi           # curated surface: lexCommandLine, commandFor, priorExitOf, passed
├── Plan.fs            # argv lexer (unexported scanner), commandFor (CommandSpec → GateCommand option),
│                       # priorExitOf (canonical-identity parse), passed (exit 0 ⇒ pass)
└── FS.GG.Governance.GateRun.fsproj   # ProjectReferences: GateExecution, CommandRecord, EvidenceReuse,
                                       # Config, Gates; compile order Model.fsi→Model.fs→Plan.fsi→Plan.fs

surface/
└── FS.GG.Governance.GateRun.surface.txt              # NEW reflective baseline (generated via BLESS_SURFACE)

src/FS.GG.Governance.RouteCommand/                     # EDITED — host seam
├── Interpreter.fsi/.fs   # + Ports.Execute: ExecutionPort; realPorts wires GateExecution.realPort; the
│                          # ExecuteGates effect runs senseExecution port per (GateId,GateCommand)
└── Loop.fsi/.fs          # + ExecuteGates effect / GatesExecuted msg; update derives commands for
                           # mustRecompute command-gates, on results folds F049 capture into the store,
                           # builds per-gate GateOutcome, projects route.json with the execution embed,
                           # persists the GROWN store; route still always exits 0

src/FS.GG.Governance.ShipCommand/                      # EDITED — host seam + verdict
├── Interpreter.fsi/.fs   # + Ports.Execute (as route)
└── Loop.fsi/.fs          # as route, plus: a pure relocation that moves PASSING command-gates from the
                           # Ship.rollup Blockers/Warnings into Passing and recomputes Verdict/ExitCodeBasis
                           # (failing & no-command gates keep their existing rollup treatment)

src/FS.GG.Governance.RouteJson/  RouteJson.fsi/.fs     # EDITED — additive optional per-gate `execution`
src/FS.GG.Governance.AuditJson/  AuditJson.fsi/.fs     #   embed (default empty ⇒ byte-identical to today),
surface/FS.GG.Governance.RouteJson.surface.txt         #   matched by GateId beside the F045 cacheEligibility
surface/FS.GG.Governance.AuditJson.surface.txt         #   embed; baselines updated for the new param

tests/FS.GG.Governance.GateRun.Tests/                  # NEW — argv lex, commandFor, priorExitOf round-trip
tests/FS.GG.Governance.RouteCommand.Tests/             # EDITED — execution scenarios (fake port + real
tests/FS.GG.Governance.ShipCommand.Tests/              #   temp scripts + writable store; two-run reuse;
                                                       #   ship verdict tracks pass/fail; recompute documents)

scripts/prelude.fsx                                    # + an F052 route+ship two-run walkthrough section
FS.GG.Governance.sln                                   # + the new GateRun src + test project entries

# Untouched (frozen): F051 GateExecution, F050 ExecutionRecord, F049 EvidenceCapture, F047/F048
# EvidenceReuseStore, F046 FreshnessSensing, F041 CacheEligibility, F032 CommandRecord, F030 EvidenceReuse,
# F024 Ship, F023 Enforcement, the F045 cache embed, the Route/Gates/Config cores, the
# fsgg.evidence-reuse-store/v1 schema, and the command-record-identity-format contract.
```

**Structure Decision**: Put the three genuinely-new pure pieces in **one small new shared library**
(`FS.GG.Governance.GateRun`) referenced by both commands, rather than duplicating them across the two `Loop.fs`
files or wedging them into a frozen core. Both `fsgg route` and `fsgg ship` need the **same** command
derivation, prior-exit recovery, and disposition/outcome vocabulary; a shared pure library is the repo idiom
(≈40 focused libraries) and keeps the host loops thin. The **impure** part (spawning a gate) is **not** new —
it is the merged F051 `realPort`, **injected** into each command's existing `Ports` record exactly like the
already-injected freshness sensor and store reader, so Principle IV is honored by extending the established MVU
boundary rather than adding a second I/O surface. The **ship-only** verdict relocation depends on `Ship.Model`/
`Enforcement` and so lives in `ShipCommand` (not `GateRun`, which stays free of any `Ship` dependency so route
need not carry it). The document embed extends `RouteJson`/`AuditJson` the same additive, default-empty way
F045 added the cache embed — preserving their goldens byte-for-byte unless a caller supplies real outcomes.

## Complexity Tracking

> No Constitution Check violations. This section is intentionally empty.
