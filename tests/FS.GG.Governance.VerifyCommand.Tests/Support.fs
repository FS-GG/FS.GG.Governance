module FS.GG.Governance.VerifyCommand.Tests.Support

// Faked-port + real-temp-git support helpers (Principle V — real inputs/outputs through faked edges, never
// mocks of the cores). The in-memory FileReader serves a literal `.fsgg` catalog; the in-memory GitPort
// returns canned read-only git output the REAL Snapshot.assemble parses; the capturing ArtifactWriter/
// OutputSink record what the interpreter writes/emits so tests can compare bytes against the genuine F056
// projection (VerifyJson.ofVerifyDecision) of the F024 rollup (Ship.rollup at RunMode.Verify). A
// `withTempRepo` helper drives REAL git for the end-to-end proofs.

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
open FS.GG.Governance.VerifyJson
open FS.GG.Governance.VerifyCommand
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
let gitSrcChange: GitPort = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]

/// A workflow change under work/** (selects the High-cost `audit` gate — the over-budget probe for F25 wiring).
let gitWorkChange: GitPort = gitWithChanges [ 'M', "work/flow/Step.fs" ]
let resultAndDecisionOf (files: Map<string, string>) (candidates: GovernedPath list) (mode: RunMode) (profile: Profile) =
    let facts = factsOf files
    let report = Routing.route facts candidates
    let registry = Gates.buildRegistry facts
    let findings = Findings.findUnknownGovernedPaths facts report
    let result = Route.select registry report findings
    result, Ship.rollup result mode profile

let decisionOf (files: Map<string, string>) (candidates: GovernedPath list) (mode: RunMode) (profile: Profile) : ShipDecision =
    snd (resultAndDecisionOf files candidates mode profile)
let fakeExecPortFail: ExecutionPort = fakeExecPortExiting 1

/// A passing fake port (exit 0): a passing command-gate is RELOCATED to `Passing` and the verdict recomputed.
let fakeExecPortPass: ExecutionPort = fakeExecPortExiting 0

/// An "uncertain"/unrecoverable fake port (exit 125): a non-pass, non-standard code. A blocking gate is NOT
/// relocated and is never coerced to passing (FR-005).
let fakeExecPortUncertain: ExecutionPort = fakeExecPortExiting 125

type ExecCounter = { mutable Calls: int }

let countingExecPort (counter: ExecCounter) (code: int) : ExecutionPort =
    fun command ->
        counter.Calls <- counter.Calls + 1
        fakeExecPortExiting code command
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
    expectedOutcomesWith fakeExecPortFail files selectedGates

// F25 wiring (064): the gates the host's budget filter DEFERS (over-budget must-recompute) for a given
// (mode, profile). Computed with the REAL `CostBudget.Budget.decide` core (never a reimplementation): over the
// empty/absent store these helpers use, every gate is `MustRecompute NoPriorEvidence`, so a gate is deferred
// iff its `Cost` exceeds `budgetFor profile mode`. Verify is fixed at `RunMode.Verify` (High ceiling), so under
// the default Standard profile this set is empty and every golden stays byte-identical; `--profile Light`
// floors the ceiling to `Cheap`, deferring an expensive must-recompute gate.
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
              Disposition = NotExecuted
              ExitCode = None
              Passed = None }
        else
            gid, o)

let relocatedDecisionWith (port: ExecutionPort) (files: Map<string, string>) (candidates: GovernedPath list) (mode: RunMode) (profile: Profile) : ShipDecision * (GateId * GateOutcome) list =
    let result, decision = resultAndDecisionOf files candidates mode profile
    let selectedGates = result.SelectedGates |> List.map (fun sg -> sg.Gate)
    let deferred = budgetDeferredIds selectedGates mode profile
    let outcomes = expectedOutcomesWith port files selectedGates |> applyDeferrals deferred

    let passedIds =
        outcomes
        |> List.choose (fun (gid, o) -> if o.Passed = Some true then Some gid else None)
        |> Set.ofList

    Loop.applyExecution passedIds decision, outcomes

/// The genuine F056 `verify.json` bytes the command persists over a given execution port: the F052-relocated
/// decision + the LIVE cache report + the per-gate execution embed.
let verifyExpectedWith (port: ExecutionPort) (files: Map<string, string>) (candidates: GovernedPath list) (profile: Profile) (snap: RepoSnapshot option) : string =
    let result, _ = resultAndDecisionOf files candidates Verify profile
    let selectedGates = result.SelectedGates |> List.map (fun sg -> sg.Gate)
    let cacheReport = expectedCacheReport selectedGates (baseHeadOfSnap snap)
    let relocated, outcomes = relocatedDecisionWith port files candidates Verify profile
    VerifyJson.ofVerifyDecision relocated (Some cacheReport) outcomes

let verifyExpected (files: Map<string, string>) (candidates: GovernedPath list) (profile: Profile) (snap: RepoSnapshot option) : string =
    verifyExpectedWith fakeExecPortFail files candidates profile snap

// ── Capturing write/output edges ──

type Capture =
    { mutable Writes: (Loop.ArtifactKind * string * string) list
      mutable Emits: string list }

let newCapture () : Capture = { Writes = []; Emits = [] }

let capturingWriter (cap: Capture) (failPaths: Set<string>) : Interpreter.ArtifactWriter =
    fun path content ->
        if Set.contains path failPaths then
            Error "no space left on device"
        else
            cap.Writes <- cap.Writes @ [ Loop.VerifyArtifact, path, content ]
            Ok()

let capturingSink (cap: Capture) : Interpreter.OutputSink =
    fun text -> cap.Emits <- cap.Emits @ [ text ]

// 065 (US3): the legacy verify fixtures declare NO `.fsgg/release.yml`, so `SenseReleasePreview` reads
// `None` and the preview is never assembled — this stub is provably never invoked (the US3 preview test
// supplies its own declaration + sense). Loud failure if that invariant is ever violated.
let fakeSenseRelease
    : FS.GG.Governance.ReleaseFactsSensing.Model.SourceLayout
        -> FS.GG.Governance.ReleaseFactsSensing.Model.ReleaseExpectations
        -> FS.GG.Governance.ReleaseFactsSensing.Model.SensedRelease =
    fun _ _ -> failwith "fakeSenseRelease: no .fsgg/release.yml in this fixture"

// 067: the default surface-sense port for the legacy faked-port fixtures. SYNTHETIC: returns no findings (the
// legacy catalogs declare no product surfaces, so the real sense would also return [] — this stub just spares
// the legacy tests the real filesystem read). Tests that exercise surface findings either inject a real
// temp-tree sense (`realSurfaceSense`) or a hand-built advisory port (disclosed at the use site).
let fakeSenseSurfaces
    : FS.GG.Governance.ProductSurfaces.Model.ProductSurfaceReport
        -> FS.GG.Governance.SurfaceChecks.Model.SurfaceFinding list =
    fun _ -> [] // SYNTHETIC: no product surfaces in the legacy catalogs ⇒ empty, matching the real sense

// F070: the default fake currency sense — no stale generated views (unconfigured / no refresh.yml). The
// currency E2E tests inject a real port; everything else inherits this empty default ⇒ byte-identical.
let fakeSenseViewCurrency: string -> FS.GG.Governance.CurrencyEnforcement.CurrencyEnforcement.CurrencyFinding list =
    fun _ -> []

// 067: the GENUINE read-only surface sense over a real temp tree — NOT synthetic. Lifted from
// `Interpreter.realPorts` so the E2E proofs drive the exact production sense (real package/docs/skill/design
// file reads, read-only package port) while git/exec stay faked. Reuses the real port without growing the
// public surface (the field is already on the `realPorts` record).
let realSurfaceSense (repo: string) : FS.GG.Governance.ProductSurfaces.Model.ProductSurfaceReport -> FS.GG.Governance.SurfaceChecks.Model.SurfaceFinding list =
    (FS.GG.Governance.VerifyCommand.Interpreter.realPorts repo).SenseSurfaces

/// Assemble faked Interpreter.Ports from a catalog map, a git port, and a capture (no failing writes). The
/// F046 sensing ports default to the fully-sensing fake sensor + an absent (⇒ empty) store; the F052 exec
/// port defaults to the failing port (advisory under Standard, blocking under Strict).
let fakePorts (files: Map<string, string>) (g: GitPort) (cap: Capture) : Interpreter.Ports =
    { Files = readerOf files
      Git = portsGit g
      Freshness = fakeSensor
      Store = absentStoreReader
      Write = capturingWriter cap Set.empty
      Out = capturingSink cap
      Execute = fakeExecPortFail
      SenseCapability = plainCapability
      RenderReport = noRichRender
      SenseEnvironment = fakeSenseEnvironment
      SenseBuilder = fakeSenseBuilder
      SenseRelease = fakeSenseRelease
      SenseSurfaces = fakeSenseSurfaces
      SenseViewCurrency = fakeSenseViewCurrency
      Handoffs = fun _ -> [] }

let fakePortsWith (files: Map<string, string>) (g: GitPort) (sensor: FreshnessSensing.FreshnessSensor) (store: FreshnessSensing.StoreReader) (cap: Capture) : Interpreter.Ports =
    { Files = readerOf files
      Git = portsGit g
      Freshness = sensor
      Store = store
      Write = capturingWriter cap Set.empty
      Out = capturingSink cap
      Execute = fakeExecPortFail
      SenseCapability = plainCapability
      RenderReport = noRichRender
      SenseEnvironment = fakeSenseEnvironment
      SenseBuilder = fakeSenseBuilder
      SenseRelease = fakeSenseRelease
      SenseSurfaces = fakeSenseSurfaces
      SenseViewCurrency = fakeSenseViewCurrency
      Handoffs = fun _ -> [] }

let fakePortsFailingWrites (files: Map<string, string>) (g: GitPort) (cap: Capture) (failPaths: Set<string>) : Interpreter.Ports =
    { Files = readerOf files
      Git = portsGit g
      Freshness = fakeSensor
      Store = absentStoreReader
      Write = capturingWriter cap failPaths
      Out = capturingSink cap
      Execute = fakeExecPortFail
      SenseCapability = plainCapability
      RenderReport = noRichRender
      SenseEnvironment = fakeSenseEnvironment
      SenseBuilder = fakeSenseBuilder
      SenseRelease = fakeSenseRelease
      SenseSurfaces = fakeSenseSurfaces
      SenseViewCurrency = fakeSenseViewCurrency
      Handoffs = fun _ -> [] }

let fakePortsExec (files: Map<string, string>) (g: GitPort) (sensor: FreshnessSensing.FreshnessSensor) (store: FreshnessSensing.StoreReader) (exec: ExecutionPort) (cap: Capture) : Interpreter.Ports =
    { Files = readerOf files
      Git = portsGit g
      Freshness = sensor
      Store = store
      Write = capturingWriter cap Set.empty
      Out = capturingSink cap
      Execute = exec
      SenseCapability = plainCapability
      RenderReport = noRichRender
      SenseEnvironment = fakeSenseEnvironment
      SenseBuilder = fakeSenseBuilder
      SenseRelease = fakeSenseRelease
      SenseSurfaces = fakeSenseSurfaces
      SenseViewCurrency = fakeSenseViewCurrency
      Handoffs = fun _ -> [] }
let writtenVerify (cap: Capture) : (string * string) option =
    cap.Writes |> List.tryPick (fun (_, p, c) -> if p = "readiness/verify.json" then Some(p, c) else None)

// F25 wiring (064): the capturing writer is a path→content port (it cannot see the ArtifactKind), so sidecar
// writes are located by their default path.
let writtenAt (path: string) (cap: Capture) : string option =
    cap.Writes |> List.tryPick (fun (_, p, c) -> if p = path then Some c else None)

let writtenCostBudget (cap: Capture) : string option = writtenAt "readiness/cost-budget.json" cap
let writtenProvenance (cap: Capture) : string option = writtenAt "readiness/provenance.json" cap

// ── Request builders ──

/// The canonical pre-PR request: `--profile standard`, verify at the default `readiness/verify.json`.
let requestFor (scope: Loop.ScopeSelector) (format: Loop.OutputFormat) : Loop.RunRequest =
    { Repo = "."
      Scope = scope
      Profile = Standard
      Format = format
      VerifyOut = "readiness/verify.json"
      StorePath = "readiness/evidence-reuse.json"
      PersistStore = false
      ExplicitPlain = false
      CostBudgetOut = "readiness/cost-budget.json"
      ProvenanceOut = "readiness/provenance.json" }

/// A request under an explicit profile (for the blocking / uncertain scenarios at Strict).
let requestForProfile (scope: Loop.ScopeSelector) (format: Loop.OutputFormat) (profile: Profile) : Loop.RunRequest =
    { requestFor scope format with Profile = profile }
let withTempRepo (body: string -> 'a) : 'a =
    let dir = Path.Combine(Path.GetTempPath(), "fsgg-verify-" + Guid.NewGuid().ToString("N"))
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
        try Directory.Delete(dir, true) with _ -> ()
let surfaceE2EPorts (dir: string) (exec: ExecutionPort) (cap: Capture) : Interpreter.Ports =
    { FS.GG.Governance.VerifyCommand.Interpreter.realPorts dir with
        Execute = exec
        Write = capturingWriter cap Set.empty
        Out = capturingSink cap }

// A package-surface catalog: one declared `kind: package` surface over `src/**/*.fsi` (with an evidenceTag)
// plus a single block-on-ship `build` gate. A drifted `.fsi` ⇒ a `package.baseline-drift` BLOCKING surface
// finding; the gate exercises the executed projection path so the fold runs over a relocated decision.
let surfaceCatalog: Map<string, string> =
    Map
        [ "project.yml", projectYml
          "capabilities.yml",
          yaml """
schemaVersion: 2
domains:
  - package-api
pathMap:
  - glob: "src/**"
    capability: package-api
surfaces:
  - id: pkg-surface
    kind: package
    paths: ["src/**/*.fsi"]
    owner: platform
    maturity: block-on-ship
    evidenceTag: api-contract
checks:
  - id: build
    domain: package-api
    command: dotnet-build
    owner: platform
    cost: medium
    environment: local-or-ci
    maturity: block-on-ship
"""
          "policy.yml", policyYml
          "tooling.yml", toolingYml ]

// A no-product-surface catalog over a real temp tree: the byte-identity anchor (US2). Reuses `validCatalog`
// (its only surface is `protected` ⇒ not a product domain ⇒ no requests ⇒ no findings ⇒ `surfaceChecks`
// omitted).
let private writeCatalog (dir: string) (catalog: Map<string, string>) : unit =
    for KeyValue(name, content) in catalog do
        writeFile dir (".fsgg/" + name) content

// Create a disposable temp git repo declaring a PACKAGE surface whose committed baseline DRIFTS from the
// head `.fsi` (a real on-disk drift the real package sensor detects): base commits `src/Api.fsi` + a
// deliberately-stale `src/Api.fsi.baseline`; head edits `src/Api.fsi` so the regenerated token set diverges
// from the committed baseline. The changed `.fsi` is the routed/classified path. `body` runs against the path.
let withDriftedPackageRepo (body: string -> 'a) : 'a =
    let dir = Path.Combine(Path.GetTempPath(), "fsgg-verify-surface-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory dir |> ignore
    try
        git dir [ "init"; "-q"; "-b"; "main" ] |> ignore
        git dir [ "config"; "user.email"; "fixture@fsgg.test" ] |> ignore
        git dir [ "config"; "user.name"; "FSGG Fixture" ] |> ignore
        git dir [ "config"; "commit.gpgsign"; "false" ] |> ignore
        writeCatalog dir surfaceCatalog
        // base: a surface source + a committed baseline that already disagrees with it on one ghost token.
        writeFile dir "src/Api.fsi" "val foo: int\n"
        writeFile dir "src/Api.fsi.baseline" "ghost-token-only\n"
        git dir [ "add"; "-A" ] |> ignore
        git dir [ "commit"; "-qm"; "base" ] |> ignore
        // head: edit the surface so the regenerated tokens diverge further from the stale baseline (drift) and
        // `src/Api.fsi` shows up as the changed/routed path in base..head.
        writeFile dir "src/Api.fsi" "val foo: int -> string\n"
        git dir [ "add"; "-A" ] |> ignore
        git dir [ "commit"; "-qm"; "head" ] |> ignore
        body dir
    finally
        try Directory.Delete(dir, true) with _ -> ()

// Create a disposable temp git repo declaring NO product surface (the `validCatalog` no-surface case) with a
// real two-commit `.fs` edit. The byte-identity anchor for SC-002.
let withNoSurfaceRepo (body: string -> 'a) : 'a =
    let dir = Path.Combine(Path.GetTempPath(), "fsgg-verify-nosurface-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory dir |> ignore
    try
        git dir [ "init"; "-q"; "-b"; "main" ] |> ignore
        git dir [ "config"; "user.email"; "fixture@fsgg.test" ] |> ignore
        git dir [ "config"; "user.name"; "FSGG Fixture" ] |> ignore
        git dir [ "config"; "commit.gpgsign"; "false" ] |> ignore
        writeCatalog dir validCatalog
        writeFile dir "src/Lib/Thing.fs" "module Thing\nlet v = 1\n"
        git dir [ "add"; "-A" ] |> ignore
        git dir [ "commit"; "-qm"; "base" ] |> ignore
        writeFile dir "src/Lib/Thing.fs" "module Thing\nlet v = 2\n"
        git dir [ "add"; "-A" ] |> ignore
        git dir [ "commit"; "-qm"; "head" ] |> ignore
        body dir
    finally
        try Directory.Delete(dir, true) with _ -> ()

// A request rooted at a real temp repo (so the default artifact paths sit under it). The capturing writer
// records the bytes; nothing is written to disk.
let requestForRepo (dir: string) (scope: Loop.ScopeSelector) (format: Loop.OutputFormat) : Loop.RunRequest =
    { requestFor scope format with
        Repo = dir
        VerifyOut = dir + "/readiness/verify.json"
        StorePath = dir + "/readiness/evidence-reuse.json"
        CostBudgetOut = dir + "/readiness/cost-budget.json"
        ProvenanceOut = dir + "/readiness/provenance.json" }

// ── 067: hand-built (SYNTHETIC, disclosed) surface findings for the verdict-fold proofs ──
// The real domain sensors emit only Blocking findings from disk today (the lone Advisory finding,
// `docs.example-freshness`, the real docs sensor does not yet populate). These literal findings drive the
// PURE verdict fold (a blocking finding fails, an advisory one does not) through the public interpreter.

let private mkSyntheticFinding (code: string) (severity: Severity) : FS.GG.Governance.SurfaceChecks.Model.SurfaceFinding =
    // SYNTHETIC: a hand-built finding, not sensed from disk — used only to drive the verdict fold under test.
    { Domain = FS.GG.Governance.SurfaceChecks.Model.DocsDomain
      Surface = SurfaceId "synthetic-surface"
      Code = code
      Location =
        ({ File = gp "docs/synthetic.md"
           Detail = "synthetic" }
        : FS.GG.Governance.SurfaceChecks.Model.FindingLocation)
      BaseSeverity = severity
      Maturity = BlockOnPr
      EvidenceTag = None
      IsInputState = false
      Message = "SYNTHETIC: hand-built finding for the verify verdict-fold test" }

let blockingSurfaceFinding: FS.GG.Governance.SurfaceChecks.Model.SurfaceFinding =
    mkSyntheticFinding "package.baseline-drift" Blocking

let advisorySurfaceFinding: FS.GG.Governance.SurfaceChecks.Model.SurfaceFinding =
    mkSyntheticFinding "docs.example-freshness" Advisory

// A synthetic surface-sense port returning a fixed finding list regardless of the report (disclosed). Lets a
// faked-port run drive the verdict fold without a real drifted tree.
let syntheticSurfaceSense (findings: FS.GG.Governance.SurfaceChecks.Model.SurfaceFinding list) : FS.GG.Governance.ProductSurfaces.Model.ProductSurfaceReport -> FS.GG.Governance.SurfaceChecks.Model.SurfaceFinding list =
    fun _ -> findings // SYNTHETIC: ignores the report, returns the literal findings under test

// A package-surface catalog with NO gates (empty `checks`) — isolates the read-only surface sense (no gate
// ever shells a process), for the no-write / no-spawn proof (FR-012, T009b) and the absent-baseline case.
let surfaceCatalogNoGates: Map<string, string> =
    Map
        [ "project.yml", projectYml
          "capabilities.yml",
          yaml """
schemaVersion: 2
domains:
  - package-api
pathMap:
  - glob: "src/**"
    capability: package-api
surfaces:
  - id: pkg-surface
    kind: package
    paths: ["src/**/*.fsi"]
    owner: platform
    maturity: block-on-ship
    evidenceTag: api-contract
checks: []
"""
          "policy.yml", policyYml
          "tooling.yml", toolingYml ]

// A temp repo declaring a package surface with NO committed baseline and a declared transcript file. A
// read-only verify MUST report `package.baseline-absent` (blocking) WITHOUT writing the `.baseline` and
// WITHOUT executing the transcript (no process). `body` runs against the repo path.
let withAbsentBaselineRepo (body: string -> 'a) : 'a =
    let dir = Path.Combine(Path.GetTempPath(), "fsgg-verify-absent-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory dir |> ignore
    try
        git dir [ "init"; "-q"; "-b"; "main" ] |> ignore
        git dir [ "config"; "user.email"; "fixture@fsgg.test" ] |> ignore
        git dir [ "config"; "user.name"; "FSGG Fixture" ] |> ignore
        git dir [ "config"; "commit.gpgsign"; "false" ] |> ignore
        writeCatalog dir surfaceCatalogNoGates
        writeFile dir "src/Api.fsi" "val foo: int\n"
        // a declared transcript next to the surface — verify must NOT run it (read-only port lists none).
        writeFile dir "src/transcripts/example.fsx" "printfn \"hi\"\n"
        git dir [ "add"; "-A" ] |> ignore
        git dir [ "commit"; "-qm"; "base" ] |> ignore
        writeFile dir "src/Api.fsi" "val foo: int -> string\n"
        git dir [ "add"; "-A" ] |> ignore
        git dir [ "commit"; "-qm"; "head" ] |> ignore
        body dir
    finally
        try Directory.Delete(dir, true) with _ -> ()
