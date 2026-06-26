module FS.GG.Governance.VerifyCommand.Tests.PersistenceEdgeTests

open System.IO
open Expecto
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.VerifyCommand
open FS.GG.Governance.VerifyCommand.Tests.Support

// T029 (US3) — a failed/interrupted artifact write ⇒ `ToolError` (exit 4) and NO partial verify.json left
// behind (the writer is atomic temp+rename), DISTINCT from a Blocked verdict.

let private srcScope = Loop.ExplicitPaths [ gp "src/Lib/Thing.fs" ]

[<Tests>]
let tests =
    testList
        "PersistenceEdge (US3)"
        [ test "a failing ArtifactWriter ⇒ ToolError (exit 4), no artifact recorded, distinct from Blocked" {
              let cap = newCapture ()
              let req = requestForProfile srcScope Loop.Text Strict
              let ports = fakePortsFailingWrites validCatalog gitSrcChange cap (Set.ofList [ req.VerifyOut ])
              let model = Interpreter.run ports req

              Expect.equal model.Exit Loop.ToolError "write failure ⇒ ToolError"
              Expect.equal (Loop.exitCode model.Exit) 4 "exit 4"
              Expect.notEqual model.Exit Loop.Blocked "ToolError is distinct from Blocked"
              Expect.isNone (writtenVerify cap) "no partial artifact recorded on a failed write"
          }

          test "a tool-error diagnostic is tagged and carries no fabricated passing verdict" {
              let cap = newCapture ()
              let req = requestForProfile srcScope Loop.Text Standard
              let ports = fakePortsFailingWrites validCatalog gitSrcChange cap (Set.ofList [ req.VerifyOut ])
              let model = Interpreter.run ports req

              Expect.isNonEmpty model.Diagnostics "a diagnostic is recorded"
              Expect.isTrue (model.Diagnostics |> List.forall (fun d -> d.Category = Loop.ToolError)) "tagged ToolError" } ]

// 066 US3 (closes 065 T009/T024): the NO-DECLARATION `verify.json` byte-identity golden. 065 changed
// `fsgg verify` (it now emits a `releaseReadiness` block) — but ONLY when a `.fsgg/release.yml` declaration
// is present. With NO declaration the bytes are unchanged, so the golden FROZEN from the pre-wiring anchor
// `5a0cb28` must still match `main` — proving the 065 wiring left the no-declaration path untouched (no
// schema bump, no readiness block). This test runs the REAL `fsgg verify` host over the fixed fixture (which
// has NO release.yml) and asserts byte-equality.

let private parseOrFail argv =
    match Loop.parse argv with
    | Ok r -> r
    | Error e -> failtestf "parse failed: %A" e

let private copyGoldenFixture (dst: string) : unit =
    let src = Path.Combine(repoRoot, "tests", "golden-fixture")

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
        [ test "no-declaration verify.json byte-identical to the frozen pre-wiring golden (5a0cb28)" {
              let tmp = Path.Combine(Path.GetTempPath(), "fsgg-golden-verify-" + System.Guid.NewGuid().ToString("N"))
              Directory.CreateDirectory tmp |> ignore

              try
                  copyGoldenFixture tmp
                  let req = parseOrFail [ "verify"; "--repo"; tmp; "--paths"; "src/Lib/Thing.fs" ]
                  let model = Interpreter.run { Interpreter.realPorts req.Repo with Out = ignore } req
                  Expect.equal model.Exit Loop.Success "verify exits 0 over the fixed fixture"
                  let produced = File.ReadAllText req.VerifyOut
                  Expect.isFalse (produced.Contains "releaseReadiness") "no releaseReadiness block without a declaration"

                  let golden =
                      File.ReadAllText(
                          Path.Combine(repoRoot, "tests", "FS.GG.Governance.VerifyCommand.Tests", "goldens", "verify.no-declaration.json")
                      )

                  Expect.equal produced golden "no-declaration verify.json byte-identical to the frozen 5a0cb28 golden"
              finally
                  try
                      Directory.Delete(tmp, true)
                  with _ ->
                      ()
          } ]
