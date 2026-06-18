module FS.GG.Governance.Cli.Tests.OutputTests

open System.Text.Json
open Expecto
open FS.GG.Governance.Kernel
open FS.GG.Governance.Cli

let route =
    { Stakes = Routine
      Advisory = []
      Blocking = []
      Reason = "light - no gates" }

let request =
    match Cli.parse [ "route"; "--root"; "."; "--json" ] with
    | Ok request -> request
    | Error errors -> failwithf "%A" errors

let result =
    { Request = Some request
      Payload = Some(RoutePayload route)
      Budget =
        { Requested = [ "b" ]
          CacheHits = []
          CacheMisses = [ "b" ]
          FreshDispatches = []
          Pending = [ "b" ]
          BudgetExhausted = [ "b" ] }
      Failures = []
      Exit = Success }

[<Tests>]
let tests =
    testList
        "Output"
        [ test "JSON rendering is stable and parseable" {
              let a = Cli.renderJson result
              let b = Cli.renderJson result
              let c = Cli.renderJson result
              Expect.equal a b "repeat 1"
              Expect.equal b c "repeat 2"
              use doc = JsonDocument.Parse(a)
              Expect.equal (doc.RootElement.GetProperty("payload").GetProperty("kind").GetString()) "route" "payload kind"
          }

          test "text rendering names command, mode, root, exit, budget, and route state" {
              let text = Cli.renderText { result with Request = Some { request with Format = Text } }
              Expect.stringContains text "command: route" "command"
              Expect.stringContains text "mode: inner" "mode"
              Expect.stringContains text "root: ." "root"
              Expect.stringContains text "exit: success (0)" "exit"
              Expect.stringContains text "budget:" "budget"
              Expect.stringContains text "light" "route"
          } ]
