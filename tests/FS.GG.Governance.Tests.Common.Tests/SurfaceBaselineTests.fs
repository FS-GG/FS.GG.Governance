module FS.GG.Governance.Tests.Common.Tests.SurfaceBaselineTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.Tests.Common

// Reflective API surface-drift + the FR-008 scope-guard for the 074 shared test-support library
// (Principle II). Reflection lives ONLY in these tests, never in the library. The baseline is blessed via
// BLESS_SURFACE=1 dotnet test, matching the sibling leaf-test convention (e.g. JsonText.Tests).

let private repoRoot = RepositoryHelpers.repoRoot

// Touch a public member to force the library assembly to load, then locate it by name.
let private testsCommonAsm =
    RepositoryHelpers.repoRoot |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.Tests.Common"
        | None -> false)

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.Tests.Common.surface.txt")

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
        [ test "Tests.Common public surface equals the committed baseline" {
              let actual = renderSurface testsCommonAsm

              // Bless path: BLESS_SURFACE=1 (re)writes the baseline intentionally.
              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

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
