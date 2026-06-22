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
open FS.GG.Governance.FreshnessSensing

// `Snapshot` has its own `Interpreter` module; alias it so `Interpreter` stays unambiguously the
// ShipCommand edge while `FS.GG.Governance.Snapshot.Interpreter.senseSnapshot` / `GitPort` / `Ports` /
// `RepoCheck`… reach the F016 git surface.
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

// Triple-quoted YAML with real 2-space indentation; strip the single leading newline the literal
// carries so `schemaVersion` is the first line (matching the on-disk fixture format exactly).
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

// A valid catalog with two domains (package-api, workflow) and three block-on-ship checks across
// three cost tiers. A change under `src/**` routes to package-api ⇒ selects format(cheap) +
// build(medium), both base-Blocking (block-on-ship) ⇒ under --mode gate --profile standard they are
// effective-Blocking ⇒ a FAIL/BLOCKED verdict; under a lower run mode (e.g. inner) they relax to
// effective-Advisory warnings (a PASS/CLEAN no-hide verdict).
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

// A valid-but-empty catalog: two domains, no checks ⇒ an empty GateRegistry (the empty-registry case).
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

// An invalid catalog: an unsupported schema version on project.yml ⇒ Invalid (a validation failure).
let invalidCatalog: Map<string, string> =
    Map [ "project.yml", yaml """
schemaVersion: 999
id: my-product
governedRoot: .
domains:
  - package-api
""" ]

// In-memory FileReader: a missing key is `Ok None` (absent, normal); a present key is `Ok (Some _)`.
let readerOf (files: Map<string, string>) : Loader.FileReader =
    fun name ->
        match Map.tryFind name files with
        | Some content -> Ok(Some content)
        | None -> Ok None

// ── In-memory git port (canned READ-ONLY output the real Snapshot.assemble parses) ──

/// A `git diff --name-status -z -M` payload for the given (statusLetter, path) changes.
let diffPayload (changes: (char * string) list) : string =
    changes |> List.map (fun (k, p) -> sprintf "%c\000%s\000" k p) |> String.concat ""

/// A git port that senses a clean repo whose committed diff is exactly `changes` (all `Modified`
/// here). No real git process — just the canned read-only stdout each command would emit.
let gitWithChanges (changes: (char * string) list) : GitPort =
    fun cmd ->
        match cmd with
        | RepoCheck -> Ok "true\n"
        | RevParse _ -> Ok "0123456\n"
        | MergeBase _ -> Ok "0123456\n"
        | DiffNameStatus _ -> Ok(diffPayload changes)
        | StatusPorcelain -> Ok ""
        | CurrentBranch -> Ok "main\n"

/// A git port over a repo with no committed changes (the empty-diff / no-changes-in-scope case).
let gitEmpty: GitPort = gitWithChanges []

/// A git port reporting the target is not a git repository (RepoCheck ⇒ "false").
let gitNotRepo: GitPort =
    fun cmd ->
        match cmd with
        | RepoCheck -> Ok "false\n"
        | _ -> Ok ""

/// A git port where `git` itself is unavailable on PATH.
let gitUnavailable: GitPort = fun _ -> Error "git-unavailable: git not found"

/// A git port that resolves every ref except `badRev`, which fails to resolve (unknown `--since`).
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

let portsGit (g: GitPort) : Ports = { Git = g; Ci = fun () -> None }

// ── Default snapshot options + expected-rollup/projection helpers (real cores, no fakes of them) ──

let defaultOpts: SnapshotOptions = { Since = None; Base = None; Head = None }
let sinceOpts (rev: string) : SnapshotOptions = { Since = Some(GitRef rev); Base = None; Head = None }

/// The validated facts a fixture catalog yields (fails loudly if a fixture is itself invalid).
let factsOf (files: Map<string, string>) : TypedFacts =
    match Loader.readSource (GovernedPath ".") (readerOf files) |> Schema.validate with
    | Valid f -> f
    | Invalid d -> failwithf "fixture catalog unexpectedly invalid: %A" d

/// The candidate changed-path set the real Snapshot core derives from a git port + options.
let candidatesOf (g: GitPort) (opts: SnapshotOptions) : GovernedPath list =
    (FS.GG.Governance.Snapshot.Interpreter.senseSnapshot (portsGit g) opts).Changed |> List.map (fun c -> c.Path)

/// The candidate set the real Snapshot core derives from a REAL git repo on disk (the e2e proof).
let candidatesOfRepo (dir: string) (opts: SnapshotOptions) : GovernedPath list =
    (FS.GG.Governance.Snapshot.Interpreter.senseSnapshot (FS.GG.Governance.Snapshot.Interpreter.realPorts dir) opts).Changed |> List.map (fun c -> c.Path)

/// The genuine F024 `ShipDecision` + its `RouteResult` for a catalog + candidate set + levers, via the
/// real F015→F017→F018→F019→F024 chain — the verdict the command must carry verbatim.
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

/// A faked sensor whose `senseFreshness` would surface an `Error` (a throwing accessor) — the US3 sense-Error
/// degrade probe. SYNTHETIC.
let throwingSensor: FreshnessSensing.FreshnessSensor =
    { fakeSensor with SenseRuleHash = fun () -> failwith "synthetic sense failure" }

/// An ABSENT store reader (no file on disk ⇒ Ok None ⇒ loadStore maps to EvidenceReuse.empty).
let absentStoreReader: FreshnessSensing.StoreReader = fun _ -> Ok None

/// A MALFORMED store reader (present-but-unreadable ⇒ Error) — the US3 store-Error degrade probe.
let malformedStoreReader: FreshnessSensing.StoreReader = fun _ -> Error "synthetic malformed store"

let private revOfCommit (CommitId c) = Revision c

/// The base/head the command derives from a snapshot's range (mirrors `Loop.baseHeadOf`); `None` snapshot ⇒
/// both `None`.
let baseHeadOfSnap (snap: RepoSnapshot option) : Revision option * Revision option =
    match snap |> Option.bind (fun s -> s.Range) with
    | Some r -> Some(revOfCommit r.Base), Some(revOfCommit r.Head)
    | None -> None, None

/// The genuine cache-eligibility report for the selected gates, recomputed LIVE through the public
/// `senseFreshness`→`resolve`→`candidate`→`evaluate` chain over the given sensor/store/baseHead. An empty
/// store ⇒ every resolved gate `mustRecompute noPriorEvidence`.
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

/// The genuine F025 `audit.json` bytes for a catalog + candidate set + levers — the document the command must
/// persist verbatim (SC-001). The document now carries the LIVE-recomputed cache report (`Some report`) over
/// the faked sensor + absent (empty) store; `snap` is the snapshot the command sees (`None` for ExplicitPaths)
/// so base/head match exactly.
let auditExpected (files: Map<string, string>) (candidates: GovernedPath list) (mode: RunMode) (profile: Profile) (snap: RepoSnapshot option) : string =
    let result, decision = resultAndDecisionOf files candidates mode profile
    let selectedGates = result.SelectedGates |> List.map (fun sg -> sg.Gate)
    let cacheReport = expectedCacheReport selectedGates (baseHeadOfSnap snap)
    AuditJson.ofShipDecision decision (Some cacheReport)

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
      Out = capturingSink cap }

/// Faked ports with explicit F046 sensing ports (for the US3 degrade probes).
let fakePortsWith (files: Map<string, string>) (g: GitPort) (sensor: FreshnessSensing.FreshnessSensor) (store: FreshnessSensing.StoreReader) (cap: Capture) (req: Loop.RunRequest) : Interpreter.Ports =
    { Files = readerOf files
      Git = portsGit g
      Freshness = sensor
      Store = store
      Write = capturingWriter cap Set.empty req.AuditOut
      Out = capturingSink cap }

/// Faked ports whose ArtifactWriter fails for the given paths (the unwritable-output case).
let fakePortsFailingWrites (files: Map<string, string>) (g: GitPort) (cap: Capture) (failPaths: Set<string>) (req: Loop.RunRequest) : Interpreter.Ports =
    { Files = readerOf files
      Git = portsGit g
      Freshness = fakeSensor
      Store = absentStoreReader
      Write = capturingWriter cap failPaths req.AuditOut
      Out = capturingSink cap }

/// A real RepoSnapshot the F016 core derives from a faked git port (for the pure `update` tests).
let snapshotOf (g: GitPort) (opts: SnapshotOptions) : RepoSnapshot =
    FS.GG.Governance.Snapshot.Interpreter.senseSnapshot (portsGit g) opts

/// The real RepoSnapshot the F016 core derives from a REAL git repo on disk (for the e2e expected report).
let snapshotOfRepo (dir: string) (opts: SnapshotOptions) : RepoSnapshot =
    FS.GG.Governance.Snapshot.Interpreter.senseSnapshot (FS.GG.Governance.Snapshot.Interpreter.realPorts dir) opts

/// The single captured audit write (path, content), if any.
let writtenAudit (cap: Capture) : (string * string) option =
    cap.Writes |> List.tryPick (fun (k, p, c) -> if k = Loop.AuditArtifact then Some(p, c) else None)

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
      PersistStore = false }

/// A request under an explicit mode/profile lever set (for the two-lever-set / no-hide proofs).
let requestForLevers (scope: Loop.ScopeSelector) (format: Loop.OutputFormat) (mode: RunMode) (profile: Profile) : Loop.RunRequest =
    { requestFor scope format with Mode = mode; Profile = profile }

// ── Real git temp-repo helper (the ONE end-to-end proof) ──

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

/// An opaque, DISCLOSED-SYNTHETIC evidence reference. SYNTHETIC: a real `EvidenceRef` is the output of gate
/// execution (a deferred row); these literal pointers keep the store shape real.
let syntheticRef (label: string) : EvidenceRef = EvidenceRef("synthetic://" + label) // SYNTHETIC: real refs need gate execution

/// A complete, literal `FreshnessInputs` varying only `Check`/`Head`. Every category present so any loss on
/// round-trip is observable.
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

/// Build a `ReuseStore` by folding the REAL `EvidenceReuse.record` (oldest-first input ⇒ newest-first store).
let storeOf (entries: (FreshnessInputs * EvidenceRef) list) : ReuseStore =
    entries |> List.fold (fun s (i, e) -> EvidenceReuse.record i e s) EvidenceReuse.empty

/// Load a store file through the REAL `FreshnessSensing.realStoreReader`; raise on a malformed file.
let readStore (path: string) : ReuseStore option =
    match FreshnessSensing.realStoreReader path with
    | Ok loaded -> loaded
    | Error r -> failwithf "realStoreReader rejected %s: %s" path r

/// Create a disposable temp git repo with a real `.fsgg` catalog and a real two-commit edit under
/// `src/`, run `body` against its path, then delete it.
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
