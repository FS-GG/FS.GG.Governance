module FS.GG.Governance.Cli.Tests.ParserTests

open System
open System.Diagnostics
open System.IO
open Expecto
open FS.GG.Governance.Kernel
open FS.GG.Governance.Cli

module Support =

    type CommandRun =
        { ExitCode: int
          Stdout: string
          Stderr: string }

    let rec findRepoRoot dir =
        if File.Exists(Path.Combine(dir, "FS.GG.Governance.sln")) then
            dir
        else
            match Directory.GetParent dir |> Option.ofObj with
            | None -> failwith "could not find repository root"
            | Some parent -> findRepoRoot parent.FullName

    let repoRoot = findRepoRoot AppContext.BaseDirectory

    let fixture name =
        Path.Combine(repoRoot, "tests", "FS.GG.Governance.Cli.Tests", "fixtures", name)

    let runProcess file args =
        let psi = ProcessStartInfo()
        psi.FileName <- file
        psi.WorkingDirectory <- repoRoot
        psi.RedirectStandardOutput <- true
        psi.RedirectStandardError <- true
        psi.UseShellExecute <- false
        psi.Environment["MSBUILDDISABLENODEREUSE"] <- "1"
        psi.Environment["DOTNET_CLI_RUN_MSBUILD_NODE_REUSE"] <- "0"

        for arg in args do
            psi.ArgumentList.Add arg

        use proc = new Process()
        proc.StartInfo <- psi

        if not (proc.Start()) then
            failwith "process did not start"

        let stdoutTask = proc.StandardOutput.ReadToEndAsync()
        let stderrTask = proc.StandardError.ReadToEndAsync()
        if not (proc.WaitForExit(120000)) then
            try
                proc.Kill(true)
            with _ ->
                ()

            failwithf "process timed out: %s %A" file args

        let stdout = stdoutTask.GetAwaiter().GetResult()
        let stderr = stderrTask.GetAwaiter().GetResult()

        { ExitCode = proc.ExitCode
          Stdout = stdout
          Stderr = stderr }

    let runCli args =
        let exe =
            let unix = Path.Combine(repoRoot, "src", "FS.GG.Governance.Cli", "bin", "Debug", "net10.0", "FS.GG.Governance.Cli")
            let windows = unix + ".exe"
            if File.Exists unix then unix else windows

        runProcess exe args

[<Tests>]
let tests =
    testList
        "Parser"
        [ test "route defaults normalize to inner/text/cache-only/all domains" {
              match Cli.parse [ "route" ] with
              | Ok request ->
                  Expect.equal request.Command RouteCommand "command"
                  Expect.equal request.Root "." "root"
                  Expect.equal request.Mode Inner "mode"
                  Expect.equal request.Format Text "format"
                  Expect.equal request.ReviewBudget CacheOnly "budget"
                  Expect.equal request.Domains (Set.ofList [ SpecKitDomain; DesignSystemDomain ]) "domains"
              | Error errors -> failtestf "unexpected parse errors: %A" errors
          }

          test "shared options parse without filesystem I/O" {
              match
                  Cli.parse
                      [ "evidence"
                        "--root"
                        "/path/that/does/not/exist"
                        "--mode"
                        "gate"
                        "--json"
                        "--scope"
                        "specs/012-cli,src"
                        "--review-budget"
                        "2"
                        "--review-store"
                        ".tmp/reviews"
                        "--out"
                        ".tmp/report.json"
                        "--judge-model"
                        "judge"
                        "--judge-version"
                        "v1" ]
              with
              | Ok request ->
                  Expect.equal request.Command EvidenceCommand "command"
                  Expect.equal request.Mode Gate "mode"
                  Expect.equal request.Format Json "format"
                  Expect.equal request.Scope [ "specs/012-cli"; "src" ] "scope"
                  Expect.equal request.ReviewBudget (FreshReviews 2) "budget"
                  Expect.equal request.Judge { ModelId = "judge"; Version = "v1" } "judge"
              | Error errors -> failtestf "unexpected parse errors: %A" errors
          }

          test "malformed invocations return usage errors" {
              Expect.equal (Cli.parse []) (Error [ MissingCommand ]) "missing command"
              Expect.equal (Cli.parse [ "nope" ]) (Error [ UnknownCommand "nope" ]) "unknown command"

              match Cli.parse [ "route"; "--mode"; "outer"; "--review-budget"; "-1"; "--format"; "xml" ] with
              | Error errors ->
                  Expect.contains errors (InvalidMode "outer") "mode"
                  Expect.contains errors (InvalidReviewBudget "-1") "budget"
                  Expect.contains errors (InvalidFormat "xml") "format"
              | Ok request -> failtestf "unexpected request: %A" request
          } ]
