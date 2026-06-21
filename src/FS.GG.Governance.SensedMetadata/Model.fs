// Sensed-metadata fact types for the sensed-metadata core (F034). The public surface is fixed by Model.fsi
// (Principle II); no top-level binding here carries an access modifier. These are product-neutral, clock-free
// values that `SensedMetadata.markDuration` / `markTimestamp` construct and `render` / `renderSection`
// project over; they reuse F032's `SensedDuration` verbatim (opened from
// `FS.GG.Governance.CommandRecord.Model`) rather than redefining it (FR-008). The only new fact is
// `SensedTimestamp`; the rest is this row's marking/rendering vocabulary.

namespace FS.GG.Governance.SensedMetadata

open FS.GG.Governance.CommandRecord.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type SensedLabel = SensedLabel of string

    type SensedTimestamp = SensedTimestamp of string

    type SensedKind =
        | TimestampKind
        | DurationKind

    type SensedValue =
        | TimestampValue of SensedTimestamp
        | DurationValue of SensedDuration

    type SensedMetadatum =
        { Label: SensedLabel
          Value: SensedValue }

    type SensedRendering = SensedRendering of string
