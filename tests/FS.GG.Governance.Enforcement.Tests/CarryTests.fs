module FS.GG.Governance.Enforcement.Tests.CarryTests

open Expecto
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Enforcement.Tests.Support

// US3 — carry + no-drop (FR-009, FR-012, SC-003, SC-006): a profile may reclassify EFFECTIVE
// severity but never alters the reported base severity / maturity / mode, and never erases a finding.
// Mapping the derivation over N findings yields exactly N decisions.

[<Tests>]
let tests =
    testList
        "Carry"
        [ test "base severity is byte-identical out=in across the full sweep (SC-003, FR-009)" {
              for i in allInputs do
                  let d = deriveEffectiveSeverity i
                  Expect.equal d.BaseSeverity i.BaseSeverity (sprintf "base severity carried for %A" i)
          }

          test "maturity/mode/profile carry through unchanged for every input (FR-009)" {
              for i in allInputs do
                  let d = deriveEffectiveSeverity i
                  Expect.equal d.Maturity i.Maturity (sprintf "maturity carried for %A" i)
                  Expect.equal d.Mode i.Mode (sprintf "mode carried for %A" i)
                  Expect.equal d.Profile i.Profile (sprintf "profile carried for %A" i)
          }

          testPropertyWithConfig fsCheckConfig "base severity always carries through (SC-003)" (fun (i: EnforcementInput) ->
              (deriveEffectiveSeverity i).BaseSeverity = i.BaseSeverity)

          test "relax-vs-strict: Light and Profile.Release report the same base/maturity/mode (US3 AS2)" {
              for s in allSeverities do
                  for m in allMaturities do
                      for md in allModes do
                          let light = deriveEffectiveSeverity { BaseSeverity = s; Maturity = m; Mode = md; Profile = Light }
                          let strict = deriveEffectiveSeverity { BaseSeverity = s; Maturity = m; Mode = md; Profile = Profile.Release }
                          Expect.equal light.BaseSeverity strict.BaseSeverity "same base severity"
                          Expect.equal light.Maturity strict.Maturity "same maturity"
                          Expect.equal light.Mode strict.Mode "same mode"
                          // Only EffectiveSeverity / Reason may differ — the profile is the only changed lever.
          }

          test "no-drop: mapping the derivation over an N-finding list yields N decisions (SC-006)" {
              let decisions = allInputs |> List.map deriveEffectiveSeverity
              Expect.equal decisions.Length allInputs.Length "one decision per input, none dropped"
              // order-preserving: each decision's levers match the input at the same index.
              List.zip allInputs decisions
              |> List.iter (fun (i, d) ->
                  Expect.equal d.BaseSeverity i.BaseSeverity "index-aligned base severity"
                  Expect.equal d.Mode i.Mode "index-aligned mode")
          } ]
