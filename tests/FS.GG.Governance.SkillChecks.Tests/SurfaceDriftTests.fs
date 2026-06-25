module FS.GG.Governance.SkillChecks.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.SkillChecks.Model
open FS.GG.Governance.SkillChecks.Tests.Support

let private library = typeof<SkillFacts>.Assembly

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.SkillChecks.surface.txt")

let private renderSurface (asm: Assembly) =
    let memberFlags =
        BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.Static ||| BindingFlags.DeclaredOnly

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
        [ test "SkillChecks public surface equals the committed baseline" {
              let actual = renderSurface library

              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath
              Expect.equal (normalize actual) (normalize baseline) "surface drifted — BLESS_SURFACE=1 to regenerate"
          }

          test "SkillChecks references only FS.GG.Governance.*/BCL/FSharp.Core (no network/SDK)" {
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."
                  || name.StartsWith "FS.GG.Governance."

              let offending =
                  library.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty offending (sprintf "unexpected dependency: %A" offending)
          } ]
