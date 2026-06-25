// Curated public signature contract for the scheduled-exhaustive-matrix decision (F26, P3).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// Matrix.fs carries NO access modifiers. `decideMatrix` is PURE, TOTAL, DETERMINISTIC: no clock/filesystem/
// process, never throws, byte-identical for identical inputs. It reuses the F25 `Budget.fits` ordered-Cost
// ceiling VERBATIM rather than re-encoding the comparison.

namespace FS.GG.Governance.ValidationMatrix

open FS.GG.Governance.CostBudget.Model
open FS.GG.Governance.ValidationMatrix.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Matrix =

    /// Decide whether a DECLARED exhaustive matrix runs now or is deferred, reusing the F25 CostBudget
    /// ceiling VERBATIM. None ⇒ NotDeclared (never an invented matrix, FR-009). Some m ⇒ RunNow m iff the
    /// budget admits m.Cost (the ceiling is the gate — a lower-cost matrix runs even at the inner loop), else
    /// Deferred (DeferredToScheduledBoundary (m.Name, m.Cost)). PURE, TOTAL.
    val decideMatrix:
        budget: CostBudget ->
        boundary: MatrixBoundary ->
        declared: ExhaustiveMatrix option ->
            MatrixPlan
