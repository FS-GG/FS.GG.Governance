// FSI entry point for the Principle I design pass (T010) and ad-hoc exploration.
//
//   dotnet build src/FS.GG.Governance.Kernel
//   dotnet fsi scripts/prelude.fsx
//
// It references the BUILT kernel assembly and opens the public namespace so the
// curated contract is exercised exactly as a downstream consumer would (SC-004).

#r "../src/FS.GG.Governance.Kernel/bin/Debug/net10.0/FS.GG.Governance.Kernel.dll"

open FS.GG.Governance.Kernel

// A toy 'fact for the sketch: a plain string. `identify` is the sole identity
// authority (D3) — here, the string itself names the fact.
let identify (s: string) = FactId s

let supplied: FactSet<string> =
    [ { Id = FactId "A"; Value = "A"; Provenance = [] } ]

// Two chained monotonic rules: A ⇒ B, B ⇒ C.
let ruleAB: Rule<string> =
    { Id = RuleId "A=>B"
      Description = "A implies B"
      Apply =
        fun facts ->
            if facts |> List.exists (fun f -> f.Value = "A") then
                [ { Id = FactId "B"; Value = "B"; Provenance = [ { Rule = RuleId "A=>B"; Inputs = [ FactId "A" ]; Note = "A implies B" } ] } ]
            else [] }

let ruleBC: Rule<string> =
    { Id = RuleId "B=>C"
      Description = "B implies C"
      Apply =
        fun facts ->
            if facts |> List.exists (fun f -> f.Value = "B") then
                [ { Id = FactId "C"; Value = "C"; Provenance = [ { Rule = RuleId "B=>C"; Inputs = [ FactId "B" ]; Note = "B implies C" } ] } ]
            else [] }

let result = FixedPoint.evaluate identify [ ruleAB; ruleBC ] supplied

printfn "Facts:  %A" (result.Facts |> List.map (fun f -> f.Id))
printfn "Rounds: %d" result.Rounds
for f in result.Facts do
    printfn "  %A  provenance=%A" f.Id f.Provenance

// ── Verdict sketch (F02) — exercise the Kleene algebra through the public surface ──
// (quickstart.md §"FSI sketch"). Calls `failwith` against the T003 stub until the
// real Verdict.fs lands — the point of this pass is that the SHAPES typecheck.

// 1. Construct the three kinds.
let pass = Pass
let fail = Fail "spacing 6px off-scale"
let pending = Uncertain "agent has not reviewed tone"

printfn "\nVerdict kinds: %A | %A | %A" pass fail pending

// 2. An undecided clause survives a conjunction of otherwise-passing clauses.
printfn "all [Pass; Uncertain; Pass] = %A" (Verdict.all [ Pass; Uncertain "tone?"; Pass ])

// 3. A definite fail dominates, even with an undecided sibling.
printfn "all [Fail; Uncertain]       = %A" (Verdict.all [ Fail "a"; Uncertain "b" ])

// 4. Disjunction: a pass dominates; otherwise an uncertain survives over fail.
printfn "any [Fail; Pass]            = %A" (Verdict.any [ Fail "a"; Pass ])
printfn "any [Fail; Uncertain]       = %A" (Verdict.any [ Fail "a"; Uncertain "b" ])

// 5. Order- and nesting-independence — outcome AND reason string byte-for-byte equal.
printfn "all reorder equal?  %b" (Verdict.all [ Fail "a"; Fail "z" ] = Verdict.all [ Fail "z"; Fail "a" ])
printfn "all re-nest equal?  %b" (Verdict.all [ Verdict.all [ Fail "a"; Fail "z" ]; Fail "m" ] = Verdict.all [ Fail "a"; Fail "z"; Fail "m" ])

// 6. Negation: pass⇄fail tags swap; uncertain fixed.
printfn "negate (Fail x)  = %A" (Verdict.negate (Fail "x"))
printfn "negate Pass      = %A" (Verdict.negate Pass)
printfn "negate Uncertain = %A" (Verdict.negate (Uncertain "y"))

// 7. Identities.
printfn "all [] = %A ; any [] = %A" (Verdict.all []) (Verdict.any [])

// ── Check sketch (F03) — fold one reified value six ways through the public surface ──
// (quickstart.md §"FSI sketch"). `open Check` brings the ==>/.&/.| operators into
// infix scope. Calls `failwith` against the T002 stub until Check.fs bodies land —
// the point of this pass is that the SHAPES typecheck against the contract.

open Check

// 1. Two probes built by hand from the smart constructors — one reads an artifact and
//    reports Met, one reports Unknown.
let contrast = Check.probe "contrastRatio" [ { Kind = "token"; Key = "text" } ] [ NumberArg 4.5 ] (fun (_: FactSet<string>) -> Met)
let tone = Check.probe "toneIsProfessional" [] [] (fun (_: FactSet<string>) -> Unknown "not reviewed")

// 2. Compose checks that read like their sentences.
let chk = contrast .& tone        // = All [contrast; tone]
let imp = contrast ==> tone       // = Implies (contrast, tone)

// 3. Evaluate (the only fold that needs facts): an undecided clause survives a
//    conjunction of otherwise-passing clauses.
printfn "\nCheck.eval chk            = %A" (Check.eval [] chk)

// 4. Render without facts — no Eval runs.
printfn "Check.render chk          = %s" (Check.render chk)

// 5. Hash: commutative canonicalization for All; positional for Implies.
printfn "hash All reorder equal?   %b" (Check.hash (All [ contrast; tone ]) = Check.hash (All [ tone; contrast ]))
printfn "hash imp reversed differ? %b" (Check.hash (contrast ==> tone) <> Check.hash (tone ==> contrast))

// 6. Explain agrees with eval.
printfn "explain verdict = eval?   %b" (Explanation.verdict (Check.explain [] chk) = Check.eval [] chk)

// 7. Reads / reified-ness (structural, no facts).
printfn "Check.reads chk           = %A" (Check.reads chk)
printfn "Check.isReified chk       = %b" (Check.isReified chk)
printfn "isReified with Opaque?    %b" (Check.isReified (chk .& Opaque("judge", fun _ -> Met)))

// 8. Never-executes proof: a probe whose Eval throws still renders and hashes.
let boom = Check.probe "boom" [] [] (fun (_: FactSet<string>) -> failwith "executed")
printfn "render boom (no exec)     = %s" (Check.render boom)
printfn "hash boom (no exec) len   = %d" (Check.hash boom).Length
