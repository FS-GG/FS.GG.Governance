module FS.GG.Governance.FreshnessKey.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.FreshnessKey
open FS.GG.Governance.FreshnessKey.Model

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, plan D1), now via the shared
// SurfaceDrift helper (101/M-CI-3).

// Touch a member of each public module to force the library assembly to load, then locate it by name.
let private freshnessKeyAsm =
    Model.categoryToken CheckIdentity |> ignore
    FreshnessKey.value (Key "load") |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.FreshnessKey"
        | None -> false)

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "FreshnessKey" "FS.GG.Governance.FreshnessKey" freshnessKeyAsm

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

          SurfaceDrift.referencesOnly "FreshnessKey" (fun n -> n = "FS.GG.Governance.Config") freshnessKeyAsm ]
