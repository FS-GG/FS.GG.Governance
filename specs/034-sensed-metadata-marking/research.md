# Phase 0 Research: Sensed-Metadata Marking Core (F034)

This row had **no `NEEDS CLARIFICATION`** in the Technical Context — the stack (F#/.NET `net10.0`, Expecto +
FsCheck, BCL-only), the architecture (a pure, total core with two `.fsi` files + a surface baseline), and the
behavior (the two sensed kinds, the by-construction flag, the flagged rendering, determinism, identity-neutrality)
are all fixed by the spec and the F015–F033 precedent. Research therefore consolidates the **planning decisions
the spec deferred to `/speckit-plan`** (Spec Assumptions) into the form: Decision / Rationale / Alternatives.

## D1 — Module home: a new pure core referencing exactly one sibling core

**Decision.** Land a new packable library `src/FS.GG.Governance.SensedMetadata` (`Model.fsi/fs` +
`SensedMetadata.fsi/fs`), referencing **only `FS.GG.Governance.CommandRecord`** to reuse F032's `SensedDuration`
verbatim. No new third-party `PackageReference`; BCL + `FSharp.Core` only.

**Rationale.** The spec Assumptions endorse a new minimal core (*"the established rhythm suggests a new minimal
core"*), continuing the F015–F033 one-new-core-per-row cadence. FR-008 mandates reusing F032's `SensedDuration`
verbatim; that type lives in `CommandRecord`, so the new core references that one project to consume it without
redefining. Unlike F033 (which needed three cores for its eight facts), this row's only reused typed fact is the
duration, so a **single** sibling reference suffices. `CommandRecord` is itself a pure vocab core (it references
only `Config`), so nothing impure (no Snapshot, no git, no host, no filesystem) is transitively pulled in;
dependency direction stays one-way (`SensedMetadata → CommandRecord → Config`), and every merged core / host
stays untouched.

**Alternatives considered.**
- *Extend an existing core (e.g. add the timestamp + marking to `CommandRecord` or `Provenance`).* Rejected — it
  would bloat a single-purpose core and entangle two surfaces under one baseline; the established rhythm is one
  new minimal core per row (Spec Assumptions).
- *Redefine the duration locally (a thin alias) so the core has zero sibling references.* Rejected — FR-008 says
  reuse F032's `SensedDuration` **verbatim**; a local alias would duplicate vocabulary the requirement says to
  reuse, and `CommandRecord` is pure so referencing it costs nothing impure.

## D2 — Duration reuses F032's `SensedDuration` verbatim; only `SensedTimestamp` is new

**Decision.** The duration kind reuses **F032's `SensedDuration`** (`SensedDuration of nanoseconds: int64`),
opened from `FS.GG.Governance.CommandRecord.Model` — never redefined. The **only genuinely new vocabulary** is
`SensedTimestamp of string` (a minimal opaque, comparable wall-clock-instant token), plus the vocabulary this
row owns: `SensedLabel of string`, the `SensedValue` DU, the `SensedKind` enum, the `SensedMetadatum` record,
and the `SensedRendering of string` newtype.

**Rationale.** FR-008 names F032's `SensedDuration` as the concrete reuse for the duration kind. No timestamp
type exists yet (F032 carries a duration but no timestamp; F033 carries no timestamp at all — Spec Key
Entities), so `SensedTimestamp` is the one new fact this row must introduce. It follows the F029 opaque-token
discipline: a single-case `string` newtype, comparable, supplied by the edge, **never read from a clock here**,
with no validation or parsing (an empty string is a literal value — FR-004, Edge cases). `SensedRendering` wraps
the canonical flagged rendering, mirroring F032's `CommandIdentity` and F033's `ProvenanceIdentity`.

**Alternatives considered.**
- *A `SensedTimestamp of int64` (epoch nanos/ticks), mirroring `SensedDuration`.* Rejected — the spec calls a
  timestamp an *opaque, comparable, already-measured instant* the edge supplies and the report renders verbatim;
  an opaque `string` imposes no format and renders the supplied instant unchanged (D6). The edge formats the
  instant however it formats it; this core does not interpret it.
- *A local duration alias instead of referencing F032.* Rejected — see D1 (violates FR-008's verbatim reuse).

## D3 — The kind is modeled as a closed two-case `SensedValue` DU (the value carries the kind)

**Decision.** Model the carried value as a closed DU `SensedValue = TimestampValue of SensedTimestamp |
DurationValue of SensedDuration`, and the metadatum as a flat record `SensedMetadatum = { Label: SensedLabel;
Value: SensedValue }`. Expose a separate closed `SensedKind = TimestampKind | DurationKind` enum with a total
`kindOf : SensedMetadatum -> SensedKind` and `kindToken : SensedKind -> string`.

**Rationale.** Putting the value inside the DU case makes **the type the flag** (FR-001): there is no way to
carry a timestamp or duration through this vocabulary without it being a `SensedValue` — i.e. sensed by
construction — and there is **no reproducible variant** of a marked timestamp or duration. The kind is intrinsic
to the case (no kind/value mismatch is representable). The separate `SensedKind` enum + `kindOf` gives the
*readable* kind (acceptance US1.3) and supplies the rendering's `kindToken` (`"timestamp"` / `"duration"`),
keeping the wire token (readable) distinct from the structural DU (the F029 precedent of separating
`InputCategory` from its terse encoding tag).

**Alternatives considered.**
- *A single record with a `Kind: SensedKind` field and an untyped/boxed value.* Rejected — it would allow a
  kind/value mismatch (a `DurationKind` carrying a timestamp) and weaken the by-construction guarantee FR-001
  requires.
- *Two separate record types (`SensedTimestampMetadatum`, `SensedDurationMetadatum`).* Rejected — it would force
  every consumer and the section renderer to handle two unrelated types; one `SensedMetadatum` with a `SensedValue`
  DU keeps the section a single `SensedMetadatum list` (FR-005) and `render` a single total function.

## D4 — Rendering: the F029/F032/F033 tagged, length-prefixed, injective discipline with a reserved `!…!` marker

**Decision.** A single sensed metadatum renders as one tagged, length-prefixed segment:

```text
!sensed!=<kindToken>;<labelByteLen>:<label>;<valueByteLen>:<value>
```

and a group renders as one counted, order-preserving section:

```text
!sensed-section!=<count>;<len1>:<r1>;<len2>:<r2>;…
```

(full bytes, edges, and a worked example in `contracts/sensed-metadata-format.md`).

**Rationale.** The encoding must satisfy three observable contracts the spec fixes (FR-003/FR-004/FR-005):
**(a) an explicit, unmistakable sensed marker** — `!sensed!` / `!sensed-section!`; the `!…!` form is **reserved**,
and no reproducible field tag in the repo (F029/F032/F033 all use lowercase-letter tags such as `src`, `exe`,
`rule`, `art`, `cmds` immediately before `=`) ever begins with `!`, so a sensed rendering is unmistakably
**distinguishable** from a reproducible field (a reader can always tell). **(b) unspoofable by its data** — every
label and value is length-prefixed (`<byteLen>:<bytes>`), so any content containing `!sensed!`, `;`, `:`, or `=`
is consumed by length and cannot masquerade as the marker, as another field, or as the absence of a value; this
is exactly the F029/F032/F033 length-prefix injectivity. **(c) one clearly-marked, separable section** — the
`!sensed-section!` envelope is a single self-delimiting value a report carries apart from its reproducible bytes,
each inner rendering itself length-prefixed (so an embedded `!sensed!`/`;`/`:` is read by length). The kind token
is a fixed total two-case map (`"timestamp"`/`"duration"`). An empty label is `0:` (distinct, unambiguous —
never collides with a missing label or the marker); an empty list is `!sensed-section!=0;` (an ordinary value,
not an error — Edge cases).

**Alternatives considered.**
- *A free-form `"sensed: <label> = <value>"` string.* Rejected — spoofable by data (a label/value containing
  `sensed:` or `=` could masquerade as the marker or a field boundary — fails FR-004) and inconsistent with the
  established encoding discipline.
- *A structured (non-string) rendering value.* Rejected for now — the spec permits either but the established
  rows (F029/F032/F033) render to a byte-stable **string** newtype for direct inclusion in a report's bytes; a
  string keeps the section trivially separable and byte-comparable. (A richer value can be layered later if a
  rendering row needs it; YAGNI now.)
- *Reusing a reproducible field tag with an extra "sensed" attribute.* Rejected — it would make the sensed marker
  a property of the value rather than the type and risk a reproducible/sensed collision; a reserved tag form is
  unambiguous by construction.

## D5 — Identity-neutrality is structural; this core owns no reproducible identity

**Decision.** This core computes **no** reproducible identity and references **no** identity-computing core
(FR-006: *"this core neither computes nor alters any reproducible identity"*). It guarantees only that a sensed
metadatum is a **separable partition** — the sensed rendering is a standalone value, and the library is
scope-guarded to reference only `CommandRecord`/`Config`/BCL. Tests demonstrate the property self-containedly: a
report modeled as `(reproducibleBytes, sensedSection)` keeps its `reproducibleBytes` byte-identical regardless of
which/how-many sensed metadata populate the section (adding/removing a metadatum changes only the sensed section).

**Rationale.** The structural exclusion of a duration from identity already exists in F032 (the `Duration` field
is outside `ReproducibleFacts`) and F033 (`canonicalId` folds only each record's reproducible identity). This row
is the **presentation half** (Spec Overview): it makes sensed-ness *visible and consistent* where a report
surfaces it, without itself computing identity. Identity-neutrality is therefore a property of *not mixing* — the
sensed section is rendered separately and no function here folds it into reproducible bytes — which is most
honestly shown by the separability test above (Principle V: real values, no mocks).

**Alternatives considered.**
- *Add a test-only reference to `FS.GG.Governance.Provenance` and assert `Provenance.canonicalId` is unchanged
  when sensed metadata are rendered alongside it.* Considered as **optional stronger evidence** (a real F033
  identity is the most faithful "report identity"), but **deferred** — it couples the F034 test project to F033
  for a property the self-contained `(reproducibleBytes, sensedSection)` model already demonstrates, and the
  scope-guard test already proves the library touches no identity core. Maintainer may opt in later.

## D6 — Value text is rendered verbatim from the supplied measured value

**Decision.** A `SensedDuration`'s `int64` nanoseconds render as their plain decimal form (including `0` for a
zero-length duration, and the full magnitude for large values); a `SensedTimestamp`'s opaque string renders
verbatim. The rendering neither rounds, re-scales, nor re-formats the magnitude beyond the deterministic,
byte-stable length-prefixed encoding (D4).

**Rationale.** The Edge cases require a zero-length duration to be marked and rendered like any other (never
"absent"), and a long-fraction/large-magnitude value to be *"carried and rendered verbatim … this core neither
rounds, re-scales, nor re-formats"*. Decimal `int64` rendering (`string` of the `int64`) is total, deterministic,
and culture-invariant for integers; the timestamp string is passed through unchanged. The length prefix carries
exactly the bytes produced.

**Alternatives considered.**
- *Render the duration as a human-friendly `1.83s`.* Rejected — it re-scales/re-formats the supplied measured
  value (violates the Edge-case verbatim rule) and risks a non-deterministic/culture-sensitive format. Friendly
  formatting, if ever wanted, belongs to a later report-rendering row, not this primitive.

## Cross-cutting facts (carried into Phase 1)

- **Purity / totality (FR-007).** Marking and rendering read no clock/filesystem/git/environment/network, measure
  no elapsed time, spawn no process, and hash no bytes; they are total over every well-typed input. Identical
  inputs ⇒ identical marked value and identical rendering, independent of cwd, time, machine, or process.
- **Surface discipline (Principle II, FR-010).** Two curated `.fsi` files are the sole public surface; a new
  `surface/FS.GG.Governance.SensedMetadata.surface.txt` baseline is guarded by a reflective `SurfaceDrift` test
  (the F029–F033 precedent) with the `BLESS_SURFACE=1` re-bless path, plus a scope-guard test pinning the
  CommandRecord/Config/BCL/FSharp.Core-only reference set.
- **No new dependency (FR-011).** The implementation is `string` building + `FSharp.Core`; the transitive
  `YamlDotNet` (via `Config`, via `CommandRecord`) is unused.
