// Curated public signature contract for the declared-exhaustive-matrix types (F26, P3).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// Model.fs carries NO access modifiers. REUSES the F014 `Cost` (Config) verbatim; the axes are opaque tokens.

namespace FS.GG.Governance.ValidationMatrix

open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// A declared broad validation matrix (product-neutral; the axes are opaque tokens).
    type ExhaustiveMatrix =
        { Name: string
          Cost: Cost
          Dimensions: string list }

    /// Which run boundary is executing.
    type MatrixBoundary =
        | InnerLoop
        | ScheduledOrRelease

    /// Why a declared matrix did not run now (named, deterministic).
    type DeferReason =
        | DeferredToScheduledBoundary of name: string * cost: Cost

    /// The decision.
    type MatrixPlan =
        | RunNow of ExhaustiveMatrix
        | Deferred of DeferReason
        | NotDeclared
