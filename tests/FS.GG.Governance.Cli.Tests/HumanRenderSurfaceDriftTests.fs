module FS.GG.Governance.Cli.Tests.HumanRenderSurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.HumanRender

// T045: reflective API surface-drift check for the F27 HumanRender presentation library (Principle
// II). Reflection lives ONLY in this test. The committed baseline is the Tier-1 surface contract for
// the second new library (RichRender.emit + the pure Watch/Tui MVU surface).

// The HumanRender assembly, resolved directly from one of its public types. `typeof<_>.Assembly`
// is the robust idiom (used by the sibling Cli SurfaceDriftTests): it is a real metadata reference
// the compiler cannot elide, so the assembly is always resolvable regardless of test-host load
// order. The prior `AppDomain.GetAssemblies() |> Array.find` relied on a `Watch.debounceWindow |>
// ignore` force-load that gets optimized away, so the assembly was often not yet loaded and the
// `Array.find` threw at module init — aborting the whole Expecto run for this project.
let private humanRender = typeof<Watch.WatchModel>.Assembly

let rec private findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root not found"
    | d ->
        if File.Exists(Path.Combine(d.FullName, "FS.GG.Governance.sln")) then
            d.FullName
        else
            findRepoRoot d.Parent

let private baselinePath =
    Path.Combine(findRepoRoot (DirectoryInfo AppContext.BaseDirectory), "surface", "FS.GG.Governance.HumanRender.surface.txt")

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
        "HumanRenderSurfaceDrift"
        [ test "HumanRender public surface equals the committed baseline" {
              let actual = renderSurface humanRender

              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" || not (File.Exists baselinePath) then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "HumanRender public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          } ]
