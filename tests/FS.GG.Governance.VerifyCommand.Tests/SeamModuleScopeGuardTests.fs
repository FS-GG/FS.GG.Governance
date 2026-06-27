module FS.GG.Governance.VerifyCommand.Tests.SeamModuleScopeGuardTests

open System
open System.Reflection
open Expecto
open FS.GG.Governance.VerifyCommand

// 076 Phase C (T022): structural per-module guard over the three additive HOST FOLD seam modules
// (SurfaceFold / ViewCurrencyFold / ReleasePreview). Asserts the additive module set is PRESENT and PUBLIC,
// and that no fold's public surface takes ANY type from the host assembly — i.e. the folds stay
// host-`Model`-free (data-model invariant 3; `Loop.update` remains the sole `Model` owner, Principle IV).
// The folds' BEHAVIOR is exercised transitively by the existing golden/rollup/preview suites (Principle I,
// Notes D1); this is the direct per-module assertion of the additive surface (vs only the aggregate drift
// baseline). Reflection lives ONLY in tests.

let private verifyCommand =
    Loop.exitCode Loop.Success |> ignore

    AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.VerifyCommand"
        | None -> false)

let private exportedTypeNames =
    verifyCommand.GetExportedTypes() |> Array.choose (fun t -> Option.ofObj t.FullName)

let private foldModules =
    [ "FS.GG.Governance.VerifyCommand.SurfaceFoldModule"
      "FS.GG.Governance.VerifyCommand.ViewCurrencyFoldModule"
      "FS.GG.Governance.VerifyCommand.ReleasePreviewModule" ]

[<Tests>]
let tests =
    testList
        "SeamModuleScopeGuard (076 host folds)"
        [ test "the three additive host fold modules are present and public" {
              for m in foldModules do
                  Expect.isTrue
                      (exportedTypeNames |> Array.exists (fun n -> n = m))
                      (sprintf "expected additive fold module %s to be public" m)
          }

          test "no host fold's public surface takes a host-assembly type (host-Model-free; data-model invariant 3)" {
              let flags = BindingFlags.Public ||| BindingFlags.Static ||| BindingFlags.DeclaredOnly

              let offenders =
                  verifyCommand.GetExportedTypes()
                  |> Array.filter (fun t ->
                      match Option.ofObj t.FullName with
                      | Some n -> List.contains n foldModules
                      | None -> false)
                  |> Array.collect (fun t -> t.GetMethods flags)
                  |> Array.filter (fun m -> m.GetParameters() |> Array.exists (fun p -> p.ParameterType.Assembly = verifyCommand))
                  |> Array.map (fun m -> m.Name)

              Expect.isEmpty
                  offenders
                  (sprintf "host folds must not take any host-assembly type (Model/Msg/Effect); found: %A" offenders)
          } ]
