// Curated public signature contract for the sensed-metadata operations (F034).
//
// This .fsi is the SOLE declaration of the operations module's public surface (Constitution Principle II).
// The matching SensedMetadata.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings;
// the length-prefix / segment helpers stay unexposed by their absence here.
//
// NAMING NOTE: the operations module `SensedMetadata` and the types module `Model` are DISTINCT CLR
// entities (`FS.GG.Governance.SensedMetadata.SensedMetadataModule` vs `…ModelModule`). The operations below
// are over the `Model` vocabulary (see contracts/sensed-metadata-api.md, data-model.md).
//
// All operations are PURE and TOTAL: defined for every well-typed input, never throwing; reading no
// clock/filesystem/git/environment/network, measuring no elapsed time, spawning no process, hashing no
// bytes; byte-for-byte identical for identical input regardless of evaluation time, machine, process, or
// working directory (FR-007). They compute and alter NO reproducible identity (FR-006, identity-neutral).

namespace FS.GG.Governance.SensedMetadata

open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.SensedMetadata.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SensedMetadata =

    /// Mark an already-measured duration as a sensed metadatum with a label (FR-002). TOTAL; reads no clock,
    /// measures no elapsed time — the duration is a supplied F032 `SensedDuration`, carried verbatim. A
    /// zero-length duration is an ordinary value. L-M1: `markDuration L d = { Label = L; Value =
    /// DurationValue d }`. L-M2: the result is sensed by construction (its `Value` is a `SensedValue`).
    val markDuration: label: SensedLabel -> duration: SensedDuration -> SensedMetadatum

    /// Mark an already-measured wall-clock timestamp as a sensed metadatum with a label (FR-002). TOTAL;
    /// reads no clock — the timestamp is a supplied opaque `SensedTimestamp`, carried verbatim. L-M1:
    /// `markTimestamp L t = { Label = L; Value = TimestampValue t }`. L-M2: sensed by construction.
    val markTimestamp: label: SensedLabel -> timestamp: SensedTimestamp -> SensedMetadatum

    /// The closed, readable kind of a sensed metadatum (FR-001). TOTAL (L-K1). `kindOf { Value =
    /// TimestampValue _ } = TimestampKind`; `kindOf { Value = DurationValue _ } = DurationKind`.
    val kindOf: metadatum: SensedMetadatum -> SensedKind

    /// The stable, injective wire token for a kind: `TimestampKind -> "timestamp"`, `DurationKind ->
    /// "duration"`; the two are distinct (L-K2). TOTAL. Used inside `render`; also available to callers.
    val kindToken: kind: SensedKind -> string

    /// Render ONE sensed metadatum to its deterministic, byte-stable, unambiguously-flagged `SensedRendering`
    /// (`contracts/sensed-metadata-format.md`): the explicit `!sensed!` marker, the kind token, the label,
    /// and the value, length-prefixed. TOTAL. L-R1: starts with `!sensed!=` — a form no reproducible field
    /// tag produces — so it is distinguishable from any reproducible field. L-R2/L-R4: carries the kind,
    /// label, and value, each length-prefixed, the value verbatim (a `DurationValue` renders its `int64`
    /// nanoseconds as decimal incl. `0`; a `TimestampValue` renders its opaque string verbatim — never
    /// rounded/re-scaled, D6). L-R3: injective / unspoofable by data — content containing `!sensed!`, `;`,
    /// `:`, or `=` is read by length and cannot masquerade as the marker, another field, or absence.
    val render: metadatum: SensedMetadatum -> SensedRendering

    /// Render a list of sensed metadata as ONE clearly-marked, order-preserving `!sensed-section!` cleanly
    /// separable from a report's reproducible bytes (FR-005). TOTAL. L-S1: one
    /// `!sensed-section!=<count>;…` whose entries are `render` of each element IN GIVEN ORDER (not
    /// sorted/deduped), each length-prefixed; the empty list ⇒ `!sensed-section!=0;` (an ordinary value, not
    /// an error). L-S2: a self-delimiting value — appended to or removed from a report's reproducible bytes it
    /// leaves them byte-identical (separable).
    val renderSection: metadata: SensedMetadatum list -> SensedRendering

    /// Unwrap a `SensedRendering` to its canonical string (for storage, messages, tests). TOTAL (L-U1).
    /// `renderingValue (SensedRendering s) = s`.
    val renderingValue: rendering: SensedRendering -> string
