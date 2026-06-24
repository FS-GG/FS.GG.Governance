# Contract: `fsgg verify` command-line + exit codes

The pre-PR verification host. Pure `Loop.parse` normalizes argv; the edge `Interpreter.run` executes the
pipeline; `Program` maps the result to a process exit code. A leading bare `verify` verb is tolerated and
dropped (no central dispatcher is assumed).

## Synopsis

```
fsgg verify [--repo <dir>]
            [--paths <p1> <p2> ... | --since <rev>]
            [--profile <light|standard|strict|release>]
            [--json]
            [--verify-out <path>]
            [--store <path>] [--persist-store]
```

## Flags

| Flag | Value | Default | Meaning |
|------|-------|---------|---------|
| `--repo` | dir | `.` | Governed repository working directory. |
| `--paths` | one+ paths | — | Explicit change scope; bypasses git diff. Mutually exclusive with `--since`. Empty list ⇒ usage error. |
| `--since` | revision | — | Scope = changes since `<rev>`. Mutually exclusive with `--paths`. |
| `--profile` | token | `standard` | Enforcement profile. Unrecognized ⇒ usage error. |
| `--json` | — | text | Print the `verify.json` document verbatim to stdout (suppresses text). |
| `--verify-out` | path | `<repo>/readiness/verify.json` | Where the artifact is written (atomic temp+rename). |
| `--store` | path | `<repo>/readiness/evidence-reuse.json` | Evidence-reuse store to read. |
| `--persist-store` | — | off | Opt-in: prune/bound/serialize the grown store back to `--store` (non-fatal). |

**No `--mode` flag.** The enforcement mode is fixed to `Verify` (FR-017): verify cannot be escalated into the
`Gate`-mode merge verdict. The default scope is the locally-changed set (`--paths`/`--since` override it).

## Exit codes (FR-009, SC-002)

| Code | `ExitDecision` | When |
|------|----------------|------|
| `0` | `Success` | No effective-blocking check unmet — including **empty selection** ("nothing to verify"), and advisory-only findings. |
| `1` | `Blocked` | ≥1 effective-blocking check unmet (recomputed-to-fail, no-command, or uncertain at `RunMode.Verify`). Distinct from every failure-to-run code. |
| `2` | `UsageError'` | Unknown flag, missing value, `--paths`+`--since` together, empty `--paths`, or unrecognized `--profile`. Decided before any port is built ⇒ no artifact written. |
| `3` | `InputUnavailable` | Absent/invalid governing input the host cannot proceed past (catalog missing/invalid, git sensing unavailable). No partial artifact. |
| `4` | `ToolError` | Genuine tool/IO defect (unwritable artifact path, execution-port defect). No partial artifact. |

A freshness/store **sensing** failure is **not** an exit code: it degrades to a safe default + a non-fatal
currency note and never changes the verdict or exit (FR-010/FR-013).

## Behavioral guarantees

- **Selection** reuses F015/F018/F017/F019 (`Routing.route → Gates.buildRegistry → Findings → Route.select`).
- **Verdict** reuses F024 `Ship.rollup` at `RunMode.Verify` + F052 `applyExecution` (relocate passing
  command-gates). Blocking/advisory split uses the established enforcement dials; an uncertain result is never
  coerced to pass (FR-005).
- **Execution/reuse** reuses F046/F041/F051/F052: fresh checks reuse prior evidence (not re-run); stale checks
  recompute once each (FR-003, SC-003).
- **Currency** is surfaced per selected check (fresh/reused, stale/recomputed with changed categories,
  recompute-by-default) — the existing freshness/reuse evaluation, no new sensing (FR-004).
- **Determinism/no-mutation**: identical state+outcomes ⇒ byte-identical `verify.json`; only `verify.json`
  (and the opt-in store) is written; no partial artifact on a tool error (FR-007/FR-013, SC-004/SC-005).
- **Network-free** own logic, verifiable by a scope guard (FR-014, SC-007).
- **Not the merge authority**: a passing `fsgg verify` does not authorize a merge; `fsgg ship` remains the
  protected-boundary verdict (FR-017).
