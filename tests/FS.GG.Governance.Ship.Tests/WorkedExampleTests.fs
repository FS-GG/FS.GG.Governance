module FS.GG.Governance.Ship.Tests.WorkedExampleTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.Ship.Ship
open FS.GG.Governance.Ship.Tests.Support

// US2: the design's worked example (docs/initial-design.md:516 — base blocking, block-on-ship, run
// mode inner, profile light ⇒ effective advisory) reproduces at CHANGE scale, and a mode/profile
// change moves an item between warning and blocker WITHOUT altering any input (FR-006, no-hide).

let private gate id maturity = mkSelectedGate (mkGate (GateId id) maturity)

// A multi-item change containing the worked-example BlockOnShip gate plus other items.
let private multiItemRoute =
    mkRoute
        [ gate "build:ship" BlockOnShip; gate "build:obs" Observe ]
        [ mkFinding UnknownGovernedPath (GovernedPath "src/x.fs") GovernedRootUnknown ]

[<Tests>]
let tests =
    testList
        "WorkedExample"
        [ test "worked example at inner/light is a self-explaining warning, not a blocker (SC-002)" {
              let d = rollup multiItemRoute Inner Light

              let shipItem =
                  d.Warnings
                  |> List.find (fun i -> i.Id = GateItem(GateId "build:ship"))

              Expect.equal shipItem.Decision.EffectiveSeverity Advisory "effective advisory at inner/light"
              Expect.equal shipItem.Decision.BaseSeverity Blocking "base severity still blocking"
              Expect.stringContains (shipItem.Decision.Reason.ToLowerInvariant()) "gate" "reason names the 'gate' boundary"
              Expect.isEmpty d.Blockers "the worked-example item contributes no blocker"
              Expect.equal d.Verdict Pass "verdict reflects only the (passing) other items"
          }

          test "same-rollup blocker + warning at gate/light (SC-002 second half)" {
              let route = mkRoute [ gate "build:ship" BlockOnShip; gate "build:rel" BlockOnRelease ] []
              let d = rollup route Gate Light

              Expect.equal d.Verdict Fail "verdict fail"
              Expect.equal d.ExitCodeBasis Blocked "blocked basis"
              Expect.equal (d.Blockers |> List.map (fun i -> i.Id)) [ GateItem(GateId "build:ship") ] "block-on-ship is the blocker"
              Expect.equal (d.Warnings |> List.map (fun i -> i.Id)) [ GateItem(GateId "build:rel") ] "block-on-release is the warning"
          }

          test "lever-only flip: same RouteResult, only mode/profile differ (US2 AS2, FR-006)" {
              let route = mkRoute [ gate "build:ship" BlockOnShip ] []

              let atInner = rollup route Inner Light
              let atGate = rollup route Gate Light

              // The SAME input value drives both — only the mode argument differs.
              Expect.equal atInner.Verdict Pass "inner/light ⇒ pass (warning)"
              Expect.equal atGate.Verdict Fail "gate/light ⇒ fail (blocker)"
              Expect.equal atInner.Warnings.Length 1 "warning at inner"
              Expect.equal atGate.Blockers.Length 1 "blocker at gate"
          }

          test "base severity is byte-identical to the input-mapped base for every item (SC-003)" {
              for mode in allModes do
                  for profile in allProfiles do
                      let d = rollup multiItemRoute mode profile

                      for item in d.Blockers @ d.Warnings @ d.Passing do
                          let expectedBase =
                              match item.Id with
                              | GateItem(GateId "build:ship") -> Blocking
                              | GateItem(GateId "build:obs") -> Advisory
                              | FindingItem(UnknownGovernedPath, _) -> Advisory
                              | other -> failtestf "unexpected item %A" other

                          Expect.equal item.Decision.BaseSeverity expectedBase (sprintf "base carry for %A at %A/%A" item.Id mode profile)
          } ]
