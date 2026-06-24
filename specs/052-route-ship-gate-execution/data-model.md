# Phase 1 Data Model: Execute Selected Gates In `fsgg route` / `fsgg ship`

This row introduces a tiny new vocabulary (`GateDisposition`, `GateOutcome`) and four pure functions — the three
genuinely-new pieces (`lexCommandLine`, `commandFor`, `priorExitOf`) plus the trivial `passed` mapping — and wires
them — with the merged F051/F050/F049/
F046/F047 cores — into the two host commands' existing MVU loops. It introduces **no** persisted schema and
**no** new identity/digest/severity scheme. The full new surface is in [contracts/Model.fsi](./contracts/Model.fsi)
and [contracts/Plan.fsi](./contracts/Plan.fsi); the host-seam deltas in [contracts/host-wiring.md](./contracts/host-wiring.md).

## New entities (`FS.GG.Governance.GateRun`)

### `GateDisposition` — how one selected gate was handled this run

```fsharp
type GateDisposition =
    | Executed      // declared a command, was run this run (mustRecompute, or reusable-but-unrecoverable)
    | Reused        // declared a command, prior captured outcome reused (NOT spawned this run)
    | NotExecuted   // declared no command; not run (keeps current rollup treatment)
```

### `GateOutcome` — the per-gate execution result attached to the document (and, in ship, the verdict)

| Field | Type | Notes |
|-------|------|-------|
| `GateId` | `GateId` (F018) | the selected gate this outcome belongs to; matches the document entry by id |
| `Disposition` | `GateDisposition` | executed / reused / not-executed |
| `ExitCode` | `ExitCode option` (F032) | the real exit (executed) or recovered prior exit (reused); `None` only for `NotExecuted` |
| `Passed` | `bool option` | `Some (passed exit)` — exit `0` ⇒ `true`, any non-zero/sentinel ⇒ `false`; `None` only for `NotExecuted` |

**Validation**: none added — `GateId`/`ExitCode` are already-validated F018/F032 values. `NotExecuted` is the
sole disposition with `None` exit/pass; `Executed`/`Reused` always carry a `Some` exit (an `Executed` gate's
exit is the F051 outcome's, possibly a sentinel; a `Reused` gate's is the `priorExitOf` recovery, which is
`Some` by construction since an unrecoverable reuse is reclassified `Executed` — see the flow below).

## New pure functions (`FS.GG.Governance.GateRun.Plan`)

### `lexCommandLine: string -> (Executable * Argument list) option`

A POSIX-style argv split of a declared command line. Whitespace separates tokens; single quotes, double
quotes, and backslash escapes group/quote; the first token is the `Executable`, the rest the ordered
`Argument list`. No shell features (no globbing, expansion, pipes, redirection). `None` for an empty /
all-whitespace line (a degenerate declared command — D1).

### `commandFor: repoRoot: string -> tooling: ToolingFacts -> gate: Gate -> GateCommand option`

Resolves the gate's `RequiresCommand` `CommandId` against `tooling.Commands`, `lexCommandLine`s the
`CommandSpec.Command`, and assembles a `GateCommand`:

| `GateCommand` field | Source |
|---------------------|--------|
| `Executable`, `Arguments` | `lexCommandLine CommandSpec.Command` (ordered; D1) |
| `WorkingDirectory` | `repoRoot` (the governed root) |
| `Environment` | empty `EnvironmentDelta` (`{ Added=[]; Changed=[]; Removed=[] }`) — declared `EnvironmentClass` is not an env mutation (D1, FR-002) |
| `Timeout` | `CommandSpec.Timeout` (verbatim; F018 default already applied) |
| `CapturedOutput` | `NoCapturedOutput` |

Returns `None` when the gate has no `RequiresCommand` prerequisite, the `CommandId` resolves to no
`CommandSpec`, or the command line lexes to nothing (⇒ `NotExecuted`, FR-005).

### `priorExitOf: EvidenceRef -> ExitCode option`

Parses the prior `ExitCode` out of a reusable gate's stored `EvidenceRef` — the F032 canonical-identity string
(F049 `referenceOf`), which embeds `exit=1<len>:<value>` (per
`specs/032-command-records/contracts/command-record-identity-format.md`). `None` when the reference is not in
canonical form ⇒ the gate is conservatively recomputed (D2, FR-004). The rest of the system keeps treating the
reference as opaque; this is the single declared-format read (FR-015).

### `passed: ExitCode -> bool`

`ExitCode 0` ⇒ `true`; any non-zero (including the F051 `startFailureExitCode` / `timeoutExitCode` sentinels)
⇒ `false` (FR-006).

## Per-gate classification (the run decision)

For each selected gate, after F046 cache eligibility is evaluated:

```text
commandFor gate = None
  └─► NotExecuted                         (no declared command — keep current treatment, FR-005)

commandFor gate = Some cmd
  ├─ isReusable verdict ∧ priorExitOf ref = Some exit
  │     └─► Reused   (exit, passed exit)  (NOT spawned — the cache payoff, US2/FR-003)
  └─ otherwise (mustRecompute, OR reusable-but-priorExitOf = None)
        └─► Executed via ExecuteGates      (spawned once — D2 recompute-when-unrecoverable, FR-004)
```

`ref` is the `EvidenceRef` from `CacheEligibility.evaluateGate`'s `Reusable` arm. Only the **Executed** set is
sent to the `ExecuteGates` effect; `Reused` and `NotExecuted` gates spawn nothing.

## Host-command flow (both commands; ship adds the verdict step)

```text
parse → sense scope → load catalog → route + select gates        (unchanged)
      → sense freshness + load reuse store (read-only)            (unchanged; degrade-to-empty on failure, FR-013)
      → resolve freshness candidates + CacheEligibility.evaluate  (unchanged, F046)
      → classify each selected gate (above)                       (NEW)
      → ExecuteGates [Executed gates] → senseExecution port each  (NEW; F051, interpreter edge, once per gate FR-001)
      → on GatesExecuted:                                          (NEW)
          • EvidenceCapture.capture <freshness inputs> <record> per executed gate   (F049, grows the store)
          • build GateOutcome per selected gate (Executed / Reused / NotExecuted)
          • project route.json / audit.json WITH the execution embed (D6)
          • ship only: relocate PASSING command-gates → Passing, recompute verdict/exit (D3)
          • PersistStore: prune → retain defaultRetentionBound → serialise → write   (F047/F048, persists the GROWN store, FR-010)
      → write artifacts → emit summary (reflecting executed/reused/passed/failed + any store failure, FR-016)
      → route: always exit 0 (FR-008);  ship: exit from the relocated ExitCodeBasis
```

### Ship verdict relocation (D3) — the only verdict change

```text
decision  = Ship.rollup route mode profile           // VERBATIM — every gate partitioned by existing effective severity
passedIds = { o.GateId | o ∈ outcomes, o.Passed = Some true }   // executed-or-reused AND exit 0
blockers' = decision.Blockers |> reject (gate ∈ passedIds)      // a PASSING gate cannot remain a blocker
warnings' = decision.Warnings |> reject (gate ∈ passedIds)
passing'  = decision.Passing  ++ (moved-out gate items)         // relocated, not rebuilt
verdict'  = if blockers' empty then Pass else Fail              // Ship's OWN rule, re-applied
exit'     = if verdict' = Pass then Clean else Blocked
```

A **failing** command-gate is never in `passedIds`, so it stays exactly where `rollup` placed it (a failing
blocking-maturity gate stays a `Blocker` ⇒ `Fail`). A **no-command** gate is never in `passedIds`, so it keeps
its current treatment (FR-005). Findings are never relocated. The relocation can only *clear* blockers a passing
gate would have raised — never create one (FR-006).

## Documents (additive embed, D6)

Each selected-gate entry in `route.json` / `audit.json` gains, beside the F045 `cacheEligibility` object, an
`execution` object matched by `GateId`:

```json
"execution": { "disposition": "executed" | "reused" | "notExecuted",
               "exitCode": <int>, "passed": <bool> }
```

`exitCode`/`passed` are omitted for `notExecuted` (no run, no exit). When no outcomes are supplied (the
emitter's default), no `execution` object is written — output is byte-identical to today (FR-009).

## Reproducible identity & the closed loop (FR-014, US5)

`senseExecution`'s `CommandRecord` has, by F050/F032 construction, a `canonicalId` that is a function only of
the reproducible facts (the declared command-to-run from `commandFor` + the captured bytes), with the measured
`SensedDuration` excluded. Because `commandFor` derives those facts deterministically (D1: repo-root cwd, empty
env delta, declared timeout) and the F051 port leaks no clock/pid/ambient-env, two runs of the same
deterministic gate over the same world assemble a byte-identical `canonicalId` ⇒ a byte-identical
`EvidenceRef` ⇒ the next run's freshness world matches the captured reference ⇒ the gate is marked `reusable`
and **reused**. The loop closes: run → record → `referenceOf` → `capture` → persist grown store → (next run)
match → reuse.

## What this row does NOT add

No new persisted schema or `schemaVersion` bump; no new identity/digest scheme (reuses F050/F032); no new
severity/mode/profile/enforcement rule (reuses F023/F024 verbatim, only relocating passing gates); no new
success/exit-code policy beyond `passed`'s exit-`0`-is-pass mapping; no captured-output file subsystem; no
sandboxing beyond the F051 timeout; no parallel/retry execution; and no edit to any frozen F023/F024/F030/F032/
F041–F051 core, the F045 embed, the `fsgg.evidence-reuse-store/v1` schema, or the
`command-record-identity-format` contract.
