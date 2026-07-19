module FS.GG.Governance.Inheritance.Tests.CompositionTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Inheritance.Inheritance
open FS.GG.Governance.Inheritance.Tests.Support

// The non-lowerable-floor contract of `composeEffectiveGates` (spec 113 US2/FR-003/FR-004/FR-005) and
// the `maturityRank` order it reduces against. Real typed gates only (Principle V).

let private maturityOf (id: string) (gates: Gate list) =
    gates
    |> List.tryFind (fun g -> gateIdValue g.Id = id)
    |> Option.map (fun g -> g.Maturity)

[<Tests>]
let tests =
    testList
        "Composition"
        [ test "maturityRank is the closed order observe<warn<block-on-pr<block-on-ship<block-on-release" {
              let ranked = [ Observe; Warn; BlockOnPr; BlockOnShip; BlockOnRelease ]
              let ranks = ranked |> List.map maturityRank
              Expect.equal ranks [ 1; 2; 3; 4; 5 ] "maturityRank must be the strict 1..5 enforcement order"
              Expect.equal (List.sortBy maturityRank (List.rev ranked)) ranked "sorting by rank recovers the order"
          }

          test "a local gate at a WEAKER maturity is raised to the inherited floor (FR-004: cannot lower)" {
              let inherited = [ mkGate "gameplay:fr-covered" BlockOnShip ]
              let local = [ mkGate "gameplay:fr-covered" Warn ]
              let effective = composeEffectiveGates inherited local
              Expect.equal (List.length effective) 1 "the shared id yields exactly one gate (deduped)"
              Expect.equal (maturityOf "gameplay:fr-covered" effective) (Some BlockOnShip) "raised to the floor"
          }

          test "a local gate at a STRONGER maturity keeps its stronger choice (FR-004: local may raise)" {
              let inherited = [ mkGate "gameplay:fr-covered" BlockOnShip ]
              let local = [ mkGate "gameplay:fr-covered" BlockOnRelease ]
              let effective = composeEffectiveGates inherited local
              Expect.equal (maturityOf "gameplay:fr-covered" effective) (Some BlockOnRelease) "local stronger stays"
          }

          test "an inherited-only gate is added verbatim (FR-005)" {
              let inherited = [ mkGate "gameplay:fr-covered" Warn ]
              let local = [ mkGate "build:compile" BlockOnShip ]
              let effective = composeEffectiveGates inherited local
              let ids = effective |> List.map (fun g -> gateIdValue g.Id)
              Expect.equal ids [ "build:compile"; "gameplay:fr-covered" ] "both present, sorted by GateId"
              Expect.equal (maturityOf "gameplay:fr-covered" effective) (Some Warn) "inherited maturity carried"
          }

          test "a local-only gate is kept unchanged" {
              let effective = composeEffectiveGates [] [ mkGate "build:compile" BlockOnShip ]
              Expect.equal (maturityOf "build:compile" effective) (Some BlockOnShip) "unchanged"
          }

          test "empty inherited => local unchanged (FR-006 identity)" {
              let local = [ mkGate "b:y" Warn; mkGate "a:x" BlockOnShip ]
              let effective = composeEffectiveGates [] local
              // Same gates, same maturities (order is the deterministic GateId sort either way).
              Expect.equal
                  (effective |> List.map (fun g -> gateIdValue g.Id, g.Maturity))
                  [ "a:x", BlockOnShip; "b:y", Warn ]
                  "no gate added, no maturity changed"
          }

          test "the effective set is deterministic: sorted by GateId ordinal regardless of input order" {
              let inherited = [ mkGate "z:1" Warn ]
              let local = [ mkGate "m:1" BlockOnShip; mkGate "a:1" Warn ]
              let ids = composeEffectiveGates inherited local |> List.map (fun g -> gateIdValue g.Id)
              Expect.equal ids [ "a:1"; "m:1"; "z:1" ] "GateId ordinal sort"
          } ]
