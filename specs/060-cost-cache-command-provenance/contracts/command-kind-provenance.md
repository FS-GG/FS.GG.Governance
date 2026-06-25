# Contract: Command-Run Kind Taxonomy + Provenance Audit Snapshot (`FS.GG.Governance.CommandKind`)

Pure. Wraps the F032 `CommandRecord` with a descriptive *kind* (does **not** change its identity) and rolls the
recorded runs together with the F033 provenance inputs into a deterministic audit snapshot. FR-008, FR-009,
FR-011, SC-005, SC-006.

## `Model.fsi`

```fsharp
namespace FS.GG.Governance.CommandKind

open FS.GG.Governance.CommandRecord.Model   // CommandRecord, canonicalId, identityValue, SensedDuration
open FS.GG.Governance.Provenance.Model        // Provenance, Revision, RuleHash, GeneratorVersion,
                                              //   ArtifactHash, EnvironmentClass, BuilderIdentity

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// The closed taxonomy of expensive command kinds a governed run performs (FR-008). Exactly seven; the
    /// kind is DESCRIPTIVE metadata sensed at the host edge — it does NOT participate in the F032 identity.
    type CommandKind =
        | Build
        | Test
        | Pack
        | TemplateInstantiation
        | GitDiff
        | PackageInspection
        | VisualCapture

    /// An F032 `CommandRecord` wrapped (NOT extended) with its kind. Identity is the record's identity.
    type KindedCommandRun =
        { Kind: CommandKind
          Record: CommandRecord }

    /// The provenance audit snapshot: the F033 `Provenance` roll-up plus the kind labels for projection.
    type AuditSnapshot =
        { Provenance: Provenance
          Runs: KindedCommandRun list }
```

## `Audit.fsi`

```fsharp
namespace FS.GG.Governance.CommandKind

open FS.GG.Governance.CommandRecord.Model
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
```

## Rules

- **Identity is F032/F033, reused verbatim (research D5).** No new identity formula. `CommandRecord` and
  `Provenance` are unchanged; the kind is carried beside them for the taxonomy and the projection only.
- **Duration-invariant.** Two runs of the same command differing only in wall-clock `SensedDuration` share a
  `runIdentity`, and two snapshots differing only in their runs' durations share a `snapshotIdentity` (SC-005,
  SC-006) — inherited from F032/F033.
- **Every kind is recordable.** A run of each of the seven kinds is captured with its reproducible identity
  (SC-005). The host edge tags each `ExecuteGates` run with the kind known at the call site.
- **Failed/timed-out runs are kept.** A run whose process failed to start or timed out is recorded with its
  F051 sentinel exit code (never dropped), so the snapshot still proves the attempt (spec edge).
- **No-op input change is stable.** Re-deriving the snapshot from the same inputs yields a byte-identical
  identity; changing a non-reproducible input (a duration) does not change it (SC-006 stability check).
