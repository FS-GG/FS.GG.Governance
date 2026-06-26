module FS.GG.Governance.EvidenceJson.Tests.FreshnessCauseTests

open System.Text.Json
open Expecto
open FS.GG.Governance.Kernel
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.FreshnessResolution.Model
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.EvidenceJson
open FS.GG.Governance.EvidenceJson.Tests.Support

// US2 — every non-effective node self-describes its freshness cause: stale (named `RecomputeCause`),
// unresolved (named `MissingFact`s), or `unknown` (the only causeless freshness; never a guessed fresh).
// Contract C4 / INV-6. These exercise the projection over real cause vocabulary built directly.

let private freshnessOf (freshness: NodeFreshness) : JsonElement =
    let root = parse (wellFormed [ mkNode "a" Real Real freshness "speckit" ] [] [])
    root.GetProperty("nodes").[0].GetProperty("freshness")

[<Tests>]
let tests =
    testList
        "FreshnessCause"
        [ test "Fresh renders { kind: fresh }" {
              let f = freshnessOf NodeFreshness.Fresh
              Expect.equal (strProp "kind" f) "fresh" "fresh kind"
          }

          test "Stale (InputsChanged cats) names the exact category tokens in core order" {
              let f = freshnessOf (NodeFreshness.Stale(InputsChanged [ RuleHashCat; CoveredArtifactsCat ]))
              Expect.equal (strProp "kind" f) "stale" "stale kind"
              let cause = f.GetProperty("cause")
              Expect.equal (strProp "kind" cause) "inputsChanged" "inputsChanged cause"

              let cats = [ for c in cause.GetProperty("categories").EnumerateArray() -> str c ]
              Expect.equal cats [ categoryToken RuleHashCat; categoryToken CoveredArtifactsCat ] "exact category tokens in core order"
          }

          test "Stale NoPriorEvidence renders cause.kind=noPriorEvidence with NO categories (distinct from inputsChanged [])" {
              let f = freshnessOf (NodeFreshness.Stale NoPriorEvidence)
              let cause = f.GetProperty("cause")
              Expect.equal (strProp "kind" cause) "noPriorEvidence" "noPriorEvidence cause"

              let hasCategories =
                  cause.EnumerateObject() |> Seq.exists (fun p -> p.Name = "categories")

              Expect.isFalse hasCategories "noPriorEvidence carries no categories field"

              // And it is genuinely distinct from an empty inputsChanged.
              let empty = freshnessOf (NodeFreshness.Stale(InputsChanged []))
              Expect.equal (strProp "kind" (empty.GetProperty("cause"))) "inputsChanged" "inputsChanged [] keeps its kind"
              Expect.equal (empty.GetProperty("cause").GetProperty("categories").GetArrayLength()) 0 "inputsChanged [] has an empty categories array"
          }

          test "Unresolved names every missing fact via missingFactToken (non-empty)" {
              let missing = [ MissingCoveredArtifacts; MissingHeadRevision ]
              let f = freshnessOf (NodeFreshness.Unresolved missing)
              Expect.equal (strProp "kind" f) "unresolved" "unresolved kind"

              let named = [ for m in f.GetProperty("missing").EnumerateArray() -> str m ]
              // Assert against the real token authority, not a guess.
              Expect.equal named (missing |> List.map FS.GG.Governance.FreshnessResolution.FreshnessResolution.missingFactToken) "named via missingFactToken"
              Expect.isGreaterThan named.Length 0 "non-empty missing list"
          }

          test "Unknown renders { kind: unknown } — the only causeless freshness (never a guessed fresh)" {
              let f = freshnessOf NodeFreshness.Unknown
              Expect.equal (strProp "kind" f) "unknown" "unknown kind"
              Expect.isFalse (f.EnumerateObject() |> Seq.exists (fun p -> p.Name = "cause")) "no cause on unknown"
          } ]
