module FS.GG.Governance.Kernel.Tests.EvidenceTests

open Expecto
open FS.GG.Governance.Kernel

// ── Evidence model & synthetic taint (F05) — V21–V29 ──
//
// EVIDENCE-OBLIGATIONS NOTE (Principle IV / V): F05 is a PURE DERIVATION, so Principle IV
// (Elmish/MVU) is N/A — there is no Model/Msg/Effect, no I/O, no workflow; the dispatching
// edge interpreter that reads a node's true declared state lives in F08, modelled there.
// All evidence here is REAL — every test input is a real `EvidenceGraph` built from real
// declared states (the inputs ARE declared-state graphs). No synthetic fixtures, no
// mocks/stubs/canned data, hence no `// SYNTHETIC:` disclosures.

/// Build (asserting success) and compute the effective-state map. Real graphs throughout.
let private eff nodes deps =
    match Evidence.build nodes deps with
    | Ok g -> Evidence.effective g
    | Error e -> failtestf "build expected Ok, got Error %A" e

let private stateOf (m: Map<string, EvidenceState>) id = Map.find id m

/// Deterministic permutation by seed (reused for the order-independence properties).
let private shuffle (seed: int) (xs: 'a list) =
    let rng = System.Random(seed)
    xs |> List.map (fun x -> rng.Next(), x) |> List.sortBy fst |> List.map snd

let private propConfig =
    { FsCheckConfig.defaultConfig with
        maxTest = 200
        replay = Some(1234, 5678) } // fixed seed → reproducible (D5)

[<Tests>]
let tests =
    testList
        "Evidence"
        [
          // ── User Story 1: propagate synthetic taint over a dependency graph ──

          test "V21 synthetic root taints its real chain; a no-synthetic graph is untouched" {
              // Synthetic root + a chain of Real nodes resting on it (US1 AS1/2, SC-001).
              let m =
                  eff
                      [ "data", Synthetic; "analysis", Real; "report", Real ]
                      [ "analysis", "data"; "report", "analysis" ]

              Expect.equal (stateOf m "data") Synthetic "root stays Synthetic (root cause, never AutoSynthetic)"
              Expect.equal (stateOf m "analysis") AutoSynthetic "direct Real dependant ⇒ AutoSynthetic"
              Expect.equal (stateOf m "report") AutoSynthetic "transitive Real dependant ⇒ AutoSynthetic"

              // No Synthetic anywhere ⇒ effective state equals declared state everywhere (US1 AS3, INV-1).
              let declared =
                  [ "a", Real; "b", Real; "c", Pending; "d", Failed; "e", Skipped ]

              let m2 = eff declared [ "b", "a"; "c", "b"; "d", "c"; "e", "d" ]
              for id, st in declared do
                  Expect.equal (stateOf m2 id) st (sprintf "%s keeps its declared state — no taint introduced" id)
          }

          testPropertyWithConfig propConfig "V22 taint reaches the full transitive depth of an N-chain" <|
              fun (n: int) ->
                  // A chain of |n| Real nodes rooted at one Synthetic node, arbitrary N (US1 AS2, SC-002).
                  let depth = (abs n) % 40 // bound the chain; arbitrary length, terminates
                  let realIds = [ for i in 1..depth -> sprintf "r%03d" i ]
                  let nodes = ("root", Synthetic) :: (realIds |> List.map (fun id -> id, Real))
                  let chain = "root" :: realIds
                  let edges = List.pairwise chain |> List.map (fun (lo, hi) -> hi, lo) // hi rests on lo
                  let m = eff nodes edges
                  stateOf m "root" = Synthetic
                  && realIds |> List.forall (fun id -> stateOf m id = AutoSynthetic)

          test "V24 a diamond to one synthetic root taints once (idempotent, order-free)" {
              // top reaches Synthetic 'root' by two distinct Real paths (US1 AS4, SC-001, INV-4).
              let m =
                  eff
                      [ "root", Synthetic; "left", Real; "right", Real; "top", Real ]
                      [ "left", "root"; "right", "root"; "top", "left"; "top", "right" ]

              Expect.equal (stateOf m "root") Synthetic "root cause"
              Expect.equal (stateOf m "left") AutoSynthetic "left path tainted"
              Expect.equal (stateOf m "right") AutoSynthetic "right path tainted"
              Expect.equal (stateOf m "top") AutoSynthetic "diamond apex tainted exactly once — no double-counting"
              Expect.equal (Map.count m) 4 "every node reported exactly once"
          }

          // ── User Story 2: taint clears automatically when the root cause is upgraded ──

          test "V23 re-declaring the synthetic root Real clears the taint; selective with two roots" {
              // (a) single root upgraded ⇒ everything formerly tainted is Real again (US2 AS1, SC-003).
              let tainted = eff [ "data", Synthetic; "analysis", Real; "report", Real ]
                                [ "analysis", "data"; "report", "analysis" ]
              Expect.equal (stateOf tainted "report") AutoSynthetic "precondition: report tainted"

              let cleared = eff [ "data", Real; "analysis", Real; "report", Real ]
                                [ "analysis", "data"; "report", "analysis" ]
              Expect.equal (stateOf cleared "data") Real "root upgraded"
              Expect.equal (stateOf cleared "analysis") Real "taint cleared with no other change"
              Expect.equal (stateOf cleared "report") Real "taint cleared transitively"

              // (b) two synthetic roots; upgrade exactly one ⇒ only nodes resting SOLELY on it clear (US2 AS2, INV-3).
              //   onlyA rests on rootA; onlyB rests on rootB; both rests on both.
              let nodes2 root1 =
                  [ "rootA", root1; "rootB", Synthetic; "onlyA", Real; "onlyB", Real; "both", Real ]
              let edges2 = [ "onlyA", "rootA"; "onlyB", "rootB"; "both", "rootA"; "both", "rootB" ]

              let before = eff (nodes2 Synthetic) edges2
              Expect.equal (stateOf before "onlyA") AutoSynthetic "precondition: onlyA tainted by rootA"

              let after = eff (nodes2 Real) edges2
              Expect.equal (stateOf after "rootA") Real "rootA upgraded"
              Expect.equal (stateOf after "onlyA") Real "onlyA rested solely on rootA ⇒ clears"
              Expect.equal (stateOf after "rootB") Synthetic "rootB still the root cause"
              Expect.equal (stateOf after "onlyB") AutoSynthetic "onlyB still rests on rootB ⇒ stays tainted"
              Expect.equal (stateOf after "both") AutoSynthetic "both still rests on rootB ⇒ stays tainted"

              // (c) effective is a pure function carrying no hidden history (US2 AS3, FR-010).
              let g = Evidence.build (nodes2 Real) edges2 |> function Ok x -> x | Error e -> failtestf "%A" e
              Expect.equal (Evidence.effective g) (Evidence.effective g) "recomputed over the same graph ⇒ identical"
          }

          // ── User Story 3: reject cyclic dependency graphs ──

          test "V26 build refuses cycles / AutoSynthetic / unknown endpoints; accepts a DAG" {
              // Precedence + each refusal (US3 AS1/2, SC-005/006, INV-6/7/10).
              Expect.equal
                  (Evidence.build [ "a", Real ] [ "a", "a" ])
                  (Error(Cycle [ "a" ]))
                  "self-dependency ⇒ Cycle [a]"

              match Evidence.build [ "a", Real; "b", Real; "c", Real ] [ "a", "b"; "b", "c"; "c", "a" ] with
              | Error(Cycle path) -> Expect.isNonEmpty path "multi-node cycle ⇒ Cycle with a witnessing loop"
              | other -> failtestf "expected Cycle, got %A" other

              Expect.equal
                  (Evidence.build [ "x", AutoSynthetic ] [])
                  (Error(AutoSyntheticDeclared "x"))
                  "declaring AutoSynthetic ⇒ refused (computed-only)"

              Expect.equal
                  (Evidence.build [ "a", Real ] [ "a", "ghost" ])
                  (Error(UnknownNode "ghost"))
                  "edge to an undeclared endpoint ⇒ UnknownNode"

              // An acyclic graph is accepted and its effective map computes (US3 AS3).
              match Evidence.build [ "a", Synthetic; "b", Real ] [ "b", "a" ] with
              | Ok g -> Expect.equal (Evidence.effective g |> Map.find "b") AutoSynthetic "acyclic ⇒ Ok and computes"
              | Error e -> failtestf "expected Ok, got %A" e
          }

          // ── User Story 4: honest, domain-neutral evidence states ──

          test "V27 non-real states are inert; declared synthetic outranks inherited taint; domain-neutral" {
              // (a) Pending/Failed/Skipped on a Synthetic dependency keep their declared state (US4 AS1, SC-007, INV-8).
              let m =
                  eff
                      [ "root", Synthetic; "p", Pending; "f", Failed; "s", Skipped ]
                      [ "p", "root"; "f", "root"; "s", "root" ]

              Expect.equal (stateOf m "p") Pending "Pending never upgraded"
              Expect.equal (stateOf m "f") Failed "Failed never upgraded"
              Expect.equal (stateOf m "s") Skipped "Skipped never upgraded"

              // (b) a node declared Synthetic that also rests on a Synthetic node is Synthetic, not AutoSynthetic (US4 AS2, INV-7).
              let m2 = eff [ "root", Synthetic; "s2", Synthetic ] [ "s2", "root" ]
              Expect.equal (stateOf m2 "s2") Synthetic "declared Synthetic reported verbatim — root cause outranks inheritance"

              // (c) domain-neutral: a research finding resting on simulated data is AutoSynthetic (US4 AS3, INV-12).
              let m3 = eff [ "simulated-data", Synthetic; "finding", Real ] [ "finding", "simulated-data" ]
              Expect.equal (stateOf m3 "finding") AutoSynthetic "Real finding on Synthetic data ⇒ AutoSynthetic"
          }

          // ── Polish: determinism, totality, accessor contract (V25/V28/V29) ──

          testPropertyWithConfig propConfig "V25 effective is invariant under node/edge permutation" <|
              fun (s1: int) (s2: int) ->
                  // A fixed diamond+chain; permute both input lists; effective map must be identical (FR-010, SC-004, INV-5).
                  let nodes =
                      [ "root", Synthetic; "a", Real; "b", Real; "c", Real; "d", Pending; "e", Failed ]
                  let edges = [ "a", "root"; "b", "root"; "c", "a"; "c", "b"; "d", "c"; "e", "a" ]
                  let canonical = eff nodes edges
                  let permuted = eff (shuffle s1 nodes) (shuffle s2 edges)
                  canonical = permuted

          test "V28 effective is total — empty graph, lone Real, no partial maps" {
              // Empty graph ⇒ Ok of an empty graph; effective ⇒ empty Map (INV-9).
              match Evidence.build [] [] with
              | Ok g -> Expect.equal (Evidence.effective g) Map.empty "empty graph ⇒ empty effective map"
              | Error e -> failtestf "empty build expected Ok, got %A" e

              // A lone Real node with no deps ⇒ Real (no synthetic ancestor).
              Expect.equal (eff [ "x", Real ] [] |> Map.find "x") Real "lone Real ⇒ Real"

              // effective covers EVERY node (no partial) for a representative mixed graph.
              let nodes = [ "root", Synthetic; "a", Real; "b", Pending; "c", Skipped ]
              let m = eff nodes [ "a", "root"; "b", "root" ]
              Expect.equal (Map.count m) (List.length nodes) "effective reports every node — never a partial map"
          }

          test "V29 accessors are order-free and history-free (dup id last-wins, dup edge, unsorted)" {
              // Duplicate id (last wins), duplicate edge, deliberately unsorted input (FR-003, INV-13).
              let g =
                  Evidence.build
                      [ "c", Real; "a", Pending; "a", Real; "b", Skipped ] // 'a' redeclared Real
                      [ "c", "b"; "c", "b"; "b", "a" ]                      // ("c","b") duplicated
                  |> function Ok x -> x | Error e -> failtestf "build expected Ok, got %A" e

              Expect.equal
                  (Evidence.nodes g)
                  [ "a", Real; "b", Skipped; "c", Real ]
                  "nodes: de-duplicated (last declaration wins) and ordered by id"

              Expect.equal
                  (Evidence.dependencies g)
                  [ "b", "a"; "c", "b" ]
                  "dependencies: duplicate edge collapsed, ordered by (dependent, dependency)"
          } ]
