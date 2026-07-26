module FS.GG.Governance.Adapters.SddHandoff.Tests.JourneyGateTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Adapters.SddHandoff
open FS.GG.Governance.Adapters.SddHandoff.Model

let private parse name =
    match Reader.parse (Fixtures.read name) with
    | Ok handoff -> handoff
    | Error diagnostic -> failtestf "fixture %s must parse: %A" name diagnostic

[<Tests>]
let tests =
    testList
        "ProductionJourney"
        [ test "published SDD 0.30 producer golden is typed and passes" {
              // Producer evidence: copied from FS.GG.SDD commit 25c7380, path
              // tests/FS.GG.SDD.Commands.Tests/goldens/full-shape/governance-handoff.json.
              let handoff = parse "sdd-0.30-producer-golden"
              Expect.equal handoff.GeneratorVersion (Some "FS.GG.SDD.Artifacts/0.30.0") "producer line is preserved"

              let readiness = handoff.JourneyReadiness |> Option.get
              Expect.equal readiness.ObligationsUnmet 0 "the reference production journey is satisfied"
              Expect.equal readiness.Disposition JourneySatisfied "zero unmet has the satisfied disposition"

              let gate = Journey.toGate (Fixtures.read "sdd-0.30-producer-golden").Source readiness
              Expect.equal gate.Maturity Warn "satisfied journey evidence is advisory"
          }

          test "Synthetic Rogue-shaped helper evidence stays green while production journey blocks" {
              // SYNTHETIC: the helper-only handoff is a compact negative contract fixture.
              let handoff = parse "rogue-helper-only-journey"
              let helper = handoff.Evidence.Nodes |> List.exactlyOne
              Expect.equal helper.State Real "generic/helper evidence remains real"

              let readiness = handoff.JourneyReadiness |> Option.get
              Expect.equal readiness.ObligationsUnmet 1 "the production journey remains unmet"
              Expect.equal readiness.Disposition JourneyReceiptInvalid "missing receipt fails closed as invalid"
              Expect.contains readiness.RelatedIds "FR-DOOR-TRANSITION" "affected obligation is preserved"

              let gate = Journey.toGate (Fixtures.read "rogue-helper-only-journey").Source readiness
              Expect.equal gate.Maturity BlockOnShip "unmet production journey blocks ship"
              Expect.equal
                  (gateIdValue gate.Id)
                  "gameplay:production-journey:rogue-helper-only-journey"
                  "the gate is organization-domain-qualified"
              Expect.stringContains gate.Description "FR-DOOR-TRANSITION" "affected obligation is actionable"
              Expect.stringContains
                  gate.Description
                  "evidence.productionJourneyReceiptInvalid"
                  "producer diagnostic is preserved"
          }

          test "stale receipt provenance remains a distinct blocking disposition" {
              let readiness =
                  { ObligationsUnmet = 1
                    BlockingDiagnosticIds = [ "evidence.productionJourneyReceiptStale" ]
                    RelatedIds = [ "scenario:boot-to-win" ]
                    Disposition = JourneyReceiptStale }

              let gate = Journey.toGate "readiness/stale/governance-handoff.json" readiness
              Expect.equal gate.Maturity BlockOnShip "stale receipt blocks"
              Expect.stringContains gate.Description "JourneyReceiptStale" "disposition remains visible"
          } ]
