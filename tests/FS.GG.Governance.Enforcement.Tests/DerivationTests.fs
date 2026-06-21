module FS.GG.Governance.Enforcement.Tests.DerivationTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Enforcement.Tests.Support

// US1 — the derivation: worked example (SC-002), observe/warn withhold (FR-007), boundary at/above/
// below floor (FR-008), base-advisory non-escalation (research D4), and the four fixed reason shapes
// (FR-010). Every input is a real typed lever value, driven through the public surface.

// ── Test-side oracles (the truth table from contracts/enforcement-decision.md, re-derived
//    independently so the test does not just echo the implementation) ──

let private maturityToken =
    function
    | Observe -> "observe"
    | Warn -> "warn"
    | BlockOnPr -> "block-on-pr"
    | BlockOnShip -> "block-on-ship"
    | BlockOnRelease -> "block-on-release"

let private modeToken =
    function
    | Sandbox -> "sandbox"
    | Inner -> "inner"
    | Focused -> "focused"
    | Verify -> "verify"
    | Gate -> "gate"
    | RunMode.Release -> "release"

let private profileToken =
    function
    | Light -> "light"
    | Standard -> "standard"
    | Strict -> "strict"
    | Profile.Release -> "release"

let private oracleFloor =
    function
    | Observe
    | Warn -> None
    | BlockOnPr -> Some 4
    | BlockOnShip -> Some 4
    | BlockOnRelease -> Some 5

let private oracleTighten =
    function
    | Light -> 0
    | Standard -> 0
    | Strict -> 1
    | Profile.Release -> 2

let private clamp lo hi v = max lo (min hi v)

let private modeOfOrdinal =
    function
    | 0 -> Sandbox
    | 1 -> Inner
    | 2 -> Focused
    | 3 -> Verify
    | 4 -> Gate
    | _ -> RunMode.Release

/// The expected (effective severity, reason) the contract requires for a given input.
let private oracle (i: EnforcementInput) : Severity * string =
    match i.Maturity with
    | Observe
    | Warn ->
        Advisory,
        sprintf "maturity '%s' withholds blocking; no run mode or profile can make it block" (maturityToken i.Maturity)
    | _ ->
        match i.BaseSeverity with
        | Advisory ->
            Advisory,
            sprintf
                "base severity is advisory; '%s' profile does not escalate it (per-class strictness dials deferred)"
                (profileToken i.Profile)
        | Blocking ->
            let floor = (oracleFloor i.Maturity).Value
            let effectiveFloor = clamp 0 5 (floor - oracleTighten i.Profile)
            let floorMode = modeToken (modeOfOrdinal effectiveFloor)

            if runModeOrdinal i.Mode >= effectiveFloor then
                Blocking,
                sprintf
                    "run mode '%s' reaches the '%s' blocking boundary for maturity '%s' under '%s' profile"
                    (modeToken i.Mode)
                    floorMode
                    (maturityToken i.Maturity)
                    (profileToken i.Profile)
            else
                Advisory,
                sprintf
                    "'%s' profile does not block this '%s' finding outside the '%s' boundary (run mode '%s')"
                    (profileToken i.Profile)
                    (maturityToken i.Maturity)
                    floorMode
                    (modeToken i.Mode)

[<Tests>]
let tests =
    testList
        "Derivation"
        [ test "worked example: blocking/block-on-ship/inner/light => advisory + exact reason (SC-002)" {
              let d =
                  deriveEffectiveSeverity { BaseSeverity = Blocking; Maturity = BlockOnShip; Mode = Inner; Profile = Light }

              Expect.equal d.EffectiveSeverity Advisory "inner is below the gate floor for block-on-ship under light"
              Expect.equal d.BaseSeverity Blocking "base severity carried unchanged"

              Expect.equal
                  d.Reason
                  "'light' profile does not block this 'block-on-ship' finding outside the 'gate' boundary (run mode 'inner')"
                  "exact relaxed reason naming profile, maturity, boundary, run mode"
          }

          test "same finding at gate and at release => blocking (FR-008)" {
              let baseInput = { BaseSeverity = Blocking; Maturity = BlockOnShip; Mode = Inner; Profile = Light }
              let atGate = deriveEffectiveSeverity { baseInput with Mode = Gate }
              let atRelease = deriveEffectiveSeverity { baseInput with Mode = RunMode.Release }

              Expect.equal atGate.EffectiveSeverity Blocking "gate reaches the block-on-ship floor"
              Expect.equal atRelease.EffectiveSeverity Blocking "release is above the block-on-ship floor"

              Expect.equal
                  atGate.Reason
                  "run mode 'gate' reaches the 'gate' blocking boundary for maturity 'block-on-ship' under 'light' profile"
                  "exact blocking reason at the boundary"
          }

          test "observe/warn withhold blocking under every mode × profile (FR-007)" {
              for m in [ Observe; Warn ] do
                  for md in allModes do
                      for p in allProfiles do
                          let d = deriveEffectiveSeverity { BaseSeverity = Blocking; Maturity = m; Mode = md; Profile = p }
                          Expect.equal d.EffectiveSeverity Advisory (sprintf "%A withholds blocking under %A/%A" m md p)

                          Expect.equal
                              d.Reason
                              (sprintf "maturity '%s' withholds blocking; no run mode or profile can make it block" (maturityToken m))
                              "withhold reason"
          }

          test "base-blocking boundary matches the truth table for every maturity × mode × profile (FR-008)" {
              for m in [ BlockOnPr; BlockOnShip; BlockOnRelease ] do
                  for md in allModes do
                      for p in allProfiles do
                          let i = { BaseSeverity = Blocking; Maturity = m; Mode = md; Profile = p }
                          let d = deriveEffectiveSeverity i
                          let expectedSev, expectedReason = oracle i
                          Expect.equal d.EffectiveSeverity expectedSev (sprintf "effective severity for %A" i)
                          Expect.equal d.Reason expectedReason (sprintf "reason for %A" i)
          }

          test "base-advisory never escalates, under every maturity × mode × profile (research D4)" {
              for m in allMaturities do
                  for md in allModes do
                      for p in allProfiles do
                          let i = { BaseSeverity = Advisory; Maturity = m; Mode = md; Profile = p }
                          let d = deriveEffectiveSeverity i
                          Expect.equal d.EffectiveSeverity Advisory (sprintf "base advisory stays advisory for %A" i)
          }

          test "base-advisory (maturity permits blocking) carries the non-escalation reason" {
              let d = deriveEffectiveSeverity { BaseSeverity = Advisory; Maturity = BlockOnShip; Mode = Gate; Profile = Strict }

              Expect.equal
                  d.Reason
                  "base severity is advisory; 'strict' profile does not escalate it (per-class strictness dials deferred)"
                  "base-advisory reason"
          }

          test "every decision over the full sweep matches the oracle and has a non-empty reason (FR-010)" {
              for i in allInputs do
                  let d = deriveEffectiveSeverity i
                  let expectedSev, expectedReason = oracle i
                  Expect.equal d.EffectiveSeverity expectedSev (sprintf "effective severity for %A" i)
                  Expect.equal d.Reason expectedReason (sprintf "reason for %A" i)
                  Expect.isNotEmpty d.Reason "reason is non-empty"
          } ]
