module FS.GG.Governance.Provenance.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.Provenance
open FS.GG.Governance.Provenance.Model

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, plan D1), now via the shared
// SurfaceDrift helper (101/M-CI-3).

// Touch a member of each public module to force the library assembly to load, then locate it by name.
let private provenanceAsm =
    Provenance.identityValue (ProvenanceIdentity "load") |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.Provenance"
        | None -> false)

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "Provenance" "FS.GG.Governance.Provenance" provenanceAsm

          test "the public surface is exactly the two modules (Model + Provenance), nothing else" {
              let typeNames =
                  provenanceAsm.GetExportedTypes()
                  |> Array.choose (fun t -> Option.ofObj t.FullName)

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.Provenance.ModelModule"))
                  "Model module is public"
              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.Provenance.ProvenanceModule"))
                  "Provenance operations module is public"
              Expect.isFalse
                  (typeNames |> Array.exists (fun n -> n.ToLowerInvariant().Contains "encode" || n.ToLowerInvariant().Contains "segment"))
                  "no encoder/segment helper module leaks into the public surface"
          }

          SurfaceDrift.referencesOnly
              "Provenance"
              (fun n ->
                  n = "FS.GG.Governance.FreshnessKey"
                  || n = "FS.GG.Governance.CommandRecord"
                  || n = "FS.GG.Governance.Config")
              provenanceAsm ]
