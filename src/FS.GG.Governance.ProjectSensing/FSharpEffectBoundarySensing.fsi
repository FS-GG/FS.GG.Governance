namespace FS.GG.Governance.ProjectSensing

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module FSharpEffectBoundarySensing =
    /// Production ProjectSensing seam for declared `fsgg:effect-boundary` symbols in compiled F# sources.
    val sense: root:string -> project:string -> Result<FS.GG.Governance.DesignChecks.FSharpEffectBoundary.BoundaryFacts list,string>
