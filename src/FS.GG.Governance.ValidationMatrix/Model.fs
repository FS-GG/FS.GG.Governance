namespace FS.GG.Governance.ValidationMatrix

open FS.GG.Governance.Config.Model

// The F26 declared-exhaustive-matrix type vocabulary (P3). Pure, product-neutral values. The surface is
// Model.fsi (Principle II) — no access modifiers here.

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type ExhaustiveMatrix =
        { Name: string
          Cost: Cost
          Dimensions: string list }

    type MatrixBoundary =
        | InnerLoop
        | ScheduledOrRelease

    type DeferReason =
        | DeferredToScheduledBoundary of name: string * cost: Cost

    type MatrixPlan =
        | RunNow of ExhaustiveMatrix
        | Deferred of DeferReason
        | NotDeclared
