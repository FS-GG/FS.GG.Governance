module FS.GG.Governance.Adapters.DesignSystem.Tests.SurfaceDriftTests

open System.IO
open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.Kernel
open FS.GG.Governance.Adapters.Spi
open FS.GG.Governance.Adapters.DesignSystem

// Reflective API surface-drift + dependency-hygiene checks for the DesignSystem adapter (Principle II),
// now via the shared SurfaceDrift helper (101/M-CI-3), plus the no-rendering-vocabulary-leak inspection
// of the kernel/SPI baselines (FR-011, SC-003), which stays local. Reflection lives in the helper.

let private designSystem = typeof<DesignArtifactRef>.Assembly
let private spi = typeof<Composed<int, int>>.Assembly
let private kernel = typeof<FactId>.Assembly
let private specKit = typeof<FS.GG.Governance.Adapters.DesignSystem.Tests.ProjectFact.SpecKitFact>.Assembly

let private surfacePath (name: string) =
    Path.Combine(RepositoryHelpers.repoRoot, "surface", name)

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "V8 DesignSystem" "FS.GG.Governance.Adapters.DesignSystem" designSystem

          // Scope guard: the shipped adapter references only the Spi + kernel — SpecKit is deliberately
          // absent from the allowed set, so an F10 edge from the shipped adapter is reported as offending.
          SurfaceDrift.referencesOnly
              "V8 DesignSystem"
              (fun n -> n = "FS.GG.Governance.Adapters.Spi" || n = "FS.GG.Governance.Kernel")
              designSystem

          // Direction guard: nothing upstream (kernel, Spi, or the F10 SpecKit adapter) references DesignSystem.
          SurfaceDrift.noInboundReferences "V8 DesignSystem" [ kernel; spi; specKit ] designSystem

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
