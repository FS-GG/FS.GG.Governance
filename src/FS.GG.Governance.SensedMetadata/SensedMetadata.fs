// Sensed-metadata operations for the sensed-metadata core (F034). The public surface is fixed by
// SensedMetadata.fsi (Principle II); no top-level binding here carries an access modifier — the length-prefix
// helper stays unexposed by its absence from the .fsi. `markDuration` / `markTimestamp` are pure record
// construction (no clock, no normalization); `kindOf` / `kindToken` are total closed matches; `render`
// renders ONE metadatum behind the reserved `!sensed!` marker in the F029/F032/F033 tagged, length-prefixed,
// injective discipline (contracts/sensed-metadata-format.md); `renderSection` groups a list into one
// order-preserving `!sensed-section!`. All operations are pure, total, deterministic, and identity-neutral:
// no clock/filesystem/git/environment/network; no process spawn; no hashing; no reproducible identity
// computed or altered. BCL string building only (FR-011).

namespace FS.GG.Governance.SensedMetadata

open System.Text
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.SensedMetadata.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SensedMetadata =

    // ── Marking: total, pure record construction (US1) ──

    let markDuration (label: SensedLabel) (duration: SensedDuration) : SensedMetadatum =
        // Verbatim carriage — no clock read, no elapsed-time measure, no normalization. The duration is the
        // supplied F032 `SensedDuration` (FR-008). The `SensedValue` DU is the flag: sensed by construction.
        { Label = label; Value = DurationValue duration }

    let markTimestamp (label: SensedLabel) (timestamp: SensedTimestamp) : SensedMetadatum =
        // Verbatim carriage — no clock read. The timestamp is the supplied opaque `SensedTimestamp` (D2).
        { Label = label; Value = TimestampValue timestamp }

    let kindOf (metadatum: SensedMetadatum) : SensedKind =
        // The kind is intrinsic to the `SensedValue` case (D3) — total over the closed two-case DU.
        match metadatum.Value with
        | TimestampValue _ -> TimestampKind
        | DurationValue _ -> DurationKind

    let kindToken (kind: SensedKind) : string =
        // Total injective two-case map — the readable wire token (contracts/sensed-metadata-format.md).
        match kind with
        | TimestampKind -> "timestamp"
        | DurationKind -> "duration"

    // ── Segment encoder (internal; hidden by SensedMetadata.fsi) — the F029/F032/F033 discipline (D4) ──

    // A length-prefixed scalar: "<utf8ByteLen>:<bytes>". The length prefix makes the encoding injective: a
    // reader consumes exactly `<byteLen>` bytes, so no value can contain a character (`!`, `;`, `:`, `=`,
    // `\n`) that lets it masquerade as the marker, as another field, or bleed across a boundary (FR-004). An
    // EMPTY string renders as "0:" — a distinct, unambiguous form that never collides with absence or the
    // marker (Edge cases).
    let lenPrefixed (s: string) : string = sprintf "%d:%s" (Encoding.UTF8.GetByteCount s) s

    // ── Flagged rendering (US2) ──

    let render (metadatum: SensedMetadatum) : SensedRendering =
        // The carried value's verbatim text (D6): an `int64` nanoseconds duration as its decimal form (incl.
        // `0`, negatives), an opaque timestamp string verbatim — never rounded or re-scaled.
        let valueText =
            match metadatum.Value with
            | TimestampValue (SensedTimestamp s) -> s
            | DurationValue (SensedDuration ns) -> string ns

        let (SensedLabel label) = metadatum.Label

        // "!sensed!=<kindToken>;<labelLen>:<label>;<valueLen>:<value>" — the reserved `!sensed!` marker (a
        // form no reproducible field tag produces, FR-003) followed by the length-prefixed kind/label/value.
        sprintf "!sensed!=%s;%s;%s" (kindToken (kindOf metadatum)) (lenPrefixed label) (lenPrefixed valueText)
        |> SensedRendering

    let renderSection (metadata: SensedMetadatum list) : SensedRendering =
        // One order-preserving "!sensed-section!=<count>;<len1>:<r1>;<len2>:<r2>;…" — entries are the FULL
        // `render` string of each element in GIVEN order (not sorted/deduped — a report decides its own
        // order, a repeated value is a real repeat), each length-prefixed so its embedded `!sensed!`/`;`/`:`
        // is read by length. Empty list ⇒ "!sensed-section!=0;" (an ordinary value, not an error).
        let body =
            metadata
            |> List.map (fun m ->
                let (SensedRendering r) = render m
                lenPrefixed r)
            |> String.concat ";"

        sprintf "!sensed-section!=%d;%s" (List.length metadata) body
        |> SensedRendering

    let renderingValue (rendering: SensedRendering) : string =
        let (SensedRendering s) = rendering
        s
