module FS.GG.Governance.Cli.Tests.RichRenderTests

open Expecto
open FS.GG.Governance.HumanText
open FS.GG.Governance.HumanRender
open FS.GG.Governance.Cli.Tests.RenderSupport

// T027 [US2]: rich emit over a console shows a verdict banner + grouped tables conveying the SAME
// verdict/blockers the plain/JSON projections do, and (on a color console) emits ANSI color.

[<Tests>]
let tests =
    testList
        "RichRender"
        [ test "rich render conveys the same verdict + blockers as the plain projection" {
              let console, sw = plainConsole 120
              RichRender.emit RenderMode.Rich blockedView blockedPlain console
              let out = sw.ToString()
              Expect.stringContains out blockedView.Title "banner carries the verdict title"
              Expect.stringContains out "build:ship" "the blocking gate appears in the table"
              Expect.stringContains out "blocked" "the blocked exit status is shown"
          }

          test "rich render emits ANSI color on a color-capable console" {
              let console, sw = colorConsole 120
              RichRender.emit RenderMode.Rich blockedView blockedPlain console
              let out = sw.ToString()
              Expect.isTrue (out.Contains esc) "rich output on a color console contains ANSI escapes"
          } ]
