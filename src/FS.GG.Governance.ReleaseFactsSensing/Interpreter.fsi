// Curated public signature contract for the EDGE of release-facts sensing (F054).
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II); the matching
// Interpreter.fs carries NO `private`/`internal`/`public` modifiers — the per-source file readers/parsers and
// the exception-reifying gather helpers live ONLY in the .fs and are absent here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any Interpreter.fs body
// exists (Principle I).
//
// This is the EDGE side of the I/O boundary (Constitution Principle IV): the ONLY impure code in the feature
// (research D2). It reads the six governing sources through a single injected `RepositoryPort` (the real one
// reads LOCAL files via BCL `System.IO` against an injected repo directory + caller `SourceLayout`), gathers
// a `RecoveredEvidence`, and applies the pure `Sensing.deriveFacts`. It NEVER throws out of itself: a missing/
// unreadable/unparseable source or a thrown read exception becomes the matching `Error` and then an
// `Unrecoverable` family with a `SensingDiagnostic` (FR-004, mirrors the F016 Snapshot / F08 Host edges).
// NETWORK-FREE by construction (FR-007, SC-004): the production port reaches no registry, publishing
// provider, or other endpoint — proven by the surface-test dependency scope guard that bans
// `System.Net.Http`/`Octokit`/`LibGit2Sharp` (research D4). No new dependency.

namespace FS.GG.Governance.ReleaseFactsSensing

open FS.GG.Governance.ReleaseFactsSensing.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =

    /// The single injected SENSING port: one read function per release family, each recovering that family's
    /// structured evidence (`Ok`) or a failure reason (`Error`) — the FreshnessSensing `FreshnessSensor`
    /// precedent (research D3). Confining all impurity to this one value satisfies "a single injected effects
    /// boundary" (FR-006). The real port reads local files; tests back it with a REAL temp fixture repository
    /// (Principle V, US3) or a hand-built fake.
    type RepositoryPort =
        { ReadVersion: unit -> Result<VersionEvidence, string>
          ReadMetadata: unit -> Result<MetadataEvidence, string>
          ReadPins: unit -> Result<PinsEvidence, string>
          ReadPublishPlan: unit -> Result<PostureEvidence, string>
          ReadTrustedPublishing: unit -> Result<PostureEvidence, string>
          ReadProvenance: unit -> Result<PostureEvidence, string> }

    /// Build the REAL port for a repository working directory and a caller-supplied `SourceLayout`: each read
    /// function reads its `layout` path under `repoDir` via `System.IO`, parses the bytes into the structured
    /// evidence, and returns `Error` when the file is absent, unreadable, or unparseable. This is the ONLY
    /// place the feature touches the filesystem; it starts NO process, opens NO socket, and references NO
    /// registry/publishing-provider SDK (FR-007, SC-004, research D4).
    val realPort: repoDir: string -> layout: SourceLayout -> RepositoryPort

    /// Run the six port read functions and gather a `RecoveredEvidence` bundle, CATCHING any thrown exception
    /// and reifying it as `Error` (FR-004) — so a port that throws still yields a well-formed bundle (that
    /// family becomes `Unrecoverable`, never a crash). TOTAL and SAFE: never throws. Exposed so a test can
    /// inspect the gathered bundle independently of the pure derivation.
    val gather: port: RepositoryPort -> RecoveredEvidence

    /// Sense the whole release-facts value + snapshot against the injected port and the caller's expectations
    /// — the single composition of edge I/O + the pure core (`gather port |> Sensing.deriveFacts expectations`).
    /// The single entry the future `fsgg release` host row wires (sense → F053 `Release.evaluate` → exit code).
    ///
    /// TOTAL and SAFE (FR-004, FR-009, SC-002): every port `Error` and every thrown exception is reified — an
    /// absent/unreadable/unparseable source becomes `Unrecoverable` (never a fabricated `Met`, never a thrown
    /// error), and the result ALWAYS carries all six families. DETERMINISTIC (FR-008, SC-003): identical
    /// repository state + identical expectations ⇒ a structurally identical `SensedRelease`. NETWORK-FREE (FR-007,
    /// SC-004): reaches no registry, publishing provider, or other endpoint. The returned `Facts` is the F053
    /// `ReleaseFacts` type, accepted by `Release.evaluate` with no adaptation (FR-002, SC-001).
    val senseRelease: port: RepositoryPort -> expectations: ReleaseExpectations -> SensedRelease
