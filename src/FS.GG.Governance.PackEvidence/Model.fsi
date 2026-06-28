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
open FS.GG.Governance.ReleaseRules.Model

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

    // ── 088 Breaking-Change (API-Compat) gate: pure, product-neutral break/verdict vocabulary ──
    // These types carry the assembly/package comparison conclusion as DATA. No type carries raw `.nupkg`
    // bytes, host paths, timestamps, or a process exit code — the ApiCompat sensor at the I/O edge produces
    // these values; the pure core (`Pack.apiCompatibilityFact`) only grades them (data-model §2–§5).

    /// 088: whether a detected break originates in THIS package's own surface (`Local`) or became public
    /// only because an UPSTREAM package it re-exports changed (`Inherited` — FR-013, the transitive /
    /// re-exported surface edge). Carried on each break so the projection can tell "I broke it" from "an
    /// upstream change surfaced through me." `UpstreamSurface` names the upstream package by its declared id
    /// (product-neutral — a `SurfaceId` value, never a hardcoded product name).
    /// `RequireQualifiedAccess` because `Local` would otherwise collide with `EnvironmentClass.Local`
    /// wherever both modules are opened — forcing `ApiBreakOrigin.Local` keeps both unions unambiguous.
    [<RequireQualifiedAccess>]
    type ApiBreakOrigin =
        | Local
        | Inherited of upstreamSurface: SurfaceId

    /// 088: the category of ONE detected break, mirroring the ApiCompat categories surfaced. Closed; an
    /// unrecognized category is carried verbatim as `OtherIncompatibility label`. Product-neutral text.
    type ApiBreakKind =
        | MemberRemoved
        | MemberSignatureChanged
        | TypeRemoved
        | OtherIncompatibility of label: string

    /// 088: ONE detected break — enough to name it in a finding (FR-003). `Member` is the removed/changed
    /// public member as ApiCompat reports it; `Origin` distinguishes a local from an inherited break
    /// (FR-013). Product-neutral text; no raw bytes, path, or exit code.
    type ApiBreak =
        { Member: string
          Kind: ApiBreakKind
          Origin: ApiBreakOrigin }

    /// 088: what the assembly/package comparison concluded for ONE package, as DATA. Produced by the
    /// ApiCompat sensor at the I/O edge; consumed by the pure verdict helper `Pack.apiCompatibilityFact`.
    ///   • `NoBreakingChanges` — compared against a baseline, no breaking change.
    ///   • `BreakingChanges`   — one or more detected breaks (non-empty by construction).
    ///   • `NoBaseline`        — never published / baseline absent (FR-009) — distinct from a tool error.
    ///   • `Indeterminate`     — feed unreachable / package unreadable / tool error (FR-008, fail-safe).
    ///   • `NotPackable`       — not an `IsPackable` target; no fact emitted (reported `NotCovered`, FR-007).
    /// `RequireQualifiedAccess` because two of its cases (`NoBaseline`, `NotPackable`) deliberately mirror
    /// `VersionVerdict` case names — forcing `ApiBreakSignal.NoBaseline` keeps the two unions unambiguous in
    /// `Pack.fs`, which references both, and leaves the existing `versionPolicy` code unchanged (T012).
    [<RequireQualifiedAccess>]
    type ApiBreakSignal =
        | NoBreakingChanges
        | BreakingChanges of breaks: ApiBreak list
        | NoBaseline
        | Indeterminate of reason: string
        | NotPackable

    /// 088: the semantic magnitude of packed-vs-baseline, derived from the existing `Pack.versionPolicy`
    /// comparator. `NoForwardChange` = equal or downgrade (a break here is also under-bumped);
    /// `NoBaselineDelta` = no baseline to compare.
    type VersionDelta =
        | MajorBump
        | MinorOrPatchBump
        | NoForwardChange
        | NoBaselineDelta

    /// 088: the explicit per-package coverage outcome so "not covered" / "not yet enforcing" is reported,
    /// never silently clean (FR-007 / SC-001).
    ///   • `Checked`       — a baseline existed and was compared; carries the governing `FactState`.
    ///   • `NoBaselineYet` — vacuously `Met` but flagged as not-yet-enforcing (the common rollout case, D5).
    ///   • `NotCovered`    — not packable / tool could not analyze (the reason is carried, FR-007).
    type ApiCompatCoverageOutcome =
        | Checked of fact: FactState
        | NoBaselineYet
        | NotCovered of reason: string

    /// 088: one package's coverage row. The coverage list (sorted by `Surface`) feeds both the human/JSON
    /// projection and SC-001's "100% covered-or-reported, zero silent passes."
    type ApiCompatCoverage =
        { Surface: SurfaceId
          Outcome: ApiCompatCoverageOutcome }
