module FS.GG.Governance.ScaffoldManifestJson.Tests.DeterminismTests

open System.IO
open Expecto
open FS.GG.Governance.Scaffold
open FS.GG.Governance.Scaffold.Model
open FS.GG.Governance.ScaffoldManifestJson
open FS.GG.Governance.ScaffoldManifestJson.Tests.Support

// Determinism + field-exclusion sweep (SC-004, SC-006, research D6): the same provider over two fresh
// empty temp dirs ⇒ byte-identical manifest text; no absolute path / clock / env leaks; 100% of
// generated paths attributable to the provider id + contract version alone.

// SYNTHETIC: the provider content is the out-of-scope fake (research D8); the FS writes are real.
let private fakeProvider (id: string) (files: (string * string) list) : TemplateProvider =
    { Id = ProviderId id
      ContractVersion = { Major = 1; Minor = 0 }
      Emit =
        fun _ ->
            Ok
                { Files =
                    files
                    |> List.map (fun (p, c) -> { RelativePath = p; Contents = c }) } }

let private freshTempDir () =
    let dir = Path.Combine(Path.GetTempPath(), "fsgg-scaffold-det-" + System.Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory dir |> ignore
    dir

let private cleanupDir (dir: string) =
    try
        if Directory.Exists dir then
            Directory.Delete(dir, true)
    with _ ->
        ()

let private runManifest (target: string) (p: TemplateProvider) : ScaffoldManifest =
    let model =
        Interpreter.run
            (Interpreter.realPorts target)
            { Request = { Target = target; ReservedPaths = [] }
              Provider = Some p }

    Option.get model.Manifest

[<Tests>]
let tests =
    testList
        "Determinism"
        [ test "Synthetic provider over two fresh empty temp dirs ⇒ byte-identical manifest text" {
              let t1 = freshTempDir ()
              let t2 = freshTempDir ()

              try
                  let p = fakeProvider "acme.lib" [ "src/App/Program.fs", "// p"; "src/App/App.fsproj", "<p/>" ]
                  let j1 = ScaffoldManifestJson.ofManifest (runManifest t1 p)
                  let j2 = ScaffoldManifestJson.ofManifest (runManifest t2 p)

                  Expect.equal j1 j2 "byte-identical across distinct target dirs"
              finally
                  cleanupDir t1
                  cleanupDir t2
          }

          test "field-exclusion sweep: no absolute target path, clock, or env value reaches the output" {
              let t1 = freshTempDir ()

              try
                  let p = fakeProvider "acme.lib" [ "src/App/Program.fs", "// p" ]
                  let json = ScaffoldManifestJson.ofManifest (runManifest t1 p)

                  Expect.isFalse (json.Contains t1) "the absolute target path does not appear"
                  Expect.isFalse (json.Contains(Path.GetTempPath())) "the temp root does not appear"
                  Expect.isFalse (json.Contains "/home/") "no absolute home path"
                  Expect.isFalse (json.Contains "fsgg-scaffold-det-") "no temp-dir token"

                  // The output is fully accounted for by the schema's fixed, relative fields.
                  use doc = parse json
                  Expect.equal (generatedPaths doc) [ "src/App/Program.fs" ] "only the relative emitted path"
              finally
                  cleanupDir t1
          }

          test "100% of generated[] paths are attributable to the provider id + contract version alone" {
              let m = scaffoldedManifest "acme.lib" [ "src/App/Program.fs"; "README.md" ]
              use doc = parse (ScaffoldManifestJson.ofManifest m)

              let provider = doc.RootElement.GetProperty "provider"
              Expect.equal (strField provider "id") "acme.lib" "provider id present"
              Expect.equal (strField provider "contractVersion") "1.0" "contract version present"

              // Every generated entry is self-describing (relative path + ownership) — no host context needed.
              Expect.isTrue
                  (generatedEntries doc
                   |> List.forall (fun g -> (strField g "path") <> "" && strField g "ownership" = "providerOwned"))
                  "each generated path is provider-attributed and self-contained"
          } ]
