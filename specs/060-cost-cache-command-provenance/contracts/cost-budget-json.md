# Contract: `cost-budget.json` projection (`FS.GG.Governance.CostBudgetJson`)

Deterministic JSON sidecar written by the existing `fsgg verify` / `fsgg ship` hosts. Follows the
`Utf8JsonWriter` + fixed-field-order + closed-enum-token discipline shared by every `*Json` module (F042/F045).
PURE, TOTAL: no file/process/clock/git/env access. Byte-identical for identical input; order-independent.
FR-011, SC-008.

## `CostBudgetJson.fsi`

```fsharp
namespace FS.GG.Governance.CostBudgetJson

open FS.GG.Governance.CostBudget.Model        // CacheDecisionReport
open FS.GG.Governance.CostBudget.Findings      // CostFinding

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CostBudgetJson =

    /// "fsgg.cost-budget/v1". Fixed; never derived from clock/env/input.
    val schemaVersion: string

    /// Project the budgeted decisions + the cost/cache findings to deterministic JSON. Entries preserved in
    /// the report's GateId-ordinal order verbatim (no re-sort). Identical input -> byte-identical text.
    val ofReport: report: CacheDecisionReport -> findings: CostFinding list -> string
```

## Document shape

```jsonc
{
  "schemaVersion": "fsgg.cost-budget/v1",
  "decisions": [                                   // GateId-ordinal order; ALWAYS present (may be [])
    {
      "gate": "<GateId>",
      "cost": "cheap|medium|high|exhaustive",
      "review": "deterministic"                    //  | "agentReviewed" (the CacheKey is NOT emitted as a
                                                   //    blocking signal; an agent-reviewed gate is labelled
                                                   //    so the reader sees it stayed advisory)
      "decision": {                                // tagged union — exactly one shape:
        "kind": "reuse", "evidence": "<EvidenceRef>"          // charges nothing
        // | "kind": "recompute", "cause": { ... }            // see cause object below
        // | "kind": "overBudget",
        //     "class": "skipped|deferred",
        //     "ceiling": "cheap|medium|high|exhaustive",
        //     "reason": "<gate> (<cost>) exceeds the <ceiling> budget"
      }
    }
  ],
  "findings": [                                    // sorted (GateId, kind); ALWAYS present (may be [])
    {
      "gate": "<GateId>",
      "kind": "stale|syntheticTaint|noEvidence",
      "baseSeverity": "advisory",
      "categories": ["ruleHash", "coveredArtifacts", …],   // ONLY for kind "stale" (the changed F029 dims)
      "message": "<names the gate and cause>"
    }
  ]
}
```

The `cause` object (recompute) reuses the F042 `CacheEligibilityJson` shape verbatim:
`{ "kind": "noPriorEvidence" }` or `{ "kind": "inputsChanged", "categories": ["ruleHash", …] }`, the
categories named via `FreshnessKey.categoryToken`.

## Rules

- **No-hide.** Every decision entry and every finding is rendered; nothing is dropped or fabricated. `decisions`
  and `findings` are always present (empty arrays when there are none) — the document is well-formed for an
  all-reusable, no-finding run.
- **Fixed field order**, verified by raw-text `IndexOf` tests: `schemaVersion` < `decisions` < `findings`;
  within a decision `gate` < `cost` < `review` < `decision`.
- **Closed-enum tokens**, exhaustive matches with no wildcard — `cost`, the decision `kind`, the deferral
  `class`, the finding `kind`, and `baseSeverity` are all rendered by helpers that fail to compile if a DU
  case is added.
- **Determinism / order-independence**: `ofReport` re-emits the report's existing order (already GateId-ordinal
  from `decide`) and the findings' (GateId, kind) order; reordering the inputs to `decide` cannot change the
  text (SC-008). No wall-clock, host path, environment value, or numeric process exit code appears.
- **Opaque references verbatim.** `EvidenceRef` strings are rendered exactly as carried, never parsed or
  re-hashed.
