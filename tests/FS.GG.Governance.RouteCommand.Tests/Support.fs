module FS.GG.Governance.RouteCommand.Tests.Support

// Faked-port + real-temp-git support helpers (Principle V — real inputs/outputs through faked edges,
// never mocks of the cores). The in-memory FileReader serves a literal `.fsgg` catalog; the in-memory
// GitPort returns canned read-only git output the REAL Snapshot.assemble parses; the capturing
// ArtifactWriter/OutputSink record what the interpreter writes/emits so tests can compare bytes
// against the genuine F020/F021 projections. A `withTempRepo` helper drives REAL git for the one
// end-to-end proof.

open System
open System.IO
open System.Diagnostics
open FS.GG.Governance.Config
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Snapshot.Model
open FS.GG.Governance.Routing
open FS.GG.Governance.Findings
open FS.GG.Governance.Gates
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Route
open FS.GG.Governance.RouteJson
open FS.GG.Governance.GatesJson
open FS.GG.Governance.RouteCommand
// F046 cache-eligibility pipeline (faked sensing ports + the genuine-core expected-report computer)
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.FreshnessResolution
open FS.GG.Governance.FreshnessResolution.Model
open FS.GG.Governance.CacheEligibility
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.EvidenceReuseStore
open FS.GG.Governance.FreshnessSensing
// F052 gate-execution test support (deterministic fake port over real byte[], outcome/grown-store helpers)
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.GateExecution
open FS.GG.Governance.GateExecution.Model
open FS.GG.Governance.GateRun
open FS.GG.Governance.GateRun.Model
open FS.GG.Governance.EvidenceCapture

// `Snapshot` has its own `Interpreter` module; alias it so `Interpreter` stays unambiguously the
// RouteCommand edge while `FS.GG.Governance.Snapshot.Interpreter.senseSnapshot` / `GitPort` / `Ports` /
// `RepoCheck`… reach the F016 git surface.
open FS.GG.Governance.Snapshot
// F27 wiring (063): the render-mode dispatch edge ports (capability sensing + rich render).
open FS.GG.Governance.HumanText

// 074: helpers consolidated into the shared FS.GG.Governance.Tests.Common library; these are
// thin re-exports so this suite's test files keep resolving them through `Support` unchanged.
open FS.GG.Governance.Tests.Common

let repoRoot = RepositoryHelpers.repoRoot
let gp = CatalogFixtures.gp
let yaml = CatalogFixtures.yaml
let projectYml = CatalogFixtures.projectYml
let policyYml = CatalogFixtures.policyYml
let toolingYml = CatalogFixtures.toolingYml
let validCatalog = CatalogFixtures.validCatalog
let emptyCatalog = CatalogFixtures.emptyCatalog
let invalidCatalog = CatalogFixtures.invalidCatalog
let readerOf = CatalogFixtures.readerOf
let factsOf = CatalogFixtures.factsOf
let plainCapability = FakePorts.plainCapability
let noRichRender = FakePorts.noRichRender
let diffPayload = FakePorts.diffPayload
let gitWithChanges = FakePorts.gitWithChanges
let gitEmpty = FakePorts.gitEmpty
let gitNotRepo = FakePorts.gitNotRepo
let gitUnavailable = FakePorts.gitUnavailable
let portsGit = FakePorts.portsGit
let fakeSensor = FakePorts.fakeSensor
let throwingSensor = FakePorts.throwingSensor
let absentStoreReader = FakePorts.absentStoreReader
let malformedStoreReader = FakePorts.malformedStoreReader
let fakeExecPortExiting = FakePorts.fakeExecPortExiting
let defaultOpts = SnapshotHelpers.defaultOpts
let sinceOpts = SnapshotHelpers.sinceOpts
let snapshotOf = SnapshotHelpers.snapshotOf
let snapshotOfRepo = SnapshotHelpers.snapshotOfRepo
let candidatesOf = SnapshotHelpers.candidatesOf
let candidatesOfRepo = SnapshotHelpers.candidatesOfRepo
let revOfCommit = SnapshotHelpers.revOfCommit
let baseHeadOfSnap = SnapshotHelpers.baseHeadOfSnap
let selectedGatesFor = SnapshotHelpers.selectedGatesFor
let expectedOutcomesWith = SnapshotHelpers.expectedOutcomesWith
let storeOf = SnapshotHelpers.storeOf
let persistInputs = SnapshotHelpers.persistInputs
let syntheticRef = SnapshotHelpers.syntheticRef
let readStore = SnapshotHelpers.readStore
let git = SnapshotHelpers.git
let writeFile = SnapshotHelpers.writeFile

let productCatalog: Map<string, string> =
    Map
        [ "project.yml", projectYml
          "capabilities.yml",
          yaml """
schemaVersion: 2
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
    kind: package
    paths: ["src/**/*.fsi"]
    owner: platform
    maturity: block-on-ship
    baseline: "src/public-api.baseline.txt"
  - id: product-root
    kind: generatedProduct
    paths: ["src"]
    owner: platform
    maturity: block-on-pr
    templateProfile: fsharp-lib
checks:
  - id: build
    domain: package-api
    command: dotnet-build
    owner: platform
    cost: medium
    environment: local-or-ci
    maturity: block-on-ship
    tier: restoreBuild
"""
          "policy.yml", policyYml
          "tooling.yml", toolingYml ]
let missingReader: Loader.FileReader = fun _ -> Ok None
let gitUnknownRev (badRev: string) : GitPort =
    fun cmd ->
        match cmd with
        | RepoCheck -> Ok "true\n"
        | RevParse(GitRef r) when r = badRev -> Error "fatal: bad revision 'nope'"
        | RevParse _ -> Ok "0123456\n"
        | MergeBase _ -> Ok "0123456\n"
        | DiffNameStatus _ -> Ok ""
        | StatusPorcelain -> Ok ""
        | CurrentBranch -> Ok "main\n"
let fakeSensorMissingCovered: FreshnessSensing.FreshnessSensor =
    { fakeSensor with SenseCoveredArtifacts = fun _ -> None }
let fakeExecPort: ExecutionPort = fakeExecPortExiting 0

/// A call-counting fake port (proves reuse SKIPS execution on a second run — the spawn-count assertion).
type ExecCounter = { mutable Calls: int }

let countingExecPort (counter: ExecCounter) (code: int) : ExecutionPort =
    fun command ->
        counter.Calls <- counter.Calls + 1
        fakeExecPortExiting code command

/// Given a model + the effects it just emitted, run any `ExecuteGates` effect through the port (mirroring the
/// interpreter edge) and feed the `GatesExecuted` records back into `update`. Returns the next (model, effects).
/// When no `ExecuteGates` effect is present, returns the inputs unchanged.
let runExecuteEffect (port: ExecutionPort) (model: Loop.Model) (effects: Loop.Effect list) : Loop.Model * Loop.Effect list =
    match
        effects
        |> List.tryPick (fun e ->
            match e with
            | Loop.ExecuteGates rs -> Some rs
            | _ -> None)
    with
    | Some requests ->
        let records =
            requests
            |> List.map (fun (gid, cmd) -> gid, FS.GG.Governance.GateExecution.Interpreter.senseExecution port cmd)

        Loop.update (Loop.GatesExecuted records) model
    | None -> model, effects
let expectedCacheReportWith
    (sensor: FreshnessSensing.FreshnessSensor)
    (store: ReuseStore)
    (selectedGates: Gate list)
    (baseHead: Revision option * Revision option)
    : CacheEligibilityReport =
    match FreshnessSensing.senseFreshness sensor selectedGates baseHead with
    | Ok sensed ->
        let report = FreshnessResolution.resolve selectedGates sensed
        let cands = FreshnessResolution.entries report |> List.choose FreshnessResolution.candidate
        CacheEligibility.evaluate cands store
    | Error _ ->
        // a sense Error degrades to empty SensedFacts ⇒ every gate unresolved ⇒ no candidate ⇒ empty report
        CacheEligibility.evaluate [] store

/// The standard (fully-sensing fake sensor + absent/empty store) expected report.
let expectedCacheReport (selectedGates: Gate list) (baseHead: Revision option * Revision option) : CacheEligibilityReport =
    expectedCacheReportWith fakeSensor EvidenceReuse.empty selectedGates baseHead
let expectedOutcomes (files: Map<string, string>) (selectedGates: Gate list) : (GateId * GateOutcome) list =
    expectedOutcomesWith fakeExecPort files selectedGates

/// The GROWN store the command persists: fold F049 `capture` over the loaded store for each selected gate
/// that declares a command and is NOT reused (mirrors the command's classify+capture exactly, repoRoot "."
/// as `requestFor` defaults). A reusable gate (recoverable prior exit) is not captured; a no-command gate is
/// skipped. Deterministic given the port/sensor/store.
let expectedGrownStoreAt (repoRoot: string) (port: ExecutionPort) (sensor: FreshnessSensing.FreshnessSensor) (files: Map<string, string>) (loaded: ReuseStore) (selectedGates: Gate list) (baseHead: Revision option * Revision option) : ReuseStore =
    let tooling = (factsOf files).Tooling

    match FreshnessSensing.senseFreshness sensor selectedGates baseHead with
    | Error _ -> loaded
    | Ok sensed ->
        let resReport = FreshnessResolution.resolve selectedGates sensed
        let candidates = FreshnessResolution.entries resReport |> List.choose FreshnessResolution.candidate

        let verdictMap =
            CacheEligibility.evaluate candidates loaded
            |> CacheEligibility.entries
            |> List.fold (fun m e -> Map.add (gateIdValue e.Gate) e.Verdict m) Map.empty

        let inputsMap =
            candidates |> List.fold (fun m c -> Map.add (gateIdValue c.Gate) c.Inputs m) Map.empty

        selectedGates
        |> List.fold
            (fun s g ->
                match tooling |> Option.bind (fun t -> Plan.commandFor repoRoot t g) with
                | None -> s
                | Some cmd ->
                    let reused =
                        match Map.tryFind (gateIdValue g.Id) verdictMap with
                        | Some(Reusable r) -> (Plan.priorExitOf r).IsSome
                        | _ -> false

                    if reused then
                        s
                    else
                        match Map.tryFind (gateIdValue g.Id) inputsMap with
                        | Some inputs ->
                            EvidenceCapture.capture inputs (FS.GG.Governance.GateExecution.Interpreter.senseExecution port cmd) s
                        | None -> s)
            loaded

let expectedGrownStore (port: ExecutionPort) (sensor: FreshnessSensing.FreshnessSensor) (files: Map<string, string>) (loaded: ReuseStore) (selectedGates: Gate list) (baseHead: Revision option * Revision option) : ReuseStore =
    expectedGrownStoreAt "." port sensor files loaded selectedGates baseHead

/// The store VALUE the command persists (F047 prune → retain pipeline over the grown store), as it re-reads.
let persistedValue (grown: ReuseStore) : ReuseStore =
    grown
    |> EvidenceReuseStore.prune
    |> EvidenceReuseStore.retain EvidenceReuseStore.defaultRetentionBound
let projectExpected (files: Map<string, string>) (candidates: GovernedPath list) (snap: RepoSnapshot option) : string * string =
    let facts = factsOf files
    let report = Routing.route facts candidates
    let registry = Gates.buildRegistry facts
    let findings = Findings.findUnknownGovernedPaths facts report
    let result = Route.select registry report findings
    let selectedGates = result.SelectedGates |> List.map (fun sg -> sg.Gate)
    let cacheReport = expectedCacheReport selectedGates (baseHeadOfSnap snap)
    let outcomes = expectedOutcomes files selectedGates
    // F23: mirror the command's edge-side product-surface classification (default profile / standard).
    let profile = facts.Policy |> Option.map (fun p -> p.DefaultProfile) |> Option.defaultValue (ProfileId "standard")
    let classifications = FS.GG.Governance.ProductSurfaces.ProductSurfaces.classify facts report profile
    GatesJson.ofGateRegistry registry, RouteJson.ofRouteResultWithProductSurfaces result (Some cacheReport) outcomes classifications

// ── Capturing write/output edges ──

type Capture =
    { mutable Writes: (Loop.ArtifactKind * string * string) list
      mutable Emits: string list }

let newCapture () : Capture = { Writes = []; Emits = [] }

/// A capturing ArtifactWriter. We cannot see the `ArtifactKind` from the writer signature
/// (path/content only), so we tag writes by matching the request's GatesOut/RouteOut paths.
let capturingWriter (cap: Capture) (failPaths: Set<string>) (gatesOut: string) (routeOut: string) : Interpreter.ArtifactWriter =
    fun path content ->
        if Set.contains path failPaths then
            Error "no space left on device"
        else
            let kind = if path = gatesOut then Loop.GatesArtifact else Loop.RouteArtifact
            ignore routeOut
            cap.Writes <- cap.Writes @ [ kind, path, content ]
            Ok()

let capturingSink (cap: Capture) : Interpreter.OutputSink =
    fun text -> cap.Emits <- cap.Emits @ [ text ]

/// Assemble faked Interpreter.Ports from a catalog map, a git port, and a capture (no failing writes).
/// The F046 sensing ports default to the fully-sensing fake sensor + an absent (⇒ empty) store; the F052
/// execution port defaults to the all-pass fake.
let fakePorts (files: Map<string, string>) (g: GitPort) (cap: Capture) (req: Loop.RunRequest) : Interpreter.Ports =
    { Files = readerOf files
      Git = portsGit g
      Freshness = fakeSensor
      Store = absentStoreReader
      Write = capturingWriter cap Set.empty req.GatesOut req.RouteOut
      Out = capturingSink cap
      Execute = fakeExecPort
      SenseCapability = plainCapability
      RenderReport = noRichRender
      Handoffs = fun _ -> [] }

/// Faked ports with explicit F046 sensing ports (for the US3 degrade probes).
let fakePortsWith (files: Map<string, string>) (g: GitPort) (sensor: FreshnessSensing.FreshnessSensor) (store: FreshnessSensing.StoreReader) (cap: Capture) (req: Loop.RunRequest) : Interpreter.Ports =
    { Files = readerOf files
      Git = portsGit g
      Freshness = sensor
      Store = store
      Write = capturingWriter cap Set.empty req.GatesOut req.RouteOut
      Out = capturingSink cap
      Execute = fakeExecPort
      SenseCapability = plainCapability
      RenderReport = noRichRender
      Handoffs = fun _ -> [] }

/// Faked ports whose ArtifactWriter fails for the given paths (the unwritable-output case).
let fakePortsFailingWrites (files: Map<string, string>) (g: GitPort) (cap: Capture) (failPaths: Set<string>) (req: Loop.RunRequest) : Interpreter.Ports =
    { Files = readerOf files
      Git = portsGit g
      Freshness = fakeSensor
      Store = absentStoreReader
      Write = capturingWriter cap failPaths req.GatesOut req.RouteOut
      Out = capturingSink cap
      Execute = fakeExecPort
      SenseCapability = plainCapability
      RenderReport = noRichRender
      Handoffs = fun _ -> [] }

/// Faked ports with an explicit execution port + sensing ports (for the US1/US2/US4 execution scenarios).
let fakePortsExec (files: Map<string, string>) (g: GitPort) (sensor: FreshnessSensing.FreshnessSensor) (store: FreshnessSensing.StoreReader) (exec: ExecutionPort) (cap: Capture) (req: Loop.RunRequest) : Interpreter.Ports =
    { Files = readerOf files
      Git = portsGit g
      Freshness = sensor
      Store = store
      Write = capturingWriter cap Set.empty req.GatesOut req.RouteOut
      Out = capturingSink cap
      Execute = exec
      SenseCapability = plainCapability
      RenderReport = noRichRender
      Handoffs = fun _ -> [] }
let writtenOf (cap: Capture) (kind: Loop.ArtifactKind) : (string * string) option =
    cap.Writes
    |> List.tryPick (fun (k, p, c) -> if k = kind then Some(p, c) else None)

// ── Request builders ──

let requestFor (scope: Loop.ScopeSelector) (format: Loop.OutputFormat) : Loop.RunRequest =
    { Repo = "."
      Scope = scope
      Format = format
      GatesOut = ".fsgg/gates.json"
      RouteOut = "readiness/route.json"
      StorePath = "readiness/evidence-reuse.json"
      PersistStore = false
      ExplicitPlain = false
      Watch = false }
let withTempRepo (body: string -> 'a) : 'a =
    let dir = Path.Combine(Path.GetTempPath(), "fsgg-route-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory dir |> ignore
    try
        git dir [ "init"; "-q"; "-b"; "main" ] |> ignore
        git dir [ "config"; "user.email"; "fixture@fsgg.test" ] |> ignore
        git dir [ "config"; "user.name"; "FSGG Fixture" ] |> ignore
        git dir [ "config"; "commit.gpgsign"; "false" ] |> ignore
        // A real catalog on disk.
        for KeyValue(name, content) in validCatalog do
            writeFile dir (".fsgg/" + name) content
        writeFile dir "src/Lib/Thing.fs" "module Thing\nlet v = 1\n"
        git dir [ "add"; "-A" ] |> ignore
        git dir [ "commit"; "-qm"; "base" ] |> ignore
        // A committed edit under src/ (so DefaultRange/HEAD~1 senses a package-api change).
        writeFile dir "src/Lib/Thing.fs" "module Thing\nlet v = 2\n"
        git dir [ "add"; "-A" ] |> ignore
        git dir [ "commit"; "-qm"; "head" ] |> ignore
        body dir
    finally
        try Directory.Delete(dir, true) with _ -> ()

/// The store value `fsgg route --since HEAD~1` over a REAL temp repo persists from a given loaded store:
/// the F047 pipeline over the grown store (each selected command-gate captured at the repo's root via the
/// all-pass fake execution port). Used by the persistence-edge round-trip assertions (the store now GROWS).
let expectedPersistedRepo (dir: string) (loaded: ReuseStore) : ReuseStore =
    let opts = sinceOpts "HEAD~1"
    let candidates = candidatesOfRepo dir opts
    let selectedGates = selectedGatesFor validCatalog candidates
    let baseHead = baseHeadOfSnap (Some(snapshotOfRepo dir opts))
    // The edge run uses the REAL freshness sensor (realPorts), so the capture keys are the real candidate
    // inputs — recompute the grown store with that same sensor + the deterministic all-pass execution port.
    persistedValue (expectedGrownStoreAt dir fakeExecPort (FreshnessSensing.realSensor dir) validCatalog loaded selectedGates baseHead)
