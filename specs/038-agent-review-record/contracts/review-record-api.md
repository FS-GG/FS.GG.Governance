# Contract: ReviewRecord Public API — signatures, laws, and scope guard

The public surface of `FS.GG.Governance.ReviewRecord` is two `.fsi` files (`Model.fsi`, `ReviewRecord.fsi`) — the
sole declaration of visibility (Principle II). All operations are **pure and total** (FR-002, FR-005): defined for
every well-typed input, never throwing, reading no clock/filesystem/git/environment/network, invoking no
model/agent, hashing no bytes, spawning no process, persisting nothing; byte-for-byte identical for identical input
regardless of evaluation time, machine, process, or working directory.

## Types (full vocabulary in [data-model.md](../data-model.md))

```fsharp
// Model.fsi
type ResponseDigest = ResponseDigest of string
type RecordedVerdict = RecordedVerdict of string
type RecordIdentity = RecordIdentity of string

type ReproducibleFacts =
    { Request: ReviewRequest
      Model: ModelId
      ModelVersion: ModelVersion
      PromptHash: ReviewerPromptHash
      ReviewedArtifacts: ArtifactHash list
      ResponseDigest: ResponseDigest
      Verdict: RecordedVerdict }

type ReviewRecord =
    { Reproducible: ReproducibleFacts
      Sensed: SensedMetadatum list }
```

## Operations

```fsharp
// ReviewRecord.fsi
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

## `build` — total assembly of the six audit facts + sensed metadata

Assembles the supplied facts (curried in the design row's audit-fact order, sensed last — the F032 `build`
convention) into one complete `ReviewRecord`.

- **L-B1 (totality, FR-002).** Defined for every well-typed argument tuple; never throws. `reviewedArtifacts =
  []`, `ResponseDigest ""`, `RecordedVerdict ""`, and `sensed = []` all produce ordinary complete records.
- **L-B2 (verbatim carriage, SC-001).** Every fact reads back unchanged:
  `(build req m v p arts resp vdt sensed).Reproducible` = `{ Request = req; Model = m; ModelVersion = v; PromptHash
  = p; ReviewedArtifacts = arts; ResponseDigest = resp; Verdict = vdt }` and `.Sensed = sensed`. The artifact list
  keeps its **supplied order and duplicates**; the sensed list is kept whole and in order. No fact is dropped,
  altered, or invented.
- **L-B3 (sensed held apart, D6).** `sensed` is placed in `record.Sensed` and **nowhere** in
  `record.Reproducible`. There is no constructor path that folds a sensed value into a reproducible fact.
- **L-B4 (no canonicalization).** `build` performs no reorder, dedup, normalization, capture, hashing, or I/O —
  canonicalization (the artifact set, the request rendering) is `canonicalId`'s job.
- **L-B5 (determinism, SC-005).** `build` is a pure function of its arguments: identical arguments ⇒ a record that
  is equal by structural equality, regardless of time/machine/cwd.

## `canonicalId` — deterministic, injective identity over the reproducible facts

Renders `record.Reproducible` to the canonical `RecordIdentity`
([review-record-identity-format.md](./review-record-identity-format.md)).

- **L-I1 (purity, FR-005).** Reads no clock/filesystem/git/environment/network; invokes no model; hashes no bytes;
  BCL string building only (plus `PromptIsolation.render` for the request segment, itself pure).
- **L-I2 (reproducible-only, FR-004/SC-003).** Computed **only** over `record.Reproducible`; `record.Sensed` is
  **never** read. Therefore two records identical in every reproducible fact but differing only in `Sensed` (or one
  with `Sensed = []`, one not) share a byte-identical identity.
- **L-I3 (determinism & byte-stability, SC-002/SC-005).** Identical reproducible facts (artifact digests compared
  as a set) ⇒ byte-identical identity, regardless of time/machine/cwd.
- **L-I4 (injective over reproducible facts, FR-003/SC-002).** Any single differing reproducible fact yields a
  different identity:
  - the **request** — any difference that changes `PromptIsolation.render request` (instructions, an artifact
    payload, an excerpt's content/bound/truncation, a digest, payload order/count);
  - `Model`, `ModelVersion`, `PromptHash`, `ResponseDigest`, or `Verdict` — any change to the unwrapped string;
  - the **artifact-digest set** — adding/removing a distinct digest (reordering or duplicating an existing one does
    **not** change identity, L-I5).
- **L-I5 (artifacts as a SET, D4).** `ReviewedArtifacts` is deduplicated and ordinal-sorted before encoding (the
  F035 `artifactSet`); supplying the same digests in a different order or with duplicates yields the same identity.
- **L-I6 (injective across fields).** The same opaque string placed in two different fields (e.g. as
  `ResponseDigest` vs `Verdict`, or as `ModelVersion` vs `PromptHash`) yields different identities — length
  prefixes + unique tags (the F032/F035 discipline). Field content containing tag characters, separators, or the
  F037 fence markers is read as data by length and cannot forge a boundary.
- **L-I7 (request distinctness vs identity, Edge Cases).** Two records whose embedded requests render identically
  but whose model/prompt identity differs yield different identities — the `req`, `mid`/`mver`, and `pph` segments
  are independent.
- **L-I8 (no hashing).** The canonical string **is** the identity; no digest is computed from its bytes.

## `identityValue` — unwrap

- **L-V1.** `identityValue (RecordIdentity s) = s`. TOTAL.

## Scope guard (Principle II surface drift + reference hygiene)

- The reflective `SurfaceDrift` test pins the public surface to
  `surface/FS.GG.Governance.ReviewRecord.surface.txt` (the F029–F037 precedent), with the `BLESS_SURFACE=1`
  re-bless path.
- The assembly references **only** the sibling pure cores `PromptIsolation` (F037) and `SensedMetadata` (F034) —
  and, transitively through them, `AgentReviewKey` (F035), `FreshnessKey` (F029), `CommandRecord` (F032), and
  `Config` (F014). It references **no** host/edge/CLI assembly (no Gates/Snapshot/Route/Findings/EvidenceReuse/
  VerdictReuse/Adapters/Host/Cli) and adds **no** new third-party `PackageReference` (FR-009). The scope-guard test
  asserts exactly this allow-list.
