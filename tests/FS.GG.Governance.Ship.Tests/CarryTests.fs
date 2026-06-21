module FS.GG.Governance.Ship.Tests.CarryTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.Ship.Ship
open FS.GG.Governance.Ship.Tests.Support

// US3: carry / no-hide. Every item carries its full F023 decision; `BaseSeverity` equals the
// input-mapped base and is NEVER altered by the profile (the design's no-hide rule
// docs/initial-design.md:575 / :806). A relaxed blocker is always visible as a warning, never dropped.

// The base severity the gate/finding mapping (research D3/D4) assigns — reconstructed in the test.
let private mappedGateBase (maturity: Maturity) =
    match maturity with
    | Observe | Warn -> Advisory
    | BlockOnPr | BlockOnShip | BlockOnRelease -> Blocking

let private mappedFindingBase (zone: FindingZone) =
    match zone with
    | GovernedRootUnknown -> Advisory
    | ProtectedBoundaryUnknown _ -> Blocking

[<Tests>]
let tests =
    testList
        "Carry"
        [ testPropertyWithConfig fsCheckConfig "every item's BaseSeverity equals the input-mapped base, never altered by profile (SC-003)" (fun (route, mode, profile) ->
              let d = rollup route mode profile
              let gateBases = route.SelectedGates |> List.map (fun g -> GateItem g.Gate.Id, mappedGateBase g.Gate.Maturity)
              let findingBases = route.Findings.Findings |> List.map (fun f -> FindingItem(f.Id, f.Path), mappedFindingBase f.Zone)
              let expected = Map.ofList (gateBases @ findingBases)

              d.Blockers @ d.Warnings @ d.Passing
              |> List.forall (fun item -> item.Decision.BaseSeverity = expected.[item.Id]))

          testPropertyWithConfig fsCheckConfig "every item carries all no-hide fields, with a non-empty reason (FR-005)" (fun (route, mode, profile) ->
              let d = rollup route mode profile

              d.Blockers @ d.Warnings @ d.Passing
              |> List.forall (fun item ->
                  item.Decision.Mode = mode
                  && item.Decision.Profile = profile
                  && not (System.String.IsNullOrWhiteSpace item.Decision.Reason)))

          testPropertyWithConfig fsCheckConfig "every base-Blocking item relaxed to Advisory is visible in Warnings, never dropped (no-hide)" (fun (route, mode, profile) ->
              let d = rollup route mode profile
              // Warnings are exactly the base-Blocking + effective-Advisory items; none silently lost.
              d.Warnings
              |> List.forall (fun i -> i.Decision.BaseSeverity = Blocking && i.Decision.EffectiveSeverity = Advisory))

          test "a shared gate (one Gate, several SelectingPaths) yields exactly one enforced item (FR-010, D7)" {
              // F019 already deduped by GateId; the SelectedGate carries several selecting paths but is
              // one entry. The rollup maps 1:1, so the gate is counted once.
              let gate =
                  { mkSelectedGate (mkGate (GateId "build:shared") BlockOnShip) with
                      SelectingPaths =
                        [ { Path = GovernedPath "src/a.fs"; MatchedGlob = GovernedPath "src/**" }
                          { Path = GovernedPath "src/b.fs"; MatchedGlob = GovernedPath "src/**" } ] }

              let d = rollup (mkRoute [ gate ] []) Gate Standard
              let all = d.Blockers @ d.Warnings @ d.Passing
              Expect.equal all.Length 1 "one gate ⇒ one enforced item regardless of selecting-path count"
          } ]
