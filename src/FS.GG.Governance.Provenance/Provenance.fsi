// Curated public signature contract for the provenance operations (F033).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The
// matching Provenance.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings —
// visibility is presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any Provenance.fs body
// exists (Principle I). All three operations are PURE and TOTAL (FR-004, FR-009): defined for every
// well-typed input, never throwing, reading no clock, filesystem, git, environment, or network, spawning no
// process, and hashing no bytes; byte-for-byte identical for identical input regardless of evaluation time,
// machine, process, or working directory. This row performs NO sensing/timing/persistence/rendering/
// attestation and adds NO CLI: its sole outputs are the `Provenance` value and its `ProvenanceIdentity`.
//
// Naming note (F029/F032 precedent): the operations module `Provenance` and the `Model.Provenance` record
// type are distinct CLR entities (a module suffix vs a type); they share a name by intent, as F019's
// `Route`, F029's, and F032's patterns do.

namespace FS.GG.Governance.Provenance

open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.Provenance.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Provenance =

    /// Assemble the nine supplied facts (curried in the design row's field order: source commit, base, head,
    /// rule hash, generator version, artifact digests, command records, environment class, builder identity)
    /// into one complete `Provenance`. PURE and TOTAL: defined for every well-typed argument tuple, never
    /// throwing — `commandRecords = []`, `artifactDigests = []`, `baseRevision = headRevision`, and command
    /// records that failed or timed out all produce ordinary complete values (FR-004, Edge cases). Carriage
    /// is VERBATIM: each fact reads back unchanged (the artifact digests in the SAME order as supplied — no
    /// dedup here; the command records WHOLE and in order, each retaining all ten of its facts incl. its
    /// sensed `Duration`). No clock/filesystem/git; no normalization, sorting, or dedup (canonicalization is
    /// `canonicalId`'s job — L-B4).
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

    /// Render a provenance's REPRODUCIBLE facts to their canonical, deterministic, byte-stable
    /// `ProvenanceIdentity` (`contracts/provenance-identity-format.md`). PURE and TOTAL: defined for every
    /// `Provenance`; reads no clock/filesystem/git/environment/network, spawns no process, hashes no bytes.
    /// Computed ONLY over the reproducible facts — each command record contributes
    /// `CommandRecord.canonicalId record` (which never reads `record.Duration`, F032 D2), so two provenances
    /// differing only in command durations share an identity (FR-005/FR-006), while differing in ANY
    /// reproducible fact (a revision, the rule hash, the generator version, the artifact-digest SET, a
    /// command record's reproducible facts OR their order, the environment class, or the builder identity)
    /// yields a different identity (FR-006/FR-007). The artifact digests are compared as a SET (order/dup
    /// invariant — L-I3); the command records are ORDER-significant (L-I4). The same opaque string placed in
    /// two different fields yields different identities (INJECTIVE across fields — length prefixes + unique
    /// tags, L-I5). BCL string building only, no hashing (FR-011, L-I7).
    val canonicalId: provenance: Provenance -> ProvenanceIdentity

    /// Unwrap a `ProvenanceIdentity` to its canonical string (for storage, messages, tests). TOTAL.
    /// `identityValue (ProvenanceIdentity s) = s`.
    val identityValue: identity: ProvenanceIdentity -> string
