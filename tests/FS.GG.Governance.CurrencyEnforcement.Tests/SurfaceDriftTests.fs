module FS.GG.Governance.CurrencyEnforcement.Tests.SurfaceDriftTests

// The leaf's public surface-drift baseline (Constitution Principle II). BLESS_SURFACE=1 regenerates it.

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.CurrencyEnforcement.Tests.Support

module CE = FS.GG.Governance.CurrencyEnforcement.CurrencyEnforcement

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

let private check (asm: Assembly) (name: string) =
    let baselinePath = Path.Combine(repoRoot, "surface", sprintf "%s.surface.txt" name)
    let actual = renderSurface asm

    if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
        File.WriteAllText(baselinePath, actual + "\n")

    let baseline = File.ReadAllText baselinePath
    Expect.equal (normalize actual) (normalize baseline) (sprintf "%s surface drifted — BLESS_SURFACE=1 to regenerate" name)

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ test "CurrencyEnforcement public surface equals the committed baseline" {
              check typeof<CE.CurrencyFinding>.Assembly "FS.GG.Governance.CurrencyEnforcement"
          } ]
