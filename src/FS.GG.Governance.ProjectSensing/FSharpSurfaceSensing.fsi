namespace FS.GG.Governance.ProjectSensing

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module FSharpSurfaceSensing =
    /// Typed ProjectSensing ownership seam for live F# project/config/policy facts.
    val sense:
        root:string -> project:string -> isTestProject:bool -> requiresBaseline:bool -> baselineCurrent:bool ->
            Result<FS.GG.Governance.DesignChecks.FSharpSurface.ModuleFacts list,string>
