module FS.GG.Governance.CodeChecks.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.CodeChecks.Model
open System
open System.IO
open System.Reflection

let library = typeof<FindingId>.Assembly

let rec repoRoot (dir: DirectoryInfo) =
    if File.Exists(Path.Combine(dir.FullName, "FS.GG.Governance.sln")) then dir.FullName
    else
        match Option.ofObj dir.Parent with
        | None -> failwith "repository root not found"
        | Some parent -> repoRoot parent

let renderSurface (assembly: Assembly) =
    let flags = BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.Static ||| BindingFlags.DeclaredOnly
    assembly.GetExportedTypes()
    |> Array.sortBy _.FullName
    |> Array.map (fun t ->
        let members = t.GetMembers(flags) |> Array.map (fun m -> sprintf "  [%A] %s" m.MemberType (m.ToString())) |> Array.sort
        String.concat "\n" (Array.append [| sprintf "TYPE %s" t.FullName |] members))
    |> String.concat "\n"

let surfaceTest = test "CodeChecks public surface equals committed baseline" {
    let path = Path.Combine(repoRoot (DirectoryInfo Environment.CurrentDirectory), "surface", "FS.GG.Governance.CodeChecks.surface.txt")
    let actual = renderSurface library
    if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then File.WriteAllText(path, actual + "\n")
    Expect.equal (actual.TrimEnd()) (File.ReadAllText(path).Replace("\r\n", "\n").TrimEnd()) "public surface drifted"
}

let dependencyTest = test "CodeChecks references only FCS and framework assemblies" {
    let forbidden =
        library.GetReferencedAssemblies()
        |> Array.choose (fun assemblyName -> Option.ofObj assemblyName.Name)
        |> Array.filter (fun name ->
            not (name = "FSharp.Core" || name = "FSharp.Compiler.Service" || name = "System.Private.CoreLib" || name = "netstandard" || name.StartsWith("System", StringComparison.Ordinal)))
    Expect.isEmpty forbidden (sprintf "%A" forbidden)
}

[<Tests>]
let tests =
    testList "SurfaceDrift" [
        surfaceTest
        dependencyTest
    ]
