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

// ── CheckRule sketch (F04) — give a check a HOME (who decides it) and bridge it ──
// (quickstart.md §"FSI sketch"). Calls `failwith` against the stub until CheckRule.fs
// bodies land — the point of this pass is that the SHAPES typecheck against the contract.

// 1. A toy adapter fact: either a governance outcome or an artifact-content fact. This
//    is the real shape a domain adapter (F09) materialises — no mock.
type Gov =
    | GovOut of RuleOutcome
    | Art of kind: string * key: string * hash: string

// A real Bridge<Gov>: the judge identity, an artifact-content lookup FROM the facts
// (no live I/O), and the Embed/Project pair between RuleOutcome and Gov.
let bridge: Bridge<Gov> =
    { Judge = { ModelId = "claude-opus-4-8"; Version = "2026-06" }
      ArtifactHash =
        fun facts ref ->
            facts
            |> List.tryPick (fun f ->
                match f.Value with
                | Art(k, key, h) when k = ref.Kind && key = ref.Key -> Some h
                | _ -> None)
            |> Option.defaultValue ""
      Embed = GovOut
      Project = (fun f -> match f with GovOut o -> Some o | _ -> None) }

let govSpec = { Document = "wcag"; Section = "1.4.3" }
let govFacts: FactSet<Gov> = []

// A reified check (reuses the F03 contrast probe shape) and an opaque one.
let contrastG = Check.probe "contrastRatio" [ { Kind = "token"; Key = "text" } ] [ NumberArg 4.5 ] (fun (_: FactSet<Gov>) -> Met)
let opaqueG = Opaque("tone", fun (_: FactSet<Gov>) -> Unknown "not reviewed")

// 2. Author rules and see the guardrail: a reified check authors Deterministic; the same
//    tier over an Opaque check is refused; author it AgentReviewed instead.
let detRule = CheckRule.rule (RuleId "contrast") Deterministic govSpec contrastG
let refused = CheckRule.rule (RuleId "judge") Deterministic govSpec opaqueG
let agentRule = CheckRule.rule (RuleId "judge") AgentReviewed govSpec opaqueG |> Result.map (CheckRule.asking "Is the tone professional?")
printfn "\nCheckRule.rule Deterministic reified = %A" detRule
printfn "CheckRule.rule Deterministic opaque  = %A" refused
printfn "CheckRule.rule AgentReviewed opaque  = %A" agentRule

// 3. Cache key (decision #1): reproducible, judge-sensitive, artifact-order-independent.
let key = CheckRule.cacheKey bridge.Judge (Check.hash opaqueG) (Check.reads opaqueG |> List.map (bridge.ArtifactHash govFacts)) (Some "prompt")
printfn "cacheKey stable?  %b" (key = CheckRule.cacheKey bridge.Judge (Check.hash opaqueG) [] (Some "prompt"))
printfn "cacheKey re-review on Version bump differs? %b" (key <> CheckRule.cacheKey { bridge.Judge with Version = "2026-07" } (Check.hash opaqueG) [] (Some "prompt"))

// 4. Bridge to a kernel rule and run it — Description is the rendered check (no drift),
//    hit/miss behaviour for the agent tier, and Deterministic/HumanOnly verbatim.
agentRule
|> Result.map (fun r ->
    let kr = CheckRule.toRule bridge r
    printfn "Description = render? %b" (kr.Description = Check.render r.Check)
    printfn "Apply (miss) = %A" (kr.Apply govFacts))
|> ignore

// ── Evidence sketch (F05) — propagate synthetic taint over a dependency DAG ──
// (quickstart.md §"FSI sketch"). A plain string is the node 'id, so the graph is real
// and domain-neutral. Exercises the public surface exactly as a downstream consumer would.

// 1. Build a small DAG: one synthetic root with a chain of real nodes resting on it.
let evG =
    Evidence.build
        [ "data", Synthetic        // the root cause: only simulated data
          "analysis", Real         // rests on data
          "report", Real ]         // rests on analysis
        [ "analysis", "data"
          "report", "analysis" ]

// 2. Compute effective states — taint flows transitively to full depth.
printfn "\nEvidence.effective chain = %A" (evG |> Result.map Evidence.effective)
// data stays Synthetic (root cause); analysis & report (both Real) become AutoSynthetic.

// 3. Auto-clear by upgrading the root — re-declare data as Real and recompute.
printfn "auto-clear on Synthetic→Real = %A"
    (Evidence.build [ "data", Real; "analysis", Real; "report", Real ]
                    [ "analysis", "data"; "report", "analysis" ]
     |> Result.map Evidence.effective)

// 4. The guardrails — build refusals (Cycle / AutoSyntheticDeclared / UnknownNode).
printfn "refuse self-cycle    = %A" (Evidence.build [ "a", Real ] [ "a", "a" ])
printfn "refuse AutoSynthetic = %A" (Evidence.build [ "x", AutoSynthetic ] [])
printfn "refuse UnknownNode   = %A" (Evidence.build [ "a", Real ] [ "a", "ghost" ])

// 5. Non-real states are inert; synthetic outranks inheritance.
printfn "inert + synthetic-outranks = %A"
    (Evidence.build [ "root", Synthetic; "f", Failed; "s2", Synthetic ]
                    [ "f", "root"; "s2", "root" ]
     |> Result.map Evidence.effective)

// 6. Domain-neutral: the same model over a research scenario — a Real finding resting on
//    a Synthetic "simulated data" node is AutoSynthetic (exactly steps 1–2, renamed).
printfn "domain-neutral research = %A"
    (Evidence.build [ "simulated-data", Synthetic; "finding", Real ] [ "finding", "simulated-data" ]
     |> Result.map Evidence.effective)

// ── F06 design pass (006-explanation-output): JSON explanation, the drift-proof
//    contract, evidence freshness, and the evidence-state report (Principle I). ──
open FS.GG.Governance.Kernel.Check // operators .&, .|, ==>

// JSON explanation — mirrors the proof tree, deterministic, round-trips.
let met name : Check<string> = Check.probe name [] [] (fun _ -> Met)
let f06chk = (met "has-tests") .& (met "has-docs")
let f06expl = Check.explain [] f06chk
let f06json = Json.ofExplanation f06expl
printfn "\nJson.ofExplanation = %s" f06json
printfn "explanation round-trips = %b" (Json.toExplanation f06json = f06expl)
printfn "explanation deterministic = %b" (Json.ofExplanation f06expl = Json.ofExplanation f06expl)

// Drift-proof contract — each Statement IS Check.render (cannot drift).
let f06spec = { Document = "constitution.md"; Section = "V" }

match CheckRule.rule (RuleId "tests-present") Deterministic f06spec f06chk |> Result.map CheckRule.blocking with
| Ok rule ->
    let contract = Contract.ofRules [ rule ]
    printfn "\ncontract statement = render: %b" ((List.head contract).Statement = Check.render rule.Check)
    printfn "%s" (Contract.render contract)
    printfn "contract round-trips = %b" (Json.toContract (Json.ofContract contract) = contract)
| Error e -> eprintfn "%A" e

// Evidence freshness — pure over supplied instants (no clock).
printfn "\nfresh [9]      = %A" (Freshness.decide 10 [ 9 ]) // Fresh
printfn "fresh [10]     = %A" (Freshness.decide 10 [ 10 ]) // Fresh (inclusive tie)
printfn "fresh [11]     = %A" (Freshness.decide 10 [ 11 ]) // Stale
printfn "fresh []       = %A" (Freshness.decide 10 []) // Fresh (covers nothing)
printfn "fresh [3;10;7] = %A" (Freshness.decide 10 [ 3; 10; 7 ]) // Fresh (>= max)

// Evidence states in the same report — AutoSynthetic visibly marked.
printfn "\nofEvidenceState AutoSynthetic = %s" (Json.ofEvidenceState AutoSynthetic)

match Evidence.build [ "a", Real; "b", Synthetic ] [ "a", "b" ] with
| Ok g -> printfn "ofEffective = %s" (Json.ofEffective id (Evidence.effective g))
| Error e -> eprintfn "%A" e
