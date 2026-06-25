module FS.GG.Governance.Cli.Tests.WatchDebounceTests

open Expecto
open FS.GG.Governance.HumanText
open FS.GG.Governance.HumanRender
open FS.GG.Governance.HumanRender.Watch

// T034 [US3]: the PURE Watch.update coalesces a burst of ChangeDetected within the window into
// exactly ONE ReRender once the window settles (SC-005); changes spread beyond the window yield one
// ReRender each. The event burst is SYNTHETIC (disclosed here; `Synthetic` in the test name).

let private isReRender =
    function
    | ReRender _ -> true
    | _ -> false

// SYNTHETIC: an in-memory event burst drives the pure update; no real timer/filesystem is needed to
// prove the coalescing transition (the FileSystemWatcher interpreter path is exercised separately).
let private drive (model: WatchModel) (msgs: WatchMsg list) : WatchModel * WatchEffect list =
    msgs
    |> List.fold
        (fun (m, acc) msg ->
            let m', es = update msg m
            m', acc @ es)
        (model, [])

let private w = debounceWindow

[<Tests>]
let tests =
    testList
        "WatchDebounce"
        [ test "Synthetic burst within the window settles to exactly one ReRender" {
              let model0, _ = init "/repo" RenderMode.Plain
              // a burst of three changes, then the settle for the LAST change.
              let _, effects =
                  drive model0 [ ChangeDetected 0L; ChangeDetected 50L; ChangeDetected 100L; WindowSettled(100L + w) ]

              let reRenders = effects |> List.filter isReRender |> List.length
              Expect.equal reRenders 1 "a burst coalesces into a single re-render"
          }

          test "Synthetic: an earlier window's settle is ignored after a newer change" {
              let model0, _ = init "/repo" RenderMode.Plain
              // change at 0 schedules settle at w; a newer change at 100 arrives before that settle.
              let _, effects =
                  drive model0 [ ChangeDetected 0L; ChangeDetected 100L; WindowSettled(0L + w); WindowSettled(100L + w) ]

              let reRenders = effects |> List.filter isReRender |> List.length
              Expect.equal reRenders 1 "the stale settle is ignored; only the latest fires once"
          }

          test "Synthetic: changes spread beyond the window yield one ReRender each" {
              let model0, _ = init "/repo" RenderMode.Plain

              let _, effects =
                  drive
                      model0
                      [ ChangeDetected 0L
                        WindowSettled(0L + w)
                        ChangeDetected(1000L)
                        WindowSettled(1000L + w) ]

              let reRenders = effects |> List.filter isReRender |> List.length
              Expect.equal reRenders 2 "two well-separated changes ⇒ two re-renders"
          }

          test "each ChangeDetected schedules a debounce at change-time + window" {
              let model0, _ = init "/repo" RenderMode.Plain
              let _, es = update (ChangeDetected 42L) model0
              Expect.contains es (ScheduleDebounce(42L + w)) "schedules the window from the change time"
          } ]
