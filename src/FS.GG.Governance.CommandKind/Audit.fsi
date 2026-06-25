// Curated public signature contract for the command-kind audit operations (F25).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// Audit.fs carries NO access modifiers. All operations are PURE, TOTAL, DETERMINISTIC: no I/O, no clock, no
// git, never throw, byte-identical for identical inputs. Identity is F032/F033 reused VERBATIM (D5) — no new
// fingerprint, and the descriptive kind never participates.

namespace FS.GG.Governance.CommandKind

open FS.GG.Governance.Config.Model
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.Provenance.Model
open FS.GG.Governance.CommandKind.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Audit =

    /// Stable wire token for a kind: build | test | pack | templateInstantiation | gitDiff |
    /// packageInspection | visualCapture. Exhaustive match, NO wildcard (a new kind is a compile error).
    val kindToken: kind: CommandKind -> string

    /// The reproducible identity of a kinded run — EXACTLY `CommandRecord.identityValue (CommandRecord
    /// .canonicalId run.Record)` (F032 reused verbatim; kind does NOT participate — research D5). Two runs
    /// differing ONLY in `SensedDuration` yield the byte-identical value (SC-005).
    val runIdentity: run: KindedCommandRun -> string

    /// Roll the sensed provenance inputs + the kinded runs into an `AuditSnapshot`. Builds the F033
    /// `Provenance` via `Provenance.build` from the inputs and the runs' `.Record`s (order-significant, as
    /// F033), and carries the `Runs` for the projection. PURE, TOTAL, byte-identical for identical inputs.
    val auditSnapshot:
        sourceCommit: Revision ->
        baseRevision: Revision ->
        headRevision: Revision ->
        ruleHash: RuleHash ->
        generatorVersion: GeneratorVersion ->
        artifactDigests: ArtifactHash list ->
        runs: KindedCommandRun list ->
        environment: EnvironmentClass ->
        builder: BuilderIdentity ->
            AuditSnapshot

    /// The snapshot's identity — EXACTLY `Provenance.canonicalId snapshot.Provenance` (F033 verbatim). Changes
    /// only when a REPRODUCIBLE input changes (commit, base/head, rule hash, generator version, an artifact
    /// digest, the environment, or a command run); duration never affects it (FR-009, SC-006).
    val snapshotIdentity: snapshot: AuditSnapshot -> string
