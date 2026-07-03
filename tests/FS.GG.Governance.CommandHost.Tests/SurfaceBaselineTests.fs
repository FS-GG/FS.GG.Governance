module FS.GG.Governance.CommandHost.Tests.SurfaceBaselineTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.CommandHost

// Reflective API surface-drift + dependency/scope-hygiene checks for the 075 CommandHost leaf
// (Principle II), now via the shared SurfaceDrift helper (101/M-CI-3). Reflection lives in the helper and
// here. Blessed via BLESS_SURFACE=1 dotnet test.

// Touch a public member to force the library assembly to load, then locate it by name.
let private commandHostAsm =
    CommandHost.under "." "x" |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.CommandHost"
        | None -> false)

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "CommandHost" "FS.GG.Governance.CommandHost" commandHostAsm

          test "CommandHost takes no host/Cli/Command/edge reference (scope guard — pure leaf)" {
              // The leaf references only the domain-type owners whose values it walks. It must NOT reach the
              // impure host capability: no `Host`, no `Cli`, and no `*Command` host (which own the
              // filesystem/git/process interpreters). It sits BELOW the command hosts and ABOVE the domain owners.
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
