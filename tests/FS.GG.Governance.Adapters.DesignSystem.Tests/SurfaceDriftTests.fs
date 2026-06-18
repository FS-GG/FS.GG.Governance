module FS.GG.Governance.Adapters.DesignSystem.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.Kernel
open FS.GG.Governance.Adapters.Spi
open FS.GG.Governance.Adapters.DesignSystem

// Reflective API surface-drift + dependency-hygiene checks for the DesignSystem adapter
// (FR-016/FR-017, SC-008, Principle II), plus the no-rendering-vocabulary-leak inspection of
// the kernel/SPI baselines (FR-011, SC-003). Reflection lives ONLY in these tests, never in
// the adapter. The two curated .fsi are the sole visibility declaration.

let private designSystem = typeof<DesignArtifactRef>.Assembly
let private spi = typeof<Composed<int, int>>.Assembly
let private kernel = typeof<FactId>.Assembly
let private specKit = typeof<FS.GG.Governance.Adapters.DesignSystem.Tests.ProjectFact.SpecKitFact>.Assembly

let rec private findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root (FS.GG.Governance.sln) not found"
    | d ->
        let here ext = File.Exists(Path.Combine(d.FullName, "FS.GG.Governance." + ext))
        if here "sln" || here "slnx" then d.FullName else findRepoRoot d.Parent

let private repoRoot = findRepoRoot (DirectoryInfo(AppContext.BaseDirectory))

let private surfacePath (name: string) =
    Path.Combine(repoRoot, "surface", name)

let private baselinePath =
    surfacePath "FS.GG.Governance.Adapters.DesignSystem.surface.txt"

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
        [ test "V8 DesignSystem public surface equals the committed baseline" {
              let actual = renderSurface designSystem

              // Bless path: BLESS_SURFACE=1 (re)writes the baseline intentionally.
              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "DesignSystem public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "V8 DesignSystem references only the BCL + FSharp.Core + the Spi + the kernel — NOT the F10 SpecKit adapter" {
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "FS.GG.Governance.Adapters.Spi"
                  || name = "FS.GG.Governance.Kernel"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."

              let referenced =
                  designSystem.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)

              let offending = referenced |> Array.filter (allowed >> not)

              Expect.isEmpty
                  offending
                  (sprintf "DesignSystem must depend on BCL/FSharp.Core/Spi/Kernel only; found: %A" offending)

              // The keystone of the adoption bar: the shipped adapter NEVER references F10.
              Expect.isFalse
                  (referenced |> Array.contains "FS.GG.Governance.Adapters.SpecKit")
                  "the shipped design-system adapter must NOT reference the F10 Spec Kit adapter (siblings, not dependants)"
          }

          test "V8 the dependency direction is adapter -> Spi -> kernel — nothing upstream references DesignSystem" {
              let designSystemName =
                  designSystem.GetName().Name |> Option.ofObj |> Option.defaultValue ""

              let references (asm: Assembly) =
                  asm.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.exists (fun n -> n = designSystemName)

              Expect.isFalse (references kernel) "the kernel must not reference DesignSystem"
              Expect.isFalse (references spi) "the Spi must not reference DesignSystem"
              Expect.isFalse (references specKit) "the F10 SpecKit adapter must not reference DesignSystem (no adapter -> adapter edge)"
          }

          test "V3 no rendering/token/colour/layout vocabulary leaks into the kernel or SPI surfaces (FR-011, N1)" {
              let banned =
                  [ "Token"; "Colour"; "Color"; "Contrast"; "Layout"; "Spacing"; "Motion"; "Elevation"
                    "Rendered"; "PagePattern"; "InteractionState"; "DesignArtifactRef"; "DesignSystem" ]

              for file in [ "FS.GG.Governance.Kernel.surface.txt"; "FS.GG.Governance.Adapters.Spi.surface.txt" ] do
                  let text = (File.ReadAllText(surfacePath file)).ToLowerInvariant()

                  for word in banned do
                      Expect.isFalse
                          (text.Contains(word.ToLowerInvariant()))
                          (sprintf "the generic %s surface must carry no design vocabulary — found '%s'" file word)
          } ]
