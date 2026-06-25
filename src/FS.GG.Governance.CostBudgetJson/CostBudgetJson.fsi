// Curated public signature contract for the cost-budget.json projection (F25).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// CostBudgetJson.fs carries NO access modifiers; every writer and closed-enum token helper lives ONLY in the
// .fs (the `CacheEligibilityJson`/`RouteJson` precedent). `ofReport` is PURE and TOTAL: no file/process/clock/
// git/env access, never throws, byte-identical for identical input, order-independent (FR-011, SC-008).

namespace FS.GG.Governance.CostBudgetJson

open FS.GG.Governance.CostBudget.Model
open FS.GG.Governance.CostBudget.Findings

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CostBudgetJson =

    /// "fsgg.cost-budget/v1". Fixed; never derived from clock/env/input.
    val schemaVersion: string

    /// Project the budgeted decisions + the cost/cache findings to deterministic JSON. Entries preserved in
    /// the report's GateId-ordinal order verbatim (no re-sort). Identical input -> byte-identical text.
    /// `decisions` and `findings` are ALWAYS present (empty arrays when there are none).
    val ofReport: report: CacheDecisionReport -> findings: CostFinding list -> string
