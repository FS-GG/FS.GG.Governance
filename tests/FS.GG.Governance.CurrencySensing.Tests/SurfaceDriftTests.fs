module FS.GG.Governance.CurrencySensing.Tests.SurfaceDriftTests

// The CurrencySensing core's public surface-drift baseline (Constitution Principle II). BLESS_SURFACE=1 regenerates.

open System
open System.IO
open System.Reflection
open Expecto

module CS = FS.GG.Governance.CurrencySensing.CurrencySensing

let private repoRoot =
    let rec find (dir: string) =
        if File.Exists(Path.Combine(dir, "FS.GG.Governance.sln")) then
            dir
        else
            match Directory.GetParent dir with
            | null -> dir
            | p -> find p.FullName

    find (Directory.GetCurrentDirectory())

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
        [ test "CurrencySensing public surface equals the committed baseline" {
              let name = "FS.GG.Governance.CurrencySensing"
              let baselinePath = Path.Combine(repoRoot, "surface", sprintf "%s.surface.txt" name)
              // Touch the module so the assembly is loaded, then resolve it by name (the module is a static
              // class with no exported type to `typeof`, so we load by assembly name).
              CS.parseManifest [] |> ignore
              let actual = renderSurface (Assembly.Load name)

              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath
              Expect.equal (normalize actual) (normalize baseline) (sprintf "%s surface drifted — BLESS_SURFACE=1 to regenerate" name)
          } ]
