module FS.GG.Governance.VerifyCommand.Tests.ProviderExecutionNativeTests

// GOV-423-C2 native journey: the real Verify host (`Program.runHosted`) captures real schema-2 provenance and an
// independent policy, plans, and runs the admitted command as a real child process through the GateExecution
// held-cwd direct-child host. The fixture is a POSIX sh script whose `run` writes the declared
// `fsgg.governance-handoff@2.0.0` evidence. Linux-x64 with glibc >= 2.40 is required by the held-cwd host, so the
// list is opt-in (`FSGG_GOV_PROVIDER_EXECUTION_NATIVE=1`), like the other native capture/execution cases.

open System
open System.IO
open System.Security.Cryptography
open System.Text
open Expecto
open FS.GG.Governance.VerifyCommand
open FS.GG.Governance.VerifyCommand.Tests

let nativeEnabled = Environment.GetEnvironmentVariable "FSGG_GOV_PROVIDER_EXECUTION_NATIVE" = "1"

let private dirOf (path: string) =
    match Path.GetDirectoryName path with
    | null -> failtest "no parent directory"
    | dir -> dir

let private sha (bytes: byte array) = Convert.ToHexString(SHA256.HashData bytes).ToLowerInvariant()

let private literalArguments =
    [ "run"; "--input"; "inputs/tests.json"; "--output"; "out/governance-handoff.json"; ""; "with spaces"; "$(literal);&" ]

/// The fixture: checks its literal argv, then by FSGG_FIXTURE_MODE writes the handoff citing Governance-verifiable
/// source bytes (`write`), writes nothing (`none`), cites wrong bytes (`mismatch`) or exits 2 after writing (`exit2`).
let private fixtureScript =
    "#!/bin/sh\n"
    + "[ \"$1\" = run ] || exit 3\n"
    + "[ \"$#\" -eq 8 ] || exit 4\n"
    + "[ \"$6\" = \"\" ] && [ \"$7\" = \"with spaces\" ] && [ \"$8\" = '$(literal);&' ] || exit 5\n"
    + "[ \"$(pwd -P)\" = \"$FSGG_FIXTURE_EXPECTED_CWD\" ] || exit 6\n"
    + "mode=\"${FSGG_FIXTURE_MODE:-write}\"\n"
    + "[ \"$mode\" = none ] && exit 0\n"
    + "digest=$(sha256sum \"$3\" | cut -d' ' -f1)\n"
    + "[ \"$mode\" = mismatch ] && digest=" + String('a', 64) + "\n"
    + "mkdir -p \"$(dirname \"$5\")\"\n"
    + "printf '{\"contractVersion\":\"2.0.0\",\"schemaVersion\":1,\"evidence\":{\"nodes\":[{\"id\":\"build:build\",\"state\":\"real\"}],\"dependencies\":[]},\"governedReferences\":[],\"sources\":[{\"path\":\"%s\",\"digest\":\"sha256:%s\"}]}' \"$3\" \"$digest\" > \"$5\"\n"
    + "[ \"$mode\" = exit2 ] && exit 2\n"
    + "exit 0\n"

let private mappings =
    "[{\"format\":\"fsgg.governance-handoff@2.0.0\",\"id\":\"fsgg.governance-handoff\",\"version\":\"2.0.0\"}]"

let private policyBytes =
    Encoding.UTF8.GetBytes(
        "{\"schemaVersion\":1,\"id\":\"native-provider-policy\",\"version\":\"1.0.0\",\"evidenceMap\":{\"id\":\"native-map\",\"version\":\"1.0.0\",\"digest\":\"sha256:"
        + sha (Encoding.UTF8.GetBytes mappings)
        + "\",\"mappings\":" + mappings
        + "},\"requiredCapabilityIds\":[\"build:build\"],\"semanticOnlyCapabilityIds\":[],\"knownPlatforms\":[\"linux-x64\"],\"environments\":[{\"id\":\"fixture-local\",\"environment\":\"local\"}],\"supportedEvidenceFormats\":[{\"id\":\"fsgg.governance-handoff\",\"version\":\"2.0.0\"}],\"maximumPreflightSeconds\":30,\"maximumScaffoldSeconds\":60,\"toolProbes\":[],\"archives\":[]}\n"
    )

let private provenanceFor (executable: string) =
    let policy =
        match FS.GG.SDD.Commands.CatalogScaffoldPolicy.parse ("sha256:" + sha policyBytes) policyBytes with
        | Ok value -> value
        | Error errors -> failtestf "%A" errors
    let original = ProviderLoaderFixture.synthetic ()
    let d = original.Declaration.Descriptor
    let capability = d.Capabilities.Head
    let descriptor =
        ProviderLoaderFixture.reseal
            { d with
                Platforms = [ "linux-x64" ]
                Tools = d.Tools |> List.map (fun t -> { t with Platforms = [ "linux-x64" ] })
                Capabilities =
                    [ { capability with
                          Platforms = [ "linux-x64" ]
                          EvidenceIds = [ "fixture-handoff" ]
                          Binding =
                            Fsgg.ProviderCatalog.Command(
                                { Executable = executable; Arguments = literalArguments },
                                { WorkingDirectory = "."; TimeoutSeconds = 20; CostClass = "cheap"; EnvironmentIds = [ "fixture-local" ] }) } ]
                Evidence =
                    [ { Id = "fixture-handoff"; Format = "fsgg.governance-handoff@2.0.0"; Path = "out/governance-handoff.json"; Required = true } ] }
    let record =
        { original with
            Observation = { original.Observation with Platform = "linux-x64" }
            Declaration =
                { original.Declaration with
                    Descriptor = descriptor
                    Policy = { Id = policy.Identity.Id; Version = policy.Identity.Version; Digest = policy.Identity.Digest }
                    EvidenceMap = { Id = policy.EvidenceMapIdentity.Id; Version = policy.EvidenceMapIdentity.Version; Digest = policy.EvidenceMapIdentity.Digest }
                    Admission =
                        { original.Declaration.Admission with
                            Platform = "linux-x64"
                            SupportedPlatforms = [ "linux-x64" ]
                            RequiredCapabilityIds = policy.RequiredCapabilityIds
                            SupportedFormats = [ { Id = "fsgg.governance-handoff"; Version = "2.0.0" } ]
                            EvidenceMappings = [ { Format = "fsgg.governance-handoff@2.0.0"; Id = "fsgg.governance-handoff"; Version = "2.0.0" } ]
                            Descriptor = descriptor } } }
    ProviderContextLoaderTests.provenanceBytes record

type private Journey =
    { Code: int
      Messages: string list
      Verify: string option
      Output: byte array option }

let private withWorkspace (body: string -> string -> string -> unit) =
    let top = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "fsgg-provider-native-" + Guid.NewGuid().ToString "N"))
    let repo = Path.Combine(top, "repo")
    let fixture = Path.Combine(top, "bin", "fixture.sh")
    let policy = Path.Combine(top, "policy", "policy.json")
    try
        for KeyValue(name, content) in ProviderExecutionTests.providerCatalog do
            Support.writeFile repo (".fsgg/" + name) content
        Support.writeFile repo "src/a.fs" "module A\n"
        Support.writeFile repo "inputs/tests.json" "{\"tests\":[\"literal\"]}\n"
        Directory.CreateDirectory(dirOf fixture) |> ignore
        File.WriteAllText(fixture, fixtureScript)
        File.SetUnixFileMode(fixture, UnixFileMode.UserRead ||| UnixFileMode.UserWrite ||| UnixFileMode.UserExecute)
        Directory.CreateDirectory(dirOf policy) |> ignore
        File.WriteAllBytes(policy, policyBytes)
        Directory.CreateDirectory(Path.Combine(repo, "readiness")) |> ignore
        File.WriteAllBytes(Path.Combine(repo, "readiness", "provenance.json"), provenanceFor fixture)
        body repo fixture policy
    finally
        try Directory.Delete(top, true) with _ -> ()

let private runJourney (repo: string) (policy: string) (mode: string) (change: string list -> string list) =
    let provenance = File.ReadAllBytes(Path.Combine(repo, "readiness", "provenance.json"))
    let argv =
        [ "--repo"; repo
          "--provider-provenance"; "readiness/provenance.json"
          "--provider-provenance-sha256"; sha provenance
          "--provider-policy"; policy
          "--provider-policy-sha256"; sha policyBytes
          "--provider-platform"; "linux-x64"
          "--paths"; "src/a.fs"; "--json" ]
        |> change
    let messages = ResizeArray<string>()
    use signals = new ProviderHost.SignalCancellation()
    let system = ProviderHost.systemEffects signals
    let admitted = Map [ ProviderHost.workSecondsVariable, "60"; ProviderHost.cleanupSecondsVariable, "75" ]
    let effects =
        { system with
            Environment = fun name -> Map.tryFind name admitted
            EnvironmentSnapshot =
                fun () ->
                    Map [ "PATH", (match Environment.GetEnvironmentVariable "PATH" with null -> "/usr/bin:/bin" | path -> path)
                          "FSGG_FIXTURE_MODE", mode
                          "FSGG_FIXTURE_EXPECTED_CWD", repo ] }
    let code = Program.runHosted effects Interpreter.realPorts (fun m -> lock messages (fun () -> messages.Add m)) (Array.ofList argv)
    let read path = if File.Exists path then Some(File.ReadAllBytes path) else None
    { Code = code
      Messages = List.ofSeq messages
      Verify = read (Path.Combine(repo, "readiness", "verify.json")) |> Option.map Encoding.UTF8.GetString
      Output = read (Path.Combine(repo, "out", "governance-handoff.json")) }

let private cases =
    [ "positive: the real child writes its declared handoff; Verify executes, accepts and passes (0)", fun () ->
        withWorkspace (fun repo _ policy ->
            let j = runJourney repo policy "write" id
            Expect.equal j.Code 0 (sprintf "accepted: %A" j.Messages)
            Expect.isSome j.Output "the real child wrote the evidence"
            Expect.isTrue (j.Verify |> Option.exists (fun v -> v.Contains "build:build")) "verify.json written")
      "missing: a real exit 0 without the declared output blocks (1)", fun () ->
        withWorkspace (fun repo _ policy ->
            let j = runJourney repo policy "none" id
            Expect.equal j.Code 1 (sprintf "%A" j.Messages)
            Expect.isTrue (j.Messages |> List.exists (fun m -> m.Contains "was not produced by the run")) "named")
      "stale: a pre-existing output is never launched over and blocks (1)", fun () ->
        withWorkspace (fun repo _ policy ->
            Support.writeFile repo "out/governance-handoff.json" "stale"
            let j = runJourney repo policy "write" id
            Expect.equal j.Code 1 (sprintf "%A" j.Messages)
            Expect.equal j.Output (Some(Encoding.UTF8.GetBytes "stale")) "untouched: the child never ran"
            Expect.isTrue (j.Messages |> List.exists (fun m -> m.Contains "existed before launch")) "named")
      "mismatched: evidence citing bytes that are not the workspace's blocks (1)", fun () ->
        withWorkspace (fun repo _ policy ->
            let j = runJourney repo policy "mismatch" id
            Expect.equal j.Code 1 (sprintf "%A" j.Messages)
            Expect.isTrue (j.Messages |> List.exists (fun m -> m.Contains "sources do not match")) "named")
      "exit 2: a real non-zero exit blocks even with written evidence (1)", fun () ->
        withWorkspace (fun repo _ policy ->
            let j = runJourney repo policy "exit2" id
            Expect.equal j.Code 1 (sprintf "%A" j.Messages)
            Expect.isTrue (j.Messages |> List.exists (fun m -> m.Contains "exited 2")) "named")
      "refusals preserved: provenance digest, policy identity, platform and unrouted path refuse before launch (3)", fun () ->
        withWorkspace (fun repo _ policy ->
            let replace flag value (argv: string list) =
                let i = List.findIndex ((=) flag) argv
                argv |> List.mapi (fun k v -> if k = i + 1 then value else v)
            let expect3 name (j: Journey) =
                Expect.equal j.Code 3 (sprintf "%s: %A" name j.Messages)
                Expect.isNone j.Output (name + ": nothing launched")
            expect3 "provenance digest" (runJourney repo policy "write" (replace "--provider-provenance-sha256" (String('0', 64))))
            expect3 "platform" (runJourney repo policy "write" (replace "--provider-platform" "linux-arm64"))
            expect3 "unrouted" (runJourney repo policy "write" (replace "--paths" "AGENTS.md"))
            let otherPolicy = Path.Combine(dirOf policy, "other.json")
            let other = Encoding.UTF8.GetBytes((Encoding.UTF8.GetString policyBytes).Replace("native-provider-policy", "other-policy"))
            File.WriteAllBytes(otherPolicy, other)
            expect3 "policy identity" (runJourney repo policy "write" (replace "--provider-policy" otherPolicy >> replace "--provider-policy-sha256" (sha other)))) ]

[<Tests>]
let tests =
    testSequenced
    <| testList "ProviderExecution Native" (cases |> List.map (fun (name, body) -> if nativeEnabled then testCase name body else ptestCase name body))
