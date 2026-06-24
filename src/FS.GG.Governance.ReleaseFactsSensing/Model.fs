// The sensing-domain vocabulary for F054 release-facts sensing — the data declarations.
// Visibility lives in Model.fsi (Principle II); no `private`/`internal`/`public` modifiers here. These are
// pure data (caller inputs, recovered evidence, observed-evidence snapshot, the combined output) — no
// behavior, so no stub. They REUSE the F053 `ReleaseFacts`/`FactState`/`ReleaseRuleKind` and F014 `SurfaceId`
// verbatim (research D1) and introduce no new fact or family vocabulary.

namespace FS.GG.Governance.ReleaseFactsSensing

open FS.GG.Governance.Config.Model
open FS.GG.Governance.ReleaseRules.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type ReleaseExpectations =
        { Surface: SurfaceId
          VersionBaseline: string option
          RequiredMetadataFields: string list option
          ExpectedPins: Map<string, string> option
          RequiredPublishPosture: string list option
          RequiredTrustedPublishing: string list option
          RequiredProvenance: string list option }

    type SourceLayout =
        { VersionPath: string
          MetadataPath: string
          PinsPath: string
          PublishPlanPath: string
          TrustedPublishingPath: string
          ProvenancePath: string }

    type VersionEvidence = { Declared: string }

    type MetadataEvidence = { PresentFields: string list }

    type PinsEvidence = { Resolved: Map<string, string> }

    type PostureEvidence = { Observed: string list }

    type RecoveredEvidence =
        { Version: Result<VersionEvidence, string>
          Metadata: Result<MetadataEvidence, string>
          Pins: Result<PinsEvidence, string>
          PublishPlan: Result<PostureEvidence, string>
          TrustedPublishing: Result<PostureEvidence, string>
          Provenance: Result<PostureEvidence, string> }

    type VersionFact = { Observed: string; Baseline: string }

    type MetadataFact = { Present: string list; Missing: string list }

    type PinsFact =
        { Resolved: (string * string) list
          Expected: (string * string) list
          Drifted: string list }

    type PostureFact =
        { Observed: string list
          Required: string list
          Missing: string list }

    type SensingDiagnostic =
        { Family: ReleaseRuleKind
          Reason: string }

    type ReleaseSnapshot =
        { Surface: SurfaceId
          Version: VersionFact option
          Metadata: MetadataFact option
          Pins: PinsFact option
          PublishPlan: PostureFact option
          TrustedPublishing: PostureFact option
          Provenance: PostureFact option
          Diagnostics: SensingDiagnostic list }

    type SensedRelease =
        { Facts: ReleaseFacts
          Snapshot: ReleaseSnapshot }
