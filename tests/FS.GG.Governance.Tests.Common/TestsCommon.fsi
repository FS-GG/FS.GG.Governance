namespace FS.GG.Governance.Tests.Common

// 074 (Phase D): the curated public surface for the test-only shared support library (Principle I/II).
// Only genuinely-duplicated, byte-identical, type-compatible helpers appear here; intentional per-suite
// variants stay in that suite's local `Support.fs` (FR-006, research D4). A reflective
// `SurfaceBaselineTests` pins this surface against `surface/FS.GG.Governance.Tests.Common.surface.txt`.
//
// NOTE on CaptureHelpers (FR-002's fifth named group): the stdout/stderr/artifact capture helpers
// (`Capture`/`newCapture`/`capturingSink`/`capturingWriter`) proved NOT shareable — each command suite's
// `Capture` is parametrised by that command's own `Loop.ArtifactKind` and its sink by that command's
// `Interpreter.OutputSink`, so the text is byte-identical but the TYPES diverge per suite. Per FR-006 they
// stay local. The four modules below host every helper that is genuinely shared.

open System.IO
open FS.GG.Governance.Config
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Snapshot
open FS.GG.Governance.Snapshot.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.GateRun.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.FreshnessSensing
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.GateExecution.Model
open FS.GG.Governance.HumanText

/// Locate the repository root (today's per-project `findRepoRoot`, copied across 60 `Support.fs` files).
/// The `sln||slnx` superset variant (research D4); dependency-free (System.IO only).
module RepositoryHelpers =

    /// Walk parent directories for the repo marker (`FS.GG.Governance.sln` OR `.slnx`); fail fast if none.
    val findRepoRoot: dir: DirectoryInfo | null -> string

    /// The repo root resolved from the test assembly's base directory.
    val repoRoot: string

/// Shared YAML catalog inputs: project/policy/tooling YAML + valid/empty/invalid catalog builders, the
/// in-memory `FileReader`, the `GovernedPath` shorthand, and the validated-`TypedFacts` reader.
module CatalogFixtures =

    /// `GovernedPath` shorthand.
    val gp: s: string -> GovernedPath
    /// Trim a leading newline off a triple-quoted YAML literal.
    val yaml: s: string -> string
    val projectYml: string
    val policyYml: string
    val toolingYml: string
    val validCatalog: Map<string, string>
    val emptyCatalog: Map<string, string>
    val invalidCatalog: Map<string, string>
    /// An in-memory `FileReader` over a name→content map (a missing key is `Ok None`).
    val readerOf: files: Map<string, string> -> Loader.FileReader
    /// Read + validate a catalog map through the real `Loader`/`Schema` edge; fail fast if invalid.
    val factsOf: files: Map<string, string> -> TypedFacts

/// The git/exec/sensor port FAKES the command and adapter suites drive. Inert port values — no MVU
/// (research D6). The `SYNTHETIC:`-tagged fakes carry their disclosure comments (Principle V).
module FakePorts =

    /// A non-TTY plain-render capability (the faked-port default).
    val plainCapability: bool -> RenderMode.ColorCapability
    /// A no-op rich renderer (the Plain path never calls it).
    val noRichRender: ReportView.ReportView -> unit
    /// Encode a git `--name-status -z` payload from `(status, path)` changes.
    val diffPayload: changes: (char * string) list -> string
    /// An in-memory `GitPort` returning canned read-only output the real `Snapshot.assemble` parses.
    val gitWithChanges: changes: (char * string) list -> GitPort
    val gitEmpty: GitPort
    val gitNotRepo: GitPort
    val gitUnavailable: GitPort
    val portsGit: g: GitPort -> Ports
    /// A fully-sensing faked freshness sensor (SYNTHETIC: fixed literal digests).
    val fakeSensor: FreshnessSensing.FreshnessSensor
    /// A faked sensor whose accessor throws — the degrade probe.
    val throwingSensor: FreshnessSensing.FreshnessSensor
    val absentStoreReader: FreshnessSensing.StoreReader
    val malformedStoreReader: FreshnessSensing.StoreReader
    /// A deterministic fake `ExecutionPort` over real `byte[]` and a chosen exit code.
    val fakeExecPortExiting: code: int -> ExecutionPort

    type ExecCounter = { mutable Calls: int }

    /// An `ExecutionPort` that counts its invocations into `counter`.
    val countingExecPort: counter: ExecCounter -> code: int -> ExecutionPort

/// Real-`git` temp-repo builders (Principle V) plus the real-core snapshot/gate/evidence expectation
/// builders the command suites assert against. `git`/`writeFile` own no durable state — they write into
/// caller-provided temp dirs; the per-suite `withTempRepo` builders compose them.
module SnapshotHelpers =

    val defaultOpts: SnapshotOptions
    val sinceOpts: rev: string -> SnapshotOptions
    val snapshotOf: g: GitPort -> opts: SnapshotOptions -> RepoSnapshot
    val snapshotOfRepo: dir: string -> opts: SnapshotOptions -> RepoSnapshot
    val candidatesOf: g: GitPort -> opts: SnapshotOptions -> GovernedPath list
    val candidatesOfRepo: dir: string -> opts: SnapshotOptions -> GovernedPath list
    val revOfCommit: CommitId -> Revision
    val baseHeadOfSnap: snap: RepoSnapshot option -> Revision option * Revision option
    val selectedGatesFor: files: Map<string, string> -> candidates: GovernedPath list -> Gate list
    val expectedOutcomesWith: port: ExecutionPort -> files: Map<string, string> -> selectedGates: Gate list -> (GateId * GateOutcome) list
    val storeOf: entries: (FreshnessInputs * EvidenceRef) list -> ReuseStore
    val persistInputs: check: string -> head: string -> FreshnessInputs
    val syntheticRef: label: string -> EvidenceRef
    val readStore: path: string -> ReuseStore option
    /// Drive REAL git in a caller-provided temp dir; fail fast on non-zero exit.
    val git: dir: string -> args: string list -> string
    /// Write `content` to `dir/relPath`, creating parent directories.
    val writeFile: dir: string -> relPath: string -> content: string -> unit

/// 101 (M-CI-3): the single shared surface-drift check. One reflective public-surface projection plus
/// the baseline-equality test with the `BLESS_SURFACE=1` bless path, replacing ~80 per-project copies
/// (74 `SurfaceDriftTests.fs` + 6 `SurfaceBaselineTests.fs` + 1 `HumanRenderSurfaceDriftTests.fs`).
/// Reflection lives here and in the thin call-sites only — never in a product project (Principle II).
module SurfaceDrift =

    open System.Reflection
    open Expecto

    /// Canonical reflective projection of an assembly's public surface — byte-identical to the
    /// projection every committed `surface/*.surface.txt` baseline was blessed against.
    val renderSurface: asm: Assembly -> string

    /// Baseline-equality test: compare `renderSurface asm` (normalised) to
    /// `surface/<baselineName>.surface.txt` (repo root via `RepositoryHelpers.repoRoot`).
    /// `BLESS_SURFACE=1` (re)writes the baseline. `label` prefixes the test title.
    val surfaceTest: label: string -> baselineName: string -> asm: Assembly -> Test

    /// Scope guard: every referenced-assembly name of `asm` is BCL / FSharp.Core or satisfies
    /// `allowed`; otherwise the test fails listing the offenders.
    val referencesOnly: label: string -> allowed: (string -> bool) -> asm: Assembly -> Test

    /// Direction guard: no assembly in `upstream` references `asm` (e.g. kernel/Spi must not
    /// reference an adapter — dependency direction adapter -> Spi -> kernel).
    val noInboundReferences: label: string -> upstream: Assembly list -> asm: Assembly -> Test
