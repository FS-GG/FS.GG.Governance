module FS.GG.Governance.RuleIdentity.Tests.SurfaceBaselineTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.RuleIdentity

// Reflective API surface-drift + dependency/scope-hygiene checks for the 068 leaf (Principle II, plan D7),
// now via the shared SurfaceDrift helper (101/M-CI-3). Reflection lives in the helper and here, never in the
// library. Blessed via BLESS_SURFACE=1 (T025).

let private ruleIdentityAsm = SurfaceDrift.assemblyNamed "FS.GG.Governance.RuleIdentity"

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "RuleIdentity" "FS.GG.Governance.RuleIdentity" ruleIdentityAsm

          // The leaf has NO governance ProjectReference: it cannot introduce a cycle and any projection may
          // reference it (research D7). The allowed set is therefore BCL/FSharp.Core only.
          SurfaceDrift.referencesOnly "RuleIdentity" (fun _ -> false) ruleIdentityAsm ]
