module FS.GG.Governance.ReleaseCommand.Tests.Support

open System
open System.IO
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.CommandKind.Model
open FS.GG.Governance.GateExecution.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.PackEvidence.Model
open FS.GG.Governance.Provenance.Model
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseFactsSensing
open FS.GG.Governance.ReleaseFactsSensing.Model
open FS.GG.Governance.ReleaseDeclaration            // 065: the shared Declaration leaf (was row-local)
open FS.GG.Governance.ReleaseCommand

// Shared REAL-input builders for the F055 ReleaseCommand tests (Principle V — every value below is a real,
// literally-constructible typed value or a real on-disk temp fixture, never a mock). The edge tests read a
// REAL temp-repo fixture (`.fsgg/release.yml` + the six governing source files) through `Interpreter.run`
// over the REAL F053/F054/ReleaseJson cores; the pure-core tests consume hand-built typed values. No
// network, no registry, no publishing provider (SC-008).

// ── The governed identity + the neutral six-source layout (matches the fixtures' release.yml) ──

let surfaceId = SurfaceId "pkg"

let layout: SourceLayout =
    { VersionPath = "version.txt"
      MetadataPath = "metadata.txt"
      PinsPath = "pins.txt"
      PublishPlanPath = "publish-plan.txt"
      TrustedPublishingPath = "trusted-publishing.txt"
      ProvenancePath = "provenance.txt" }

/// The product-neutral expectation set every fixture's release.yml declares (hand-built twin for the
/// pure-core tests — kept in lockstep with `releaseYmlAllBlocking`).
let expectations: ReleaseExpectations =
    { Surface = surfaceId
      VersionBaseline = Some "1.2.0"
      RequiredMetadataFields = Some [ "authors"; "license" ]
      ExpectedPins = Some(Map [ "base", "9.0.0" ])
      RequiredPublishPosture = Some [ "plan-present" ]
      RequiredTrustedPublishing = Some [ "oidc" ]
      RequiredProvenance = Some [ "attestation" ] }

// ── Hand-built recovered evidence (the pure-core sensed-value input, no disk) ──

let recoveredMet: RecoveredEvidence =
    { Version = Ok { Declared = "1.3.0" }
      Metadata = Ok { PresentFields = [ "authors"; "license" ] }
      Pins = Ok { Resolved = Map [ "base", "9.0.0" ] }
      PublishPlan = Ok { Observed = [ "plan-present" ] }
      TrustedPublishing = Ok { Observed = [ "oidc" ] }
      Provenance = Ok { Observed = [ "attestation" ] } }

/// All met except VersionBump (declared version equals the baseline ⇒ not bumped past ⇒ Unmet).
let recoveredUnbumped: RecoveredEvidence =
    { recoveredMet with Version = Ok { Declared = "1.2.0" } }

/// The all-met sensed value (via the REAL F054 pure core).
let sensedMet: SensedRelease = Sensing.deriveFacts expectations recoveredMet

let sensedUnbumped: SensedRelease = Sensing.deriveFacts expectations recoveredUnbumped

// ── The fixtures' release.yml declarations (row-local surface; kebab-case kind tokens) ──

let releaseYmlAllBlocking =
    "surface: pkg\n"
    + "rules:\n"
    + "  - kind: version-bump\n    severity: blocking\n    maturity: block-on-release\n"
    + "  - kind: package-metadata\n    severity: blocking\n    maturity: block-on-release\n"
    + "  - kind: template-pins\n    severity: blocking\n    maturity: block-on-release\n"
    + "  - kind: publish-plan\n    severity: blocking\n    maturity: block-on-release\n"
    + "  - kind: trusted-publishing\n    severity: blocking\n    maturity: block-on-release\n"
    + "  - kind: provenance\n    severity: blocking\n    maturity: block-on-release\n"
    + "expectations:\n"
    + "  versionBaseline: \"1.2.0\"\n"
    + "  requiredMetadataFields: [authors, license]\n"
    + "  expectedPins:\n    base: \"9.0.0\"\n"
    + "  requiredPublishPosture: [plan-present]\n"
    + "  requiredTrustedPublishing: [oidc]\n"
    + "  requiredProvenance: [attestation]\n"
    + "layout:\n"
    + "  versionPath: version.txt\n"
    + "  metadataPath: metadata.txt\n"
    + "  pinsPath: pins.txt\n"
    + "  publishPlanPath: publish-plan.txt\n"
    + "  trustedPublishingPath: trusted-publishing.txt\n"
    + "  provenancePath: provenance.txt\n"

/// As above but the provenance rule is blocking-but-`warn` — a violation relaxes to effective advisory
/// (⇒ a WARNING, not a blocker) under the fixed Release mode/profile (F024 never escalates).
let releaseYmlProvenanceAdvisory =
    releaseYmlAllBlocking.Replace(
        "  - kind: provenance\n    severity: blocking\n    maturity: block-on-release\n",
        "  - kind: provenance\n    severity: blocking\n    maturity: warn\n"
    )

let ymlLines (yml: string) : string list =
    yml.Replace("\r\n", "\n").Split('\n') |> List.ofArray

let parseYml (yml: string) : Declaration.ReleaseDeclaration =
    match Declaration.parse (ymlLines yml) with
    | Ok d -> d
    | Error e -> failwithf "fixture release.yml failed to parse: %s" e.Reason

/// The compliant declaration (parsed from the fixture yml — exercises the real adapter). No packable
/// projects ⇒ the pack precondition is vacuously satisfied.
let compliantDeclaration: Declaration.ReleaseDeclaration = parseYml releaseYmlAllBlocking

/// 065: a declaration with ONE declared packable project (surface `pkg`, baseline `1.2.0`) — the pack
/// boundary input. A packed version above `1.2.0` ⇒ Bumped; equal ⇒ Unbumped; below ⇒ Downgraded.
let releaseYmlWithPackables =
    releaseYmlAllBlocking
    + "packableProjects:\n  - surface: pkg\n    packCommand:\n      executable: dotnet\n      arguments: [pack]\n    baseline: \"1.2.0\"\n"

let declWithPackables: Declaration.ReleaseDeclaration = parseYml releaseYmlWithPackables

/// 065: as above PLUS a declared exhaustive matrix (admitted `RunNow` at the release boundary, never run).
let releaseYmlWithPackablesAndMatrix =
    releaseYmlWithPackables + "matrix:\n  name: cross\n  cost: exhaustive\n  dimensions: [net10]\n"

let declWithPackablesAndMatrix: Declaration.ReleaseDeclaration = parseYml releaseYmlWithPackablesAndMatrix

/// 065: TWO declared packable projects (the order-independence / multi-project boundary fixture).
let releaseYmlTwoPackables =
    releaseYmlAllBlocking
    + "packableProjects:\n"
    + "  - surface: pkg\n    packCommand:\n      executable: dotnet\n      arguments: [pack]\n    baseline: \"1.2.0\"\n"
    + "  - surface: pkg2\n    packCommand:\n      executable: dotnet\n      arguments: [pack]\n    baseline: \"2.0.0\"\n"

let declWithTwoPackables: Declaration.ReleaseDeclaration = parseYml releaseYmlTwoPackables

// ── Real temp-repository fixture (the edge tests' Principle-V input) ──

let withTempDir (body: string -> 'a) : 'a =
    let dir = Path.Combine(Path.GetTempPath(), "fsgg-release-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory dir |> ignore

    try
        body dir
    finally
        try
            Directory.Delete(dir, true)
        with _ ->
            ()

let writeFile (dir: string) (relPath: string) (content: string) : unit =
    let full = Path.Combine(dir, relPath)

    match Path.GetDirectoryName full with
    | null -> ()
    | parent -> Directory.CreateDirectory parent |> ignore

    File.WriteAllText(full, content)

/// Write the six source files that all SATISFY the expectations (the compliant fixture).
let writeMetSources (dir: string) : unit =
    writeFile dir layout.VersionPath "1.3.0\n"
    writeFile dir layout.MetadataPath "authors\nlicense\n"
    writeFile dir layout.PinsPath "base=9.0.0\n"
    writeFile dir layout.PublishPlanPath "plan-present\n"
    writeFile dir layout.TrustedPublishingPath "oidc\n"
    writeFile dir layout.ProvenancePath "attestation\n"

/// All met except the version is NOT bumped past the baseline (⇒ VersionBump blocks).
let writeUnbumpedSources (dir: string) : unit =
    writeMetSources dir
    writeFile dir layout.VersionPath "1.2.0\n"

/// All met except the provenance source is ABSENT (⇒ Provenance Unrecoverable/unmet).
let writeMissingProvenanceSources (dir: string) : unit =
    writeFile dir layout.VersionPath "1.3.0\n"
    writeFile dir layout.MetadataPath "authors\nlicense\n"
    writeFile dir layout.PinsPath "base=9.0.0\n"
    writeFile dir layout.PublishPlanPath "plan-present\n"
    writeFile dir layout.TrustedPublishingPath "oidc\n"
// provenance.txt intentionally not written

/// Materialize a temp repo with `<repo>/.fsgg/release.yml` = `yml` and the six sources via `writeSources`,
/// run `body repoDir`, then clean up.
let withTempRepo (yml: string) (writeSources: string -> unit) (body: string -> 'a) : 'a =
    withTempDir (fun dir ->
        writeFile dir (Path.Combine(".fsgg", "release.yml")) yml
        writeSources dir
        body dir)

// ── 065: fake pack/provenance ports (the cores stay REAL; only the execution/IO edge is faked, disclosed) ──

/// A synthetic `KindedCommandRun` for a `Pack` at a given exit code (the run is recorded in every outcome,
/// never dropped — FR-001). SYNTHETIC: no real `dotnet pack` process; the run record is literal.
let synthaticPackRun (exitCode: int) : KindedCommandRun =
    let record =
        FS.GG.Governance.ExecutionRecord.ExecutionRecord.recordOf
            (Executable "dotnet")
            [ Argument "pack" ]
            (WorkingDirectory ".")
            { Added = []; Changed = []; Removed = [] }
            (TimeoutLimit 600)
            (ExitCode exitCode)
            [||]
            [||]
            NoCapturedOutput
            (SensedDuration 0L)

    { Kind = Pack; Record = record }

/// A `Packed` outcome at a real (literal) version/digest. SYNTHETIC: the artifact is a literal, not a real
/// `.nupkg` (the pure cores evaluate it for real).
let synthaticPacked (surface: string) (version: string) : PackOutcome =
    Packed(
        { Surface = SurfaceId surface
          ArtifactPath = sprintf "%s.%s.nupkg" surface version
          PackedVersion = version
          Digest = ArtifactHash(sprintf "digest-%s-%s" surface version) },
        synthaticPackRun 0
    )

let synthaticPackFailed (surface: string) (sentinel: int) : PackOutcome =
    PackFailed(SurfaceId surface, sentinel, synthaticPackRun sentinel)

/// A faked `Ports` over the REAL cores: real `Files` reader + real `Sense` (F054), with a supplied
/// capturing/faulting `Write` and a capturing `Out`, normalized provenance senses, and an injected list of
/// pack outcomes (`PackRead` replays them in request order; `Execute` is a never-failing stub the pure
/// cores never inspect). SYNTHETIC: the pack execution/output read is replayed from `packs`, disclosed here.
let portsWithPacks (repo: string) (packs: PackOutcome list) (write: Interpreter.ArtifactWriter) (out: string -> unit) : Interpreter.Ports =
    let remaining = ref packs

    { Files = FS.GG.Governance.Config.Loader.fileSystemReader repo
      Sense =
        fun lay exp ->
            FS.GG.Governance.ReleaseFactsSensing.Interpreter.senseRelease
                (FS.GG.Governance.ReleaseFactsSensing.Interpreter.realPort repo lay)
                exp
      Execute =
        fun _ ->
            { Stdout = [||]
              Stderr = [||]
              ExitCode = ExitCode 0
              Duration = SensedDuration 0L }
      PackRead =
        fun _ _ ->
            // Replay the injected outcomes in request order (the interpreter calls per declared project).
            match !remaining with
            | x :: rest ->
                remaining.Value <- rest
                x
            | [] -> failwith "portsWithPacks: more PackRead calls than injected outcomes"
      SenseHead = fun () -> Revision ""
      SenseEnvironment = fun () -> EnvironmentClass.Local
      SenseBuilder = fun () -> BuilderIdentity "fsgg"
      Write = write
      Out = out }

/// The common no-packable-projects faked ports (the existing fixtures declare no packable projects ⇒ no
/// pack call). Mirrors `realPorts` but lets a test fault the writer or capture stdout.
let portsWith (repo: string) (write: Interpreter.ArtifactWriter) (out: string -> unit) : Interpreter.Ports =
    portsWithPacks repo [] write out

// ── Repo root (for the surface baseline path) ──

let rec private findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root (FS.GG.Governance.sln) not found"
    | d ->
        if File.Exists(Path.Combine(d.FullName, "FS.GG.Governance.sln")) then
            d.FullName
        else
            findRepoRoot d.Parent

let repoRoot = findRepoRoot (DirectoryInfo(AppContext.BaseDirectory))
