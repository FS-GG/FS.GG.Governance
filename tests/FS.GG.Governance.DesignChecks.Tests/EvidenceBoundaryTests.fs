module FS.GG.Governance.DesignChecks.Tests.EvidenceBoundaryTests

open System
open System.Diagnostics
open System.IO
open Expecto
open FS.GG.Governance.DesignChecks.EvidenceBoundary

let private real kind observation =
    { Subject = string kind; Kind = kind; Provenance = Real; Command = "dotnet --version"; ExitCode = 0; SourceDigest = "abc123"; Fresh = true; Observation = observation }

let private clean =
    { RequiresProductionJourney = true
      RequiresObservedOutcome = true
      Evidence = [ real SemanticRegression ObservedOutcome; real BoundaryFixture ObservedOutcome; real GoldenOrSchema ObservedOutcome; real ProductionJourney ObservedOutcome ]
      GeneratedArtifacts = [ { Path = "contracts/generated.json"; Source = Some "contracts/source.yml"; RegenerationDeterministic = true; Consumer = Some "fsgg-governance route"; HasGoldenOrSchema = true } ]
      Mitigations = [ { Claim = "sound is never requested"; ProducerClasses = [ "native"; "fallback" ]; ReintroducedByMutation = [ "native"; "fallback" ] } ]
      Render = Some { Fixture = "tests/render-evidence.fsx"; Executed = true; ByteReproducible = false; SemanticReceiptStable = true } }

let private codes request = evaluate request |> List.map _.Code

[<Tests>]
let tests =
    testList "EvidenceBoundary" [
        test "real complete boundary evidence passes" { Expect.isEmpty (evaluate clean) "all required evidence is independently complete" }
        test "synthetic records are disclosed support but cannot satisfy real obligations" {
            let request = { clean with Evidence = clean.Evidence |> List.map (fun item -> { item with Provenance = Synthetic "test boundary" }) }
            Expect.contains (codes request) "evidence.real-boundary-required" "every required kind needs real evidence" }
        test "dispatch only and degraded only cannot satisfy observed outcome" {
            for observation in [ DispatchOnly; Degraded ] do
                let request = { clean with Evidence = clean.Evidence |> List.map (fun item -> { item with Observation = observation }) }
                Expect.contains (codes request) "evidence.observed-outcome-missing" (string observation) }
        test "explicit malformed nonzero result is a valid safe-failure receipt" {
            let request = { clean with Evidence = { real BoundaryFixture MalformedInput with ExitCode = 3 } :: clean.Evidence }
            Expect.isEmpty (evaluate request) "the malformed input is explicitly and truthfully represented" }
        test "unknown partial stale and failed-command controls fail closed" {
            let controls = [ UnknownInput, 0, true; PartialWrite, 0, true; ObservedOutcome, 0, false; ObservedOutcome, 1, true ]
            for control in controls do
                let observation, exitCode, fresh = control
                let request = { clean with Evidence = { real BoundaryFixture observation with ExitCode = exitCode; Fresh = fresh } :: clean.Evidence }
                Expect.isNonEmpty (evaluate request) (string observation) }
        test "generated schema source regeneration consumer and compatibility mutations red" {
            let mutations =
                [ { clean.GeneratedArtifacts.Head with Source = None }, "evidence.generated-source-missing"
                  { clean.GeneratedArtifacts.Head with RegenerationDeterministic = false }, "evidence.generated-regeneration-nondeterministic"
                  { clean.GeneratedArtifacts.Head with Consumer = None }, "evidence.generated-consumer-missing"
                  { clean.GeneratedArtifacts.Head with HasGoldenOrSchema = false }, "evidence.generated-compatibility-missing" ]
            for artifact, expected in mutations do
                Expect.contains (codes { clean with GeneratedArtifacts = [ artifact ] }) expected expected }
        test "each producer inventory mutation has teeth" {
            let empty = { clean.Mitigations.Head with ProducerClasses = [] }
            let incomplete = { clean.Mitigations.Head with ReintroducedByMutation = [ "native" ] }
            Expect.contains (codes { clean with Mitigations = [ empty ] }) "evidence.producer-inventory-empty" "empty inventory is not a mitigation"
            Expect.contains (codes { clean with Mitigations = [ incomplete ] }) "evidence.producer-mutation-incomplete" "each producer mutation has teeth" }
        test "findings identify the fact and one bounded correction" {
            let finding = evaluate { clean with GeneratedArtifacts = [ { clean.GeneratedArtifacts.Head with Consumer = None } ] } |> List.exactlyOne
            Expect.equal (finding.Subject, finding.Correction) ("contracts/generated.json", "name and execute a consuming route") "consumer diagnostic is actionable" }
        test "render execution and semantic receipt mutations red" {
            let notExecuted = { clean.Render.Value with Executed = false }
            let unstable = { clean.Render.Value with ByteReproducible = false; SemanticReceiptStable = false }
            Expect.contains (codes { clean with Render = Some notExecuted }) "evidence.render-not-executed" "fixture must run"
            Expect.contains (codes { clean with Render = Some unstable }) "evidence.render-receipt-unstable" "non-byte-stable fixture needs stable receipt" }
        test "real process render fixture records a stable semantic receipt" {
            let psi = ProcessStartInfo("dotnet", RedirectStandardOutput = true, UseShellExecute = false)
            psi.ArgumentList.Add "--version"
            use child = Process.Start psi |> Option.ofObj |> Option.defaultWith (fun () -> failtest "dotnet process did not start")
            let output = child.StandardOutput.ReadToEnd().Trim()
            child.WaitForExit()
            let root = Path.Combine(Path.GetTempPath(), "fsgg-evidence-render-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory root |> ignore
            try
                let receipt = Path.Combine(root, "semantic-receipt.txt")
                File.WriteAllText(receipt, sprintf "exit=%d;has-version=%b" child.ExitCode (not (String.IsNullOrWhiteSpace output)))
                Expect.equal (File.ReadAllText receipt) "exit=0;has-version=true" "real process output yields a deterministic semantic receipt"
            finally Directory.Delete(root, true) }
    ]
