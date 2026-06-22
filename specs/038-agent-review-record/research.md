# Phase 0 Research: Agent-Review Record — Auditable Review-Record Core

All Technical Context unknowns are resolved below. There were **no** `NEEDS CLARIFICATION` markers: the spec
fixes the observable contract (faithful capture of the six audit facts, a deterministic injective identity over
the reproducible facts, exclusion of sensed metadata, purity over supplied values) and explicitly defers a closed
set of *shaping* decisions to planning. Each decision is recorded as **Decision / Rationale / Alternatives
considered**, anchored to the established F029 / F032 / F033 / F034 / F035 / F037 precedent. This row is the
**agent-review analogue of F032 `CommandRecord` / F033 `Provenance`** — a pure record-building core that assembles
a complete typed record from already-sensed facts and derives a byte-stable canonical identity over its
reproducible facts.

---

## D1 — A new pure-core library, `FS.GG.Governance.ReviewRecord`, with two sibling references

**Decision.** Add one new packable pure-core library, **`FS.GG.Governance.ReviewRecord`**, compiled
`Model → ReviewRecord`, with **two** `ProjectReference`s: `FS.GG.Governance.PromptIsolation` (F037) and
`FS.GG.Governance.SensedMetadata` (F034). No new third-party `PackageReference`.

**Rationale.** This row is the analogue of **F033 `Provenance`**, which is itself a record-assembling core that
references **three** siblings (`Config`, `FreshnessKey`, `CommandRecord`) to reuse their vocabulary verbatim. The
spec Assumptions endorse this — *"Whether this core references the owning cores directly (the F033 three-sibling
precedent) or carries thin local aliases … the established rhythm suggests direct references."* The two references
yield every reused fact:

- **F037 `PromptIsolation`** provides the **review request** (`ReviewRequest`) and its canonical
  **`render`** (the injective rendering used as the request's identity contribution, D5). Transitively — because
  F037 references F035 `AgentReviewKey`, which references F029 `FreshnessKey` — it also makes the F035 model/prompt
  vocabulary (`ModelId`, `ModelVersion`, `ReviewerPromptHash`) and F029's `ArtifactHash` available by `open`,
  with no extra reference (the F037→F035→F029 transitive-reference shape).
- **F034 `SensedMetadata`** provides the **sensed-metadata** carrier (`SensedMetadatum` + `markTimestamp` /
  `markDuration` / `SensedTimestamp` / `SensedDuration` / `SensedLabel`) for the honesty boundary (D6).

The dependency direction stays one-way and acyclic:
`ReviewRecord → { PromptIsolation → AgentReviewKey → FreshnessKey → Config ; SensedMetadata → CommandRecord →
Config }` (a diamond on `Config`/`FreshnessKey`, which .NET resolves cleanly). Every merged core/host stays
untouched.

**Alternatives considered.**
- *A single reference (F037 only), carrying no sensed metadata.* Simpler and closer to the F036/F037
  single-reference rhythm, but it forgoes the demonstrable honesty boundary (D6) and the spec's explicit steer to
  the F034 marking. Rejected — this row is the F033 *record* analogue, where multi-sibling reuse is the precedent,
  and the sensed boundary is the heart of the F032/F033 lineage. (See D6 for why sensed metadata is carried.)
- *Reference `AgentReviewKey` / `FreshnessKey` directly as well (four references).* Honest about the direct use of
  `ModelId`/`ArtifactHash`, but heavier than needed — transitive project references flow as compile references, so
  `open FS.GG.Governance.AgentReviewKey.Model` / `open FS.GG.Governance.FreshnessKey.Model` resolve through the
  F037 reference. Rejected for the minimal reference set; the scope-guard test names them as allowed (transitive)
  references.
- *Extend an existing core (e.g. `PromptIsolation`).* Rejected — it would modify a merged surface/baseline
  (violating SC-006 and additive-only) and mix *prompt shape* with *audit record*, two distinct concerns. The
  spec Assumptions favour a new minimal core.

---

## D2 — Reuse five facts verbatim; introduce three new types

**Decision.** Reuse, verbatim and unmodified:

| Audit fact | Reused type | Owner |
|---|---|---|
| review request | `ReviewRequest` | F037 `PromptIsolation.Model` |
| model identity (id) | `ModelId` | F035 `AgentReviewKey.Model` (transitive) |
| model identity (version) | `ModelVersion` | F035 `AgentReviewKey.Model` (transitive) |
| prompt identity | `ReviewerPromptHash` | F035 `AgentReviewKey.Model` (transitive) |
| artifact digests | `ArtifactHash list` | F029 `FreshnessKey.Model` (transitive) |
| sensed metadata | `SensedMetadatum` | F034 `SensedMetadata.Model` |

Introduce only the minimal new vocabulary the row needs:

- **`ResponseDigest`** = `ResponseDigest of string` — the supplied hash of the reviewer's response, an opaque
  token carrying **no** response bytes (the F029/F032 `OutputDigest` discipline).
- **`RecordedVerdict`** = `RecordedVerdict of string` — the final recorded verdict, an **opaque recorded fact**
  (D3).
- **`RecordIdentity`** = `RecordIdentity of string` — the canonical, byte-stable identity (mirrors F032
  `CommandIdentity` / F033 `ProvenanceIdentity`).

**Rationale.** FR-006 names the reuses concretely — the model-identity and prompt-identity vocabulary from F035,
the artifact-hash vocabulary from F029, and the prompt-isolated review-request value from F037. Each maps cleanly
to an audit fact and is reused single-sourced. The three new types have no existing home: there is no response-
digest value (F032's `OutputDigest` is command stdout/stderr, a different fact), no recorded-verdict value (F036's
`VerdictRef` is an opaque *store handle*, not the verdict the design names as an audit fact), and no record-
identity type for this record.

**Alternatives considered.**
- *Reuse F036 `VerdictRef` for the verdict.* Rejected — `VerdictRef` is "an opaque handle to an already-cached
  verdict" used by the verdict *store* (F036); the audit fact here is the *final recorded verdict itself*, a
  distinct concept. Coupling to F036 would also pull the cache-store vocabulary into an auditability core that
  must run no cache operation (FR-007).
- *Reuse F032 `OutputDigest` for the response digest.* Rejected — `OutputDigest` is documented as a digest of a
  *command's* stdout/stderr; reusing it for a *model response* would overload its meaning. A dedicated
  `ResponseDigest` keeps the audit vocabulary honest at one extra newtype's cost.
- *Group model id + version into a `ModelIdentity` record.* Rejected — F035 already carries them as two flat
  fields; keeping them flat in `ReproducibleFacts` (the F032/F033 flat-record style) reuses F035 verbatim without
  a new grouping type. Model **configuration** is deliberately **not** carried: the design names the audit fact as
  *model id + version* (FR-001), and config is part of the F035 *cache key*, not this audit record.

---

## D3 — The verdict and the response digest are opaque recorded facts (strings), never interpreted

**Decision.** Both `RecordedVerdict` and `ResponseDigest` wrap a `string` supplied by the edge. This core never
parses, validates, compares, thresholds, or promotes either. An empty string is a literal value, distinct from a
non-empty one.

**Rationale.** FR-007 is emphatic: the verdict is carried **as an opaque recorded fact only** — *"MUST NOT promote
any finding from advisory to blocking (the fifth row), MUST NOT interpret, compare, or threshold the verdict, and
MUST NOT define any judge-vs-human calibration (the sixth row)."* A structured DU (`Pass | Fail | …`) would invite
exactly the interpretation the fifth/sixth rows own and pre-empt their design. An opaque string is the faithful
"record a verdict already produced, with an honest identity" choice — the same opaque-token discipline F032 uses
for `OutputDigest` and F036 uses for `VerdictRef`. The response digest is identically opaque (the spec: *"a
supplied opaque token … no validation, no parsing"*).

**Alternatives considered.**
- *A closed verdict DU (`Pass`/`Fail`/`Inconclusive`).* Rejected — it interprets the verdict and overlaps the
  advisory-vs-blocking promotion row (the fifth), violating FR-007's "MUST NOT interpret".
- *Carry the raw response bytes alongside the digest.* Rejected — US3/FR-001/SC-004 forbid raw response bytes; the
  response appears **only** as its digest. Artifact content appears **only** inside the F037-bounded request.

---

## D4 — The reviewed-artifact digests are compared as a SET in identity (order- and duplicate-insensitive)

**Decision.** `ReproducibleFacts.ReviewedArtifacts` is an `ArtifactHash list`, **carried verbatim** in the value
(supplied order/duplicates preserved) but compared as a **SET** (deduplicated, ordinal-sorted) in `canonicalId` —
the F035 `artifactSet`/`artSegment` discipline, reused verbatim.

**Rationale.** *Which* artifacts a review covered is an order-independent identity, exactly as F035's
`ReviewedArtifacts` (the cache key's artifact set) and F033's `ArtifactDigests` (the provenance artifact set).
Two records reviewing the same artifacts supplied in a different order, or with a duplicate, are the **same**
review for audit purposes and must share an identity (Edge Cases: *"Duplicate or identical artifact digests … the
same supplied digests always yield the same record identity"*). Carrying the list verbatim while setting it in
identity matches F033's *"carried verbatim but treated as a SET in the identity"* exactly.

**Alternatives considered.**
- *Order-significant sequence (mirror F037's data channel).* Rejected — F037 keeps artifacts ordered because a
  *reviewer reads them in sequence* (presentation order matters for the prompt). Here the artifact digests record
  *which artifacts were reviewed* — an identity, where order is noise. The reviewed-as-presented order already
  lives inside the embedded `ReviewRequest` (and thus in the `req` identity segment, D5); the artifact-digest set
  is the separate "which artifacts" identity fact, set-compared like F035/F033.

---

## D5 — The record identity: tagged, length-prefixed, injective — the F032/F035 discipline; the request contributes its F037 rendering

**Decision.** `canonicalId : ReviewRecord -> RecordIdentity` renders the **reproducible facts only** as tagged,
length-prefixed segments joined by `'\n'` (no trailing newline), in the F029/F032/F035 injective discipline (full
grammar in [contracts/review-record-identity-format.md](./contracts/review-record-identity-format.md)). The
**request** segment is the F037 rendering of the embedded request — `PromptIsolation.render request |>
renderedValue` — carried as one length-prefixed payload. Field order and tags:

| # | Field | Tag | Encoding |
|---|---|---|---|
| 1 | Request | `req` | required string = the F037 `RenderedPrompt` of the request |
| 2 | Model | `mid` | required string |
| 3 | ModelVersion | `mver` | required string |
| 4 | PromptHash | `pph` | required string |
| 5 | ReviewedArtifacts | `art` | **set segment** (deduped, ordinal-sorted, counted) — the F035 `artSegment` |
| 6 | ResponseDigest | `resp` | required string |
| 7 | Verdict | `vdt` | required string |

The sensed metadata (D6) is **never rendered**. Required strings use the F035/F037 plain form
(`<tag>=<utf8ByteLen>:<value>`, no presence digit — every reproducible field is required, so there is no absence
case, exactly as F035/F037).

**Rationale.** Injectivity and byte-stability are what make the record citable and comparable (FR-003, SC-002),
exactly as F032 `canonicalId` and F033 `ProvenanceIdentity`. Reusing F037's `render` for the `req` segment is the
direct **F033 analogue** — F033 contributes each embedded `CommandRecord` to its identity via
`CommandRecord.canonicalId record` (the owning core's own canonical encoding), so this core contributes the
embedded request via `PromptIsolation.render request` (F037's own injective, byte-stable rendering). Because that
rendering is already injective over the request's instructions and ordered data channel, *any* difference in the
request (instructions, an artifact payload, an excerpt's content/bound/truncation, a digest) changes the `req`
segment and thus the identity; and because model/prompt/response/verdict are *separate* segments, two requests
that render identically but differ in judge or prompt identity still yield different identities (Edge Cases). The
length prefix on every value means any structural character or marker inside a value is read as data and cannot
forge a field boundary (Edge Cases: *"Field content that contains the encoding's tag characters … read as data by
length"*).

**Alternatives considered.**
- *Embed the `RenderedPrompt` directly in the record (store the rendering, not the structured request).* A valid
  reading of FR-001 (*"the F037 prompt-isolated request **or its rendering**"*). Rejected for the value's shape —
  carrying the structured `ReviewRequest` is richer for audit (a consumer can re-inspect the instruction and data
  channels) and still derives identity from the rendering, giving both. (The identity *contribution* is the
  rendering either way.)
- *Re-walk the request's `ArtifactPayload`/`BoundedExcerpt` internals to build a fresh per-field encoding.*
  Rejected — it would duplicate F037's render logic and risk drifting from it; reusing `PromptIsolation.render` is
  single-sourced and matches the F033 "embed-and-reuse-the-owner's-canonicalId" precedent.

---

## D6 — Sensed metadata is carried (reusing F034 `SensedMetadatum`) and held structurally apart from identity

**Decision.** The complete record splits into reproducible facts and sensed metadata, mirroring F032's
`{ Reproducible; Duration }`:

```fsharp
type ReviewRecord =
    { Reproducible: ReproducibleFacts
      Sensed: SensedMetadatum list }   // F034, excluded from identity
```

`Sensed` is a list of F034 `SensedMetadatum` (e.g. a `markTimestamp` "when the review ran", a `markDuration`).
`canonicalId` reads **only** `record.Reproducible` and **never** `record.Sensed`, so two records identical in
every reproducible fact but differing in (or absent of) sensed metadata share an identity.

**Rationale.** The spec's Edge Cases steer this directly: *"Any sensed timestamp/duration the edge attaches is
held structurally apart from the reproducible facts and excluded from identity (the F032 `SensedDuration` / F033
honesty boundary, and exactly the marking F034 (`SensedMetadata`) provides)."* Carrying sensed metadata makes the
honesty boundary **demonstrable** (SC-003, US2 scenario 3, Story 2's independent test: *"supply only
sensed/non-deterministic metadata differing between two otherwise-identical records and assert the identity is
unchanged"*) rather than vacuous. A `list` handles "carries none" (empty list) and "carries some" uniformly, and
reuses F034 — the row the spec explicitly names — verbatim. The split is the F032 `CommandRecord` shape with the
richer F034 carrier; identity is structurally over `Reproducible` only, so sensed data is *impossible* to fold in
(the F032 D2 guarantee, achieved by the field split + reading only `Reproducible`).

**Alternatives considered.**
- *Carry no sensed metadata.* Spec-compliant (FR-004 hedges *"if it does, this boundary applies"*), and the
  honesty boundary would hold *vacuously* (no sensed field to leak). Rejected — it makes SC-003 / US2-3 / the
  Story-2 independent test undemonstrable (no differing sensed value to vary), and forgoes genuinely useful audit
  data ("when was this reviewed"). The F032 lineage exists precisely to carry sensed facts honestly; this row
  should too.
- *Carry a single `SensedDuration` (mirror F032 exactly).* Rejected — the natural sensed fact for a completed
  review is *when it ran* (a timestamp), and F034 already provides `SensedTimestamp` + the general `SensedMetadatum`
  marking. A `SensedMetadatum list` is the most faithful reuse of F034 and the most flexible (timestamp and/or
  duration), at no identity cost.
- *A `SensedMetadatum option` (zero-or-one).* Rejected — a list is no more complex to render-never/exclude and
  lets the edge attach both a timestamp and a duration; F034's `renderSection` already operates on a list.

---

## D7 — `build` is a total curried assembly; carriage is verbatim

**Decision.** `ReviewRecord.build` takes the supplied facts curried in the audit-fact order, with the sensed list
last (the F032 `build` shape — sensed argument last), and assembles a complete `ReviewRecord`. It is **total**
over all supplied values (a request over zero artifacts, empty/unusual digests, an empty verdict, an empty sensed
list) and performs no normalization, reorder, or dedup — canonicalization is `canonicalId`'s job (the F032/F033
`build` discipline). Each fact reads back from the value unchanged.

**Rationale.** FR-002 requires a total build; FR-005 requires purity; SC-001 requires every fact carried exactly
as supplied with none dropped. A curried, order-preserving, normalization-free assembly is precisely F032
`CommandRecord.build` / F033 `Provenance.build`, reused in shape. Placing `sensed` last keeps the reproducible
facts grouped first and the sensed carrier visibly separate at the call site (the F032 `duration`-last
convention).

**Alternatives considered.**
- *Take a pre-built `ReproducibleFacts` record + sensed list (two arguments).* A valid shape, but the F032/F033
  precedent curries the individual facts so the call site names each audit fact explicitly. Rejected for
  consistency with the sibling record cores; the curried form also reads as the design row's fact list.

---

## Resolved Technical Context summary

| Unknown | Resolution |
|---|---|
| Module/assembly home and name | New core `FS.GG.Governance.ReviewRecord`, `Model → ReviewRecord` (D1) |
| Project references | `PromptIsolation` (F037) + `SensedMetadata` (F034); F035/F029/Config transitive (D1) |
| Which existing types are reused | `ReviewRequest` (F037); `ModelId`/`ModelVersion`/`ReviewerPromptHash` (F035); `ArtifactHash` (F029); `SensedMetadatum` (F034) (D2) |
| Which types are new | `ResponseDigest`, `RecordedVerdict`, `RecordIdentity` (D2) |
| Embedded request: structured or rendered | Carry the structured `ReviewRequest`; identity via `PromptIsolation.render` (D5) |
| Verdict / response-digest shape | Opaque `string` newtypes — never interpreted, compared, or thresholded (D3) |
| Artifact digests: set or sequence | SET in identity (deduped, ordinal-sorted) — the F035/F033 discipline (D4) |
| Identity format | Tagged, UTF-8-length-prefixed, injective, `'\n'`-joined; `req` = F037 rendering (D5) |
| Sensed metadata | Carried as `SensedMetadatum list` (F034), held apart, excluded from identity (D6) |
| `build` shape | Total curried assembly, sensed last, verbatim carriage, no canonicalization (D7) |
| New third-party dependency | None — BCL `System.Text` + `FSharp.Core` + reused siblings only (FR-009) |
| MVU boundary | N/A — pure total functions over supplied values (Principle IV) |
