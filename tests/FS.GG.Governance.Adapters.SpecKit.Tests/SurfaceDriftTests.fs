module FS.GG.Governance.Adapters.SpecKit.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.Kernel
open FS.GG.Governance.Adapters.Spi
open FS.GG.Governance.Adapters.SpecKit

// Reflective API surface-drift + dependency-hygiene checks for the SpecKit adapter
// (FR-016/FR-017, SC-008, Principle II). Reflection lives ONLY in these tests, never in
// the adapter. The two curated .fsi are the sole visibility declaration.

let private specKit = typeof<ConstitutionDial>.Assembly
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
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.Adapters.SpecKit.surface.txt")

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
        [ test "V8 SpecKit public surface equals the committed baseline" {
              let actual = renderSurface specKit

              // Bless path: BLESS_SURFACE=1 (re)writes the baseline intentionally.
              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "SpecKit public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "V8 SpecKit references only the BCL + FSharp.Core + the Spi + the kernel" {
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "FS.GG.Governance.Adapters.Spi"
                  || name = "FS.GG.Governance.Kernel"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."

              let offending =
                  specKit.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty
                  offending
                  (sprintf "SpecKit must depend on BCL/FSharp.Core/Spi/Kernel only; found: %A" offending)
          }

          test "V8 neither the kernel nor the Spi references SpecKit (dependency direction adapter -> Spi -> kernel)" {
              let specKitName =
                  specKit.GetName().Name |> Option.ofObj |> Option.defaultValue ""

              let references (asm: Assembly) =
                  asm.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.exists (fun n -> n = specKitName)

              Expect.isFalse (references kernel) "the kernel must not reference SpecKit"
              Expect.isFalse (references spi) "the Spi must not reference SpecKit"
          } ]
