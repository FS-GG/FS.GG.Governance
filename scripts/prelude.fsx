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

// ── Routing sketch (F07) — light by default, deterministic precedence, explainable ──
// (quickstart.md §"FSI sketch"). Calls `failwith`-stubs against the T002 stub until the
// Route.fs bodies land — the point of the pass is that the SHAPES typecheck against
// Route.fsi (Principle I). open Check is already in scope (operators .&, .|, ==>).

// A real, domain-neutral 'change: a set of changed "paths" (any adapter shape works — D1).
let change = set [ "src/Api.fs"; "README.md" ]

// Two declared fences. forbid-trumps-permit: ANY trip ⇒ Fenced (order-independent).
let mergeFence  = { Name = "merge-boundary";   Trips = fun (c: Set<string>) -> c |> Set.exists (fun p -> p.StartsWith "src/") }
let secFence    = { Name = "security-surface"; Trips = fun (c: Set<string>) -> c.Contains "src/Auth.fs" }
let fences = [ mergeFence; secFence ]

// 1. Light by default: a change tripping no fence is Routine (V40).
printfn "\nstakesOf [] (no fence)   = %A" (Route.stakesOf [] change)                 // Routine
printfn "stakesOf docs-only       = %A" (Route.stakesOf fences (set [ "README.md" ]))  // Routine

// 2. A single matching fence ⇒ Fenced; order-independent across permutations (V41/V43).
printfn "stakesOf fenced          = %A" (Route.stakesOf fences change)             // Fenced "merge-boundary"
printfn "stakesOf permuted equal? %b" (Route.stakesOf fences change = Route.stakesOf (List.rev fences) change)

// A real blocking rule (reuse an F03 check; F04 authors + promotes it).
let hasReview = Check.probe "peer-reviewed" [] [] (fun (_: FactSet<string>) -> Met)
let routeSpec = { Document = "constitution.md"; Section = "I" }
let blockingRule =
    CheckRule.rule (RuleId "peer-review") Deterministic routeSpec hasReview
    |> Result.map CheckRule.blocking
    |> function Ok r -> r | Error e -> failwithf "%A" e

// 3. Run-mode matrix: same fenced change + blocking rule — advisory in Inner, blocking in Gate;
//    stakes identical across modes (V44).
let inGate  = Route.route fences [ blockingRule ] Gate  change
let inInner = Route.route fences [ blockingRule ] Inner change
printfn "\nGate  blocking count = %d" (List.length inGate.Blocking)    // 1
printfn "Inner blocking count = %d" (List.length inInner.Blocking)     // 0 (advisory only)
printfn "stakes equal across modes? %b" (inGate.Stakes = inInner.Stakes)

// 4. Light change at Gate still blocks nothing (V40).
let lightAtGate = Route.route fences [ blockingRule ] Gate (set [ "README.md" ])
printfn "light @ Gate blocking = %d" (List.length lightAtGate.Blocking)  // 0

// 5. Drift-proof gate: the gate's Statement IS Check.render of the rule's check (V46).
printfn "gate statement = render? %b" ((List.head inGate.Blocking).Statement = Check.render blockingRule.Check)

// 6. Every route carries a non-empty reason — routine and fenced (V45).
printfn "fenced reason non-empty? %b"  (inGate.Reason <> "")
printfn "routine reason non-empty? %b" ((Route.route [] [] Inner change).Reason <> "")

// 7. renderRoute is deterministic and execution-free (V47).
printfn "\n%s" (Route.renderRoute inGate)
printfn "render deterministic? %b" (Route.renderRoute inGate = Route.renderRoute inGate)

// ── Effects-shell sketch (F08) — sense → plan → act, nondeterminism reified as evidence ──
// Exercises the built Host through its PUBLIC surface, exactly as the F12 CLI would (SC-010).
#r "../src/FS.GG.Governance.Host/bin/Debug/net10.0/FS.GG.Governance.Host.dll"
open FS.GG.Governance.Host

// A domain-neutral 'fact: a sensed artifact, or an embedded governance RuleOutcome.
type HFact =
    | HArtifact of ArtifactRef * string
    | HOutcome of RuleOutcome

let apiRef : ArtifactRef = { Kind = "file"; Key = "src/Api.fs" }
let hIdentify = function
    | HArtifact (r, _) -> FactId (sprintf "artifact:%s/%s" r.Kind r.Key)
    | HOutcome o ->
        FactId (
            "outcome:" +
            match o with
            | Decided (RuleId r, _) -> "decided:" + r
            | NeedsReview req -> "needs:" + req.Key
            | RuleOutcome.Reviewed rr -> "reviewed:" + rr.Key
            | Escalated (RuleId r) -> "escalated:" + r)
let hReadContent (facts: FactSet<HFact>) (r: ArtifactRef) =
    facts |> List.tryPick (function { Value = HArtifact (rr, c) } when rr = r -> Some c | _ -> None)
let hBridge : Bridge<HFact> =
    { Judge = { ModelId = "sketch-judge"; Version = "1" }
      ArtifactHash = fun facts r -> match hReadContent facts r with Some c -> "h:" + string (c.GetHashCode()) | None -> ""
      Embed = HOutcome
      Project = function HOutcome o -> Some o | HArtifact _ -> None }

let agentRule =
    Check.probe "reviewApi" [ apiRef ] [] (fun _ -> Met)
    |> fun chk -> CheckRule.rule (RuleId "R1") AgentReviewed { Document = "doc"; Section = "api" } chk
    |> function Ok r -> r |> CheckRule.blocking |> CheckRule.asking "Does the API meet the bar?" | Error e -> failwithf "%A" e

let hCfg : LoopConfig<Set<string>, HFact> =
    { Identify = hIdentify; Rules = [ agentRule ]; Bridge = hBridge
      Fences = [ { Name = "merge-boundary"; Trips = fun (c: Set<string>) -> c |> Set.exists (fun p -> p.StartsWith "src/") } ]
      Mode = Gate; Policy = Loop.defaultPolicy
      SenseArtifact = (fun r c -> HArtifact (r, c))
      ReadContent = hReadContent }

let hChange = set [ "src/Api.fs" ]

// PURE side: init computes the route + emits one ReadArtifact — NO I/O (V48).
let (hm0, hStartup) = Loop.init hCfg hChange
printfn "\n[F08] init phase   = %A" hm0.Phase
printfn "[F08] startup eff  = %A" hStartup
printfn "[F08] route stakes = %A" hm0.Route.Stakes
printfn "[F08] accept single= %A" (Loop.accept SingleSample [ { Verdict = Pass; Confidence = 0.9 } ])
printfn "[F08] accept agree<= %A" (Loop.accept (Agreement 2) [ { Verdict = Pass; Confidence = 0.9 } ])

// EDGE side: drive run against a REAL temp fixture + a FAKE judge + a real-ish store (V53/V55).
let hTmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.Guid.NewGuid().ToString("N"))
System.IO.Directory.CreateDirectory hTmp |> ignore
System.IO.File.WriteAllText(System.IO.Path.Combine(hTmp, "Api.fs"), "let x = 1")
let mutable hDispatches = 0
let hCache = System.Collections.Generic.Dictionary<string, RecordedReview>()
let hPorts : Ports =
    { Read = (fun r -> try Ok (System.IO.File.ReadAllText(System.IO.Path.Combine(hTmp, System.IO.Path.GetFileName r.Key |> Option.ofObj |> Option.defaultValue r.Key))) with e -> Error e.Message)
      Judge = (fun _ -> hDispatches <- hDispatches + 1; Ok { Verdict = Pass; Confidence = 1.0 })  // SYNTHETIC: fake judge — a real agent is not a reproducible oracle (F12)
      Store = { Load = (fun k -> Ok (match hCache.TryGetValue k with | true, v -> Some v | _ -> None)); Save = (fun rr -> hCache.[rr.Key] <- rr; Ok ()) }
      Sink = fun out -> printfn "[F08] emit: %s" (match out with ExplanationJson _ -> "explanation" | ContractJson _ -> "contract" | RouteText _ -> "route") }
let hFirst  = Interpreter.run hPorts hCfg hChange
printfn "[F08] first  run dispatches = %d" hDispatches   // 1
let hSecond = Interpreter.run hPorts hCfg hChange
printfn "[F08] second run dispatches = %d (cache hit)" hDispatches  // 1 — zero new
printfn "[F08] final phase = %A · failures = %b" hSecond.Phase (List.isEmpty hSecond.Failures)

// ─────────────────────────────────────────────────────────────────────────────
// F09 · adapter SPI & composition root sketch — a domain plugs in by supplying ONLY
// its own vocabulary; lifting is faithful; composition is deterministic. Exercised
// against the BUILT FS.GG.Governance.Adapters.Spi library (Principle I, SC-008).
//   dotnet build src/FS.GG.Governance.Adapters.Spi   # before running this script
// ─────────────────────────────────────────────────────────────────────────────
#r "../src/FS.GG.Governance.Adapters.Spi/bin/Debug/net10.0/FS.GG.Governance.Adapters.Spi.dll"

open FS.GG.Governance.Adapters.Spi
open Check // the ==> operator

let f09GovKey (o: RuleOutcome) =
    match o with
    | Decided (RuleId r, _) -> r
    | NeedsReview rq -> rq.Key
    | RuleOutcome.Reviewed rr -> rr.Key
    | Escalated (RuleId r) -> r

// ── Toy domain A (SYNTHETIC example domain) — a tiny "document" domain ──
type DocFact = HasTitle of bool | DocGov of RuleOutcome
type DocArtifact = TheDoc
let docToRef (a: DocArtifact) = match a with TheDoc -> { Kind = "doc"; Key = "the-doc" }
let titledProbe: Probe<DocFact> =
    { Name = "has-title"; Reads = [ docToRef TheDoc ]; Args = []
      Eval = fun fs -> if fs |> List.exists (fun f -> f.Value = HasTitle true) then Met else Unmet "no title" }
let docRule =
    CheckRule.rule (RuleId "doc-titled") Deterministic { Document = "doc-policy"; Section = "title" } (Atom titledProbe)
    |> function Ok r -> CheckRule.blocking r | Error e -> failwithf "%A" e
let docIdentify = function HasTitle b -> FactId(sprintf "doc:title:%b" b) | DocGov o -> FactId("doc:gov:" + f09GovKey o)
let docBridge: Bridge<DocFact> =
    { Judge = { ModelId = "sketch"; Version = "1" }; ArtifactHash = fun _ _ -> ""
      Embed = DocGov; Project = function DocGov o -> Some o | HasTitle _ -> None }
let docAdapter: Adapter<DocFact, DocArtifact, Set<string>> =
    { Identify = docIdentify; ToRef = docToRef; Probes = [ titledProbe ]
      Rules = [ docRule ]; Fences = [ { Name = "doc"; Trips = Set.contains "doc.md" } ]; Bridge = docBridge }

// 1. STANDALONE: the adapter governs itself using ONLY kernel facilities.
let f09Supplied = [ { Id = FactId "t"; Value = HasTitle true; Provenance = [] } ]
let f09Std = FixedPoint.evaluate docAdapter.Identify (Adapter.toRules docAdapter) f09Supplied
printfn "\n[F09] standalone facts = %d (rounds %d)" f09Std.Facts.Length f09Std.Rounds

// ── Toy domain B (SYNTHETIC) — an UNRELATED "task" domain (distinct vocabulary) ──
type TaskFact = TaskOpen of bool | TaskGov of RuleOutcome
type TaskArtifact = TheTask
let taskToRef (a: TaskArtifact) = match a with TheTask -> { Kind = "task"; Key = "the-task" }
let taskProbe: Probe<TaskFact> =
    { Name = "task-closed"; Reads = [ taskToRef TheTask ]; Args = []
      Eval = fun fs -> if fs |> List.exists (fun f -> f.Value = TaskOpen false) then Met else Unmet "open" }
let taskRule =
    CheckRule.rule (RuleId "task-closed") Deterministic { Document = "task-policy"; Section = "closure" } (Atom taskProbe)
    |> function Ok r -> r | Error e -> failwithf "%A" e
let taskIdentify = function TaskOpen b -> FactId(sprintf "task:open:%b" b) | TaskGov o -> FactId("task:gov:" + f09GovKey o)
let taskBridge: Bridge<TaskFact> =
    { Judge = { ModelId = "sketch"; Version = "1" }; ArtifactHash = fun _ _ -> ""
      Embed = TaskGov; Project = function TaskGov o -> Some o | TaskOpen _ -> None }
let taskAdapter: Adapter<TaskFact, TaskArtifact, Set<string>> =
    { Identify = taskIdentify; ToRef = taskToRef; Probes = [ taskProbe ]
      Rules = [ taskRule ]; Fences = [ { Name = "task"; Trips = Set.contains "task.md" } ]; Bridge = taskBridge }

// ── The composition root (consumer-authored): the closed coproduct + its wiring ──
type ProjF = Doc of DocFact | Task of TaskFact | Gov of RuleOutcome
let (|DocP|_|) = function Doc f -> Some f | _ -> None
let (|TaskP|_|) = function Task f -> Some f | _ -> None
let projIdentify = function Doc d -> docIdentify d | Task t -> taskIdentify t | Gov o -> FactId("proj:gov:" + f09GovKey o)
let projBridge: Bridge<ProjF> =
    { Judge = { ModelId = "sketch"; Version = "1" }; ArtifactHash = fun _ _ -> ""
      Embed = Gov; Project = function Gov o -> Some o | _ -> None }

// 2. FAITHFUL LIFT: the lifted check's render & hash are byte-identical to standalone.
let f09Lifted = Lift.checkRule (|DocP|_|) docRule
printfn "[F09] render invariant = %b" (Check.render f09Lifted.Check = Check.render docRule.Check)
printfn "[F09] hash invariant   = %b (cache key stable)" (Check.hash f09Lifted.Check = Check.hash docRule.Check)

// 3. COMPOSE two unrelated adapters + one cross-domain Implies at the one root.
let f09Cross =
    [ CheckRule.rule (RuleId "doc-implies-task") Deterministic { Document = "root"; Section = "x" }
        (Lift.check (|DocP|_|) (Atom titledProbe)
         ==> Check.probe "task-closed?" [] [] (fun (fs: FactSet<ProjF>) ->
             if fs |> List.exists (fun f -> match f.Value with Task (TaskOpen false) -> true | _ -> false) then Met
             else Unmet "open"))
      |> function Ok r -> CheckRule.blocking r | Error e -> failwithf "%A" e ]
let f09Composed =
    Composition.compose
        [ Composition.lift (|DocP|_|) id docAdapter; Composition.lift (|TaskP|_|) id taskAdapter ]
        f09Cross
printfn "[F09] composed = %d rules / %d fences" f09Composed.Catalog.Length f09Composed.Fences.Length
let f09ProjFacts =
    [ { Id = FactId "d"; Value = Doc(HasTitle true); Provenance = [] }
      { Id = FactId "k"; Value = Task(TaskOpen false); Provenance = [] } ]
let f09Proj = FixedPoint.evaluate projIdentify (Composition.toRules projBridge f09Composed) f09ProjFacts
let f09CrossOk = f09Proj.Facts |> List.exists (fun f -> f.Value = Gov(Decided(RuleId "doc-implies-task", Pass)))
printfn "[F09] project facts = %d (rounds %d) · cross-domain Pass = %b" f09Proj.Facts.Length f09Proj.Rounds f09CrossOk

// 4. REMOVAL/BOUNDARY: drop the Doc adapter — the rest is intact and the cross-domain
//    rule (whose ANTECEDENT domain is now gone) goes INERT (vacuous Pass), never errors.
let f09NoDoc = Composition.compose [ Composition.lift (|TaskP|_|) id taskAdapter ] f09Cross
let f09NoDocResult =
    FixedPoint.evaluate projIdentify (Composition.toRules projBridge f09NoDoc)
        [ { Id = FactId "k"; Value = Task(TaskOpen false); Provenance = [] } ]
let f09Inert = f09NoDocResult.Facts |> List.exists (fun f -> f.Value = Gov(Decided(RuleId "doc-implies-task", Pass)))
printfn "[F09] after removal = %d rules (kernel + task intact) · cross rule inert = %b" f09NoDoc.Catalog.Length f09Inert

// ─────────────────────────────────────────────────────────────────────────────
// F10 · the Spec Kit adapter — governance dogfoods this repo's own workflow as data.
// The FIRST concrete production adapter (domain #1 of M3). It supplies ONLY its own five
// SPI components + the Bridge and reuses 100% of the kernel. Exercised against the BUILT
// FS.GG.Governance.Adapters.SpecKit library (Principle I, SC-001/SC-008).
//   dotnet build src/FS.GG.Governance.Adapters.SpecKit   # before running this script
// ─────────────────────────────────────────────────────────────────────────────
#r "../src/FS.GG.Governance.Adapters.SpecKit/bin/Debug/net10.0/FS.GG.Governance.Adapters.SpecKit.dll"

open FS.GG.Governance.Adapters.SpecKit

let f10Judge: JudgeId = { ModelId = "speckit-judge"; Version = "1" }
let f10Adapter = Catalog.adapter f10Judge Catalog.defaultDial   // the ONE Adapter value (FR-003)

// 1. OBSERVER-ONLY + FIVE-COMPONENT (SC-001): fully specified by the five SPI components.
printfn "\n[F10] rules = %d / probes = %d / fences = %d"
    f10Adapter.Rules.Length f10Adapter.Probes.Length f10Adapter.Fences.Length

// 2. PHASE GUARD (SC-002): a whenPhase Plan rule is a definite not-applicable before Plan.
let f10BeforePlan = [ { Id = FactId "ph"; Value = PhaseReached Phase.Specify; Provenance = [] } ]
let f10AtPlan = [ { Id = FactId "ph"; Value = PhaseReached Phase.Plan; Provenance = [] } ]
printfn "[F10] before Plan = %A (vacuous Pass) · at Plan = %A (Opaque contributes)"
    (Check.eval f10BeforePlan Catalog.planSatisfiesSpec.Check)
    (Check.eval f10AtPlan Catalog.planSatisfiesSpec.Check)

// 3. INNER-LOOP vs MERGE (SC-003): nothing blocks before merge; merge is the single fence.
let f10Inner = { Phase = Phase.Tasks; Surfaces = Set.ofList [ SpecKitArtifact.Tasks ] }
let f10Merge = { Phase = Phase.Merge; Surfaces = Set.ofList [ SpecKitArtifact.Tasks ] }
let f10InnerRoute = Route.route f10Adapter.Fences f10Adapter.Rules Inner f10Inner
let f10MergeRoute = Route.route f10Adapter.Fences f10Adapter.Rules Gate f10Merge
printfn "[F10] inner blocking = %d / merge blocking = %d" f10InnerRoute.Blocking.Length f10MergeRoute.Blocking.Length

// 4. EVIDENCE / TAINT via the KERNEL (SC-004): AutoSynthetic flows down TaskDependsOn.
let f10Tainted =
    [ { Id = FactId "t1"; Value = TaskState("T1", Synthetic); Provenance = [] }
      { Id = FactId "t2"; Value = TaskState("T2", Real); Provenance = [] }
      { Id = FactId "d"; Value = TaskDependsOn("T2", "T1"); Provenance = [] } ]
printfn "[F10] evidence (synthetic upstream) = %A (Fail — T2 is AutoSynthetic via T1)"
    (Check.eval f10Tainted Catalog.evidenceNotSynthetic.Check)

// 5. THE CONSTITUTION DIAL (SC-005): the blocking set is the dial's, not a fixed list.
let f10Light = { Catalog.defaultDial with BlockingAtMerge = Set.empty }
let f10LightAdapter = Catalog.adapter f10Judge f10Light
let f10LightMerge = Route.route f10LightAdapter.Fences f10LightAdapter.Rules Gate f10Merge
printfn "[F10] light merge blocking = %d (fewer than default %d)" f10LightMerge.Blocking.Length f10MergeRoute.Blocking.Length

// 6. RENDER & EXPLAIN (SC-006): every rule renders to a sentence and explains itself.
for r in Catalog.catalog do
    let (RuleId id) = r.Id
    printfn "[F10]   %-24s :: %s" id (Check.render r.Check)

// ── Design-system adapter sketch (F11) — a second, UNRELATED domain adopts the kernel ──
// (quickstart.md §"FSI sketch"). Exercised against the REAL bodies through the BUILT library.
// The F10 namespace is already open above, so the design domain's `Catalog` is reached via a
// full-path alias (its `ArtifactPresent`/`Catalog` would otherwise clash with F10's).
#r "../src/FS.GG.Governance.Adapters.DesignSystem/bin/Debug/net10.0/FS.GG.Governance.Adapters.DesignSystem.dll"

open FS.GG.Governance.Adapters.DesignSystem
module DsCatalog = FS.GG.Governance.Adapters.DesignSystem.Catalog

let f11Judge: JudgeId = { ModelId = "design-judge"; Version = "1" }
let f11Adapter = DsCatalog.adapter f11Judge          // the ONE Adapter value — NO dial (FR-003, D8)

// 1. FIVE-COMPONENT + NO-F10-SHAPE (SC-001): five SPI components + the Bridge; no authoring
//    op, no Phase/whenPhase/merge fence/dial. fences = 1 (token-surface only).
printfn "[F11] rules = %d / probes = %d / fences = %d"
    f11Adapter.Rules.Length f11Adapter.Probes.Length f11Adapter.Fences.Length

// 2. THE TIER SPLIT (SC-002): deterministic token/contrast/surface checks block; judgement is
//    Opaque (AgentReviewed); adopting a new policy is HumanOnly.
let f11DriftOk = [ { Id = FactId "m"; Value = SurfaceObservation("surface-matches", GeneratedTokenSurface, true); Provenance = [] } ]
let f11DriftBad = [ { Id = FactId "m"; Value = SurfaceObservation("surface-matches", GeneratedTokenSurface, false); Provenance = [] } ]
printfn "[F11] drift ok = %A / drift bad = %A / contrast absent = %A"
    (Check.eval f11DriftOk DsCatalog.tokenDrift.Check)
    (Check.eval f11DriftBad DsCatalog.tokenDrift.Check)
    (Check.eval [] DsCatalog.contrastPolicy.Check)          // Uncertain — never a silent Pass (Pr3)
printfn "[F11] colour-informational reified = %b / adopt-new-policy tier = %A"
    (Check.isReified DsCatalog.colourInformational.Check)   // false ⇒ AgentReviewed (FR-008)
    DsCatalog.adoptNewPolicy.Tier                            // HumanOnly (escalates, never decides)

// 3. ADVISORY BY DEFAULT; THE TOKEN-SURFACE FENCE (SC-002): only a change touching the public
//    token surface trips the single fence — there is NO merge fence and NO phase (the F10 diff).
let f11Plain = { Surfaces = Set.ofList [ RenderedCapture ] }
let f11Surface = { Surfaces = Set.ofList [ GeneratedTokenSurface ] }
let f11PlainRoute = Route.route f11Adapter.Fences f11Adapter.Rules Gate f11Plain
let f11FencedRoute = Route.route f11Adapter.Fences f11Adapter.Rules Gate f11Surface
printfn "[F11] plain blocking = %d / fenced blocking = %d" f11PlainRoute.Blocking.Length f11FencedRoute.Blocking.Length

// 4. EVIDENCE / TAINT via the KERNEL (SC-003): a deterministic verdict resting on a synthetic
//    input is AutoSynthetic via F05's fixed point; evidenceMeasured fails and NO flag flips it.
let f11Tainted =
    [ { Id = FactId "x"; Value = MeasurementState("contrast-px", Synthetic); Provenance = [] }
      { Id = FactId "v"; Value = MeasurementState("contrast-verdict", Real); Provenance = [] }
      { Id = FactId "e"; Value = VerdictRestsOn("contrast-verdict", "contrast-px"); Provenance = [] } ]
printfn "[F11] evidence (synthetic upstream) = %A (Fail — contrast-verdict is AutoSynthetic)"
    (Check.eval f11Tainted DsCatalog.evidenceMeasured.Check)

// 5. RENDER & EXPLAIN (SC-004): every rule renders to a sentence and explains itself.
for r in DsCatalog.catalog do
    let (RuleId id) = r.Id
    printfn "[F11]   %-24s :: %s" id (Check.render r.Check)

// ── CLI sketch (F12) — the optional command surface wraps Host through its own MVU ──
// Build first:
//   dotnet build src/FS.GG.Governance.Cli
#r "../src/FS.GG.Governance.Host/bin/Debug/net10.0/FS.GG.Governance.Host.dll"
#r "../src/FS.GG.Governance.Cli/bin/Debug/net10.0/FS.GG.Governance.Cli.dll"

open FS.GG.Governance.Cli

let f12Request =
    match Cli.parse [ "route"; "--root"; "."; "--mode"; "inner"; "--json"; "--review-budget"; "0" ] with
    | Ok request -> request
    | Error errors -> failwithf "F12 parse failed: %A" errors

let f12Model, f12Effects = Cli.init [ "route"; "--root"; "."; "--mode"; "inner" ]
printfn "[F12] parsed command = %A / mode = %A / format = %A" f12Request.Command f12Request.Mode f12Request.Format
printfn "[F12] init phase = %A / effects = %A" f12Model.Phase f12Effects

let f12Snapshot =
    { Root = "."
      Supplied = []
      Change = { SpecKit = None; DesignSystem = None; Scope = [] }
      Artifacts = [] }

let f12AfterSnapshot, f12AfterSnapshotEffects = Cli.update (SnapshotLoaded(Ok f12Snapshot)) f12Model
printfn "[F12] after snapshot phase = %A / effects = %A" f12AfterSnapshot.Phase f12AfterSnapshotEffects

// ── Config sketch (F014) — the optional `.fsgg` schema library (Principle I design pass) ──
// Build first:
//   dotnet build src/FS.GG.Governance.Config
// The library is YAML-free at its OUTPUT boundary: it takes raw file slots and returns
// typed, product-neutral facts (FR-010). YamlDotNet is an internal detail; a library does
// not copy it into its own bin, so resolve the transitive dependency from the test project
// (an executable) which does.
#r "../tests/FS.GG.Governance.Config.Tests/bin/Debug/net10.0/YamlDotNet.dll"
#r "../src/FS.GG.Governance.Config/bin/Debug/net10.0/FS.GG.Governance.Config.dll"

open FS.GG.Governance.Config
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Config.Schema

// Build a RawSource in memory (the shape the Loader edge assembles from disk) and run the
// PURE core. `project.yml` + `capabilities.yml` are required; `policy.yml`/`tooling.yml` are
// optional. This sketch records the intended typed-fact flow before reading any real tree.
let f14Source: RawSource =
    { Root = GovernedPath "."
      Project = Present "schemaVersion: 1\nid: demo\ngovernedRoot: .\ndomains:\n  - workflow"
      Policy = Absent
      Capabilities = Present "schemaVersion: 1\ndomains:\n  - workflow"
      Tooling = Absent }

match Schema.validate f14Source with
| Valid facts ->
    printfn "[F14] valid: id=%A domains=%A policy=%b tooling=%b" facts.Project.Id facts.Capabilities.Domains facts.Policy.IsSome facts.Tooling.IsSome
| Invalid diags ->
    printfn "[F14] invalid: %A" (diags |> List.map (fun d -> Model.diagnosticIdToken d.Id))

// A malformed slot (unsupported schemaVersion) flows to a stable, located diagnostic —
// never a thrown exception (validate is total).
let f14Bad: RawSource =
    { f14Source with Project = Present "schemaVersion: 99\nid: demo\ngovernedRoot: .\ndomains: []" }

match Schema.validate f14Bad with
| Invalid diags -> printfn "[F14] rejected: %A" (diags |> List.map (fun d -> Model.diagnosticIdToken d.Id))
| Valid _ -> printfn "[F14] UNEXPECTED valid"
