module FS.GG.Governance.Cli.Tests.JsonNoOpTests

open Expecto
open FS.GG.Governance.HumanText
open FS.GG.Governance.HumanRender
open FS.GG.Governance.Cli.Tests.RenderSupport

// T030 [US2] (library-level): JSON always overrides and never reaches the rich renderer — `emit Json`
// is a no-op, so the host writes the byte-identical, ANSI-free *Json string directly (FR-004, SC-002).

[<Tests>]
let tests =
    testList
        "JsonNoOp"
        [ test "emit Json writes nothing to the console (JSON bypasses RichRender)" {
              let console, sw = colorConsole 120
              RichRender.emit RenderMode.Json blockedView blockedPlain console
              Expect.equal (sw.ToString()) "" "Json mode is a no-op in RichRender"
          } ]
