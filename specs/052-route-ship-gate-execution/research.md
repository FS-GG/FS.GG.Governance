# Phase 0 Research: Execute Selected Gates In `fsgg route` / `fsgg ship`

All NEEDS CLARIFICATION from the Technical Context are resolved below. Each decision records what was chosen,
why, and the alternatives rejected. The two scope departures (ship verdict is execution-driven; reusable gates
are skipped) were maintainer-confirmed this session and are encoded in the spec's Assumptions; the
*mechanisms* that realize them — flagged in the spec as plan-time decisions — are resolved here (D1, D2, D3).

## D1 — Derive the `GateCommand` from the declared command spec: argv-lex the command line, repo-root cwd, empty env delta, declared timeout, no captured-output target

**Decision**: A gate's command-to-run is built purely from its declared `CommandSpec`
(`{ Id; Command: string; Timeout: TimeoutLimit; Environment: EnvironmentClass }`, resolved from the loaded
`ToolingFacts.Commands` by the gate's `RequiresCommand` `CommandId`) as follows:

- **`Executable` + `Arguments`** ← a pure POSIX-style argv split (`lexCommandLine`) of `CommandSpec.Command`:
  whitespace separates tokens; single quotes, double quotes, and backslash escapes group/quote a token; the
  first token is the `Executable`, the rest are the ordered `Argument list`. No shell features (no globbing,
  variable expansion, pipes, redirection) — a literal argv split only.
- **`WorkingDirectory`** ← the **governed repo root** (the same root the commands already resolve for sensing).
- **`Environment`** ← the **empty** `EnvironmentDelta` (`{ Added=[]; Changed=[]; Removed=[] }`).
- **`Timeout`** ← `CommandSpec.Timeout` verbatim (the catalog default `defaultTimeout` already applies when a
  command omits it, per F018 Gates).
- **`CapturedOutput`** ← `NoCapturedOutput`.

A gate with **no** `RequiresCommand` prerequisite (or whose `CommandId` resolves to no `CommandSpec`) yields
**no** `GateCommand` (`commandFor` returns `None`) and is not executed (FR-005).

**Rationale**: FR-002 requires the declared inputs applied **verbatim** with **no ambient-env leak** and no
fabricated command or altered timeout. The repo confirms the gap this fills: `CommandSpec.Command` is stored as
a raw shell string (no parsing at config load) and **nothing** in the codebase splits it into executable +
arguments, so the argv lexer is genuinely new and minimal. The F051 `realPort` builds `ProcessStartInfo` from
an **ordered `ArgumentList`** (it does no shell string-splitting itself — that is deliberately the caller's
job), so an argv split is exactly the bridge it expects. The **empty** env delta is the only choice consistent
with FR-002 + determinism (SC-008): `CommandSpec.Environment` is an `EnvironmentClass` (`Local | Ci |
LocalOrCi | Release`) — a declaration of *where a command may run*, **not** a specification of environment
*mutations*; there is no existing mapping from a class to a delta, and synthesizing one (or diffing the ambient
process environment) would inject machine-specific, non-deterministic variables into the reproducible facts and
break the byte-identical `canonicalId` the reuse loop depends on. The repo root is the natural, deterministic
working directory (a relative cwd or the process cwd would be ambient and non-reproducible), and
`NoCapturedOutput` matches F051's common case (a captured-output *file* subsystem is explicitly out of scope).

**Alternatives rejected**: Mapping `EnvironmentClass` to a non-empty delta — rejected: no declared mapping
exists, and any synthesized delta is either fabricated (violating FR-002) or ambient (violating determinism).
Diffing the live process environment into the record — rejected for the same determinism reason (the F051
research D7 already rejected ambient-env diffing for identical reasons). Treating `CommandSpec.Command` as a
single executable with no arguments — rejected: real command lines (`dotnet test --no-build`) carry arguments
that are identity-significant (F032 D6), so they must be split into the ordered `Argument list`. Shelling out
via `/bin/sh -c "<command>"` — rejected: it would make identity depend on a shell and leak shell/locale
behavior into the run, defeating reproducibility and the operating rule's genericity. **A degenerate declared
command** (empty/all-whitespace `Command`) lexes to an empty executable; the F051 port reifies that as an
ordinary `startFailureExitCode` outcome (totality), so no special-casing is needed and the misconfiguration is
surfaced as a recorded failed gate rather than a crash.

## D2 — Recover a reusable gate's prior exit code by parsing the canonical-identity string in its stored `EvidenceRef`; recompute when it is not in canonical form

**Decision**: A `reusable` gate's prior pass/fail is recovered by **parsing the prior `ExitCode` out of the
gate's stored `EvidenceRef`**. `CacheEligibility.evaluateGate` returns `Reusable of EvidenceRef`; that
`EvidenceRef` is, by F049 `referenceOf`, the **F032 canonical-identity string** of the prior `CommandRecord`,
which embeds the exit code as the length-prefixed segment `exit=1<len>:<value>` (per
`specs/032-command-records/contracts/command-record-identity-format.md`). `priorExitOf: EvidenceRef ->
ExitCode option` splits the reference on its segment separator, finds the `exit=` segment, and parses the
integer. The reused gate's `GateOutcome` carries `Disposition = Reused`, that recovered `ExitCode`, and the
derived pass/fail. If the reference is **not** in canonical form (any parse miss — a future/foreign reference
shape), `priorExitOf` returns `None`; the gate is then **conservatively recomputed** (executed), never treated
as passed (FR-004, Edge: "prior outcome not recoverable ⇒ recompute").

**Rationale**: This is the pivotal feasibility question for US2, and the store **already carries enough** to
answer it with **no schema change** (FR-010, Out of Scope: no `fsgg.evidence-reuse-store/v1` change). The
`EvidenceRef` is **not a hash** — it is the full, reversible, length-prefixed identity string, and the exit
code is one of its segments, so the prior pass/fail is recoverable from the persisted `evidence` field alone.
F032's canonical-identity format is a *committed contract*, so parsing a single documented segment is reading a
contract, not reverse-engineering an opaque blob — but it is still a narrow, declared-format read, so it is
isolated in one small pure `GateRun.priorExitOf` function (the rest of the system keeps treating the reference
as opaque — FR-015). Failing safe to recompute on any non-canonical reference satisfies FR-004 and the spec's
"safe default — recompute when in doubt": a reused verdict is only ever produced from a recovered, real prior
outcome.

**Alternatives rejected**: Adding an `exitCode`/`verdict` field to the store schema — rejected: it bumps the
`fsgg.evidence-reuse-store/v1` schema (forbidden, FR-010 / Out of Scope) and is unnecessary because the datum
is already recoverable. Dereferencing the `EvidenceRef` against some external record artifact — rejected: no
such artifact exists, and the canonical identity is self-describing. Reusing a gate's **evidence** while
**re-running** it to get a fresh verdict — rejected: it defeats US2's entire purpose (no work saved) and the
spec is explicit that a reusable gate is **not** spawned a second time. Treating any `reusable` gate as a pass
without recovering the outcome — rejected: it would let the cache *invent* a passing verdict (a safety hole the
spec forbids in FR-004 and the "not recoverable ⇒ recompute" edge case).

## D3 — Gate the ship verdict by relocating PASSING command-gates to `Passing` after a verbatim `Ship.rollup`; no edit to F023/F024 and no new severity scheme

**Decision**: `fsgg ship` computes its decision in two pure steps that together feed real pass/fail into the
**existing** enforcement without touching a frozen core:

1. Call **`Ship.rollup route mode profile` verbatim** on the **full** selected-gate set + findings — exactly
   as today. This partitions every gate by its **existing** maturity-derived effective severity into
   `Blockers` / `Warnings` / `Passing`, and computes `Verdict` / `ExitCodeBasis`.
2. Apply one pure post-processing step keyed by the set of gate ids that this run **executed-or-reused and
   PASSED** (exit code `0`): **relocate** each such gate's `EnforcedItem` out of `Blockers`/`Warnings` and
   into `Passing`, then **recompute** `Verdict = if Blockers' empty then Pass else Fail` and `ExitCodeBasis`
   accordingly. A gate that **failed** (non-zero / timeout / start-failure exit) is **left exactly where
   `Ship.rollup` placed it** — a failing blocking-maturity gate stays a `Blocker`, a failing relaxed gate stays
   a `Warning`. A gate with **no declared command** is never in the passing-id set, so it **keeps its current
   rollup treatment** unchanged (FR-005). Findings are never moved.

**Rationale**: The repo confirms the constraint precisely — `Ship.rollup` is the **sole** public entry,
`gateToInput` derives `BaseSeverity` from `Gate.Maturity` **alone** (hidden in `Ship.fs`), and
`Enforcement.deriveEffectiveSeverity` takes **no** pass/fail parameter — so there is *no* public seam to feed an
outcome **into** the rollup, and FR-017 forbids editing `Ship.fs`/`Enforcement.fs`. The spec's own wording
fixes the semantics: a failing gate "becomes a blocker or a warning **at its existing effective severity**" and
the row "supplies the **real pass/fail** the enforcement rollup previously had no way to obtain" while
introducing "**no new** severity scheme." That is exactly "outcome gates *whether* a gate is counted at its
already-computed severity," **not** "outcome changes a gate's severity." Realizing it as a **relocation after a
verbatim rollup** is the minimal faithful mechanism: it reuses **every** F023/F024 severity decision unchanged
(the effective severity each gate carries is the one `deriveEffectiveSeverity` produced), it duplicates **no**
severity logic into the host (unlike rebuilding inputs and re-partitioning), and it only moves passing gates —
which by definition cannot *create* a blocker — so it can only ever *clear* blockers a passing gate would
otherwise have raised. The verdict re-derivation re-applies Ship's **own** one-line rule ("`Fail` iff a blocker
remains") to the relocated partition; it is the same rule, not a new one. This is the verdict change FR-009
explicitly permits ("any verdict change in `fsgg ship` that follows directly from a gate's real pass/fail
result"). A **reused** gate is in the passing-id set on identical terms (its recovered exit `0` ⇒ pass), so
FR-007 ("a reusable gate contributes its prior outcome on the same terms") falls out for free.

**Alternatives rejected**: Setting `BaseSeverity` from the outcome (pass ⇒ Advisory, fail ⇒ Blocking) and
calling `deriveEffectiveSeverity` — rejected: it **redefines** base severity (today a maturity property),
which *is* a new severity scheme (forbidden, FR-006) and contradicts "at its existing effective severity."
Filtering passing gates out of the `RouteResult` before `rollup` and rebuilding their `Passing` items in the
host — rejected: it forces the host to replicate `gateToInput`'s hidden maturity→severity map (duplication of a
frozen core's logic) and to reconstruct the `Passing` partition, more code and more drift risk than a
relocation. Editing `Ship.rollup` to accept a per-gate pass/fail — rejected: forbidden by FR-017 and
unnecessary given the relocation seam. Building a fresh `ShipDecision` from scratch in the host — rejected: it
would re-implement the whole partition/sort/verdict logic of a safety-critical frozen core.

## D4 — Wire execution into the commands' existing MVU loop: an injected `ExecutionPort` in `Ports`, an `ExecuteGates` effect, a `GatesExecuted` msg

**Decision**: Extend each command's established MVU boundary rather than adding a parallel I/O surface. The
`Ports` record (today `{ Files; Git; Freshness; Store; Write; Out }`) gains `Execute: ExecutionPort`;
`realPorts` wires it to the merged F051 `GateExecution.realPort`. A new pure `Effect` case `ExecuteGates of
(GateId * GateCommand) list` is requested by `update` for the selected **must-recompute command-gates**; the
`Interpreter` runs it by calling `GateExecution.senseExecution ports.Execute` once per command (in selected-gate
order) and feeds the assembled records back as a new `Msg` case `GatesExecuted of (GateId * CommandRecord)
list`. The pure `update`, on `GatesExecuted`, folds each record into the store via F049 `capture`, builds the
per-gate `GateOutcome`s (executed gates from the fresh records, reusable gates from `priorExitOf`, no-command
gates as `NotExecuted`), projects the documents with the execution embed, and emits the persist-grown-store
effect.

**Rationale**: Principle IV is **live** (this adds process I/O to a stateful workflow) and is met the way the
constitution sanctions for CLIs/tools: I/O is represented as data (the injected port + the `ExecuteGates`
effect), `update` is pure and starts no process, and interpretation happens only at the edge — the **same**
pattern the existing `SenseFreshness`/`LoadStore`/`PersistStore` effects already use in these very commands. It
keeps a single coherent loop, makes the new step uniformly testable (fake port for `update`, real port for the
interpreter), and adds no Elmish `Program` ceremony Principle III warns against.

**Alternatives rejected**: Running gates eagerly inside `update` — rejected: it makes `update` impure,
breaking the boundary and the pure-transition tests. A second, separate execution pipeline outside the loop —
rejected: it fragments the command into two control structures and duplicates the ports/threading the loop
already provides.

## D5 — Execution is sequenced after cache eligibility and before document projection; reuse decided per gate

**Decision**: The run order inside each command becomes: sense freshness + load store (unchanged) → resolve
freshness candidates + **evaluate cache eligibility** (F046, unchanged) → **classify each selected gate** →
`ExecuteGates` for the recompute set → on results, **capture + persist grown store** + project documents (+
ship verdict relocation). Per-gate classification: a gate is **reused** (not run) iff
`CacheEligibility.isReusable` **and** `priorExitOf` recovers its prior exit; **executed** iff it declares a
command and is `mustRecompute` (or is `reusable` but its prior exit is unrecoverable — D2); **not executed**
iff it declares no command.

**Rationale**: Eligibility must precede execution (the cache decides what to run), and capture/persist must
follow it (the store grows from the run). Inserting execution as a new phase between the existing
eligibility-evaluation join and the existing document-projection step is the smallest change that preserves
every existing step's place and the degrade-to-empty behavior on store failure (FR-013). Classifying reuse as
"`isReusable` ∧ recoverable" makes the "not recoverable ⇒ recompute" edge case fall out of the same branch.

**Alternatives rejected**: Executing all selected command-gates and ignoring the cache — rejected: it defeats
US2 (no work saved) and FR-003. Persisting the store before capture — rejected: it would persist the *ungrown*
store, the exact bug this row exists to fix.

## D6 — `route.json` / `audit.json` gain an additive, optional, default-empty per-gate `execution` embed (no schema bump)

**Decision**: `RouteJson.ofRouteResult` and `AuditJson.ofShipDecision` each take a **new optional parameter**
carrying the per-`GateId` `GateOutcome`s (e.g. an `(GateId * GateOutcome) list` or `Map`, empty by default).
When non-empty, each selected-gate entry gains an `execution` object — `{ disposition: "executed" | "reused" |
"notExecuted"; exitCode: <int>; passed: <bool> }` — placed beside the F045 `cacheEligibility` object and
matched by `GateId`. When empty, output is **byte-identical** to today. No `schemaVersion` is bumped.

**Rationale**: This mirrors **exactly** how F045 embedded `cacheEligibility` (an optional report, matched by
`GateId`, additive), so it is the established, low-risk extension point and keeps `RouteJson`/`AuditJson`'s own
golden baselines byte-stable (their tests pass the empty default), satisfying FR-009 + FR-017's "no re-bless
beyond the route/ship command tests." The additive optional field is additive JSON, not a schema redefinition —
the same reading under which F045's embed bumped no schema version.

**Alternatives rejected**: A new top-level execution section — rejected: the spec wants the outcome **per
selected-gate entry** (FR-008, FR-009) beside the cache verdict, so a per-gate embed is correct. A new
`schemaVersion` — rejected: forbidden (FR-017) and unwarranted for an additive field (F045 precedent). New
`ofRouteResultWithExecution`/`ofShipDecisionWithExecution` functions — rejected: needless surface duplication;
an optional parameter on the existing function with a default keeps one emitter and one golden path.

## D7 — `GateRun` is a small new pure library, free of any `Ship`/host dependency; the ship-only relocation lives in `ShipCommand`

**Decision**: The shared, command-agnostic pieces — `GateDisposition`/`GateOutcome` (Model) and
`lexCommandLine`/`commandFor`/`priorExitOf`/`passed` (Plan) — live in a new pure library
`FS.GG.Governance.GateRun` referenced by both commands. The ship verdict **relocation** (D3), which needs
`Ship.Model`/`Enforcement`, lives in `ShipCommand` so `GateRun` carries **no** `Ship` dependency (route must
not transitively gain one).

**Rationale**: Both commands share the derivation/recovery/vocabulary; one shared library is the repo idiom and
avoids duplicating it across two `Loop.fs`. Keeping `GateRun` `Ship`-free preserves the clean layering (route is
advisory and has no business depending on the ship enforcement core) and matches the constitution's
"heavier capabilities layer on top, not into the core."

**Alternatives rejected**: Inlining the helpers into each command — rejected: duplication across two files and
two test suites. Putting the relocation in `GateRun` — rejected: it would drag `Ship`/`Enforcement` onto
route's graph for no reason.

## D8 — Tests: fake `ExecutionPort` + real temp-script fixtures + a writable temp store; documents recomputed live

**Decision**: The command tests drive execution through a **deterministic fake `ExecutionPort`** (literal
`byte[]` + chosen `ExitCode`/`SensedDuration`) over **real `/bin/sh` temp-script fixtures** for the edge cases
(exit 0, exit non-zero, missing executable, sleep-past-timeout), against a **writable temp reuse store**. The
two-run reuse test runs the same command twice over the same fixture state and asserts the second run reports
`reusable`, **spawns no process** (the fake port records its call count), and reuses the prior outcome. The
ship tests assert the verdict + exit code track real pass/fail (a non-zero blocking gate ⇒ `Fail`/exit 1; the
same gate exiting `0` ⇒ not a blocker). `GateRun.Tests` covers the three pure helpers, including a `priorExitOf`
**round-trip** against a **real** `EvidenceCapture.referenceOf` of a `senseExecution` record. Command tests
**recompute** their expected `route.json`/`audit.json` live; digests derive from **real captured bytes**.

**Rationale**: Principle IV/V — both sides tested, real evidence preferred, no `Synthetic` outcomes; the
fake-port-over-real-scripts split is F051's exact, proven discipline (deterministic control for the pure
transitions, real processes for the edge), reaching no network and no governed repository (SC-007). The
spawn-count assertion is the only honest proof that reuse **skips** execution (US2).

**Alternatives rejected**: A real port everywhere — rejected: non-deterministic durations make document
recomputation and identity assertions flaky. Mocking the store on disk — rejected: a real temp file is the
honest, cheap test of the grow-and-persist round-trip.

## D9 — Packable like the thread siblings; surface baselines for every changed public module

**Decision**: `FS.GG.Governance.GateRun` is `IsPackable=true`, `PackageId=FS.GG.Governance.GateRun`,
`Version=0.1.0`, matching the F047/F049/F050/F051 thread siblings, and ships a reflective surface baseline.
`RouteJson`/`AuditJson`/`RouteCommand`/`ShipCommand` baselines are updated for their additive surface changes.

**Rationale**: The evidence-reuse thread ships each library as an independently packable unit; `GateRun`
continues that. Every Tier-1 surface change updates its baseline (Principle II / Change Classification) —
the new library's baseline is generated via `BLESS_SURFACE`, the edited modules' baselines re-blessed for the
single new field/parameter each gains.

**Alternatives rejected**: `IsPackable=false` — defensible (the `FreshnessSensing` edge chose it) but diverges
from the three thread siblings this row directly continues; a trivial one-flag reversal if the maintainer
prefers the edge convention.
