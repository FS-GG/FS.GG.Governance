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

/// Locate an assembly by simple name. Touching `SddReferenceProvider.providerId` forces the sample
/// provider load; the two generic-core assemblies are ProjectReferences (their `.dll` sits beside this
/// test in the output dir), so when nothing has JIT-touched them yet we force-load by display name from
/// the app base rather than depend on test ordering. (Before this the guard assumed a prior test had
/// touched `ScaffoldManifestJson.ofManifest` — true locally, but on CI this ran first and threw
/// "assembly not loaded"; H1 exposed it once the suite finally ran in CI.)
let private assemblyByName (name: string) : Assembly =
    SddReferenceProvider.providerId |> ignore

    AppDomain.CurrentDomain.GetAssemblies()
    |> Array.tryFind (fun a -> a.GetName().Name = name)
    |> function
        | Some a -> a
        | None ->
            try
                Assembly.Load(AssemblyName name)
            with ex ->
                failwithf "assembly not loaded and could not be force-loaded: %s (%s)" name ex.Message

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
