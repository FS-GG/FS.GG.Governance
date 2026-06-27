module FS.GG.Governance.CommandHost.Tests.SurfaceBaselineTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.CommandHost
open FS.GG.Governance.Tests.Common

// Reflective API surface-drift + dependency/scope-hygiene checks for the 075 CommandHost leaf
// (Principle II). Reflection lives ONLY in these tests. Blessed via BLESS_SURFACE=1 dotnet test.

// Touch a public member to force the library assembly to load, then locate it by name.
let private commandHostAsm =
    CommandHost.exitCode CommandHost.Success |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.CommandHost"
        | None -> false)

let private baselinePath =
    Path.Combine(RepositoryHelpers.repoRoot, "surface", "FS.GG.Governance.CommandHost.surface.txt")

let private renderSurface (asm: Assembly) =
    let memberFlags =
        BindingFlags.Public
        ||| BindingFlags.Instance
        ||| BindingFlags.Static
        ||| BindingFlags.DeclaredOnly

    asm.GetExportedTypes()
    |> Array.sortBy (fun t -> t.FullName)
    |> Array.map (fun t ->
        let members =
            t.GetMembers(memberFlags)
            |> Array.map (fun m -> sprintf "  [%A] %s" m.MemberType (m.ToString()))
            |> Array.sort

        String.concat "\n" (Array.append [| sprintf "TYPE %s" t.FullName |] members))
    |> String.concat "\n"

let private normalize (s: string) = s.Replace("\r\n", "\n").TrimEnd()

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ test "CommandHost public surface equals the committed baseline" {
              let actual = renderSurface commandHostAsm

              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

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
