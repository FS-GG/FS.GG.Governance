namespace FS.GG.Governance.Kernel

// Three-valued (Kleene "strong") verdict algebra (F02).
//
// The matching Verdict.fsi is the SOLE visibility declaration — no top-level binding
// here carries private/internal/public (Principle II). Pure values and total
// functions only: no fact inspection, no domain vocabulary, no I/O (FR-010).

type Verdict =
    | Pass
    | Fail of reason: string
    | Uncertain of reason: string

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Verdict =

    // Reasons attached to the cases of a given kind, in list order.
    let failReasons verdicts =
        verdicts |> List.choose (function Fail r -> Some r | _ -> None)

    let uncertainReasons verdicts =
        verdicts |> List.choose (function Uncertain r -> Some r | _ -> None)

    // Reason aggregation (FR-006, SC-001 — the Hazard-2 mitigation): make a combined
    // reason a function of the SET of "; "-delimited components, not their order or
    // multiplicity. Split each contributing reason on the reserved separator dropping
    // empty components, de-duplicate, ordinal-sort (culture-invariant, byte-stable),
    // and re-join. This is what makes the reason byte-for-byte order-, nesting-, and
    // duplication-independent — and absorbs the `any []` identity reason "".
    let combineReasons (reasons: string list) : string =
        reasons
        |> List.collect (fun r -> List.ofArray (r.Split([| "; " |], System.StringSplitOptions.RemoveEmptyEntries)))
        |> List.distinct
        |> List.sortWith (fun a b -> System.String.CompareOrdinal(a, b))
        |> String.concat "; "

    let all (verdicts: Verdict list) : Verdict =
        // Kleene "strong" conjunction (FR-002), first-match priority over a single
        // pass: any Fail ⇒ Fail; else any Uncertain ⇒ Uncertain; else Pass.
        if verdicts |> List.exists (function Fail _ -> true | _ -> false) then
            Fail(combineReasons (failReasons verdicts))
        elif verdicts |> List.exists (function Uncertain _ -> true | _ -> false) then
            Uncertain(combineReasons (uncertainReasons verdicts))
        else
            Pass

    let any (verdicts: Verdict list) : Verdict =
        // Kleene "strong" disjunction (FR-003): any Pass ⇒ Pass; else any Uncertain
        // ⇒ Uncertain; else Fail (the empty list ⇒ Fail "").
        if verdicts |> List.exists (function Pass -> true | _ -> false) then
            Pass
        elif verdicts |> List.exists (function Uncertain _ -> true | _ -> false) then
            Uncertain(combineReasons (uncertainReasons verdicts))
        else
            Fail(combineReasons (failReasons verdicts))

    let negate (verdict: Verdict) : Verdict =
        // Polarity flip (FR-004): swap the pass/fail TAG; leave Uncertain fixed (an
        // unresolved judgement has no polarity). Reasons are NOT carried across the
        // flip — negate Pass = Fail "" and negate (Fail _) = Pass — so `negate` is an
        // involution on tags, and a full involution only for Uncertain and ""-reasoned
        // values (data-model INV-7). That is exactly what F03's Not/Implies needs.
        match verdict with
        | Pass -> Fail ""
        | Fail _ -> Pass
        | Uncertain r -> Uncertain r
