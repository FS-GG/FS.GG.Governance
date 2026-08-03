module FS.GG.Governance.DesignChecks.Tests.EvidenceBoundaryTests

open System
open System.Diagnostics
open System.IO
open System.Security.Cryptography
open System.Text.Json
open Expecto
open FS.GG.Governance.DesignChecks.EvidenceBoundary

let private runCommand executable arguments =
    let info = ProcessStartInfo(executable, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false)
    arguments |> List.iter info.ArgumentList.Add
    use child = Process.Start info |> Option.ofObj |> Option.defaultWith (fun () -> failwithf "could not start %s" executable)
    let output = child.StandardOutput.ReadToEnd()
    let error = child.StandardError.ReadToEnd()
    child.WaitForExit()
    child.ExitCode, output.Trim(), error.Trim()

let private digest path =
    use stream = File.OpenRead path
    SHA256.HashData stream |> Convert.ToHexString

let private regenerate sourcePath targetPath suffix =
    use source = JsonDocument.Parse(File.ReadAllText sourcePath)
    let name = source.RootElement.GetProperty("name").GetString()
    let version = source.RootElement.GetProperty("version").GetInt32()
    let generated = sprintf "{\"name\":\"%s\",\"version\":%d}%s\n" name version suffix
    File.WriteAllText(targetPath, generated)
    generated

let private validatesSchemaAndGolden goldenPath generatedPath =
    try
        use generated = JsonDocument.Parse(File.ReadAllText generatedPath)
        let root = generated.RootElement
        root.GetProperty("name").GetString() = "evidence"
        && root.GetProperty("version").GetInt32() = 1
        && File.ReadAllText goldenPath = File.ReadAllText generatedPath
    with :? JsonException -> false

let private consumeGeneratedContract generatedPath =
    try
        use generated = JsonDocument.Parse(File.ReadAllText generatedPath)
        let root = generated.RootElement
        Some(sprintf "%s:%d" (root.GetProperty("name").GetString()) (root.GetProperty("version").GetInt32()))
    with :? JsonException -> None

let private withRealFixture action =
    let root = Path.Combine(Path.GetTempPath(), "fsgg-evidence-boundary-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory(Path.Combine(root, "contracts")) |> ignore
    try
        let sourcePath = Path.Combine(root, "contracts", "source.json")
        let generatedPath = Path.Combine(root, "contracts", "generated.json")
        let regeneratedPath = Path.Combine(root, "contracts", "generated-again.json")
        let goldenPath = Path.Combine(root, "contracts", "generated.golden.json")
        File.WriteAllText(sourcePath, "{\"name\":\"evidence\",\"version\":1}\n")
        File.WriteAllText(goldenPath, "{\"name\":\"evidence\",\"version\":1}\n")
        let first = regenerate sourcePath generatedPath ""
        let second = regenerate sourcePath regeneratedPath ""
        let dotnetExit, dotnetVersion, dotnetError = runCommand "dotnet" [ "--version" ]
        let headExit, head, headError = runCommand "git" [ "rev-parse"; "HEAD" ]
        if dotnetExit <> 0 then failwithf "dotnet --version failed: %s" dotnetError
        if headExit <> 0 then failwithf "git rev-parse HEAD failed: %s" headError
        let sourceAndHeadDigest = sprintf "%s:%s" head (digest sourcePath)
        let real kind observation =
            { Subject = "contracts/source.json"
              Kind = kind
              Provenance = Real
              Command = sprintf "dotnet --version=%s; git rev-parse HEAD=%s" dotnetVersion head
              ExitCode = dotnetExit
              SourceDigest = sourceAndHeadDigest
              Fresh = true
              Observation = observation }
        let artifact =
            { Path = "contracts/generated.json"
              Source = Some "contracts/source.json"
              RegenerationDeterministic = first = second
              Consumer =
                match consumeGeneratedContract generatedPath with
                | Some "evidence:1" -> Some "EvidenceBoundaryTests.consumeGeneratedContract"
                | _ -> None
              HasGoldenOrSchema = validatesSchemaAndGolden goldenPath generatedPath }
        let request =
            { RequiresProductionJourney = true
              RequiresObservedOutcome = true
              Evidence = [ real SemanticRegression ObservedOutcome; real BoundaryFixture ObservedOutcome; real GoldenOrSchema ObservedOutcome; real ProductionJourney ObservedOutcome ]
              GeneratedArtifacts = [ artifact ]
              Mitigations = [ { Claim = "sound is never requested"; ProducerClasses = [ "native"; "fallback" ]; ReintroducedByMutation = [ "native"; "fallback" ] } ]
              Render = Some { Fixture = "tests/render-evidence.fsx"; Executed = true; ByteReproducible = false; SemanticReceiptStable = true } }
        action (root, sourcePath, generatedPath, goldenPath, real, request)
    finally
        Directory.Delete(root, true)

let private codes request = evaluate request |> List.map _.Code

[<Tests>]
let tests =
    testList "EvidenceBoundary" [
        test "real process filesystem schema golden and consumer receipts feed evaluate" {
            withRealFixture (fun (_, _, _, _, _, request) ->
                Expect.isEmpty (evaluate request) "actual command, source/head digest, schema/golden regeneration, and consuming route complete the request") }
        test "synthetic records are disclosed support but cannot satisfy real obligations" {
            withRealFixture (fun (_, _, _, _, _, clean) ->
                let request = { clean with Evidence = clean.Evidence |> List.map (fun item -> { item with Provenance = Synthetic "test boundary" }) }
                Expect.contains (codes request) "evidence.real-boundary-required" "every required kind needs real evidence") }
        test "dispatch only and degraded only cannot satisfy observed outcome" {
            withRealFixture (fun (_, _, _, _, _, clean) ->
                for observation in [ DispatchOnly; Degraded ] do
                    let request = { clean with Evidence = clean.Evidence |> List.map (fun item -> { item with Observation = observation }) }
                    Expect.contains (codes request) "evidence.observed-outcome-missing" (string observation)) }
        test "optional degraded receipt is accepted beside an observed real outcome" {
            withRealFixture (fun (_, _, _, _, real, clean) ->
                let optional = { real SemanticRegression Degraded with Subject = "optional telemetry boundary" }
                Expect.isEmpty (evaluate { clean with Evidence = optional :: clean.Evidence }) "an explicit optional degradation does not erase the observed required route") }
        test "explicit malformed nonzero result is a valid safe-failure receipt" {
            withRealFixture (fun (_, sourcePath, _, _, _, clean) ->
                let exitCode, _, _ = runCommand "dotnet" [ "--definitely-malformed-evidence-boundary-option" ]
                let request =
                    { clean with
                        Evidence =
                            { Subject = "malformed boundary input"; Kind = BoundaryFixture; Provenance = Real; Command = "dotnet --definitely-malformed-evidence-boundary-option"; ExitCode = exitCode; SourceDigest = digest sourcePath; Fresh = true; Observation = MalformedInput } :: clean.Evidence }
                Expect.isTrue (exitCode <> 0) "the real malformed command fails"
                Expect.isEmpty (evaluate request) "the malformed input is explicitly and truthfully represented") }
        test "unknown partial stale and failed-command controls fail closed" {
            withRealFixture (fun (_, _, _, _, real, clean) ->
                let controls = [ UnknownInput, 0, true; PartialWrite, 0, true; ObservedOutcome, 0, false; ObservedOutcome, 1, true ]
                for observation, exitCode, fresh in controls do
                    let request = { clean with Evidence = { real BoundaryFixture observation with ExitCode = exitCode; Fresh = fresh } :: clean.Evidence }
                    Expect.isNonEmpty (evaluate request) (string observation)) }
        test "actual generated source regeneration schema golden and consumer mutations red" {
            withRealFixture (fun (root, sourcePath, generatedPath, goldenPath, _, clean) ->
                let nondeterministic = regenerate sourcePath (Path.Combine(root, "contracts", "generated-broken.json")) "-broken" <> File.ReadAllText generatedPath
                File.WriteAllText(goldenPath, "{\"name\":\"different\",\"version\":1}\n")
                let mutations =
                    [ { clean.GeneratedArtifacts.Head with Source = None }, "evidence.generated-source-missing"
                      { clean.GeneratedArtifacts.Head with RegenerationDeterministic = not nondeterministic }, "evidence.generated-regeneration-nondeterministic"
                      { clean.GeneratedArtifacts.Head with Consumer = consumeGeneratedContract goldenPath |> Option.bind (fun _ -> None) }, "evidence.generated-consumer-missing"
                      { clean.GeneratedArtifacts.Head with HasGoldenOrSchema = validatesSchemaAndGolden goldenPath generatedPath }, "evidence.generated-compatibility-missing" ]
                for artifact, expected in mutations do
                    Expect.contains (codes { clean with GeneratedArtifacts = [ artifact ] }) expected expected) }
        test "each producer inventory mutation has teeth" {
            withRealFixture (fun (_, _, _, _, _, clean) ->
                let empty = { clean.Mitigations.Head with ProducerClasses = [] }
                let incomplete = { clean.Mitigations.Head with ReintroducedByMutation = [ "native" ] }
                Expect.contains (codes { clean with Mitigations = [ empty ] }) "evidence.producer-inventory-empty" "empty inventory is not a mitigation"
                Expect.contains (codes { clean with Mitigations = [ incomplete ] }) "evidence.producer-mutation-incomplete" "each producer mutation has teeth") }
        test "findings identify the fact and one bounded correction" {
            withRealFixture (fun (_, _, _, _, _, clean) ->
                let finding = evaluate { clean with GeneratedArtifacts = [ { clean.GeneratedArtifacts.Head with Consumer = None } ] } |> List.exactlyOne
                Expect.equal (finding.Subject, finding.Correction) ("contracts/generated.json", "name and execute a consuming route") "consumer diagnostic is actionable") }
        test "render execution and semantic receipt mutations red" {
            withRealFixture (fun (_, _, _, _, _, clean) ->
                let notExecuted = { clean.Render.Value with Executed = false }
                let unstable = { clean.Render.Value with ByteReproducible = false; SemanticReceiptStable = false }
                Expect.contains (codes { clean with Render = Some notExecuted }) "evidence.render-not-executed" "fixture must run"
                Expect.contains (codes { clean with Render = Some unstable }) "evidence.render-receipt-unstable" "non-byte-stable fixture needs stable receipt") }
    ]
