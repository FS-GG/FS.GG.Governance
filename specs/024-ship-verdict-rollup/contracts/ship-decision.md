# Contract: `Ship.rollup` and the `ShipDecision` value

**Feature**: `024-ship-verdict-rollup`

This document is the prose contract behind `contracts/Ship.fsi` and `contracts/Model.fsi`. It is the
acceptance surface the semantic tests assert against.

## Entry point

```fsharp
Ship.rollup : RouteResult -> RunMode -> Profile -> ShipDecision
```

Total, deterministic, pure, never-throwing. One `RunMode` and one `Profile` apply to the whole change.

## Behavioural contract

| ID | Guarantee | Tied to |
|---|---|---|
| C1 | `Verdict = Fail` iff at least one enforced item derives effective `Blocking`; else `Pass`. | FR-002, SC-001 |
| C2 | Each item's effective severity & reason come from F023 `deriveEffectiveSeverity`, not re-implemented. | FR-003 |
| C3 | `Blockers` = exactly the effective-`Blocking` items; `Warnings` = exactly the base-`Blocking`-relaxed-to-`Advisory` items. | FR-004 |
| C4 | Every item (blocker/warning/passing) carries base severity, effective severity, mode, profile, maturity, reason. | FR-005 |
| C5 | Each item's `Decision.BaseSeverity` equals the base severity the mapping assigned — never altered/hidden. | FR-006, SC-003 |
| C6 | `ExitCodeBasis = Clean` iff `Verdict = Pass`; `Blocked` iff `Fail`. No process exit code set. | FR-007 |
| C7 | Total over all (gate maturities × finding zones × mode × profile), incl. empty `RouteResult`; never throws. | FR-008, SC-005 |
| C8 | Deterministic; `Blockers`/`Warnings`/`Passing` ordered by the stable composite key, not arrival order. | FR-009, SC-004 |
| C9 | Each distinct selected gate evaluated once; no gate/finding dropped; `|B|+|W|+|P| = N+M`. | FR-010, SC-006 |
| C10 | A base-`Advisory` item is always a passing item — never escalated. | FR-011 |
| C11 | No audit.json/serialized doc, no I/O, no command, no policy.yml, no cache/freshness, no exit code. | FR-012, SC-007 |

## Worked example (SC-002)

- **Worked example item** — a gate with `Maturity = BlockOnShip` (base `Blocking` per the mapping),
  `rollup` at `Mode = Inner`, `Profile = Light` ⇒ effective `Advisory` ⇒ lands in `Warnings` with the
  F023 relaxed reason naming the `gate` boundary. Contributes no blocker.
- **Same-rollup blocker + warning** — at `Mode = Gate`, `Profile = Light`: a `BlockOnShip` gate blocks
  (`Blockers`), a `BlockOnRelease` gate warns (`Warnings`); `Verdict = Fail`, `ExitCodeBasis = Blocked`.

## Edge cases (from spec)

| Input | Expected |
|---|---|
| Empty `RouteResult` (no gates, no findings) | `Verdict = Pass`, all three lists `[]`, `ExitCodeBasis = Clean` — never an error/fabricated blocker. |
| All items derive effective `Advisory` | `Verdict = Pass`; base-blocking-relaxed items appear in `Warnings` (visible, not hidden). |
| `ProtectedBoundaryUnknown` finding, no gates selected | Can be a `Blocker` at `Mode = Gate`+ on its own base severity (independent of any gate). |
| `Observe`/`Warn` gate | base `Advisory` ⇒ always `Passing` (never a warning, never a blocker). |
| Base-`Advisory` item under strictest profile | stays `Passing` — never escalated (C10). |

## Surface-area baseline

A `surface/FS.GG.Governance.Ship.surface.txt` baseline is generated for the new public module and
validated by an in-project surface-drift test (Tier 1 obligation; the F018/F019/F023 precedent).
