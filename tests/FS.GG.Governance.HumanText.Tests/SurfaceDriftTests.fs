module FS.GG.Governance.HumanText.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.HumanText
open FS.GG.Governance.HumanText.Tests.Support

// T012: reflective API surface-drift check (Principle II). Reflection lives ONLY in this test.

let private humanText =
    RenderMode.selectMode true |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.HumanText"
        | None -> false)

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.HumanText.surface.txt")

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
        [ test "HumanText public surface equals the committed baseline" {
              let actual = renderSurface humanText

              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" || not (File.Exists baselinePath) then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          } ]
