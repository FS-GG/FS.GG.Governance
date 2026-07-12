module FS.GG.Governance.ProjectSensing.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.Cli // the moved Project module keeps the FS.GG.Governance.Cli namespace
open FS.GG.Governance.Tests.Common

// 100 (M-ARCH-2): re-homed from Cli.Tests. Reflective API surface-drift + dependency-hygiene (Principle II)
// for the FS.GG.Governance.ProjectSensing library, which now owns the F12 `Project` composition root and its
// coproduct types (Domain / ProjectFact / … / ProjectEvidenceReport), still in the FS.GG.Governance.Cli
// namespace. This library sits BELOW the command executables and must not reference any of them.

let private repoRoot = FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot

let private projectSensing = SurfaceDrift.assemblyNamed "FS.GG.Governance.ProjectSensing"

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.ProjectSensing.surface.txt")

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
        [ test "ProjectSensing public surface equals the committed baseline" {
              let actual = renderSurface projectSensing

              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "the Project composition root is public" {
              let typeNames = projectSensing.GetExportedTypes() |> Array.choose (fun t -> Option.ofObj t.FullName)

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.Cli.ProjectModule"))
                  "Project module is public"

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n = "FS.GG.Governance.Cli.ProjectFact"))
                  "ProjectFact coproduct is public"
          }

          test "ProjectSensing sits below the command executables (references no exe)" {
              let forbidden =
                  projectSensing.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (fun n ->
                      n = "FS.GG.Governance.Cli"
                      || n = "FS.GG.Governance.EvidenceCommand"
                      || n = "FS.GG.Governance.RouteCommand"
                      || n = "FS.GG.Governance.RoutePipeline")

              Expect.isEmpty
                  forbidden
                  (sprintf "ProjectSensing must not reference a command executable or the route pipeline; found: %A" forbidden)
          } ]
