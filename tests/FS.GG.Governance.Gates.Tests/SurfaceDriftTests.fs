module FS.GG.Governance.Gates.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Gates.Tests.Support

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, research D1).
// Reflection lives ONLY in these tests, never in the library.

let private gates = typeof<GateId>.Assembly

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.Gates.surface.txt")

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
        [ test "Gates public surface equals the committed baseline" {
              let actual = renderSurface gates

              // Bless path: BLESS_SURFACE=1 (re)writes the baseline intentionally.
              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "the public surface is exactly the two modules Model and Gates, nothing private" {
              let typeNames =
                  gates.GetExportedTypes()
                  |> Array.choose (fun t -> Option.ofObj t.FullName)
                  |> Array.sort

              let hasModel = typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.Gates.ModelModule")
              let hasGates = typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.Gates.GatesModule")
              Expect.isTrue hasModel "Model module is public"
              Expect.isTrue hasGates "Gates module is public"
          }

          test "Gates references only Config + BCL + FSharp.Core — NO Routing, NO kernel (FR-013/D1 scope guard)" {
              // No Routing edge (unlike Findings), and no kernel/host/adapter/Snapshot/Findings/CLI
              // dependency, and no git/CI/enforcement/severity package — the absence confirms no
              // later-phase capability leaked into F018. The transitive YamlDotNet arrives only via
              // Config and is unused by Gates' own code.
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "FS.GG.Governance.Config"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."

              let offending =
                  gates.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty
                  offending
                  (sprintf "Gates must depend on Config/BCL/FSharp.Core only; found: %A" offending)

              // Specifically: NOT Routing, the kernel/host/adapters/Snapshot/Findings/CLI.
              let forbidden =
                  gates.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (fun n ->
                      n = "FS.GG.Governance.Routing"
                      || n = "FS.GG.Governance.Kernel"
                      || n = "FS.GG.Governance.Host"
                      || n = "FS.GG.Governance.Snapshot"
                      || n = "FS.GG.Governance.Findings"
                      || n = "FS.GG.Governance.Cli"
                      || n.StartsWith "FS.GG.Governance.Adapters")

              Expect.isEmpty
                  forbidden
                  (sprintf "Gates must not reference Routing/kernel/host/adapters/Snapshot/Findings/CLI; found: %A" forbidden)
          } ]
