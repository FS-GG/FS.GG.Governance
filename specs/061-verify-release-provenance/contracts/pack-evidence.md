# Contract: `FS.GG.Governance.PackEvidence` (pure, P1)

The central new value: packed evidence + the version policy, evaluated against the **packed artifact's**
version (research D1). Pure and total over already-sensed pack outcomes — the only I/O (running the pack,
reading the artifact) lives at the `ReleaseCommand` edge (FR-014). Feeds the **existing** F53 families via
`factContributions`; introduces no new release-rule family (FR-003).

## `Model.fsi` (draft)

```fsharp
namespace FS.GG.Governance.PackEvidence

open FS.GG.Governance.Config.Model            // SurfaceId
open FS.GG.Governance.FreshnessKey.Model       // ArtifactHash
open FS.GG.Governance.ReleaseRules.Model        // ReleaseRuleKind, FactState
open FS.GG.Governance.CommandKind.Model          // KindedCommandRun

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// The real output of packing one project, read at the host edge. Path is normalized
    /// (repo-relative, forward-slash). PackedVersion is the source of truth for releasability (D1).
    type PackArtifact =
        { Surface: SurfaceId
          ArtifactPath: string
          PackedVersion: string
          Digest: ArtifactHash }

    /// Why a zero-exit pack produced nothing usable (an input signal, never a fabricated pass — D6).
    type NoArtifactReason =
        | NoArtifactEmitted
        | ArtifactUnreadable of string

    /// The closed result of attempting to pack one project — the recorded Pack run is carried in EVERY
    /// case (never dropped, FR-001). run.Kind = Pack.
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
```

## `Pack.fsi` (draft)

```fsharp
namespace FS.GG.Governance.PackEvidence

open FS.GG.Governance.Config.Model
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.PackEvidence.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Pack =

    /// Compare a packed version against an optional baseline (the packed-version-is-truth rule, D1).
    /// None baseline ⇒ NoBaseline; None packed ⇒ NotPackable. PURE, TOTAL, never throws.
    val versionPolicy: baseline: string option -> packed: string option -> VersionVerdict

    /// Build the deterministic PackEvidenceSet from the released baselines + the sensed pack outcomes.
    /// Verdicts sorted (SurfaceId, ArtifactPath); empty outcomes ⇒ NoPackableProjects = true (D6).
    /// PURE, TOTAL.
    val evaluatePack: baselines: Map<SurfaceId, string> -> outcomes: PackOutcome list -> PackEvidenceSet

    /// Derive the Met/Unmet contributions for the VersionBump / PackageMetadata / Provenance families from
    /// real pack output (merged over the F54 sensed facts at the edge — packed evidence wins on these three,
    /// D1). A Packed+Bumped project ⇒ Met; any PackedNoArtifact/PackFailed/Unbumped/Downgraded ⇒ Unmet.
    /// PURE, TOTAL.
    val factContributions: set: PackEvidenceSet -> Map<ReleaseRuleKind, FactState>
```

## Behavioral guarantees (tested)

- A product whose every project packs at a `Bumped` version ⇒ `factContributions` all `Met` ⇒ the existing
  `Release.evaluateRelease` is not blocked on packing/versioning (SC-001 / Story 1.1).
- A `PackFailed` ⇒ `Unmet` and the failed run is in `Runs` with its sentinel (Story 1.2).
- An `Unbumped`/`Downgraded` ⇒ `Unmet`, reason names the project + version (Story 1.3).
- `PackedNoArtifact` ⇒ `Unmet`, "packed but no artifact produced" (edge case).
- Reordering the input `outcomes` ⇒ byte-identical `PackEvidenceSet.Verdicts` and `factContributions` (D7,
  SC-001.4).
- Empty `outcomes` ⇒ `NoPackableProjects = true`, no family blocked on packing (D6, edge case).
