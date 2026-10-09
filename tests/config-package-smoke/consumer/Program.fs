// Package-only consumption of synthetic declarations, not execution evidence.
open FS.GG.Governance.Config
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Config.CapabilityBindings

let private command : Fsgg.Provider.DeclaredCommand =
    { Executable = "tool with spaces"; Arguments = [ ""; "two words"; "$HOME"; ";"; "last" ] }
let private limits =
    { WorkingDirectory = Some (GovernedPath "src/./cli")
      Timeout = Some (TimeoutLimit 60); Cost = Some Cheap; Environments = [ Local ] }
let private binding =
    { CapabilityId = "build:build"; Required = false; Platforms = [ "linux" ]
      ToolIds = [ "compiler" ]; EvidenceIds = [ "build-log" ]
      Binding = Command command; Limits = Some limits }
let private format = { Id = "build-log"; Version = "1.0.0" }
let private request =
    { ContractVersion = "1.0.0"; SelectedPlatform = "linux"
      RequiredCapabilityIds = [ "build:build" ]; SemanticOnlyObligationIds = []
      KnownPlatforms = [ "linux"; "windows" ]; SupportedEnvironments = [ Local; Ci ]
      SupportedEvidenceFormats = [ format ]
      Tools = [ { Id = "compiler"; ExactVersion = "10.0.0" } ]
      Evidence = [ { Id = "build-log"; Format = format; OutputPath = GovernedPath "artifacts/./build.json" } ]
      Bindings = [ binding ] }

let require condition message = if not condition then failwith message
let expectResolved candidate =
    match resolve candidate with
    | Resolved value -> value
    | Rejected diagnostics -> failwithf "unexpected rejection: %A" diagnostics
let expectRejected candidate =
    match resolve candidate with
    | Rejected _ -> ()
    | Resolved _ -> failwith "invalid request accepted"

[<EntryPoint>]
let main arguments =
    let resolved = expectResolved request
    require (resolved.Bindings.Head.Binding = Command command) "literal ordered argv changed"
    require resolved.Bindings.Head.Required "trusted requirement lowered"
    require (resolved.Bindings.Head.Limits.Value.WorkingDirectory = Some (GovernedPath "src/cli")) "working path not normalized"
    require (resolved.Evidence.Head.OutputPath = GovernedPath "artifacts/build.json") "output path not normalized"
    expectRejected { request with Bindings = [] }
    expectRejected { request with Bindings = [ { binding with Limits = None } ] }
    expectRejected { request with SupportedEvidenceFormats = [ { format with Version = "2.0.0" } ] }
    let floors = [ "fsharp:public-surface"; "gameplay:fr-covered"; "gameplay:production-journey" ]
    let floorBindings = floors |> List.map (fun id ->
        { binding with CapabilityId = id; Required = true; ToolIds = []; EvidenceIds = []; Binding = SemanticOnly; Limits = None })
    let result = expectResolved { request with SemanticOnlyObligationIds = floors; Bindings = binding :: { binding with CapabilityId = "future:check" } :: floorBindings }
    require (result.UnsupportedCapabilityIds = [ "future:check" ]) "optional unsupported ID satisfied"
    require (result.Bindings |> List.filter (fun b -> List.contains b.CapabilityId floors) |> List.forall (fun b -> b.Binding = SemanticOnly)) "semantic-only floor acquired a command"
    let reader name =
        match name with
        | "governance.yml" -> Ok (Some "schemaVersion: 1\nid: p\ngovernedRoot: .\ndomains:\n  - workflow")
        | "capabilities.yml" -> Ok (Some "schemaVersion: 2\ndomains:\n  - workflow")
        | _ -> Ok None
    match Schema.validate (Loader.readSource (GovernedPath ".") reader) with
    | Valid _ -> ()
    | Invalid diagnostics -> failwithf "legacy loading rejected: %A" diagnostics
    require (arguments.Length = 3) "receipt path, Config digest and source revision required"
    let configAssembly = typeof<GovernedPath>.Assembly
    let contractsAssembly = typeof<Fsgg.Provider.DeclaredCommand>.Assembly
    let loadedCount name =
        System.AppDomain.CurrentDomain.GetAssemblies()
        |> Array.filter (fun assembly -> assembly.GetName().Name = name)
        |> Array.length
    let configCount = loadedCount "FS.GG.Governance.Config"
    let contractsCount = loadedCount "FS.GG.Contracts"
    require (configCount = 1 && contractsCount = 1) "ambiguous runtime assembly identity"
    let assemblyDigest (assembly: System.Reflection.Assembly) =
        assembly.Location
        |> System.IO.File.ReadAllBytes
        |> System.Security.Cryptography.SHA256.HashData
        |> System.Convert.ToHexString
        |> fun value -> value.ToLowerInvariant()
    let receipt =
        {| result = "passed"
           configSha256 = arguments[1]
           sourceRevision = arguments[2]
           configAssemblySha256 = assemblyDigest configAssembly
           contractsAssemblySha256 = assemblyDigest contractsAssembly
           contractsAssemblyVersion = string (contractsAssembly.GetName().Version)
           loadedConfigCount = configCount
           loadedContractsCount = contractsCount |}
    System.IO.File.WriteAllText(arguments[0], System.Text.Json.JsonSerializer.Serialize(receipt))
    printfn "Config packaged resolver and legacy loading passed (synthetic requests only)."
    0
