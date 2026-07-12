module FS.GG.Governance.EvidenceCommand.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.EvidenceCommand

// Reflective API surface-drift check (Principle II), now via the shared SurfaceDrift helper (101/M-CI-3).
// Reflection lives in the helper and here, never in the host.

let private evidenceCommand = SurfaceDrift.assemblyNamed "FS.GG.Governance.EvidenceCommand"

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "EvidenceCommand" "FS.GG.Governance.EvidenceCommand" evidenceCommand ]
