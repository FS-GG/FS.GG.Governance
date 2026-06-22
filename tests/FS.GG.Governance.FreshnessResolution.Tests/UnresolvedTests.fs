module FS.GG.Governance.FreshnessResolution.Tests.UnresolvedTests

open Expecto
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.FreshnessResolution
open FS.GG.Governance.FreshnessResolution.Model
open FS.GG.Governance.FreshnessResolution.Tests.Support

// User Story 2 — NO-FABRICATE + NO-HIDE (SC-002, L-no-fabricate / L-no-hide). A gate missing one or more
// required facts is `Unresolved` naming EXACTLY and EVERY gap (in `MissingFact` enum order), produces no
// `FreshnessInputs`, and fabricates/defaults/zero-fills nothing. The six wire tokens are stable and injective.

let private onlyOutcome (gates: Gate list) (s: SensedFacts) : ResolutionOutcome =
    match FreshnessResolution.entries (FreshnessResolution.resolve gates s) with
    | [ e ] -> e.Outcome
    | other -> failwithf "expected exactly one entry, got %d" (List.length other)

/// Worked example C: lint:style with RuleHash/Base unsensed, gate id absent from CoveredArtifacts, eslint absent
/// from CommandVersions; generator + head present.
let private cSensed: SensedFacts =
    { RuleHash = None
      GeneratorVersion = Some(GeneratorVersion "gen-1")
      Base = None
      Head = Some(Revision "head-1")
      CoveredArtifacts = Map.empty
      CommandVersions = Map.empty }

let private allFacts =
    [ MissingRuleHash
      MissingCoveredArtifacts
      MissingCommandVersion
      MissingGeneratorVersion
      MissingBaseRevision
      MissingHeadRevision ]

[<Tests>]
let tests =
    testList
        "Unresolved"
        [ test "single dropped fact ⇒ Unresolved [thatFact] and NO FreshnessInputs (SC-002, no-fabricate)" {
              // gLintStyle is fully sensed by fullSensed; drop EXACTLY one fact at a time.
              for (fact, drop) in gapTable gLintStyle do
                  let outcome = onlyOutcome [ gLintStyle ] (drop fullSensed)
                  Expect.isFalse (FreshnessResolution.isResolved outcome) (sprintf "dropping %A ⇒ not Resolved" fact)
                  Expect.equal (FreshnessResolution.missingFacts outcome) [ fact ] (sprintf "dropping %A names exactly that fact" fact)
          }

          test "worked example C: several facts unsensed ⇒ Unresolved naming every gap in enum order (no-hide)" {
              let outcome = onlyOutcome [ gLintStyle ] cSensed

              Expect.equal
                  (FreshnessResolution.missingFacts outcome)
                  [ MissingRuleHash; MissingCoveredArtifacts; MissingCommandVersion; MissingBaseRevision ]
                  "every gap named, in enum order, never truncated to the first"
          }

          test "worked example C tokens are ruleHash/coveredArtifacts/commandVersion/baseRevision" {
              let outcome = onlyOutcome [ gLintStyle ] cSensed
              let tokens = FreshnessResolution.missingFacts outcome |> List.map FreshnessResolution.missingFactToken
              Expect.equal tokens [ "ruleHash"; "coveredArtifacts"; "commandVersion"; "baseRevision" ] "no-hide tokens for example C"
          }

          test "an Unresolved outcome carries ONLY the MissingFact list — no fabricated hash/version/revision" {
              // Structural: missingFacts is the entire payload; there is no FreshnessInputs to inspect.
              let outcome = onlyOutcome [ gLintStyle ] cSensed
              Expect.equal (FreshnessResolution.candidate { Gate = gLintStyle.Id; Outcome = outcome }) None "no candidate ⇒ no fabricated inputs"
              Expect.isNonEmpty (FreshnessResolution.missingFacts outcome) "Unresolved always names ≥1 gap"
          }

          test "all six missingFactToken values are exact and pairwise distinct (stable, injective wire vocabulary)" {
              Expect.equal (FreshnessResolution.missingFactToken MissingRuleHash) "ruleHash" "ruleHash"
              Expect.equal (FreshnessResolution.missingFactToken MissingCoveredArtifacts) "coveredArtifacts" "coveredArtifacts"
              Expect.equal (FreshnessResolution.missingFactToken MissingCommandVersion) "commandVersion" "commandVersion"
              Expect.equal (FreshnessResolution.missingFactToken MissingGeneratorVersion) "generatorVersion" "generatorVersion"
              Expect.equal (FreshnessResolution.missingFactToken MissingBaseRevision) "baseRevision" "baseRevision"
              Expect.equal (FreshnessResolution.missingFactToken MissingHeadRevision) "headRevision" "headRevision"
              let tokens = allFacts |> List.map FreshnessResolution.missingFactToken
              Expect.equal (List.distinct tokens |> List.length) 6 "the six tokens are pairwise distinct (injective)"
          }

          testPropertyWithConfig fscheckConfig "no-hide property: missingFacts equals EXACTLY the omitted facts (enum order), neither subset nor superset"
          <| fun (g: Gate) (s: SensedFacts) ->
              FreshnessResolution.missingFacts (onlyOutcome [ g ] s) = expectedMissing g s

          testPropertyWithConfig fscheckConfig "a partially-sensed gate is Unresolved iff at least one required fact is missing, and then names ≥1 gap"
          <| fun (g: Gate) (s: SensedFacts) ->
              let outcome = onlyOutcome [ g ] s
              let missing = expectedMissing g s

              if List.isEmpty missing then
                  FreshnessResolution.isResolved outcome
              else
                  not (FreshnessResolution.isResolved outcome)
                  && not (List.isEmpty (FreshnessResolution.missingFacts outcome)) ]
