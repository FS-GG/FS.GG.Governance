namespace FS.GG.Governance.Route

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Findings.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type SelectingPath =
        { Path: GovernedPath
          MatchedGlob: GovernedPath }

    type SelectedGate =
        { Gate: Gate
          SelectingPaths: SelectingPath list }

    type CostRollup =
        { Cheap: int
          Medium: int
          High: int
          Exhaustive: int }

    type RouteResult =
        { SelectedGates: SelectedGate list
          Findings: FindingReport
          Cost: CostRollup }
