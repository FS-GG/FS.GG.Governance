// Curated public signature contract for the packed-evidence + version-policy types (F26, P1).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// Model.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — visibility is
// presence/absence here. These REUSE the upstream vocabulary verbatim (opened, never redefined): F014
// `SurfaceId` (Config), F029 `ArtifactHash` (FreshnessKey), F053 `ReleaseRuleKind`/`FactState`
// (ReleaseRules), F25 `KindedCommandRun` (CommandKind). PackedVersion is the source of truth for
// releasability (research D1).

namespace FS.GG.Governance.PackEvidence

open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.CommandKind.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// The real output of packing one project, read at the host edge. Path is normalized (repo-relative,
    /// forward-slash). PackedVersion is the source of truth for releasability (D1).
    type PackArtifact =
        { Surface: SurfaceId
          ArtifactPath: string
          PackedVersion: string
          Digest: ArtifactHash }

    /// Why a zero-exit pack produced nothing usable (an input signal, never a fabricated pass — D6).
    type NoArtifactReason =
        | NoArtifactEmitted
        | ArtifactUnreadable of string

    /// The closed result of attempting to pack one project — the recorded Pack run is carried in EVERY case
    /// (never dropped, FR-001). run.Kind = Pack.
    type PackOutcome =
        | Packed of artifact: PackArtifact * run: KindedCommandRun
        | PackedNoArtifact of surface: SurfaceId * reason: NoArtifactReason * run: KindedCommandRun
        | PackFailed of surface: SurfaceId * sentinel: int * run: KindedCommandRun

    /// The per-project version-policy outcome, evaluated against the PACKED version vs baseline (D1).
    type VersionVerdict =
        | Bumped of baseline: string * packed: string
        | Unbumped of version: string
        | Downgraded of baseline: string * packed: string
        | NoBaseline of packed: string
        | NotPackable

    /// One project's rollup: surface, pack outcome, version verdict, self-explaining product-neutral reason.
    /// Exactly one per packable project (never dropped, never fabricated, FR-001/FR-011).
    type PackVerdict =
        { Surface: SurfaceId
          Outcome: PackOutcome
          Version: VersionVerdict
          Reason: string }

    /// The whole-product pack evidence — immutable input to the report and attestation. Verdicts sorted by
    /// SurfaceId then ArtifactPath; Runs order-significant for the snapshot (D7). NoPackableProjects is the
    /// vacuously-satisfied edge (no project blocks on packing — D6).
    type PackEvidenceSet =
        { Verdicts: PackVerdict list
          Runs: KindedCommandRun list
          NoPackableProjects: bool }
