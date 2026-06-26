// DESIGN-FIRST SKETCH (Principle I) of the curated public surface for the new test-only
// shared library `FS.GG.Governance.Tests.Common`. This is the planning artifact; the
// delivered tests/FS.GG.Governance.Tests.Common/TestsCommon.fsi is finalized during
// implementation from the three command suites' actual helper signatures (exact member sets
// and src types are pinned then). The matching .fs carries NO private/internal/public
// modifiers — visibility is presence/absence here (Principle II). A reflective
// SurfaceBaselineTests pins this surface against surface/FS.GG.Governance.Tests.Common.surface.txt.
//
// Scope rule: ONLY genuinely-duplicated, byte-identical helpers appear here. Intentional
// per-suite variants stay in that suite's local Support.fs (FR-006; research D4).

namespace FS.GG.Governance.Tests.Common

open System.IO
// open FS.GG.Governance.Config          // Loader.FileReader, catalog model
// open FS.GG.Governance.Snapshot.Model  // GitPort, RepoSnapshot, SnapshotOptions
// open FS.GG.Governance.GateExecution   // ExecutionPort
// open FS.GG.Governance.FreshnessSensing// FreshnessSensor, StoreReader
// … (the precise src-type opens are enumerated at implementation)

/// Locate the repository root and related path helpers (today's per-project `findRepoRoot`,
/// copied across 68 files). Dependency-free (System.IO only).
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RepositoryHelpers =

    /// Walk parent directories for the repo marker (`FS.GG.Governance.sln` OR `.slnx` —
    /// the superset variant, research D4); fail fast if none is found.
    val findRepoRoot: dir: DirectoryInfo | null -> string

    /// The repo root resolved from the test assembly's base directory.
    val repoRoot: string

/// Real-`git` ProcessStartInfo helper + git/exec/sensor port FAKES the command and adapter
/// suites drive (7 copies of the git helper today). Inert port values — no MVU (research D6).
/// SYNTHETIC-tagged fakes move verbatim with their disclosure comments (Principle V).
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module FakePorts =
    // Representative members (final signatures pinned at implementation):
    //   val gitWithChanges: changes:(char * string) list -> GitPort
    //   val gitEmpty / gitNotRepo / gitUnavailable: GitPort
    //   val portsGit: g:GitPort -> Ports
    //   val readerOf: files:Map<string,string> -> Loader.FileReader
    //   val fakeExecPortExiting: code:int -> ExecutionPort
    //   val fakeSensor / throwingSensor: FreshnessSensor
    //   val absentStoreReader / malformedStoreReader: StoreReader
    ()

/// Shared YAML catalog inputs: project/policy/tooling YAML + valid/empty/invalid catalog
/// builders (~387 LOC across the 3 command suites). String literals are dependency-free.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CatalogFixtures =
    //   val projectYml / policyYml / toolingYml: string
    //   val validCatalog / emptyCatalog / invalidCatalog: Map<string,string>
    ()

/// Temp-repo + file-writing snapshot builders that drive REAL git for end-to-end proofs
/// (Principle V). Writes into caller-provided temp dirs; owns no durable state.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SnapshotHelpers =
    //   val writeFile: dir:string -> relative:string -> contents:string -> unit
    //   val withTempRepo: (string -> 'a) -> 'a
    ()

/// stdout/stderr/exit-code capture utilities used to assert against goldens.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CaptureHelpers =
    //   capturing OutputSink / ArtifactWriter; redirect-and-collect helpers
    ()
