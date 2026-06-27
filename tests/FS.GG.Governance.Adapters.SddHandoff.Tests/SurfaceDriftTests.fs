module FS.GG.Governance.Adapters.SddHandoff.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.Adapters.SddHandoff

// Reflective API surface-drift + dependency-hygiene checks for the SDD-handoff consumer
// (Principle II, SC-006). Reflection lives ONLY in these tests, never in the adapter. The five
// curated .fsi are the sole visibility declaration.

let private sddHandoff = typeof<Model.Diagnostic>.Assembly

let rec private findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root (FS.GG.Governance.sln) not found"
    | d ->
        let here ext = File.Exists(Path.Combine(d.FullName, "FS.GG.Governance." + ext))
        if here "sln" || here "slnx" then d.FullName else findRepoRoot d.Parent

let private repoRoot = findRepoRoot (DirectoryInfo(AppContext.BaseDirectory))

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.Adapters.SddHandoff.surface.txt")

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
        [ test "SddHandoff public surface equals the committed baseline" {
              let actual = renderSurface sddHandoff

              // Bless path: BLESS_SURFACE=1 (re)writes the baseline intentionally.
              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "SddHandoff public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "SddHandoff references no SDD assembly (SC-006 — consumer imports no SDD source)" {
              let offending =
                  sddHandoff.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (fun n -> n.Contains "FS.GG.SDD" || n.Contains "Sdd" && n.StartsWith "FS.GG.SDD")

              Expect.isEmpty
                  offending
                  (sprintf "the consumer must reference no SDD assembly; found: %A" offending)
          }

          test "SddHandoff references only BCL/FSharp.Core + the Governance cores it maps onto" {
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "FS.GG.Governance.Kernel"
                  || name = "FS.GG.Governance.Config"
                  || name = "FS.GG.Governance.Gates"
                  || name = "FS.GG.Governance.Route"
                  || name = "FS.GG.Governance.Routing"
                  || name = "FS.GG.Governance.Findings"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."

              let offending =
                  sddHandoff.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty
                  offending
                  (sprintf "the consumer must depend only on BCL/FSharp.Core + Kernel/Config/Gates/Route(+transitive Routing/Findings); found: %A" offending)
          } ]
