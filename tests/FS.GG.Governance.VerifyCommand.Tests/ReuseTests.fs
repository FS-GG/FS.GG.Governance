module FS.GG.Governance.VerifyCommand.Tests.ReuseTests

open System
open System.IO
open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.FreshnessSensing
open FS.GG.Governance.VerifyCommand
open FS.GG.Governance.VerifyCommand.Tests.Support

// T021 (US2) — fresh evidence is REUSED, stale evidence RECOMPUTED, via a REAL evidence-reuse store round-trip
// (a real on-disk store written under `--persist-store` and re-read by the real F046 reader; Principle V).
// Run 1 populates the store; run 2 (no change) reuses EVERY selected check (Execute invoked zero more times);
// then flipping ONE check's command version recomputes EXACTLY that check, reported stale with its category.

// A `--since` scope so the fake git port supplies a base/head Range ⇒ gates resolve their freshness facts and
// become cacheable/reusable (an unresolved gate has no candidate and is never reused).
let private srcScope = Loop.Since "HEAD~1"

let private withTmp (body: string -> 'a) : 'a =
    let dir = Path.Combine(Path.GetTempPath(), "fsgg-verify-reuse-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory dir |> ignore
    try body dir
    finally try Directory.Delete(dir, true) with _ -> ()

let private diskWriter: Interpreter.ArtifactWriter =
    fun path content ->
        try
            match Path.GetDirectoryName path with
            | null | "" -> ()
            | d -> Directory.CreateDirectory d |> ignore
            File.WriteAllText(path, content)
            Ok()
        with e -> Error e.Message

let private portsWith (sensor: FreshnessSensing.FreshnessSensor) (exec) (cap: Capture) : Interpreter.Ports =
    { Files = readerOf validCatalog
      Git = portsGit gitSrcChange
      Freshness = sensor
      Store = FreshnessSensing.realStoreReader
      Write = diskWriter
      Out = capturingSink cap
      Execute = exec
      SenseCapability = plainCapability
      RenderReport = noRichRender
      SenseEnvironment = fakeSenseEnvironment
      SenseBuilder = fakeSenseBuilder
      SenseRelease = fakeSenseRelease
      SenseSurfaces = fakeSenseSurfaces
      SenseViewCurrency = fakeSenseViewCurrency
      Handoffs = fun _ -> [] }

let private reqIn dir =
    { requestForProfile srcScope Loop.Text Standard with
        VerifyOut = Path.Combine(dir, "verify.json")
        StorePath = Path.Combine(dir, "store.json")
        PersistStore = true }

/// A sensor that changes ONLY the dotnet-build command version (so build goes stale, format stays fresh).
let private sensorBuildChanged: FreshnessSensing.FreshnessSensor =
    { fakeSensor with
        SenseCommandVersion = fun (CommandId c) -> if c = "dotnet-build" then Some(CommandVersion "cmd-CHANGED") else Some(CommandVersion "cmd-synthetic") }

[<Tests>]
let tests =
    testList
        "Reuse (US2)"
        [ test "run twice with no change ⇒ run 2 reuses every selected check (zero further executions), all fresh" {
              withTmp (fun dir ->
                  let req = reqIn dir

                  let c1 = { Calls = 0 }
                  Interpreter.run (portsWith fakeSensor (countingExecPort c1 0) (newCapture ())) req |> ignore
                  Expect.isGreaterThan c1.Calls 0 "run 1 executes the stale (no-prior-evidence) checks"

                  let cap2 = newCapture ()
                  let c2 = { Calls = 0 }
                  Interpreter.run (portsWith fakeSensor (countingExecPort c2 0) cap2) req |> ignore
                  Expect.equal c2.Calls 0 "run 2 reuses everything — no further executions"

                  let text = String.concat "\n" cap2.Emits
                  // F27 wiring (063): reuse shows as the HumanText "Cache eligibility" `reusable:` outcome.
                  Expect.stringContains text "reusable:" "run 2 reports reusable evidence"
                  Expect.isFalse (text.Contains "no prior evidence") "nothing recomputed for no-prior-evidence on run 2")
          }

          test "flipping one check's command version recomputes EXACTLY that check, reported stale with its category" {
              withTmp (fun dir ->
                  let req = reqIn dir

                  // Populate the store.
                  Interpreter.run (portsWith fakeSensor (countingExecPort { Calls = 0 } 0) (newCapture ())) req |> ignore

                  // Re-run with the build command version flipped: only build is stale.
                  let cap = newCapture ()
                  let c = { Calls = 0 }
                  Interpreter.run (portsWith sensorBuildChanged (countingExecPort c 0) cap) req |> ignore
                  Expect.equal c.Calls 1 "exactly one check (build) is recomputed"

                  let text = String.concat "\n" cap.Emits
                  // F27 wiring (063): format stays `reusable:`; build recomputes as `inputs changed (n)`.
                  Expect.stringContains text "reusable:" "format stays reusable"
                  Expect.stringContains text "inputs changed" "build is stale with its changed category") } ]
