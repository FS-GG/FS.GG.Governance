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

// ── Routing sketch (F015) — the first consumer of the F014 typed facts (Principle I) ──
// Build first:
//   dotnet build src/FS.GG.Governance.Routing
// Routing is a PURE, TOTAL function: it takes the typed facts + a caller-supplied set of
// already-normalized candidate paths and returns a deterministic RouteReport. No I/O, no git,
// no clock. It references only Config and adds no new dependency. This sketch records the
// intended route → report flow: a deterministic precedence winner, an in-root miss, an
// out-of-scope path, and an ambiguity diagnostic with a still-total winner.
#r "../src/FS.GG.Governance.Routing/bin/Debug/net10.0/FS.GG.Governance.Routing.dll"

open FS.GG.Governance.Routing
open FS.GG.Governance.Routing.Model

// Assemble a minimal valid TypedFacts in memory (the shape Config emits) — governed root
// "src", an overlapping path map (broad default + narrow exceptions, the normal pattern), plus
// a deliberately co-specific pair to show the ambiguity branch.
let f15PathMap =
    [ { Glob = GovernedPath "src/**"; Capability = DomainId "core" }
      { Glob = GovernedPath "src/Adapters/**"; Capability = DomainId "adapters" }
      { Glob = GovernedPath "src/*/Eval.fs"; Capability = DomainId "a" }
      { Glob = GovernedPath "src/Kernel/*.fs"; Capability = DomainId "b" } ]

let f15Domains = f15PathMap |> List.map (fun e -> e.Capability) |> List.distinct

let f15Facts: TypedFacts =
    { Project =
        { SchemaVersion = SchemaVersion 1
          Id = ProjectId "demo"
          Domains = f15Domains
          GovernedRoot = GovernedPath "src"
          PackageSurfaces = []
          PolicyRef = None
          CapabilitiesRef = None }
      Policy = None
      Capabilities =
        { SchemaVersion = SchemaVersion 1
          Domains = f15Domains
          PathMap = f15PathMap
          Surfaces = []
          Checks = [] }
      Tooling = None }

let f15Report =
    Routing.route
        f15Facts
        [ GovernedPath "src/Adapters/SpecKit.fs" // precedence: more literal segments wins → adapters
          GovernedPath "src/Kernel/Eval.fs" // co-specific src/*/Eval.fs vs src/Kernel/*.fs → AmbiguousRoute
          GovernedPath "src/README.md" // in root, no glob → UnmatchedInRoot
          GovernedPath "docs/guide.md" ] // outside governed root → OutOfScope

for r in f15Report.Routings do
    printfn "[F15] %A → %A" r.Path r.Result

for d in f15Report.Diagnostics do
    printfn "[F15] diag %s: %s" (Model.routingDiagnosticIdToken d.Id) d.Message

// ── Snapshot sketch (F016) — the SENSING counterpart to F015 routing (Principle I) ──
// Build first:
//   dotnet build src/FS.GG.Governance.Snapshot
// Snapshot has a PURE core (planResolution + assemble over a RawSensing) and an impure EDGE
// (Interpreter.senseSnapshot, which runs READ-ONLY git via System.Diagnostics.Process). It
// references only Config and adds no new dependency. This sketch records the intended flow:
// resolve a loose range to a plan, assemble a hand-built RawSensing (the actual NUL-delimited
// `-z` wire bytes, written here with \000 escapes), and sense this real repo.
#r "../src/FS.GG.Governance.Snapshot/bin/Debug/net10.0/FS.GG.Governance.Snapshot.dll"

open FS.GG.Governance.Snapshot
open FS.GG.Governance.Snapshot.Model

// (1) Pure range planning — no git involved (US3, FR-004).
let f16Plan =
    Snapshot.planResolution { Since = None; Base = Some(GitRef "main"); Head = Some(GitRef "HEAD") }

printfn "[F16] plan: form=%A useMergeBase=%b" f16Plan.Form f16Plan.UseMergeBase

// (2) Pure assembly over a hand-built RawSensing — actual `-z` wire bytes, parsed/normalized.
let f16Raw: Snapshot.RawSensing =
    { RepoOk = true
      BaseResolved = Ok(CommitId "aaaa111")
      HeadResolved = Ok(CommitId "bbbb222")
      MergeBaseResolved = Ok(CommitId "aaaa111")
      DiffRaw = Ok "M\000src/Kernel/Eval.fs\000A\000docs/intro.md\000"
      StatusRaw = Ok "?? scratch.txt\000"
      BranchRaw = Ok "feature/x"
      RawCi = None
      Digests = []
      Plan = f16Plan }

let f16Snap = Snapshot.assemble f16Raw
printfn "[F16] changed: %A" (f16Snap.Changed |> List.map (fun c -> Model.changeKindToken c.Kind, c.Path))

printfn
    "[F16] dirty=%A untracked=%A branch=%A"
    f16Snap.WorkingTree.Dirty
    f16Snap.WorkingTree.Untracked
    f16Snap.Branch

// (3) Impure EDGE — sense THIS repository read-only (resolved against HEAD by default).
let f16Live =
    Interpreter.senseSnapshot (Interpreter.realPorts ".") { Since = None; Base = None; Head = None }

printfn
    "[F16] live branch=%A range=%b diagnostics=%A"
    f16Live.Branch
    f16Live.Range.IsSome
    (f16Live.Diagnostics |> List.map (fun d -> Model.sensingDiagnosticIdToken d.Id))

// ── Findings sketch (F017) — turn F015's deferred UnmatchedInRoot into a typed finding ──
// Build first:
//   dotnet build src/FS.GG.Governance.Findings
// Findings is a PURE, TOTAL classifier: it takes the F014 typed facts (only Capabilities.Surfaces
// is read) and the F015 RouteReport, and returns a deterministic FindingReport. No I/O, no git,
// no clock. It references only Config + Routing and adds no new dependency. This sketch records
// the intended route → classify flow: an ordinary in-root unknown, a protected-boundary
// escalation, a routine suppression, and an out-of-scope silence.
#r "../src/FS.GG.Governance.Findings/bin/Debug/net10.0/FS.GG.Governance.Findings.dll"

open FS.GG.Governance.Findings
open FS.GG.Governance.Findings.Model

// Assemble a minimal TypedFacts in memory: governed root "src", a path map covering ONLY
// src/Kernel/** (so paths elsewhere in-root are misses), a ProtectedSurface over src/Core (NOT
// covered by the glob, so a path under it is an in-root miss that escalates), and a Routine
// surface over src/Legacy.
let f17PathMap =
    [ { Glob = GovernedPath "src/Kernel/**"; Capability = DomainId "kernel" } ]

let f17Surfaces =
    [ { Id = SurfaceId "kernel-core"; Class = ProtectedSurface; Paths = [ GovernedPath "src/Core" ]; Owner = Owner "core-team"; Maturity = Observe }
      { Id = SurfaceId "legacy"; Class = Routine; Paths = [ GovernedPath "src/Legacy" ]; Owner = Owner "nobody"; Maturity = Observe } ]

let f17Facts: TypedFacts =
    { Project =
        { SchemaVersion = SchemaVersion 1
          Id = ProjectId "demo"
          Domains = [ DomainId "kernel" ]
          GovernedRoot = GovernedPath "src"
          PackageSurfaces = []
          PolicyRef = None
          CapabilitiesRef = None }
      Policy = None
      Capabilities =
        { SchemaVersion = SchemaVersion 1
          Domains = [ DomainId "kernel" ]
          PathMap = f17PathMap
          Surfaces = f17Surfaces
          Checks = [] }
      Tooling = None }

// Route a candidate set, then classify it.
let f17Report =
    Routing.route
        f17Facts
        [ GovernedPath "src/Kernel/Eval.fs"  // matched by src/Kernel/** → Routed → no finding
          GovernedPath "src/Core/Secret.fs"  // in-root miss on a ProtectedSurface → escalated finding
          GovernedPath "src/Legacy/Old.fs"   // in-root miss within a Routine surface → suppressed
          GovernedPath "src/Loose.fs"        // in-root miss, no surface → ordinary finding
          GovernedPath "docs/guide.md" ]     // outside the governed root → OutOfScope → no finding

let f17Findings = Findings.findUnknownGovernedPaths f17Facts f17Report

printfn "\n[F17] findings = %d" f17Findings.Findings.Length
for f in f17Findings.Findings do
    printfn "[F17] %s · %A · %A" (Model.findingIdToken f.Id) f.Path f.Zone
    printfn "[F17]   %s" f.Message

// Determinism: identical inputs ⇒ byte-identical report; an empty result is a valid success.
printfn "[F17] deterministic? %b" (Findings.findUnknownGovernedPaths f17Facts f17Report = f17Findings)
printfn "[F17] empty-on-clean = %A" (Findings.findUnknownGovernedPaths f17Facts (Routing.route f17Facts [ GovernedPath "src/Kernel/Eval.fs" ]))

// ── F018: the typed gate registry — Gates.buildRegistry : TypedFacts -> GateRegistry ──
// A pure, total projection of the already-validated F014 facts into a typed gate registry: one
// Gate per declared capability Check, carrying a stable GateId ("domain:checkId") and the *Gate
// identities* field set (domain, description, prerequisites, cost, timeout, owner, maturity,
// product-check, freshness key). No diagnostics, no I/O, no clock — F014's guarantees are
// preserved by construction and the output is byte-identical for identical input.

#r "../src/FS.GG.Governance.Gates/bin/Debug/net10.0/FS.GG.Governance.Gates.dll"

open FS.GG.Governance.Gates
open FS.GG.Governance.Gates.Model

// Three checks across two domains: one references a declared tooling command (timeout 600s) and
// runs in the Release environment (→ product-check); the other two are command-less and Local.
let f18Checks: Check list =
    [ { Id = CheckId "tests"; Domain = DomainId "build"; Command = Some(CommandId "dotnet-test")
        Owner = Owner "team-a"; Cost = Medium; Environment = Release; Maturity = BlockOnShip }
      { Id = CheckId "format"; Domain = DomainId "build"; Command = None
        Owner = Owner "team-a"; Cost = Cheap; Environment = Local; Maturity = Observe }
      { Id = CheckId "lint"; Domain = DomainId "docs"; Command = None
        Owner = Owner "team-c"; Cost = Cheap; Environment = Local; Maturity = Warn } ]

let f18Facts: TypedFacts =
    { Project =
        { SchemaVersion = SchemaVersion 1; Id = ProjectId "demo"; Domains = [ DomainId "build"; DomainId "docs" ]
          GovernedRoot = GovernedPath "src"; PackageSurfaces = []; PolicyRef = None; CapabilitiesRef = None }
      Policy = None
      Capabilities =
        { SchemaVersion = SchemaVersion 1; Domains = [ DomainId "build"; DomainId "docs" ]
          PathMap = []; Surfaces = []; Checks = f18Checks }
      Tooling =
        Some
            { SchemaVersion = SchemaVersion 1
              Commands = [ { Id = CommandId "dotnet-test"; Command = "dotnet test"; Timeout = TimeoutLimit 600; Environment = FS.GG.Governance.Config.Model.Ci } ]
              EnvironmentClasses = []; ExternalTools = [] } }

let f18Registry = Gates.buildRegistry f18Facts

printfn "\n[F18] gates = %d (in GateId ordinal order)" f18Registry.Gates.Length
for g in f18Registry.Gates do
    printfn "[F18] %s · cost=%A · timeout=%A · product=%b · prereqs=%A"
        (gateIdValue g.Id) g.Cost g.Timeout g.ProductCheck g.Prerequisites

// Determinism: identical inputs ⇒ byte-identical registry; empty facts ⇒ empty (successful) registry.
printfn "[F18] deterministic? %b" (Gates.buildRegistry f18Facts = f18Registry)
printfn "[F18] empty-facts → empty-registry = %A" (Gates.buildRegistry { f18Facts with Capabilities = { f18Facts.Capabilities with Checks = [] } })


// ── F019: the route-resolution core — Route.select : GateRegistry -> RouteReport -> FindingReport -> RouteResult ──
// A pure, total JOIN of three already-typed upstream outputs: the F018 gate registry (which gates
// exist per domain), the F015 route report (which domain each changed path belongs to), and the F017
// finding report (which governed paths are unclassified). For each `Routed` path it selects every
// registry gate whose declared `Domain` equals the path's routed `DomainId` (by id equality), unions
// them deduplicated by `GateId`, annotates each with the selecting path(s) and the winning glob,
// rolls up the distinct selected gates' costs as a per-tier multiset, and carries the F017 findings
// through unchanged. No diagnostics, no I/O, no clock — byte-identical for identical input.

#r "../src/FS.GG.Governance.Route/bin/Debug/net10.0/FS.GG.Governance.Route.dll"

open FS.GG.Governance.Route
open FS.GG.Governance.Route.Model

// Two domains with path-map globs, a protected surface, and one check per domain. `release` is a
// declared domain whose gate no change reaches in this sketch.
let f19Facts: TypedFacts =
    { Project =
        { SchemaVersion = SchemaVersion 1; Id = ProjectId "demo"
          Domains = [ DomainId "build"; DomainId "docs" ]
          GovernedRoot = GovernedPath "src"; PackageSurfaces = []; PolicyRef = None; CapabilitiesRef = None }
      Policy = None
      Capabilities =
        { SchemaVersion = SchemaVersion 1
          Domains = [ DomainId "build"; DomainId "docs" ]
          PathMap =
            [ { Glob = GovernedPath "src/build/**"; Capability = DomainId "build" }
              { Glob = GovernedPath "src/docs/**"; Capability = DomainId "docs" } ]
          Surfaces =
            [ { Id = SurfaceId "kernel"; Class = ProtectedSurface; Paths = [ GovernedPath "src/build" ]
                Owner = Owner "team-a"; Maturity = BlockOnShip } ]
          Checks =
            [ { Id = CheckId "tests"; Domain = DomainId "build"; Command = None
                Owner = Owner "team-a"; Cost = Medium; Environment = FS.GG.Governance.Config.Model.Local; Maturity = BlockOnShip }
              { Id = CheckId "format"; Domain = DomainId "build"; Command = None
                Owner = Owner "team-a"; Cost = Cheap; Environment = FS.GG.Governance.Config.Model.Local; Maturity = Observe }
              { Id = CheckId "lint"; Domain = DomainId "docs"; Command = None
                Owner = Owner "team-c"; Cost = Cheap; Environment = FS.GG.Governance.Config.Model.Local; Maturity = Warn } ] }
      Tooling = None }

// A change touching one build path, one docs path, and one unclassified in-root path.
let f19Change = [ GovernedPath "src/build/Core.fs"; GovernedPath "src/docs/Guide.md"; GovernedPath "src/loose/x.fs" ]

// The genuine F015 -> F017 -> F018 -> F019 chain.
let f19Registry = Gates.buildRegistry f19Facts
let f19Report = Routing.route f19Facts f19Change
let f19Findings = Findings.findUnknownGovernedPaths f19Facts f19Report
let f19Result = Route.select f19Registry f19Report f19Findings

printfn "\n[F19] selected gates = %d (in GateId ordinal order)" f19Result.SelectedGates.Length
for sg in f19Result.SelectedGates do
    let paths = sg.SelectingPaths |> List.map (fun p -> sprintf "%A via %A" p.Path p.MatchedGlob)
    printfn "[F19] %s · domain=%A · cost=%A · selectedBy=%A" (gateIdValue sg.Gate.Id) sg.Gate.Domain sg.Gate.Cost paths

printfn "[F19] carried findings = %d" f19Result.Findings.Findings.Length
for f in f19Result.Findings.Findings do
    printfn "[F19]   finding %s on %A" (findingIdToken f.Id) f.Path
printfn "[F19] cost rollup = %A" f19Result.Cost

// Determinism: identical inputs ⇒ byte-identical result; an empty change ⇒ empty (successful) route.
printfn "[F19] deterministic? %b" (Route.select f19Registry f19Report f19Findings = f19Result)
let f19Empty = Route.select f19Registry (Routing.route f19Facts []) (Findings.findUnknownGovernedPaths f19Facts (Routing.route f19Facts []))
printfn "[F19] empty-change → empty route, zero cost = %b" (List.isEmpty f19Empty.SelectedGates && f19Empty.Cost = { Cheap = 0; Medium = 0; High = 0; Exhaustive = 0 })

// ── F020: the route.json projection — RouteJson.ofRouteResult : RouteResult -> string ──
// A pure, total render of the F019 `RouteResult` into the deterministic, versioned `route.json`
// document text — the stable machine-readable contract every downstream consumer reads. It
// re-derives/re-sorts/re-classifies NOTHING (the result already fixed every order), emits no
// severity/enforcement/cache verdict/ship verdict/raw YAML/host path/clock/environment value, and
// is byte-identical for identical input. Serialization is the net10.0 shared-framework
// `System.Text.Json` (`Utf8JsonWriter`) — NO new dependency.

#r "../src/FS.GG.Governance.RouteJson/bin/Debug/net10.0/FS.GG.Governance.RouteJson.dll"

open FS.GG.Governance.RouteJson

// Project the genuine F019 `f19Result` (built above from the real F015->F017->F018->F019 chain).
let f20Json = RouteJson.ofRouteResult f19Result
printfn "\n[F20] schemaVersion = %s" RouteJson.schemaVersion
printfn "[F20] document (%d bytes):\n%s" f20Json.Length f20Json

// Determinism: a second projection of the same result is byte-identical.
printfn "[F20] deterministic? %b" (RouteJson.ofRouteResult f19Result = f20Json)

// The empty route projects to a valid document with empty sections + all-zero cost (never an error,
// never a "select everything" fallback).
let f20Empty = RouteJson.ofRouteResult f19Empty
printfn "[F20] empty route → %s" f20Empty

// ── F021: the gates.json projection — GatesJson.ofGateRegistry : GateRegistry -> string ──
// A pure, total render of the F018 `GateRegistry` into the deterministic, versioned `gates.json`
// WHOLE-CATALOG document text — the stable machine-readable gate-catalog contract every downstream
// consumer reads. Where F020's route.json is the PER-CHANGE view (from a RouteResult), this is the
// WHOLE-CATALOG view (every declared gate, from the GateRegistry). It re-derives/re-sorts/
// re-classifies NOTHING (the registry already fixed the GateId order), emits no severity/enforcement/
// cache verdict/selection/raw YAML/host path/clock/environment value, and is byte-identical for
// identical input. Serialization is the net10.0 shared-framework `System.Text.Json` — NO new
// dependency. The per-gate field set is exactly F020's selectedGates[*] entry MINUS selectingPaths.

#r "../src/FS.GG.Governance.GatesJson/bin/Debug/net10.0/FS.GG.Governance.GatesJson.dll"

open FS.GG.Governance.GatesJson

// Project the genuine F018 `f18Registry` (built above from the real F014->F018 facts).
let f21Json = GatesJson.ofGateRegistry f18Registry
printfn "\n[F21] schemaVersion = %s" GatesJson.schemaVersion
printfn "[F21] document (%d bytes):\n%s" f21Json.Length f21Json

// Determinism: a second projection of the same registry is byte-identical.
printfn "[F21] deterministic? %b" (GatesJson.ofGateRegistry f18Registry = f21Json)

// The empty registry projects to a valid document with an empty gates array (never an error, never a
// placeholder gate). Build a real empty registry from facts with no declared checks.
let f21EmptyFacts =
    { f18Facts with
        Capabilities = { f18Facts.Capabilities with Checks = [] } }
let f21Empty = GatesJson.ofGateRegistry (Gates.buildRegistry f21EmptyFacts)
printfn "[F21] empty registry → %s" f21Empty

// ── F022: the `fsgg route` host command — the first COMPOSITION/EDGE tier ──
// The PURE MVU boundary (Loop) wires the eight cores end-to-end and the EDGE (Interpreter) executes
// the requested I/O through injected ports. Here we walk ONLY the pure side (no I/O): parse → init →
// feed literal Msgs through update, printing the running Phase/Exit and emitted Effects, then render
// the summary both ways and print exitCode for each ExitDecision. The artifacts are the F020/F021
// projections byte-for-byte — this row serializes nothing of its own and computes NO ship verdict.

#r "../src/FS.GG.Governance.RouteCommand/bin/Debug/net10.0/FS.GG.Governance.RouteCommand.dll"

open FS.GG.Governance.RouteCommand

// Parse a real invocation. ExplicitPaths bypasses git diff (research D4), so `init` emits LoadCatalog
// directly and we never need a sensed snapshot to walk the composition.
let f22Parsed = Loop.parse [ "route"; "--paths"; "src/Lib/Thing.fs" ]
printfn "\n[F22] parse → %A" f22Parsed

match f22Parsed with
| Error e -> printfn "[F22] usage error: %A" e
| Ok req ->
    let m0, e0 = Loop.init req
    printfn "[F22] init  Phase=%A Exit=%A effects=%A" m0.Phase m0.Exit e0

    // Feed the validated F014 facts straight through the single Loaded(Valid) transition — the cores
    // run in-process and both documents are projected before either write effect is emitted.
    let m1, e1 = Loop.update (Loop.Loaded(Valid f18Facts)) m0
    printfn "[F22] loaded Phase=%A effects=%A" m1.Phase (e1 |> List.map (function Loop.WriteArtifact(k, p, _) -> sprintf "Write(%A,%s)" k p | x -> string x))

    let m2, e2 = Loop.update (Loop.Wrote(Loop.GatesArtifact, Ok())) m1
    let m3, e3 = Loop.update (Loop.Wrote(Loop.RouteArtifact, Ok())) m2
    printfn "[F22] wrote×2 Phase=%A nextEffects=%A" m3.Phase (e3 |> List.map (function Loop.EmitSummary _ -> "EmitSummary" | x -> string x))

    let m4, _ = Loop.update Loop.Emitted m3
    printfn "[F22] done  Phase=%A Exit=%A exitCode=%d" m4.Phase m4.Exit (Loop.exitCode m4.Exit)
    printfn "[F22] render Text:\n%s" (Loop.render m3 Loop.Text)
    printfn "[F22] render Json: %s" (Loop.render m3 Loop.Json)

// The exit taxonomy (research D6): Success 0, UsageError' 2, InputUnavailable 3, ToolError 4.
printfn "[F22] exit codes: %A"
    [ Loop.exitCode Loop.Success; Loop.exitCode Loop.UsageError'; Loop.exitCode Loop.InputUnavailable; Loop.exitCode Loop.ToolError ]

// ── F023: enforcement levers & effective severity — the first Phase-5 pure core ──
// Design-first sketch (Principle I): exercises the PUBLIC Enforcement surface the way a downstream
// `fsgg ship` / `audit.json` caller will. PURE and TOTAL — no I/O, no clock, never throws; the
// derivation maps (base severity, maturity, run mode, profile) -> (effective severity, reason). While
// Enforcement.fs is the `failwith "F023"` stub this block THROWS at runtime; it runs green only once
// Foundation + US1 land (T025 re-runs it end-to-end against the real body).

#r "../src/FS.GG.Governance.Enforcement/bin/Debug/net10.0/FS.GG.Governance.Enforcement.dll"

open FS.GG.Governance.Config.Model        // Maturity, ProfileId
open FS.GG.Governance.Enforcement.Enforcement

// The design's worked example — base blocking, block-on-ship, inner, light ⇒ advisory.
let f23Input: EnforcementInput =
    { BaseSeverity = Blocking; Maturity = BlockOnShip; Mode = Inner; Profile = Light }
let f23Example = deriveEffectiveSeverity f23Input
printfn "\n[F23] worked example  effective=%A base=%A" f23Example.EffectiveSeverity f23Example.BaseSeverity
printfn "[F23] reason: %s" f23Example.Reason
// expect: effective=Advisory base=Blocking; reason names 'light'/'block-on-ship'/'gate'/'inner'

// Same finding at the gate ⇒ blocking.
let f23AtGate = deriveEffectiveSeverity { f23Input with Mode = Gate }
printfn "[F23] at gate         effective=%A" f23AtGate.EffectiveSeverity   // expect Blocking

// observe withholds blocking under any mode/profile.
let f23Observed =
    deriveEffectiveSeverity
        { BaseSeverity = Blocking; Maturity = Observe; Mode = RunMode.Release; Profile = Profile.Release }
printfn "[F23] observe withhold effective=%A" f23Observed.EffectiveSeverity   // expect Advisory

// Determinism — derive twice, assert byte-identical.
let f23Twice = deriveEffectiveSeverity f23Input
printfn "[F23] deterministic?  %b"
    (f23Example.EffectiveSeverity = f23Twice.EffectiveSeverity && f23Example.Reason = f23Twice.Reason)

// Total recognition — canonical maps, unknown carried, never throws.
printfn "[F23] recognize: %A | %A | %A"
    (recognizeMode "gate") (recognizeMode "ship") (recognizeProfile "strict")
// expect: Recognized Gate | Unrecognized "ship" | Recognized Strict

// ── F024: ship verdict rollup — the second Phase-5 pure core ──
// Design-first sketch (Principle I): exercises the PUBLIC Ship surface the way a downstream
// `audit.json` / `fsgg ship` caller will. PURE and TOTAL — no I/O, no clock, never throws; `rollup`
// maps (RouteResult, RunMode, Profile) -> ShipDecision. While Ship.fs is the `failwith "F024"` stub
// this block THROWS at runtime; it runs green only once Foundation + US1 land (T025 re-runs it
// end-to-end against the real body).

#r "../src/FS.GG.Governance.Ship/bin/Debug/net10.0/FS.GG.Governance.Ship.dll"

open FS.GG.Governance.Gates.Model            // Gate, GateId, Maturity (via Config)
open FS.GG.Governance.Findings.Model          // findings
open FS.GG.Governance.Route.Model             // RouteResult, SelectedGate, CostRollup
open FS.GG.Governance.Ship.Model              // Verdict, ExitCodeBasis, EnforcedItem, ShipDecision
open FS.GG.Governance.Ship.Ship               // rollup

// Minimal real fixtures (the rollup reads only Id + Maturity from each gate).
let f24Gate (raw: string) (maturity: Maturity) : SelectedGate =
    let domain = DomainId "build"
    { Gate =
        { Id = GateId raw
          Domain = domain
          Description = raw
          Prerequisites = []
          Cost = Cheap
          Timeout = TimeoutLimit 60
          Owner = Owner "team"
          Maturity = maturity
          ProductCheck = false
          FreshnessKey = { Check = CheckId raw; Domain = domain; Cost = Cheap; Environment = Local; Command = None } }
      SelectingPaths = [] }

let f24Route (gates: SelectedGate list) : RouteResult =
    { SelectedGates = gates; Findings = { Findings = [] }; Cost = { Cheap = 0; Medium = 0; High = 0; Exhaustive = 0 } }

// Empty route at gate/standard ⇒ clean pass.
let f24Empty = rollup (f24Route []) Gate Standard
printfn "\n[F24] empty route  Verdict=%A Blockers=%d Warnings=%d Passing=%d Exit=%A"
    f24Empty.Verdict f24Empty.Blockers.Length f24Empty.Warnings.Length f24Empty.Passing.Length f24Empty.ExitCodeBasis
// expect: Verdict=Pass Blockers=0 Warnings=0 Passing=0 Exit=Clean

// A block-on-ship gate at inner/light ⇒ one warning (relaxed, effective Advisory).
let f24Warn = rollup (f24Route [ f24Gate "build:ship" BlockOnShip ]) Inner Light
printfn "[F24] inner/light  Verdict=%A Warnings=%d (effective=%A)"
    f24Warn.Verdict f24Warn.Warnings.Length (f24Warn.Warnings |> List.tryHead |> Option.map (fun i -> i.Decision.EffectiveSeverity))
// expect: Verdict=Pass Warnings=1 (effective=Some Advisory)

// A block-on-ship + block-on-release pair at gate/light ⇒ Fail; one blocker, one warning.
let f24Fail = rollup (f24Route [ f24Gate "build:ship" BlockOnShip; f24Gate "build:rel" BlockOnRelease ]) Gate Light
printfn "[F24] gate/light   Verdict=%A Blockers=%d Warnings=%d Exit=%A"
    f24Fail.Verdict f24Fail.Blockers.Length f24Fail.Warnings.Length f24Fail.ExitCodeBasis
// expect: Verdict=Fail Blockers=1 Warnings=1 Exit=Blocked

// ── F025: the audit.json projection — AuditJson.ofShipDecision : ShipDecision -> string ──
// The pure, total emit of the F024 `ShipDecision` to the deterministic, versioned `audit.json`
// WHOLE-CHANGE verdict document. Design-first record (Principle I): this throws while the body is the
// `failwith` stub and runs green once US1 lands (T028 re-runs it end-to-end).
#r "../src/FS.GG.Governance.AuditJson/bin/Debug/net10.0/FS.GG.Governance.AuditJson.dll"

open FS.GG.Governance.AuditJson

printfn "\n[F25] schemaVersion = %s" AuditJson.schemaVersion   // expect: fsgg.audit/v1

// A failing decision (BlockOnShip blocker + BlockOnRelease relaxed-to-Advisory warning) at gate/light.
let f25Json = AuditJson.ofShipDecision f24Fail
printfn "[F25] failing audit.json (%d bytes):\n%s" f25Json.Length f25Json
// expect: verdict:"fail", exitCodeBasis:"blocked"; a warning with baseSeverity:"blocking" +
//         effectiveSeverity:"advisory" (the no-hide case).

// Determinism: a second projection is byte-identical.
printfn "[F25] deterministic? %b" (AuditJson.ofShipDecision f24Fail = f25Json)   // expect: true

// The empty/clean decision projects to the empty-but-valid document (three present empty arrays).
let f25Empty = AuditJson.ofShipDecision (rollup (f24Route []) Gate Standard)
printfn "[F25] empty/clean audit.json:\n%s" f25Empty
// expect: { "schemaVersion":"fsgg.audit/v1","verdict":"pass","exitCodeBasis":"clean",
//           "blockers":[],"warnings":[],"passing":[] }

// ── F026: the `fsgg ship` host command — the protected-branch verdict COMPOSITION/EDGE tier ──
// The second host edge. Like F022 `route` it parses argv to a normalized request, but it rolls the
// routed change up into a pass/fail MERGE VERDICT (F024), projects it to `audit.json` (F025), and maps
// the verdict's ExitCodeBasis to a BLOCKING process exit code. Design-first FSI proof (Principle I):
// exercise parse/exitCode and the pure `update` step shape without any I/O.
#r "../src/FS.GG.Governance.ShipCommand/bin/Debug/net10.0/FS.GG.Governance.ShipCommand.dll"

open FS.GG.Governance.ShipCommand

// parse: defaults gate/standard, AuditOut readiness/audit.json; tolerates the leading `ship` verb.
let f26Req =
    match Loop.parse [ "ship"; "--since"; "HEAD~1" ] with
    | Ok r -> r
    | Error e -> failwithf "unexpected parse error: %A" e
printfn "\n[F26] parse defaults: Mode=%A Profile=%A AuditOut=%s" f26Req.Mode f26Req.Profile f26Req.AuditOut
// expect: Mode=Gate Profile=Standard AuditOut=readiness/audit.json

// An unrecognized lever is a UsageError decided in parse — before any port is built (no artifact).
printfn "[F26] unrecognized mode ⇒ %A" (Loop.parse [ "ship"; "--mode"; "bogus" ])
// expect: Error (UnrecognizedMode "bogus")

// exitCode taxonomy: the NEW Blocked=1, distinct from every tool-failure code 2/3/4.
printfn "[F26] exitCode  Success=%d Blocked=%d Usage=%d Input=%d Tool=%d"
    (Loop.exitCode Loop.Success) (Loop.exitCode Loop.Blocked) (Loop.exitCode Loop.UsageError')
    (Loop.exitCode Loop.InputUnavailable) (Loop.exitCode Loop.ToolError)
// expect: Success=0 Blocked=1 Usage=2 Input=3 Tool=4

// The pure `update` on Loaded(Valid) rolls up + projects and emits ONE WriteArtifact whose content is
// AuditJson.ofShipDecision (Ship.rollup result mode profile) — proven against the cores in LoopTests.
printfn "[F26] design-first surface exercised (update/render/exitCode) — see ShipCommand.Tests for the full proof"

// ── F029: the freshness-key computation core — the Phase-11 *Cost, Cache, Provenance* opening row ──
// The pure-core-first foundation of evidence reuse: a total `FreshnessKey.compute : FreshnessInputs -> Key`
// renders the closed freshness-input set into a deterministic, byte-stable, INJECTIVE canonical key, with
// `matches` (the reuse predicate) and `diff` (the no-hide explainer). No clock/fs/git/network; no cache,
// no hashing, no verdict. Design-first FSI proof (Principle I): construct a literal FreshnessInputs and
// exercise compute/matches/diff with the expected canonical key shape, order/dup invariance, a
// flipped-field non-match, and a command-less match.
#r "../src/FS.GG.Governance.FreshnessKey/bin/Debug/net10.0/FS.GG.Governance.FreshnessKey.dll"

open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey
open FS.GG.Governance.FreshnessKey.Model

// The worked example from contracts/freshness-key-format.md (covered artifacts deduped {h1,h2} + sorted).
let f29Inputs =
    { Check = CheckId "build:tests"
      Domain = DomainId "build"
      Command = Some(CommandId "dotnet")
      Environment = Local
      RuleHash = RuleHash "r1"
      CoveredArtifacts = [ ArtifactHash "h2"; ArtifactHash "h1"; ArtifactHash "h1" ]
      CommandVersion = Some(CommandVersion "8.0")
      GeneratorVersion = GeneratorVersion "g1"
      Base = Revision "aaa"
      Head = Revision "bbb" }

printfn "\n[F29] canonical key:\n%s" (FreshnessKey.value (FreshnessKey.compute f29Inputs))
// expect (joined by \n):
//   check=111:build:tests / domain=15:build / cmd=16:dotnet / env=15:local / rule=12:r1
//   art=2;2:h1;2:h2 / cmdv=13:8.0 / genv=12:g1 / base=13:aaa / head=13:bbb

// Order + duplication of covered artifacts never change the key (FR-004).
let f29Shuffled = { f29Inputs with CoveredArtifacts = [ ArtifactHash "h1"; ArtifactHash "h2"; ArtifactHash "h2" ] }
printfn "[F29] order/dup invariant ⇒ matches = %b" (FreshnessKey.matches f29Inputs f29Shuffled)
// expect: true

// A single flipped field forbids reuse and diff names exactly that category (FR-005, FR-007).
let f29NewRule = { f29Inputs with RuleHash = RuleHash "r2" }
printfn "[F29] flipped ruleHash ⇒ matches = %b, diff = %A"
    (FreshnessKey.matches f29Inputs f29NewRule)
    (FreshnessKey.diff f29Inputs f29NewRule |> List.map Model.categoryToken)
// expect: matches = false, diff = ["ruleHash"]

// A command-less gate (Command = None, CommandVersion = None) is a total, stable, matchable value (FR-011).
let f29NoCmd = { f29Inputs with Command = None; CommandVersion = None }
printfn "[F29] command-less self-match ⇒ %b" (FreshnessKey.matches f29NoCmd f29NoCmd)
// expect: true

// ── F030: the evidence-reuse decision core — the Phase-11 *Cost, Cache, Provenance* row 2 ──
// "Cache reusable evidence only when all freshness inputs match." A pure, total `decide : FreshnessInputs
// -> ReuseStore -> ReuseDecision` answers "may I reuse recorded evidence for this run — and if not, which
// input changed?": *Reuse* iff some recorded entry F029-`matches` the candidate on EVERY category, else
// *Recompute* with a located cause (`NoPriorEvidence` or `InputsChanged` of the F029 `diff` categories —
// the no-hide rule). `record` is the pure, de-duplicating insert (most-recent-wins, no mutation).
// `EvidenceRef` is an opaque edge-supplied token, never interpreted. No clock/fs/git/network; no
// persistence, eviction, gate, or verdict. Design-first FSI proof (Principle I), reusing F029's `f29Inputs`.
#r "../src/FS.GG.Governance.EvidenceReuse/bin/Debug/net10.0/FS.GG.Governance.EvidenceReuse.dll"

open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model

// Record the F029 worked example under an opaque evidence handle.
let f30Store = EvidenceReuse.record f29Inputs (EvidenceRef "ev-1") EvidenceReuse.empty

// A candidate identical in every freshness input ⇒ Reuse that handle (FR-004).
printfn "[F30] all inputs match ⇒ %A" (EvidenceReuse.decide f29Inputs f30Store)
// expect: Reuse (EvidenceRef "ev-1")

// A one-field-changed candidate (same gate) ⇒ Recompute, naming exactly the moved category (FR-006).
printfn "[F30] ruleHash moved ⇒ %A"
    (match EvidenceReuse.decide f29NewRule f30Store with
     | Recompute (InputsChanged cats) -> sprintf "Recompute (InputsChanged %A)" (cats |> List.map Model.categoryToken)
     | other -> sprintf "%A" other)
// expect: Recompute (InputsChanged ["ruleHash"])

// A different-gate candidate (no entry shares Check+Domain) ⇒ Recompute NoPriorEvidence.
let f30OtherGate = { f29Inputs with Domain = DomainId "release" }
printfn "[F30] different gate ⇒ %A" (EvidenceReuse.decide f30OtherGate f30Store)
// expect: Recompute NoPriorEvidence

// An empty store ⇒ Recompute NoPriorEvidence for any candidate.
printfn "[F30] empty store ⇒ %A" (EvidenceReuse.decide f29Inputs EvidenceReuse.empty)
// expect: Recompute NoPriorEvidence

// Re-recording the same inputs refreshes the handle with NO duplicate (most-recent-wins, FR-008).
let f30Refreshed = EvidenceReuse.record f29Inputs (EvidenceRef "ev-2") f30Store
printfn "[F30] refresh ⇒ %A, entries = %d"
    (EvidenceReuse.decide f29Inputs f30Refreshed)
    (EvidenceReuse.entries f30Refreshed |> List.length)
// expect: Reuse (EvidenceRef "ev-2"), entries = 1


// ── F031: the broad-route cost-explanation core — the Phase-11 *Cost, Cache, Provenance* row 3 ──
// "Explain high-cost routes with matched rule, changed path, affected capability, selected gate, cost, and
// cheaper local alternative." A pure, total `explain : RouteResult -> GateRegistry -> RouteExplanation`
// answers "which selected gates are high-cost, why is each on the route, and is there a cheaper gate in the
// same capability I could run locally first?": one `HighCostFinding` per selected gate whose declared
// `Cost >= High` (the fixed `highCostThreshold`), each embedding F019's `SelectedGate` VERBATIM and
// carrying its resolved `Alternative` — a same-domain, strictly-cheaper, locally-runnable registry gate
// (cheapest, ties by `GateId`) as `CheaperLocalAlternative`, else the explicit `NoCheaperLocalAlternative`
// (the no-hide rule). `Findings` is sorted by `GateId`; an empty route ⇒ `{ Findings = [] }`. No
// JSON/budget/severity/enforcement/freshness/ship verdict; no clock/fs/git/network. Design-first FSI proof
// (Principle I) over literal F018 `Gate` / F019 `SelectedGate` values.
#r "../src/FS.GG.Governance.RouteExplain/bin/Debug/net10.0/FS.GG.Governance.RouteExplain.dll"

open FS.GG.Governance.RouteExplain
open FS.GG.Governance.RouteExplain.Model

// A literal F018 `Gate` of the given domain/check/cost/environment (the worked example,
// contracts/explanation-semantics.md §2). The declared environment lives inside the gate's `FreshnessKey`.
let f31Gate (domain: string) (checkId: string) (cost: Cost) (env: EnvironmentClass) : Gate =
    { Id = GateId(domain + ":" + checkId)
      Domain = DomainId domain
      Description = sprintf "%s:%s" domain checkId
      Prerequisites = []
      Cost = cost
      Timeout = TimeoutLimit 60
      Owner = Owner "demo"
      Maturity = Observe
      ProductCheck = (env = Release)
      FreshnessKey =
        { Check = CheckId checkId
          Domain = DomainId domain
          Cost = cost
          Environment = env
          Command = None } }

// The catalog: build:full (Exhaustive Ci) + a cheaper local build:unit (Cheap Local) and
// build:integration (Medium LocalOrCi).
let f31Full = f31Gate "build" "full" Exhaustive Ci
let f31Unit = f31Gate "build" "unit" Cheap Local
let f31Integration = f31Gate "build" "integration" Medium LocalOrCi
let f31Registry: GateRegistry = { Gates = [ f31Full; f31Unit; f31Integration ] }

// A route selecting build:full, reached by one changed path.
let f31Selected: SelectedGate =
    { Gate = f31Full
      SelectingPaths = [ { Path = GovernedPath "src/a.fs"; MatchedGlob = GovernedPath "src/**" } ] }

let f31Route: RouteResult =
    { SelectedGates = [ f31Selected ]
      Findings = { Findings = [] }
      Cost = { Cheap = 0; Medium = 0; High = 0; Exhaustive = 0 } }

// The fixed MVP threshold is High.
printfn "[F31] highCostThreshold ⇒ %A" RouteExplain.highCostThreshold
// expect: High

// One finding for build:full, carrying its verbatim selecting path, with the cheapest local same-domain
// alternative (build:unit).
let f31Explanation = RouteExplain.explain f31Route f31Registry

printfn "[F31] findings ⇒ %A"
    (f31Explanation.Findings
     |> List.map (fun f ->
         let altId =
             match f.Alternative with
             | CheaperLocalAlternative g -> gateIdValue g.Id
             | NoCheaperLocalAlternative -> "<none>"

         gateIdValue f.Selected.Gate.Id, f.Selected.SelectingPaths.Length, altId))
// expect: [("build:full", 1, "build:unit")]

// Removing the cheaper same-domain gates ⇒ NoCheaperLocalAlternative (the explicit none).
let f31NoAlt = RouteExplain.explain f31Route { Gates = [ f31Full ] }
printfn "[F31] no cheaper gate ⇒ %A" ((f31NoAlt.Findings |> List.head).Alternative)
// expect: NoCheaperLocalAlternative

// A route of only Cheap/Medium gates ⇒ no high-cost route to explain.
let f31Cheap: RouteResult =
    { SelectedGates = [ { Gate = f31Unit; SelectingPaths = [] }; { Gate = f31Integration; SelectingPaths = [] } ]
      Findings = { Findings = [] }
      Cost = { Cheap = 0; Medium = 0; High = 0; Exhaustive = 0 } }

printfn "[F31] only cheap/medium gates ⇒ %A" (RouteExplain.explain f31Cheap f31Registry)
// expect: { Findings = [] }

// ── F032: the command-record core — the Phase-11 *Cost, Cache, Provenance* row 4 ──
// "Record command runs with executable, arguments, working directory, environment delta, timeout, exit
// code, stdout digest, stderr digest, captured output path, and duration." A pure, total
// `build : … -> CommandRecord` assembles the ten ALREADY-SENSED facts into one complete record =
// `{ Reproducible: ReproducibleFacts; Duration: SensedDuration }` — the sensed duration held STRUCTURALLY
// apart from the nine reproducible facts (D2). `canonicalId : CommandRecord -> CommandIdentity` renders
// ONLY `record.Reproducible` to a byte-stable identity in the F029 tagged/length-prefixed/injective
// discipline (D6): arguments in order (order-significant), each env-delta class as a SET (order/dup
// invariant), the duration NEVER read. No execution/process spawn, no byte hashing (digests are supplied
// opaque tokens — D3), no clock/fs/git/network, no JSON/persistence/provenance, no CLI. Reuses F014
// `TimeoutLimit` verbatim. Design-first FSI proof (Principle I) over literal run facts.
#r "../src/FS.GG.Governance.CommandRecord/bin/Debug/net10.0/FS.GG.Governance.CommandRecord.dll"

open FS.GG.Governance.CommandRecord
open FS.GG.Governance.CommandRecord.Model

// The worked example of contracts/command-record-identity-format.md: gcc -c main.c in /work, one added
// env var CI=1, timeout 30, exit 0, stdout/stderr digests, no captured-output file, some sensed duration.
let f32Env: EnvironmentDelta =
    { Added = [ { Name = EnvVarName "CI"; Value = EnvVarValue "1" } ]
      Changed = []
      Removed = [] }

let f32Record =
    CommandRecord.build
        (Executable "gcc")
        [ Argument "-c"; Argument "main.c" ]
        (WorkingDirectory "/work")
        f32Env
        (TimeoutLimit 30)
        (ExitCode 0)
        (OutputDigest "sha-out")
        (OutputDigest "sha-err")
        NoCapturedOutput
        (SensedDuration 123_456L)

// All ten facts read back verbatim (arguments in order; the sensed duration reachable apart).
printfn "[F32] exe/args/cwd ⇒ %A" (f32Record.Reproducible.Executable, f32Record.Reproducible.Arguments, f32Record.Reproducible.WorkingDirectory)
// expect: (Executable "gcc", [Argument "-c"; Argument "main.c"], WorkingDirectory "/work")
printfn "[F32] duration (sensed, apart) ⇒ %A" f32Record.Duration
// expect: SensedDuration 123456L

// Two records differing ONLY in duration ⇒ EQUAL identity (duration excluded — D2/FR-005).
let f32Faster = CommandRecord.build (Executable "gcc") [ Argument "-c"; Argument "main.c" ] (WorkingDirectory "/work") f32Env (TimeoutLimit 30) (ExitCode 0) (OutputDigest "sha-out") (OutputDigest "sha-err") NoCapturedOutput (SensedDuration 1L)
printfn "[F32] duration-only difference ⇒ equal id? %b" (CommandRecord.canonicalId f32Record = CommandRecord.canonicalId f32Faster)
// expect: true

// Flip one reproducible fact (an argument) ⇒ DIFFERENT identity (FR-006).
let f32OtherArg = CommandRecord.build (Executable "gcc") [ Argument "-c"; Argument "other.c" ] (WorkingDirectory "/work") f32Env (TimeoutLimit 30) (ExitCode 0) (OutputDigest "sha-out") (OutputDigest "sha-err") NoCapturedOutput (SensedDuration 123_456L)
printfn "[F32] flip an argument ⇒ different id? %b" (CommandRecord.canonicalId f32Record <> CommandRecord.canonicalId f32OtherArg)
// expect: true

// Reorder/duplicate the env-delta entries ⇒ UNCHANGED identity (env class is a SET — FR-007).
let f32EnvDup: EnvironmentDelta = { f32Env with Added = f32Env.Added @ f32Env.Added }
let f32EnvReordered = CommandRecord.build (Executable "gcc") [ Argument "-c"; Argument "main.c" ] (WorkingDirectory "/work") f32EnvDup (TimeoutLimit 30) (ExitCode 0) (OutputDigest "sha-out") (OutputDigest "sha-err") NoCapturedOutput (SensedDuration 123_456L)
printfn "[F32] duplicate env entry ⇒ unchanged id? %b" (CommandRecord.canonicalId f32Record = CommandRecord.canonicalId f32EnvReordered)
// expect: true

// NoCapturedOutput vs CapturedAt (CapturedOutputPath "") ⇒ DIFFERENT identity (absence ≠ empty path — D5).
let f32EmptyCap = CommandRecord.build (Executable "gcc") [ Argument "-c"; Argument "main.c" ] (WorkingDirectory "/work") f32Env (TimeoutLimit 30) (ExitCode 0) (OutputDigest "sha-out") (OutputDigest "sha-err") (CapturedAt(CapturedOutputPath "")) (SensedDuration 123_456L)
printfn "[F32] NoCapturedOutput vs empty path ⇒ different id? %b" (CommandRecord.canonicalId f32Record <> CommandRecord.canonicalId f32EmptyCap)
// expect: true

// The worked-example identity equals the contract's exact block.
printfn "[F32] worked-example identity:\n%s" (CommandRecord.identityValue (CommandRecord.canonicalId f32Record))
// expect:
// exe=13:gcc
// args=2;2:-c;6:main.c
// cwd=15:/work
// env+=1;n:2:CI|v:1:1
// env~=0;
// env-=0;
// to=12:30
// exit=11:0
// out=17:sha-out
// err=17:sha-err
// cap=0


// ── F033: the provenance core — the Phase-11 *Cost, Cache, Provenance* row 5 ──
// "Include source commit, base/head, rule hash, generator version, artifact digests, command records,
// environment class, and builder identity in provenance." A pure, total `build : … -> Provenance` assembles
// the nine ALREADY-SENSED facts into one flat complete `Provenance` carrying ALL eight declared facts (base
// and head are the two revisions of one base/head fact). `canonicalId : Provenance -> ProvenanceIdentity`
// renders ONLY the reproducible facts to a byte-stable identity in the F029/F032 tagged/length-prefixed/
// injective discipline (D5): the artifact digests as a SET (order/dup invariant), the command records in
// ORDER (order-significant), each command record folded via F032 `CommandRecord.canonicalId` so the sensed
// durations — held apart inside the embedded F032 records (D3) — are NEVER read. No sensing/timing/hashing/
// persistence/JSON/attestation/CLI. FIRST core to reference three sibling cores (FreshnessKey + CommandRecord
// + Config — D1), reusing F029 `Revision`/`RuleHash`/`GeneratorVersion`/`ArtifactHash`, F032 `CommandRecord`,
// and F014 `EnvironmentClass` verbatim. Design-first FSI proof (Principle I) over literal build facts.
#r "../src/FS.GG.Governance.FreshnessKey/bin/Debug/net10.0/FS.GG.Governance.FreshnessKey.dll"
#r "../src/FS.GG.Governance.Provenance/bin/Debug/net10.0/FS.GG.Governance.Provenance.dll"

open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.Provenance
open FS.GG.Governance.Provenance.Model

// The worked example of contracts/provenance-identity-format.md: source commit c0ffee; base base1; head
// head2; rule rule-x; generator gen-1; artifact digests a1,a2; ONE command record (the F032 worked example,
// reusing f32Env from above); environment Local; builder ci-runner.
let f33Record = CommandRecord.build (Executable "gcc") [ Argument "-c"; Argument "main.c" ] (WorkingDirectory "/work") f32Env (TimeoutLimit 30) (ExitCode 0) (OutputDigest "sha-out") (OutputDigest "sha-err") NoCapturedOutput (SensedDuration 123_456L)

let f33Prov =
    Provenance.build
        (Revision "c0ffee")
        (Revision "base1")
        (Revision "head2")
        (RuleHash "rule-x")
        (GeneratorVersion "gen-1")
        [ ArtifactHash "a1"; ArtifactHash "a2" ]
        [ f33Record ]
        Local
        (BuilderIdentity "ci-runner")

// All nine facts read back verbatim (command records whole, artifact digests as supplied).
printfn "[F33] src/base/head ⇒ %A" (f33Prov.SourceCommit, f33Prov.Base, f33Prov.Head)
// expect: (Revision "c0ffee", Revision "base1", Revision "head2")
printfn "[F33] artifact digests carried ⇒ %A" f33Prov.ArtifactDigests
// expect: [ArtifactHash "a1"; ArtifactHash "a2"]

// Two provenances differing ONLY in the embedded record's duration ⇒ EQUAL identity, while the durations
// still differ (sensed, structurally apart — D3/FR-005).
let f33Faster = { f33Prov with CommandRecords = [ CommandRecord.build (Executable "gcc") [ Argument "-c"; Argument "main.c" ] (WorkingDirectory "/work") f32Env (TimeoutLimit 30) (ExitCode 0) (OutputDigest "sha-out") (OutputDigest "sha-err") NoCapturedOutput (SensedDuration 1L) ] }
printfn "[F33] duration-only difference ⇒ equal id? %b (durations differ? %b)" (Provenance.canonicalId f33Prov = Provenance.canonicalId f33Faster) (f33Prov.CommandRecords.Head.Duration <> f33Faster.CommandRecords.Head.Duration)
// expect: true (durations differ? true)

// Flip one reproducible fact (the head revision) ⇒ DIFFERENT identity (FR-006).
let f33OtherHead = { f33Prov with Head = Revision "head9" }
printfn "[F33] flip head revision ⇒ different id? %b" (Provenance.canonicalId f33Prov <> Provenance.canonicalId f33OtherHead)
// expect: true

// Reorder/duplicate the artifact digests ⇒ UNCHANGED identity (artifact digests are a SET — FR-008).
let f33DigestsPermuted = { f33Prov with ArtifactDigests = [ ArtifactHash "a2"; ArtifactHash "a1"; ArtifactHash "a2" ] }
printfn "[F33] reorder/duplicate artifact digests ⇒ unchanged id? %b" (Provenance.canonicalId f33Prov = Provenance.canonicalId f33DigestsPermuted)
// expect: true

// Reorder the command records ⇒ CHANGED identity (command records are ORDERED — D4).
let f33SecondRecord = CommandRecord.build (Executable "ld") [] (WorkingDirectory "/work") { Added = []; Changed = []; Removed = [] } (TimeoutLimit 30) (ExitCode 0) (OutputDigest "o2") (OutputDigest "e2") NoCapturedOutput (SensedDuration 7L)
let f33Forward = { f33Prov with CommandRecords = [ f33Record; f33SecondRecord ] }
let f33Reversed = { f33Prov with CommandRecords = [ f33SecondRecord; f33Record ] }
printfn "[F33] reorder command records ⇒ changed id? %b" (Provenance.canonicalId f33Forward <> Provenance.canonicalId f33Reversed)
// expect: true

// The worked-example identity equals the contract's exact block (the embedded F032 id is 135 bytes).
printfn "[F33] worked-example identity:\n%s" (Provenance.identityValue (Provenance.canonicalId f33Prov))
// expect:
// src=16:c0ffee
// base=15:base1
// head=15:head2
// rule=16:rule-x
// gen=15:gen-1
// art=2;2:a1;2:a2
// cmds=1;135:exe=13:gcc
// args=2;2:-c;6:main.c
// cwd=15:/work
// env+=1;n:2:CI|v:1:1
// env~=0;
// env-=0;
// to=12:30
// exit=11:0
// out=17:sha-out
// err=17:sha-err
// cap=0
// env=15:local
// bld=19:ci-runner

// ── F034: the sensed-metadata marking + flagged-rendering core — the Phase-11 *Cost, Cache, Provenance* row 6
// (its sixth and final line) ──
// "Mark wall-clock timestamps and durations as sensed or non-deterministic metadata when included in
// deterministic reports." A pure, total `markDuration` / `markTimestamp` marks an ALREADY-MEASURED duration /
// timestamp (each with its label) as a `SensedMetadatum` whose `Value` is a closed `SensedValue` DU — THE TYPE
// IS THE FLAG: there is no representation of a marked timestamp or duration that is reproducible (D3, FR-001).
// `render : SensedMetadatum -> SensedRendering` renders ONE metadatum behind a RESERVED `!sensed!` marker in
// the F029/F032/F033 tagged/length-prefixed/INJECTIVE discipline (D4) — a form no reproducible field tag ever
// produces — so it is unmistakably distinguishable from a reproducible field and unspoofable by its data
// (FR-003/FR-004). `renderSection` groups a list into one order-preserving `!sensed-section!`. No
// sensing/timing/hashing/persistence/JSON/attestation/CLI; identity-NEUTRAL (D5, FR-006). References EXACTLY
// ONE sibling core — CommandRecord — reusing F032 `SensedDuration` VERBATIM for the duration kind (FR-008);
// the only genuinely new fact is `SensedTimestamp`. Design-first FSI proof (Principle I) over literal values.
#r "../src/FS.GG.Governance.SensedMetadata/bin/Debug/net10.0/FS.GG.Governance.SensedMetadata.dll"

open FS.GG.Governance.SensedMetadata
open FS.GG.Governance.SensedMetadata.Model

// The worked example of contracts/sensed-metadata-format.md: a duration labelled `elapsed` (1.83s) and a
// timestamp labelled `at`. The duration is a VERBATIM F032 `SensedDuration` (FR-008).
let f34Dur = SensedMetadata.markDuration (SensedLabel "elapsed") (SensedDuration 1_830_000_000L)
let f34Ts = SensedMetadata.markTimestamp (SensedLabel "at") (SensedTimestamp "2026-06-21T12:00:00Z")

// The kind is intrinsic to the value's case (D3) — sensed by construction, no reproducible variant.
printfn "[F34] kindOf duration/timestamp ⇒ %A / %A" (SensedMetadata.kindOf f34Dur) (SensedMetadata.kindOf f34Ts)
// expect: DurationKind / TimestampKind

// One metadatum renders behind the reserved `!sensed!` marker, length-prefixed and injective.
printfn "[F34] render duration ⇒ %s" (SensedMetadata.renderingValue (SensedMetadata.render f34Dur))
// expect: !sensed!=duration;7:elapsed;10:1830000000
printfn "[F34] render timestamp ⇒ %s" (SensedMetadata.renderingValue (SensedMetadata.render f34Ts))
// expect: !sensed!=timestamp;2:at;20:2026-06-21T12:00:00Z

// A list renders as one order-preserving, separable `!sensed-section!` (timestamp then duration).
printfn "[F34] renderSection [ ts; dur ] ⇒ %s" (SensedMetadata.renderingValue (SensedMetadata.renderSection [ f34Ts; f34Dur ]))
// expect: !sensed-section!=2;47:!sensed!=timestamp;2:at;20:2026-06-21T12:00:00Z;41:!sensed!=duration;7:elapsed;10:1830000000

// The empty list is an ordinary value, not an error.
printfn "[F34] renderSection [] ⇒ %s" (SensedMetadata.renderingValue (SensedMetadata.renderSection []))
// expect: !sensed-section!=0;

// The length prefix neutralizes spoofs: an empty label is a distinct `0:` form; a label whose text is itself
// `!sensed!` is read as 8 label bytes, never as a marker (FR-004).
printfn "[F34] empty-label zero-duration ⇒ %s" (SensedMetadata.renderingValue (SensedMetadata.render (SensedMetadata.markDuration (SensedLabel "") (SensedDuration 0L))))
// expect: !sensed!=duration;0:;1:0
printfn "[F34] label-is-!sensed! spoof ⇒ %s" (SensedMetadata.renderingValue (SensedMetadata.render (SensedMetadata.markTimestamp (SensedLabel "!sensed!") (SensedTimestamp "2026-06-21T12:00:00Z"))))
// expect: !sensed!=timestamp;8:!sensed!;20:2026-06-21T12:00:00Z

// ─────────────────────────────────────────────────────────────────────────────────────────────────────
// F035 — Agent-Review Verdict Cache-Key Core (FS.GG.Governance.AgentReviewKey)
// ─────────────────────────────────────────────────────────────────────────────────────────────────────
// Phase-12 opening row: cache agent-reviewed verdicts by the SEVEN identity tokens they depend on (model
// id, model version, reviewer prompt hash, model configuration, check hash, the SET of reviewed-artifact
// hashes, and the question text). The direct analogue of F029 `FreshnessKey` specialised to agent-reviewed
// verdicts: one pure, total `compute : AgentReviewInputs -> CacheKey` in the F029/F032/F033 tagged,
// length-prefixed, INJECTIVE discipline, plus the total cache-hit predicate `matches` and the no-hide
// explainer `diff`. The CHECK hash REUSES F029 `RuleHash` and the reviewed-artifact hashes REUSE F029
// `ArtifactHash` VERBATIM (FR-008); the only genuinely new types are the five opaque newtypes. No model
// invoked, no bytes hashed, no clock/filesystem/network read, no cached verdict carried, no cache
// store/lookup/invalidation run (FR-007/FR-009). Design-first FSI proof (Principle I) over literal values.
#r "../src/FS.GG.Governance.FreshnessKey/bin/Debug/net10.0/FS.GG.Governance.FreshnessKey.dll"
#r "../src/FS.GG.Governance.AgentReviewKey/bin/Debug/net10.0/FS.GG.Governance.AgentReviewKey.dll"

open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.AgentReviewKey
open FS.GG.Governance.AgentReviewKey.Model

// The worked example of contracts/agent-review-key-format.md. The CHECK hash is a verbatim F029 `RuleHash`,
// the reviewed artifacts are verbatim F029 `ArtifactHash`s — supplied (with a duplicate + out of order) as
// data; the key dedups to {h1,h2} and ordinally sorts (FR-008).
let f35Inputs =
    { Model = ModelId "claude-opus-4"
      ModelVersion = ModelVersion "20260101"
      Config = ModelConfig "temp=0"
      PromptHash = ReviewerPromptHash "p1"
      Question = QuestionText "explains API?"
      Check = RuleHash "c1"
      ReviewedArtifacts = [ ArtifactHash "h2"; ArtifactHash "h1"; ArtifactHash "h1" ] }

// The canonical worked-example key (seven tagged, length-prefixed segments joined by '\n', no trailing '\n').
printfn "[F35] compute |> value ⇒\n%s" (AgentReviewKey.value (AgentReviewKey.compute f35Inputs))
// expect:
// mid=13:claude-opus-4
// mver=8:20260101
// prompt=2:p1
// cfg=6:temp=0
// chk=2:c1
// art=2;2:h1;2:h2
// q=13:explains API?

// Reviewed artifacts are a SET: reordering ⇒ same key, `matches = true`, `diff = []` (FR-006).
let f35Reordered = { f35Inputs with ReviewedArtifacts = [ ArtifactHash "h1"; ArtifactHash "h2" ] }
printfn "[F35] reorder ⇒ matches=%b diff=%A" (AgentReviewKey.matches f35Inputs f35Reordered) (AgentReviewKey.diff f35Inputs f35Reordered)
// expect: matches=true diff=[]

// A single-input change (the model version) ⇒ not a match; `diff` names exactly that input (FR-004/FR-005).
let f35Flipped = { f35Inputs with ModelVersion = ModelVersion "20260202" }
printfn "[F35] modelVersion flip ⇒ matches=%b diff=%A token=%s"
    (AgentReviewKey.matches f35Inputs f35Flipped)
    (AgentReviewKey.diff f35Inputs f35Flipped)
    (AgentReviewKey.diff f35Inputs f35Flipped |> List.map Model.inputToken |> String.concat ",")
// expect: matches=false diff=[ModelVersionInput] token=modelVersion

// The empty reviewed-artifact set keys to the distinct `art=0;` form — never treated as "absent" (Edge case).
let f35Empty = { f35Inputs with ReviewedArtifacts = [] }
printfn "[F35] empty artifact set ⇒\n%s" (AgentReviewKey.value (AgentReviewKey.compute f35Empty))
// expect: …\nchk=2:c1\nart=0;\nq=13:explains API?

// ─────────────────────────────────────────────────────────────────────────────────────────────────────
// F036 — Agent-Reviewed Verdict Store & Invalidation Decision Core (FS.GG.Governance.VerdictReuse)
// ─────────────────────────────────────────────────────────────────────────────────────────────────────
// Phase-12 SECOND row: "invalidate cached verdicts when judge identity or prompt identity changes." The
// direct analogue of F030 EvidenceReuse specialised to agent-reviewed verdicts — it consumes F035
// `matches`/`diff` VERBATIM. Two pure, total operations: `lookup : AgentReviewInputs -> VerdictStore ->
// LookupDecision` (Valid iff some entry matches the request on every one of the seven inputs; else
// Invalidated with a LOCATED cause — NoCachedVerdict for different work, or InputsChanged naming exactly the
// moved inputs) and `record : AgentReviewInputs -> VerdictRef -> VerdictStore -> VerdictStore` (pure,
// de-duplicating, most-recent-wins insert). No persistence, no eviction, no key bytes computed, no model
// invoked, no clock/filesystem/network read; the `VerdictRef` is an opaque edge-minted token, never
// dereferenced (FR-009/FR-011). Design-first FSI proof (Principle I) over literal values — reuses F035's
// worked example `f35Inputs` as the cached identity.
#r "../src/FS.GG.Governance.VerdictReuse/bin/Debug/net10.0/FS.GG.Governance.VerdictReuse.dll"

open FS.GG.Governance.VerdictReuse
open FS.GG.Governance.VerdictReuse.Model

// Record the F035 worked-example identity under an opaque verdict reference, then probe the decisions.
let f36Store = VerdictReuse.record f35Inputs (VerdictRef "verdict:v1") VerdictReuse.empty

// A request equal on all seven inputs (artifacts compared as a SET) ⇒ Valid, reusing the cached reference.
printfn "[F36] exact match ⇒ %A" (VerdictReuse.lookup f35Inputs f36Store)
// expect: Valid (VerdictRef "verdict:v1")

// A model-version bump (judge identity) ⇒ Invalidated (InputsChanged [ModelVersionInput]); inputGroup ⇒ JudgeIdentity.
let f36JudgeBump = { f35Inputs with ModelVersion = ModelVersion "20260202" }
let f36JudgeDecision = VerdictReuse.lookup f36JudgeBump f36Store
printfn "[F36] model-version bump ⇒ %A groups=%A"
    f36JudgeDecision
    (match f36JudgeDecision with
     | Invalidated (InputsChanged inputs) -> inputs |> List.map Model.inputGroup
     | _ -> [])
// expect: Invalidated (InputsChanged [ModelVersionInput]) groups=[JudgeIdentity]

// A question change (prompt identity) ⇒ Invalidated (InputsChanged [QuestionTextInput]) — NOT NoCachedVerdict.
let f36PromptChange = { f35Inputs with Question = QuestionText "covers errors?" }
let f36PromptDecision = VerdictReuse.lookup f36PromptChange f36Store
printfn "[F36] question change ⇒ %A groups=%A"
    f36PromptDecision
    (match f36PromptDecision with
     | Invalidated (InputsChanged inputs) -> inputs |> List.map Model.inputGroup
     | _ -> [])
// expect: Invalidated (InputsChanged [QuestionTextInput]) groups=[PromptIdentity]

// A different check (different work) ⇒ Invalidated NoCachedVerdict (never a spurious input diff).
let f36OtherWork = { f35Inputs with Check = RuleHash "c-other" }
printfn "[F36] different work ⇒ %A" (VerdictReuse.lookup f36OtherWork f36Store)
// expect: Invalidated NoCachedVerdict

// Re-recording the SAME inputs refreshes (most-recent-wins) without accumulating a duplicate entry.
let f36Refreshed = VerdictReuse.record f35Inputs (VerdictRef "verdict:v2") f36Store
printfn "[F36] re-record same inputs ⇒ %A entries=%d"
    (VerdictReuse.lookup f35Inputs f36Refreshed)
    (VerdictReuse.entries f36Refreshed |> List.length)
// expect: Valid (VerdictRef "verdict:v2") entries=1
