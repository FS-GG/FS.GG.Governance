module FS.GG.Governance.Cli.Tests.ReadOnlyTests

open System.IO
open System.Security.Cryptography
open Expecto
open FS.GG.Governance.Cli.Tests.ParserTests.Support

let hashFile path =
    use sha = SHA256.Create()
    File.ReadAllBytes path
    |> sha.ComputeHash
    |> Array.map (fun b -> b.ToString "x2")
    |> String.concat ""

let fixtureHashes root =
    Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
    |> Seq.sort
    |> Seq.map (fun file -> Path.GetRelativePath(root, file), hashFile file)
    |> Map.ofSeq

[<Tests>]
let tests =
    testList
        "Read-only"
        [ test "commands do not mutate governed fixture files" {
              let root = fixture "light"
              let before = fixtureHashes root

              for command in [ "route"; "explain"; "contract"; "evidence" ] do
                  let run = runCli [ command; "--root"; root; "--mode"; "inner"; "--json"; "--review-budget"; "0" ]
                  Expect.equal run.ExitCode 0 (command + run.Stdout + run.Stderr)

              let after = fixtureHashes root
              Expect.equal after before "fixture hashes unchanged"
          }

          test "--out writes only the selected report path" {
              let root = fixture "light"
              let outFile = Path.Combine(repoRoot, ".tmp", "f12-read-only", "route.json")
              if File.Exists outFile then File.Delete outFile
              let before = fixtureHashes root
              let run = runCli [ "route"; "--root"; root; "--json"; "--out"; outFile ]
              Expect.equal run.ExitCode 0 (run.Stdout + run.Stderr)
              Expect.isTrue (File.Exists outFile) "report written"
              Expect.equal (fixtureHashes root) before "fixture unchanged"
          } ]
