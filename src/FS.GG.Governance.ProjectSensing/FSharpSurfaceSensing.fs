namespace FS.GG.Governance.ProjectSensing

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module FSharpSurfaceSensing =
    let sense root project isTestProject requiresBaseline baselineCurrent =
        FS.GG.Governance.DesignChecks.FSharpSurface.senseProject root project isTestProject requiresBaseline baselineCurrent
