module FS.GG.Governance.Kernel.Tests.FixedPointTests

open Expecto
open FS.GG.Governance.Kernel

// ── Toy domain (real facts/rules/evaluation — no synthetic fixtures, Principle V) ──
//
// The fact type is a plain string; `identify` makes the string itself the fact's
// identity (D3). Rules pattern-match their OWN fact type freely — opacity (FR-009)
// constrains the kernel, not the caller's rules.

let identify (s: string) = FactId s

let supplied (names: string list) : FactSet<string> =
    names |> List.map (fun n -> { Id = FactId n; Value = n; Provenance = [] })

/// A monotonic rule: when every `inputs` fact is present, assert `output`,
/// carrying the ProvenanceStep that justifies it.
let chainRule (ruleId: string) (inputs: string list) (output: string) : Rule<string> =
    { Id = RuleId ruleId
      Description = sprintf "%s => %s" (String.concat "," inputs) output
      Apply =
        fun facts ->
            let present name = facts |> List.exists (fun f -> f.Value = name)
            if inputs |> List.forall present then
                [ { Id = FactId output
                    Value = output
                    Provenance =
                      [ { Rule = RuleId ruleId
                          Inputs = inputs |> List.map FactId
                          Note = sprintf "derived %s" output } ] } ]
            else
                [] }

/// FactIds of a result, in the kernel's returned (canonical) order — NOT re-sorted,
/// so assertions also pin the canonical-emit ordering (SC-001, US3).
let factIds (r: EvaluationResult<string>) = r.Facts |> List.map (fun f -> f.Id)

// A fixed, multi-path rule set reused by the order-independence/determinism tests.
let private fixedRules =
    [ chainRule "r1" [ "A" ] "B"
      chainRule "r2" [ "B" ] "C"
      chainRule "r3" [ "A"; "C" ] "D"
      chainRule "r4" [ "D" ] "E"
      chainRule "r5" [ "A" ] "D" ] // D reachable by two chains (r3 and r5)

let private fixedSupplied = supplied [ "A" ]

let private canonical = FixedPoint.evaluate identify fixedRules fixedSupplied

let private shuffle (seed: int) (xs: 'a list) =
    let rng = System.Random(seed)
    xs |> List.map (fun x -> rng.Next(), x) |> List.sortBy fst |> List.map snd

let private propConfig =
    { FsCheckConfig.defaultConfig with
        maxTest = 200
        replay = Some(1234UL, 5679UL, None) } // fixed seed → reproducible (D5, V7)

[<Tests>]
let tests =
    testList
        "FixedPoint"
        [
          // ── User Story 1: derive new facts from facts and rules ──

          test "V1 chained rules produce exact transitive closure" {
              // A supplied; A⇒B, B⇒C
              let r = FixedPoint.evaluate identify [ chainRule "r1" [ "A" ] "B"; chainRule "r2" [ "B" ] "C" ] (supplied [ "A" ])
              Expect.equal (factIds r) [ FactId "A"; FactId "B"; FactId "C" ] "supplied + closure, no spurious/missing"
          }

          test "V2 unmet preconditions and the no-rules case derive nothing" {
              let unmet = FixedPoint.evaluate identify [ chainRule "r" [ "X" ] "Y" ] (supplied [ "A" ])
              Expect.equal (factIds unmet) [ FactId "A" ] "unmet preconditions add nothing"
              Expect.equal unmet.Rounds 0 "Rounds = 0 when nothing derived"

              let noRules = FixedPoint.evaluate identify [] (supplied [ "B"; "A" ])
              Expect.equal (factIds noRules) [ FactId "A"; FactId "B" ] "no rules ⇒ exactly the supplied facts (canonical order)"
              Expect.equal noRules.Rounds 0 "Rounds = 0 with no rules"

              let empty = FixedPoint.evaluate identify [] (supplied [])
              Expect.isEmpty empty.Facts "empty supplied ⇒ empty Facts"
          }

          test "V3 self-referential monotone chain quiesces (terminates)" {
              // 'self' re-derives A (its own precondition); the run must terminate.
              let rules = [ chainRule "self" [ "A" ] "A"; chainRule "ab" [ "A" ] "B"; chainRule "selfB" [ "B" ] "B" ]
              let r = FixedPoint.evaluate identify rules (supplied [ "A" ])
              Expect.equal (factIds r) [ FactId "A"; FactId "B" ] "re-derivation adds nothing; quiesces at {A,B}"
          }

          test "V10 Rounds counts productive rounds (0 and 2)" {
              let none = FixedPoint.evaluate identify [ chainRule "r" [ "X" ] "Y" ] (supplied [ "A" ])
              Expect.equal none.Rounds 0 "no derivation ⇒ 0 rounds"
              let depth2 = FixedPoint.evaluate identify [ chainRule "r1" [ "A" ] "B"; chainRule "r2" [ "B" ] "C" ] (supplied [ "A" ])
              Expect.equal depth2.Rounds 2 "A⇒B⇒C is a depth-2 chain ⇒ 2 rounds"
          }

          // ── User Story 2: understand why each derived fact holds ──

          test "V4 a derived fact's provenance names its rule and exact inputs" {
              let r = FixedPoint.evaluate identify [ chainRule "r1" [ "A" ] "B"; chainRule "r2" [ "B" ] "C" ] (supplied [ "A" ])
              let c = r.Facts |> List.find (fun f -> f.Id = FactId "C")
              Expect.equal (List.length c.Provenance) 1 "exactly one justification step"
              let step = List.head c.Provenance
              Expect.equal step.Rule (RuleId "r2") "names the producing rule"
              Expect.equal step.Inputs [ FactId "B" ] "names the exact inputs consumed"
          }

          test "V5 an asserted fact carries empty provenance" {
              let r = FixedPoint.evaluate identify [ chainRule "r1" [ "A" ] "B" ] (supplied [ "A" ])
              let a = r.Facts |> List.find (fun f -> f.Id = FactId "A")
              Expect.equal a.Provenance [] "supplied/asserted ⇒ Provenance = []"
          }

          test "V6 a two-chain fact gets the deterministic (FactId,RuleId) tie-break" {
              // Both r_z and r_a produce D from A in the same round; lowest RuleId wins.
              let r = FixedPoint.evaluate identify [ chainRule "r_z" [ "A" ] "D"; chainRule "r_a" [ "A" ] "D" ] (supplied [ "A" ])
              let d = r.Facts |> List.find (fun f -> f.Id = FactId "D")
              Expect.equal (List.length d.Provenance) 1 "a single, deterministic justification"
              Expect.equal (List.head d.Provenance).Rule (RuleId "r_a") "lowest (FactId,RuleId) establishes it"
          }

          // ── User Story 3: same answer regardless of rule order ──

          testPropertyWithConfig propConfig "V7 result is invariant under rule-order permutation" <| fun (seed: int) ->
              let permuted = shuffle seed fixedRules
              let r = FixedPoint.evaluate identify permuted fixedSupplied
              r = canonical // Facts AND per-fact Provenance identical (EvaluationResult has structural equality)

          test "V8 facts sharing an identify id collapse to one entry" {
              let r =
                  FixedPoint.evaluate
                      identify
                      [ chainRule "r_z" [ "A" ] "D"; chainRule "r_a" [ "A" ] "D" ]
                      (supplied [ "A"; "A" ]) // duplicate supplied A too
              let count id = r.Facts |> List.filter (fun f -> f.Id = id) |> List.length
              Expect.equal (count (FactId "D")) 1 "two rules → single deduplicated D"
              Expect.equal (count (FactId "A")) 1 "duplicate supplied A collapsed"
          }

          test "V9 repeated evaluation is byte-for-byte identical" {
              let run () = FixedPoint.evaluate identify fixedRules fixedSupplied
              Expect.equal (run ()) (run ()) "deterministic across repeated runs"
          } ]
