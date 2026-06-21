// Evidence-reuse operations for the evidence-reuse decision core (F030). The public surface is fixed by
// EvidenceReuse.fsi (Principle II); no top-level binding here carries an access modifier. Both `decide` and
// `record` are pure, total, and deterministic (FR-003, FR-009): no clock, filesystem, git, environment, or
// network; identical inputs always yield the identical decision/store. Reuse is EXACTLY F029 `matches`; the
// explanation is EXACTLY F029 `diff` (research D2). The decision tables are fixed by
// contracts/reuse-decision-semantics.md.

namespace FS.GG.Governance.EvidenceReuse

open FS.GG.Governance.FreshnessKey
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module EvidenceReuse =

    let empty: ReuseStore = ReuseStore []

    let entries (store: ReuseStore) : RecordedEvidence list =
        let (ReuseStore es) = store
        es

    let referenceValue (reference: EvidenceRef) : string =
        let (EvidenceRef s) = reference
        s

    let record (inputs: FreshnessInputs) (evidence: EvidenceRef) (store: ReuseStore) : ReuseStore =
        // Drop any prior FULL-match (superseded evidence for the same world), then cons the new entry at the
        // head so it is the most-recent (newest-first). Entries that only share the gate are kept. Returns a
        // new value; the input store is never mutated (FR-007, FR-008).
        let (ReuseStore es) = store
        let kept = es |> List.filter (fun e -> not (FreshnessKey.matches inputs e.Inputs))
        ReuseStore({ Inputs = inputs; Evidence = evidence } :: kept)

    let decide (candidate: FreshnessInputs) (store: ReuseStore) : ReuseDecision =
        let (ReuseStore es) = store
        // Step 1 — full match? Head-first over the newest-first store ⇒ most-recent wins (FR-004, FR-005).
        match es |> List.tryFind (fun e -> FreshnessKey.matches candidate e.Inputs) with
        | Some e -> Reuse e.Evidence
        | None ->
            // Step 2 — no full match: locate the cause (FR-006). The most-recent entry sharing the
            // candidate's GateId (Check AND Domain) explains exactly what moved; if none shares the gate,
            // there is no prior evidence for this work.
            match
                es
                |> List.tryFind (fun e -> e.Inputs.Check = candidate.Check && e.Inputs.Domain = candidate.Domain)
            with
            | Some e -> Recompute(InputsChanged(FreshnessKey.diff candidate e.Inputs))
            | None -> Recompute NoPriorEvidence
