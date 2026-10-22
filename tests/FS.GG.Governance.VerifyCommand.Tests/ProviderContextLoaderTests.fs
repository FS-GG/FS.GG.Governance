module FS.GG.Governance.VerifyCommand.Tests.ProviderContextLoaderTests

open System
open System.Text
open System.Security.Cryptography
open Expecto
open FS.GG.Governance.VerifyCommand

// Synthetic document bytes test actual selected package codecs; no capture or provider ran.
let bytes (text: string) = Encoding.UTF8.GetBytes text
let sha (value: byte array) = Convert.ToHexString(SHA256.HashData value).ToLowerInvariant()
let policyBytes =
    let mapping = "[]"
    bytes ("""{"schemaVersion":1,"id":"fixture-policy","version":"1.0.0",
      "evidenceMap":{"id":"fixture-map","version":"1.0.0","digest":"sha256:DIGEST","mappings":[]},
      "requiredCapabilityIds":[],"semanticOnlyCapabilityIds":[],"knownPlatforms":["linux"],
      "environments":[{"id":"fixture-local","environment":"local"}],"supportedEvidenceFormats":[],
      "maximumPreflightSeconds":5,"maximumScaffoldSeconds":10,"toolProbes":[],"archives":[]}""".Replace("DIGEST", sha (bytes mapping)))
let coherentPolicyBytes = bytes """{"schemaVersion":1,"id":"independent-qualification-policy","version":"1.0.0","evidenceMap":{"id":"qualification-map","version":"1.0.0","digest":"sha256:61dc9d0938fc1c23541de3941279db5f410e05620390c099d3bba3600f96efb8","mappings":[{"format":"opaque@literal","id":"opaque-format","version":"1.0.0"}]},"requiredCapabilityIds":["build:build"],"semanticOnlyCapabilityIds":[],"knownPlatforms":["fixture-platform"],"environments":[{"id":"fixture-local","environment":"local"}],"supportedEvidenceFormats":[{"id":"opaque-format","version":"1.0.0"}],"maximumPreflightSeconds":30,"maximumScaffoldSeconds":60,"toolProbes":[],"archives":[]}
"""
let coherentRecord () =
    let policy =
        match FS.GG.SDD.Commands.CatalogScaffoldPolicy.parse ("sha256:" + sha coherentPolicyBytes) coherentPolicyBytes with
        | Ok value -> value
        | Error errors -> failtestf "%A" errors
    let original = ProviderLoaderFixture.synthetic ()
    { original with
        Declaration =
            { original.Declaration with
                Policy = { Id = policy.Identity.Id; Version = policy.Identity.Version; Digest = policy.Identity.Digest }
                EvidenceMap = { Id = policy.EvidenceMapIdentity.Id; Version = policy.EvidenceMapIdentity.Version; Digest = policy.EvidenceMapIdentity.Digest }
                Admission = { original.Declaration.Admission with RequiredCapabilityIds = policy.RequiredCapabilityIds } } }
let provenanceBytes record =
    match FS.GG.SDD.Artifacts.CatalogScaffoldProvenance.serialize record with
    | Ok text -> bytes text
    | Error errors -> failtestf "%A" errors
let selection provenance policy : ProviderContext.Selection =
    { ProvenancePath = "readiness/provenance.json"; ProvenanceSha256 = sha provenance
      PolicyPath = "/independent/policy.json"; PolicySha256 = sha policy; Platform = "linux" }
let refusedAt path result =
    match result with
    | Ok _ -> failtest "Malformed input must not produce a request"
    | Error errors -> Expect.isTrue (errors |> List.exists (fun (d: Fsgg.ProviderCatalog.Diagnostic) -> d.Path = path)) "located refusal"
let argv =
    [| "--provider-provenance"; "readiness/provenance.json"; "--provider-provenance-sha256"; String('a', 64)
       "--provider-policy"; "/independent/policy.json"; "--provider-policy-sha256"; String('b', 64)
       "--provider-platform"; "linux" |]

[<Tests>]
let tests = testList "ProviderContextLoader" [
    testCase "coherent synthetic bytes use actual codecs and preserve complete literal request" (fun _ ->
        let provenance = provenanceBytes (coherentRecord ())
        let selected = { selection provenance coherentPolicyBytes with Platform = "fixture-platform" }
        match ProviderContextLoader.decode selected provenance coherentPolicyBytes with
        | Error errors -> failtestf "%A" errors
        | Ok request ->
            Expect.equal request.RequiredCapabilityIds [ "build:build" ] "independent required floor"
            match request.Bindings.Head.Binding with
            | FS.GG.Governance.Config.CapabilityBindings.Command command ->
                Expect.equal command.Arguments ProviderLoaderFixture.command.Arguments "literal argv"
            | _ -> failtest "executable declaration lost")
    testCase "independent policy cannot silently replace recorded required obligations" (fun _ ->
        let record = coherentRecord ()
        let provenance = provenanceBytes { record with Declaration = { record.Declaration with Admission = { record.Declaration.Admission with RequiredCapabilityIds = [] } } }
        let selected = { selection provenance coherentPolicyBytes with Platform = "fixture-platform" }
        ProviderContextLoader.decode selected provenance coherentPolicyBytes |> refusedAt "$.policy")
    testCase "coherent provenance cannot replace explicit selected platform" (fun _ ->
        let provenance = provenanceBytes (coherentRecord ())
        ProviderContextLoader.decode (selection provenance coherentPolicyBytes) provenance coherentPolicyBytes |> refusedAt "$.platform")

    testCase "raw provenance mismatch refuses before package parsing" (fun _ ->
        let original = bytes "{}"
        ProviderContextLoader.decode (selection original policyBytes) (bytes "different") policyBytes |> refusedAt "$.provenance")
    testCase "raw policy mismatch refuses before package parsing" (fun _ ->
        let provenance = bytes "{}"
        ProviderContextLoader.decode (selection provenance policyBytes) provenance (bytes "different") |> refusedAt "$.policy")
    testCase "actual selected policy codec accepts prefixed recomputed raw digest" (fun _ ->
        match FS.GG.SDD.Commands.CatalogScaffoldPolicy.parse ("sha256:" + sha policyBytes) policyBytes with
        | Ok _ -> ()
        | Error errors -> failtestf "%A" errors)
    testCase "strict UTF8 refuses with valid independent policy" (fun _ ->
        let provenance = [| 0xffuy |]
        ProviderContextLoader.decode (selection provenance policyBytes) provenance policyBytes |> refusedAt "$.provenance")
    testCase "unsupported provenance schema never becomes absence or legacy mode" (fun _ ->
        let provenance = bytes "{\"schemaVersion\":99}"
        match ProviderContextLoader.decode (selection provenance policyBytes) provenance policyBytes with
        | Error errors -> Expect.isNonEmpty errors "actual package refusal"
        | Ok _ -> failtest "Unsupported schema accepted")
    testCase "legacy schema-1 provenance is refused and never becomes legacy mode" (fun _ ->
        let provenance = bytes (FS.GG.SDD.Artifacts.ScaffoldProvenance.serialize (coherentRecord ()).Ownership)
        let selected = { selection provenance coherentPolicyBytes with Platform = "fixture-platform" }
        ProviderContextLoader.decode selected provenance coherentPolicyBytes |> refusedAt "$.provenance.schemaVersion")
    testCase "recorded admission budgets above the independent policy ceilings are refused" (fun _ ->
        let record = coherentRecord ()
        let budgets = { record.Declaration.Budgets with AdmittedPreflightSeconds = 31 }
        let provenance = provenanceBytes { record with Declaration = { record.Declaration with Budgets = budgets } }
        let selected = { selection provenance coherentPolicyBytes with Platform = "fixture-platform" }
        ProviderContextLoader.decode selected provenance coherentPolicyBytes |> refusedAt "$.policy.budgets")
    testCase "binding to an environment the independent policy does not map is refused at request admission" (fun _ ->
        let record = coherentRecord ()
        let declared = record.Declaration.Descriptor.Capabilities.Head
        let binding =
            match declared.Binding with
            | Fsgg.ProviderCatalog.Command(command, settings) ->
                Fsgg.ProviderCatalog.Command(command, { settings with EnvironmentIds = [ "unselected-environment" ] })
            | other -> other
        let changed = { record.Declaration.Descriptor with Capabilities = [ { declared with Binding = binding } ] }
        let descriptor = ProviderLoaderFixture.reseal changed
        let provenance = provenanceBytes { record with Declaration = { record.Declaration with Descriptor = descriptor; Admission = { record.Declaration.Admission with Descriptor = descriptor } } }
        let selected = { selection provenance coherentPolicyBytes with Platform = "fixture-platform" }
        match ProviderContextLoader.decode selected provenance coherentPolicyBytes with
        | Ok _ -> failtest "An unmapped environment must not produce a request"
        | Error errors ->
            Expect.isNonEmpty errors "rejected"
            Expect.isTrue (errors |> List.exists (fun d -> d.Path = "$.descriptor.capabilities.build:build.environments")) (sprintf "located admission diagnostic: %A" errors))
    testCase "Program provider path decodes actual bytes and drives nonpassing planning without legacy execution" (fun _ ->
        let provenance = provenanceBytes (coherentRecord ())
        let selected = { selection provenance coherentPolicyBytes with Platform = "fixture-platform" }
        let cap = Support.newCapture ()
        let mutable loads = 0
        let mutable execution = false
        let mutable cache = false
        let ports = Support.fakePorts Support.validCatalog Support.gitSrcChange cap
        let ports = { ports with
                        Execute = (fun _ -> execution <- true; failtest "provider planning cannot execute")
                        Store = (fun _ -> cache <- true; failtest "provider planning cannot reuse") }
        let load _ actual =
            loads <- loads + 1
            Expect.equal actual selected "all selectors routed unchanged"
            match ProviderContextLoader.decode actual provenance coherentPolicyBytes with
            | Error errors -> Error(sprintf "%A" errors)
            | Ok request -> Ok ({ ResolutionRequest = request; CommandContext = ProviderPlanningTests.context }: Loop.ProviderPlanningInput)
        let actualArgv =
            [| "--provider-provenance"; selected.ProvenancePath; "--provider-provenance-sha256"; selected.ProvenanceSha256
               "--provider-policy"; selected.PolicyPath; "--provider-policy-sha256"; selected.PolicySha256
               "--provider-platform"; selected.Platform; "--paths"; "src/a.fs" |]
        let code = Program.runWithProviderLoader load (fun _ -> ports) ignore actualArgv
        Expect.equal loads 1 "single package-backed admission"
        Expect.notEqual code 0 "planning never passes"
        Expect.isTrue (code = 1 || code = 3) "planning refuses or remains blocked"
        Expect.isFalse execution "no execution"
        Expect.isFalse cache "no legacy reuse"
        Expect.isEmpty cap.Writes "no passing artifact")
    testCase "Program standalone provider path refuses without constructing real ports" (fun _ ->
        let mutable created = false
        let messages = ResizeArray<string>()
        let code = Program.runWithProviderLoader (fun _ _ -> Error "original capture owner unavailable")
                       (fun _ -> created <- true; failtest "ports must remain unbuilt") messages.Add argv
        Expect.equal code 3 "input unavailable"
        Expect.isFalse created "no ports"
        Expect.isTrue (messages |> Seq.exists (fun m -> m.Contains "original capture owner")) "exact gap retained")
    testCase "Program invalid provider usage never calls loader or ports" (fun _ ->
        let code = Program.runWithProviderLoader (fun _ _ -> failtest "loader must remain uncalled")
                       (fun _ -> failtest "ports must remain unbuilt") ignore [| "--provider-platform"; "linux" |]
        Expect.equal code 2 "provider usage")
]
