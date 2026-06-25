// Curated public signature contract for the provenance.json projection (F25).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// ProvenanceJson.fs carries NO access modifiers; every writer and token helper lives ONLY in the .fs.
// `ofSnapshot` is PURE and TOTAL: no file/process/clock/git/env access, never throws, byte-identical for
// identical inputs; it changes only when a reproducible input changes. Identity is F033/F032 reused VERBATIM.

namespace FS.GG.Governance.ProvenanceJson

open FS.GG.Governance.CommandKind.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ProvenanceJson =

    /// "fsgg.provenance/v1". Fixed; never derived from clock/env/input.
    val schemaVersion: string

    /// Project the audit snapshot to deterministic JSON. Identical input -> byte-identical text; differs only
    /// when a reproducible input (commit, base/head, rule hash, generator version, an artifact digest, the
    /// environment, the builder, or a command run) differs. Wall-clock duration is emitted ONLY as clearly
    /// sensed metadata (`durationNanos`) that never affects the document's `identity` field.
    val ofSnapshot: snapshot: AuditSnapshot -> string
