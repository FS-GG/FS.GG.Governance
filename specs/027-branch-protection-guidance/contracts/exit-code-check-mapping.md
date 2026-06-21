# Contract: exit code → GitHub check status → merge outcome

The consumer-facing mapping this row **documents**. It re-derives nothing: the authority is
`Loop.exitCode` in `src/FS.GG.Governance.ShipCommand` (F026). This file fixes how an adopter's CI must
treat each code so a blocked verdict blocks the merge, a tool failure also blocks (but stays
distinguishable), and nothing is ever silently treated as a passing merge (FR-003, FR-004, FR-005).

## The mapping

| `fsgg ship` exit code | F026 meaning | GitHub Actions step result | Required-check status | Merge | Run category (diagnosable) |
|---|---|---|---|---|---|
| `0` | `Success` — clean verdict | success | passing (green) | **allowed** | clean |
| `1` | `Blocked` — blocked merge verdict | failure | failing (red) | **blocked** | **blocked verdict** |
| `2` | `UsageError'` — usage error (e.g. unrecognized lever, `--paths`+`--since` together) | failure | failing (red) | **blocked** | tool failure (usage) |
| `3` | `InputUnavailable` — input unavailable (e.g. not a git repo, unresolved/shallow base, catalog absent) | failure | failing (red) | **blocked** | tool failure (input) |
| `4` | `ToolError` — tool error (e.g. unwritable output) | failure | failing (red) | **blocked** | tool failure (tool) |

## Invariants the wiring MUST preserve

1. **No translation (FR-003).** The command's process exit code *is* the step result. The run step MUST
   NOT use `|| true`, `continue-on-error: true`, an `if:` that swallows non-zero, or any remap of the
   numeric code. A non-zero exit ⇒ a red required check ⇒ a blocked merge.
2. **Blocked is the single merge-blocked code (FR-004, SC-002).** `1` means "this change may not merge"
   and **only** that. It is distinct from every tool-failure code (`2`/`3`/`4`). A reader of the run can
   tell *blocked verdict* from *tool failure* (and which tool-failure category) from the exit code and the
   command's stderr/diagnostic — without rerunning locally.
3. **Fail-closed (FR-005).** A tool failure (`2`/`3`/`4`) is **never** reported as a passing merge. There
   is no exit code that yields a green check other than `0`. The required check MUST NOT be bypassable for
   a governed change (see [ship-ci-workflow.md](./ship-ci-workflow.md): no path filters; runs on forks).
4. **Determinism (FR-014).** Re-running over the same commit yields the same exit code and therefore the
   same check outcome (inherited from F026/F025). The wiring adds no wall-clock- or environment-dependent
   pass/fail step.
5. **Blocking is exit-code-only (FR-008).** Branch-protection blocking derives **solely** from this
   deterministic exit code. Advisory or agent-reviewed findings MAY be reported in the run but are **never**
   wired to fail the check until calibration exists.

## Cross-check (how this contract is kept honest)

`scripts/check-ship-ci-guidance.sh` asserts that the codes/meanings in this table and in the guidance
equal the live `Loop.exitCode` mapping in `src/FS.GG.Governance.ShipCommand`. If F026 ever renumbers a
code, the cross-check fails until this contract and the guidance are updated — the docs cannot silently
drift from the command they wire (FR-011).
