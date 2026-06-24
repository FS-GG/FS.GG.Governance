module FS.GG.Governance.ReleaseCommand.Tests.NoMutationTests

open System.IO
open System.Security.Cryptography
open Expecto
open FS.GG.Governance.ReleaseCommand
open FS.GG.Governance.ReleaseCommand.Tests.Support

// The governed repository is never mutated except the explicitly-requested `release.json` (FR-016). Snapshot
// the tree (relative path → content hash) before `Interpreter.run`, then assert it is unchanged when `--out`
// is OUTSIDE the working tree, and changed by exactly the requested file when `--out` is INSIDE it.

let private hashTree (root: string) : Map<string, string> =
    Directory.GetFiles(root, "*", SearchOption.AllDirectories)
    |> Array.map (fun f ->
        use sha = SHA256.Create()
        let rel = Path.GetRelativePath(root, f).Replace('\\', '/')
        let hash = sha.ComputeHash(File.ReadAllBytes f) |> System.Convert.ToHexString
        rel, hash)
    |> Map.ofArray

let private runBoth repo outPath =
    let ports = { Interpreter.realPorts repo with Out = ignore }

    let request =
        { Loop.Repo = repo
          Loop.Format = Loop.TextAndJson
          Loop.ReleaseOut = outPath }

    Interpreter.run ports request

[<Tests>]
let tests =
    testList
        "NoMutation"
        [ test "writing --out OUTSIDE the repo leaves the working tree byte-for-byte unchanged" {
              withTempRepo releaseYmlAllBlocking writeMetSources (fun repo ->
                  let before = hashTree repo

                  withTempDir (fun outDir ->
                      let outPath = Path.Combine(outDir, "release.json")
                      let model = runBoth repo outPath
                      Expect.equal model.Exit Loop.Success "run succeeded"
                      Expect.isTrue (File.Exists outPath) "artifact written outside the repo"
                      Expect.equal (hashTree repo) before "repo tree unchanged"))
          }

          test "writing --out INSIDE the repo adds only the requested release.json" {
              withTempRepo releaseYmlAllBlocking writeMetSources (fun repo ->
                  let before = hashTree repo
                  let outPath = Path.Combine(repo, "release.json")
                  let model = runBoth repo outPath
                  Expect.equal model.Exit Loop.Success "run succeeded"

                  let after = hashTree repo
                  let added = Map.toList after |> List.map fst |> List.filter (fun p -> not (Map.containsKey p before))
                  Expect.equal added [ "release.json" ] "the only added path is release.json"

                  // No pre-existing file changed.
                  let changed =
                      before |> Map.toList |> List.filter (fun (p, h) -> Map.tryFind p after <> Some h)

                  Expect.isEmpty changed "no pre-existing file was modified")
          } ]
