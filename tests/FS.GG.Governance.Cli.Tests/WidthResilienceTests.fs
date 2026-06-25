module FS.GG.Governance.Cli.Tests.WidthResilienceTests

open Expecto
open FS.GG.Governance.HumanText
open FS.GG.Governance.HumanRender
open FS.GG.Governance.Cli.Tests.RenderSupport

// T029 [US2]: rich render at a range of widths (incl. very narrow) reflows/truncates cleanly without
// throwing or corrupting layout; the safe default is used when width is unknown (FR-006, SC-004).

[<Tests>]
let tests =
    testList
        "WidthResilience"
        [ for width in [ 200; 80; 40; 20; 10 ] do
              yield
                  test (sprintf "rich render at width %d produces output without throwing" width) {
                      let console, sw = plainConsole width
                      RichRender.emit RenderMode.Rich blockedView blockedPlain console
                      let out = sw.ToString()
                      Expect.isGreaterThan out.Length 0 "render produced output"
                      // each emitted line fits the console width (Spectre reflows/truncates to it).
                      for line in out.Replace("\r\n", "\n").Split('\n') do
                          Expect.isLessThanOrEqual line.Length width (sprintf "line within width %d" width)
                  }

          yield
              test "the safe default width is a sane positive value" {
                  Expect.isGreaterThan RichRender.defaultWidth 0 "default width is positive"
              } ]
