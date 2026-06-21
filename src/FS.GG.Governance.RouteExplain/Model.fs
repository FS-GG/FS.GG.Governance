// Broad-route cost-explanation types for the explanation core (F031). The public surface is fixed by
// Model.fsi (Principle II); no top-level binding here carries an access modifier. These are product-neutral,
// YAML-free values that `RouteExplain.explain` returns; they reuse the F019 `SelectedGate` and the F018
// `Gate` verbatim rather than redefining them (FR-009). The only new shape is the explanation itself.

namespace FS.GG.Governance.RouteExplain

open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Route.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type AlternativeOutcome =
        | CheaperLocalAlternative of Gate
        | NoCheaperLocalAlternative

    type HighCostFinding =
        { Selected: SelectedGate
          Alternative: AlternativeOutcome }

    type RouteExplanation = { Findings: HighCostFinding list }
