module FS.GG.Governance.ReleaseCommand.Tests.Support

open System
open System.IO
open System.Diagnostics
open System.Security.Cryptography
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

// ── 066 (US1/US2): the REAL `dotnet pack` pack-boundary fixture — a real temp multi-project tree, a real
//    pack-output reader, and an SDK probe. Nothing here is synthetic: every `dotnet pack` is a real process
//    run through `GateExecution.Interpreter.realPort`, and `realPackReadInto` reads the produced `.nupkg`
//    bytes off disk (a real reader, never a replay). The only `Synthetic`-named element is the deliberately
//    BuildFails / NoArtifact project rigged to provoke a non-happy outcome — the pack execution is real. ──

/// What a generated fixture project should do when packed. The surface doubles as the `PackageId` and the
/// project dir/file name, so a `Buildable` project packs to `<surface>.<version>.nupkg`.
type ProjectKind =
    | Buildable // a normal net10.0 library that packs to a real `.nupkg`
    | BuildFails // SYNTHETIC: a deliberate compile error so `dotnet pack` exits non-zero (failed pack)
    | NoArtifact // SYNTHETIC: `IsPackable=false` ⇒ `dotnet pack` exits 0 but emits no `.nupkg`

/// One generated packable project: its surface/PackageId, its `<Version>`, an optional declared baseline
/// (None ⇒ first release), and what its pack should do.
type PackProjectSpec =
    { Surface: string
      Version: string
      Baseline: string option
      Kind: ProjectKind }

let buildable surface version baseline =
    { Surface = surface
      Version = version
      Baseline = Some baseline
      Kind = Buildable }

/// The fsproj text for one fixture project. `<Deterministic>`/`<ContinuousIntegrationBuild>` make the packed
/// `.nupkg` byte-identical across packs (SC-003); `IsPackable=false` realises the zero-exit-no-artifact edge.
let private fsprojText (spec: PackProjectSpec) : string =
    let isPackable =
        match spec.Kind with
        | NoArtifact -> "false"
        | _ -> "true"

    "<Project Sdk=\"Microsoft.NET.Sdk\">\n"
    + "  <PropertyGroup>\n"
    + "    <TargetFramework>net10.0</TargetFramework>\n"
    + sprintf "    <Version>%s</Version>\n" spec.Version
    + sprintf "    <PackageId>%s</PackageId>\n" spec.Surface
    + "    <Authors>fsgg</Authors>\n"
    + "    <Description>fsgg release-pack fixture (066)</Description>\n"
    + "    <Deterministic>true</Deterministic>\n"
    + "    <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>\n"
    + "    <EnableSourceLink>false</EnableSourceLink>\n"
    + sprintf "    <IsPackable>%s</IsPackable>\n" isPackable
    + "    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>\n"
    + "  </PropertyGroup>\n"
    + "  <ItemGroup><Compile Include=\"Lib.fs\" /></ItemGroup>\n"
    + "</Project>\n"

/// The one source file. `BuildFails` plants a type error so the F# compiler — hence `dotnet pack` — fails.
let private libText (spec: PackProjectSpec) : string =
    match spec.Kind with
    // SYNTHETIC: an intentional type error forces a real non-zero `dotnet pack` exit (the failed-pack case).
    | BuildFails -> "module Lib\nlet broken: int = \"not an int\"\n"
    | _ -> "module Lib\nlet value = 1\n"

/// Write one fixture project under `<repo>/<surface>/` (the fsproj + its single source file).
let writeProject (repo: string) (spec: PackProjectSpec) : unit =
    writeFile repo (Path.Combine(spec.Surface, spec.Surface + ".fsproj")) (fsprojText spec)
    writeFile repo (Path.Combine(spec.Surface, "Lib.fs")) (libText spec)

/// The `.fsgg/release.yml` for a real-pack tree: the all-blocking rules/expectations/layout (so the six
/// non-pack families are governed) PLUS a `packableProjects` entry per spec whose `packCommand` runs a REAL
/// `dotnet pack` with RELATIVE paths only — `workingDirectory: "."` (resolved against the test process CWD,
/// which `withRealPackRepo` pins to the repo) and `--output artifacts`, so NO machine path enters the pack
/// command identity that the attestation serializes (FR-006).
let realPackYml (specs: PackProjectSpec list) : string =
    let entry (s: PackProjectSpec) =
        let baseline =
            match s.Baseline with
            | Some b -> sprintf "    baseline: \"%s\"\n" b
            | None -> ""

        sprintf "  - surface: %s\n" s.Surface
        + "    packCommand:\n"
        + "      executable: dotnet\n"
        + sprintf
            "      arguments: [pack, %s/%s.fsproj, --output, artifacts, --configuration, Release, --nologo, -v, quiet]\n"
            s.Surface
            s.Surface
        + "      workingDirectory: \".\"\n"
        + baseline

    releaseYmlAllBlocking + "packableProjects:\n" + (specs |> List.map entry |> String.concat "")

let private cwdLock = obj ()

/// Materialize a real multi-project temp repo (`.fsgg/release.yml` + the six met sources + the generated
/// projects), then run `body repo` with the process CWD pinned to `repo` (so the RELATIVE pack commands
/// resolve) under a lock, restoring the original CWD afterward. SEQUENCED at the call site so the global CWD
/// swap never races another test. The repo is deleted on the way out.
let withRealPackRepo (specs: PackProjectSpec list) (body: string -> 'a) : 'a =
    withTempRepo (realPackYml specs) writeMetSources (fun repo ->
        for s in specs do
            writeProject repo s

        lock cwdLock (fun () ->
            let original = Directory.GetCurrentDirectory()
            Directory.SetCurrentDirectory repo

            try
                body repo
            finally
                Directory.SetCurrentDirectory original))

/// Parse the package version out of a `<surface>.<version>.nupkg` filename for a known surface — the file is
/// produced by the surface's own pack command, so the version is exactly the substring between the surface
/// prefix and the `.nupkg` suffix.
let private versionFromArtifact (surface: string) (fileName: string) : string =
    let withoutSuffix =
        if fileName.EndsWith ".nupkg" then
            fileName.Substring(0, fileName.Length - ".nupkg".Length)
        else
            fileName

    let prefix = surface + "."

    if withoutSuffix.StartsWith prefix then
        withoutSuffix.Substring prefix.Length
    else
        withoutSuffix

/// The REAL pack-output reader (US1, T002), per-surface so a multi-project tree resolves correctly: a
/// non-zero exit ⇒ `PackFailed` (the sentinel is the real exit code, the run carried, never dropped); a
/// zero exit whose `<repo>/artifacts/<surface>.*.nupkg` is present ⇒ `Packed` with the artifact's real
/// packed version + a real SHA-256 `ArtifactHash` over the `.nupkg` bytes; a zero exit with no such artifact
/// ⇒ `PackedNoArtifact NoArtifactEmitted`; an unreadable artifact ⇒ `PackedNoArtifact (ArtifactUnreadable …)`.
/// The `ArtifactPath` is normalized to the bare filename so the release/attestation bytes are
/// machine-independent (FR-006). A real reader, never a replay.
let realPackReadInto (repo: string) : SurfaceId -> KindedCommandRun -> PackOutcome =
    fun (SurfaceId surface as sid) run ->
        let (ExitCode code) = run.Record.Reproducible.ExitCode

        if code <> 0 then
            PackFailed(sid, code, run)
        else
            try
                let dir = Path.Combine(repo, "artifacts")

                if not (Directory.Exists dir) then
                    PackedNoArtifact(sid, NoArtifactEmitted, run)
                else
                    match
                        Directory.GetFiles(dir, surface + ".*.nupkg")
                        |> Array.sortWith (fun a b -> String.CompareOrdinal(a, b))
                        |> Array.tryHead
                    with
                    | None -> PackedNoArtifact(sid, NoArtifactEmitted, run)
                    | Some path ->
                        use sha = SHA256.Create()
                        let digest = File.ReadAllBytes path |> sha.ComputeHash |> Convert.ToHexString

                        let fileName =
                            match Path.GetFileName path with
                            | null -> path
                            | f -> f

                        Packed(
                            { Surface = sid
                              ArtifactPath = fileName
                              PackedVersion = versionFromArtifact surface fileName
                              Digest = ArtifactHash(digest.ToLowerInvariant()) },
                            run
                        )
            with e ->
                PackedNoArtifact(sid, ArtifactUnreadable e.Message, run)

/// The REAL edge ports for the pack-boundary fixture: the wired `065` `realPorts` (real F51 execution port,
/// real senses, atomic writer) with the per-surface real `PackRead` swapped in for the global nuget-local
/// reader and stdout silenced. Driving `Interpreter.run` with these over a `withRealPackRepo` tree runs real
/// `dotnet pack` processes — the whole point of US1.
let realPackPorts (repo: string) : Interpreter.Ports =
    { Interpreter.realPorts repo with
        PackRead = realPackReadInto repo
        Out = ignore }

/// Probe for a working `dotnet pack` SDK. Returns None when the SDK is present, or Some diagnostic naming
/// the problem when it is absent — so the real-pack tests surface a DISCLOSED skip, never a silent green
/// (FR-008). Runs `dotnet --version`; a non-zero exit or a throw (e.g. `dotnet` not on PATH) ⇒ skip reason.
let dotnetSdkSkipReason () : string option =
    try
        let psi = ProcessStartInfo("dotnet", "--version")
        psi.RedirectStandardOutput <- true
        psi.RedirectStandardError <- true
        psi.UseShellExecute <- false

        match Process.Start psi with
        | null -> Some "SKIPPED (FR-008): could not start `dotnet` to probe the SDK — no real `dotnet pack`."
        | proc ->
            use proc = proc
            proc.WaitForExit()

            if proc.ExitCode = 0 then
                None
            else
                Some(
                    sprintf
                        "SKIPPED (FR-008): `dotnet --version` exited %d — no working SDK for a real `dotnet pack`."
                        proc.ExitCode
                )
    with e ->
        Some(sprintf "SKIPPED (FR-008): `dotnet` SDK probe threw (%s) — no real `dotnet pack`." e.Message)

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
