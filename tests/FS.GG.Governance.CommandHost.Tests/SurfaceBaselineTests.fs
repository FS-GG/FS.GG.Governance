module FS.GG.Governance.CommandHost.Tests.SurfaceBaselineTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.CommandHost

// Reflective API surface-drift + dependency/scope-hygiene checks for the 075 CommandHost leaf
// (Principle II), now via the shared SurfaceDrift helper (101/M-CI-3). Reflection lives in the helper and
// here. Blessed via BLESS_SURFACE=1 dotnet test.

let private commandHostAsm = SurfaceDrift.assemblyNamed "FS.GG.Governance.CommandHost"

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "CommandHost" "FS.GG.Governance.CommandHost" commandHostAsm

          test "CommandHost takes no host/Cli/Command reference (scope guard)" {
              // The leaf references the domain-type owners whose values it walks plus the edge owners its
              // shared host leaves delegate to (Snapshot/Config interpreters, the SddHandoff reader — #49).
              // It must NOT reach the ORCHESTRATION host capability: no `Host`, no `Cli`, and no `*Command`
              // host (which own the command loops). It sits BELOW the command hosts and ABOVE the domain owners.
              let forbidden (n: string) =
                  n = "FS.GG.Governance.Host"
                  || n = "FS.GG.Governance.Cli"
                  || (n.StartsWith "FS.GG.Governance" && n.EndsWith "Command")

              let offending =
                  commandHostAsm.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter forbidden

              Expect.isEmpty
                  offending
                  (sprintf "CommandHost must not reference host/Cli/*Command; found: %A" offending)
          } ]
