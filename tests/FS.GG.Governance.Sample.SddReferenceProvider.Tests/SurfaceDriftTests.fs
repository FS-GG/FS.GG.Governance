module FS.GG.Governance.Sample.SddReferenceProvider.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.Sample.SddReferenceProvider
open FS.GG.Governance.Sample.SddReferenceProvider.Tests.Support

// Reflective surface-drift guard (Principle II). Reflection lives ONLY in this test. It checks the
// sample provider's OWN additive baseline (blessed with BLESS_SURFACE=1), AND asserts the two generic-
// CORE baselines are byte-identical to their committed form — the SC-006 no-delta guard proving the
// generic seam gained no provider knowledge (contract R6, quickstart Scenario 5).

/// Locate a loaded assembly by simple name. Touching `SddReferenceProvider.providerId` (and the worked
/// example's use of `ScaffoldManifestJson.ofManifest`) guarantees all three are loaded.
let private assemblyByName (name: string) : Assembly =
    SddReferenceProvider.providerId |> ignore

    AppDomain.CurrentDomain.GetAssemblies()
    |> Array.tryFind (fun a -> a.GetName().Name = name)
    |> function
        | Some a -> a
        | None -> failwithf "assembly not loaded: %s" name

let private renderSurface (asm: Assembly) =
    let memberFlags =
        BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.Static ||| BindingFlags.DeclaredOnly

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

let private baseline (name: string) =
    Path.Combine(repoRoot, "surface", name + ".surface.txt")

/// Assert a committed CORE baseline equals the live assembly surface WITHOUT ever blessing it (the core
/// baselines are 071-owned — this feature must not touch them).
let private assertCoreUnchanged (name: string) =
    let actual = renderSurface (assemblyByName name)
    let committed = File.ReadAllText(baseline name)
    Expect.equal
        (normalize actual)
        (normalize committed)
        (sprintf "CORE surface %s drifted — SC-006 forbids changing the generic core's surface" name)

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ test "sample provider public surface equals its own committed baseline" {
              let actual = renderSurface (assemblyByName "FS.GG.Governance.Sample.SddReferenceProvider")
              let path = baseline "FS.GG.Governance.Sample.SddReferenceProvider"

              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(path, actual + "\n")

              let committed = File.ReadAllText path

              Expect.equal
                  (normalize actual)
                  (normalize committed)
                  "sample surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "generic-core baselines are byte-identical (SC-006 no-delta guard)" {
              assertCoreUnchanged "FS.GG.Governance.Scaffold"
              assertCoreUnchanged "FS.GG.Governance.ScaffoldManifestJson"
          } ]
