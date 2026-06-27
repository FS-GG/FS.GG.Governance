module FS.GG.Governance.VerifyJson.Tests.SeamModuleScopeGuardTests

open System
open Expecto
open FS.GG.Governance.VerifyJson

// 076 Phase C (T022): structural per-module guard over the four additive PROJECTION seam modules
// (Core / SurfaceChecks / ReleaseReadiness / GeneratedViews). Asserts the additive module set is PRESENT and
// PUBLIC, and that the projection assembly keeps NO edge to a command HOST — the seams stay a pure leaf below
// every host (no host-`Model` edge into a projection seam). The seams' BEHAVIOR is exercised transitively by
// the existing golden/determinism suites (Principle I, Notes D1); this is the direct per-module assertion of
// the additive surface (vs only the aggregate drift baseline). Reflection lives ONLY in tests.

let private verifyJson =
    VerifyJson.schemaVersion |> ignore

    AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.VerifyJson"
        | None -> false)

let private exportedTypeNames =
    verifyJson.GetExportedTypes() |> Array.choose (fun t -> Option.ofObj t.FullName)

let private seamModules =
    [ "FS.GG.Governance.VerifyJson.CoreModule"
      "FS.GG.Governance.VerifyJson.SurfaceChecksModule"
      "FS.GG.Governance.VerifyJson.ReleaseReadinessModule"
      "FS.GG.Governance.VerifyJson.GeneratedViewsModule" ]

[<Tests>]
let tests =
    testList
        "SeamModuleScopeGuard (076 projection seams)"
        [ test "the four additive projection seam modules are present and public" {
              for m in seamModules do
                  Expect.isTrue
                      (exportedTypeNames |> Array.exists (fun n -> n = m))
                      (sprintf "expected additive seam module %s to be public" m)
          }

          test "the projection references no command host (no host-Model edge into a projection seam)" {
              let referenced =
                  verifyJson.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)

              let hostEdges = referenced |> Array.filter (fun n -> n.EndsWith "Command")

              Expect.isEmpty hostEdges (sprintf "the projection must not reference a command host; found: %A" hostEdges)
          } ]
