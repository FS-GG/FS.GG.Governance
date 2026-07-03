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
        // Resolve the CLI host binary built in the SAME configuration as this test assembly. The test's
        // own BaseDirectory is `.../bin/<Config>/<tfm>/`, and the ProjectReference'd CLI is built into the
        // matching `src/.../bin/<Config>/net10.0/`. Preferring that config (then falling back, unix apphost
        // then .exe) makes the spawn work under both `dotnet test` (Debug) and `dotnet test -c Release` —
        // the prior hardcoded `bin/Debug` path failed under Release (e.g. publish.yml's cli-tests gate).
        let cliBinDir config =
            Path.Combine(repoRoot, "src", "FS.GG.Governance.Cli", "bin", config, "net10.0")

        let configFromBase =
            if AppContext.BaseDirectory.Replace('\\', '/').Contains "/bin/Release/" then "Release" else "Debug"

        let candidates =
            [ configFromBase; "Debug"; "Release" ]
            |> List.distinct
            |> List.collect (fun config ->
                let unix = Path.Combine(cliBinDir config, "FS.GG.Governance.Cli")
                [ unix; unix + ".exe" ])

        match candidates |> List.tryFind File.Exists with
        | Some exe -> runProcess exe args
        | None -> failwithf "CLI host binary not found; looked in: %A" candidates

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
          }

          test "a stray positional argument is an unexpected argument, not an unknown option (#55 F14)" {
              match Cli.parse [ "route"; "stray" ] with
              | Error errors ->
                  Expect.contains errors (UnexpectedArgument "stray") "stray positional is UnexpectedArgument"
                  Expect.isFalse (List.contains (UnknownOption "stray") errors) "not misreported as UnknownOption"
              | Ok request -> failtestf "unexpected request: %A" request
          }

          test "missing-command help enumerates every dispatchable subcommand incl. watch/tui (#55 F12)" {
              let help = CliRender.renderParseError MissingCommand

              for cmd in [ "route"; "explain"; "contract"; "evidence"; "watch"; "tui" ] do
                  Expect.stringContains help cmd (sprintf "help lists '%s'" cmd)
          } ]
