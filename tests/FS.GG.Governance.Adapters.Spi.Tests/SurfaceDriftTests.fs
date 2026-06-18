module FS.GG.Governance.Adapters.Spi.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.Kernel
open FS.GG.Governance.Adapters.Spi

// Reflective API surface-drift + dependency-hygiene checks for the SPI (FR-016, V72,
// SC-008, Principle II). Reflection lives ONLY in these tests, never in the SPI.

let private spi = typeof<Composed<int, int>>.Assembly
let private kernel = typeof<FactId>.Assembly

let rec private findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root (FS.GG.Governance.sln) not found"
    | d ->
        let here ext = File.Exists(Path.Combine(d.FullName, "FS.GG.Governance." + ext))
        if here "sln" || here "slnx" then d.FullName else findRepoRoot d.Parent

let private repoRoot = findRepoRoot (DirectoryInfo(AppContext.BaseDirectory))

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.Adapters.Spi.surface.txt")

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
        [ test "V72 Spi public surface equals the committed baseline" {
              let actual = renderSurface spi

              // Bless path: BLESS_SURFACE=1 (re)writes the baseline intentionally.
              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "Spi public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "V72 Spi references only the BCL + FSharp.Core + the kernel" {
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "FS.GG.Governance.Kernel"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."

              let offending =
                  spi.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty offending (sprintf "Spi must depend on BCL/FSharp.Core/Kernel only; found: %A" offending)
          }

          test "V72 the kernel does NOT reference the Spi (dependency direction adapters -> kernel)" {
              let spiName = spi.GetName().Name |> Option.ofObj |> Option.defaultValue ""

              let kernelRefsSpi =
                  kernel.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.exists (fun n -> n = spiName)

              Expect.isFalse kernelRefsSpi "the kernel must not reference the Spi — adapters depend on the kernel, never the reverse"
          } ]
