module FS.GG.Governance.Kernel.Tests.CheckTests

open Expecto
open FsCheck
open FS.GG.Governance.Kernel
open FS.GG.Governance.Kernel.Check

// Semantic tests for the reified Check algebra (F03), written against the PUBLIC
// surface (Principle I) with REAL Check values and real probes (Principle V) — no
// synthetic fixtures. V1–V12 map to quickstart.md's validation table. Evidence note
// (T024): F03 is pure/applicative ⇒ Principle IV (Elmish/MVU) is N/A, and every test
// here uses real Check values and real probes (incl. the throwing-Eval probe of V4) —
// there are no synthetic fixtures and no `// SYNTHETIC:` disclosures in this feature.

// Tag predicates — the exact reason text is asserted only where it matters.
let private isPass = function Pass -> true | _ -> false
let private isFail = function Fail _ -> true | _ -> false
let private isUncertain = function Uncertain _ -> true | _ -> false

let private noFacts : FactSet<string> = []

// Build atoms through the PUBLIC smart constructor (SC-007). The probe's Eval ignores
// facts and reports a fixed outcome — enough to exercise every fold deterministically.
let private mkProbe name reads args outcome : Check<string> =
    Check.probe name reads args (fun _ -> outcome)

let private atomMet name = mkProbe name [] [] Met
let private atomUnmet name r = mkProbe name [] [] (Unmet r)
let private atomUnknown name r = mkProbe name [] [] (Unknown r)

// Deterministic shuffle by a seed (no wall-clock / ambient randomness; cf. VerdictTests).
let private shuffle (seed: int) (xs: 'a list) =
    let rng = System.Random(seed)
    xs |> List.map (fun x -> rng.Next(), x) |> List.sortBy fst |> List.map snd

// ── A real generator over Check<string> for the FsCheck properties (Principle V) ──

let private genName = Gen.elements [ "a"; "b"; "c"; "contrast"; "tone" ]
let private genReason = Gen.elements [ ""; "r1"; "spacing"; "tone?"; "a; z" ]

let private genOutcome =
    Gen.oneof
        [ Gen.constant Met
          genReason |> Gen.map Unmet
          genReason |> Gen.map Unknown ]

let private genArtifact =
    gen { let! k = Gen.elements [ "token"; "essay" ]
          let! key = Gen.elements [ "text"; "title" ]
          return { Kind = k; Key = key } }

let private genArg =
    Gen.oneof
        [ genArtifact |> Gen.map ArtifactArg
          Gen.elements [ "x"; "y" ] |> Gen.map LiteralArg
          Gen.elements [ 1.0; 4.5 ] |> Gen.map NumberArg ]

let private boundedList g =
    gen { let! n = Gen.choose (0, 3)
          return! Gen.listOfLength n g }

let private genAtomGen =
    gen { let! name = genName
          let! reads = boundedList genArtifact
          let! args = boundedList genArg
          let! oc = genOutcome
          return Check.probe name reads args (fun (_: FactSet<string>) -> oc) }

let private genOpaqueGen =
    gen { let! name = genName
          let! oc = genOutcome
          return Opaque(name, (fun (_: FactSet<string>) -> oc)) }

let rec private genCheckSized size =
    if size <= 0 then
        Gen.oneof [ genAtomGen; genOpaqueGen ]
    else
        let smaller = genCheckSized (size / 2)
        Gen.oneof
            [ genAtomGen
              genOpaqueGen
              boundedList smaller |> Gen.map All
              boundedList smaller |> Gen.map Any
              smaller |> Gen.map Not
              Gen.map2 (fun a b -> Implies(a, b)) smaller smaller ]

let private genCheck = Gen.sized genCheckSized

// Public (not module-private): FsCheck discovers Arbitrary instances by reflection.
type CheckArb =
    static member Check() = Arb.fromGen genCheck

let private propConfig =
    { FsCheckConfig.defaultConfig with
        maxTest = 300
        arbitrary = [ typeof<CheckArb> ]
        replay = Some(1234, 5678) } // fixed seed → reproducible (cf. F01/F02)

[<Tests>]
let tests =
    testList
        "Check"
        [
          // ── User Story 1: evaluate a reified check to a three-valued verdict ──

          test "V1 eval: atom outcomes map; All/Any/Not follow Kleene; order-independent (US1 AS1-4, INV-2/11)" {
              // Atom maps its outcome one-to-one.
              Expect.equal (Check.eval noFacts (atomMet "p")) Pass "Met → Pass"
              Expect.equal (Check.eval noFacts (atomUnmet "p" "spacing")) (Fail "spacing") "Unmet r → Fail r"
              Expect.equal (Check.eval noFacts (atomUnknown "p" "tone?")) (Uncertain "tone?") "Unknown r → Uncertain r"

              // All: Fail dominates; else Uncertain survives; else Pass.
              Expect.isTrue (isFail (Check.eval noFacts (All [ atomMet "a"; atomUnknown "b" "?"; atomUnmet "c" "x" ]))) "a Fail dominates All"
              Expect.isTrue (isUncertain (Check.eval noFacts (All [ atomMet "a"; atomUnknown "b" "?"; atomMet "c" ]))) "Uncertain survives All of passes"
              Expect.equal (Check.eval noFacts (All [ atomMet "a"; atomMet "b" ])) Pass "all pass → Pass"

              // Any: Pass dominates; else Uncertain survives; else Fail.
              Expect.isTrue (isPass (Check.eval noFacts (Any [ atomUnmet "a" "x"; atomMet "b" ]))) "a Pass dominates Any"
              Expect.isTrue (isUncertain (Check.eval noFacts (Any [ atomUnmet "a" "x"; atomUnknown "b" "?" ]))) "Uncertain survives Any of fails"
              Expect.isTrue (isFail (Check.eval noFacts (Any [ atomUnmet "a" "x"; atomUnmet "b" "y" ]))) "all fail → Fail"

              // Not: flips Pass/Fail tag; leaves Uncertain.
              Expect.isTrue (isFail (Check.eval noFacts (Not (atomMet "a")))) "not (met) → Fail"
              Expect.isTrue (isPass (Check.eval noFacts (Not (atomUnmet "a" "x")))) "not (unmet) → Pass"
              Expect.isTrue (isUncertain (Check.eval noFacts (Not (atomUnknown "a" "?")))) "not (unknown) → Uncertain"

              // Order-independence (verdict AND reason) for commutative nodes (INV-11, SC-003 reason half).
              let xs = [ atomUnmet "a" "spacing"; atomUnknown "b" "tone?"; atomUnmet "c" "color" ]
              for seed in [ 1; 7; 42; 99 ] do
                  Expect.equal (Check.eval noFacts (All xs)) (Check.eval noFacts (All (shuffle seed xs))) "All eval order-independent (verdict+reason)"
                  Expect.equal (Check.eval noFacts (Any xs)) (Check.eval noFacts (Any (shuffle seed xs))) "Any eval order-independent (verdict+reason)"
          }

          test "V2 eval: (a ==> b) = Any [Not a; b] for representative a,b (US1 AS5, FR-006)" {
              let cases =
                  [ atomMet "a", atomMet "b"
                    atomMet "a", atomUnmet "b" "x"
                    atomUnmet "a" "x", atomMet "b"
                    atomUnknown "a" "?", atomMet "b"
                    atomUnknown "a" "?", atomUnknown "b" "??"
                    atomUnmet "a" "x", atomUnknown "b" "?" ]
              for a, b in cases do
                  Expect.equal (Check.eval noFacts (a ==> b)) (Check.eval noFacts (Any [ Not a; b ])) "implication desugars to Any [Not a; b]"
          }

          test "V3 eval: Opaque maps its function's outcome (US1 AS6, FR-006)" {
              Expect.equal (Check.eval noFacts (Opaque("o", fun _ -> Met))) Pass "opaque Met → Pass"
              Expect.equal (Check.eval noFacts (Opaque("o", fun _ -> Unmet "no"))) (Fail "no") "opaque Unmet → Fail"
              Expect.equal (Check.eval noFacts (Opaque("o", fun _ -> Unknown "dunno"))) (Uncertain "dunno") "opaque Unknown → Uncertain"
          }

          // ── User Story 2: inspect without running — render and hash ──

          test "V4 never-executes: a throwing-Eval probe still renders/hashes/reads/isReified; only eval throws (US2 AS1, SC-001, INV-1)" {
              let boom = Check.probe "boom" [ { Kind = "token"; Key = "t" } ] [ NumberArg 4.5 ] (fun (_: FactSet<string>) -> failwith "executed")
              let composed = All [ boom; Not boom; boom ==> atomMet "ok" ]
              // The four structure-only folds must succeed without running Eval.
              Expect.isTrue ((Check.render composed).Length > 0) "render succeeds (no Eval)"
              Expect.isTrue ((Check.hash composed).Length > 0) "hash succeeds (no Eval)"
              Expect.equal (Check.reads composed) [ { Kind = "token"; Key = "t" }; { Kind = "token"; Key = "t" }; { Kind = "token"; Key = "t" } ] "reads succeeds (no Eval): boom, Not boom, boom in the implication"
              Expect.isTrue (Check.isReified composed) "isReified succeeds (no Eval)"
              // Only eval runs Eval — and therefore throws.
              Expect.throws (fun () -> Check.eval noFacts boom |> ignore) "eval runs Eval and throws"
          }

          test "V5 render: deterministic, execution-free, authoring order preserved (US2 AS1, FR-007)" {
              let contrast = Check.probe "contrastRatio" [ { Kind = "token"; Key = "text" } ] [ NumberArg 4.5 ] (fun (_: FactSet<string>) -> Met)
              let tone = Check.probe "toneIsProfessional" [] [] (fun (_: FactSet<string>) -> Unknown "not reviewed")
              let chk = contrast .& tone
              Expect.equal (Check.render chk) "all of [contrastRatio(token:text, 4.5); toneIsProfessional]" "render uses structure only, in authoring order"
              Expect.equal (Check.render chk) (Check.render chk) "render is deterministic"
              // Authoring order is preserved (render does NOT canonicalize).
              Expect.notEqual (Check.render (All [ contrast; tone ])) (Check.render (All [ tone; contrast ])) "render preserves authoring order"
          }

          testPropertyWithConfig propConfig "V6 hash: permuting members of All/Any is hash-invariant (US2 AS3, SC-002, INV-3)" <|
              fun (children: Check<string> list) (seed: int) ->
                  let permuted = shuffle seed children
                  Check.hash (All children) = Check.hash (All permuted)
                  && Check.hash (Any children) = Check.hash (Any permuted)

          test "V6b hash: duplicate members in a commutative node hash deterministically (edge case, INV-3)" {
              let a = atomMet "a"
              let b = atomUnmet "b" "x"
              Expect.equal (Check.hash (All [ a; a; b ])) (Check.hash (All [ b; a; a ])) "duplicates: position-independent"
              Expect.equal (Check.hash (Any [ a; b; a ])) (Check.hash (Any [ a; a; b ])) "duplicates: position-independent (Any)"
          }

          test "V7 hash: positional structure changes the key (US2 AS4/AS5, SC-002, INV-4)" {
              let a = atomMet "a"
              let b = atomUnmet "b" "x"
              Expect.notEqual (Check.hash (a ==> b)) (Check.hash (b ==> a)) "implication sides are positional"
              // A probe's ordered Args are positional.
              let p1 = Check.probe "p" [] [ LiteralArg "x"; NumberArg 1.0 ] (fun (_: FactSet<string>) -> Met)
              let p2 = Check.probe "p" [] [ NumberArg 1.0; LiteralArg "x" ] (fun (_: FactSet<string>) -> Met)
              Expect.notEqual (Check.hash p1) (Check.hash p2) "a probe's Args order changes the hash"
          }

          test "V8 hash: re-hash identical ⇒ identical; Opaque hashes by name only (US2 AS2/AS6, INV-5)" {
              let chk = All [ atomMet "a"; Not (atomUnmet "b" "x") ]
              Expect.equal (Check.hash chk) (Check.hash chk) "re-hash identical check ⇒ identical key"
              // Two opaques with the same name but different Eval hash identically.
              let o1 = Opaque("judge", fun (_: FactSet<string>) -> Met)
              let o2 = Opaque("judge", fun (_: FactSet<string>) -> Unmet "different function entirely")
              Expect.equal (Check.hash o1) (Check.hash o2) "Opaque hashes from name only"
              let o3 = Opaque("other", fun (_: FactSet<string>) -> Met)
              Expect.notEqual (Check.hash o1) (Check.hash o3) "different Opaque name ⇒ different hash"
          }

          // ── User Story 3: explain as a proof tree ──

          testPropertyWithConfig propConfig "V9 explain: Explanation.verdict (explain f c) = eval f c (US3 AS1, SC-004, INV-6)" <|
              fun (c: Check<string>) ->
                  Explanation.verdict (Check.explain noFacts c) = Check.eval noFacts c

          test "V10 explain: structure mirrors the check and records each atom's outcome (US3 AS2, INV-7)" {
              let chk = All [ atomMet "a"; Any [ atomUnmet "b" "x"; atomUnknown "c" "?" ]; Not (atomMet "d") ]
              match Check.explain noFacts chk with
              | AllExplained([ AtomExplained("a", Met, _)
                               AnyExplained([ AtomExplained("b", Unmet "x", _); AtomExplained("c", Unknown "?", _) ], _)
                               NotExplained(AtomExplained("d", Met, _), _) ], rootV) ->
                  Expect.equal rootV (Check.eval noFacts chk) "root explanation verdict = eval verdict"
              | other -> failtestf "explanation structure did not mirror the check: %A" other
          }

          // ── User Story 4: detect opacity and collect declared reads ──

          test "V11 isReified false iff Opaque present; reads = exactly declared refs, Opaque adds none (US4 AS1-3, SC-005, INV-8/9)" {
              let r1 = { Kind = "token"; Key = "text" }
              let r2 = { Kind = "essay"; Key = "intro" }
              let structural = All [ mkProbe "a" [ r1 ] [] Met; Not (mkProbe "b" [ r2 ] [] (Unmet "x")) ]
              Expect.isTrue (Check.isReified structural) "no Opaque ⇒ reified"
              Expect.equal (Check.reads structural) [ r1; r2 ] "reads = exactly the declared refs, left-to-right"
              // Insert an Opaque anywhere ⇒ not reified; it contributes no reads.
              let withOpaque = All [ structural; Opaque("judge", fun _ -> Met) ]
              Expect.isFalse (Check.isReified withOpaque) "an Opaque anywhere ⇒ not reified"
              Expect.equal (Check.reads withOpaque) [ r1; r2 ] "Opaque contributes no reads"
          }

          // ── Polish: totality across all six interpreters ──

          test "V12 totality: empty All/Any and combinator mixes fold through all six interpreters (FR-013, SC-006, INV-10)" {
              // Empty-combinator identities.
              Expect.equal (Check.eval noFacts (All [])) Pass "all [] → Pass"
              Expect.equal (Check.eval noFacts (Any [])) (Fail "") "any [] → Fail \"\""
              // Every interpreter is total over a spread of shapes — none throws or returns a partial.
              let samples : Check<string> list =
                  [ All []
                    Any []
                    atomMet "a"
                    Not (All [])
                    Any [ All []; Not (Any []) ]
                    (atomMet "a") ==> (Any [])
                    All [ Opaque("o", fun _ -> Unknown "?"); atomUnmet "b" "x" ] ]
              for c in samples do
                  Check.eval noFacts c |> ignore
                  Check.render c |> ignore
                  Check.hash c |> ignore
                  Check.explain noFacts c |> ignore
                  Check.reads c |> ignore
                  Check.isReified c |> ignore
              Expect.isTrue true "all six interpreters total over every sample"
          }
        ]
