namespace FS.GG.Governance.Tests.Common

// 074 (Phase D): the implementations behind the curated `TestsCommon.fsi`. Visibility lives in the
// `.fsi` (Principle II) — NO `private`/`internal`/`public` modifiers here. Every helper is moved
// VERBATIM (behaviour-preserving) from the per-suite `Support.fs` copies it consolidates; the
// `SYNTHETIC:`-tagged fakes keep their disclosure comments intact (Principle V). Only genuinely
// byte-identical, type-compatible helpers live here — intentional per-suite variants (and the
// capture helpers, which are parametrised by each command's own `Loop.ArtifactKind`/`Interpreter`
// ports) stay local in their suites (FR-006, research D4).

module RepositoryHelpers =

    open System
    open System.IO

    let rec findRepoRoot (dir: DirectoryInfo | null) : string =
        match dir with
        | null -> failwith "repo root (FS.GG.Governance.sln) not found"
        | d ->
            let here ext = File.Exists(Path.Combine(d.FullName, "FS.GG.Governance." + ext))
            if here "sln" || here "slnx" then d.FullName else findRepoRoot d.Parent

    let repoRoot = findRepoRoot (DirectoryInfo(AppContext.BaseDirectory))

module CatalogFixtures =

    open FS.GG.Governance.Config
    open FS.GG.Governance.Config.Model

    let gp (s: string) = GovernedPath s

    let yaml (s: string) = s.TrimStart('\n')

    let projectYml =
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

    let policyYml =
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

    let toolingYml =
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

    // A valid catalog with two domains and three block-on-ship checks across three cost tiers. A change
    // under `src/**` routes to package-api ⇒ selects format(cheap) + build(medium).
    let validCatalog: Map<string, string> =
        Map
            [ "governance.yml", projectYml
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
            [ "governance.yml", projectYml
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
checks: []
"""
              "policy.yml", policyYml
              "tooling.yml", toolingYml ]

    // An invalid catalog: an unsupported schema version on governance.yml ⇒ Invalid.
    let invalidCatalog: Map<string, string> =
        Map [ "governance.yml", yaml """
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

    let factsOf (files: Map<string, string>) : TypedFacts =
        match Loader.readSource (GovernedPath ".") (readerOf files) |> Schema.validate with
        | Valid f -> f
        | Invalid d -> failwithf "fixture catalog unexpectedly invalid: %A" d

module FakePorts =

    open FS.GG.Governance.Snapshot
    open FS.GG.Governance.FreshnessKey.Model
    open FS.GG.Governance.FreshnessSensing
    open FS.GG.Governance.CommandRecord.Model
    open FS.GG.Governance.GateExecution.Model
    open FS.GG.Governance.HumanText

    // A non-TTY (piped/redirected) capability ⇒ `selectMode` picks `Plain` — the default for the faked
    // ports so existing tests keep capturing the ANSI-free summary via the `Out` sink.
    let plainCapability: bool -> RenderMode.ColorCapability =
        fun explicitPlain ->
            { IsTty = false
              NoColorEnv = false
              ExplicitPlain = explicitPlain
              Width = None }

    // A no-op rich renderer for the faked ports (the Plain path never calls it).
    let noRichRender: ReportView.ReportView -> unit = fun _ -> ()

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

    /// A git port over a repo with no committed changes (the nothing-to-verify case).
    let gitEmpty: GitPort = gitWithChanges []

    let gitNotRepo: GitPort =
        fun cmd ->
            match cmd with
            | RepoCheck -> Ok "false\n"
            | _ -> Ok ""

    let gitUnavailable: GitPort = fun _ -> Error "git-unavailable: git not found"

    let portsGit (g: GitPort) : Ports = { Git = g; Ci = fun () -> None }

    /// A faked freshness sensor with fixed literal digests. SYNTHETIC: no real bytes hashed (the real
    /// sensor is proven over real temp-dir bytes in FS.GG.Governance.FreshnessSensing.Tests). Senses every
    /// gate fully.
    let fakeSensor: FreshnessSensing.FreshnessSensor =
        { SenseRuleHash = fun () -> Some(RuleHash "rule-synthetic") // SYNTHETIC: fixed literal hash
          SenseGeneratorVersion = fun () -> Some(GeneratorVersion "gen-synthetic")
          SenseCoveredArtifacts = fun _ -> Some [ ArtifactHash "art-synthetic" ]
          SenseCommandVersion = fun _ -> Some(CommandVersion "cmd-synthetic") }

    /// A faked sensor whose `senseFreshness` would surface an `Error` (a throwing accessor) — the degrade
    /// probe.
    let throwingSensor: FreshnessSensing.FreshnessSensor =
        { fakeSensor with SenseRuleHash = fun () -> failwith "synthetic sense failure" }

    let absentStoreReader: FreshnessSensing.StoreReader = fun _ -> Ok None

    let malformedStoreReader: FreshnessSensing.StoreReader = fun _ -> Error "synthetic malformed store"

    let fakeExecPortExiting (code: int) : ExecutionPort =
        fun _command ->
            { Stdout = System.Text.Encoding.UTF8.GetBytes "out"
              Stderr = System.Text.Encoding.UTF8.GetBytes "err"
              ExitCode = ExitCode code
              Duration = SensedDuration 7L }

    type ExecCounter = { mutable Calls: int }

    let countingExecPort (counter: ExecCounter) (code: int) : ExecutionPort =
        fun command ->
            counter.Calls <- counter.Calls + 1
            fakeExecPortExiting code command

module SnapshotHelpers =

    open System.IO
    open System.Diagnostics
    open FS.GG.Governance.Config.Model
    open FS.GG.Governance.Snapshot
    open FS.GG.Governance.Snapshot.Model
    open FS.GG.Governance.Routing
    open FS.GG.Governance.Findings
    open FS.GG.Governance.Gates
    open FS.GG.Governance.Gates.Model
    open FS.GG.Governance.Route
    open FS.GG.Governance.GateRun
    open FS.GG.Governance.GateRun.Model
    open FS.GG.Governance.GateExecution.Model
    open FS.GG.Governance.FreshnessKey.Model
    open FS.GG.Governance.FreshnessSensing
    open FS.GG.Governance.EvidenceReuse
    open FS.GG.Governance.EvidenceReuse.Model
    open CatalogFixtures
    open FakePorts

    let defaultOpts: SnapshotOptions = { Since = None; Base = None; Head = None }

    let sinceOpts (rev: string) : SnapshotOptions = { Since = Some(GitRef rev); Base = None; Head = None }

    let snapshotOf (g: GitPort) (opts: SnapshotOptions) : RepoSnapshot =
        FS.GG.Governance.Snapshot.Interpreter.senseSnapshot (portsGit g) opts

    let snapshotOfRepo (dir: string) (opts: SnapshotOptions) : RepoSnapshot =
        FS.GG.Governance.Snapshot.Interpreter.senseSnapshot (FS.GG.Governance.Snapshot.Interpreter.realPorts dir) opts

    let candidatesOf (g: GitPort) (opts: SnapshotOptions) : GovernedPath list =
        (FS.GG.Governance.Snapshot.Interpreter.senseSnapshot (portsGit g) opts).Changed |> List.map (fun c -> c.Path)

    let candidatesOfRepo (dir: string) (opts: SnapshotOptions) : GovernedPath list =
        (FS.GG.Governance.Snapshot.Interpreter.senseSnapshot (FS.GG.Governance.Snapshot.Interpreter.realPorts dir) opts).Changed |> List.map (fun c -> c.Path)

    let revOfCommit (CommitId c) = Revision c

    let baseHeadOfSnap (snap: RepoSnapshot option) : Revision option * Revision option =
        match snap |> Option.bind (fun s -> s.Range) with
        | Some r -> Some(revOfCommit r.Base), Some(revOfCommit r.Head)
        | None -> None, None

    let selectedGatesFor (files: Map<string, string>) (candidates: GovernedPath list) : Gate list =
        let facts = factsOf files
        let report = Routing.route facts candidates
        let registry = Gates.buildRegistry facts
        let findings = Findings.findUnknownGovernedPaths facts report
        (Route.select registry report findings).SelectedGates |> List.map (fun sg -> sg.Gate)

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

    let storeOf (entries: (FreshnessInputs * EvidenceRef) list) : ReuseStore =
        entries |> List.fold (fun s (i, e) -> EvidenceReuse.record i e s) EvidenceReuse.empty

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

    let syntheticRef (label: string) : EvidenceRef = EvidenceRef("synthetic://" + label) // SYNTHETIC: real refs need gate execution

    let readStore (path: string) : ReuseStore option =
        match FreshnessSensing.realStoreReader path with
        | Ok loaded -> loaded
        | Error r -> failwithf "realStoreReader rejected %s: %s" path r

    /// Create a disposable temp git repo by driving REAL git (Principle V). Writes into the caller-provided
    /// temp dir; owns no durable state. The per-suite `withTempRepo` builders compose these two.
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
