namespace FS.GG.Governance.Config

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module FSharpSurfacePolicy =

    type Exemption =
        { Module: string
          Owner: string
          Rationale: string
          ReviewBy: System.DateOnly }

    type ProjectPolicy =
        { RequiresBaseline: bool
          BaselineCurrent: bool }

    type Facts =
        { DeclaredGlob: string
          Projects: Map<string, ProjectPolicy>
          Exemptions: Exemption list }

    type LoadResult =
        | Missing of Facts
        | Loaded of Facts
        | Invalid of reason: string

    val defaultFacts: Facts

    /// Read and validate `.fsgg/fsharp-surface.json` at the configuration edge.
    val load: root: string -> LoadResult
