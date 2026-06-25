module FS.GG.Governance.Cli.Tests.TuiReadOnlyTests

open Expecto
open FS.GG.Governance.HumanRender
open FS.GG.Governance.HumanRender.Tui
open FS.GG.Governance.Cli.Tests.RenderSupport

// T041 [US4]: driving the pure Tui.update over recorded keypresses changes ONLY Path/Expanded — no
// verdict changes, no new gate runs, no contract emitted; only ReadKey/Draw/Exit effects appear
// (FR-008, FR-009, SC-006).

let private onlyKnownEffects =
    List.forall (function
        | ReadKey
        | Draw _
        | Exit -> true)

[<Tests>]
let tests =
    testList
        "TuiReadOnly"
        [ test "navigation changes only Path/Expanded and the View is never mutated" {
              let model0, _ = init blockedView
              let m1, _ = update MoveDown model0
              let m2, _ = update Expand m1
              let m3, _ = update MoveUp m2
              let m4, _ = update Collapse m3

              Expect.equal m4.View blockedView "the navigated report view is never mutated"
          }

          test "every effect is ReadKey/Draw/Exit; Quit ⇒ Exit" {
              let model0, e0 = init blockedView
              let _, eQuit = update Quit model0
              let _, eMove = update MoveDown model0

              Expect.isTrue (onlyKnownEffects (e0 @ eQuit @ eMove)) "only read-only navigation effects appear"
              Expect.contains eQuit Exit "Quit exits"
          }

          test "Expand records the current path as expanded (read-only selection state)" {
              let model0, _ = init blockedView
              let m1, _ = update Expand model0
              Expect.isTrue (Set.contains m1.Path m1.Expanded) "expand toggles only selection state"
          } ]
