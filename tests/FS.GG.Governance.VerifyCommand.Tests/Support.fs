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

// ── repo-root locator (for the surface baseline) ──

let rec private findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root (FS.GG.Governance.sln) not found"
    | d ->
        let here ext = File.Exists(Path.Combine(d.FullName, "FS.GG.Governance." + ext))
        if here "sln" || here "slnx" then d.FullName else findRepoRoot d.Parent

let repoRoot = findRepoRoot (DirectoryInfo(AppContext.BaseDirectory))

let gp (s: string) = GovernedPath s

// ── In-memory `.fsgg` catalog fixtures ──

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

// A valid catalog with two domains and three block-on-ship checks across three cost tiers. A change under
// `src/**` routes to package-api ⇒ selects format(cheap) + build(medium). At `RunMode.Verify` those
// base-Blocking block-on-ship checks RELAX to effective-Advisory under Standard (a pass/clean verdict), and
// TIGHTEN to effective-Blocking under Strict (the verify-stage blocking boundary).
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

// A valid-but-empty catalog: two domains, no checks ⇒ an empty GateRegistry.
let emptyCatalog: Map<string, string> =
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
checks: []
"""
          "policy.yml", policyYml
          "tooling.yml", toolingYml ]

// An invalid catalog: an unsupported schema version on project.yml ⇒ Invalid.
let invalidCatalog: Map<string, string> =
    Map [ "project.yml", yaml """
schemaVersion: 999
id: my-product
governedRoot: .
domains:
  - package-api
""" ]

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

/// A package-api change under src/** (selects format + build).
let gitSrcChange: GitPort = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]

/// A git port over a repo with no committed changes (the nothing-to-verify case).
let gitEmpty: GitPort = gitWithChanges []

let gitNotRepo: GitPort =
    fun cmd ->
        match cmd with
        | RepoCheck -> Ok "false\n"
        | _ -> Ok ""

let gitUnavailable: GitPort = fun _ -> Error "git-unavailable: git not found"

let portsGit (g: GitPort) : Ports = { Git = g; Ci = fun () -> None }

// ── Default snapshot options + expected-rollup/projection helpers (real cores, no fakes of them) ──

let defaultOpts: SnapshotOptions = { Since = None; Base = None; Head = None }
let sinceOpts (rev: string) : SnapshotOptions = { Since = Some(GitRef rev); Base = None; Head = None }

let factsOf (files: Map<string, string>) : TypedFacts =
    match Loader.readSource (GovernedPath ".") (readerOf files) |> Schema.validate with
    | Valid f -> f
    | Invalid d -> failwithf "fixture catalog unexpectedly invalid: %A" d

let candidatesOf (g: GitPort) (opts: SnapshotOptions) : GovernedPath list =
    (FS.GG.Governance.Snapshot.Interpreter.senseSnapshot (portsGit g) opts).Changed |> List.map (fun c -> c.Path)

let candidatesOfRepo (dir: string) (opts: SnapshotOptions) : GovernedPath list =
    (FS.GG.Governance.Snapshot.Interpreter.senseSnapshot (FS.GG.Governance.Snapshot.Interpreter.realPorts dir) opts).Changed |> List.map (fun c -> c.Path)

/// The genuine F024 `ShipDecision` + its `RouteResult` for a catalog + candidate set + levers, via the real
/// F015→F017→F018→F019→F024 chain (verify threads `RunMode.Verify`).
let resultAndDecisionOf (files: Map<string, string>) (candidates: GovernedPath list) (mode: RunMode) (profile: Profile) =
    let facts = factsOf files
    let report = Routing.route facts candidates
    let registry = Gates.buildRegistry facts
    let findings = Findings.findUnknownGovernedPaths facts report
    let result = Route.select registry report findings
    result, Ship.rollup result mode profile

let decisionOf (files: Map<string, string>) (candidates: GovernedPath list) (mode: RunMode) (profile: Profile) : ShipDecision =
    snd (resultAndDecisionOf files candidates mode profile)

// ── F046 faked sensing ports (fixed literal hashes — Synthetic, disclosed) + expected-report computer ──

/// A faked freshness sensor with fixed literal digests. SYNTHETIC: no real bytes hashed (the real sensor is
/// proven over real temp-dir bytes in FS.GG.Governance.FreshnessSensing.Tests). Senses every gate fully.
let fakeSensor: FreshnessSensing.FreshnessSensor =
    { SenseRuleHash = fun () -> Some(RuleHash "rule-synthetic") // SYNTHETIC: fixed literal hash
      SenseGeneratorVersion = fun () -> Some(GeneratorVersion "gen-synthetic")
      SenseCoveredArtifacts = fun _ -> Some [ ArtifactHash "art-synthetic" ]
      SenseCommandVersion = fun _ -> Some(CommandVersion "cmd-synthetic") }

/// A faked sensor whose `senseFreshness` would surface an `Error` (a throwing accessor) — the degrade probe.
let throwingSensor: FreshnessSensing.FreshnessSensor =
    { fakeSensor with SenseRuleHash = fun () -> failwith "synthetic sense failure" }

let absentStoreReader: FreshnessSensing.StoreReader = fun _ -> Ok None

let malformedStoreReader: FreshnessSensing.StoreReader = fun _ -> Error "synthetic malformed store"

// ── F052 deterministic fake ExecutionPort (real byte[] + chosen exit; NEVER a Synthetic outcome literal) ──

let fakeExecPortExiting (code: int) : ExecutionPort =
    fun _command ->
        { Stdout = System.Text.Encoding.UTF8.GetBytes "out"
          Stderr = System.Text.Encoding.UTF8.GetBytes "err"
          ExitCode = ExitCode code
          Duration = SensedDuration 7L }

/// A failing fake port (exit 1): a failing command-gate is NOT relocated, so it stays where `Ship.rollup`
/// placed it (a blocker under Strict; an advisory warning under Standard).
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

let private revOfCommit (CommitId c) = Revision c

let baseHeadOfSnap (snap: RepoSnapshot option) : Revision option * Revision option =
    match snap |> Option.bind (fun s -> s.Range) with
    | Some r -> Some(revOfCommit r.Base), Some(revOfCommit r.Head)
    | None -> None, None

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

let expectedOutcomesWith (port: ExecutionPort) (files: Map<string, string>) (selectedGates: Gate list) : (GateId * GateOutcome) list =
    let tooling = (factsOf files).Tooling

    selectedGates
    |> List.map (fun g ->
        let outcome =
            match tooling |> Option.bind (fun t -> Plan.commandFor "." t g) with
            | Some cmd ->
                let record = FS.GG.Governance.GateExecution.Interpreter.senseExecution port cmd
                let code = record.Reproducible.ExitCode

                { GateId = g.Id
                  Disposition = Executed
                  ExitCode = Some code
                  Passed = Some(Plan.passed code) }
            | None ->
                { GateId = g.Id
                  Disposition = NotExecuted
                  ExitCode = None
                  Passed = None }

        g.Id, outcome)

let expectedOutcomes (files: Map<string, string>) (selectedGates: Gate list) : (GateId * GateOutcome) list =
    expectedOutcomesWith fakeExecPortFail files selectedGates

let relocatedDecisionWith (port: ExecutionPort) (files: Map<string, string>) (candidates: GovernedPath list) (mode: RunMode) (profile: Profile) : ShipDecision * (GateId * GateOutcome) list =
    let result, decision = resultAndDecisionOf files candidates mode profile
    let selectedGates = result.SelectedGates |> List.map (fun sg -> sg.Gate)
    let outcomes = expectedOutcomesWith port files selectedGates

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
      Execute = fakeExecPortFail }

let fakePortsWith (files: Map<string, string>) (g: GitPort) (sensor: FreshnessSensing.FreshnessSensor) (store: FreshnessSensing.StoreReader) (cap: Capture) : Interpreter.Ports =
    { Files = readerOf files
      Git = portsGit g
      Freshness = sensor
      Store = store
      Write = capturingWriter cap Set.empty
      Out = capturingSink cap
      Execute = fakeExecPortFail }

let fakePortsFailingWrites (files: Map<string, string>) (g: GitPort) (cap: Capture) (failPaths: Set<string>) : Interpreter.Ports =
    { Files = readerOf files
      Git = portsGit g
      Freshness = fakeSensor
      Store = absentStoreReader
      Write = capturingWriter cap failPaths
      Out = capturingSink cap
      Execute = fakeExecPortFail }

let fakePortsExec (files: Map<string, string>) (g: GitPort) (sensor: FreshnessSensing.FreshnessSensor) (store: FreshnessSensing.StoreReader) (exec: ExecutionPort) (cap: Capture) : Interpreter.Ports =
    { Files = readerOf files
      Git = portsGit g
      Freshness = sensor
      Store = store
      Write = capturingWriter cap Set.empty
      Out = capturingSink cap
      Execute = exec }

let snapshotOf (g: GitPort) (opts: SnapshotOptions) : RepoSnapshot =
    FS.GG.Governance.Snapshot.Interpreter.senseSnapshot (portsGit g) opts

let snapshotOfRepo (dir: string) (opts: SnapshotOptions) : RepoSnapshot =
    FS.GG.Governance.Snapshot.Interpreter.senseSnapshot (FS.GG.Governance.Snapshot.Interpreter.realPorts dir) opts

let writtenVerify (cap: Capture) : (string * string) option =
    cap.Writes |> List.tryPick (fun (k, p, c) -> if k = Loop.VerifyArtifact then Some(p, c) else None)

// ── Request builders ──

/// The canonical pre-PR request: `--profile standard`, verify at the default `readiness/verify.json`.
let requestFor (scope: Loop.ScopeSelector) (format: Loop.OutputFormat) : Loop.RunRequest =
    { Repo = "."
      Scope = scope
      Profile = Standard
      Format = format
      VerifyOut = "readiness/verify.json"
      StorePath = "readiness/evidence-reuse.json"
      PersistStore = false }

/// A request under an explicit profile (for the blocking / uncertain scenarios at Strict).
let requestForProfile (scope: Loop.ScopeSelector) (format: Loop.OutputFormat) (profile: Profile) : Loop.RunRequest =
    { requestFor scope format with Profile = profile }

// ── Real git temp-repo helper (the end-to-end proofs) ──

let git (dir: string) (args: string list) : string =
    let psi = ProcessStartInfo "git"
    for a in args do psi.ArgumentList.Add a
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

// ── F048 persistence fixtures: a REAL F030 store + the REAL F046 reader round-trip (Principle V) ──

let syntheticRef (label: string) : EvidenceRef = EvidenceRef("synthetic://" + label) // SYNTHETIC: real refs need gate execution

let persistInputs (check: string) (head: string) : FreshnessInputs =
    { Check = CheckId check
      Domain = DomainId "package-api"
      Command = Some(CommandId "dotnet")
      Environment = Local
      RuleHash = RuleHash "r1"
      CoveredArtifacts = [ ArtifactHash "h1" ]
      CommandVersion = Some(CommandVersion "8.0")
      GeneratorVersion = GeneratorVersion "g1"
      Base = Revision "aaa"
      Head = Revision head }

let storeOf (entries: (FreshnessInputs * EvidenceRef) list) : ReuseStore =
    entries |> List.fold (fun s (i, e) -> EvidenceReuse.record i e s) EvidenceReuse.empty

let readStore (path: string) : ReuseStore option =
    match FreshnessSensing.realStoreReader path with
    | Ok loaded -> loaded
    | Error r -> failwithf "realStoreReader rejected %s: %s" path r

/// Create a disposable temp git repo with a real `.fsgg` catalog and a real two-commit edit under `src/`,
/// run `body` against its path, then delete it.
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

// ── F052 grown-store helpers ──

let selectedGatesFor (files: Map<string, string>) (candidates: GovernedPath list) : Gate list =
    let facts = factsOf files
    let report = Routing.route facts candidates
    let registry = Gates.buildRegistry facts
    let findings = Findings.findUnknownGovernedPaths facts report
    (Route.select registry report findings).SelectedGates |> List.map (fun sg -> sg.Gate)
