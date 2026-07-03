module FS.GG.Governance.Tests.Common.Tests.SurfaceBaselineTests

open System.IO
open Expecto
open FS.GG.Governance.Tests.Common

// Reflective API surface-drift + the FR-008 scope-guard for the 074/101 shared test-support library
// (Principle II). The surface check now runs through the shared `SurfaceDrift` helper this library
// exposes (dogfooding — 101/M-CI-3); the FR-008 scope guard is bespoke and stays local.

let private repoRoot = RepositoryHelpers.repoRoot

// Touch a public member to force the library assembly to load, then locate it by name.
let private testsCommonAsm =
    RepositoryHelpers.repoRoot |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.Tests.Common"
        | None -> false)

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "Tests.Common" "FS.GG.Governance.Tests.Common" testsCommonAsm

          test "no src/*.fsproj references FS.GG.Governance.Tests.Common (FR-008 scope guard)" {
              // The library is TEST-ONLY: it lives under tests/, is IsPackable=false, and MUST NOT enter the
              // production dependency graph. This guard makes FR-008 a tested invariant, not a convention.
              let srcDir = Path.Combine(repoRoot, "src")

              let offenders =
                  Directory.GetFiles(srcDir, "*.fsproj", SearchOption.AllDirectories)
                  |> Array.filter (fun f -> File.ReadAllText(f).Contains "FS.GG.Governance.Tests.Common")
                  |> Array.map Path.GetFileName

              Expect.isEmpty
                  offenders
                  (sprintf "no src project may reference the test-only library; found: %A" offenders)
          } ]
