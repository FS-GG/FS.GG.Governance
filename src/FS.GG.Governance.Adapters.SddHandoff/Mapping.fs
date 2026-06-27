// ADR-0002 evidence mapping (F081, US1). Visibility lives in Mapping.fsi (Principle II).
// PURE and TOTAL. Reproduces ADR-0002's accepted mapping row-for-row (research D4) and reuses the
// domain-neutral kernel `Evidence.build`/`effective` verbatim for the taint closure.

namespace FS.GG.Governance.Adapters.SddHandoff

open FS.GG.Governance.Kernel
open FS.GG.Governance.Adapters.SddHandoff.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Mapping =

    // ADR-0002 row: declared state → kernel EvidenceState. `Deferred`/`AcceptedDeferral` map to
    // `Skipped` (a recorded-rationale `[-]` skip, NOT `Pending` — FR-004). `autoSynthetic` is not a
    // `DeclaredState` member (rejected on read), so this map is total over the closed union.
    let private toEvidenceState (state: DeclaredState) : EvidenceState =
        match state with
        | DeclaredState.Pending -> EvidenceState.Pending
        | DeclaredState.Real -> EvidenceState.Real
        | DeclaredState.Synthetic -> EvidenceState.Synthetic
        | DeclaredState.Failed -> EvidenceState.Failed
        | DeclaredState.Skipped -> EvidenceState.Skipped
        | DeclaredState.Deferred -> EvidenceState.Skipped
        | DeclaredState.AcceptedDeferral -> EvidenceState.Skipped

    let mapEvidence
        (source: string)
        (block: EvidenceBlock)
        : Result<(string * EvidenceState) list * (string * string) list, Diagnostic> * Diagnostic list =
        let mapped = block.Nodes |> List.map (fun n -> n.Id, toEvidenceState n.State)

        // FR-006: a `Stale = true` node carries a StaleEvidence diagnostic ALONGSIDE its underlying
        // mapped state (staleness is Governance-owned freshness, never a freshness verdict here).
        let staleDiags =
            block.Nodes
            |> List.filter (fun n -> n.Stale)
            |> List.map (fun n ->
                { Cause = StaleEvidence
                  Source = source
                  Message =
                    sprintf "evidence node '%s' is declared stale; its underlying state is carried plus a staleEvidence diagnostic (FR-006)" n.Id })

        Ok(mapped, block.Dependencies), staleDiags

    let effectiveStates
        (nodes: (string * EvidenceState) list)
        (deps: (string * string) list)
        : Result<Map<string, EvidenceState>, Diagnostic> =
        match Evidence.build nodes deps with
        | Ok graph -> Ok(Evidence.effective graph)
        | Error err ->
            // Defence in depth: `Evidence.build` independently refuses a declared `AutoSynthetic`
            // (research D4) and an inconsistent graph (cycle / dangling edge).
            let cause, message =
                match err with
                | GraphError.AutoSyntheticDeclared id ->
                    Model.AutoSyntheticDeclared,
                    sprintf "evidence node '%s' is AutoSynthetic, which is computed-only and never a valid declared input" id
                | Cycle cycle ->
                    Model.Malformed, sprintf "declared evidence dependencies form a cycle: %A" cycle
                | UnknownNode node ->
                    Model.Malformed, sprintf "a declared evidence dependency names an unknown node '%s'" node

            Error { Cause = cause; Source = ""; Message = message }
