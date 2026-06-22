# Data Model: Agent-Review Record — Auditable Review-Record Core

The typed vocabulary of `FS.GG.Governance.ReviewRecord`, compiled `Model → ReviewRecord`. It **reuses verbatim**
the F037 review request, the F035 model/prompt identities, the F029 artifact hash, and the F034 sensed-metadatum
marking (D2), and introduces only three new types. Every value is supplied by the edge as already-formed data;
nothing here is clocked, hashed-from-bytes, persisted, or model-invoked (FR-005). Field names mirror the F032
`CommandRecord` / F033 `Provenance` record cores.

References used: `open FS.GG.Governance.PromptIsolation.Model` (request), `open
FS.GG.Governance.AgentReviewKey.Model` (model/prompt identity, transitive via F037), `open
FS.GG.Governance.FreshnessKey.Model` (`ArtifactHash`, transitive), `open FS.GG.Governance.SensedMetadata.Model`
(sensed metadatum).

## Reused vocabulary (not redefined)

| Type | Owner module | Role here |
|---|---|---|
| `ReviewRequest` | `PromptIsolation.Model` (F037) | The prompt-isolated *review request* — *what was asked*. Carried whole; its identity contribution is `PromptIsolation.render request` (D5). |
| `ModelId` | `AgentReviewKey.Model` (F035) | The judge's id — half of *model identity*. |
| `ModelVersion` | `AgentReviewKey.Model` (F035) | The judge's version — half of *model identity*. |
| `ReviewerPromptHash` | `AgentReviewKey.Model` (F035) | The reviewer-prompt hash — *prompt identity*. |
| `ArtifactHash` | `FreshnessKey.Model` (F029) | One reviewed-artifact content hash — *about which artifacts*. |
| `SensedMetadatum` | `SensedMetadata.Model` (F034) | A sensed timestamp/duration the edge attaches — held apart from identity (D6). |

## New vocabulary (this feature)

### `ResponseDigest`

```fsharp
type ResponseDigest = ResponseDigest of string
```

The supplied hash standing in for the reviewer's response — *what came back* — carrying identity without response
bytes. Opaque and comparable: the F029/F032 opaque-token discipline (no validation, no parsing; an **empty string
is a literal value**, distinct from a non-empty one). Carries **no** response bytes (FR-001, US3, SC-004).

### `RecordedVerdict`

```fsharp
type RecordedVerdict = RecordedVerdict of string
```

The final verdict produced by the review, carried as an **opaque recorded fact** — *what answer was recorded*.
This core never interprets, compares, thresholds, or promotes it (FR-007, D3); an empty string is a literal value.
Deliberately **not** a structured DU — interpreting the verdict belongs to the fifth row (advisory promotion) and
the sixth (calibration), not here.

### `RecordIdentity`

```fsharp
type RecordIdentity = RecordIdentity of string
```

The byte-stable canonical identity over the record's reproducible facts (FR-003). The wrapped string is the
canonical rendering ([contracts/review-record-identity-format.md](./contracts/review-record-identity-format.md));
equality is exact byte equality and the value is portable across runs and machines. Mirrors F032 `CommandIdentity`
and F033 `ProvenanceIdentity`.

## Key entities

### `ReproducibleFacts` — the six audit facts that carry identity

```fsharp
type ReproducibleFacts =
    { Request: ReviewRequest               // F037 — identity via PromptIsolation.render (D5)
      Model: ModelId                       // F035
      ModelVersion: ModelVersion           // F035
      PromptHash: ReviewerPromptHash       // F035
      ReviewedArtifacts: ArtifactHash list // F029 — carried verbatim, SET in identity (D4)
      ResponseDigest: ResponseDigest       // new
      Verdict: RecordedVerdict }           // new — opaque recorded fact (D3)
```

The reproducible facts of a completed review — the addressable "reproducible part of the review" value and the
**sole** input to `canonicalId` (the sensed metadata is deliberately absent, the F032 `ReproducibleFacts`
discipline). These are exactly the **six** audit facts the design names (FR-001): review request, response digest,
model identity (`Model` + `ModelVersion`), prompt identity, artifact digests, and final verdict. Model
**configuration** is intentionally not carried — the named audit fact is *model id + version* (D2).
`ReviewedArtifacts` is carried in supplied order/duplicates but compared as a SET in identity (D4); a review over
**zero** artifacts is the ordinary empty list, never malformed (Edge Cases).

### `ReviewRecord` — the complete, immutable record

```fsharp
type ReviewRecord =
    { Reproducible: ReproducibleFacts
      Sensed: SensedMetadatum list }       // F034 — held apart, EXCLUDED from identity (D6)
```

The complete review record (FR-001): all six reproducible audit facts plus any sensed metadata, none dropped or
optional-by-omission. The sensed metadata is a **separate field** of a distinct shape (the F032 `{ Reproducible;
Duration }` split, with F034's richer carrier): reachable as `record.Sensed`, structurally apart from
`record.Reproducible`, and structurally impossible to fold into `canonicalId` (D6). A consumer reads
`record.Reproducible.*` for the reproducible facts and `record.Sensed` for the sensed metadata. An empty `Sensed`
list is the ordinary "no sensed metadata attached" value. The record carries **no raw response bytes** and **no
raw, unbounded artifact bytes** — artifact content appears only inside the F037-bounded `Request` (FR-001, US3,
SC-004), and the response appears only as `Reproducible.ResponseDigest`.

## Operations (signatures; laws in [contracts/review-record-api.md](./contracts/review-record-api.md))

```fsharp
module ReviewRecord =
    val build:
        request: ReviewRequest ->
        model: ModelId ->
        modelVersion: ModelVersion ->
        promptHash: ReviewerPromptHash ->
        reviewedArtifacts: ArtifactHash list ->
        responseDigest: ResponseDigest ->
        verdict: RecordedVerdict ->
        sensed: SensedMetadatum list ->
            ReviewRecord
    val canonicalId: record: ReviewRecord -> RecordIdentity
    val identityValue: identity: RecordIdentity -> string
```

`build` is the total curried assembly (sensed last — the F032 convention, D7); `canonicalId` renders the
reproducible facts to the canonical identity (never reading `record.Sensed`); `identityValue` unwraps a
`RecordIdentity` to its string. (Naming note, the F029/F032/F033 precedent: the operations module `ReviewRecord`
and the `Model.ReviewRecord` record type are distinct CLR entities — a module suffix vs a type — sharing a name by
intent.)

## Validation & invariants

- **Totality (FR-002).** `build` is defined for every well-typed argument tuple and never throws — a zero-artifact
  request, an empty `ResponseDigest`/`RecordedVerdict`, and an empty `Sensed` list all produce ordinary complete
  records (Edge Cases).
- **Faithful carriage (SC-001).** Every supplied fact reads back unchanged: `ReviewedArtifacts` in the same order,
  the request whole, the verdict/digest verbatim, the sensed list whole. No fact is dropped, altered, or invented.
- **Identity is over reproducible facts only (FR-003/FR-004, SC-002/SC-003).** `canonicalId` reads only
  `record.Reproducible`; `record.Sensed` is never read. Records with identical reproducible facts share a
  byte-identical identity; any single differing reproducible fact (the request — including any change rendered by
  F037 —, model id, model version, prompt hash, the artifact-digest set, the response digest, or the verdict)
  yields a different identity; records differing only in `Sensed` share an identity.
- **No raw bytes (FR-001, SC-004).** No type carries raw response or raw, unbounded artifact bytes; the response is
  a digest, the reviewed artifacts are digests, and artifact content lives only inside the F037-bounded request
  (whose `BoundedExcerpt`s are bounded by construction).
- **Purity (FR-005, SC-005).** No clock, filesystem, git, environment, or network; no model/agent invoked; no hash
  computed from bytes; no time measured; no process spawned; nothing persisted. Identical inputs ⇒ identical record
  and identical identity, regardless of time, machine, or working directory.
