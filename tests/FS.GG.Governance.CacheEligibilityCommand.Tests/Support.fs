module FS.GG.Governance.CacheEligibilityCommand.Tests.Support

// Faked-port + real-temp-git support helpers (Principle V — real inputs/outputs through faked edges, never
// mocks of the cores). The in-memory FileReader serves a literal `.fsgg` catalog; the in-memory GitPort
// returns canned read-only git output the REAL Snapshot.assemble parses; the fake FreshnessSensor returns
// fixed literal facts (Synthetic — a real hash is a non-reproducible oracle; the real sensor is proven once
// in EndToEndTests); the capturing Write/Out record what the interpreter writes/emits. Expected documents are
// computed with the genuine F043/F041/F042 cores. A `withTempRepo` helper drives REAL git for the e2e proof.

open System
open System.IO
open System.Diagnostics
open System.Text.Json
open FS.GG.Governance.Config
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Snapshot
open FS.GG.Governance.Snapshot.Model
open FS.GG.Governance.Routing
open FS.GG.Governance.Findings
open FS.GG.Governance.Gates
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Route
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.FreshnessResolution
open FS.GG.Governance.FreshnessResolution.Model
open FS.GG.Governance.CacheEligibility
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.CacheEligibilityJson
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.CacheEligibilityCommand

// ── repo-root locator (for the surface baseline) ──

let rec private findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root (FS.GG.Governance.sln) not found"
    | d ->
        let here ext = File.Exists(Path.Combine(d.FullName, "FS.GG.Governance." + ext))
        if here "sln" || here "slnx" then d.FullName else findRepoRoot d.Parent

let repoRoot = findRepoRoot (DirectoryInfo(AppContext.BaseDirectory))

let gp (s: string) = GovernedPath s

let storeSchemaVersion = "fsgg.evidence-reuse-store/v1"

/// Read a JSON string value, coalescing a JSON `null` to "" (keeps the Nullable=enable build green).
let jsonStr (el: JsonElement) : string =
    match el.GetString() with
    | null -> ""
    | s -> s

/// Read a named string property of a JSON object (null-coalesced).
let jsonProp (el: JsonElement) (name: string) : string = jsonStr (el.GetProperty name)

// ── In-memory `.fsgg` catalog fixtures (the F022 worked example) ──

let private yaml (s: string) = s.TrimStart('\n')

let private projectYml =
    yaml """
schemaVersion: 1
id: my-product
governedRoot: .
domains:
  - package-api
  - workflow
packageSurfaces:
  - src
policyRef: .fsgg/policy.yml
capabilitiesRef: .fsgg/capabilities.yml
"""

let private policyYml =
    yaml """
schemaVersion: 1
defaultProfile: standard
profiles:
  - light
  - standard
  - strict
branchPolicy:
  pattern: "main"
  requirePr: true
reviewBudget:
  maxReviews: 3
"""

let private toolingYml =
    yaml """
schemaVersion: 1
commands:
  - id: dotnet-format
    command: "dotnet format"
    timeout: 600
    environment: local-or-ci
  - id: dotnet-build
    command: "dotnet build"
    timeout: 600
    environment: local-or-ci
  - id: dotnet-audit
    command: "dotnet audit"
    timeout: 600
    environment: local-or-ci
environmentClasses:
  - local
  - ci
"""

let validCatalog: Map<string, string> =
    Map
        [ "project.yml", projectYml
          "capabilities.yml",
          yaml """
schemaVersion: 1
domains:
  - package-api
  - workflow
pathMap:
  - glob: "src/**"
    capability: package-api
  - glob: "work/**"
    capability: workflow
surfaces:
  - id: public-api
    kind: protected
    paths: ["src/**/*.fsi"]
    owner: platform
    maturity: block-on-ship
checks:
  - id: format
    domain: package-api
    command: dotnet-format
    owner: platform
    cost: cheap
    environment: local-or-ci
    maturity: block-on-ship
  - id: build
    domain: package-api
    command: dotnet-build
    owner: platform
    cost: medium
    environment: local-or-ci
    maturity: block-on-ship
  - id: audit
    domain: workflow
    command: dotnet-audit
    owner: platform
    cost: high
    environment: local-or-ci
    maturity: block-on-ship
"""
          "policy.yml", policyYml
          "tooling.yml", toolingYml ]

let invalidCatalog: Map<string, string> =
    Map [ "project.yml", yaml """
schemaVersion: 999
id: my-product
governedRoot: .
domains:
  - package-api
""" ]

// In-memory FileReader: a missing key is `Ok None`; a present key is `Ok (Some _)`.
let readerOf (files: Map<string, string>) : Loader.FileReader =
    fun name ->
        match Map.tryFind name files with
        | Some content -> Ok(Some content)
        | None -> Ok None

// ── In-memory git port (canned READ-ONLY output the real Snapshot.assemble parses) ──

let diffPayload (changes: (char * string) list) : string =
    changes |> List.map (fun (k, p) -> sprintf "%c\000%s\000" k p) |> String.concat ""

let gitWithChanges (changes: (char * string) list) : GitPort =
    fun cmd ->
        match cmd with
        | RepoCheck -> Ok "true\n"
        | RevParse _ -> Ok "0123456\n"
        | MergeBase _ -> Ok "0123456\n"
        | DiffNameStatus _ -> Ok(diffPayload changes)
        | StatusPorcelain -> Ok ""
        | CurrentBranch -> Ok "main\n"

let gitEmpty: GitPort = gitWithChanges []

let gitNotRepo: GitPort =
    fun cmd ->
        match cmd with
        | RepoCheck -> Ok "false\n"
        | _ -> Ok ""

let portsGit (g: GitPort) : Ports = { Git = g; Ci = fun () -> None }

let defaultOpts: SnapshotOptions = { Since = None; Base = None; Head = None }
let sinceOpts (rev: string) : SnapshotOptions = { Since = Some(GitRef rev); Base = None; Head = None }

let snapshotOf (g: GitPort) (opts: SnapshotOptions) : RepoSnapshot =
    FS.GG.Governance.Snapshot.Interpreter.senseSnapshot (portsGit g) opts

let candidatesOf (g: GitPort) (opts: SnapshotOptions) : GovernedPath list =
    (snapshotOf g opts).Changed |> List.map (fun c -> c.Path)

let baseHeadOfSnapshot (snap: RepoSnapshot) : Revision option * Revision option =
    match snap.Range with
    | Some r ->
        let (CommitId b) = r.Base
        let (CommitId h) = r.Head
        Some(Revision b), Some(Revision h)
    | None -> None, None

// ── Real selection chain (the verbatim F022 sequence, real cores) ──

let factsOf (files: Map<string, string>) : TypedFacts =
    match Loader.readSource (GovernedPath ".") (readerOf files) |> Schema.validate with
    | Valid f -> f
    | Invalid d -> failwithf "fixture catalog unexpectedly invalid: %A" d

let selectedGatesOf (files: Map<string, string>) (candidates: GovernedPath list) : Gate list =
    let facts = factsOf files
    let report = Routing.route facts candidates
    let registry = Gates.buildRegistry facts
    let findings = Findings.findUnknownGovernedPaths facts report
    let result = Route.select registry report findings
    result.SelectedGates |> List.map (fun sg -> sg.Gate)

// ── A literal gate builder (for the pure-Loop tier; mirrors the prelude f43Gate shape) ──

let mkGate (domain: string) (check: string) (cost: Cost) (env: EnvironmentClass) (command: CommandId option) : Gate =
    { Id = GateId(domain + ":" + check)
      Domain = DomainId domain
      Description = ""
      Prerequisites =
        (match command with
         | Some c -> [ RequiresCommand c ]
         | None -> [])
      Cost = cost
      Timeout = TimeoutLimit 600
      Owner = Owner "platform"
      Maturity = BlockOnShip
      ProductCheck = false
      FreshnessKey =
        { Check = CheckId check
          Domain = DomainId domain
          Cost = cost
          Environment = env
          Command = command } }

// ── Fake FreshnessSensor (SYNTHETIC: fixed literal facts — a real hash is a non-reproducible oracle; the
//    real BCL-crypto sensor is proven once in EndToEndTests) ──

let fixedSensor: Interpreter.FreshnessSensor =
    { SenseRuleHash = fun () -> Some(RuleHash "rule-1")
      SenseGeneratorVersion = fun () -> Some(GeneratorVersion "gen-1")
      SenseCoveredArtifacts = fun _ -> Some [ ArtifactHash "art-1" ]
      SenseCommandVersion = fun _ -> Some(CommandVersion "cmd-1") }

/// A sensor that cannot sense covered artifacts (returns None) ⇒ gates unresolved on covered artifacts.
let sensorNoCovered: Interpreter.FreshnessSensor =
    { fixedSensor with SenseCoveredArtifacts = fun _ -> None }

/// A sensor whose covered set is sensed-but-EMPTY (Some []) ⇒ resolves (sensed-empty ≠ unsensed).
let sensorEmptyCovered: Interpreter.FreshnessSensor =
    { fixedSensor with SenseCoveredArtifacts = fun _ -> Some [] }

// The interpreter's SensedFacts assembly, replicated as a test oracle (base/head passed through from the
// snapshot range; the per-key facts from the sensor — a present key = sensed, absent = unsensed).
let assembleSensed (sensor: Interpreter.FreshnessSensor) (gates: Gate list) (baseHead: Revision option * Revision option) : SensedFacts =
    let baseOpt, headOpt = baseHead

    { RuleHash = sensor.SenseRuleHash()
      GeneratorVersion = sensor.SenseGeneratorVersion()
      Base = baseOpt
      Head = headOpt
      CoveredArtifacts =
        gates
        |> List.choose (fun g -> sensor.SenseCoveredArtifacts g |> Option.map (fun hs -> g.Id, hs))
        |> Map.ofList
      CommandVersions =
        gates
        |> List.choose (fun g -> g.FreshnessKey.Command)
        |> List.distinct
        |> List.choose (fun c -> sensor.SenseCommandVersion c |> Option.map (fun v -> c, v))
        |> Map.ofList }

// A fully-sensed SensedFacts over the given gates (every gate resolves).
let fullSensed (gates: Gate list) : SensedFacts = assembleSensed fixedSensor gates (Some(Revision "base-1"), Some(Revision "head-1"))

// ── Expected-document computers (genuine cores) ──

let expectedCacheDoc (gates: Gate list) (sensed: SensedFacts) (store: ReuseStore) : string =
    let report = FreshnessResolution.resolve gates sensed
    let candidates = FreshnessResolution.entries report |> List.choose FreshnessResolution.candidate
    CacheEligibilityJson.ofReport (CacheEligibility.evaluate candidates store)

/// A store whose newest matching entry makes EVERY resolvable gate reusable, built from the genuine resolved
/// inputs (so the match is real, never fabricated). `ev g` names the evidence per gate.
let storeMakingReusable (gates: Gate list) (sensed: SensedFacts) (ev: GateId -> string) : ReuseStore =
    let report = FreshnessResolution.resolve gates sensed

    FreshnessResolution.entries report
    |> List.choose (fun e ->
        FreshnessResolution.candidate e
        |> Option.map (fun c ->
            { Inputs = c.Inputs
              Evidence = EvidenceRef(ev e.Gate) }))
    |> ReuseStore

// ── Read-only store serializer (test/fixture side — the command has NO writer this row, A5) ──

let private jstr (s: string) = JsonSerializer.Serialize s

let private envToken (e: EnvironmentClass) : string =
    match e with
    | EnvironmentClass.Local -> "local"
    | EnvironmentClass.Ci -> "ci"
    | EnvironmentClass.LocalOrCi -> "local-or-ci"
    | EnvironmentClass.Release -> "release"

let serializeStore (store: ReuseStore) : string =
    let (ReuseStore recs) = store

    let entry (r: RecordedEvidence) : string =
        let i = r.Inputs
        let (CheckId c) = i.Check
        let (DomainId d) = i.Domain

        let cmd =
            match i.Command with
            | Some(CommandId x) -> jstr x
            | None -> "null"

        let (RuleHash rh) = i.RuleHash
        let covered = i.CoveredArtifacts |> List.map (fun (ArtifactHash h) -> jstr h) |> String.concat ","

        let cv =
            match i.CommandVersion with
            | Some(CommandVersion x) -> jstr x
            | None -> "null"

        let (GeneratorVersion gv) = i.GeneratorVersion
        let (Revision b) = i.Base
        let (Revision h) = i.Head
        let (EvidenceRef ev) = r.Evidence

        sprintf
            "{\"check\":%s,\"domain\":%s,\"command\":%s,\"environment\":%s,\"ruleHash\":%s,\"coveredArtifacts\":[%s],\"commandVersion\":%s,\"generatorVersion\":%s,\"base\":%s,\"head\":%s,\"evidence\":%s}"
            (jstr c)
            (jstr d)
            cmd
            (jstr (envToken i.Environment))
            (jstr rh)
            covered
            cv
            (jstr gv)
            (jstr b)
            (jstr h)
            (jstr ev)

    let body = recs |> List.map entry |> String.concat ","
    sprintf "{\"schemaVersion\":%s,\"recorded\":[%s]}" (jstr storeSchemaVersion) body

// ── Capturing write/output edges + StoreReader stubs ──

type Capture =
    { mutable Writes: (Loop.ArtifactKind * string * string) list
      mutable Emits: string list }

let newCapture () : Capture = { Writes = []; Emits = [] }

let capturingWriter (cap: Capture) (failPaths: Set<string>) (cacheOut: string) (unresolvedOut: string) : string -> string -> Result<unit, string> =
    fun path content ->
        if Set.contains path failPaths then
            Error "no space left on device"
        else
            let kind = if path = cacheOut then Loop.CacheArtifact else Loop.UnresolvedArtifact
            ignore unresolvedOut
            cap.Writes <- cap.Writes @ [ kind, path, content ]
            Ok()

let capturingSink (cap: Capture) : string -> unit =
    fun text -> cap.Emits <- cap.Emits @ [ text ]

/// A StoreReader stub returning a fixed result regardless of path.
let storeReaderOf (result: Result<ReuseStore option, string>) : Interpreter.StoreReader = fun _ -> result

let fakePorts
    (files: Map<string, string>)
    (g: GitPort)
    (sensor: Interpreter.FreshnessSensor)
    (store: Interpreter.StoreReader)
    (cap: Capture)
    (req: Loop.RunRequest)
    : Interpreter.Ports =
    { Files = readerOf files
      Git = portsGit g
      Freshness = sensor
      Store = store
      Write = capturingWriter cap Set.empty req.CacheOut req.UnresolvedOut
      Out = capturingSink cap }

let fakePortsFailingWrites
    (files: Map<string, string>)
    (g: GitPort)
    (sensor: Interpreter.FreshnessSensor)
    (store: Interpreter.StoreReader)
    (cap: Capture)
    (failPaths: Set<string>)
    (req: Loop.RunRequest)
    : Interpreter.Ports =
    { Files = readerOf files
      Git = portsGit g
      Freshness = sensor
      Store = store
      Write = capturingWriter cap failPaths req.CacheOut req.UnresolvedOut
      Out = capturingSink cap }

let writtenOf (cap: Capture) (kind: Loop.ArtifactKind) : (string * string) option =
    cap.Writes |> List.tryPick (fun (k, p, c) -> if k = kind then Some(p, c) else None)

// ── Request builders ──

let requestFor (scope: Loop.ScopeSelector) (format: Loop.OutputFormat) : Loop.RunRequest =
    { Repo = "."
      Scope = scope
      StorePath = "readiness/evidence-reuse.json"
      CacheOut = "readiness/cache-eligibility.json"
      UnresolvedOut = "readiness/cache-eligibility.unresolved.json"
      Format = format }

/// A fresh Model in the `Selected` phase carrying literal gates — the entry point for the pure-Loop tier
/// (bypasses catalog/selection so resolve→evaluate→project can be driven with controlled SensedFacts/store).
let selectedModel (gates: Gate list) (request: Loop.RunRequest) : Loop.Model =
    { fst (Loop.init request) with
        Phase = Loop.Selected
        SelectedGates = gates }

/// Drive the pure pipeline tail: feed FreshnessSensed then StoreLoaded, returning the model after the
/// projection (with the two WriteArtifact effects from the second message).
let driveProjection (model: Loop.Model) (sensed: SensedFacts) (store: ReuseStore) : Loop.Model * Loop.Effect list =
    let m1, _ = Loop.update (Loop.FreshnessSensed(Ok sensed)) model
    Loop.update (Loop.StoreLoaded(Ok store)) m1

// ── Real git temp-repo helper (the e2e proof) ──

let git (dir: string) (args: string list) : string =
    let psi = ProcessStartInfo "git"
    for a in args do
        psi.ArgumentList.Add a

    psi.WorkingDirectory <- dir
    psi.RedirectStandardOutput <- true
    psi.RedirectStandardError <- true
    psi.UseShellExecute <- false

    match Process.Start psi with
    | null -> failwith "git did not start"
    | p ->
        let out = p.StandardOutput.ReadToEnd()
        let err = p.StandardError.ReadToEnd()
        p.WaitForExit()

        if p.ExitCode <> 0 then
            failwithf "git %s failed in %s: %s" (String.concat " " args) dir err

        out

let writeFile (dir: string) (relPath: string) (content: string) : unit =
    let full = Path.Combine(dir, relPath)

    match Path.GetDirectoryName full with
    | null -> ()
    | parent -> Directory.CreateDirectory parent |> ignore

    File.WriteAllText(full, content)

/// Create a disposable temp git repo with a real `.fsgg` catalog and a real two-commit edit under `src/`,
/// run `body` against its path, then delete it.
let withTempRepo (body: string -> 'a) : 'a =
    let dir = Path.Combine(Path.GetTempPath(), "fsgg-ce-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory dir |> ignore

    try
        git dir [ "init"; "-q"; "-b"; "main" ] |> ignore
        git dir [ "config"; "user.email"; "fixture@fsgg.test" ] |> ignore
        git dir [ "config"; "user.name"; "FSGG Fixture" ] |> ignore
        git dir [ "config"; "commit.gpgsign"; "false" ] |> ignore

        for KeyValue(name, content) in validCatalog do
            writeFile dir (".fsgg/" + name) content

        writeFile dir "src/Lib/Thing.fs" "module Thing\nlet v = 1\n"
        git dir [ "add"; "-A" ] |> ignore
        git dir [ "commit"; "-qm"; "base" ] |> ignore
        writeFile dir "src/Lib/Thing.fs" "module Thing\nlet v = 2\n"
        git dir [ "add"; "-A" ] |> ignore
        git dir [ "commit"; "-qm"; "head" ] |> ignore
        body dir
    finally
        try
            Directory.Delete(dir, true)
        with _ ->
            ()
