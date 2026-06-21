module FS.GG.Governance.Enforcement.Tests.TotalityTests

open Expecto
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Enforcement.Tests.Support

// US1 — totality (FR-005, SC-001): the derivation is defined for every one of the finite input
// combinations and never throws. All inputs are real enumerated lever values — no synthetic evidence
// (the domain is finite and literally constructible).

[<Tests>]
let tests =
    testList
        "Totality"
        [ test "deriveEffectiveSeverity over the full 240-input cross-product never throws (SC-001)" {
              for i in allInputs do
                  let d = deriveEffectiveSeverity i
                  // Touch every field so the decision must be fully constructed.
                  ignore (d.BaseSeverity, d.Maturity, d.Mode, d.Profile, d.EffectiveSeverity, d.Reason)
          }

          test "every decision carries all six fields with a non-empty reason (FR-010)" {
              for i in allInputs do
                  let d = deriveEffectiveSeverity i
                  Expect.equal d.BaseSeverity i.BaseSeverity "base severity present and echoed"
                  Expect.equal d.Maturity i.Maturity "maturity present and echoed"
                  Expect.equal d.Mode i.Mode "mode present and echoed"
                  Expect.equal d.Profile i.Profile "profile present and echoed"
                  Expect.isNotEmpty d.Reason (sprintf "non-empty reason for %A" i)
          }

          testPropertyWithConfig fsCheckConfig "deriveEffectiveSeverity is total over generated inputs (SC-001)" (fun (i: EnforcementInput) ->
              let d = deriveEffectiveSeverity i
              d.Reason.Length > 0) ]
