# Contract: `ApiCompatibility` Release Rule (the verdict)

The pure verdict. Extends the existing release-rules core additively; reuses `FactState`, `evaluate`, `rollup`/`evaluateRelease`, and the `Maturity` ratchet verbatim.

## Surface added

- `ReleaseRuleKind.ApiCompatibility` (additive case).
- `apiCompatibilityFact : ApiBreakSignal -> VersionDelta -> FactState option` (PackEvidence; pure/total). `None` ⇒ not covered (no fact emitted, FR-007).
- `ApiBreakSignal`, `ApiBreak`, `ApiBreakKind`, `VersionDelta`, `ApiCompatCoverage`, `ApiCompatCoverageOutcome` (data-model.md).

## Fact derivation (authoritative — D4)

`apiCompatibilityFact signal delta`:

| signal | delta | FactState |
|---|---|---|
| `NoBreakingChanges` | any | `Some Met` |
| `BreakingChanges _` | `MajorBump` | `Some Met` |
| `BreakingChanges _` | `MinorOrPatchBump` \| `NoForwardChange` | `Some Unmet` |
| `NoBaseline` | `NoBaselineDelta` | `Some Met` (vacuous, FR-009) |
| `Indeterminate _` | any | `Some Unrecoverable` (fail-safe, FR-008) |
| `NotPackable` | any | `None` (not covered, FR-007) |

The host writes `Some s` into `ReleaseFacts.States.[ApiCompatibility]`; `None` packages are excluded from the rule and listed in coverage as `NotCovered`.

## Rule evaluation & rollup (reused, unchanged)

- A declared `ApiCompatibility` rule → exactly one `ReleaseFinding` via `Release.evaluate` (`Met`→`Satisfied`, `Unmet`/`Unrecoverable`→`Violated`).
- `Reason` MUST name the package (`Surface`), the break(s)/cause, and the required remediation: *"breaking change(s) detected vs published vX.Y.Z; requires a MAJOR version bump or revert"* / *"API comparison indeterminate: <reason>"* (FR-003).
- `rollup`/`evaluateRelease` partitions into Blockers/Warnings/Passing **unchanged**.

## Advisory → required (Maturity ratchet — D3)

| Phase | Declared `Maturity` | `Unmet`/`Unrecoverable` lands in | `Verdict` impact |
|---|---|---|---|
| **US1 — advisory** | advisory (base `Blocking` relaxed) | `Warnings` | none (visible only) |
| **US2 — required** | `BlockOnRelease` | `Blockers` | `Verdict = Fail`, `ExitCodeBasis = Blocked` |

Promotion = a reviewed change to the declared rule's `Maturity` when SC-005 holds (zero breaking-under-bump across covered packages), mirrored by adding the CI job to required checks. `AdvisoryPromotion` (F039) is NOT used (that governs agent-reviewed findings, not this deterministic check — D3).

## Invariants

- Additive: no existing rule kind, `Pack.versionPolicy`, or `evaluate`/`rollup` behavior changes.
- Pure/total/deterministic: identical `(signal, delta)` ⇒ identical fact; identical facts ⇒ byte-identical findings (existing release-core property).
- Fail-safe and vacuous-safe per the table; `NoBaseline` is `Met` but reported as `NoBaselineYet` in coverage (FR-007).
