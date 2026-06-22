// Agent-reviewed verdict store + invalidation-decision operations for the F036 core. The public surface is
// fixed by VerdictReuse.fsi (Principle II); no top-level binding here carries an access modifier. Both
// decision operations are pure, total, and deterministic (FR-003, FR-009): no clock, filesystem, git,
// environment, or network; no model invoked, no key bytes computed (F035 owns the key); identical inputs
// always yield the identical decision/store. Validity is EXACTLY F035 `matches`; the explanation is EXACTLY
// F035 `diff`. BCL list/option handling only. The decision tables are fixed by
// contracts/lookup-decision-semantics.md.

namespace FS.GG.Governance.VerdictReuse

open FS.GG.Governance.AgentReviewKey
open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.VerdictReuse.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module VerdictReuse =

    let empty: VerdictStore = VerdictStore []

    let entries (store: VerdictStore) : CachedVerdict list =
        let (VerdictStore es) = store
        es

    let referenceValue (verdict: VerdictRef) : string =
        let (VerdictRef s) = verdict
        s

    let record (inputs: AgentReviewInputs) (verdict: VerdictRef) (store: VerdictStore) : VerdictStore =
        // Drop any superseded full-match entry (de-dup, FR-008), then cons the new entry at the head so it
        // is the most-recent (newest-first; no mutation, FR-007). Entries that merely share the work but
        // differ in some input are KEPT — they are verdicts for a different judge/prompt/artifact world.
        let (VerdictStore es) = store
        let kept = es |> List.filter (fun e -> not (AgentReviewKey.matches inputs e.Inputs))
        VerdictStore({ Inputs = inputs; Verdict = verdict } :: kept)

    let lookup (request: AgentReviewInputs) (store: VerdictStore) : LookupDecision =
        let (VerdictStore es) = store
        // Step 1 — validity: Valid iff some entry matches on EVERY input (head-first over newest-first ⇒ the
        // most-recent matching entry, deterministic, FR-004/FR-005).
        match es |> List.tryFind (fun e -> AgentReviewKey.matches request e.Inputs) with
        | Some e -> Valid e.Verdict
        | None ->
            // Step 2 — located cause: the most-recent entry sharing the request's check hash (the work key)
            // explains the change; its `diff` is non-empty and never contains CheckHashInput by construction
            // (research D5). No same-work entry ⇒ no cached verdict for this work (FR-006).
            match es |> List.tryFind (fun e -> e.Inputs.Check = request.Check) with
            | Some e -> Invalidated(InputsChanged(AgentReviewKey.diff request e.Inputs))
            | None -> Invalidated NoCachedVerdict
