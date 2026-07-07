# Contract: `fsgg ship --dry-run`

## CLI surface

```
fsgg ship --dry-run [--repo <dir>] [--since <rev> | --paths <p>…] [--mode <mode>] [--profile <name>] [--json | --plain]
```

- `--dry-run` — **boolean flag** (no value). Turns on the simulated gate: no gate command is
  executed, nothing is written to `readiness/`, and the store is not persisted.
- All existing ship flags remain valid and compose (`--repo`, `--since`/`--paths`, `--mode`,
  `--profile`, `--json`, `--plain`). Recognized inside `parse`; a typo'd flag is a `UsageError`
  and writes nothing.
- **Exit status**: a dry-run is a *preview* — it completes with **exit 0** by default and carries
  the simulated verdict in its output (not the process exit code). (An opt-in "exit reflects the
  simulated verdict" lever is out of scope for this feature; see spec Assumptions.)

## Behaviour contract

| Aspect | Real `fsgg ship` | `fsgg ship --dry-run` |
|---|---|---|
| Gate commands | executed via `ExecutionPort` | **not executed** — every selected gate is `NotExecuted` |
| `readiness/audit.json` | written | **not written** |
| store persistence | on `--persist-store` | **never** persisted |
| stdout | verdict summary (text/json) | **simulated** verdict + **sufficiency** breakdown, marked |
| working tree after run | may write artifacts | **unchanged** (zero writes) |
| real `audit.json` schema | `fsgg.audit/v2` | not emitted; simulated uses `fsgg.audit.dryrun/v1` |

## Simulated JSON document (`--dry-run --json`)

Schema id **`fsgg.audit.dryrun/v1`** — distinct from the real `fsgg.audit/v2` so it can never be
consumed as a genuine gate result. Structurally recognizable to audit readers; adds `simulated`
and `sufficiency`.

```jsonc
{
  "schemaVersion": "fsgg.audit.dryrun/v1",   // distinct — NOT fsgg.audit/v2
  "simulated": true,                          // explicit marker (FR-006)
  "verdict": "pass" | "fail",                 // from Ship.rollup (simulated)
  "exitCodeBasis": "clean" | "blocked",
  "blockers":  [ /* EnforcedItem, same shape as real audit */ ],
  "warnings":  [ /* … */ ],
  "passing":   [ /* … */ ],
  "sufficiency": {
    "requiredAbsentCount": 0,                 // > 0 ⇒ handoff insufficient
    "allNotEvaluated": false,                 // true ⇒ the all-absent (Audio) failure mode
    "gates": [
      { "gateId": "<id>", "class": "requiredSatisfied" | "requiredAbsent" | "notRequired" }
    ]
  },
  "handoffDiagnostics": [
    { "cause": "malformed" | "versionMismatch" | "staleEvidence" | "autoSyntheticDeclared",
      "source": "<path>", "message": "<text>" }
  ]
}
```

**Guarantees**
- **G1** `schemaVersion` is exactly `fsgg.audit.dryrun/v1`; the string `fsgg.audit/v2` never
  appears in dry-run output.
- **G2** `simulated` is always present and `true`.
- **G3** Deterministic: identical inputs ⇒ byte-identical document (fixed key order).
- **G4** When `allNotEvaluated` is `true`, the output must make the absence visible; a Pass with
  empty `blockers` is not emitted as an unqualified clean result.
- **G5** The real `AuditJson.ofShipDecision` (`fsgg.audit/v2`) output is byte-identical to
  pre-feature — dry-run adds a *separate* projection, it does not alter the existing one.

## Simulated text document (`--dry-run` default / `--plain`)

- Leads with a prominent banner: `SIMULATED (dry-run) — not a real gate result`.
- Renders the reused `HumanText` verdict view, then a **Sufficiency** section listing
  required-absent gates first (the actionable gaps), then required-satisfied, then not-required.
- When `allNotEvaluated`, states plainly that nothing was evaluable (handoff carried no required
  signal) rather than showing a bare Pass.
- ANSI-free and deterministic (the repo's plain-text convention).

## Non-goals (this contract)

- No change to what the **real** gate enforces, to the `governance-handoff` contract, or to how
  the runtime is installed.
- No detached "evaluate an arbitrary handoff file against a bundled policy with no repo" mode
  (separable increment; see research R5).
