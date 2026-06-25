module FS.GG.Governance.RefreshCommand.Tests.DryRunTests

open Expecto
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.RefreshJson.RefreshModel
open FS.GG.Governance.RefreshCommand
open FS.GG.Governance.RefreshCommand.Tests.Support

// US2 — `--dry-run` performs the identical currency evaluation, reports `would-regenerate`, and writes
// NOTHING. The pure-update invariant (no RegenerateView / RecordProvenance / view write) plus the
// real-interpreter preview.

let private effectsAfterSensing (recorded) (sensed) =
    let req = { requestFor "." with DryRun = true }
    let m0, _ = Loop.init req
    let m1, _ = Loop.update (Loop.ManifestLoaded(Ok(parseYml refreshYmlOneView))) m0
    let m2, _ = Loop.update (Loop.Sensed("doc", sensed)) m1
    Loop.update (Loop.RecordedRead("doc", recorded)) m2

[<Tests>]
let tests =
    testList
        "DryRun"
        [ test "pure update: a stale entry yields WouldRegenerate and emits NO regenerate/record/view-write" {
              let model, effects = effectsAfterSensing None (Ok(digestsOf [ "d1" ], GeneratorVersion "g1"))

              match (model.Views |> List.find (fun v -> v.Entry.ViewId = "doc")).Status with
              | WouldRegenerate _ -> ()
              | other -> failtestf "expected WouldRegenerate, got %A" other

              let forbidden =
                  effects
                  |> List.exists (function
                      | Loop.RegenerateView _
                      | Loop.RecordProvenance _ -> true
                      | _ -> false)

              Expect.isFalse forbidden "dry-run emits no regenerate/record effect"
          }

          test "roll-up of a would-regenerate ⇒ ViewsRegenerated (exit 5)" {
              let model, _ = effectsAfterSensing None (Ok(digestsOf [ "d1" ], GeneratorVersion "g1"))
              Expect.equal model.Exit ViewsRegenerated "a would-regenerate is the exit-5 success shade"
          }

          test "real interpreter: --dry-run over a stale fixture ⇒ exit 5, would-regenerate, nothing written" {
              withTempRepo refreshYmlOneView (fun d -> writeFile d "src.txt" "hello\n") (fun repo ->
                  let m = runReal repo { requestFor repo with DryRun = true }
                  Expect.equal m.Exit ViewsRegenerated "stale views exist ⇒ exit 5 even in dry-run"
                  Expect.isFalse (fileExists repo "out.txt") "no view written"
                  Expect.isFalse (fileExists repo ".fsgg/refresh.lock.json") "no provenance written"

                  match m.Decision |> Option.map (fun d -> (d.Views |> List.head).Status) with
                  | Some(WouldRegenerate _) -> ()
                  | other -> failtestf "expected WouldRegenerate, got %A" other)
          }

          test "real interpreter: --dry-run over an all-current repo ⇒ exit 0" {
              withTempRepo refreshYmlOneView (fun d -> writeFile d "src.txt" "hello\n") (fun repo ->
                  runReal repo (requestFor repo) |> ignore // seed current
                  let m = runReal repo { requestFor repo with DryRun = true }
                  Expect.equal m.Exit NothingToRefresh "all current ⇒ exit 0")
          } ]
