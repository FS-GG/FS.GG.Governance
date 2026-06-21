// Curated public signature contract for the sensed-metadata types (F034).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The
// matching Model.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — visibility
// is presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any Model.fs body
// exists (Principle I). These are the product-neutral, clock-free values the `SensedMetadata.markDuration` /
// `markTimestamp` / `render` / `renderSection` operations construct and project over. They REUSE F032's
// `SensedDuration` verbatim — opened from `FS.GG.Governance.CommandRecord.Model` — never redefined (FR-008).
// The only genuinely new fact is `SensedTimestamp` (an opaque, supplied, never-clocked wall-clock instant —
// no timestamp type existed before this row); everything else is this row's marking/rendering vocabulary.
// Nothing here carries raw bytes, a clock reading, or product vocabulary.

namespace FS.GG.Governance.SensedMetadata

open FS.GG.Governance.CommandRecord.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// The opaque label of the report field a sensed value annotates (e.g. `"at"`, `"elapsed"`). The F029
    /// opaque-token discipline: no validation, no parsing; an EMPTY string is a literal value (FR-004, Edge
    /// cases), distinct from a missing label and from the marker. Comparable.
    type SensedLabel = SensedLabel of string

    /// An already-measured wall-clock instant, supplied by the edge as an opaque, comparable string — NEVER
    /// read from a clock here (D2). The ONLY genuinely new fact type this row adds: no timestamp type existed
    /// before (F032 carries a duration but no timestamp, F033 carries no timestamp). No validation, no
    /// parsing; an empty string is a literal value. Rendered verbatim (D6).
    type SensedTimestamp = SensedTimestamp of string

    /// The closed, readable set of non-deterministic kinds the design names — a wall-clock `timestamp` and a
    /// `duration` (FR-001). Returned by `kindOf`, tokenized by `kindToken` (`"timestamp"` / `"duration"`) for
    /// the rendering. No other kind is in scope.
    type SensedKind =
        | TimestampKind
        | DurationKind

    /// The already-measured value a sensed metadatum carries. THE TYPE IS THE FLAG (FR-001, D3): a value can
    /// only be carried as a `SensedValue`, so it is sensed by construction, and there is NO reproducible
    /// variant. The kind is intrinsic to the case — `TimestampValue` is a `TimestampKind`, `DurationValue` is
    /// a `DurationKind` — so no kind/value mismatch is representable. The duration arm carries F032's
    /// `SensedDuration` verbatim (FR-008).
    type SensedValue =
        | TimestampValue of SensedTimestamp
        | DurationValue of SensedDuration

    /// One complete, typed, explicitly-flagged sensed value (FR-001): the `Label` of the field it annotates
    /// and the carried `Value`. Sensed by construction (its `Value` is a `SensedValue`); identity-neutral (it
    /// is never folded into any reproducible identity — D5). Its kind/label/value are all readable:
    /// `metadatum.Label`, `metadatum.Value` (match for kind + value), and `SensedMetadata.kindOf metadatum`.
    type SensedMetadatum =
        { Label: SensedLabel
          Value: SensedValue }

    /// The deterministic, byte-stable, unambiguously-flagged rendering of a sensed metadatum (or of a group
    /// as one `!sensed-section!`), produced by `render` / `renderSection`
    /// (`contracts/sensed-metadata-format.md`). The wrapped string carries the explicit `!sensed!` marker,
    /// the kind token, the label, and the value, in the F029/F032/F033 tagged, length-prefixed, injective
    /// discipline; equality is exact byte equality. Mirrors F032's `CommandIdentity` and F033's
    /// `ProvenanceIdentity`. Unwrapped via `renderingValue`.
    type SensedRendering = SensedRendering of string
