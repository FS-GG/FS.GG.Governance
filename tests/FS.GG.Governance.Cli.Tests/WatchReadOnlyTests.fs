module FS.GG.Governance.Cli.Tests.WatchReadOnlyTests

open Expecto
open FS.GG.Governance.HumanText
open FS.GG.Governance.HumanRender
open FS.GG.Governance.HumanRender.Watch

// T035 [US3]: across the watch transition the ONLY effects are SenseChanges / ScheduleDebounce /
// ReRender; no Msg changes a verdict, evaluates a new rule, or emits a JSON contract write. ReRender
// re-runs the EXISTING evaluation and re-projects only (FR-008, SC-006).

let private allowedEffect =
    function
    | SenseChanges _
    | ScheduleDebounce _
    | ReRender _ -> true

[<Tests>]
let tests =
    testList
        "WatchReadOnly"
        [ test "init emits only a read-only SenseChanges effect" {
              let _, es = init "/repo" RenderMode.Rich
              Expect.equal es [ SenseChanges "/repo" ] "init only starts sensing"
          }

          test "every effect across the lifecycle is sense/schedule/re-render (read-only)" {
              let model0, e0 = init "/repo" RenderMode.Plain
              let m1, e1 = update (ChangeDetected 0L) model0
              let m2, e2 = update (WindowSettled debounceWindow) m1
              let _, e3 = update (Rerendered Rendered) m2

              for e in e0 @ e1 @ e2 @ e3 do
                  Expect.isTrue (allowedEffect e) (sprintf "effect %A is read-only" e)
          }

          test "ReRender names the existing root + mode — it re-projects, it does not re-decide" {
              let model0, _ = init "/repo" RenderMode.Rich
              let m1, _ = update (ChangeDetected 0L) model0
              let _, es = update (WindowSettled debounceWindow) m1
              Expect.contains es (ReRender("/repo", RenderMode.Rich)) "re-render reuses root + mode"
          } ]
