module FS.GG.Governance.Enforcement.Tests.DeterminismTests

open Expecto
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Enforcement.Tests.Support

// US1 — determinism (FR-006, SC-004): the derivation reads only the four typed lever inputs (no
// clock, environment, ordering, or host-path value exists on EnforcementInput), so identical inputs
// always yield byte-identical EffectiveSeverity AND byte-identical Reason text.

[<Tests>]
let tests =
    testList
        "Determinism"
        [ test "twice-run derivation is byte-identical for every input in the full sweep (SC-004)" {
              for i in allInputs do
                  let a = deriveEffectiveSeverity i
                  let b = deriveEffectiveSeverity i
                  Expect.equal a.EffectiveSeverity b.EffectiveSeverity (sprintf "effective severity stable for %A" i)
                  Expect.equal a.Reason b.Reason (sprintf "reason string stable for %A" i)
          }

          testPropertyWithConfig fsCheckConfig "twice-run derivation is byte-identical for generated inputs (SC-004)" (fun (i: EnforcementInput) ->
              let a = deriveEffectiveSeverity i
              let b = deriveEffectiveSeverity i
              a.EffectiveSeverity = b.EffectiveSeverity && a.Reason = b.Reason) ]
