module FS.GG.Governance.FreshnessResolution.Tests.ResolveTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.FreshnessResolution
open FS.GG.Governance.FreshnessResolution.Model
open FS.GG.Governance.FreshnessResolution.Tests.Support

// User Story 1 — the CARRY path (SC-001, L-carry). For a fully-sensed gate, the `Resolved` `FreshnessInputs`
// carries the gate's four identity fields (Check/Domain/Environment/Command) from its carried `FreshnessKey`
// verbatim and the six sensed fields (RuleHash/CoveredArtifacts/CommandVersion/GeneratorVersion/Base/Head) from
// `SensedFacts` verbatim, with `Cost` DROPPED and nothing fabricated/defaulted/zero-filled.

/// The single outcome of resolving exactly one gate.
let private onlyOutcome (gates: Gate list) (s: SensedFacts) : ResolutionOutcome =
    match FreshnessResolution.entries (FreshnessResolution.resolve gates s) with
    | [ e ] -> e.Outcome
    | other -> failwithf "expected exactly one entry, got %d" (List.length other)

/// The expected resolved value for worked example A (build:tests, fully sensed).
let private expectedA: FreshnessInputs =
    { Check = CheckId "tests"
      Domain = DomainId "build"
      Command = Some dotnetCmd
      Environment = Ci
      RuleHash = RuleHash "rule-1"
      CoveredArtifacts = [ artA; artB ]
      CommandVersion = Some(CommandVersion "8.0")
      GeneratorVersion = GeneratorVersion "gen-1"
      Base = Revision "base-1"
      Head = Revision "head-1" }

[<Tests>]
let tests =
    testList
        "Resolve"
        [ test "worked example A: fully-sensed build:tests ⇒ Resolved with carried identity + sensed facts (SC-001)" {
              Expect.equal (onlyOutcome [ gBuildTests ] fullSensed) (Resolved expectedA) "carry: identity (cost dropped) + sensed facts, verbatim"
          }

          test "the four identity fields equal the carried FreshnessKey verbatim, Command option preserved" {
              match onlyOutcome [ gBuildTests ] fullSensed with
              | Resolved i ->
                  Expect.equal i.Check gBuildTests.FreshnessKey.Check "Check from carried FreshnessKey"
                  Expect.equal i.Domain gBuildTests.FreshnessKey.Domain "Domain from carried FreshnessKey"
                  Expect.equal i.Environment gBuildTests.FreshnessKey.Environment "Environment from carried FreshnessKey"
                  Expect.equal i.Command gBuildTests.FreshnessKey.Command "Command option preserved verbatim"
              | Unresolved facts -> failtestf "expected Resolved, got Unresolved %A" facts
          }

          test "the six sensed fields equal the supplied SensedFacts verbatim" {
              match onlyOutcome [ gBuildTests ] fullSensed with
              | Resolved i ->
                  Expect.equal (Some i.RuleHash) fullSensed.RuleHash "RuleHash sensed verbatim"
                  Expect.equal i.CoveredArtifacts [ artA; artB ] "CoveredArtifacts sensed verbatim (order preserved)"
                  Expect.equal i.CommandVersion (Some(CommandVersion "8.0")) "CommandVersion sensed verbatim"
                  Expect.equal (Some i.GeneratorVersion) fullSensed.GeneratorVersion "GeneratorVersion sensed verbatim"
                  Expect.equal (Some i.Base) fullSensed.Base "Base sensed verbatim"
                  Expect.equal (Some i.Head) fullSensed.Head "Head sensed verbatim"
              | Unresolved facts -> failtestf "expected Resolved, got Unresolved %A" facts
          }

          test "Cost is DROPPED: two gates differing ONLY in Cost resolve to the same FreshnessInputs (SC-001)" {
              // Same id, check, domain, env, command — only Cost differs ⇒ identical resolved value (cost is not
              // a freshness input; it never appears in the ten-field FreshnessInputs).
              let cheap = gateWith "build" "tests" Cheap Ci (Some dotnetCmd)
              let exhaustive = gateWith "build" "tests" Exhaustive Ci (Some dotnetCmd)
              Expect.equal (onlyOutcome [ cheap ] fullSensed) (onlyOutcome [ exhaustive ] fullSensed) "Cost does not affect the resolved FreshnessInputs"
          }

          testPropertyWithConfig fscheckConfig "carry property: any fully-sensed gate resolves to its carried identity + sensed facts, zero fabrication"
          <| fun (g: Gate) ->
              let s = senseFully g
              onlyOutcome [ g ] s = Resolved(expectedResolved g s)

          testPropertyWithConfig fscheckConfig "Cost-drop property: changing only Cost never changes the resolved value"
          <| fun (g: Gate) ->
              let other = if g.Cost = Cheap then Exhaustive else Cheap
              let g2 =
                  { g with
                      Cost = other
                      FreshnessKey = { g.FreshnessKey with Cost = other } }
              // Same Id (Cost is not part of the id) ⇒ senseFully g covers g2 too.
              onlyOutcome [ g ] (senseFully g) = onlyOutcome [ g2 ] (senseFully g) ]
