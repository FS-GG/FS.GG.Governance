module FS.GG.Governance.ReleaseCommand.Tests.PersistenceEdgeTests

open System.IO
open Expecto
open FS.GG.Governance.ReleaseCommand
open FS.GG.Governance.ReleaseCommand.Tests.Support

// A failed/interrupted `Write` leaves no partial `release.json` (FR-012/US2.3) using a faked failing
// `ArtifactWriter`: the command maps the failure to `ToolError` and no artifact appears on disk.

[<Tests>]
let tests =
    testList
        "PersistenceEdge"
        [ test "a failing ArtifactWriter ⇒ ToolError and no release.json on disk" {
              withTempRepo releaseYmlAllBlocking writeMetSources (fun repo ->
                  let outPath = Path.Combine(repo, "release.json")
                  let ports = portsWith repo (fun _ _ -> Error "simulated write failure") ignore

                  let request =
                      { Loop.Repo = repo
                        Loop.Format = Loop.TextAndJson
                        Loop.ReleaseOut = outPath
                        Loop.AttestationOut = outPath + ".attestation.json" }

                  let model = Interpreter.run ports request
                  Expect.equal model.Exit Loop.ToolError "write failure → ToolError (exit 4)"
                  Expect.equal (Loop.exitCode model.Exit) 4 "exit code 4"
                  Expect.isFalse (File.Exists outPath) "no partial artifact left behind")
          }

          test "the real atomic writer leaves no partial file when the destination cannot be created" {
              withTempRepo releaseYmlAllBlocking writeMetSources (fun repo ->
                  // A regular file stands where the output's parent directory would need to be ⇒ the atomic
                  // temp+rename writer fails cleanly (no partial release.json).
                  let blocker = Path.Combine(repo, "blocker")
                  File.WriteAllText(blocker, "x")
                  let outPath = Path.Combine(blocker, "nested", "release.json")
                  let ports = { Interpreter.realPorts repo with Out = ignore }

                  let request =
                      { Loop.Repo = repo
                        Loop.Format = Loop.Json
                        Loop.ReleaseOut = outPath
                        Loop.AttestationOut = outPath + ".attestation.json" }

                  let model = Interpreter.run ports request
                  Expect.equal model.Exit Loop.ToolError "unwritable destination → ToolError"
                  Expect.isFalse (File.Exists outPath) "no partial artifact")
          } ]

// 066 US3 (closes 065 T024): the empty-additive `release.json` v2 byte-identity golden. Unlike route/ship/
// no-decl-verify, `release.json` v2 is INTRODUCED by 065 — there is no honest pre-wiring v2 to freeze — so
// this golden is the F26-blessed empty-additive contract captured from CURRENT code, pinning the additive
// `fsgg.release/v2` shape (empty packageEvidence/versionPolicy, null-free attestation ref) going forward.
// The fixture declares NO packable projects, so `fsgg release` performs no `dotnet pack` here (zero Pack runs).

let private copyDirInto (src: string) (dst: string) : unit =
    for f in Directory.GetFiles(src, "*", SearchOption.AllDirectories) do
        let target = Path.Combine(dst, Path.GetRelativePath(src, f))

        Path.GetDirectoryName target
        |> Option.ofObj
        |> Option.iter (fun d -> Directory.CreateDirectory d |> ignore)

        File.Copy(f, target, true)

[<Tests>]
let goldenTests =
    testList
        "ByteIdentityGolden"
        [ test "empty-v2 release.json byte-identical to the F26-blessed golden; zero Pack runs" {
              let tmp = Path.Combine(Path.GetTempPath(), "fsgg-golden-release-" + System.Guid.NewGuid().ToString("N"))
              Directory.CreateDirectory tmp |> ignore

              try
                  copyDirInto (Path.Combine(repoRoot, "tests", "golden-fixture-release")) tmp
                  let outPath = Path.Combine(tmp, "release.json")

                  let request =
                      { Loop.Repo = tmp
                        Loop.Format = Loop.Json
                        Loop.ReleaseOut = outPath
                        Loop.AttestationOut = Path.Combine(tmp, "readiness", "attestation.json") }

                  // Pin the environment class so this byte-identity golden is HERMETIC: `realPorts` senses
                  // it from the ambient `CI` env var (Local off-CI, Ci on the runner), which would otherwise
                  // leak into the attestation `env=` segment and break byte-identity on CI. The golden was
                  // blessed with `env=…local`, so pin Local. (Surfaced by 102 when the suite first ran in CI.)
                  let ports =
                      { Interpreter.realPorts tmp with
                          Out = ignore
                          SenseEnvironment = fun () -> FS.GG.Governance.Config.Model.EnvironmentClass.Local }

                  let model = Interpreter.run ports request
                  Expect.equal model.Exit Loop.Success "the empty-additive product releases clean"

                  // No packable projects ⇒ no `dotnet pack` is run here (the empty-v2 contract).
                  Expect.isTrue
                      (model.PackEvidence |> Option.map (fun p -> p.NoPackableProjects) |> Option.defaultValue false)
                      "no packable projects (zero Pack runs)"

                  Expect.isEmpty
                      (model.PackEvidence |> Option.map (fun p -> p.Runs) |> Option.defaultValue [])
                      "zero recorded Pack runs"

                  let produced = File.ReadAllText outPath

                  let golden =
                      File.ReadAllText(
                          Path.Combine(repoRoot, "tests", "FS.GG.Governance.ReleaseCommand.Tests", "goldens", "release.empty-v2.json")
                      )

                  Expect.equal produced golden "empty-v2 release.json byte-identical to the F26-blessed golden"
              finally
                  try
                      Directory.Delete(tmp, true)
                  with _ ->
                      ()
          } ]
