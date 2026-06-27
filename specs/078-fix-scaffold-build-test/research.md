# Research: Bound the scaffold real-evidence build test

Phase 0 design decisions. There are no open `NEEDS CLARIFICATION` items — the spec's
Assumptions section fixes the defaults (fast-default + opt-in real evidence; ~120 s
budget) and explicitly licenses the plan to choose the mechanics. Each decision below is
**Decision / Rationale / Alternatives considered**.

---

## D1 — Defeat the `ReadToEnd()` deadlock before any timeout can fire

**Decision**: Replace the synchronous `StandardOutput.ReadToEnd()` /
`StandardError.ReadToEnd()` + bare `WaitForExit()` with **asynchronous** capture:
attach `OutputDataReceived` / `ErrorDataReceived` handlers that append to two
`StringBuilder`s, call `BeginOutputReadLine()` / `BeginErrorReadLine()` after
`Process.Start`, then wait with the bounded overload `WaitForExit(int milliseconds)`.

**Rationale**: The current code's first pathology is subtle: `ReadToEnd()` *itself*
blocks until the child closes its stdout — which a hung build never does — so execution
never reaches `WaitForExit()`. Simply adding a timeout argument to `WaitForExit` would be
dead code; the hang is in the read. The async event pattern is the canonical .NET way to
drain both pipes without deadlock while still being able to time out the wait. After
`WaitForExit(ms)` returns `true`, call the no-arg `WaitForExit()` once to flush any
in-flight async output before reading the builders (documented .NET requirement).

**Alternatives considered**:
- *Background `Task.Run` per stream + `Task.WaitAll(timeout)`*: equivalent but more moving
  parts than the built-in event hooks; rejected for simplicity (Principle III).
- *Keep `ReadToEnd()`, add a watchdog thread that kills on timeout*: works but reintroduces
  thread-management complexity the event pattern avoids; rejected.

---

## D2 — Terminate the whole process tree on timeout

**Decision**: On timeout, call `proc.Kill(entireProcessTree = true)` then
`proc.WaitForExit(<small margin>)` to drain, and classify the attempt as `TimedOut`.

**Rationale**: `dotnet build` spawns MSBuild worker nodes and a restore child; killing
only the parent leaves orphans that keep consuming CPU/handles (the spec's "90-minute
orphan" edge case). `Process.Kill(true)` (net5+, available on `net10.0`) recursively
terminates descendants. This directly satisfies FR-002 ("terminate the build subprocess
**and all of its descendant processes**") and the orphan edge case.

**Alternatives considered**:
- *`Kill()` (parent only)*: leaves the worker fan-out alive — exactly the resource
  pathology FR-007 calls out; rejected.
- *Posix process-group signalling*: not portable, and `Kill(true)` already does it
  cross-platform; rejected.

---

## D3 — New `TimedOut` outcome, distinct from `SdkMissing` and `Built`

**Decision**: Extend the test-support DU to
`BuildAttempt = Built of exitCode:int * output:string | SdkMissing of detail:string |
TimedOut of budget:TimeSpan * partialOutput:string`. The build test maps `TimedOut` to a
named `skiptestf` ("BUDGET EXCEEDED: dotnet build exceeded <budget>; partial output: …"),
kept textually distinct from the existing missing-SDK `skiptestf` and from a genuine
`Built`-with-nonzero failure.

**Rationale**: FR-002/FR-004/FR-009 require the timeout skip to be a *named* outcome,
observably different from "passed", from the missing-SDK skip, and from a real failure.
A third DU case makes the three outcomes total and pattern-matchable, so the test cannot
silently collapse a timeout into a pass. Carrying the budget + partial output makes the
skip actionable. This lives in test `Support` only — no production surface (Tier 2).

**Alternatives considered**:
- *Reuse `SdkMissing` for timeouts*: would conflate two distinct conditions and violate
  FR-004's distinctness requirement; rejected.
- *Throw on timeout*: an exception reads as a failure, not a skip — would convert a
  contended-build timeout into a red suite (the opposite of US1); rejected.

---

## D4 — Default fast, real evidence behind an explicit opt-in (and CI)

**Decision**: Gate the heavyweight `dotnet build` behind a test-harness opt-in. The build
runs when **either** `FSGG_REAL_EVIDENCE=1` is set (explicit local opt-in) **or** the
`CI` environment variable is truthy (the canonical full-evidence path, FR-005). Otherwise
the build test emits a *named* opt-out skip ("REAL-EVIDENCE OPT-OUT: set FSGG_REAL_EVIDENCE=1
(or run under CI) to exercise the real `dotnet build`…") and returns immediately — it does
not even scaffold, keeping the default project run fast (SC-002). A small
`Support.realEvidenceEnabled : unit -> bool` and `Support.realEvidenceSkipReason : string`
encapsulate the decision.

**Rationale**: This is the spec's assumed default (Assumptions §"Default = fast, opt-in =
real evidence") and satisfies both US2 (fast default) and FR-005 (real build still runs on
the authoritative CI path). Honoring `CI` (a near-universal convention) means no extra CI
wiring is needed for the 072 guarantee to keep being exercised. Two tiny env-reading
helpers keep the gate in one place and testable.

**Alternatives considered**:
- *Always-on but strictly time-bounded* (the spec's rejected alternative): satisfies US1
  and US3 but still pays minutes on every routine run, failing US2/SC-002; rejected as the
  default. The bounding work (D1–D2) still makes this lane safe when opted in.
- *A custom xUnit/Expecto trait/category filter instead of an env var*: heavier and less
  discoverable than an env var that mirrors the repo's existing `BLESS_*` env convention;
  rejected.
- *Gate on `CI` only (no local opt-in)*: a developer could never reproduce the real-evidence
  failure locally; rejected — FR-009 wants the lane discoverable.

---

## D5 — Finite, enforced budget with a documented default and an optional override

**Decision**: A single `Support.buildBudget : TimeSpan` constant, defaulting to **120 s**,
overridable via `FSGG_BUILD_BUDGET_SECONDS` (parsed; falls back to 120 on absent/garbage).
`dotnetBuild` passes this budget to the bounded runner.

**Rationale**: The spec's assumption is ~120 s — a from-scratch restore + compile of the
tiny skeleton on a warm cache finishes well within this, so a real build rarely trips the
budget, while a genuine stall is cut off (SC-001). Making it a named constant keeps the
"finite and enforced" requirement (FR-001) obvious; the env override lets a slow cold-cache
CI agent raise it without code changes. The default-on-garbage behavior keeps the
classification deterministic (FR-010): a malformed override never produces an unbounded
wait.

**Alternatives considered**:
- *Hard-coded literal with no override*: simplest, but a cold first CI run on a slow agent
  could false-timeout; the override is cheap insurance. Kept the constant default + opt-in
  override.

---

## D6 — Bound MSBuild fan-out to kill the resource-contention pathology

**Decision**: Invoke `dotnet build "<sln>" -maxcpucount:1 --disable-build-servers`
(working directory = the solution's directory, as today).

**Rationale**: Running `dotnet build` *inside* `dotnet test <solution>` otherwise spawns a
fan of MSBuild worker nodes (and persistent build-server processes) that contend with the
parent run for CPU/handles — the `Resource temporarily unavailable` / fork-exhaustion seen
in 077 (FR-007). `-maxcpucount:1` collapses the worker fan-out to a single node;
`--disable-build-servers` (valid `dotnet build` option since .NET 7) stops persistent
VBCSCompiler/MSBuild-server processes from being spawned/left behind, which — combined with
the `Kill(true)` tree-termination (D2) — guarantees no lingering workers after a skip.
These flags slow a *single* tiny-skeleton build only marginally and keep it within budget.

**Alternatives considered**:
- *`-nodeReuse:false` alone*: stops node reuse but not the per-build server processes;
  `--disable-build-servers` is the more complete knob. Kept the pair above.
- *Leave flags default and rely only on the tree-kill*: the contention still happens during
  a *successful* nested build (FR-007 is about the running build, not just timeouts);
  rejected.

---

## D7 — Refactor `dotnetBuild` over a reusable bounded primitive (enables a real US1 test)

**Decision**: Extract a `Support.runBounded (exe:string) (args:string)
(workingDir:string option) (budget:TimeSpan) : BuildAttempt` that owns D1/D2's async
capture + bounded wait + tree-kill. `dotnetBuild slnPath` becomes a thin call:
`runBounded "dotnet" (sprintf "build \"%s\" -maxcpucount:1 --disable-build-servers" sln)
(Some dir) buildBudget`.

**Rationale**: FR-010 wants the bounding mechanism deterministic and self-contained, and
US1's Independent Test wants a "forced-stall condition" proving the bound *without* a real
hanging `dotnet build`. Exposing the primitive lets a fast test (D9) run a real sleeper
process under a sub-second budget and assert it is cut off — real evidence of the bound,
reproducible, with no network/SDK dependence. The same primitive carries the missing-SDK
detection (Win32Exception/InvalidOperationException → `SdkMissing`), preserving FR-004.

**Alternatives considered**:
- *Keep the timeout logic inline in `dotnetBuild`*: then the only way to test the bound is a
  real hanging build — non-deterministic and slow; rejected.

---

## D8 — Golden + sibling-test stability (no re-bless, no surface delta)

**Decision**: Touch only `Support.fs`, the *build* test in `WorkedExampleTests.fs`, and the
new `BoundedBuildTests.fs` (+ the `.fsproj` `<Compile>` line). The scaffold-correctness test
and the golden/determinism test in `WorkedExampleTests.fs` are left byte-identical; the
committed manifest golden is not regenerated.

**Rationale**: FR-008/SC-005 forbid golden or sibling-behavior drift. The two non-build
tests don't shell out a build, so the change cannot reach them. Test assemblies are not
reflected by any surface baseline (the project's own `SurfaceDriftTests` reflects only the
three *production* assemblies and asserts the core baselines are byte-identical), so there
is no surface-baseline edit — consistent with Tier 2.

**Alternatives considered**: none — this is a constraint, not a choice.

---

## D9 — A fast, deterministic forced-stall test for the bound (US1 / SC-001)

**Decision**: Add `BoundedBuildTests.fs` with a test that runs `runBounded` against a real
**sleeper** process under a ~1 s budget, asserting (a) the outcome is `TimedOut budget`,
(b) the call returns within `budget + margin` (a `Stopwatch` check), and (c) the spawned
process is no longer alive. The sleeper command is OS-selected
(`sleep <n>` on Unix; `timeout`/`ping`-based wait on Windows); on an unsupported platform
the test emits a *named* `skiptest` ("PLATFORM: no sleeper available to force a stall") —
never a silent green.

**Rationale**: This is US1's Independent Test made real and cheap: a genuine process tree
is genuinely killed within the budget, proving SC-001 in ~1–2 s without a real hanging
`dotnet build` and without network/SDK reliance (FR-010). It fails before D1/D2 exist
(today's `dotnetBuild` would hang) and passes after — satisfying Principle V's
fail-before/pass-after requirement for the bound itself.

**Alternatives considered**:
- *Force the stall with a real `dotnet build` of a project that never finishes*: slow,
  flaky, and SDK-dependent — defeats the point; rejected.
- *Mock the process*: synthetic evidence where real is cheap and available; rejected
  (Principle V prefers the real bounded kill).

---

## D10 — Distinct, deterministic classification across all not-run paths (FR-009/FR-010)

**Decision**: The build test resolves to exactly one of four observable results, in this
order: (1) **opt-out skip** if `realEvidenceEnabled ()` is false; else after the build:
(2) **missing-SDK skip** (`SdkMissing`), (3) **timeout skip** (`TimedOut`), or (4) the real
**pass/fail** (`Built` exit 0 / non-zero). Each skip carries a distinct named message. None
of the decisions depends on wall-clock formatting or network state — only on the env gate,
the process-start outcome, the fixed budget, and the build's own exit code.

**Rationale**: FR-009 requires a reader to always tell "passed" from "skipped — because X"
from "failed", and FR-010 requires the classification to be reproducible. A total,
ordered match over the gate + `BuildAttempt` cases delivers both, and guarantees a genuine
first-attempt failure (case 4, non-zero) is never absorbed by a skip path (FR-003 / SC-003).

**Alternatives considered**: none — this is the direct encoding of FR-003/004/009/010.

---

## Cross-project parity (recorded, deferred)

The spec notes the CLI `dotnet pack` packaging test and the release `RealPackTests` share
the same heavyweight/contended shape. This feature fixes the worst offender only; promoting
`runBounded` + the opt-in gate into `FS.GG.Governance.Tests.Common` and applying it to those
suites is an explicitly-deferred, bounded follow-up (constitution: deferrals must be
explicit and scoped). Not in scope here.
