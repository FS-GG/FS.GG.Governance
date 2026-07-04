namespace FS.GG.Governance.ValidationMatrix

open FS.GG.Governance.CostBudget
open FS.GG.Governance.CostBudget.Model
open FS.GG.Governance.ValidationMatrix.Model

// The F26 scheduled-exhaustive-matrix decision (P3). PURE and TOTAL: reuses the F25 `Budget.fits` ordered-Cost
// ceiling VERBATIM (research D4) — the ceiling is the gate, not the boundary label. None ⇒ NotDeclared (never
// an invented matrix, FR-009). The surface is Matrix.fsi (Principle II) — no access modifiers here.

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Matrix =

    let decideMatrix (budget: CostBudget) (declared: ExhaustiveMatrix option) : MatrixPlan =
        // The budget already encodes the boundary's ceiling (Budget.fits is the gate), so the boundary label
        // was never read — it is not a parameter (111/B7).
        match declared with
        | None -> NotDeclared
        | Some m ->
            if Budget.fits budget m.Cost then
                RunNow m
            else
                Deferred(DeferredToScheduledBoundary(m.Name, m.Cost))
