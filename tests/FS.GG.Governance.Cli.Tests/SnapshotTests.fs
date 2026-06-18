module FS.GG.Governance.Cli.Tests.SnapshotTests

open System.Text.Json
open Expecto
open FS.GG.Governance.Cli.Tests.ParserTests.Support

[<Tests>]
let tests =
    testList
        "Snapshot command surface"
        [ test "built route over light fixture exits successfully and reports route JSON" {
              let run = runCli [ "route"; "--root"; fixture "light"; "--mode"; "inner"; "--json" ]
              Expect.equal run.ExitCode 0 run.Stderr
              use doc = JsonDocument.Parse(run.Stdout)
              Expect.equal (doc.RootElement.GetProperty("request").GetProperty("command").GetString()) "route" "command"
              Expect.equal (doc.RootElement.GetProperty("exit").GetProperty("code").GetInt32()) 0 "exit"
          }

          test "built route over blocking fixture in gate mode exits governed blocking" {
              let run = runCli [ "route"; "--root"; fixture "blocking"; "--mode"; "gate"; "--json" ]
              Expect.equal run.ExitCode 2 run.Stdout
              use doc = JsonDocument.Parse(run.Stdout)
              Expect.equal (doc.RootElement.GetProperty("exit").GetProperty("category").GetString()) "governed-blocking" "category"
          }

          test "unavailable root is input-unavailable rather than usage" {
              let run = runCli [ "route"; "--root"; fixture "does-not-exist"; "--json" ]
              Expect.equal run.ExitCode 66 run.Stdout
              use doc = JsonDocument.Parse(run.Stdout)
              Expect.equal (doc.RootElement.GetProperty("exit").GetProperty("code").GetInt32()) 66 "exit"
          }

          test "all four commands run against this repository root" {
              for command in [ "route"; "explain"; "contract"; "evidence" ] do
                  let run = runCli [ command; "--root"; "."; "--mode"; "inner"; "--json"; "--review-budget"; "0" ]
                  Expect.equal run.ExitCode 0 (command + ": " + run.Stdout + run.Stderr)
                  use doc = JsonDocument.Parse(run.Stdout)
                  Expect.equal (doc.RootElement.GetProperty("request").GetProperty("command").GetString()) command command
          } ]
