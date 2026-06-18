module FS.GG.Governance.Cli.Tests.PackagingTests

open System.IO
open Expecto
open FS.GG.Governance.Cli.Tests.ParserTests.Support

[<Tests>]
let tests =
    testList
        "Packaging"
        [ test "packaged tool installs from local feed and runs route" {
              let feed = Path.Combine(System.Environment.GetFolderPath System.Environment.SpecialFolder.UserProfile, ".local", "share", "nuget-local")
              Directory.CreateDirectory feed |> ignore

              let pack = runProcess "dotnet" [ "pack"; "src/FS.GG.Governance.Cli"; "-c"; "Release"; "-o"; feed ]
              Expect.equal pack.ExitCode 0 (pack.Stdout + pack.Stderr)

              let toolPath = Path.Combine(repoRoot, ".tmp", "f12-tool-tests")
              if Directory.Exists toolPath then Directory.Delete(toolPath, true)

              let install =
                  runProcess
                      "dotnet"
                      [ "tool"
                        "install"
                        "FS.GG.Governance.Cli"
                        "--tool-path"
                        toolPath
                        "--add-source"
                        feed ]

              Expect.equal install.ExitCode 0 (install.Stdout + install.Stderr)

              let exe = Path.Combine(toolPath, "fsgg-governance")
              let run = runProcess exe [ "route"; "--root"; fixture "light"; "--mode"; "inner"; "--json" ]
              Expect.equal run.ExitCode 0 (run.Stdout + run.Stderr)
              Expect.stringContains run.Stdout "\"command\":\"route\"" "route command"
          } ]
