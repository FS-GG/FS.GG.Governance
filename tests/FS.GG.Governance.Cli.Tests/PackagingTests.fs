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

              // The repo nuget.config declares packageSourceMapping (FS.GG.Contracts resolves from
              // the private org GitHub Packages feed). `dotnet tool install --add-source` is REJECTED
              // whenever a source mapping is in effect, and the mapping would otherwise misroute the
              // freshly-packed FS.GG.Governance.Cli away from this local feed. So install from a
              // self-contained temp config that maps: this local feed for the packed FS.GG.Governance.*
              // tool, the org feed for its FS.GG.Contracts dependency, and nuget.org for the rest.
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
    <add key=\"fsgg-github\" value=\"https://nuget.pkg.github.com/FS-GG/index.json\" protocolVersion=\"3\" />
    <add key=\"nuget.org\" value=\"https://api.nuget.org/v3/index.json\" protocolVersion=\"3\" />
  </packageSources>
  <packageSourceCredentials>
    <fsgg-github>
      <add key=\"Username\" value=\"%%FSGG_PACKAGES_ACTOR%%\" />
      <add key=\"ClearTextPassword\" value=\"%%FSGG_PACKAGES_READ_TOKEN%%\" />
    </fsgg-github>
  </packageSourceCredentials>
  <packageSourceMapping>
    <packageSource key=\"local\">
      <package pattern=\"FS.GG.Governance.*\" />
    </packageSource>
    <packageSource key=\"fsgg-github\">
      <package pattern=\"FS.GG.Contracts\" />
    </packageSource>
    <packageSource key=\"nuget.org\">
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
                      [ "tool"
                        "install"
                        "FS.GG.Governance.Cli"
                        "--tool-path"
                        toolPath
                        "--configfile"
                        configFile ]

              Expect.equal install.ExitCode 0 (install.Stdout + install.Stderr)

              let exe = Path.Combine(toolPath, "fsgg-governance")
              let run = runProcess exe [ "route"; "--root"; fixture "light"; "--mode"; "inner"; "--json" ]
              Expect.equal run.ExitCode 0 (run.Stdout + run.Stderr)
              Expect.stringContains run.Stdout "\"command\":\"route\"" "route command"
          } ]
