module FS.GG.Governance.FreshnessSensing.Tests.Support

// Real-temp-directory support helpers (Principle V — real on-disk bytes through the real sensing edge,
// never a mock of the cores). `withTempDir` builds a disposable directory with a minimal `.fsgg/*.yml`
// catalog + a couple of `src/**` files (the bytes the real SHA-256 sensor hashes), an on-disk
// well-formed `fsgg.evidence-reuse-store/v1` built to round-trip the public F029/F030 constructors, and a
// malformed-store file. No git, no network — only filesystem reads/writes the real edge performs.

open System
open System.IO
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.Gates.Model
// 074: findRepoRoot consolidated into the shared RepositoryHelpers (sln||slnx superset).
let repoRoot = FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot

// ── a literally-constructible gate (for senseFreshness over real selected gates) ──

/// Build a complete, real F018 `Gate` from a domain/check id and an optional command. Only `Id` and
/// `FreshnessKey` matter to the sensing assembly; the rest is real-but-unread metadata.
let gateWith (domain: string) (check: string) (command: CommandId option) : Gate =
    let fk: FreshnessKey =
        { Check = CheckId check
          Domain = DomainId domain
          Cost = Cost.Medium
          Environment = EnvironmentClass.Ci
          Command = command }

    { Id = GateId(domain + ":" + check)
      Domain = DomainId domain
      Description = sprintf "gate %s:%s" domain check
      Prerequisites =
        (match command with
         | Some c -> [ RequiresCommand c ]
         | None -> [])
      Cost = Cost.Medium
      Timeout = TimeoutLimit 60
      Owner = Owner "team"
      Maturity = Observe
      ProductCheck = false
      FreshnessKey = fk }

let dotnetCmd = CommandId "dotnet"

// ── the well-formed store fixture (round-trips the public constructors) ──

/// The freshness inputs of the single recorded entry the well-formed store fixture carries — built via the
/// public F029 constructors, the exact value the real deserializer must reconstruct (round-trip equality).
let sampleInputs: FreshnessInputs =
    { Check = CheckId "tests"
      Domain = DomainId "build"
      Command = Some(CommandId "dotnet")
      Environment = EnvironmentClass.Ci
      RuleHash = RuleHash "rule-1"
      CoveredArtifacts = [ ArtifactHash "artA"; ArtifactHash "artB" ]
      CommandVersion = Some(CommandVersion "8.0")
      GeneratorVersion = GeneratorVersion "gen-1"
      Base = Revision "base-1"
      Head = Revision "head-1" }

let sampleEvidence = EvidenceRef "ev-1"

/// The expected `ReuseStore` for the well-formed fixture — built the REAL way (`EvidenceReuse.record` over
/// `empty`), so the round-trip assertion compares the deserializer against the genuine recording path.
let expectedStore: ReuseStore =
    EvidenceReuse.record sampleInputs sampleEvidence EvidenceReuse.empty

/// The on-disk JSON of the well-formed fixture (`fsgg.evidence-reuse-store/v1`), field-for-field matching
/// `sampleInputs` + `sampleEvidence`.
let private wellFormedStoreJson =
    """{"schemaVersion":"fsgg.evidence-reuse-store/v1","recorded":[{"check":"tests","domain":"build","command":"dotnet","environment":"ci","ruleHash":"rule-1","coveredArtifacts":["artA","artB"],"commandVersion":"8.0","generatorVersion":"gen-1","base":"base-1","head":"head-1","evidence":"ev-1"}]}"""

/// A malformed store file (unknown schema version) — the real deserializer must surface `Error`, never throw.
let private malformedStoreJson =
    """{"schemaVersion":"fsgg.evidence-reuse-store/v99","recorded":[]}"""

// ── a disposable temp directory with real catalog/src bytes + the store fixtures ──

type TempRepo =
    { Dir: string
      WellFormedStorePath: string
      MalformedStorePath: string
      AbsentStorePath: string }

let writeFile (dir: string) (relPath: string) (content: string) : unit =
    let full = Path.Combine(dir, relPath)

    match Path.GetDirectoryName full with
    | null -> ()
    | parent -> Directory.CreateDirectory parent |> ignore

    File.WriteAllText(full, content)

/// Create a disposable temp directory with a minimal `.fsgg/*.yml` catalog, a couple of `src/**` files, a
/// well-formed and a malformed store on disk, run `body`, then delete it.
let withTempDir (body: TempRepo -> 'a) : 'a =
    let dir = Path.Combine(Path.GetTempPath(), "fsgg-sense-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory dir |> ignore

    try
        // A minimal rule-pack catalog (only the bytes matter to the SHA-256 sensor — no validation here).
        writeFile dir ".fsgg/project.yml" "schemaVersion: 1\nid: my-product\n"
        writeFile dir ".fsgg/capabilities.yml" "schemaVersion: 1\nchecks: []\n"
        // A couple of covered-surface files under src/**.
        writeFile dir "src/Lib/A.fs" "module A\nlet v = 1\n"
        writeFile dir "src/Lib/B.fs" "module B\nlet v = 2\n"

        let wf = Path.Combine(dir, "well-formed-store.json")
        let mf = Path.Combine(dir, "malformed-store.json")
        File.WriteAllText(wf, wellFormedStoreJson)
        File.WriteAllText(mf, malformedStoreJson)

        body
            { Dir = dir
              WellFormedStorePath = wf
              MalformedStorePath = mf
              AbsentStorePath = Path.Combine(dir, "no-such-store.json") }
    finally
        try
            Directory.Delete(dir, true)
        with _ ->
            ()

/// Create a disposable temp directory that has NO `.fsgg` catalog and NO `src/` surface (the unsensed case).
let withBareDir (body: string -> 'a) : 'a =
    let dir = Path.Combine(Path.GetTempPath(), "fsgg-bare-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory dir |> ignore

    try
        body dir
    finally
        try
            Directory.Delete(dir, true)
        with _ ->
            ()
