module FS.GG.Governance.Adapters.Spi.Tests.AdapterTests

open Expecto
open FS.GG.Governance.Kernel
open FS.GG.Governance.Adapters.Spi
open FS.GG.Governance.Adapters.Spi.Tests.ExampleAdapters

// US1/US2 semantic tests, exercised THROUGH the built FS.GG.Governance.Adapters.Spi +
// FS.GG.Governance.Kernel libraries (Principle I). Every test that drives an example
// domain carries the `Synthetic` token (the two domains are synthetic example domains —
// see the evidence-obligations note in ExampleAdapters.fs).
//
// Structural-reuse review note (V62, SC-001): by inspection, neither `docAdapter` nor
// `taskAdapter` defines any inference, arbitration, evidence, rendering, hashing,
// explanation, severity, or routing of its own — each supplies ONLY its five components
// + the F04 `Bridge`, and reaches those facilities exclusively through
// `FS.GG.Governance.Kernel` (`FixedPoint.evaluate`, `Route.route`, `Check.*`,
// `CheckRule.toRule`) and the SPI (`Adapter.toRules`, `Lift.*`). The "contains no
// cross-cutting code of its own" claim is therefore this dependency-symbol fact, not a
// runtime assertion (per the task T012 guidance).

let private factIdStr (FactId s) = s
let private sortFacts (fs: FactSet<'a>) = fs |> List.sortBy (fun f -> factIdStr f.Id)

[<Tests>]
let tests =
    testList
        "Adapter"
        [
          // ── V61 (US1): the five-part contract is TOTAL (FR-001/FR-014, SC-001) ──
          test "V61 the five-part contract is total — Synthetic example adapter" {
              // The record carries exactly the five domain components + the F04 Bridge.
              Expect.equal docAdapter.Probes.Length 1 "one declared probe"
              Expect.equal docAdapter.Rules.Length 2 "two rules in the catalog"
              Expect.equal docAdapter.Fences.Length 1 "one fence"
              Expect.equal (docAdapter.Identify (HasTitle true)) (FactId "doc:title:true") "Identify is supplied"
              Expect.equal (docAdapter.ToRef DocBody) { Kind = "doc"; Key = "body" } "ToRef is supplied"
              Expect.equal docAdapter.Bridge.Judge judge "Bridge is supplied"

              // COMPILE GUARD (documented, intentionally commented): because `Adapter` is
              // a RECORD, an adapter that omits a component does not compile — adoption is
              // never silently partial (FR-014, a typed boundary error). Un-commenting the
              // following fails the build with "no assignment given for field 'Bridge'":
              //
              //   let partial : Adapter<DocFact, DocArtifact, DocChange> =
              //       { Identify = docIdentify; ToRef = docToRef
              //         Probes = [ titledProbe ]; Rules = [ docTitledRule ]; Fences = [ docFence ] }
              ()
          }

          // ── V62 (US1): an example adapter governs ITSELF through the kernel (SC-001) ──
          test "V62 standalone adapter governs itself through kernel entry points — Synthetic" {
              let supplied: FactSet<DocFact> = [ { Id = FactId "f1"; Value = HasTitle true; Provenance = [] } ]
              let rules = Adapter.toRules docAdapter
              let result = FixedPoint.evaluate docAdapter.Identify rules supplied

              // It derives the governance facts: a decided PASS for the titled rule and a
              // review request for the agent rule (cache miss) — all kernel inference.
              let outcomes = result.Facts |> List.choose (fun f -> docAdapter.Bridge.Project f.Value)

              Expect.contains outcomes (Decided(RuleId "doc-titled", Pass)) "doc-titled decided Pass"

              Expect.isTrue
                  (outcomes
                   |> List.exists (function
                       | NeedsReview r -> r.Rule = RuleId "doc-reviewed"
                       | _ -> false))
                  "doc-reviewed routes to review"

              // It routes through the UNCHANGED F07 Route — a fenced change at Gate makes
              // the blocking rule a blocking gate; the advisory agent rule stays advisory.
              let change = { DocPaths = set [ "doc.md" ] }
              let route = Route.route docAdapter.Fences docAdapter.Rules Gate change
              Expect.equal route.Stakes (Fenced "doc-body") "the change is fenced"
              Expect.isTrue (route.Blocking |> List.exists (fun e -> e.Id = RuleId "doc-titled")) "doc-titled blocks"

              // It renders & explains through the UNCHANGED F03 Check interpreters.
              Expect.isFalse (System.String.IsNullOrEmpty(Check.render titled)) "render is non-empty"
              let explanation = Check.explain supplied titled
              Expect.equal (Explanation.verdict explanation) Pass "explanation verdict matches eval"
          }

          // ── V64 (US2): lifting is render/hash/reads/isReified INVARIANT (law L1) ──
          test "V64 lifting is render/hash/reads/isReified invariant — Synthetic" {
              for rule in docAdapter.Rules do
                  let c = rule.Check
                  let lc = Lift.check (|DocP|_|) c

                  Expect.equal (Check.render lc) (Check.render c) (sprintf "render invariant for %A" rule.Id)
                  Expect.equal (Check.hash lc) (Check.hash c) (sprintf "hash invariant (cache key stable) for %A" rule.Id)
                  Expect.equal (Check.reads lc) (Check.reads c) (sprintf "reads invariant for %A" rule.Id)
                  Expect.equal (Check.isReified lc) (Check.isReified c) (sprintf "isReified invariant for %A" rule.Id)
          }

          // ── V63 (US2): lifting is verdict + provenance FAITHFUL (law L2/L3, SC-002) ──
          test "V63 lifting is verdict+provenance faithful for 100% of rules — Synthetic" {
              let supplied: FactSet<DocFact> = [ { Id = FactId "f1"; Value = HasTitle true; Provenance = [] } ]
              let big = supplied |> List.map (fun fa -> { Id = fa.Id; Value = Doc fa.Value; Provenance = fa.Provenance })

              let stdRules = Adapter.toRules docAdapter
              let liftedRules = stdRules |> List.map (Lift.rule Doc (|DocP|_|))

              let stdResult = FixedPoint.evaluate docAdapter.Identify stdRules supplied
              let liftedResult = FixedPoint.evaluate projIdentify liftedRules big

              // projIdentify agrees with docIdentify on injected facts, so every derived
              // fact's (FactId, Value-up-to-inj, ProvenanceStep) is byte-for-byte identical.
              let expectedBig =
                  stdResult.Facts
                  |> List.map (fun fa -> { Id = fa.Id; Value = Doc fa.Value; Provenance = fa.Provenance })

              Expect.equal (sortFacts liftedResult.Facts) (sortFacts expectedBig) "lifted facts equal injected standalone facts"
              Expect.equal liftedResult.Rounds stdResult.Rounds "same number of rounds (no added behaviour)"
          }

          // ── V65 (US2): a lifted Opaque/AgentReviewed rule stays out of Deterministic ──
          test "V65 lifted Opaque/AgentReviewed stays out of Deterministic and routes to review — Synthetic" {
              let lifted = Lift.checkRule (|DocP|_|) docReviewRule

              Expect.isFalse (Check.isReified lifted.Check) "lifted Opaque check stays opaque (cannot be Deterministic)"
              Expect.equal lifted.Tier AgentReviewed "tier preserved under lifting"
              Expect.equal lifted.Question (Some "Is the document well written?") "question preserved"

              // Bridged at the root with NO recorded review present, it emits a review
              // request — it routes to review exactly as the un-lifted rule does.
              let r = CheckRule.toRule projBridge lifted
              let produced = r.Apply []

              match produced |> List.choose (fun fa -> projBridge.Project fa.Value) with
              | [ NeedsReview req ] -> Expect.equal req.Rule (RuleId "doc-reviewed") "review request names the rule"
              | other -> failtestf "expected exactly one NeedsReview, got %A" other
          } ]
