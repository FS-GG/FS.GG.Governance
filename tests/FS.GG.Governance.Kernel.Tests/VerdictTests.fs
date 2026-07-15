module FS.GG.Governance.Kernel.Tests.VerdictTests

open Expecto
open FsCheck
open FsCheck.FSharp
open FS.GG.Governance.Kernel

// Semantic tests for the three-valued Kleene verdict algebra (F02), written against
// the PUBLIC surface (Principle I) with REAL verdict values (Principle V) — no
// synthetic fixtures. V1–V10 here; V11/V12 (surface drift, dep hygiene) stay in
// SurfaceDriftTests.fs. Maps to quickstart.md's validation table.

// Tag predicates — US1 pins the OUTCOME (which case dominates); the exact reason
// string is US2's determinism concern (V6–V8), so these don't assert reason text.
let private isPass = function Pass -> true | _ -> false
let private isFail = function Fail _ -> true | _ -> false
let private isUncertain = function Uncertain _ -> true | _ -> false

// A real generator over the three cases with arbitrary reason strings (Principle V).
// Reasons sometimes embed the reserved "; " separator so the property exercises
// component-level normalisation (US2), not just whole-reason atoms.
let private genVerdict : Gen<Verdict> =
    let genReason =
        Gen.oneof
            [ (ArbMap.defaults |> ArbMap.generate<string>) |> Gen.map (fun s -> if System.String.IsNullOrEmpty s then "" else s)
              Gen.elements [ "a"; "z"; "m"; "a; z"; "spacing"; "tone; spacing" ] ]
    Gen.oneof
        [ Gen.constant Pass
          genReason |> Gen.map Fail
          genReason |> Gen.map Uncertain ]

// Public (not module-private): FsCheck discovers Arbitrary instances by reflection
// over public static members. (Principle II governs the kernel surface, not tests.)
type VerdictArb =
    static member Verdict() = Arb.fromGen genVerdict

let private propConfig =
    { FsCheckConfig.defaultConfig with
        maxTest = 300
        arbitrary = [ typeof<VerdictArb> ]
        replay = Some(1234UL, 5679UL, None) } // fixed seed → reproducible (cf. F01 V7)

// Deterministic shuffle by a seed (no wall-clock / ambient randomness).
let private shuffle (seed: int) (xs: 'a list) =
    let rng = System.Random(seed)
    xs |> List.map (fun x -> rng.Next(), x) |> List.sortBy fst |> List.map snd

[<Tests>]
let tests =
    testList
        "Verdict"
        [
          // ── User Story 1: combine without losing "uncertain" (Kleene outcomes) ──

          test "V1 all with a Fail among undecided/pass siblings ⇒ Fail (US1 AS1)" {
              let r = Verdict.all [ Pass; Uncertain "tone?"; Fail "spacing"; Pass ]
              Expect.isTrue (isFail r) "a definite fail dominates conjunction"
          }

          test "V2 all with no Fail but ≥1 Uncertain ⇒ Uncertain, not Pass (US1 AS2, INV-4)" {
              let r = Verdict.all [ Pass; Uncertain "agent has not reviewed"; Pass ]
              Expect.isTrue (isUncertain r) "an undecided clause survives a conjunction of passes"
              Expect.isFalse (isPass r) "Uncertain is NOT silently coerced to Pass"
          }

          test "V3 any with a Pass among undecided/fail siblings ⇒ Pass (US1 AS3)" {
              let r = Verdict.any [ Fail "a"; Uncertain "b"; Pass ]
              Expect.isTrue (isPass r) "a definite pass dominates disjunction"
          }

          test "V4 any with no Pass but ≥1 Uncertain ⇒ Uncertain, not Fail (US1 AS4, INV-4)" {
              let r = Verdict.any [ Fail "a"; Uncertain "b" ]
              Expect.isTrue (isUncertain r) "an undecided clause survives a disjunction of fails"
              Expect.isFalse (isFail r) "Uncertain is NOT silently coerced to Fail"
          }

          test "V5 all-Pass under all ⇒ Pass; all-Fail under any ⇒ Fail (US1 AS5)" {
              Expect.equal (Verdict.all [ Pass; Pass; Pass ]) Pass "every clause passes ⇒ Pass"
              Expect.isTrue (isFail (Verdict.any [ Fail "a"; Fail "b" ])) "nothing satisfies the disjunction ⇒ Fail"
          }

          // ── User Story 2: same combined verdict (outcome AND reason) regardless of order ──

          testPropertyWithConfig propConfig "V6 permuting the input list ⇒ identical verdict and reason (FR-005/006, SC-001)" <|
              fun (xs: Verdict list) (seed: int) ->
                  let permuted = shuffle seed xs
                  Verdict.all xs = Verdict.all permuted && Verdict.any xs = Verdict.any permuted

          testPropertyWithConfig propConfig "V7 re-nesting ⇒ identical verdict and reason (associativity, US2 AS3)" <|
              fun (xs: Verdict list) (ys: Verdict list) ->
                  let allNested = Verdict.all [ Verdict.all xs; Verdict.all ys ]
                  let allFlat = Verdict.all (xs @ ys)
                  let anyNested = Verdict.any [ Verdict.any xs; Verdict.any ys ]
                  let anyFlat = Verdict.any (xs @ ys)
                  allNested = allFlat && anyNested = anyFlat

          test "V8 duplicate/shuffled reasons ⇒ dedup'd, ordinal-sorted, position-independent reason (US2 AS4)" {
              // Reorder is byte-for-byte identical, including reason text.
              Expect.equal (Verdict.all [ Fail "a"; Fail "z" ]) (Verdict.all [ Fail "z"; Fail "a" ]) "reorder ⇒ identical Fail"
              Expect.equal (Verdict.all [ Fail "a"; Fail "z" ]) (Fail "a; z") "reasons ordinal-sorted and joined on \"; \""
              // Re-nesting collapses to the same component set.
              Expect.equal
                  (Verdict.all [ Verdict.all [ Fail "a"; Fail "z" ]; Fail "m" ])
                  (Fail "a; m; z")
                  "nested aggregation splits back into components and re-sorts"
              // Duplication-invariance: count and position of duplicates don't matter.
              Expect.equal (Verdict.all [ Fail "z"; Fail "a"; Fail "z"; Fail "a" ]) (Fail "a; z") "duplicates collapse"
          }

          test "V8b \"; \" is RESERVED inside a single leaf reason — it is split and reordered (CORE-3)" {
              // Pins the documented reservation (Check.Outcome / Verdict .fsi): a single leaf
              // reason that embeds "; " is treated as multiple components, so it is fragmented
              // and ordinal-reordered on roll-up — a probe author must not put "; " in one reason.
              Expect.equal (Verdict.all [ Fail "spacing; tone" ]) (Fail "spacing; tone") "already-ordinal components pass through unchanged"
              Expect.equal (Verdict.all [ Fail "tone; spacing" ]) (Fail "spacing; tone") "a leaf reason's \"; \"-parts are split and reordered, not preserved verbatim"
              Expect.equal (Verdict.any [ Uncertain "z; a; z" ]) (Uncertain "a; z") "duplicate components within one leaf reason collapse too"
              // The token is inert without aggregation: a bare reason carries "; " verbatim.
              Expect.equal (Fail "tone; spacing") (Fail "tone; spacing") "an un-aggregated reason is stored as authored"
          }

          // ── User Story 3: negate a verdict ──

          test "V9 negate swaps pass/fail tags, fixes Uncertain; double-negate recovers tags (US3)" {
              Expect.equal (Verdict.negate (Fail "x")) Pass "negate (Fail _) = Pass (US3 AS2)"
              Expect.equal (Verdict.negate Pass) (Fail "") "negate Pass = Fail \"\" (US3 AS1)"
              Expect.equal (Verdict.negate (Uncertain "y")) (Uncertain "y") "negate Uncertain = Uncertain (US3 AS3)"

              // Double-negation: tags always recover; exact recovery for Uncertain and
              // for \"\"-reasoned pass/fail (INV-7).
              Expect.equal (Verdict.negate (Verdict.negate (Uncertain "y"))) (Uncertain "y") "Uncertain recovers exactly"
              Expect.equal (Verdict.negate (Verdict.negate (Fail ""))) (Fail "") "\"\"-reasoned Fail recovers exactly"
              Expect.equal (Verdict.negate (Verdict.negate Pass)) Pass "Pass recovers exactly (Pass→Fail \"\"→Pass)"
              Expect.isTrue (isFail (Verdict.negate (Verdict.negate (Fail "x")))) "reasoned Fail recovers its TAG (reason dropped at the flip)"
          }

          // ── Polish: edges, identities, totality ──

          test "V10 empty + single-element identities (FR-008/009, INV-5)" {
              Expect.equal (Verdict.all []) Pass "all [] = Pass (identity of conjunction)"
              Expect.equal (Verdict.any []) (Fail "") "any [] = Fail \"\" (identity of disjunction)"
              // all [v] = any [v] = v (reason preserved) for each canonical-reasoned kind.
              for v in [ Pass; Fail "spacing"; Uncertain "pending review" ] do
                  Expect.equal (Verdict.all [ v ]) v (sprintf "all [%A] = %A" v v)
                  Expect.equal (Verdict.any [ v ]) v (sprintf "any [%A] = %A" v v)
          }

          testPropertyWithConfig propConfig "V10b every operation is total — no input throws (FR-008, SC-003, INV-6)" <|
              fun (xs: Verdict list) (v: Verdict) ->
                  // Forcing the results is enough: a throw would fail the property.
                  Verdict.all xs |> ignore
                  Verdict.any xs |> ignore
                  Verdict.negate v |> ignore
                  true
          ]
