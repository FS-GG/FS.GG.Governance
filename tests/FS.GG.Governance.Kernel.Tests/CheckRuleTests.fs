module FS.GG.Governance.Kernel.Tests.CheckRuleTests

open Expecto
open FsCheck
open FsCheck.FSharp
open FS.GG.Governance.Kernel

// Semantic tests for the CheckTier arbitration model & the Rule bridge (F04), written
// against the PUBLIC surface (Principle I) with REAL CheckRule/Bridge values and a real
// in-test adapter 'fact (Principle V) — no synthetic fixtures. V13–V22 map to the
// quickstart/tasks validation table.
//
// Evidence note (T018): F04 is the pure functional core — Principle IV (Elmish/MVU) is
// N/A (the dispatching update/interpreter is F08). All evidence here is REAL: real
// CheckRule/Bridge values and a real in-test adapter 'fact (`Gov` — exactly the shape
// F09 will materialise) — there are no synthetic fixtures and no `// SYNTHETIC:`
// disclosures in this feature.

// ── A real in-test adapter 'fact: either a governance outcome or an artifact-content
//    fact (the shape a domain adapter F09 materialises) — real evidence, not a mock. ──
type Gov =
    | GovOut of RuleOutcome
    | Art of kind: string * key: string * hash: string

let private bridge: Bridge<Gov> =
    { Judge = { ModelId = "claude-opus-4-8"; Version = "2026-06" }
      ArtifactHash =
        fun facts ref ->
            facts
            |> List.tryPick (fun f ->
                match f.Value with
                | Art(k, key, h) when k = ref.Kind && key = ref.Key -> Some h
                | _ -> None)
            |> Option.defaultValue "" // unknown artifact ⇒ fixed sentinel, never an exception
      Embed = GovOut
      Project = (fun f -> match f with GovOut o -> Some o | _ -> None) }

let private spec = { Document = "wcag"; Section = "1.4.3" }

let private mkFact (id: string) (v: Gov) : FactAssertion<Gov> =
    { Id = FactId id; Value = v; Provenance = [] }

// Probes over Gov, built through the PUBLIC smart constructor (SC-007).
let private mk name reads outcome : Check<Gov> = Check.probe name reads [] (fun _ -> outcome)
let private metCheck = mk "ok" [] Met
let private failCheck = mk "bad" [] (Unmet "nope")
let private uncertainCheck = mk "dunno" [] (Unknown "tbd")
let private opaqueCheck: Check<Gov> = Opaque("judge", fun _ -> Met)

let private okRule r =
    match r with
    | Ok x -> x
    | Error e -> failtestf "expected Ok, got Error %A" e

// The governance outcomes a bridged rule asserts (projected back through the Bridge).
let private outcomes (r: CheckRule<Gov>) (facts: FactSet<Gov>) =
    (CheckRule.toRule bridge r).Apply facts
    |> List.choose (fun f -> bridge.Project f.Value)

// The key a bridged AgentReviewed rule computes for a fact set (the same fold toRule uses).
let private keyOf (facts: FactSet<Gov>) (r: CheckRule<Gov>) =
    CheckRule.cacheKey
        bridge.Judge
        (Check.hash r.Check)
        (Check.reads r.Check |> List.map (bridge.ArtifactHash facts))
        r.Question

// ── FsCheck: non-null strings only (digest over null would throw; real keys are never
//    null — they come from Check.hash / a JudgeId). Overrides string generation. ──
let private genStr =
    Gen.elements [ ""; "a"; "b"; "c"; "spacing"; "tone?"; "x; y"; "4.5"; "claude-opus"; "2026-06" ]

type CkArb =
    static member String() = Arb.fromGen genStr

let private propConfig =
    { FsCheckConfig.defaultConfig with
        maxTest = 300
        arbitrary = [ typeof<CkArb> ]
        replay = Some(1234UL, 5678UL, None) } // fixed seed → reproducible (cf. F01/F02/F03)

[<Tests>]
let tests =
    testList
        "CheckRule"
        [
          // ── User Story 3: the reified-ness guardrail on the Deterministic tier ──

          test "V13 rule: Deterministic refuses an opaque check iff non-reified; every other tier accepts (US3 AS1-3, FR-006, SC-001, INV-1)" {
              let id = RuleId "judge"
              // CheckRule contains functions (no equality), so refusals are pattern-matched.
              let refusedWith r =
                  match r with
                  | Error(OpaqueCannotBeDeterministic rid) -> rid = id
                  | Ok _ -> false
              let accepted r = match r with Ok _ -> true | Error _ -> false
              Expect.isTrue (refusedWith (CheckRule.rule id Deterministic spec opaqueCheck)) "Deterministic over an Opaque check is refused with the rule id"
              // A nested Opaque is refused too (isReified walks the whole structure).
              Expect.isTrue (refusedWith (CheckRule.rule id Deterministic spec (All [ metCheck; opaqueCheck ]))) "Deterministic over a check containing an Opaque anywhere is refused"
              // The same opaque check at AgentReviewed / HumanOnly is accepted.
              Expect.isTrue (accepted (CheckRule.rule id AgentReviewed spec opaqueCheck)) "AgentReviewed accepts an opaque check"
              Expect.isTrue (accepted (CheckRule.rule id HumanOnly spec opaqueCheck)) "HumanOnly accepts an opaque check"
              // A fully reified check at Deterministic is accepted.
              Expect.isTrue (accepted (CheckRule.rule id Deterministic spec metCheck)) "Deterministic accepts a reified check"
          }

          // ── User Story 2: reproducible, cacheable agent reviews ──

          testPropertyWithConfig propConfig "V14 cacheKey: identical ingredients ⇒ identical key; any one ingredient changing ⇒ a different key (US2 AS1/2, FR-011/012, SC-002, INV-2)" <|
              fun (modelId: string) (version: string) (checkHash: string) (arts: string list) (prompt: string) ->
                  let judge = { ModelId = modelId; Version = version }
                  let q = Some prompt
                  let k = CheckRule.cacheKey judge checkHash arts q
                  let reproducible = k = CheckRule.cacheKey judge checkHash arts q
                  let diffModel = CheckRule.cacheKey { judge with ModelId = modelId + "!" } checkHash arts q <> k
                  let diffVersion = CheckRule.cacheKey { judge with Version = version + "!" } checkHash arts q <> k
                  let diffHash = CheckRule.cacheKey judge (checkHash + "!") arts q <> k
                  // Changing the content of an artifact hash (meaningful only when non-empty).
                  let diffArt =
                      List.isEmpty arts
                      || CheckRule.cacheKey judge checkHash (arts |> List.map (fun s -> s + "!")) q <> k
                  let diffPrompt = CheckRule.cacheKey judge checkHash arts (Some(prompt + "!")) <> k
                  reproducible && diffModel && diffVersion && diffHash && diffArt && diffPrompt

          test "V15 cacheKey: artifact half is order- and duplicate-independent; empty set still stable and varies with check hash + judge (US2 edge, FR-012, INV-3)" {
              let judge = bridge.Judge
              let arts = [ "h1"; "h2"; "h3" ]
              let k = CheckRule.cacheKey judge "ch" arts (Some "q")
              Expect.equal (CheckRule.cacheKey judge "ch" [ "h3"; "h1"; "h2" ] (Some "q")) k "permuted artifact hashes ⇒ same key"
              Expect.equal (CheckRule.cacheKey judge "ch" [ "h1"; "h1"; "h2"; "h3"; "h3" ] (Some "q")) k "duplicated artifact hashes ⇒ same key"
              // Empty read set: stable, and still varies with the check hash + judge identity.
              let e = CheckRule.cacheKey judge "ch" [] (Some "q")
              Expect.equal (CheckRule.cacheKey judge "ch" [] (Some "q")) e "empty artifact set ⇒ stable key"
              Expect.notEqual (CheckRule.cacheKey judge "ch2" [] (Some "q")) e "empty set: key still varies with the check hash"
              Expect.notEqual (CheckRule.cacheKey { judge with ModelId = "other" } "ch" [] (Some "q")) e "empty set: key still varies with the judge"
              // The None reviewer-prompt is a stable, distinct ingredient.
              Expect.notEqual (CheckRule.cacheKey judge "ch" [] None) e "None prompt ⇒ a different key than Some"
          }

          test "V16 AgentReviewed: a recorded review with the matching key ⇒ cache HIT — Decided, zero NeedsReview, provenance carries the review's FactId (US2 AS3, FR-009, SC-003, INV-4)" {
              let id = RuleId "judge"
              let r = okRule (CheckRule.rule id AgentReviewed spec opaqueCheck) |> CheckRule.asking "Is the tone professional?"
              let k = keyOf [] r
              let recorded = mkFact "rev1" (GovOut(Reviewed { Rule = id; Key = k; Verdict = Pass }))
              let emitted = (CheckRule.toRule bridge r).Apply [ recorded ]
              let outs = emitted |> List.choose (fun f -> bridge.Project f.Value)
              Expect.equal outs [ Decided(id, Pass) ] "cache hit ⇒ emits exactly Decided(id, recorded verdict)"
              Expect.isEmpty (outs |> List.filter (function NeedsReview _ -> true | _ -> false)) "cache hit ⇒ zero NeedsReview (no agent call)"
              let inputs = emitted |> List.collect (fun f -> f.Provenance) |> List.collect (fun p -> p.Inputs)
              Expect.equal inputs [ recorded.Id ] "cache hit ⇒ provenance Inputs = the consumed RecordedReview's FactId"
          }

          test "V17 AgentReviewed: no matching recorded review ⇒ cache MISS — exactly one NeedsReview carrying the key (US2 AS4, FR-009, SC-003, INV-5)" {
              let id = RuleId "judge"
              let r = okRule (CheckRule.rule id AgentReviewed spec opaqueCheck) |> CheckRule.asking "Is the tone professional?"
              // No facts, and a non-matching recorded review under a different key — both miss.
              let nonMatching = mkFact "other" (GovOut(Reviewed { Rule = id; Key = "not-the-key"; Verdict = Pass }))
              for facts in [ []; [ nonMatching ] ] do
                  let outs = outcomes r facts
                  let requests = outs |> List.choose (function NeedsReview req -> Some req | _ -> None)
                  Expect.equal requests [ { Rule = id; Question = Some "Is the tone professional?"; Key = keyOf facts r } ] "cache miss ⇒ exactly one NeedsReview carrying the key"
                  Expect.isEmpty (outs |> List.filter (function Decided _ -> true | _ -> false)) "cache miss ⇒ no Decided"
          }

          test "V18 AgentReviewed: a verdict recorded under an old judge/prompt no longer matches once the judge or the question changes ⇒ fresh NeedsReview (US2 AS5, FR-013, SC-004, INV-6)" {
              let id = RuleId "judge"
              let r = okRule (CheckRule.rule id AgentReviewed spec opaqueCheck) |> CheckRule.asking "Is the tone professional?"
              let k = keyOf [] r
              let recorded = mkFact "rev" (GovOut(Reviewed { Rule = id; Key = k; Verdict = Pass }))

              // (a) Judge change: bridge2's judge differs ⇒ the old key misses, fresh request under the new key.
              let bridge2 = { bridge with Judge = { bridge.Judge with Version = "2099-01" } }
              let k2 =
                  CheckRule.cacheKey
                      bridge2.Judge
                      (Check.hash r.Check)
                      (Check.reads r.Check |> List.map (bridge2.ArtifactHash [ recorded ]))
                      r.Question
              Expect.notEqual k2 k "a judge change changes the key"
              let outs2 = (CheckRule.toRule bridge2 r).Apply [ recorded ] |> List.choose (fun f -> bridge2.Project f.Value)
              Expect.equal outs2 [ NeedsReview { Rule = id; Question = r.Question; Key = k2 } ] "stale verdict under an old judge ⇒ fresh NeedsReview"

              // (b) Prompt change: same judge, different Question ⇒ a different key ⇒ miss.
              let r2 = r |> CheckRule.asking "A different reviewer question entirely?"
              Expect.notEqual (keyOf [] r2) k "a prompt change changes the key"
              let outs3 = outcomes r2 [ recorded ]
              Expect.equal outs3 [ NeedsReview { Rule = id; Question = r2.Question; Key = keyOf [] r2 } ] "stale verdict under an old prompt ⇒ fresh NeedsReview"
          }

          // ── User Story 1: bridge a tiered rule into the executable kernel ──

          test "V19 Deterministic: Apply asserts Decided(id, eval) verbatim (Uncertain not coerced); Description = render for every rule (US1 AS1/2, FR-007/008, SC-005/006, INV-7/8)" {
              let cases = [ metCheck, Pass; failCheck, Fail "nope"; uncertainCheck, Uncertain "tbd" ]
              for chk, expected in cases do
                  let id = RuleId "det"
                  let r = okRule (CheckRule.rule id Deterministic spec chk)
                  let kr = CheckRule.toRule bridge r
                  Expect.equal (Check.eval [] chk) expected "sanity: the check evaluates as expected"
                  Expect.equal (outcomes r []) [ Decided(id, Check.eval [] chk) ] "Deterministic ⇒ Decided(id, eval) verbatim (Uncertain preserved)"
                  Expect.equal kr.Description (Check.render chk) "Description = Check.render (no drift)"
          }

          test "V20 HumanOnly: Apply asserts Escalated id and never a Decided verdict (US1 AS3, FR-010, INV-9)" {
              let id = RuleId "human"
              let r = okRule (CheckRule.rule id HumanOnly spec metCheck)
              let outs = outcomes r []
              Expect.equal outs [ Escalated id ] "HumanOnly ⇒ exactly Escalated id"
              Expect.isEmpty (outs |> List.filter (function Decided _ -> true | _ -> false)) "HumanOnly ⇒ never a Decided verdict"
          }

          // ── User Story 4: severity and spec provenance, orthogonal to tier ──

          test "V21 severity ⟂ tier: defaults Advisory; blocking flips severity leaving tier; every tier×severity recorded independently; HumanOnly escalates either way; Spec recoverable (US4 AS1-4, FR-002/003/010, SC-008, INV-9)" {
              let id = RuleId "r"
              let baseRule = okRule (CheckRule.rule id AgentReviewed spec opaqueCheck)
              Expect.equal baseRule.Severity Advisory "an undeclared severity defaults to Advisory"
              let promoted = CheckRule.blocking baseRule
              Expect.equal promoted.Severity Blocking "blocking sets Severity = Blocking"
              Expect.equal promoted.Tier baseRule.Tier "blocking leaves the tier unchanged"
              // Every tier × severity recorded independently.
              for tier in [ Deterministic; AgentReviewed; HumanOnly ] do
                  let chk = if tier = Deterministic then metCheck else opaqueCheck
                  let ar = okRule (CheckRule.rule id tier spec chk)
                  Expect.equal ar.Severity Advisory "advisory default holds for every tier"
                  Expect.equal ar.Tier tier "tier recorded independently of severity"
                  let br = CheckRule.blocking ar
                  Expect.equal br.Severity Blocking "blocking holds for every tier"
                  Expect.equal br.Tier tier "tier unchanged under blocking for every tier"
              // HumanOnly escalates whether Advisory or Blocking.
              let h = okRule (CheckRule.rule id HumanOnly spec metCheck)
              Expect.equal (outcomes h []) [ Escalated id ] "HumanOnly Advisory ⇒ Escalated"
              Expect.equal (outcomes (CheckRule.blocking h) []) [ Escalated id ] "HumanOnly Blocking ⇒ Escalated (severity-independent)"
              // The SpecSource travels with the authored rule for provenance.
              Expect.equal baseRule.Spec spec "Spec (SpecSource) recoverable from the authored rule"
          }

          // ── Polish: totality and the FR-015 no-I/O boundary ──

          test "V22 totality: toRule + Apply over every tier, empty All/Any, empty facts, and an unknown artifact never throw; the AgentReviewed miss emits NeedsReview as data (FR-015/017, SC-007, INV-10/11)" {
              let id = RuleId "t"
              let reading = Check.probe "r" [ { Kind = "token"; Key = "absent" } ] [] (fun (_: FactSet<Gov>) -> Met)
              let checks: Check<Gov> list =
                  [ Check.allOf []
                    Check.anyOf []
                    metCheck
                    reading
                    All [ opaqueCheck; failCheck ]
                    Implies(metCheck, Check.anyOf []) ]
              let factSets: FactSet<Gov> list = [ []; [ mkFact "a" (Art("token", "other", "h")) ] ]
              for tier in [ Deterministic; AgentReviewed; HumanOnly ] do
                  for chk in checks do
                      // Deterministic refuses a non-reified check — author it AgentReviewed instead.
                      let authored =
                          match tier with
                          | Deterministic when not (Check.isReified chk) -> CheckRule.rule id AgentReviewed spec chk
                          | _ -> CheckRule.rule id tier spec chk
                      match authored with
                      | Ok r ->
                          let kr = CheckRule.toRule bridge r
                          for facts in factSets do
                              kr.Apply facts |> ignore // empty facts + unknown artifact (sentinel path) — never throws
                      | Error _ -> ()
              Expect.isTrue true "toRule + Apply total over every tier / check / fact set"
              // FR-015 boundary: the artifact hash for an unknown read came only from
              // bridge.ArtifactHash (sentinel ""), and the miss emits a NeedsReview VALUE —
              // no agent call, no I/O performed by the kernel.
              let ar = okRule (CheckRule.rule id AgentReviewed spec reading)
              let outs = outcomes ar []
              Expect.isTrue (outs |> List.exists (function NeedsReview _ -> true | _ -> false)) "AgentReviewed miss emits a NeedsReview value (no I/O, no agent call)"
          } ]
