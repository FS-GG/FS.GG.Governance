module FS.GG.Governance.CostBudget.Tests.FindingsDeterminismTests

open Expecto
open FsCheck
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.CostBudget.Model
open FS.GG.Governance.CostBudget
open FS.GG.Governance.CostBudget.Findings
open FS.GG.Governance.CostBudget.Tests.Support

// US3 (T029): repeated `cacheFindings` over identical input is byte-identical; reordering the report's
// entries leaves the (GateId ordinal, kind tag)-sorted findings unchanged (FR-011, SC-004, SC-008).

/// Build a report directly (bypassing `decide`'s own sort) from entries in a chosen order.
let private entryFor (gate: GateId) (decision: CacheDecision) : CacheDecisionEntry =
    { Gate = gate; Cost = Cheap; Review = Deterministic; Decision = decision }

let private entries =
    [ entryFor (gid "z" "z") (Recompute(InputsChanged [ RuleHashCat ]))
      entryFor (gid "a" "a") (Recompute NoPriorEvidence)
      entryFor (gid "m" "m") (Recompute(InputsChanged [ BaseRevisionCat; HeadRevisionCat ])) ]

[<Tests>]
let tests =
    testList
        "FindingsDeterminism"
        [ test "repeated cacheFindings over identical input is identical" {
              let report = CacheDecisionReport entries
              Expect.equal (Findings.cacheFindings report allReal) (Findings.cacheFindings report allReal) "identical"
          }

          testProperty "reordering the report entries leaves the sorted findings unchanged" (fun (seed: int) ->
              let shuffled =
                  entries
                  |> List.mapi (fun i e -> (((i + 1) * (abs seed + 5)) % 97), e)
                  |> List.sortBy fst
                  |> List.map snd

              let a = Findings.cacheFindings (CacheDecisionReport entries) allReal
              let b = Findings.cacheFindings (CacheDecisionReport shuffled) allReal
              a = b)

          test "findings are sorted by GateId ordinal" {
              let findings = Findings.cacheFindings (CacheDecisionReport entries) allReal
              let gateValues = findings |> List.map (fun f -> let (GateId g) = f.Gate in g)
              Expect.equal gateValues (List.sortWith (fun a b -> System.String.CompareOrdinal(a, b)) gateValues) "ordinal-sorted"
          } ]
