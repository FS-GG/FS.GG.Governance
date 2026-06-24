module FS.GG.Governance.VerifyCommand.Tests.NoMutationTests

open System.IO
open System.Security.Cryptography
open Expecto
open FS.GG.Governance.Config
open FS.GG.Governance.FreshnessSensing
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.VerifyCommand
open FS.GG.Governance.VerifyCommand.Tests.Support

// T035 (Polish) — the no-mutation guarantee (FR-013): the governed repository is never mutated other than the
// requested verify.json (and the opt-in store, not exercised here). The reused cores read through real edges
// (real Config reader, real git sensing, real freshness sensor, real store reader); only the gate EXECUTION
// is a deterministic fake (no real `dotnet` process) and the artifact write is a real disk write.

let private diskWriter: Interpreter.ArtifactWriter =
    fun path content ->
        try
            match Path.GetDirectoryName path with
            | null | "" -> ()
            | d -> Directory.CreateDirectory d |> ignore
            File.WriteAllText(path, content)
            Ok()
        with e -> Error e.Message

let private realishPorts (dir: string) (cap: Capture) : Interpreter.Ports =
    { Files = Loader.fileSystemReader dir
      Git = FS.GG.Governance.Snapshot.Interpreter.realPorts dir
      Freshness = FreshnessSensing.realSensor dir
      Store = FreshnessSensing.realStoreReader
      Write = diskWriter
      Out = capturingSink cap
      Execute = fakeExecPortPass }

/// Relative-path → content-hash snapshot of a directory tree, skipping `.git`.
let private snapshotTree (dir: string) : Map<string, string> =
    Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
    |> Seq.filter (fun p -> not ((Path.GetRelativePath(dir, p)).Replace('\\', '/').StartsWith ".git/"))
    |> Seq.map (fun p ->
        let rel = (Path.GetRelativePath(dir, p)).Replace('\\', '/')
        use sha = SHA256.Create()
        rel, (sha.ComputeHash(File.ReadAllBytes p) |> System.Convert.ToHexString))
    |> Map.ofSeq

[<Tests>]
let tests =
    testList
        "NoMutation (Polish)"
        [ test "writing --verify-out OUTSIDE the repo leaves the repo tree byte-for-byte unchanged" {
              withTempRepo (fun dir ->
                  let outDir = Path.Combine(Path.GetTempPath(), "fsgg-verify-out-" + System.Guid.NewGuid().ToString("N"))
                  Directory.CreateDirectory outDir |> ignore

                  try
                      let before = snapshotTree dir
                      let cap = newCapture ()

                      let req =
                          { requestForProfile (Loop.Since "HEAD~1") Loop.Text Standard with
                              Repo = dir
                              VerifyOut = Path.Combine(outDir, "verify.json")
                              StorePath = Path.Combine(outDir, "store.json") }

                      Interpreter.run (realishPorts dir cap) req |> ignore

                      let after = snapshotTree dir
                      Expect.equal after before "the governed repo tree is unchanged when the artifact is written outside it"
                  finally
                      try Directory.Delete(outDir, true) with _ -> ())
          }

          test "writing --verify-out INSIDE the repo (no --persist-store) adds only the requested verify.json" {
              withTempRepo (fun dir ->
                  let before = snapshotTree dir
                  let cap = newCapture ()

                  let req =
                      { requestForProfile (Loop.Since "HEAD~1") Loop.Text Standard with
                          Repo = dir
                          VerifyOut = Path.Combine(dir, "readiness", "verify.json")
                          StorePath = Path.Combine(dir, "readiness", "evidence-reuse.json") }

                  Interpreter.run (realishPorts dir cap) req |> ignore

                  let after = snapshotTree dir
                  let added = Map.toList after |> List.map fst |> List.filter (fun p -> not (Map.containsKey p before))
                  let changed = Map.toList before |> List.filter (fun (p, h) -> Map.tryFind p after <> Some h) |> List.map fst

                  Expect.equal added [ "readiness/verify.json" ] "only verify.json is added"
                  Expect.isEmpty changed "no existing file is changed") } ]
