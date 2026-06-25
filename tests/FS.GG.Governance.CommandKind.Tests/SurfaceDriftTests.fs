module FS.GG.Governance.CommandKind.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.CommandKind
open FS.GG.Governance.CommandKind.Tests.Support

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, SC-008).

let private commandKindAsm =
    Audit.kindToken FS.GG.Governance.CommandKind.Model.Build |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.CommandKind"
        | None -> false)

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.CommandKind.surface.txt")

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
        [ test "CommandKind public surface equals the committed baseline" {
              let actual = renderSurface commandKindAsm

              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "CommandKind references only CommandRecord/Provenance (+transitive cores)/BCL/FSharp.Core" {
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "FS.GG.Governance.CommandRecord"
                  || name = "FS.GG.Governance.Provenance"
                  || name = "FS.GG.Governance.FreshnessKey"
                  || name = "FS.GG.Governance.Config"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."

              let offending =
                  commandKindAsm.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty offending (sprintf "unexpected references: %A" offending)
          } ]
