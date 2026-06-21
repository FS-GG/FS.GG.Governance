# Contract: Sensed-Metadata Public API (F034)

The public operations of `FS.GG.Governance.SensedMetadata` — module `SensedMetadata` over the `Model` vocabulary
(see [../data-model.md](../data-model.md)). All operations are **pure** and **total**: defined for every
well-typed input, never throwing, reading no clock/filesystem/git/environment/network, measuring no elapsed time,
spawning no process, hashing no bytes; byte-for-byte identical for identical input regardless of evaluation time,
machine, process, or working directory (FR-007).

## Signatures

```fsharp
namespace FS.GG.Governance.SensedMetadata

open FS.GG.Governance.CommandRecord.Model   // SensedDuration (reused verbatim, F032)
open FS.GG.Governance.SensedMetadata.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SensedMetadata =

    /// Mark an already-measured duration as a sensed metadatum with a label (FR-002). TOTAL; reads no clock,
    /// measures no elapsed time — the duration is a supplied F032 `SensedDuration`. A zero-length duration is
    /// an ordinary value.
    val markDuration: label: SensedLabel -> duration: SensedDuration -> SensedMetadatum

    /// Mark an already-measured wall-clock timestamp as a sensed metadatum with a label (FR-002). TOTAL; reads
    /// no clock — the timestamp is a supplied opaque `SensedTimestamp`.
    val markTimestamp: label: SensedLabel -> timestamp: SensedTimestamp -> SensedMetadatum

    /// The closed, readable kind of a sensed metadatum (FR-001). TOTAL.
    /// `kindOf { Value = TimestampValue _ } = TimestampKind`; `kindOf { Value = DurationValue _ } = DurationKind`.
    val kindOf: metadatum: SensedMetadatum -> SensedKind

    /// The stable, injective wire token for a kind: `TimestampKind -> "timestamp"`, `DurationKind -> "duration"`.
    /// TOTAL. Used inside `render`; also available to callers/messages.
    val kindToken: kind: SensedKind -> string

    /// Render ONE sensed metadatum to its deterministic, byte-stable, unambiguously-flagged `SensedRendering`
    /// (`contracts/sensed-metadata-format.md`): the explicit `!sensed!` marker, the kind token, the label, and
    /// the value, length-prefixed (FR-003/FR-004). TOTAL; distinguishable from any reproducible field; unspoofable
    /// by its data.
    val render: metadatum: SensedMetadatum -> SensedRendering

    /// Render a list of sensed metadata as ONE clearly-marked, order-preserving `!sensed-section!` that is cleanly
    /// separable from a report's reproducible bytes (FR-005). TOTAL; the empty list renders to `!sensed-section!=0;`
    /// (an ordinary value, not an error).
    val renderSection: metadata: SensedMetadatum list -> SensedRendering

    /// Unwrap a `SensedRendering` to its canonical string (for storage, messages, tests). TOTAL.
    /// `renderingValue (SensedRendering s) = s`.
    val renderingValue: rendering: SensedRendering -> string
```

## Laws

Let `L`/`L'` be `SensedLabel`s, `d`/`d'` be `SensedDuration`s, `t`/`t'` be `SensedTimestamp`s, `m` be a
`SensedMetadatum`, and `ms` be a `SensedMetadatum list`.

- **L-M1 (carriage).** `markDuration L d = { Label = L; Value = DurationValue d }` and
  `markTimestamp L t = { Label = L; Value = TimestampValue t }` — label and value read back verbatim (SC-001).
- **L-M2 (sensed by construction).** Every result of `markDuration` / `markTimestamp` has a `SensedValue`; there
  is no constructor that yields a reproducible variant (FR-001, SC-001).
- **L-K1 (kind).** `kindOf (markTimestamp L t) = TimestampKind`; `kindOf (markDuration L d) = DurationKind`.
- **L-K2 (token injectivity).** `kindToken TimestampKind = "timestamp"`, `kindToken DurationKind = "duration"`;
  the two tokens are distinct.
- **L-R1 (marker present & distinguishable).** `renderingValue (render m)` starts with `!sensed!=` — a form no
  reproducible field tag ever produces — so it is distinguishable from a reproducible field's rendering, in 100%
  of cases (FR-003, SC-002).
- **L-R2 (content present).** `renderingValue (render m)` contains the kind token, the label, and the value, each
  length-prefixed exactly as in `contracts/sensed-metadata-format.md` (FR-003).
- **L-R3 (unspoofable / injective).** For any labels/values, `render m = render m'` **iff** `m` and `m'` have the
  same kind, label, and value. No label or value — including one whose text contains `!sensed!`, `;`, `:`, or `=`
  — can make two different metadata render equal or make a metadatum render as another field or as absence
  (FR-004, SC-002). An empty label renders to a distinct `0:` form.
- **L-R4 (verbatim value).** A `DurationValue (SensedDuration ns)` renders its value as the decimal of `ns`
  (incl. `0`); a `TimestampValue (SensedTimestamp s)` renders its value as `s` verbatim — never rounded,
  re-scaled, or re-formatted (D6, Edge cases).
- **L-S1 (section grouping & order).** `renderSection ms` is one `!sensed-section!=<count>;…` whose entries are
  `render` of each element **in given order** (order-preserving, not sorted/deduped), each length-prefixed; the
  empty list ⇒ `!sensed-section!=0;` (FR-005, SC-004).
- **L-S2 (separability).** A `!sensed-section!` is a single self-delimiting value; appended to or removed from a
  report's reproducible bytes it leaves those bytes byte-identical — the sensed and reproducible partitions are
  cleanly separable (FR-005, FR-006).
- **L-D1 (determinism).** `markDuration` / `markTimestamp` / `render` / `renderSection` are pure functions of
  their inputs: the same input yields a byte-identical result every time (SC-004).
- **L-P1 (purity).** No operation reads a clock, filesystem, git, environment, or network, measures elapsed time,
  spawns a process, or hashes bytes; results are identical across working directories, times, machines, and
  processes (FR-007, SC-005).
- **L-N1 (identity-neutrality).** No operation computes or alters any reproducible identity; a sensed metadatum
  contributes nothing to any identity a report computes over its reproducible facts (FR-006, SC-003).
- **L-U1 (unwrap).** `renderingValue (SensedRendering s) = s`, and `renderingValue (render m)` is the canonical
  rendering string.

## Out of scope (this contract asserts their ABSENCE)

No clock read / timing; no digest from bytes; no filesystem/git/environment/network; no persistence; no complete
report document (no `audit.json`, no provenance document, no route report — only the individual rendering and the
section); no attestation/signing; no CLI (FR-009). The sole outputs are the `SensedMetadatum` value(s) and their
`SensedRendering`.
