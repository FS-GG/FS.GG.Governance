module FS.GG.Governance.AdvisoryPromotion.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.AdvisoryPromotion

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, plan D1/D3, SC-007), now via
// the shared SurfaceDrift helper (101/M-CI-3). Reflection lives in the helper and here, never in the library.

let private advisoryPromotionAsm = SurfaceDrift.assemblyNamed "FS.GG.Governance.AdvisoryPromotion"

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "AdvisoryPromotion" "FS.GG.Governance.AdvisoryPromotion" advisoryPromotionAsm

          test "the public surface is exactly the two modules (Model + AdvisoryPromotion), nothing else" {
              let typeNames =
                  advisoryPromotionAsm.GetExportedTypes() |> Array.choose (fun t -> Option.ofObj t.FullName)

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.AdvisoryPromotion.ModelModule"))
                  "Model module is public"
              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.AdvisoryPromotion.AdvisoryPromotionModule"))
                  "AdvisoryPromotion operations module is public"
              Expect.isFalse
                  (typeNames
                   |> Array.exists (fun n ->
                       let l = n.ToLowerInvariant()
                       l.Contains "helper" || l.Contains "internal"))
                  "no helper/internal module leaks into the public surface"
          }

          SurfaceDrift.referencesOnly
              "AdvisoryPromotion"
              (fun n ->
                  n = "FS.GG.Governance.EvidenceReuse"
                  || n = "FS.GG.Governance.FreshnessKey"
                  || n = "FS.GG.Governance.Config")
              advisoryPromotionAsm ]
