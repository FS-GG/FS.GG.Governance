module FS.GG.Governance.Cli.Tests.TuiParityTests

open Expecto
open FS.GG.Governance.HumanText
open FS.GG.Governance.HumanRender
open FS.GG.Governance.Cli.Tests.RenderSupport

// T040 [US4]: Tui.init holds the SAME ReportView the plain/JSON views project; the navigable nodes
// carry the same facts and are never separately derived (FR-009, SC-006).

[<Tests>]
let tests =
    testList
        "TuiParity"
        [ test "Tui.init View is exactly the ReportView projected from the same decision" {
              let model, _ = Tui.init blockedView
              Expect.equal model.View blockedView "the TUI navigates the same report view"
          }

          test "the navigable view carries the same verdict + blocker facts as the plain projection" {
              let model, _ = Tui.init blockedView
              // the blocking gate appears as a navigable leaf somewhere in the sections.
              let rec labels node =
                  match node with
                  | ReportView.Leaf(l, _) -> [ l ]
                  | ReportView.Group(t, cs) -> t :: List.collect labels cs

              let allLabels = model.View.Sections |> List.collect labels
              Expect.isTrue (allLabels |> List.exists (fun l -> l.Contains "build:ship")) "blocking gate is navigable"
              Expect.stringContains model.View.Title "FAIL" "verdict carried in the view title"
          } ]
