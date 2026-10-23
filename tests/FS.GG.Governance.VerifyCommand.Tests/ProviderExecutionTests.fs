module FS.GG.Governance.VerifyCommand.Tests.ProviderExecutionTests

// GOV-423-C2 execution join: pure evidence acceptance, the evidence edge over a real filesystem, and the actual
// Verify pipeline driven to a verdict with a fixture whose `run` writes the declared handoff evidence. The fixture
// launcher here is in-process (SYNTHETIC process facts, real files); the native held-cwd journey with a real child
// process is the opt-in `ProviderExecution Native` list below.

open System
open System.IO
open System.Security.Cryptography
open System.Text
open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.GateExecution.Model
open FS.GG.Governance.VerifyCommand
open FS.GG.Governance.VerifyCommand.Tests.Support
open FS.GG.Governance.VerifyCommand.Tests.ProviderPlanningTests

module CB = FS.GG.Governance.Config.CapabilityBindings

let sha256 (bytes: byte array) =
    "sha256:" + Convert.ToHexString(SHA256.HashData bytes).ToLowerInvariant()

let run: Loop.ProviderGateRun =
    { Gate = GateId "build:build"
      Required = true
      WorkingDirectory = gp "."
      Command =
        { Executable = Executable "/fixture/run"
          Arguments = [ Argument "run" ]
          WorkingDirectory = WorkingDirectory "/fixture"
          Environment = { Added = []; Changed = []; Removed = [] }
          Timeout = TimeoutLimit 5
          CapturedOutput = NoCapturedOutput }
      Evidence = [ handoffEvidence ] }

let produced text sources : Loop.ProviderEvidenceObservation list =
    [ { Evidence = handoffEvidence; State = Loop.Produced(text, sources) } ]

let launchedWith (observed: BoundedObservation) evidence : Loop.ProviderGateObservation =
    { Gate = run.Gate; Launch = Loop.Launched observed; Evidence = evidence }

let cited = [ ({ Path = gp "src/a.fs"; Digest = digestA }: FS.GG.Governance.Adapters.SddHandoff.Model.HandoffSource) ]
let ok = observedExit "build:build" (Some 0)

let refusalsOf verdict =
    match verdict with
    | Loop.ProviderAccepted -> []
    | Loop.ProviderRefused refusals -> refusals

let expectRefused (predicate: Loop.ProviderRefusal -> bool) name verdict =
    let refusals = refusalsOf verdict
    Expect.isNonEmpty refusals (name + ": refused")
    Expect.isTrue (refusals |> List.exists predicate) (sprintf "%s: expected refusal in %A" name refusals)

[<Tests>]
let acceptanceTests =
    testList "ProviderExecution acceptance" [
        testCase "launched, exit 0, EOF, released, fresh source-bound passing handoff is accepted" (fun _ ->
            Expect.equal (Loop.acceptProviderGate run (launchedWith ok (produced goodHandoff cited))) Loop.ProviderAccepted "accepted")
        testCase "not launched is refused with its reason" (fun _ ->
            let observation: Loop.ProviderGateObservation =
                { Gate = run.Gate; Launch = Loop.NotLaunched "lease refused"; Evidence = [ { Evidence = handoffEvidence; State = Loop.AbsentAfterRun } ] }
            Loop.acceptProviderGate run observation |> expectRefused (function Loop.LaunchRefused "lease refused" -> true | _ -> false) "not launched")
        testCase "a non-zero exit is refused even with passing evidence" (fun _ ->
            Loop.acceptProviderGate run (launchedWith (observedExit "build:build" (Some 1)) (produced goodHandoff cited))
            |> expectRefused (function Loop.NonZeroExit 1 -> true | _ -> false) "exit 1")
        testCase "a bounded failure without a direct exit is refused with both facts" (fun _ ->
            let timedOut = { observedExit "build:build" None with FirstFailure = Some WorkDeadlineReached; WorkDeadlineReached = true }
            let verdict = Loop.acceptProviderGate run (launchedWith timedOut (produced goodHandoff cited))
            verdict |> expectRefused (function Loop.ExecutionFailed WorkDeadlineReached -> true | _ -> false) "deadline"
            verdict |> expectRefused (function Loop.NoDirectExit -> true | _ -> false) "no exit")
        testCase "a secondary bounded failure alone is refused" (fun _ ->
            let secondary = { ok with SecondaryFailures = [ OutputLimitReached ] }
            Loop.acceptProviderGate run (launchedWith secondary (produced goodHandoff cited))
            |> expectRefused (function Loop.ExecutionFailed OutputLimitReached -> true | _ -> false) "secondary")
        testCase "streams that did not reach EOF are refused" (fun _ ->
            let pending = { ok with Stdout = { ok.Stdout with State = Pending } }
            Loop.acceptProviderGate run (launchedWith pending (produced goodHandoff cited))
            |> expectRefused (function Loop.OutputIncomplete -> true | _ -> false) "pending stream")
        testCase "retained direct resources are refused" (fun _ ->
            Loop.acceptProviderGate run (launchedWith { ok with Settlement = Retained } (produced goodHandoff cited))
            |> expectRefused (function Loop.DirectResourcesRetained -> true | _ -> false) "retained")
        testCase "evidence present before launch is stale" (fun _ ->
            Loop.acceptProviderGate run (launchedWith ok [ { Evidence = handoffEvidence; State = Loop.PresentBeforeLaunch } ])
            |> expectRefused (function Loop.EvidenceStale "log" -> true | _ -> false) "stale")
        testCase "evidence absent after the run is missing" (fun _ ->
            Loop.acceptProviderGate run (launchedWith ok [ { Evidence = handoffEvidence; State = Loop.AbsentAfterRun } ])
            |> expectRefused (function Loop.EvidenceMissing "log" -> true | _ -> false) "missing")
        testCase "unreadable evidence is refused with its reason" (fun _ ->
            Loop.acceptProviderGate run (launchedWith ok [ { Evidence = handoffEvidence; State = Loop.Unreadable "not strict UTF-8" } ])
            |> expectRefused (function Loop.EvidenceUnreadable("log", "not strict UTF-8") -> true | _ -> false) "unreadable")
        testCase "a declared binding the edge did not observe is refused" (fun _ ->
            Loop.acceptProviderGate run (launchedWith ok [])
            |> expectRefused (function Loop.EvidenceNotObserved "log" -> true | _ -> false) "unobserved")
        testCase "an observation for another gate is not this run" (fun _ ->
            Expect.equal
                (Loop.acceptProviderGate run { launchedWith ok (produced goodHandoff cited) with Gate = GateId "other:gate" })
                (Loop.ProviderRefused [ Loop.NotObserved ])
                "not observed")
        testCase "an unsupported evidence format is refused even when its bytes are a valid handoff" (fun _ ->
            let junit = { handoffEvidence with Format = { Id = "junit"; Version = "1.0.0" } }
            let other = { run with Evidence = [ junit ] }
            let observation = { launchedWith ok [] with Evidence = [ { Evidence = junit; State = Loop.Produced(goodHandoff, cited) } ] }
            Loop.acceptProviderGate other observation
            |> expectRefused (function Loop.EvidenceFormatUnsupported("log", "junit@1.0.0") -> true | _ -> false) "junit")
        testCase "a handoff format of another major is unsupported" (fun _ ->
            let v3 = { handoffEvidence with Format = { handoffFormat with Version = "3.0.0" } }
            let other = { run with Evidence = [ v3 ] }
            let observation = { launchedWith ok [] with Evidence = [ { Evidence = v3; State = Loop.Produced(goodHandoff, cited) } ] }
            Loop.acceptProviderGate other observation
            |> expectRefused (function Loop.EvidenceFormatUnsupported _ -> true | _ -> false) "major 3")
        testCase "a document of another contract major is a version mismatch" (fun _ ->
            Loop.acceptProviderGate run (launchedWith ok (produced (handoffJson "3.0.0" [ "build:build", "real" ] [ "src/a.fs", digestA ]) cited))
            |> expectRefused (function Loop.EvidenceVersionMismatch("log", "2.0.0", "3.0.0") -> true | _ -> false) "3.0.0")
        testCase "malformed evidence is refused" (fun _ ->
            Loop.acceptProviderGate run (launchedWith ok (produced "{ not json" cited))
            |> expectRefused (function Loop.EvidenceMalformed("log", _) -> true | _ -> false) "malformed")
        testCase "evidence without sources[] cannot be bound to the workspace" (fun _ ->
            let unsourced = goodHandoff.Replace(",\"sources\":[{\"path\":\"src/a.fs\",\"digest\":\"" + digestA + "\"}]", "")
            Expect.isFalse (unsourced.Contains "sources") "fixture removed sources"
            Loop.acceptProviderGate run (launchedWith ok (produced unsourced []))
            |> expectRefused (function Loop.EvidenceSourcesRejected _ -> true | _ -> false) "no sources")
        testCase "a cited source whose current bytes differ is stale/mismatched evidence" (fun _ ->
            let current = [ { cited.Head with Digest = "sha256:" + String('b', 64) } ]
            Loop.acceptProviderGate run (launchedWith ok (produced goodHandoff current))
            |> expectRefused (function Loop.EvidenceSourcesRejected("log", reasons) -> reasons |> List.exists (fun r -> r.Contains "digest") | _ -> false) "digest")
        testCase "a cited source Governance could not read is uncaptured" (fun _ ->
            Loop.acceptProviderGate run (launchedWith ok (produced goodHandoff []))
            |> expectRefused (function Loop.EvidenceSourcesRejected("log", reasons) -> reasons |> List.exists (fun r -> r.Contains "not captured") | _ -> false) "uncaptured")
        testCase "a failed, pending or synthetic node is not passing evidence" (fun _ ->
            for state in [ "failed"; "pending"; "synthetic" ] do
                Loop.acceptProviderGate run (launchedWith ok (produced (handoffJson "2.0.0" [ "build:build", "real"; "extra", state ] [ "src/a.fs", digestA ]) cited))
                |> expectRefused (function Loop.EvidenceNotPassing("log", _) -> true | _ -> false) state)
        testCase "a document with no real node is not passing evidence" (fun _ ->
            Loop.acceptProviderGate run (launchedWith ok (produced (handoffJson "2.0.0" [ "build:build", "skipped" ] [ "src/a.fs", digestA ]) cited))
            |> expectRefused (function Loop.EvidenceNotPassing("log", reasons) -> reasons |> List.exists (fun r -> r.Contains "no real") | _ -> false) "no real")
        testCase "a blocking readiness projection is not passing evidence" (fun _ ->
            let blocking =
                goodHandoff.Replace(
                    "\"governedReferences\"",
                    "\"readiness\":{\"shipDisposition\":\"blocked\",\"verificationReadiness\":\"incomplete\",\"blockingDiagnosticIds\":[\"x\"],\"counts\":{},\"perViewState\":{}},\"governedReferences\"")
            Loop.acceptProviderGate run (launchedWith ok (produced blocking cited))
            |> expectRefused (function Loop.EvidenceNotPassing _ -> true | _ -> false) "readiness")
        testCase "every refusal has actionable text" (fun _ ->
            let all =
                [ Loop.NotObserved; Loop.LaunchRefused "r"; Loop.ExecutionFailed AlreadyRun; Loop.NoDirectExit; Loop.NonZeroExit 2
                  Loop.OutputIncomplete; Loop.DirectResourcesRetained; Loop.EvidenceNotObserved "e"; Loop.EvidenceStale "e"
                  Loop.EvidenceMissing "e"; Loop.EvidenceUnreadable("e", "r"); Loop.EvidenceFormatUnsupported("e", "f")
                  Loop.EvidenceMalformed("e", "r"); Loop.EvidenceVersionMismatch("e", "2", "3")
                  Loop.EvidenceSourcesRejected("e", [ "r" ]); Loop.EvidenceNotPassing("e", [ "r" ]) ]
            let texts = all |> List.map Loop.describeProviderRefusal
            Expect.allEqual (texts |> List.map String.IsNullOrWhiteSpace) false "non-empty"
            Expect.equal (List.distinct texts).Length texts.Length "distinct")
    ]

// ── The evidence edge over a real filesystem, with an in-process fixture launcher that writes the evidence. ──

let withRoot (body: string -> unit) =
    let root = Path.Combine(Path.GetTempPath(), "fsgg-provider-exec-" + Guid.NewGuid().ToString "N")
    Directory.CreateDirectory(Path.Combine(root, "src")) |> ignore
    File.WriteAllText(Path.Combine(root, "src", "a.fs"), "module A\n")
    try
        body (Path.GetFullPath root)
    finally
        try Directory.Delete(root, true) with _ -> ()

let sourceDigest root = sha256 (File.ReadAllBytes(Path.Combine(root, "src", "a.fs")))

let outputPath root = Path.Combine(root, "artifacts", "log.json")

/// The fixture's `run`: writes the declared handoff citing the current bytes of src/a.fs, then exits 0.
let writingLauncher (root: string) (calls: int ref) (content: string -> string) : Loop.ProviderGateRun -> Loop.ProviderLaunch =
    fun run ->
        calls.Value <- calls.Value + 1
        Directory.CreateDirectory(Path.Combine(root, "artifacts")) |> ignore
        File.WriteAllText(outputPath root, content (sourceDigest root))
        Loop.Launched(observedExit (gateIdValue run.Gate) (Some 0))

let handoffCiting digest = handoffJson "2.0.0" [ "build:build", "real" ] [ "src/a.fs", digest ]

let observeWith root (launcher: Loop.ProviderGateRun -> Loop.ProviderLaunch) =
    let observation = ProviderExecution.executeWith root launcher [ run ] |> List.exactlyOne
    observation, Loop.acceptProviderGate run observation

[<Tests>]
let edgeTests =
    testSequenced <| testList "ProviderExecution evidence edge" [
        testCase "fixture run writes the declared handoff; Governance digests the cited source and accepts it" (fun _ ->
            withRoot (fun root ->
                let calls = ref 0
                let observation, verdict = observeWith root (writingLauncher root calls handoffCiting)
                Expect.equal calls.Value 1 "launched once"
                match observation.Evidence with
                | [ { State = Loop.Produced(_, [ captured ]) } ] ->
                    Expect.equal captured.Digest (sourceDigest root) "Governance's own digest of the cited source"
                | other -> failtestf "unexpected evidence %A" other
                Expect.equal verdict Loop.ProviderAccepted "accepted"))
        testCase "an output present before launch is not launched, not overwritten and refused as stale" (fun _ ->
            withRoot (fun root ->
                Directory.CreateDirectory(Path.Combine(root, "artifacts")) |> ignore
                File.WriteAllText(outputPath root, handoffCiting (sourceDigest root))
                let before = File.ReadAllBytes(outputPath root)
                let calls = ref 0
                let observation, verdict = observeWith root (writingLauncher root calls (fun _ -> "overwritten"))
                Expect.equal calls.Value 0 "nothing started"
                Expect.equal (File.ReadAllBytes(outputPath root)) before "stale output untouched"
                match observation.Launch with
                | Loop.NotLaunched _ -> ()
                | other -> failtestf "unexpected launch %A" other
                verdict |> expectRefused (function Loop.EvidenceStale "log" -> true | _ -> false) "stale"))
        testCase "a run that writes nothing is refused as missing evidence" (fun _ ->
            withRoot (fun root ->
                let launcher (run: Loop.ProviderGateRun) = Loop.Launched(observedExit (gateIdValue run.Gate) (Some 0))
                let _, verdict = observeWith root launcher
                verdict |> expectRefused (function Loop.EvidenceMissing "log" -> true | _ -> false) "missing"))
        testCase "evidence citing bytes that are not the workspace's current bytes is refused" (fun _ ->
            withRoot (fun root ->
                let calls = ref 0
                let _, verdict = observeWith root (writingLauncher root calls (fun _ -> handoffCiting digestA))
                verdict |> expectRefused (function Loop.EvidenceSourcesRejected _ -> true | _ -> false) "mismatch"))
        testCase "a source changed after the run wrote its evidence is refused (stale evidence)" (fun _ ->
            withRoot (fun root ->
                let calls = ref 0
                let launcher (run: Loop.ProviderGateRun) =
                    let launched = writingLauncher root calls handoffCiting run
                    File.AppendAllText(Path.Combine(root, "src", "a.fs"), "// later edit\n")
                    launched
                let _, verdict = observeWith root launcher
                verdict |> expectRefused (function Loop.EvidenceSourcesRejected _ -> true | _ -> false) "stale source"))
        testCase "an output that is a link is never read" (fun _ ->
            withRoot (fun root ->
                let elsewhere = Path.Combine(root, "elsewhere.json")
                let launcher (run: Loop.ProviderGateRun) =
                    File.WriteAllText(elsewhere, handoffCiting (sourceDigest root))
                    Directory.CreateDirectory(Path.Combine(root, "artifacts")) |> ignore
                    File.CreateSymbolicLink(outputPath root, elsewhere) |> ignore
                    Loop.Launched(observedExit (gateIdValue run.Gate) (Some 0))
                let _, verdict = observeWith root launcher
                verdict |> expectRefused (function Loop.EvidenceUnreadable _ -> true | _ -> false) "link"))
        testCase "an output larger than the bound or not strict UTF-8 is unreadable" (fun _ ->
            withRoot (fun root ->
                let launcher (bytes: byte array) (run: Loop.ProviderGateRun) =
                    Directory.CreateDirectory(Path.Combine(root, "artifacts")) |> ignore
                    File.WriteAllBytes(outputPath root, bytes)
                    Loop.Launched(observedExit (gateIdValue run.Gate) (Some 0))
                let _, oversized = observeWith root (launcher (Array.create (ProviderExecution.evidenceByteLimit + 1) (byte 'a')))
                oversized |> expectRefused (function Loop.EvidenceUnreadable _ -> true | _ -> false) "oversized"
                File.Delete(outputPath root)
                let _, invalid = observeWith root (launcher [| 0xC3uy; 0x28uy |])
                invalid |> expectRefused (function Loop.EvidenceUnreadable("log", "not strict UTF-8") -> true | _ -> false) "utf8"))
        testCase "a launcher fault is contained as not launched" (fun _ ->
            withRoot (fun root ->
                let _, verdict = observeWith root (fun _ -> failwith "synthetic launcher fault")
                verdict |> expectRefused (function Loop.LaunchRefused reason -> reason.Contains "synthetic launcher fault" | _ -> false) "fault"))
    ]

// ── The actual Verify pipeline (Interpreter.runProvider over real Loop/acceptance and the evidence edge). ──

let providerCatalog =
    Map
        [ "governance.yml", projectYml.Replace("  - package-api\n  - workflow\n", "  - build\n")
          "capabilities.yml",
          yaml
              """
schemaVersion: 2
domains:
  - build
pathMap:
  - glob: "src/**"
    capability: build
checks:
  - id: build
    domain: build
    command: dotnet-build
    owner: platform
    cost: cheap
    environment: local-or-ci
    maturity: block-on-ship
"""
          "policy.yml", policyYml
          "tooling.yml", toolingYml ]

let journey (root: string) (launcher: Loop.ProviderGateRun -> Loop.ProviderLaunch) =
    let cap = newCapture ()
    let ports = fakePorts providerCatalog gitSrcChange cap
    let ports = { ports with Execute = (fun _ -> failtest "provider mode never uses the legacy execution port") }
    let provider: Loop.ProviderPlanningInput =
        { ResolutionRequest = requiredHandoffResolution
          CommandContext = { context with RepoRoot = root } }
    let model =
        Interpreter.runProvider ports (ProviderExecution.executeWith root launcher) (requestFor (Loop.ExplicitPaths [ gp "src/a.fs" ]) Loop.Json) provider
    model, cap

[<Tests>]
let journeyTests =
    testSequenced <| testList "ProviderExecution Verify journey (in-process fixture)" [
        testCase "positive: the fixture writes its declared handoff and Verify passes on accepted evidence" (fun _ ->
            withRoot (fun root ->
                let calls = ref 0
                let model, cap = journey root (writingLauncher root calls handoffCiting)
                Expect.equal calls.Value 1 "the admitted command was handed to the launcher once"
                Expect.equal model.Legacy.Exit Loop.Success (sprintf "accepted: %A" model.Legacy.Diagnostics)
                Expect.equal (Loop.exitCode model.Legacy.Exit) 0 "exit 0"
                match model.Runs with
                | [ actual ] ->
                    Expect.equal actual.Command.Arguments (List.map Argument command.Arguments) "literal argv, no lexer roundtrip"
                    Expect.equal actual.Command.WorkingDirectory (WorkingDirectory(Path.Combine(root, "src"))) "binding cwd under the captured root"
                | other -> failtestf "unexpected runs %A" other
                let _, _, doc = cap.Writes |> List.exactlyOne
                Expect.stringContains doc "build:build" "verify.json projects the gate"))
        testCase "missing evidence blocks (1)" (fun _ ->
            withRoot (fun root ->
                let model, _ = journey root (fun (run: Loop.ProviderGateRun) -> Loop.Launched(observedExit (gateIdValue run.Gate) (Some 0)))
                Expect.equal (Loop.exitCode model.Legacy.Exit) 1 "blocked"))
        testCase "stale evidence blocks (1) without launching" (fun _ ->
            withRoot (fun root ->
                Directory.CreateDirectory(Path.Combine(root, "artifacts")) |> ignore
                File.WriteAllText(outputPath root, handoffCiting (sourceDigest root))
                let calls = ref 0
                let model, _ = journey root (writingLauncher root calls handoffCiting)
                Expect.equal calls.Value 0 "not launched"
                Expect.equal (Loop.exitCode model.Legacy.Exit) 1 "blocked"))
        testCase "mismatched evidence blocks (1)" (fun _ ->
            withRoot (fun root ->
                let calls = ref 0
                let model, _ = journey root (writingLauncher root calls (fun _ -> handoffCiting digestA))
                Expect.equal (Loop.exitCode model.Legacy.Exit) 1 "blocked"))
        testCase "a non-zero exit with valid evidence blocks (1)" (fun _ ->
            withRoot (fun root ->
                let calls = ref 0
                let launcher (run: Loop.ProviderGateRun) =
                    writingLauncher root calls handoffCiting run |> ignore
                    Loop.Launched(observedExit (gateIdValue run.Gate) (Some 2))
                let model, _ = journey root launcher
                Expect.equal (Loop.exitCode model.Legacy.Exit) 1 "blocked"))
        testCase "an unrouted scope refuses before any launch (3)" (fun _ ->
            withRoot (fun root ->
                let cap = newCapture ()
                let ports = fakePorts providerCatalog gitSrcChange cap
                let provider: Loop.ProviderPlanningInput = { ResolutionRequest = requiredHandoffResolution; CommandContext = { context with RepoRoot = root } }
                let model =
                    Interpreter.runProvider ports (fun _ -> failtest "nothing launches") (requestFor (Loop.ExplicitPaths [ gp "AGENTS.md" ]) Loop.Json) provider
                Expect.equal (Loop.exitCode model.Legacy.Exit) 3 "input unavailable"
                Expect.isEmpty cap.Writes "no artifact"))
    ]
