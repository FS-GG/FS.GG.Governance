# Data Model: Bound the scaffold real-evidence build test

The "data" here is the small set of test-support values and the outcome type that classify
a real-evidence build attempt. All of it lives in
`tests/FS.GG.Governance.Sample.SddReferenceProvider.Tests/Support.fs` (test-support; no
production surface, no `.fsi`, no surface baseline — Tier 2).

---

## 1. `BuildAttempt` (the build outcome) — CHANGED

A total, three-case discriminated union classifying one attempt to build the emitted
skeleton. The third case is **new**.

```fsharp
type BuildAttempt =
    /// The SDK ran and returned within budget: real exit code + captured (stdout+stderr) output.
    | Built of exitCode: int * output: string
    /// `dotnet` is absent / would not start on this machine ⇒ a NAMED prerequisite skip (existing).
    | SdkMissing of detail: string
    /// The build did not return within the finite budget; its process tree was terminated ⇒ a NAMED
    /// timeout skip carrying the budget exceeded and whatever partial output was captured (NEW).
    | TimedOut of budget: TimeSpan * partialOutput: string
```

| Case | Trigger | Maps to (in the build test) | Requirements |
|---|---|---|---|
| `Built(0, _)` | SDK present, build returns exit 0 within budget | **PASS** | FR-003, SC-003 |
| `Built(<>0, out)` | SDK present, build returns non-zero within budget | **FAIL** (with `out`) | FR-003, SC-003 |
| `SdkMissing detail` | `Process.Start` throws / returns null (`dotnet` not on PATH) | **named skip** (missing SDK) | FR-004 |
| `TimedOut(budget, partial)` | `WaitForExit(budget)` returns false ⇒ tree killed | **named skip** (timeout) | FR-001, FR-002, SC-001, SC-004 |

**Validation / invariants**
- The three cases are mutually exclusive and exhaustive; the build test pattern-matches all
  three so a timeout can never silently become a pass (FR-009).
- `TimedOut.budget` equals the `buildBudget` used for the attempt (deterministic, FR-010).
- A non-zero `Built` is never rewritten to a skip (FR-003 / SC-003).

---

## 2. Time budget — NEW

```fsharp
/// The finite ceiling a real-evidence build may consume before it is cut off as TimedOut.
/// Default 120 s; overridable via FSGG_BUILD_BUDGET_SECONDS (garbage/absent ⇒ the 120 s default).
let buildBudget : TimeSpan
```

| Field | Value | Notes |
|---|---|---|
| default | `TimeSpan.FromSeconds 120.` | research D5; ample for a warm-cache tiny-skeleton build |
| override | env `FSGG_BUILD_BUDGET_SECONDS` | parsed to seconds; non-numeric/absent ⇒ default |
| invariant | always finite & > 0 | FR-001; a malformed override never yields an unbounded wait |

Two **named margin constants** disambiguate the two distinct "small margin" notions (so the
timing assertion is reproducible, FR-010):

| Constant | Value | Role |
|---|---|---|
| `killDrainMargin` | `TimeSpan.FromSeconds 5.` | bounded post-`Kill` drain wait in `runBounded` so reading the partial output after a tree-kill cannot itself block |
| `boundAssertionMargin` | `TimeSpan.FromSeconds 2.` | assertion tolerance the forced-stall test allows over `budget` (distinct from the drain margin) |

---

## 3. Run configuration — NEW

Whether the current run exercises the heavyweight real-evidence build.

```fsharp
/// True when the real-evidence build should run: FSGG_REAL_EVIDENCE=1 OR a truthy CI env var.
let realEvidenceEnabled : unit -> bool
/// The NAMED opt-out skip message used when realEvidenceEnabled () is false.
let realEvidenceSkipReason : string
```

| State | Determined by | Build test behavior |
|---|---|---|
| **Fast default** | neither `FSGG_REAL_EVIDENCE=1` nor `CI` truthy | named opt-out skip; no scaffold, no build (SC-002) |
| **Real evidence** | `FSGG_REAL_EVIDENCE=1` **or** `CI` truthy | scaffold + bounded real `dotnet build` (FR-005) |

---

## 4. Bounded process primitive — NEW (internal helper)

```fsharp
/// Run `exe args` (optionally in workingDir), capturing stdout+stderr asynchronously, waiting at most
/// `budget`. On overrun, kill the whole process tree and return TimedOut. A start failure ⇒ SdkMissing.
/// `onStarted` is invoked synchronously (caller's thread) with the spawned PID right after Start.
let runBounded :
    exe:string -> args:string -> workingDir:string option -> budget:TimeSpan -> onStarted:(int -> unit) -> BuildAttempt
```

> **Signature deviation (recorded).** The planned 4-arg `runBounded` gained a fifth
> parameter `onStarted : int -> unit` during implementation. The forced-stall test's
> assertion (c) ("the spawned process is no longer alive") was first written as a scan of the
> OS process table *by name* (`sleep`/`ping`); under the parallel `dotnet test <solution>`
> run that proved **non-deterministic** — an unrelated concurrent sleeper is misread as a
> surviving orphan, failing the test (observed once in the full-suite run). The only race-free
> death check is on the EXACT spawned PID, so `runBounded` now reports it synchronously via
> `onStarted` (production caller `dotnetBuild` passes `ignore`). This strengthens FR-010
> (determinism) and is confined to test-support (no `.fsi`, no surface baseline). (research D9)

`dotnetBuild` (CHANGED) becomes a thin wrapper:
`runBounded "dotnet" "build \"<sln>\" -maxcpucount:1 --disable-build-servers" (Some <dir>) buildBudget ignore`
(research D6/D7). The fan-out-bounding flags and tree-kill are what keep a *running* build
from wedging the surrounding suite (FR-007).

---

## 5. State transitions (one build attempt)

```text
realEvidenceEnabled () = false ──────────────────────────────► [named opt-out skip]   (US2, SC-002)

realEvidenceEnabled () = true
        │
        ▼
   runBounded "dotnet" …
        │
        ├─ Process.Start throws/null ───────────────────────► SdkMissing ─► [named missing-SDK skip] (FR-004)
        │
        ├─ WaitForExit(budget) = false ─► Kill(entireTree) ─► TimedOut  ─► [named timeout skip]      (US1, SC-001)
        │
        └─ WaitForExit(budget) = true  ─► read exit code ──► Built(code,out)
                                                              ├─ code = 0  ─► [PASS]   (US3, SC-003)
                                                              └─ code ≠ 0  ─► [FAIL]   (US3, SC-003)
```

The two non-build worked-example tests (scaffold-correctness, golden/determinism) are
outside this model and are unchanged (FR-006, FR-008).
