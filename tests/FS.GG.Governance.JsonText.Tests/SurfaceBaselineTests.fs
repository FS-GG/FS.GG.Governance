module FS.GG.Governance.JsonText.Tests.SurfaceBaselineTests

open System
open System.IO
open System.Reflection
open System.Text.Json
open Expecto
open FS.GG.Governance.JsonText

// Reflective API surface-drift + dependency/scope-hygiene checks for the 073 JsonText leaf
// (Principle II). Reflection lives ONLY in these tests, never in the library. The baseline is blessed
// via BLESS_SURFACE=1 dotnet test, matching the sibling leaf-test convention.

let rec private findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root (FS.GG.Governance.sln) not found"
    | d ->
        if File.Exists(Path.Combine(d.FullName, "FS.GG.Governance.sln")) then d.FullName
        else findRepoRoot d.Parent

let private repoRoot = findRepoRoot (DirectoryInfo(AppContext.BaseDirectory))

// Touch the public member to force the library assembly to load, then locate it by name.
let private jsonTextAsm =
    JsonText.writeToString (fun (w: Utf8JsonWriter) -> w.WriteNullValue()) |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.JsonText"
        | None -> false)

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.JsonText.surface.txt")

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
        [ test "JsonText public surface equals the committed baseline" {
              let actual = renderSurface jsonTextAsm

              // Bless path: BLESS_SURFACE=1 (re)writes the baseline intentionally.
              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "JsonText references only FSharp.Core + BCL (scope guard — no cycle, no kernel/host)" {
              // The leaf has NO governance ProjectReference: it cannot introduce a cycle and any
              // projection may reference it WITHOUT pulling in the kernel/host capability the pure
              // projections deliberately exclude. Serialization is the shared-framework System.Text.Json.
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."

              let offending =
                  jsonTextAsm.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty
                  offending
                  (sprintf "JsonText must depend on FSharp.Core/BCL only; found: %A" offending)

              // Specifically: NOT any FS.GG.Governance assembly — the leaf is dependency-free upstream.
              let forbidden =
                  jsonTextAsm.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (fun n -> n.StartsWith "FS.GG.Governance")

              Expect.isEmpty
                  forbidden
                  (sprintf "JsonText must not reference any governance project; found: %A" forbidden)
          } ]
