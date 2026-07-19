module FS.GG.Governance.Inheritance.Tests.ReferenceFloorTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Route.Model
open FS.GG.Governance.Inheritance.Inheritance
open FS.GG.Governance.Inheritance.Tests.Support

// The embedded reference floor + the `TemplateProfile`-keyed lookup + the pre-rollup fold
// (spec 113 US1/US3, FR-001/FR-002/FR-006/FR-008/FR-009).

let private gateIds (gates: Gate list) = gates |> List.map (fun g -> gateIdValue g.Id)

[<Tests>]
let tests =
    testList
        "ReferenceFloor"
        [ test "the game profile binds the gameplay gate, single-sourced through buildRegistry (FR-001/FR-009)" {
              let gates = referenceGatesFor (TemplateProfile "game")
              Expect.equal (gateIds gates) [ "gameplay:fr-covered" ] "the game floor is the per-FR gameplay gate"
          }

          test "the game gameplay gate binds BLOCKING (block-on-ship) — WI-8 flip (FR-008)" {
              let gate = referenceGatesFor (TemplateProfile "game") |> List.exactlyOne
              Expect.equal gate.Maturity BlockOnShip "WI-5 bound at warn; WI-8 flipped to block-on-ship once WI-7 was green"
          }

          test "an unknown / unbound profile yields no gates — never a fabricated gate (FR-001)" {
              Expect.isEmpty (referenceGatesFor (TemplateProfile "not-a-profile")) "unknown profile inherits nothing"
              Expect.isEmpty (referenceGatesFor (TemplateProfile "")) "empty profile inherits nothing"
          }

          test "productTemplateProfiles reads distinct, sorted profiles off the surfaces (FR-002)" {
              let facts = factsWithProfiles [ "game"; "lib"; "game" ]
              let profiles = productTemplateProfiles facts |> List.map (fun (TemplateProfile p) -> p)
              Expect.equal profiles [ "game"; "lib" ] "deduped and sorted"
          }

          test "a product with no template-profile inherits nothing" {
              Expect.isEmpty (inheritedGatesFor (factsWithProfiles [])) "no bound profile => empty inherited set"
          }

          test "a game product inherits the gameplay gate (FR-002 -> FR-001)" {
              let inherited = inheritedGatesFor (factsWithProfiles [ "game" ])
              Expect.equal (gateIds inherited) [ "gameplay:fr-covered" ] "game inherits its gameplay floor"
          }

          test "applyInheritance is the IDENTITY for a product with no bound profile (FR-006)" {
              let route = mkRoute [ mkSelectedGate (mkGate "build:compile" BlockOnShip) ]
              let out = applyInheritance (factsWithProfiles []) route
              Expect.equal out route "no binding => route returned byte-for-byte"
          }

          test "applyInheritance folds the inherited gate into a game product's selection (US1)" {
              let route = mkRoute [ mkSelectedGate (mkGate "build:compile" BlockOnShip) ]
              let out = applyInheritance (factsWithProfiles [ "game" ]) route
              let ids = out.SelectedGates |> List.map (fun sg -> gateIdValue sg.Gate.Id)
              Expect.equal ids [ "build:compile"; "gameplay:fr-covered" ] "the inherited gate joins the local one"
          }

          test "an inherited-only gate carries an EMPTY selection trace (FR-005)" {
              let route = mkRoute []
              let out = applyInheritance (factsWithProfiles [ "game" ]) route
              let inheritedSel =
                  out.SelectedGates |> List.find (fun sg -> gateIdValue sg.Gate.Id = "gameplay:fr-covered")
              Expect.isEmpty inheritedSel.SelectingPaths "present because inherited, not path-selected"
          }

          test "applyInheritance preserves the local gate's selection trace" {
              let sel = mkSelectedGate (mkGate "build:compile" BlockOnShip)
              let out = applyInheritance (factsWithProfiles [ "game" ]) (mkRoute [ sel ])
              let localSel =
                  out.SelectedGates |> List.find (fun sg -> gateIdValue sg.Gate.Id = "build:compile")
              Expect.equal localSel.SelectingPaths sel.SelectingPaths "the local trace is preserved"
          } ]
