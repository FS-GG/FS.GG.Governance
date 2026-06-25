# Contract — Cost/Cache Findings Fold (advisory only)

**Cores consumed (verbatim):** `CostBudget.Findings.cacheFindings`, `CostBudget.Findings.enforce`.

## Transformation
```
taint   : GateId -> EvidenceTaint                     // Real | Synthetic per gate
findings = cacheFindings report taint                  // Stale | SyntheticTaint | NoEvidence, each named + severity
_        = findings |> List.map (enforce mode profile)  // proves each advisory through the existing machinery
```
The findings are projected into **`cost-budget.json` only** (via `ofReport report findings`). They are **NOT**
appended to the `ShipDecision` that `verify.json` / `audit.json` project.

## Guarantees (asserted by tests)
- Findings fold through the **existing** enforcement severity machinery and are **advisory only** — no new verdict,
  no new exit-code, no enforcement-truth-table change (FR-008, SC-006).
- Because findings land in the sidecar and never in the `ShipDecision`, `verify.json` / `audit.json` stay
  **byte-identical** (FR-007, SC-004).
- Agent-reviewed checks are never promoted to a blocker by any finding or cache decision (FR-009, SC-006).
