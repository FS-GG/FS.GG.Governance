namespace FS.GG.Governance.CodeChecks

open FS.GG.Governance.CodeChecks.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CodeChecks =

    /// SHA-256 binding used by structured complexity justifications.
    val sourceDigest: source: string -> string

    /// Analyze supplied F# documents through compiler parse/type-check facts. No repository I/O,
    /// clock, network, or process exit occurs at this boundary.
    val analyze: request: AnalysisRequest -> Async<AnalysisReport>
