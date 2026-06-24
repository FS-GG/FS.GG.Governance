// The sensing-domain vocabulary for F054 release-facts sensing.
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II); the matching
// Model.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — visibility is
// presence/absence here.
//
// These are the INPUTS the sensing reads against (caller expectations + source layout), the structured
// recovered evidence, the gathered bundle, and the observed-evidence snapshot. They REUSE the F053
// `ReleaseFacts`/`FactState`/`ReleaseRuleKind` and the F014 `SurfaceId` rather than redefining them
// (research D1). The OUTPUT `SensedRelease.Facts` IS the F053 `ReleaseFacts` type, handed straight to
// `Release.evaluate` with no adaptation (FR-002, SC-001). No field carries a live lookup, a network handle, or
// a serialized document — sensing reads only local repository state (FR-007); the `fsgg release` host command
// and the `release.json` projection are following rows (FR-012).

namespace FS.GG.Governance.ReleaseFactsSensing

open FS.GG.Governance.Config.Model
open FS.GG.Governance.ReleaseRules.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    // ── Caller-supplied expectations (the criteria that define "met") — research D5, FR-011 ──

    /// The product-neutral criteria that define "met" per family, SUPPLIED by the caller so the library
    /// hardcodes no package id, version, field name, pin, posture, or layout (the one-way operating rule).
    /// Each family's criterion is OPTIONAL: an absent criterion means "met cannot be decided" and resolves
    /// that family to `Unrecoverable` (fail-safe — never an assumed `Met`, edge case "expectation absent").
    type ReleaseExpectations =
        { /// The governed release surface (an F014 `SurfaceId`, typically a declared `ReleaseSurface`),
          /// carried onto the snapshot. No hardcoded identity (FR-011).
          Surface: SurfaceId
          /// The version the declared version must be bumped strictly PAST (e.g. the last released version).
          VersionBaseline: string option
          /// The metadata field names the package manifest must contain.
          RequiredMetadataFields: string list option
          /// The template→version pins that must be resolved to their expected version.
          ExpectedPins: Map<string, string> option
          /// The posture tokens the publish plan must evidence.
          RequiredPublishPosture: string list option
          /// The trusted-publishing configuration tokens required.
          RequiredTrustedPublishing: string list option
          /// The provenance / attestation tokens required.
          RequiredProvenance: string list option }

    /// The per-family RELATIVE paths the production `realPort` reads, SUPPLIED by the caller so the library
    /// assumes no directory layout (FR-011). The host row, which knows a customer's layout, supplies this.
    type SourceLayout =
        { VersionPath: string
          MetadataPath: string
          PinsPath: string
          PublishPlanPath: string
          TrustedPublishingPath: string
          ProvenancePath: string }

    // ── Structured recovered evidence (what the port yields per family) — research D3 ──

    /// The declared version recovered from the version source.
    type VersionEvidence = { Declared: string }

    /// The metadata field names present in the package manifest.
    type MetadataEvidence = { PresentFields: string list }

    /// The resolved template pins (template name → resolved version).
    type PinsEvidence = { Resolved: Map<string, string> }

    /// The observed posture/config/provenance tokens (reused by the publish-plan, trusted-publishing, and
    /// provenance families — the uniform present-token shape, research D6).
    type PostureEvidence = { Observed: string list }

    /// The gathered bundle the edge fills and the pure core consumes (Snapshot's `RawSensing` precedent).
    /// One `Result` per family: `Error` carries the read/parse failure reason ⇒ `Unrecoverable` (FR-004).
    type RecoveredEvidence =
        { Version: Result<VersionEvidence, string>
          Metadata: Result<MetadataEvidence, string>
          Pins: Result<PinsEvidence, string>
          PublishPlan: Result<PostureEvidence, string>
          TrustedPublishing: Result<PostureEvidence, string>
          Provenance: Result<PostureEvidence, string> }

    // ── Observed-evidence snapshot (the auditable detail behind each fact) — US2, research D8 ──

    /// The version evidence: both the version OBSERVED and the BASELINE it was compared against (US2.2).
    type VersionFact = { Observed: string; Baseline: string }

    /// The package-metadata evidence: the present and the missing required fields (US2.1). Both sorted (D7).
    type MetadataFact = { Present: string list; Missing: string list }

    /// The template-pin evidence: the resolved and the expected pins (key-sorted association lists) and the
    /// drifted (missing-or-mismatched) template names (D7).
    type PinsFact =
        { Resolved: (string * string) list
          Expected: (string * string) list
          Drifted: string list }

    /// The posture/config/provenance evidence: the observed and required tokens and the missing required
    /// tokens. Sorted (D7). Reused by the publish-plan, trusted-publishing, and provenance families.
    type PostureFact =
        { Observed: string list
          Required: string list
          Missing: string list }

    /// Why a family is `Unrecoverable` — an absent source, an unreadable/unparseable source, or an absent
    /// caller expectation (FR-004, research D6). Product-neutral text.
    type SensingDiagnostic =
        { Family: ReleaseRuleKind
          Reason: string }

    /// The typed snapshot of observed evidence per family (US2). Each per-family field is `Some` when the
    /// evidence was recovered (`Met`/`Unmet`) and `None` when `Unrecoverable`. `Diagnostics` is ordered by
    /// `releaseRuleKindOrdinal` (D7). A pure value: no live lookup, no host handle, no serialized document.
    type ReleaseSnapshot =
        { Surface: SurfaceId
          Version: VersionFact option
          Metadata: MetadataFact option
          Pins: PinsFact option
          PublishPlan: PostureFact option
          TrustedPublishing: PostureFact option
          Provenance: PostureFact option
          Diagnostics: SensingDiagnostic list }

    // ── The combined sensing output (research D8) ──

    /// The whole sensing result: the bare `Facts` (the F053 `ReleaseFacts` value handed straight to
    /// `Release.evaluate` — FR-002, SC-001) plus the `Snapshot` of observed evidence (US2). Produced in one
    /// sense; `Facts` alone is the P1/MVP value, `Snapshot` is the P2 audit layer over the same recovered
    /// evidence. Identical repository state + identical expectations ⇒ a structurally identical `SensedRelease` (D7).
    type SensedRelease =
        { Facts: ReleaseFacts
          Snapshot: ReleaseSnapshot }
