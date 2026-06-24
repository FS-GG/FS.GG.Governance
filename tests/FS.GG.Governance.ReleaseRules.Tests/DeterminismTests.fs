module FS.GG.Governance.ReleaseRules.Tests.DeterminismTests

open Expecto
open FS.GG.Governance.ReleaseRules
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseRules.Tests.Support

// US3 (acc. 1–2, FR-006/FR-007, SC-001/SC-003): two evaluations over identical input are byte-identical;
// re-ordering the input rules yields the same sorted output (order-independence); the output rule-kind
// multiset equals the declared rule-kind multiset (no drops, no fabrications); duplicates each emit a
// finding; facts for an undeclared kind invent nothing.

let private kinds (findings: ReleaseFinding list) =
    findings |> List.map (fun f -> f.Kind) |> List.sort

[<Tests>]
let tests =
    testList
        "DeterminismTests"
        [ test "two evaluate calls over identical input are structurally equal" {
              let rules = allKinds |> List.map (fun k -> blocking k "pkg")
              let facts = factsOf [ VersionBump, Met; PackageMetadata, Unmet; TemplatePins, Unrecoverable ]
              Expect.equal (Release.evaluate rules facts) (Release.evaluate rules facts) "deterministic"
          }

          test "re-ordering the input rules yields the same sorted output (order-independence)" {
              let rules = allKinds |> List.map (fun k -> blocking k "pkg")
              let facts = allMet rules
              let forward = Release.evaluate rules facts
              let reversed = Release.evaluate (List.rev rules) facts
              Expect.equal reversed forward "sorted output is independent of input order"
          }

          test "two rollup results over identical findings are equal" {
              let rules = [ blocking VersionBump "pkg"; advisory PackageMetadata "pkg" ]
              let findings = Release.evaluate rules (factsOf [ VersionBump, Unmet ])
              Expect.equal (Release.rollup findings) (Release.rollup findings) "rollup deterministic"
          }

          test "the output rule-kind multiset equals the declared rule-kind multiset" {
              let rules = allKinds |> List.map (fun k -> blocking k "pkg")
              let findings = Release.evaluate rules (factsOf [ VersionBump, Met ])
              Expect.equal (kinds findings) (rules |> List.map (fun r -> r.Kind) |> List.sort) "no drops, no fabrications"
          }

          test "duplicate same-kind rules each yield their own finding" {
              let rules =
                  [ blocking VersionBump "a"; blocking VersionBump "b"; blocking VersionBump "c" ]
              let findings = Release.evaluate rules (factsOf [ VersionBump, Met ])
              Expect.equal findings.Length 3 "three rules ⇒ three findings"
          }

          test "facts for an undeclared kind invent no finding" {
              let rules = [ blocking VersionBump "pkg" ]
              // facts mention several kinds no rule declares — none must appear.
              let facts = factsOf [ VersionBump, Met; Provenance, Unmet; TemplatePins, Met ]
              let findings = Release.evaluate rules facts
              Expect.equal findings.Length 1 "only the declared rule yields a finding"
              Expect.equal (List.exactlyOne findings).Kind VersionBump "no undeclared-kind finding invented"
          } ]
