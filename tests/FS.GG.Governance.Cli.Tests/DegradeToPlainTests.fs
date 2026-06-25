module FS.GG.Governance.Cli.Tests.DegradeToPlainTests

open Expecto
open FS.GG.Governance.HumanText
open FS.GG.Governance.HumanRender
open FS.GG.Governance.Cli.Tests.RenderSupport

// T028 [US2]: the Plain degrade path (taken on non-TTY / NO_COLOR / explicit-plain) writes the
// EXACT HumanText.of* string with NO ANSI escapes — degrade is to the precomputed plain projection,
// not a third rendering (FR-004, SC-002, SC-004).

[<Tests>]
let tests =
    testList
        "DegradeToPlain"
        [ test "emit Plain writes the precomputed plain string verbatim" {
              let console, sw = plainConsole 120
              RichRender.emit RenderMode.Plain blockedView blockedPlain console
              Expect.equal (sw.ToString()) blockedPlain "Plain mode is byte-equal to the HumanText projection"
          }

          test "emit Plain output contains no ANSI escape" {
              let console, sw = colorConsole 120
              RichRender.emit RenderMode.Plain blockedView blockedPlain console
              Expect.isFalse (sw.ToString().Contains esc) "the degrade path is ANSI-free even on a color console"
          } ]
