# Contract: Provenance Public API (F033)

The public surface of `FS.GG.Governance.Provenance` — the types in `Model.fsi` and the three operations in
`Provenance.fsi`. This is the sole contract callers (the later audit / attestation rows, the host edge, tests,
and FSI) depend on. All three operations are **pure** and **total** (FR-004, FR-009): defined for every
well-typed input, never throwing, reading no clock / filesystem / git / environment / network, spawning no
process, and hashing no bytes; byte-for-byte identical for identical input regardless of evaluation time,
machine, process, or working directory.

## Types (from `Model.fsi`)

Reused verbatim (not redeclared by this core — see `data-model.md`): `Revision`, `RuleHash`,
`GeneratorVersion`, `ArtifactHash` (F029); `CommandRecord` (F032); `EnvironmentClass` (F014).

New:

```fsharp
type BuilderIdentity = BuilderIdentity of string
type ProvenanceIdentity = ProvenanceIdentity of string

type Provenance =
    { SourceCommit:     Revision
      Base:             Revision
      Head:             Revision
      RuleHash:         RuleHash
      GeneratorVersion: GeneratorVersion
      ArtifactDigests:  ArtifactHash list
      CommandRecords:   CommandRecord list
      Environment:      EnvironmentClass
      Builder:          BuilderIdentity }
```

## `build`

```fsharp
val build:
    sourceCommit: Revision ->
    baseRevision: Revision ->
    headRevision: Revision ->
    ruleHash: RuleHash ->
    generatorVersion: GeneratorVersion ->
    artifactDigests: ArtifactHash list ->
    commandRecords: CommandRecord list ->
    environment: EnvironmentClass ->
    builder: BuilderIdentity ->
        Provenance
```

Assemble the nine supplied facts (curried in the design row's field order) into one complete `Provenance`.

**Laws.**
- **L-B1 (verbatim carriage, SC-001).** Every fact reads back unchanged: `(build sc b h rh gv ad cr env bld)`
  has `.SourceCommit = sc`, `.Base = b`, `.Head = h`, `.RuleHash = rh`, `.GeneratorVersion = gv`,
  `.ArtifactDigests = ad` (the list as supplied — no normalization here), `.CommandRecords = cr` (each record
  whole, in order), `.Environment = env`, `.Builder = bld`.
- **L-B2 (records whole, FR-002).** Each embedded `CommandRecord` is carried unchanged — all ten of its facts
  (including its sensed `Duration`) retained; never reduced to an identity-only stub, never filtered by the
  command's exit code or timeout.
- **L-B3 (totality, FR-004).** Defined for every well-typed argument tuple, never throwing — `commandRecords =
  []`, `artifactDigests = []`, `baseRevision = headRevision`, and records that failed or timed out all produce
  ordinary complete values.
- **L-B4 (no canonicalization here).** `build` does **not** sort or deduplicate the artifact digests or the
  command records; it carries them verbatim. Canonicalization is `canonicalId`'s job (so the carried value can
  faithfully report what the edge sensed, e.g. a duplicate artifact supplied twice is visible on the value but
  collapsed only in the identity).
- **L-B5 (purity).** Reads no clock / filesystem / git / environment / network; spawns no process; hashes no
  bytes.

## `canonicalId`

```fsharp
val canonicalId: provenance: Provenance -> ProvenanceIdentity
```

Render a provenance's **reproducible** facts to their canonical, deterministic, byte-stable
`ProvenanceIdentity` (`contracts/provenance-identity-format.md`).

**Laws.**
- **L-I1 (duration-free / sensed-excluded, FR-005/FR-006).** Computed only over the reproducible facts; each
  command record contributes `CommandRecord.canonicalId record` (which never reads `record.Duration`). Two
  provenances identical in every reproducible fact whose records differ **only** in duration yield the **same**
  identity.
- **L-I2 (per-field sensitivity, FR-006/FR-007).** Differing in **any** reproducible fact — `SourceCommit`,
  `Base`, `Head`, `RuleHash`, `GeneratorVersion`, the artifact-digest **set**, any command record's reproducible
  facts **or their order**, `Environment`, or `Builder` — yields a **different** identity.
- **L-I3 (artifact digests are a set, FR-008).** Reordering the artifact digests, or supplying duplicates,
  leaves the identity unchanged (deduped, ordinal-sorted in the encoding).
- **L-I4 (command records are ordered, D4).** Reordering the command records **changes** the identity (their
  identity contributions are rendered in the given order, not sorted or deduped).
- **L-I5 (injective across fields).** The same opaque string placed in two different fields (e.g. the same
  revision as `SourceCommit` and `Head`, or the same string as `RuleHash` and `GeneratorVersion`) yields
  different identities — length prefixes + unique tags guarantee it (`provenance-identity-format.md`).
- **L-I6 (determinism / byte-stability, SC-005/SC-006).** `identityValue (canonicalId p)` is byte-for-byte
  equal across repeated calls, machines, working directories, and times; equal for equal reproducible facts.
- **L-I7 (totality, no hashing).** Defined for every `Provenance` (incl. empty record/digest collections);
  never throws; computes no digest from bytes (the canonical string *is* the identity, FR-011).

## `identityValue`

```fsharp
val identityValue: identity: ProvenanceIdentity -> string
```

Unwrap a `ProvenanceIdentity` to its canonical string (for storage, messages, tests). Total.

**Law.** **L-V1.** `identityValue (ProvenanceIdentity s) = s` for all `s`.

## Out of scope (asserted by negative review, not a callable surface)

No sensing (commit / base-head resolution, artifact hashing, command execution, builder-identity reading); no
persistence; no rendering into audit.json / a provenance document; no attestation / signing; no severity /
enforcement / freshness / ship verdict; no CLI; no new third-party dependency (FR-011, FR-013). The sole outputs
are the `Provenance` value and its `ProvenanceIdentity`.
