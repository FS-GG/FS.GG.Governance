module FS.GG.Governance.Cli.Tests.HumanRenderSurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.HumanRender

// T045: reflective API surface-drift check for the F27 HumanRender presentation library (Principle II),
// now via the shared SurfaceDrift helper (101/M-CI-3). HumanRender has NO dedicated test project by
// design — its Tier-1 surface contract is covered here in the Cli test suite. The assembly is resolved
// from one of its public types (`typeof<_>.Assembly`), the robust idiom the compiler cannot elide.
let private humanRender = typeof<Watch.WatchModel>.Assembly

[<Tests>]
let tests =
    testList
        "HumanRenderSurfaceDrift"
        [ SurfaceDrift.surfaceTest "HumanRender" "FS.GG.Governance.HumanRender" humanRender ]
