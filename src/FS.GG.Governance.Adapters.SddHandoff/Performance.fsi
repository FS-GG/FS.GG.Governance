namespace FS.GG.Governance.Adapters.SddHandoff

open FS.GG.Governance.Adapters.SddHandoff.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Performance =

    /// Independently validate bindings and recompute measurements from raw samples. `stale` is the
    /// handoff's typed stale-evidence signal for this evidence id. Pure and total.
    val evaluate:
        stale: bool ->
        evidence: Fsgg.Schemas.GovernanceHandoffPerformanceEvidence ->
            PerformanceEvaluation
