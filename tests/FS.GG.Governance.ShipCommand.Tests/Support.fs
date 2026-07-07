module FS.GG.Governance.ShipCommand.Tests.Support

// Faked-port + real-temp-git support helpers (Principle V — real inputs/outputs through faked edges,
// never mocks of the cores). The in-memory FileReader serves a literal `.fsgg` catalog; the in-memory
// GitPort returns canned read-only git output the REAL Snapshot.assemble parses; the capturing
// ArtifactWriter/OutputSink record what the interpreter writes/emits so tests can compare bytes
// against the genuine F025 projection of the F024 rollup. A `withTempRepo` helper drives REAL git for
// the one end-to-end proof.

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
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Ship
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.AuditJson
open FS.GG.Governance.ShipCommand
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
// ShipCommand edge while `FS.GG.Governance.Snapshot.Interpreter.senseSnapshot` / `GitPort` / `Ports` /
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

let fakeSenseEnvironment: unit -> EnvironmentClass = fun () -> Local // SYNTHETIC: fixed env class
let fakeSenseBuilder: unit -> FS.GG.Governance.Provenance.Model.BuilderIdentity =
    fun () -> FS.GG.Governance.Provenance.Model.BuilderIdentity "fsgg-test" // SYNTHETIC: fixed builder id

// F070: the default fake currency sense — no stale views (unconfigured). The currency E2E tests inject a
// real/synthetic port that returns findings; everything else inherits this empty default ⇒ byte-identical.
let fakeSenseViewCurrency: string -> FS.GG.Governance.CurrencyEnforcement.CurrencyEnforcement.CurrencyFinding list =
    fun _ -> []
let gitSrcChange: GitPort = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]

/// A workflow change under work/** (selects the High-cost `audit` gate — the over-budget probe for F25 wiring).
let gitWorkChange: GitPort = gitWithChanges [ 'M', "work/flow/Step.fs" ]
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
let resultAndDecisionOf (files: Map<string, string>) (candidates: GovernedPath list) (mode: RunMode) (profile: Profile) =
    let facts = factsOf files
    let report = Routing.route facts candidates
    let registry = Gates.buildRegistry facts
    let findings = Findings.findUnknownGovernedPaths facts report
    let result = Route.select registry report findings
    result, Ship.rollup result mode profile

let decisionOf (files: Map<string, string>) (candidates: GovernedPath list) (mode: RunMode) (profile: Profile) : ShipDecision =
    snd (resultAndDecisionOf files candidates mode profile)
let fakeExecPort: ExecutionPort = fakeExecPortExiting 1

/// A passing fake port (exit 0): a passing command-gate is RELOCATED to `Passing` and the verdict recomputed.
let fakeExecPortPass: ExecutionPort = fakeExecPortExiting 0

/// A call-counting fake port (proves reuse SKIPS execution on a second run — the spawn-count assertion).
type ExecCounter = { mutable Calls: int }

let countingExecPort (counter: ExecCounter) (code: int) : ExecutionPort =
    fun command ->
        counter.Calls <- counter.Calls + 1
        fakeExecPortExiting code command

/// Given a model + the effects it just emitted, run any `ExecuteGates` effect through the port (mirroring the
/// interpreter edge) and feed the `GatesExecuted` records back into `update`. Returns the next (model, effects).
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
    | Error _ -> CacheEligibility.evaluate [] store

let expectedCacheReport (selectedGates: Gate list) (baseHead: Revision option * Revision option) : CacheEligibilityReport =
    expectedCacheReportWith fakeSensor EvidenceReuse.empty selectedGates baseHead
let expectedOutcomes (files: Map<string, string>) (selectedGates: Gate list) : (GateId * GateOutcome) list =
    expectedOutcomesWith fakeExecPort files selectedGates

// F25 wiring (064): the gates the host's budget filter DEFERS (over-budget must-recompute) for a given
// (mode, profile). Computed with the REAL `CostBudget.Budget.decide` core (never a reimplementation): over the
// empty/absent store these helpers use, every gate is `MustRecompute NoPriorEvidence`, so a gate is deferred
// iff its `Cost` exceeds `budgetFor profile mode`. Under the DEFAULT Gate/Standard (Medium) ceiling this set is
// empty, so every default golden stays byte-identical; only a tight lever set (e.g. Inner ⇒ Cheap) defers.
let budgetDeferredIds (selectedGates: Gate list) (mode: RunMode) (profile: Profile) : Set<string> =
    let budget = FS.GG.Governance.CostBudget.Budget.budgetFor profile mode

    let candidates: FS.GG.Governance.CostBudget.Model.CandidateCost list =
        selectedGates
        |> List.map (fun g ->
            { Gate = g.Id
              Cost = g.Cost
              Verdict = MustRecompute NoPriorEvidence
              Review = FS.GG.Governance.CostBudget.Model.Deterministic })

    FS.GG.Governance.CostBudget.Budget.decide budget mode candidates
    |> FS.GG.Governance.CostBudget.Budget.overBudget
    |> List.map (fst >> gateIdValue)
    |> Set.ofList

let private applyDeferrals (deferred: Set<string>) (outcomes: (GateId * GateOutcome) list) : (GateId * GateOutcome) list =
    outcomes
    |> List.map (fun (gid, o) ->
        if Set.contains (gateIdValue gid) deferred then
            gid,
            { GateId = gid
              Disposition = NotExecuted }
        else
            gid, o)

/// The relocated `ShipDecision` the command carries: `Ship.rollup` (verbatim) then F052 `applyExecution` over
/// the gates that PASSED on the given execution port (data-model §verdict relocation). F25 wiring (064): an
/// over-budget gate is deferred (NotExecuted) before relocation, matching the host's budget filter.
let relocatedDecisionWith (port: ExecutionPort) (files: Map<string, string>) (candidates: GovernedPath list) (mode: RunMode) (profile: Profile) : ShipDecision * (GateId * GateOutcome) list =
    let result, decision = resultAndDecisionOf files candidates mode profile
    let selectedGates = result.SelectedGates |> List.map (fun sg -> sg.Gate)
    let deferred = budgetDeferredIds selectedGates mode profile
    let outcomes = expectedOutcomesWith port files selectedGates |> applyDeferrals deferred

    let passedIds =
        outcomes
        |> List.choose (fun (gid, o) -> if isPassing o.Disposition then Some gid else None)
        |> Set.ofList

    Loop.applyExecution passedIds decision, outcomes

/// The genuine F025 `audit.json` bytes the command persists over a given execution port: the F052-relocated
/// decision + the LIVE cache report + the per-gate execution embed (D3/D6).
let auditExpectedWith (port: ExecutionPort) (files: Map<string, string>) (candidates: GovernedPath list) (mode: RunMode) (profile: Profile) (snap: RepoSnapshot option) : string =
    let result, _ = resultAndDecisionOf files candidates mode profile
    let selectedGates = result.SelectedGates |> List.map (fun sg -> sg.Gate)
    let cacheReport = expectedCacheReport selectedGates (baseHeadOfSnap snap)
    let relocated, outcomes = relocatedDecisionWith port files candidates mode profile
    AuditJson.ofShipDecision relocated (Some cacheReport) outcomes

/// The standard (default fail fake port) expected audit document.
let auditExpected (files: Map<string, string>) (candidates: GovernedPath list) (mode: RunMode) (profile: Profile) (snap: RepoSnapshot option) : string =
    auditExpectedWith fakeExecPort files candidates mode profile snap

// ── Capturing write/output edges ──

type Capture =
    { mutable Writes: (Loop.ArtifactKind * string * string) list
      mutable Emits: string list }

let newCapture () : Capture = { Writes = []; Emits = [] }

/// A capturing ArtifactWriter. The only artifact is `audit.json`, so every write is `AuditArtifact`;
/// a path in `failPaths` returns `Error` (the unwritable-output case) and records NOTHING (no partial
/// artifact). `auditOut` is accepted for symmetry with the request and to make the tag explicit.
let capturingWriter (cap: Capture) (failPaths: Set<string>) (auditOut: string) : Interpreter.ArtifactWriter =
    fun path content ->
        if Set.contains path failPaths then
            Error "no space left on device"
        else
            ignore auditOut
            cap.Writes <- cap.Writes @ [ Loop.AuditArtifact, path, content ]
            Ok()

let capturingSink (cap: Capture) : Interpreter.OutputSink =
    fun text -> cap.Emits <- cap.Emits @ [ text ]

/// Assemble faked Interpreter.Ports from a catalog map, a git port, and a capture (no failing writes).
/// The F046 sensing ports default to the fully-sensing fake sensor + an absent (⇒ empty) store.
let fakePorts (files: Map<string, string>) (g: GitPort) (cap: Capture) (req: Loop.RunRequest) : Interpreter.Ports =
    { Files = readerOf files
      Git = portsGit g
      Freshness = fakeSensor
      Store = absentStoreReader
      Write = capturingWriter cap Set.empty req.AuditOut
      Out = capturingSink cap
      Execute = fakeExecPort
      SenseCapability = plainCapability
      RenderReport = noRichRender
      SenseEnvironment = fakeSenseEnvironment
      SenseBuilder = fakeSenseBuilder
      SenseViewCurrency = fakeSenseViewCurrency
      Handoffs = fun _ -> [] }

/// Faked ports with explicit F046 sensing ports (for the US3 degrade probes).
let fakePortsWith (files: Map<string, string>) (g: GitPort) (sensor: FreshnessSensing.FreshnessSensor) (store: FreshnessSensing.StoreReader) (cap: Capture) (req: Loop.RunRequest) : Interpreter.Ports =
    { Files = readerOf files
      Git = portsGit g
      Freshness = sensor
      Store = store
      Write = capturingWriter cap Set.empty req.AuditOut
      Out = capturingSink cap
      Execute = fakeExecPort
      SenseCapability = plainCapability
      RenderReport = noRichRender
      SenseEnvironment = fakeSenseEnvironment
      SenseBuilder = fakeSenseBuilder
      SenseViewCurrency = fakeSenseViewCurrency
      Handoffs = fun _ -> [] }

/// Faked ports whose ArtifactWriter fails for the given paths (the unwritable-output case).
let fakePortsFailingWrites (files: Map<string, string>) (g: GitPort) (cap: Capture) (failPaths: Set<string>) (req: Loop.RunRequest) : Interpreter.Ports =
    { Files = readerOf files
      Git = portsGit g
      Freshness = fakeSensor
      Store = absentStoreReader
      Write = capturingWriter cap failPaths req.AuditOut
      Out = capturingSink cap
      Execute = fakeExecPort
      SenseCapability = plainCapability
      RenderReport = noRichRender
      SenseEnvironment = fakeSenseEnvironment
      SenseBuilder = fakeSenseBuilder
      SenseViewCurrency = fakeSenseViewCurrency
      Handoffs = fun _ -> [] }

/// Faked ports with an explicit execution port + sensing ports (for the US1/US2/US4 execution scenarios).
let fakePortsExec (files: Map<string, string>) (g: GitPort) (sensor: FreshnessSensing.FreshnessSensor) (store: FreshnessSensing.StoreReader) (exec: ExecutionPort) (cap: Capture) (req: Loop.RunRequest) : Interpreter.Ports =
    { Files = readerOf files
      Git = portsGit g
      Freshness = sensor
      Store = store
      Write = capturingWriter cap Set.empty req.AuditOut
      Out = capturingSink cap
      Execute = exec
      SenseCapability = plainCapability
      RenderReport = noRichRender
      SenseEnvironment = fakeSenseEnvironment
      SenseBuilder = fakeSenseBuilder
      SenseViewCurrency = fakeSenseViewCurrency
      Handoffs = fun _ -> [] }
let writtenAudit (cap: Capture) : (string * string) option =
    cap.Writes |> List.tryPick (fun (_, p, c) -> if p = "readiness/audit.json" then Some(p, c) else None)

// F25 wiring (064): the capturing writer is a path→content port (it cannot see the ArtifactKind), so sidecar
// writes are located by their default path.
let writtenAt (path: string) (cap: Capture) : string option =
    cap.Writes |> List.tryPick (fun (_, p, c) -> if p = path then Some c else None)

let writtenCostBudget (cap: Capture) : string option = writtenAt "readiness/cost-budget.json" cap
let writtenProvenance (cap: Capture) : string option = writtenAt "readiness/provenance.json" cap

// ── Request builders ──

/// The canonical protected-branch request: `--mode gate --profile standard`, audit at the default
/// `readiness/audit.json` (research D5/D7).
let requestFor (scope: Loop.ScopeSelector) (format: Loop.OutputFormat) : Loop.RunRequest =
    { Repo = "."
      Scope = scope
      Mode = Gate
      Profile = Standard
      Format = format
      AuditOut = "readiness/audit.json"
      StorePath = "readiness/evidence-reuse.json"
      PersistStore = false
      ExplicitPlain = false
      CostBudgetOut = "readiness/cost-budget.json"
      ProvenanceOut = "readiness/provenance.json"
      DryRun = false }

/// A request under an explicit mode/profile lever set (for the two-lever-set / no-hide proofs).
let requestForLevers (scope: Loop.ScopeSelector) (format: Loop.OutputFormat) (mode: RunMode) (profile: Profile) : Loop.RunRequest =
    { requestFor scope format with Mode = mode; Profile = profile }
let withTempRepo (body: string -> 'a) : 'a =
    let dir = Path.Combine(Path.GetTempPath(), "fsgg-ship-" + Guid.NewGuid().ToString("N"))
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

// ── F052 grown-store helpers (the store now GROWS as the command captures each executed gate's evidence) ──

/// The GROWN store the command persists: fold F049 `capture` over the loaded store for each selected gate
/// that declares a command and is NOT reused (mirrors the command's classify+capture at repoRoot `repoRoot`).
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
                match tooling |> Option.map (fun t -> Plan.commandFor repoRoot t g) with
                | None
                | Some(Error _) -> s
                | Some(Ok cmd) ->
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

/// The store VALUE the command persists (F047 prune → retain over the grown store), as it re-reads.
let persistedValue (grown: ReuseStore) : ReuseStore =
    grown
    |> EvidenceReuseStore.prune
    |> EvidenceReuseStore.retain EvidenceReuseStore.defaultRetentionBound
let expectedPersistedRepo (dir: string) (loaded: ReuseStore) : ReuseStore =
    let opts = sinceOpts "HEAD~1"
    let candidates = candidatesOfRepo dir opts
    let selectedGates = selectedGatesFor validCatalog candidates
    let baseHead = baseHeadOfSnap (Some(snapshotOfRepo dir opts))
    persistedValue (expectedGrownStoreAt dir fakeExecPort (FreshnessSensing.realSensor dir) validCatalog loaded selectedGates baseHead)
