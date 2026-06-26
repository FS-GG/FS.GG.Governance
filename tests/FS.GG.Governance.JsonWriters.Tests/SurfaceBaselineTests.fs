module FS.GG.Governance.JsonWriters.Tests.SurfaceBaselineTests

open System
open System.IO
open System.Reflection
open System.Text.Json
open Expecto
open FS.GG.Governance.JsonWriters
open FS.GG.Governance.EvidenceReuse.Model

// Reflective API surface-drift + dependency/scope-hygiene checks for the 073 JsonWriters leaf
// (Principle II). Reflection lives ONLY in these tests. Blessed via BLESS_SURFACE=1 dotnet test.

let rec private findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root (FS.GG.Governance.sln) not found"
    | d ->
        if File.Exists(Path.Combine(d.FullName, "FS.GG.Governance.sln")) then d.FullName
        else findRepoRoot d.Parent

let private repoRoot = findRepoRoot (DirectoryInfo(AppContext.BaseDirectory))

// Touch a public member to force the library assembly to load, then locate it by name.
let private jsonWritersAsm =
    use stream = new MemoryStream()
    use w = new Utf8JsonWriter(stream)
    JsonWriters.writeCause w NoPriorEvidence

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.JsonWriters"
        | None -> false)

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.JsonWriters.surface.txt")

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
        [ test "JsonWriters public surface equals the committed baseline" {
              let actual = renderSurface jsonWritersAsm

              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "JsonWriters takes no kernel/host/projection edge (scope guard — pure writer leaf)" {
              // The leaf references the JsonTokens leaf + the domain owners of the values it walks. It must
              // NOT reach the kernel/host capability the pure projections exclude, NOT any *Json projection,
              // and NOT the sibling JsonText leaf — it sits ABOVE the domain owners and BELOW the projections.
              let forbidden (n: string) =
                  n = "FS.GG.Governance.Kernel"
                  || n = "FS.GG.Governance.Host"
                  || n = "FS.GG.Governance.Cli"
                  || n = "FS.GG.Governance.Snapshot"
                  || n = "FS.GG.Governance.JsonText"
                  || n.StartsWith "FS.GG.Governance.Adapters"
                  || (n.StartsWith "FS.GG.Governance" && n.EndsWith "Json")

              let offending =
                  jsonWritersAsm.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter forbidden

              Expect.isEmpty
                  offending
                  (sprintf "JsonWriters must not reference kernel/host/projection/JsonText; found: %A" offending)
          } ]
