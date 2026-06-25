module FS.GG.Governance.Config.Tests.LoaderTests

open System
open System.IO
open Expecto
open FS.GG.Governance.Config
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Config.Schema
open FS.GG.Governance.Config.Tests.Support

// The Loader EDGE over REAL fixture directories (Principle V): absent vs present-but-invalid
// (FR-015), read-error surfacing (Principle VI), and host-path independence (SC-002/SC-005).

let private minimalProject = "schemaVersion: 1\nid: p\ngovernedRoot: .\ndomains:\n  - workflow"
let private minimalCaps = "schemaVersion: 2\ndomains:\n  - workflow"

/// Recursively copy a fixture's `.fsgg` dir into a fresh parent and return that parent.
let private copyFixtureToTemp (name: string) : string =
    let parent = Path.Combine(Path.GetTempPath(), "fsgg-" + Guid.NewGuid().ToString("N"))
    let srcFsgg = Path.Combine(fixtureDir name, ".fsgg")
    let dstFsgg = Path.Combine(parent, ".fsgg")
    Directory.CreateDirectory dstFsgg |> ignore
    for file in Directory.GetFiles srcFsgg do
        File.Copy(file, Path.Combine(dstFsgg, (FileInfo file).Name))
    parent

[<Tests>]
let tests =
    testList
        "Loader.edge"
        [ test "loadAndValidate over a real directory → Valid" {
              match validateFixture "valid-complete" with
              | Valid _ -> ()
              | Invalid d -> failtestf "expected Valid, got %A" d
          }

          test "absent optional file → Valid with None (FR-015)" {
              match validateFixture "valid-no-policy" with
              | Valid f -> Expect.isNone f.Policy "absent optional → None"
              | Invalid d -> failtestf "expected Valid, got %A" d
          }

          test "present-but-invalid optional file → Invalid (FR-015)" {
              match validateFixture "malformed-policy-present" with
              | Invalid d -> Expect.isTrue (d |> List.exists (fun x -> x.File = Policy)) "the invalid optional file fails"
              | Valid _ -> failtest "a present-but-invalid policy.yml must fail, not be ignored"
          }

          test "a FileReader Error is surfaced, never swallowed (Principle VI)" {
              let erroringReader name =
                  match name with
                  | "project.yml" -> Ok(Some minimalProject)
                  | "capabilities.yml" -> Ok(Some minimalCaps)
                  | "policy.yml" -> Error "permission denied"
                  | _ -> Ok None
              match Schema.validate (Loader.readSource (GovernedPath ".") erroringReader) with
              | Invalid _ -> ()
              | Valid _ -> failtest "a read Error on the optional policy.yml must not pass as Valid/None"
          }

          test "an absent (Ok None) optional file with the same reader → Valid (contrast)" {
              let absentReader name =
                  match name with
                  | "project.yml" -> Ok(Some minimalProject)
                  | "capabilities.yml" -> Ok(Some minimalCaps)
                  | _ -> Ok None
              match Schema.validate (Loader.readSource (GovernedPath ".") absentReader) with
              | Valid f -> Expect.isNone f.Policy "absent optional → None"
              | Invalid d -> failtestf "expected Valid, got %A" d
          }

          test "host-path independence: same content under two absolute parents → identical Validation (I3)" {
              let p1 = copyFixtureToTemp "valid-complete"
              let p2 = copyFixtureToTemp "valid-complete"
              try
                  Expect.notEqual p1 p2 "two distinct absolute parents"
                  Expect.equal (Loader.loadAndValidate p1) (Loader.loadAndValidate p2) "no absolute host path leaks into the facts"
              finally
                  for p in [ p1; p2 ] do
                      try Directory.Delete(p, true) with _ -> ()
          } ]
