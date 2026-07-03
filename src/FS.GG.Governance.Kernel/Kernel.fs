namespace FS.GG.Governance.Kernel

// Kernel core (F01): a pure, domain-neutral forward-chaining reasoner.
//
// The matching Kernel.fsi is the SOLE visibility declaration — no top-level binding
// here carries private/internal/public (Principle II). The types below DEFINE the
// shapes the signature exposes; the signature decides what stays public.
//
// Precondition (documented, not runtime-enforced — FR-012): rules are MONOTONIC
// (add-only). Negated, aggregated, or recursively-negated facts are SUPPLIED from a
// lower stratum, never derived within this fixed point. See Kernel.fsi / README.

type FactId = FactId of string

type RuleId = RuleId of string

type ProvenanceStep =
    { Rule: RuleId
      Inputs: FactId list
      Note: string }

type FactAssertion<'fact> =
    { Id: FactId
      Value: 'fact
      Provenance: ProvenanceStep list }

type FactSet<'fact> = FactAssertion<'fact> list

type Rule<'fact> =
    { Id: RuleId
      Description: string
      Apply: FactSet<'fact> -> FactAssertion<'fact> list }

type EvaluationResult<'fact> =
    { Facts: FactSet<'fact>
      Rounds: int }

module FixedPoint =

    let evaluate
        (identify: 'fact -> FactId)
        (rules: Rule<'fact> list)
        (supplied: FactSet<'fact>)
        : EvaluationResult<'fact> =

        // mutable: fixed-point iteration to convergence — the single accumulator
        // blessed by Principle III for a rule-evaluation pass. It holds the known
        // facts keyed by FactId; nothing outside this function observes the mutation.
        let known = System.Collections.Generic.Dictionary<FactId, FactAssertion<'fact>>()

        // Snapshot the known set as a canonically (FactId-)ordered list. Rules and the
        // final emit both read this, so the result is order-independent (D1, SC-001).
        let snapshot () =
            known.Values |> Seq.sortBy (fun f -> f.Id) |> List.ofSeq

        // (1) Normalize supplied (data-model step 1). `identify` is the SOLE identity
        // authority (D3); asserted facts get empty Provenance (FR-005); duplicates
        // collapse by FactId, first occurrence wins (FR-007).
        // FR-009 (opacity): `Value` is handed to `identify` and carried verbatim — it is
        // never pattern-matched or branched on. Opacity is guaranteed structurally by the
        // generic `'fact` signature, not by a runtime check.
        for a in supplied do
            let id = identify a.Value
            if not (known.ContainsKey id) then
                known.[id] <- { Id = id; Value = a.Value; Provenance = [] }

        // (2)-(4) Synchronous rounds to quiescence.
        let mutable rounds = 0
        let mutable changed = true

        // Fuel guard (#56/B2): every productive round commits ≥1 NEW fact (candidates keep only ids not
        // already `known`), so `known` grows each round and `rounds` can never exceed `known.Count` for a
        // well-formed monotone rule set. Bounding the loop by that live invariant hardens the totality the
        // kernel promises elsewhere: a pathological rule that produced without growing `known` would spin
        // forever otherwise. The bound is UNREACHABLE for correct inputs (rounds ≤ known.Count always), so
        // it never changes an existing result.
        while changed && rounds <= known.Count do
            // (2) Apply EVERY rule to the SAME immutable snapshot; a fact derived this
            // round is invisible to other rules until the next round (D1).
            let current = snapshot ()

            let produced =
                rules
                |> List.collect (fun rule -> rule.Apply current |> List.map (fun a -> rule.Id, a))

            // (3a) Canonicalize each produced fact's id via `identify` (D3, overriding any
            // rule-supplied Id); discard ids already known. Keep what is new this round.
            let candidates =
                produced
                |> List.choose (fun (ruleId, a) ->
                    let id = identify a.Value
                    if known.ContainsKey id then None else Some(id, ruleId, a))

            // (3b) Group new candidates by FactId; for each, keep the single step chosen
            // by the total order on (FactId, RuleId) — the deterministic tie-break (D2).
            let selected =
                candidates
                |> List.groupBy (fun (id, _, _) -> id)
                |> List.map (fun (id, group) ->
                    let (_, _, winner) = group |> List.minBy (fun (fid, ruleId, _) -> (fid, ruleId))
                    { Id = id; Value = winner.Value; Provenance = winner.Provenance })

            // (4) Commit. A productive round (≥1 new fact) bumps Rounds and iterates;
            // an empty round is quiescence and is NOT counted (D4).
            match selected with
            | [] -> changed <- false
            | _ ->
                for f in selected do
                    known.[f.Id] <- f

                rounds <- rounds + 1

        // (5) Emit: deduplicated by construction, sorted by FactId for byte-for-byte
        // reproducibility (SC-001).
        { Facts = snapshot (); Rounds = rounds }
