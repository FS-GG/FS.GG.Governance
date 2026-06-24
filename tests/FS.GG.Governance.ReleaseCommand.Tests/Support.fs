module FS.GG.Governance.ReleaseCommand.Tests.Support

open System
open System.IO
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseFactsSensing
open FS.GG.Governance.ReleaseFactsSensing.Model
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

/// The compliant declaration (parsed from the fixture yml — exercises the real adapter).
let compliantDeclaration: Declaration.ReleaseDeclaration = parseYml releaseYmlAllBlocking

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

/// A faked `Ports` over the REAL cores: real `Files` reader + real `Sense` (F054), with a supplied
/// capturing/faulting `Write` and a capturing `Out`. Mirrors `realPorts` but lets a test fault the writer
/// or capture stdout without touching the real console/atomic writer.
let portsWith (repo: string) (write: Interpreter.ArtifactWriter) (out: string -> unit) : Interpreter.Ports =
    { Files = FS.GG.Governance.Config.Loader.fileSystemReader repo
      Sense =
        fun lay exp ->
            FS.GG.Governance.ReleaseFactsSensing.Interpreter.senseRelease
                (FS.GG.Governance.ReleaseFactsSensing.Interpreter.realPort repo lay)
                exp
      Write = write
      Out = out }

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
