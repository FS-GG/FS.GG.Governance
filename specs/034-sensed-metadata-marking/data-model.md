# Phase 1 Data Model: Sensed-Metadata Marking Core (F034)

The typed vocabulary the F034 core introduces and reuses. Two public modules in the
`FS.GG.Governance.SensedMetadata` assembly: **`Model`** (the values below) and **`SensedMetadata`** (the
operations — see [contracts/sensed-metadata-api.md](./contracts/sensed-metadata-api.md)). Everything here is
product-neutral and carries no raw bytes, clock reading, or report vocabulary. Compile order is `Model →
SensedMetadata`.

## Reused verbatim (NOT redefined)

| Type | Source module | Why reused |
|---|---|---|
| `SensedDuration` (`SensedDuration of nanoseconds: int64`) | `FS.GG.Governance.CommandRecord.Model` (F032) | FR-008 mandates reusing F032's `SensedDuration` verbatim for the duration kind. Opened, never redefined. The sensed wall-clock duration as an opaque `int64`-nanoseconds measure. |

## New vocabulary (`FS.GG.Governance.SensedMetadata.Model`)

### `SensedLabel` — the field label a sensed value annotates

```fsharp
type SensedLabel = SensedLabel of string
```

The opaque label of the report field this sensed value annotates (e.g. `"at"`, `"elapsed"`). The F029
opaque-token discipline: no validation, no parsing; an **empty string is a literal value** (FR-004, Edge cases),
distinct from a missing label and from the marker. Comparable.

### `SensedTimestamp` — an already-measured wall-clock instant (the only genuinely new fact)

```fsharp
type SensedTimestamp = SensedTimestamp of string
```

An already-measured wall-clock instant, supplied by the edge as an opaque, comparable string — **never read from
a clock here** (D2). No timestamp type existed before this row (F032 carries a duration but no timestamp; F033
carries no timestamp). No validation, no parsing; an empty string is a literal value. Rendered verbatim (D6).

### `SensedKind` — the closed, readable set of sensed kinds

```fsharp
type SensedKind =
    | TimestampKind
    | DurationKind
```

The closed set of non-deterministic kinds the design names — a wall-clock **timestamp** and a **duration**
(FR-001). The readable kind, returned by `kindOf` and tokenized by `kindToken` (`"timestamp"` / `"duration"`)
for the rendering. No other kind is in scope.

### `SensedValue` — the carried value; the kind is intrinsic to the case

```fsharp
type SensedValue =
    | TimestampValue of SensedTimestamp
    | DurationValue of SensedDuration
```

The already-measured value a sensed metadatum carries. **The type is the flag** (FR-001): a value can only be
carried as a `SensedValue`, so it is sensed by construction, and there is **no reproducible variant**. The kind
is intrinsic to the case — `TimestampValue` is a `TimestampKind`, `DurationValue` is a `DurationKind` — so no
kind/value mismatch is representable (D3). The duration arm carries F032's `SensedDuration` verbatim.

### `SensedMetadatum` — one typed, explicitly-flagged sensed value (the unit a report surfaces)

```fsharp
type SensedMetadatum =
    { Label: SensedLabel
      Value: SensedValue }
```

The complete sensed metadatum (FR-001): the label of the field it annotates and the carried `SensedValue`.
Sensed by construction (its `Value` is a `SensedValue`); identity-neutral (it is never folded into any
reproducible identity — D5). Its kind, label, and value are all readable: `metadatum.Label`, `metadatum.Value`
(match for kind + value), and `SensedMetadata.kindOf metadatum` (the closed `SensedKind`).

### `SensedRendering` — the byte-stable, unambiguously-flagged rendering

```fsharp
type SensedRendering = SensedRendering of string
```

The deterministic, byte-stable rendering of a sensed metadatum (or of a group as one `!sensed-section!`),
produced by `render` / `renderSection` (`contracts/sensed-metadata-format.md`). The wrapped string carries the
explicit `!sensed!` marker, the kind token, the label, and the value, in the F029/F032/F033 tagged,
length-prefixed, injective discipline; equality is exact byte equality. Mirrors F032's `CommandIdentity` and
F033's `ProvenanceIdentity`. Unwrapped via `renderingValue`.

## Entity relationships

```text
SensedMetadatum
├── Label : SensedLabel                  (opaque, possibly empty)
└── Value : SensedValue
            ├── TimestampValue of SensedTimestamp   (NEW opaque instant token)
            └── DurationValue  of SensedDuration    (REUSED verbatim from F032)

kindOf  : SensedMetadatum -> SensedKind  ( TimestampKind | DurationKind )
render  : SensedMetadatum -> SensedRendering           ( !sensed!=… )
renderSection : SensedMetadatum list -> SensedRendering ( !sensed-section!=… )
```

## Validation & invariants (from the spec)

- **Sensed by construction (FR-001, SC-001).** Every `SensedMetadatum` is sensed; there is no representation of a
  marked timestamp or duration that is reproducible. Enforced by the `SensedValue` DU (D3).
- **Totality (FR-002, FR-003, VI).** `markDuration` / `markTimestamp` / `kindOf` / `kindToken` / `render` /
  `renderSection` / `renderingValue` are defined for every well-typed input — zero-length duration, empty label,
  same-label/different-kind, empty list, and marker-containing text are all ordinary values, never errors.
- **Unspoofable by data (FR-004, SC-002).** The length-prefixed encoding means no label or value can masquerade
  as the marker, another field, or the absence of a value. An empty label renders to a distinct `0:` form.
- **Separable section (FR-005, SC-004).** A `SensedMetadatum list` renders as one `!sensed-section!` cleanly
  separable from a report's reproducible bytes; the empty list is `!sensed-section!=0;`.
- **Identity-neutral (FR-006, SC-003).** A sensed metadatum contributes nothing to any reproducible identity;
  this core computes none (D5).
- **Pure & deterministic (FR-007, SC-004/005).** No clock/filesystem/git/environment/network; no timing, no
  process, no hashing; identical inputs ⇒ identical marked value and rendering.
- **Verbatim value (D6, Edge cases).** Duration → decimal `int64` nanoseconds (incl. `0`); timestamp → opaque
  string verbatim; never rounded, re-scaled, or re-formatted.
