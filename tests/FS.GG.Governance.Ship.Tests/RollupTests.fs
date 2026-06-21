module FS.GG.Governance.Ship.Tests.RollupTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.Ship.Ship
open FS.GG.Governance.Ship.Tests.Support

// US1: `Ship.rollup` maps (RouteResult, RunMode, Profile) -> ShipDecision over REAL fixtures. The
// hidden gate/finding -> EnforcementInput mappings and the partition are exercised ONLY through
// `rollup` (Principle V) — no mocks, no private helpers.

let private gate id maturity = mkSelectedGate (mkGate (GateId id) maturity)

[<Tests>]
let tests =
    testList
        "Rollup"
        [ test "all-advisory route ⇒ pass / clean at any mode and profile (US1 AS1)" {
              let route =
                  mkRoute
                      [ gate "build:obs" Observe; gate "build:warn" Warn ]
                      [ mkFinding UnknownGovernedPath (GovernedPath "src/x.fs") GovernedRootUnknown ]

              for mode in allModes do
                  for profile in allProfiles do
                      let d = rollup route mode profile
                      Expect.equal d.Verdict Pass (sprintf "pass at %A/%A" mode profile)
                      Expect.isEmpty d.Blockers "no blockers"
                      Expect.equal d.ExitCodeBasis Clean "clean basis"
                      Expect.equal d.Passing.Length 3 "all three items passing"
          }

          test "a block-on-* gate at a reaching mode ⇒ fail / blocked (US1 AS2, FR-002/FR-007)" {
              let route = mkRoute [ gate "build:ship" BlockOnShip ] []
              let d = rollup route Gate Strict
              Expect.equal d.Verdict Fail "verdict fail"
              Expect.equal d.ExitCodeBasis Blocked "blocked basis"
              Expect.equal d.Blockers.Length 1 "exactly one blocker"
              Expect.isTrue
                  (d.Blockers |> List.forall (fun i -> i.Decision.EffectiveSeverity = Blocking))
                  "blockers are exactly the effective-Blocking items"
          }

          test "a base-blocking item relaxed to advisory lands in Warnings carrying full detail (US1 AS3)" {
              let route = mkRoute [ gate "build:ship" BlockOnShip ] []
              let d = rollup route Inner Light
              Expect.equal d.Verdict Pass "relaxed ⇒ no blocker ⇒ pass"
              Expect.equal d.Warnings.Length 1 "one warning"
              let w = d.Warnings.Head
              Expect.equal w.Decision.BaseSeverity Blocking "base severity still Blocking (no-hide)"
              Expect.equal w.Decision.EffectiveSeverity Advisory "effective relaxed to Advisory"
              Expect.equal w.Decision.Mode Inner "carries run mode"
              Expect.equal w.Decision.Profile Light "carries profile"
              Expect.equal w.Decision.Maturity BlockOnShip "carries maturity"
              Expect.isFalse (System.String.IsNullOrWhiteSpace w.Decision.Reason) "carries a non-empty reason"
          }

          test "Observe/Warn gates and GovernedRootUnknown findings are always Passing (FR-011)" {
              let route =
                  mkRoute
                      [ gate "build:obs" Observe; gate "build:warn" Warn ]
                      [ mkFinding UnknownGovernedPath (GovernedPath "src/r.fs") GovernedRootUnknown ]
              // Even under the strictest mode/profile, base-advisory items never escalate.
              let d = rollup route RunMode.Release Profile.Release
              Expect.equal d.Passing.Length 3 "all base-advisory ⇒ all Passing"
              Expect.isEmpty d.Blockers "never escalated to blockers"
              Expect.isEmpty d.Warnings "never warnings"
          }

          test "a protected-boundary finding blocks at Gate with NO gate selected (edge, research D4)" {
              let route =
                  mkRoute [] [ mkFinding UnknownProtectedBoundaryPath (GovernedPath "api/v1.fs") (ProtectedBoundaryUnknown(SurfaceId "api")) ]

              let d = rollup route Gate Standard
              Expect.equal d.Verdict Fail "escalated finding blocks with no gate"
              Expect.equal d.Blockers.Length 1 "the finding is the sole blocker"
              match d.Blockers.Head.Id with
              | FindingItem(UnknownProtectedBoundaryPath, _) -> ()
              | other -> failtestf "expected the protected-boundary finding as blocker, got %A" other
          }

          testPropertyWithConfig fsCheckConfig
              "every item's partition membership agrees with its own derived severity (SC-001)"
              (fun (route, mode, profile) ->
                  let d = rollup route mode profile
                  let ok (items: EnforcedItem list) pred = items |> List.forall pred
                  ok d.Blockers (fun i -> i.Decision.EffectiveSeverity = Blocking)
                  && ok d.Warnings (fun i -> i.Decision.EffectiveSeverity = Advisory && i.Decision.BaseSeverity = Blocking)
                  && ok d.Passing (fun i -> i.Decision.BaseSeverity = Advisory)
                  && (d.Verdict = (if List.isEmpty d.Blockers then Pass else Fail))) ]
