module FS.GG.Governance.Findings.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Findings.Tests.Support

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, research D1).
// Reflection lives ONLY in these tests, never in the library.

let private findings = typeof<FindingId>.Assembly

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.Findings.surface.txt")

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
        [ test "Findings public surface equals the committed baseline" {
              let actual = renderSurface findings

              // Bless path: BLESS_SURFACE=1 (re)writes the baseline intentionally.
              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "the public surface is exactly the two modules Model and Findings, nothing private" {
              let typeNames =
                  findings.GetExportedTypes()
                  |> Array.choose (fun t -> Option.ofObj t.FullName)
                  |> Array.sort

              // The two module suffixes (ModelModule/FindingsModule) and the DU nested types
              // under Model; the only top-level F# modules are Model and Findings.
              let hasModel = typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.Findings.ModelModule")
              let hasFindings = typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.Findings.FindingsModule")
              Expect.isTrue hasModel "Model module is public"
              Expect.isTrue hasFindings "Findings module is public"
          }

          test "Findings references only Config + Routing + BCL + FSharp.Core (FR-013/FR-015 scope guard)" {
              // No kernel/host/adapter/Snapshot/CLI dependency, and no git/CI/gate/enforcement/
              // severity package — the absence confirms no later-phase capability leaked into F017.
              // The transitive YamlDotNet arrives only via Config and is unused by Findings' code.
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "FS.GG.Governance.Config"
                  || name = "FS.GG.Governance.Routing"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."

              let offending =
                  findings.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty
                  offending
                  (sprintf "Findings must depend on Config/Routing/BCL/FSharp.Core only; found: %A" offending)

              // Specifically: NOT the kernel/host/adapters/Snapshot/CLI.
              let forbidden =
                  findings.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (fun n ->
                      n = "FS.GG.Governance.Kernel"
                      || n = "FS.GG.Governance.Host"
                      || n = "FS.GG.Governance.Snapshot"
                      || n = "FS.GG.Governance.Cli"
                      || n.StartsWith "FS.GG.Governance.Adapters")

              Expect.isEmpty forbidden (sprintf "Findings must not reference kernel/host/adapters/Snapshot/CLI; found: %A" forbidden)
          } ]
