// Curated public signature contract for the attestation.json projection (F26, P2).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// AttestationJson.fs carries NO access modifiers; every writer and token helper lives ONLY in the .fs.
// `ofAttestation` is PURE and TOTAL: no file/process/clock/git/env access, never throws, byte-identical for
// identical inputs; it changes only when a reproducible input changes. Identity is F033/F032 reused VERBATIM.

namespace FS.GG.Governance.AttestationJson

open FS.GG.Governance.Attestation.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AttestationJson =

    /// "fsgg.attestation/v1". Fixed; never derived from clock/env/input.
    val schemaVersion: string

    /// Project the attestation summary to deterministic JSON. Identical input -> byte-identical text; differs
    /// only when a reproducible input changes. Wall-clock duration is emitted ONLY as clearly-sensed metadata
    /// (durationNanos inside each invocation run) that never affects the document's `identity` field.
    val ofAttestation: summary: AttestationSummary -> string
