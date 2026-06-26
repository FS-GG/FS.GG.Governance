module FS.GG.Governance.ReleaseCommand.Tests.FailureTests

open System.IO
open Expecto
open FS.GG.Governance.ReleaseDeclaration
open FS.GG.Governance.ReleaseCommand
open FS.GG.Governance.ReleaseCommand.Tests.Support

// Fail-safe diagnostics distinguish bad INPUT (exit 3) from bad ARGV (exit 2) from a TOOL defect (exit 4)
// — in both message and exit code (Constitution VI, FR-010/FR-011, SC-005). Never a fabricated pass.

let private runText repo =
    let ports = portsWith repo (fun _ _ -> Ok()) ignore

    let request =
        { Loop.Repo = repo
          Loop.Format = Loop.Text
          Loop.ReleaseOut = Path.Combine(repo, "release.json")
          Loop.AttestationOut = Path.Combine(repo, "attestation.json") }

    Interpreter.run ports request

let private m0 () =
    Loop.init
        { Loop.Repo = "."
          Loop.Format = Loop.Text
          Loop.ReleaseOut = "release.json"
          Loop.AttestationOut = "attestation.json" }
    |> fst

[<Tests>]
let tests =
    testList
        "Failure"
        [ test "DeclarationLoaded(Error) ⇒ InputUnavailable, no sense/write emitted (US3.1)" {
              let errResult: Result<Declaration.ReleaseDeclaration, Declaration.DeclError> =
                  Error { Reason = "no declaration" }

              let model, effects = Loop.update (Loop.DeclarationLoaded errResult) (m0 ())
              Expect.equal model.Exit Loop.InputUnavailable "input-unavailable (exit 3)"
              Expect.equal (Loop.exitCode model.Exit) 3 "exit code 3"
              Expect.isEmpty effects "no SenseRelease/WriteArtifact after a declaration failure"
              Expect.isNonEmpty model.Diagnostics "an actionable diagnostic is recorded"
          }

          test "an absent .fsgg/release.yml reaches exit 3 via Interpreter.run" {
              // A temp repo with sources but NO .fsgg/release.yml.
              withTempDir (fun repo ->
                  writeMetSources repo
                  let model = runText repo
                  Expect.equal model.Exit Loop.InputUnavailable "absent declaration → InputUnavailable"
                  Expect.equal (Loop.exitCode model.Exit) 3 "exit 3")
          }

          test "a malformed .fsgg/release.yml reaches exit 3 via Interpreter.run" {
              withTempRepo "this: is: not: a: valid: release: declaration\n- x\n" writeMetSources (fun repo ->
                  let model = runText repo
                  Expect.equal model.Exit Loop.InputUnavailable "malformed declaration → InputUnavailable"
                  Expect.equal (Loop.exitCode model.Exit) 3 "exit 3 (not 1, not 4)")
          }

          test "an unwritable --out (faked Write error) ⇒ ToolError, distinct from InputUnavailable" {
              withTempRepo releaseYmlAllBlocking writeMetSources (fun repo ->
                  let outPath = Path.Combine(repo, "release.json")
                  let ports = portsWith repo (fun _ _ -> Error "permission denied") ignore

                  let request =
                      { Loop.Repo = repo
                        Loop.Format = Loop.Json
                        Loop.ReleaseOut = outPath
                        Loop.AttestationOut = outPath + ".attestation.json" }

                  let model = Interpreter.run ports request
                  Expect.equal model.Exit Loop.ToolError "write failure → ToolError (exit 4)"
                  Expect.notEqual model.Exit Loop.InputUnavailable "distinct from input-unavailable")
          }

          test "bad argv ⇒ UsageError' (exit 2)" {
              match Loop.parse [ "--repo" ] with
              | Error _ -> Expect.equal (Loop.exitCode Loop.UsageError') 2 "usage exit 2"
              | Ok _ -> failtest "expected a usage error"
          } ]
