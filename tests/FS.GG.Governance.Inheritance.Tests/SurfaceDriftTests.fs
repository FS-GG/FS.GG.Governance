module FS.GG.Governance.Inheritance.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common

// Reflective API surface-drift + dependency-hygiene checks (Principle II), via the shared SurfaceDrift
// helper. The embedded reference-floor table (`referenceChecks`) and the `refFacts` skeleton are hidden
// (absent from Inheritance.fsi) and must never leak into the public surface.

let private asm = SurfaceDrift.assemblyNamed "FS.GG.Governance.Inheritance"

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "Inheritance" "FS.GG.Governance.Inheritance" asm

          test "the embedded reference-floor table and facts skeleton stay hidden" {
              let surfaceText = SurfaceDrift.renderSurface asm

              for hidden in [ "referenceChecks"; "refFacts" ] do
                  Expect.isFalse (surfaceText.Contains hidden) (sprintf "%s must be hidden (absent from Inheritance.fsi)" hidden)
          }

          test "the surface exposes no verdict/ship/IO/CLI member (pure composition only)" {
              let surfaceText = (SurfaceDrift.renderSurface asm).ToLowerInvariant()

              for forbidden in [ "verdict"; "rollup"; "writeall"; "filestream"; "httpclient" ] do
                  Expect.isFalse (surfaceText.Contains forbidden) (sprintf "no %s member (pure composition)" forbidden)
          }

          SurfaceDrift.referencesOnly
              "Inheritance"
              (fun n ->
                  n = "FS.GG.Governance.Config"
                  || n = "FS.GG.Governance.Gates"
                  || n = "FS.GG.Governance.Route"
                  || n = "FS.GG.Governance.Findings"
                  || n = "FS.GG.Governance.Routing")
              asm ]
