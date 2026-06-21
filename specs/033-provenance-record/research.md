# Phase 0 Research: Provenance Core (F033)

This row had **no `NEEDS CLARIFICATION`** in the Technical Context — the stack (F#/.NET `net10.0`, Expecto +
FsCheck, BCL-only), the architecture (a pure, total core with two `.fsi` files + a surface baseline), and the
behavior (the eight facts, the sensed/reproducible split, the canonical-identity rules, determinism) are all
fixed by the spec and the F015–F032 precedent. Research therefore consolidates the **planning decisions the
spec deferred to `/speckit-plan`** (Spec Assumptions) into the form: Decision / Rationale / Alternatives.

## D1 — Module home: a new pure core referencing three sibling cores

**Decision.** Land a new packable library `src/FS.GG.Governance.Provenance` (`Model.fsi/fs` +
`Provenance.fsi/fs`), referencing **`FS.GG.Governance.FreshnessKey`**, **`FS.GG.Governance.CommandRecord`**, and
**`FS.GG.Governance.Config`** — all reused verbatim. This is the **first core to reference more than one sibling
core**; every prior Phase-11 core (F029, F030, F031, F032) referenced only `Config`. No new third-party
`PackageReference`; BCL + `FSharp.Core` only.

**Rationale.** FR-010 mandates reusing the existing typed facts verbatim where one maps to a declared provenance
fact: F029's `RuleHash` / `GeneratorVersion` / `ArtifactHash` / `Revision`, F032's `CommandRecord` (and its
public `canonicalId` / `identityValue`), and F014's `EnvironmentClass`. Those live in three different projects,
so the provenance core must reference all three to consume them without redefining. The spec explicitly endorses
this (*"the established rhythm suggests direct references"*). The multi-reference does **not** breach the
"helper core stays minimal — MUST NOT depend on git / filesystem scanning" engineering constraint, because all
three referenced projects are themselves pure config/vocab cores: nothing impure (no Snapshot, no git, no host,
no filesystem) is transitively pulled in. Dependency direction stays one-way (`Provenance → { FreshnessKey,
CommandRecord, Config }`); every merged core / host stays untouched.

**Alternatives considered.**
- *Extend an existing core (e.g. add provenance to `CommandRecord` or `FreshnessKey`).* Rejected — it would
  bloat a single-purpose core and entangle two surfaces under one baseline; the established rhythm is one new
  minimal core per row (Spec Assumptions: *"the established rhythm suggests a new minimal core"*).
- *A thin shared "vocabulary" project re-exporting F029/F032 types so the new core has one reference.* Rejected
  — it adds an indirection layer and a new surface for no behavioral gain; direct references are simpler and are
  what the spec suggests.
- *Redefine the reused tokens locally (as F029 did with `Revision` vs Snapshot's `CommitId`).* Rejected here —
  F029 redefined `Revision` to avoid referencing the **git-sensing Snapshot assembly** (an impure dependency).
  F029, F032, and Config are **pure**, so referencing them directly costs nothing and is exactly the verbatim
  reuse FR-010 requires; redefining would *violate* FR-010 ("reuse … verbatim … redefines none of them").

## D2 — Source commit reuses `Revision`; only `BuilderIdentity` is new

**Decision.** Base and head are F029 `Revision` (mandated). The **source commit also reuses `Revision`**. The
**only** genuinely new vocabulary is `BuilderIdentity of string`. A `ProvenanceIdentity of string` newtype wraps
the canonical identity (mirroring F032's `CommandIdentity`).

**Rationale.** The source commit *is* a resolved revision — the same opaque, comparable revision identity as
base/head (Spec Key Entities name it "the resolved revision the evidence was built against"). Reusing `Revision`
keeps the new vocabulary minimal (one builder token) and consistent. The three revisions never collide in the
identity because each is a **distinct tagged segment** (`src` / `base` / `head`), so injectivity across fields is
preserved (D5). `BuilderIdentity` is a minimal opaque comparable token following the F029 opaque-token
discipline (no validation, no parsing; an empty string is a literal value — FR-011, Edge cases).

**Alternatives considered.**
- *A dedicated `SourceCommit` newtype.* Rejected — it adds a type for a value the design already treats as a
  revision; `Revision` already carries the right opaque-comparable semantics and the spec lists reuse as the
  leading candidate. (If a future row needs to distinguish a source commit from a branch revision structurally,
  it can be introduced then; YAGNI now.)

## D3 — Sensed/reproducible split is inherited from F032, not re-modeled

**Decision.** `Provenance` is a **flat record of eight facts**. It introduces **no** provenance-level
`Sensed*` / `Reproducible*` wrapper. The only sensed metadata is the embedded command records' `Duration`
fields, which F032 already holds in a field structurally apart from each record's `Reproducible` facts. No
wall-clock build timestamp is carried.

**Rationale.** `canonicalId` folds each embedded record via F032's public `CommandRecord.canonicalId`, which by
construction reads only `record.Reproducible` and **never** `record.Duration` (F032 D2). So the durations are
*structurally* excluded from the provenance identity while remaining reachable as sensed metadata through the
carried records (`provenance.CommandRecords.[i].Duration`) — satisfying FR-005 and SC-003 with **zero** new
machinery. The design row lists eight facts and no timestamp, and the spec marks the timestamp explicitly
optional/not-required (Spec Assumptions); adding one now would invent a sensed fact the row does not ask for.

**Alternatives considered.**
- *Mirror F032's `{ Reproducible; <sensed> }` shape at the provenance level.* Rejected — there is no
  provenance-level sensed fact to separate; the sensed data lives one level down, already separated by F032.
  A wrapper would be empty ceremony.
- *Carry a wall-clock timestamp as a provenance-level `SensedTimestamp`.* Rejected — not required by the design
  row; out of scope (it would be sensed metadata if a later row adds it).

## D4 — Artifact digests are a SET; command records are an ORDERED sequence

**Decision.** Artifact digests: an `ArtifactHash list` compared as a **set** in the identity (deduped,
ordinal-sorted — the F029/F032 covered-artifacts discipline). Command records: a `CommandRecord list` carried
**whole and in order**, with an **order-significant** identity contribution (each contributes its F032
`canonicalId`, rendered in the given order — not sorted or deduped).

**Rationale.** FR-008 mandates order-independence **only** for set-like collections and names them concretely:
*"concretely the artifact digests."* The order of command runs, by contrast, is itself reproducible provenance
— a build that ran `build → test → pack` is not the same build as one that ran them in another order, and a
genuinely repeated run (e.g. a retried test) is a real fact, not a duplicate to collapse. Keeping command
records order-significant mirrors F032's own decision to keep *arguments* order-significant while treating each
*environment-delta class* as a set. The spec's Assumptions explicitly defer this choice to the plan and fix only
that each record is carried whole and contributes its **reproducible** identity — both honored here.

**Alternatives considered.**
- *Treat command records as a set (dedup/ordinal-sort by their canonical id).* Rejected — it would silently
  collapse a real repeated run and discard meaningful execution order; the spec only requires set treatment for
  the artifact digests.

## D5 — Canonical identity: the F029/F032 tagged, length-prefixed, injective string

**Decision.** `canonicalId` renders the reproducible facts to a byte-stable `ProvenanceIdentity` string in the
established encoding (full spec in `contracts/provenance-identity-format.md`): nine fixed `'\n'`-joined segments,
each a unique lowercase ASCII tag + a length-prefixed payload. The artifact-digest segment is a **set** (deduped,
ordinal-sorted); the command-records segment is an **ordered** list whose each entry is the **length-prefixed
full F032 canonical-id string**. The environment class renders via the same total four-token map F029 uses
internally (`local` / `ci` / `localOrCi` / `release`), replicated locally as a small total match. No hashing.

**Rationale.** This is the identical discipline already proven in F029 (`freshness-key-format.md`) and F032
(`command-record-identity-format.md`): length prefixes + unique tags make the encoding injective (no value can
masquerade as another field or bleed across a boundary), so any single reproducible difference changes the
identity and the same string in two fields yields different segments. Length-prefixing each embedded command id
is essential because an F032 canonical id **contains** `'\n'`, `:`, `;`, and `=`; the outer length prefix makes
those inner bytes harmless. F029's `environmentToken` is an **internal** helper (hidden by its `.fsi`), so it is
**convention-reused** (the same four tokens) rather than called — replicating a four-case total match is trivial
and keeps the dependency surface to types only. No digest is computed from bytes (FR-011): the canonical string
*is* the identity.

**Alternatives considered.**
- *Hash the canonical string into a fixed-width digest.* Rejected — FR-011 forbids computing a digest from
  bytes here; the inspectable canonical string is the identity (the F029/F032 precedent).
- *Make `environmentToken` public on F029 and call it.* Rejected — it would widen an existing core's surface
  (a change to a merged baseline, against SC-007 / FR-010); replicating the four-token match locally is cheaper
  and keeps F029 untouched.

## Cross-cutting facts (carried into Phase 1)

- **Totality.** `build` and `canonicalId` are defined for every well-typed input: empty command-records list,
  empty artifact-digest set, equal base/head, and embedded records that failed or timed out all yield ordinary
  complete values (FR-004, Edge cases). No exceptions, no filtering by command outcome.
- **Purity.** No clock / filesystem / git / environment / network read; no process spawn; no byte hashing
  (FR-009). Identical supplied facts ⇒ identical provenance and identical identity, independent of cwd, time,
  and unrelated repo/filesystem state (SC-006).
- **Determinism boundaries.** Reordering / duplicating the artifact digests never changes the identity (set);
  reordering the command records **does** (ordered) — these are the two order laws the tests pin (SC-005).
- **Additivity.** No merged core, `.fsi`, surface baseline, or existing test changes (FR-010, SC-007); the new
  library + test project are purely additive.
