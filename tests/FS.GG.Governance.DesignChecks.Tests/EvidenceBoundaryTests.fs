module FS.GG.Governance.DesignChecks.Tests.EvidenceBoundaryTests

open System
open System.Diagnostics
open System.IO
open Expecto
open FS.GG.Governance.DesignChecks.EvidenceBoundary

let private real kind observation =
    { Kind = kind; Provenance = Real; Command = "dotnet --version"; ExitCode = 0; SourceDigest = "abc123"; Fresh = true; Observation = observation }

let private clean =
    { RequiresProductionJourney = true
      RequiresObservedOutcome = true
      Evidence = [ real SemanticRegression ObservedOutcome; real BoundaryFixture ObservedOutcome; real GoldenOrSchema ObservedOutcome; real ProductionJourney ObservedOutcome ]
      GeneratedArtifacts = [ { Source = Some "contracts/source.yml"; RegenerationDeterministic = true; Consumer = Some "fsgg-governance"; HasGoldenOrSchema = true } ]
      Mitigations = [ { ProducerClasses = [ "native"; "fallback" ]; ReintroducedByMutation = [ "native"; "fallback" ] } ]
      Render = Some { Executed = true; ByteReproducible = false; SemanticReceiptStable = true } }

let private codes request = evaluate request |> List.map _.Code

[<Tests>]
let tests =
    testList "EvidenceBoundary" [
        test "real complete boundary evidence passes" { Expect.isEmpty (evaluate clean) "all required evidence is independently complete" }
        test "emission only cannot satisfy observed outcome" {
            let request = { clean with Evidence = clean.Evidence |> List.map (fun item -> if item.Kind = BoundaryFixture then { item with Observation = DispatchOnly } else item) }
            Expect.contains (codes request) "evidence.dispatch-is-not-outcome" "effect emission is not boundary result" }
        test "malformed stale partial and unknown controls fail closed" {
            let controls = [ MalformedInput; UnknownInput; PartialWrite ]
            for control in controls do
                let request = { clean with Evidence = { real BoundaryFixture control with Fresh = false } :: clean.Evidence }
                Expect.isNonEmpty (evaluate request) (string control) }
        test "consumerless generated artifact and incomplete producer mutation red" {
            let request = { clean with GeneratedArtifacts = [ { clean.GeneratedArtifacts.Head with Consumer = None } ]; Mitigations = [ { ProducerClasses = [ "native"; "fallback" ]; ReintroducedByMutation = [ "native" ] } ] }
            Expect.contains (codes request) "evidence.generated-consumer-missing" "consumer is a required reachability fact"
            Expect.contains (codes request) "evidence.producer-mutation-incomplete" "each producer mutation has teeth" }
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
