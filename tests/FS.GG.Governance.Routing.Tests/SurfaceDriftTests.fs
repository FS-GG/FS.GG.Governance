module FS.GG.Governance.Routing.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.Routing.Model
open FS.GG.Governance.Routing.Tests.Support

// Reflective API surface-drift + dependency-hygiene checks (Principle II, research D1).
// Reflection lives ONLY in these tests, never in the library.

let private routing = typeof<RoutingDiagnosticId>.Assembly

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.Routing.surface.txt")

/// Render the assembly's public surface to canonical, sorted text. Any change to the public
/// surface changes this text and trips the baseline assertion.
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
        [ test "Routing public surface equals the committed baseline" {
              let actual = renderSurface routing

              // Bless path: BLESS_SURFACE=1 (re)writes the baseline intentionally.
              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "Routing references only FS.GG.Governance.Config + BCL + FSharp.Core (FR-016 scope guard)" {
              // No kernel/host/adapter/CLI dependency, and no git/CI/gate/enforcement/command
              // package — the absence confirms no later-phase capability leaked into F015. The
              // transitive YamlDotNet arrives only via Config and is unused by Routing's code.
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "FS.GG.Governance.Config"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."

              let offending =
                  routing.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty
                  offending
                  (sprintf "Routing must depend on FS.GG.Governance.Config/BCL/FSharp.Core only; found: %A" offending)
          } ]
