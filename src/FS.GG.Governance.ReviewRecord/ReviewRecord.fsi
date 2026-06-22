// Curated public signature contract for the operations of the auditable review-record core (F038).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II); the matching
// ReviewRecord.fs carries NO access modifiers, and the length-prefix / segment helpers stay unexposed by
// their absence here. All three operations are pure, total, and deterministic (FR-002, FR-005): defined for
// every well-typed input, never throwing; reading no clock/filesystem/git/environment/network, invoking no
// model/agent, hashing no bytes (digests are supplied tokens), spawning no process, persisting nothing;
// byte-for-byte identical for identical input regardless of evaluation time, machine, process, or working
// directory. The canonical injective identity is fixed by contracts/review-record-identity-format.md.
//
// NAMING NOTE (the F029/F032/F033 precedent): the operations module `ReviewRecord` and the
// `Model.ReviewRecord` record type are DISTINCT CLR entities (a module suffix vs a type) sharing a name by
// intent.

namespace FS.GG.Governance.ReviewRecord

open FS.GG.Governance.PromptIsolation.Model
open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.SensedMetadata.Model
open FS.GG.Governance.ReviewRecord.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ReviewRecord =

    /// Assemble the supplied facts (curried in the design row's audit-fact order, sensed last — the F032
    /// `build` convention, research D7) into one complete `ReviewRecord`. TOTAL (L-B1): defined for every
    /// well-typed argument tuple and never throws — `reviewedArtifacts = []`, `ResponseDigest ""`,
    /// `RecordedVerdict ""`, and `sensed = []` all produce ordinary complete records. VERBATIM CARRIAGE (L-B2,
    /// SC-001): every fact reads back unchanged; the artifact list keeps its supplied order and duplicates,
    /// the sensed list is kept whole and in order; no fact is dropped, altered, or invented. SENSED HELD APART
    /// (L-B3, research D6): `sensed` is placed in `record.Sensed` and NOWHERE in `record.Reproducible`. NO
    /// CANONICALIZATION (L-B4): performs no reorder, dedup, normalization, capture, hashing, or I/O —
    /// canonicalization is `canonicalId`'s job. DETERMINISM (L-B5, SC-005): a pure function of its arguments.
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

    /// Render `record.Reproducible` to the canonical `RecordIdentity`
    /// (contracts/review-record-identity-format.md). PURE (L-I1, FR-005): reads no
    /// clock/filesystem/git/environment/network, invokes no model, hashes no bytes; BCL string building only
    /// (plus `PromptIsolation.render` for the request segment, itself pure). REPRODUCIBLE-ONLY (L-I2,
    /// FR-004/SC-003): computed ONLY over `record.Reproducible`; `record.Sensed` is NEVER read, so records
    /// differing only in `Sensed` share a byte-identical identity. DETERMINISTIC & BYTE-STABLE (L-I3,
    /// SC-002/SC-005). INJECTIVE over reproducible facts (L-I4, FR-003/SC-002): any single differing
    /// reproducible fact — the request (any change that alters `PromptIsolation.render request`), the model
    /// id/version, the prompt hash, the response digest, the verdict, or the artifact-digest SET (L-I5,
    /// research D4: reorder/duplicate does NOT change identity; add/remove a distinct digest does) — yields a
    /// different identity. INJECTIVE ACROSS FIELDS (L-I6): the same opaque string in two different fields
    /// yields different identities; field content with tag/separator/fence characters is read as data by
    /// length and forges no boundary. NO HASHING (L-I8): the canonical string IS the identity.
    val canonicalId: record: ReviewRecord -> RecordIdentity

    /// Unwrap a `RecordIdentity` to its canonical string (for citation, comparison, messages, tests). TOTAL
    /// (L-V1): `identityValue (RecordIdentity s) = s`.
    val identityValue: identity: RecordIdentity -> string
