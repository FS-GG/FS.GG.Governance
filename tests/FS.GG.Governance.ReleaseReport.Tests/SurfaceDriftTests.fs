module FS.GG.Governance.ReleaseReport.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.ReleaseReport
open FS.GG.Governance.ReleaseReport.Tests.Support

let private asm =
    let sensed = sensedFrom allMet []
    Report.assemble (decisionFor sensed) sensed packEvidence attestation |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.ReleaseReport"
        | None -> false)

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.ReleaseReport.surface.txt")

let private renderSurface (a: Assembly) =
    let memberFlags =
        BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.Static ||| BindingFlags.DeclaredOnly

    a.GetExportedTypes()
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
        [ test "ReleaseReport public surface equals the committed baseline" {
              let actual = renderSurface asm

              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath
              Expect.equal (normalize actual) (normalize baseline) "public surface drifted — bless with BLESS_SURFACE=1"
          } ]
