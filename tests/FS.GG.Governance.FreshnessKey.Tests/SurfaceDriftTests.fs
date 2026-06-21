module FS.GG.Governance.FreshnessKey.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.FreshnessKey
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.FreshnessKey.Tests.Support

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, plan D1). Reflection lives
// ONLY in these tests, never in the library.

// Touch a member of each public module to force the library assembly to load, then locate it by name.
let private freshnessKeyAsm =
    Model.categoryToken CheckIdentity |> ignore
    FreshnessKey.value (Key "load") |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.FreshnessKey"
        | None -> false)

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.FreshnessKey.surface.txt")

/// Render the assembly's public surface to canonical, sorted text. Any change to the public surface
/// changes this text and trips the baseline assertion.
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
        [ test "FreshnessKey public surface equals the committed baseline" {
              let actual = renderSurface freshnessKeyAsm

              // Bless path: BLESS_SURFACE=1 (re)writes the baseline intentionally.
              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "the public surface is exactly the two modules (Model + FreshnessKey), nothing else" {
              let typeNames =
                  freshnessKeyAsm.GetExportedTypes()
                  |> Array.choose (fun t -> Option.ofObj t.FullName)

              // The two operation/type modules plus the DU/record/newtype types they declare are exported,
              // but no token/encoder/buffer HELPER module leaks (those are hidden by the .fsi files).
              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.FreshnessKey.ModelModule"))
                  "Model module is public"
              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.FreshnessKey.FreshnessKeyModule"))
                  "FreshnessKey operations module is public"
              Expect.isFalse
                  (typeNames |> Array.exists (fun n -> n.ToLowerInvariant().Contains "encode" || n.ToLowerInvariant().Contains "segment"))
                  "no encoder/segment helper module leaks into the public surface"
          }

          test "FreshnessKey references only Config + BCL + FSharp.Core (plan D1 scope guard)" {
              // One-way dependency: FreshnessKey -> Config (for the F014 newtypes). No git-sensing
              // Snapshot, no Gates record wrapper, no host/adapter/CLI edge, and no new third-party package.
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "FS.GG.Governance.Config"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."

              let offending =
                  freshnessKeyAsm.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty
                  offending
                  (sprintf "FreshnessKey must depend on Config/BCL/FSharp.Core only; found: %A" offending)

              // Specifically: NOT Gates/Snapshot/Route/Adapters/Host/CLI or any other edge assembly.
              let forbidden =
                  freshnessKeyAsm.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (fun n ->
                      n = "FS.GG.Governance.Gates"
                      || n = "FS.GG.Governance.Snapshot"
                      || n = "FS.GG.Governance.Route"
                      || n = "FS.GG.Governance.Routing"
                      || n = "FS.GG.Governance.Findings"
                      || n = "FS.GG.Governance.Host"
                      || n = "FS.GG.Governance.Cli"
                      || n.StartsWith "FS.GG.Governance.Adapters")

              Expect.isEmpty
                  forbidden
                  (sprintf "FreshnessKey must not reference Gates/Snapshot/Route/Routing/Findings/host/adapters/CLI; found: %A" forbidden)
          } ]
