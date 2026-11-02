module FS.GG.Governance.Cli.Tests.PackagingTests

open System.IO
open Expecto
open FS.GG.Governance.Cli.Tests.ParserTests.Support

[<Tests>]
let tests =
    testList
        "Packaging"
        [
            test "packaged tool installs from local feed and runs route" {
                let feed = Path.Combine(repoRoot, ".tmp", "f12-tool-feed")

                Directory.CreateDirectory feed |> ignore

                let pack =
                    runProcess "dotnet" [ "pack"; "src/FS.GG.Governance.Cli"; "-c"; "Release"; "-o"; feed ]

                Expect.equal pack.ExitCode 0 (pack.Stdout + pack.Stderr)

                let toolPath = Path.Combine(repoRoot, ".tmp", "f12-tool-tests")

                if Directory.Exists toolPath then
                    Directory.Delete(toolPath, true)

                // The repo nuget.config declares packageSourceMapping. `dotnet tool install --add-source` is REJECTED
                // whenever a source mapping is in effect, and the mapping would otherwise misroute the
                // freshly-packed FS.GG.Governance.Cli away from this local feed. So install from a
                // self-contained temp config that maps: this local feed for the packed FS.GG.Governance.*
                // tool, and public nuget.org for Contracts and the remaining dependencies (#484).
                let cfgDir = Path.Combine(repoRoot, ".tmp", "f12-tool-cfg")
                Directory.CreateDirectory cfgDir |> ignore
                let configFile = Path.Combine(cfgDir, "tool-install.nuget.config")

                File.WriteAllText(
                    configFile,
                    sprintf
                        "<?xml version=\"1.0\" encoding=\"utf-8\"?>
<configuration>
  <packageSources>
    <clear />
    <add key=\"local\" value=\"%s\" />
    <add key=\"nuget.org\" value=\"https://api.nuget.org/v3/index.json\" protocolVersion=\"3\" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key=\"local\">
      <package pattern=\"FS.GG.Governance.*\" />
    </packageSource>
    <packageSource key=\"nuget.org\">
      <package pattern=\"FS.GG.Contracts\" />
      <package pattern=\"*\" />
    </packageSource>
  </packageSourceMapping>
</configuration>
"
                        feed
                )

                let install =
                    runProcess
                        "dotnet"
                        [
                            "tool"
                            "install"
                            "FS.GG.Governance.Cli"
                            "--tool-path"
                            toolPath
                            "--configfile"
                            configFile
                        ]

                Expect.equal install.ExitCode 0 (install.Stdout + install.Stderr)

                let exe = Path.Combine(toolPath, "fsgg-governance")

                let run =
                    runProcess exe [ "route"; "--root"; fixture "light"; "--mode"; "inner"; "--json" ]

                Expect.equal run.ExitCode 0 (run.Stdout + run.Stderr)
                Expect.stringContains run.Stdout "\"command\":\"route\"" "route command"

                // #470: the packed tool carries the Verify host (no second executable or package id).
                let verify = runProcess exe [ "verify"; "--bogus" ]
                Expect.equal verify.ExitCode 2 (verify.Stdout + verify.Stderr)

                Expect.stringContains
                    verify.Stderr
                    "fsgg verify: unknown flag: --bogus"
                    "installed tool reaches the Verify host"
            }
        ]
