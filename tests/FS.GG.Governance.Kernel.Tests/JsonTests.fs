module FS.GG.Governance.Kernel.Tests.JsonTests

open System.Text.Json
open Expecto
open FsCheck
open FsCheck.FSharp
open FS.GG.Governance.Kernel
open FS.GG.Governance.Kernel.Check // operators .&, .|, ==> and not'

// ── JSON output layer (F06 · US1 explanation + US4 evidence states) — V31–V34, V39 ──
//
// EVIDENCE-OBLIGATIONS NOTE (Principle IV / V): F06 is a PURE DERIVATION — Principle IV
// (Elmish/MVU) is N/A (no Model/Msg/Effect, no I/O, no clock; persisting/printing the JSON
// is the F08/F12 edge). All evidence here is REAL: real Explanation/EvidenceGraph values
// built from real checks and declared states, with an FsCheck property (V33) over real
// generated trees. No synthetic fixtures, no mocks/stubs, hence no `// SYNTHETIC:` lines.

let private met name : Check<string> = Check.probe name [] [] (fun _ -> Met)
let private unmet name r : Check<string> = Check.probe name [] [] (fun _ -> Unmet r)

/// Fail-fast accessor for a JSON string property (the kernel never emits null here).
let private getStr (el: JsonElement) (name: string) =
    match el.GetProperty(name).GetString() with
    | null -> failtestf "property %s unexpectedly null" name
    | s -> s

let private verdictTag =
    function
    | Pass -> "pass"
    | Fail _ -> "fail"
    | Uncertain _ -> "uncertain"

/// Walk the emitted tree asserting the structural invariants: EVERY node carries a
/// `verdict`; atom/opaque nodes record a `name` and an `outcome`; the kind set is closed.
let rec private checkNode (el: JsonElement) =
    let hasVerdict, _ = el.TryGetProperty "verdict"
    Expect.isTrue hasVerdict "every node carries a rolled-up verdict"

    match getStr el "kind" with
    | "atom"
    | "opaque" ->
        Expect.isTrue (fst (el.TryGetProperty "name")) "atomic node records its probe name"
        Expect.isTrue (fst (el.TryGetProperty "outcome")) "atomic node records its outcome"
    | "all"
    | "any" -> for p in el.GetProperty("parts").EnumerateArray() do checkNode p
    | "not" -> checkNode (el.GetProperty "part")
    | "implies" ->
        checkNode (el.GetProperty "antecedent")
        checkNode (el.GetProperty "consequent")
    | other -> failtestf "unexpected node kind %s" other

// ── A real generator over Explanation for the round-trip property (Principle V) ──

let private genName = Gen.elements [ "a"; "b"; "has-tests"; "contrast" ]
let private genReason = Gen.elements [ ""; "r1"; "missing"; "tone?" ]

let private genOutcome =
    Gen.oneof [ Gen.constant Met; genReason |> Gen.map Unmet; genReason |> Gen.map Unknown ]

let private genVerdict =
    Gen.oneof [ Gen.constant Pass; genReason |> Gen.map Fail; genReason |> Gen.map Uncertain ]

let rec private genExpl size =
    let leaf =
        Gen.oneof
            [ Gen.map3 (fun n o v -> AtomExplained(n, o, v)) genName genOutcome genVerdict
              Gen.map3 (fun n o v -> OpaqueExplained(n, o, v)) genName genOutcome genVerdict ]

    if size <= 0 then
        leaf
    else
        let sub = genExpl (size / 2)
        let parts = gen { let! n = Gen.choose (0, 3) in return! Gen.listOfLength n sub }

        Gen.oneof
            [ leaf
              Gen.map2 (fun ps v -> AllExplained(ps, v)) parts genVerdict
              Gen.map2 (fun ps v -> AnyExplained(ps, v)) parts genVerdict
              Gen.map2 (fun p v -> NotExplained(p, v)) sub genVerdict
              Gen.map3 (fun a c v -> ImpliesExplained(a, c, v)) sub sub genVerdict ]

type ExplArb =
    static member Explanation() : Arbitrary<Explanation> = Gen.sized genExpl |> Arb.fromGen

let private propConfig =
    { FsCheckConfig.defaultConfig with
        maxTest = 200
        arbitrary = [ typeof<ExplArb> ]
        replay = Some(1234UL, 5678UL, None) } // fixed seed → reproducible

[<Tests>]
let tests =
    testList
        "Json"
        [
          // ── User Story 1: emit a check's explanation as stable, round-trippable JSON ──

          test "V31 ofExplanation mirrors the proof tree; root verdict = Check.eval (all six shapes)" {
              let shapes: (string * Check<string>) list =
                  [ "atom", met "has-tests"
                    "all", met "a" .& met "b"
                    "any", met "a" .| unmet "b" "no"
                    "not", not' (unmet "c" "x")
                    "implies", met "a" ==> met "b"
                    "opaque", Opaque("judge", fun _ -> Met) ]

              for expectedKind, chk in shapes do
                  let expl = Check.explain [] chk
                  let json = Json.ofExplanation expl
                  use doc = JsonDocument.Parse json
                  let root = doc.RootElement
                  Expect.equal (getStr root "kind") expectedKind (sprintf "root kind for the %s shape" expectedKind)
                  checkNode root // every node has a verdict; atom/opaque carry name+outcome

                  // the root node's verdict is identical to Check.eval over the same check/facts (FR-001, SC-001)
                  Expect.equal
                      (getStr (root.GetProperty "verdict") "tag")
                      (verdictTag (Check.eval [] chk))
                      (sprintf "root verdict equals Check.eval for the %s shape" expectedKind)
          }

          test "V32 ofExplanation is byte-for-byte deterministic" {
              let chk = (met "a" .& (met "b" .| unmet "c" "r")) ==> not' (met "d")
              let expl = Check.explain [] chk
              Expect.equal (Json.ofExplanation expl) (Json.ofExplanation expl) "same explanation ⇒ identical JSON (SC-002)"
          }

          testPropertyWithConfig propConfig "V33 ofExplanation round-trips losslessly (atom/opaque stay distinct)"
          <| fun (e: Explanation) -> Json.toExplanation (Json.ofExplanation e) = e

          test "V34 serialization runs no probe; OpaqueExplained emits name + recorded outcome only" {
              let calls = ref 0
              let chk: Check<string> = Opaque("judge", (fun _ -> incr calls; Met))
              let expl = Check.explain [] chk // explain runs the opaque Eval exactly once
              Expect.equal calls.Value 1 "explain evaluated the opaque probe once"

              let json = Json.ofExplanation expl // serialization must NOT re-run Eval
              Expect.equal calls.Value 1 "ofExplanation ran no probe (invocation count unchanged) (SC-004)"

              use doc = JsonDocument.Parse json
              let root = doc.RootElement
              Expect.equal (getStr root "kind") "opaque" "opaque node tagged opaque"
              Expect.equal (getStr root "name") "judge" "opaque node carries its declared name only (FR-002)"
              Expect.equal (Json.toExplanation json) expl "opaque round-trips and stays distinct from atom"

              // an explanation node whose source probe WOULD throw still serializes — no
              // function is ever held in the tree or called during serialization.
              let boom = OpaqueExplained("boom", Unmet "x", Fail "x")
              Expect.equal (Json.toExplanation (Json.ofExplanation boom)) boom "opaque-by-data serializes with no probe to run"
          }

          // ── User Story 4: serialize evidence states for the evidence report ──

          test "V39 six distinct evidence-state tokens round-trip; effective map round-trips" {
              let states = [ Pending; Real; Synthetic; Failed; Skipped; AutoSynthetic ]
              let tokens = states |> List.map Json.ofEvidenceState
              Expect.equal (tokens |> List.distinct |> List.length) 6 "each of the six states ⇒ a distinct token"

              for s in states do
                  Expect.equal (Json.toEvidenceState (Json.ofEvidenceState s)) s (sprintf "%A round-trips" s)

              Expect.equal (Json.ofEvidenceState AutoSynthetic) "\"autoSynthetic\"" "AutoSynthetic gets its own visible token"

              // effective map over a tainted graph (F05): a Real node on Synthetic data ⇒ AutoSynthetic
              let g =
                  match Evidence.build [ "a", Real; "b", Synthetic ] [ "a", "b" ] with
                  | Ok g -> g
                  | Error e -> failtestf "build expected Ok, got %A" e

              let eff = Evidence.effective g
              let json = Json.ofEffective id eff
              Expect.stringContains json "autoSynthetic" "the tainted node is visibly marked AutoSynthetic"
              Expect.equal (Json.toEffective json) eff "effective map round-trips to the equal projected map (FR-011)"
              Expect.equal (Json.ofEffective id eff) (Json.ofEffective id eff) "ofEffective is byte-for-byte deterministic (SC-002)"
          } ]
