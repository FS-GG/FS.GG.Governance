namespace FS.GG.Governance.Kernel

// The light routing layer (F07 · 007-routing-severity-modes). Sits ABOVE the F04
// CheckRule bridge and decides, for an abstract change, WHICH requirements apply,
// WHETHER they advise or block, and WHY — as a pure value. No I/O, no probe, no agent
// (FR-016). The matching Route.fsi is the SOLE visibility declaration — no top-level
// binding here carries private/internal/public (Principle II).

type Stakes =
    | Routine
    | Fenced of name: string

type Fence<'change> =
    { Name: string
      Trips: 'change -> bool }

type RunMode =
    | Sandbox
    | Inner
    | Gate

type Route =
    { Stakes: Stakes
      Advisory: ContractEntry list
      Blocking: ContractEntry list
      Reason: string }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Route =

    // The lifecycle position as a short, stable label (used only in the mandatory reason).
    let modeName (mode: RunMode) : string =
        match mode with
        | Sandbox -> "sandbox"
        | Inner -> "inner"
        | Gate -> "gate"

    let stakesOf (fences: Fence<'change> list) (change: 'change) : Stakes =
        // Forbid trumps permit: Fenced iff ANY fence trips; else Routine (R-S1/S3). The
        // decision is on the SET of tripping fences, so it is independent of fence order.
        match fences |> List.filter (fun f -> f.Trips change) with
        | [] -> Routine
        | tripped ->
            // The carried name is a function of the SET of tripped fence names — reusing the F02
            // reason-combination convention VERBATIM (`Verdict.combineReasons`, 111/A6): split on the
            // reserved "; " separator dropping empties, de-duplicate, ordinal-sort, re-join. Hence it is
            // identical under any permutation of `fences` (R-S2, closes hazard 5 / decision #4).
            let name = Verdict.combineReasons (tripped |> List.map (fun f -> f.Name))

            Fenced name

    let route (fences: Fence<'change> list) (rules: CheckRule<'fact> list) (mode: RunMode) (change: 'change) : Route =
        // 1. Classify — independent of `mode` (R-R1/FR-009).
        let stakes = stakesOf fences change

        // 2. Fold the APPLICABLE rules into drift-proof requirements: each ContractEntry's
        //    Statement IS Check.render of the rule's check (reuses F06 Contract.ofRules), in
        //    catalog order (R-R2). No probe runs — render folds structure only.
        let entries = Contract.ofRules rules

        // 3. Enforcement gate: a Blocking-severity requirement becomes a blocking gate ONLY
        //    when the change is Fenced AND the run mode is Gate (R-R3/R-R4, FR-008). This is
        //    the SOLE lever moving a requirement from Advisory to Blocking; List.partition
        //    preserves catalog order within each side.
        let enforced =
            match stakes with
            | Fenced _ -> mode = Gate
            | Routine -> false

        let blocking, advisory =
            entries |> List.partition (fun e -> e.Severity = Blocking && enforced)

        // 4. The mandatory, non-empty reason naming the stakes, the run mode, and the
        //    outcome (R-R6, FR-011).
        let reason =
            match stakes with
            | Routine ->
                sprintf
                    "light — no declared fence matched; routine change carries no blocking gates in %s mode"
                    (modeName mode)
            | Fenced names when enforced ->
                sprintf
                    "gate — fenced (%s); %d blocking-severity requirement(s) enforced as blocking gate(s) at the merge boundary"
                    names
                    (List.length blocking)
            | Fenced names ->
                sprintf
                    "fenced (%s); advisory only in %s mode — stakes recorded, not enforced (blocking gates apply only at gate)"
                    names
                    (modeName mode)

        { Stakes = stakes
          Advisory = advisory
          Blocking = blocking
          Reason = reason }

    let renderRoute (route: Route) : string =
        // Deterministic, execution-free explanation (R-D1/D2/D3, FR-014). Folds the Route
        // value only — names rule/severity/statement/spec without running any probe.
        let severityTag (s: Severity) =
            match s with
            | Advisory -> "advisory"
            | Blocking -> "blocking"

        let stakesLine =
            match route.Stakes with
            | Routine -> "stakes: routine — light, no gates"
            | Fenced names -> sprintf "stakes: fenced (%s)" names

        // A blocking gate also names the fence(s) that raised the stakes (R-D1).
        let fenceSuffix =
            match route.Stakes with
            | Fenced names -> sprintf "   ← fence %s" names
            | Routine -> ""

        let renderEntry (suffix: string) (e: ContractEntry) =
            let (RuleId id) = e.Id
            sprintf "  - [%s] %s — %s   (%s §%s)%s" (severityTag e.Severity) id e.Statement e.Spec.Document e.Spec.Section suffix

        // Every section header is ALWAYS rendered with its count — "(0)" when empty, never
        // omitted — so the shape is fixed and deterministic (R-D2).
        [ yield stakesLine
          yield sprintf "reason: %s" route.Reason
          yield sprintf "blocking (%d):" (List.length route.Blocking)
          yield! route.Blocking |> List.map (renderEntry fenceSuffix)
          yield sprintf "advisory (%d):" (List.length route.Advisory)
          yield! route.Advisory |> List.map (renderEntry "") ]
        |> String.concat "\n"
