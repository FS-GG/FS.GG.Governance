module FS.GG.Governance.ReleaseRules.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseRules.Tests.Support

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II). Reflection lives ONLY in
// these tests, never in the library.

// Touch a member to force the library assembly to load, then locate it by name among the loaded assemblies.
let private releaseRulesAsm =
    (VersionBump: ReleaseRuleKind) |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.ReleaseRules"
        | None -> false)

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.ReleaseRules.surface.txt")

/// Render the assembly's public surface to canonical, sorted text. Any change to the public surface
/// changes this text and trips the baseline assertion.
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
        [ test "ReleaseRules public surface equals the committed baseline" {
              let actual = renderSurface releaseRulesAsm

              // Bless path: BLESS_SURFACE=1 (re)writes the baseline intentionally.
              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "the per-rule classifier, EnforcementInput builder, and partition helper never leak" {
              let surfaceText = (renderSurface releaseRulesAsm).ToLowerInvariant()

              for hidden in [ "classify"; "enforcementinput"; "partition"; "toinput"; "reasonfor" ] do
                  Expect.isFalse (surfaceText.Contains hidden) (sprintf "%s must be hidden (absent from Release.fsi)" hidden)
          }

          test "the surface exposes no sensing/process/document/IO member (FR-007/FR-008)" {
              let surfaceText = (renderSurface releaseRulesAsm).ToLowerInvariant()

              for forbidden in [ "writeall"; "filestream"; "httpclient"; "process"; "json"; "release.json" ] do
                  Expect.isFalse (surfaceText.Contains forbidden) (sprintf "no %s member (pure decision only)" forbidden)
          }

          test "ReleaseRules references only Config + Enforcement + Ship + transitive deps + BCL + FSharp.Core" {
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "FS.GG.Governance.Config"
                  || name = "FS.GG.Governance.Enforcement"
                  || name = "FS.GG.Governance.Ship"
                  // Ship pulls Route/Gates/Findings transitively.
                  || name = "FS.GG.Governance.Route"
                  || name = "FS.GG.Governance.Gates"
                  || name = "FS.GG.Governance.Findings"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."

              let offending =
                  releaseRulesAsm.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty
                  offending
                  (sprintf
                      "ReleaseRules must depend on Config/Enforcement/Ship(+transitive)/BCL/FSharp.Core only; found: %A"
                      offending)

              // Specifically: a release rule is NOT an F018 gate / F017 finding, but those flow transitively
              // through Ship; what must NEVER appear are the kernel/host/CLI/json/snapshot edges.
              let forbidden =
                  releaseRulesAsm.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (fun n ->
                      n = "FS.GG.Governance.Kernel"
                      || n = "FS.GG.Governance.Host"
                      || n = "FS.GG.Governance.Snapshot"
                      || n = "FS.GG.Governance.RouteJson"
                      || n = "FS.GG.Governance.GatesJson"
                      || n = "FS.GG.Governance.Cli"
                      || n.StartsWith "FS.GG.Governance.Adapters")

              Expect.isEmpty
                  forbidden
                  (sprintf "ReleaseRules must not reference kernel/host/adapters/snapshot/json/CLI edges; found: %A" forbidden)
          } ]
