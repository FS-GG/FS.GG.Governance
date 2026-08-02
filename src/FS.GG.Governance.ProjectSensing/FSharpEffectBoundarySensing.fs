namespace FS.GG.Governance.ProjectSensing

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module FSharpEffectBoundarySensing =
    let sense root project = FS.GG.Governance.DesignChecks.FSharpEffectBoundary.senseProject root project
