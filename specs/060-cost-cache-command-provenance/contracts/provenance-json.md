# Contract: `provenance.json` projection (`FS.GG.Governance.ProvenanceJson`)

Deterministic JSON sidecar: the provenance audit snapshot that proves what ran, against what inputs, in what
environment. Written by the existing `fsgg verify` / `fsgg ship` hosts. PURE, TOTAL: no file/process/clock/git/
env access. Byte-identical for identical inputs; changes only when a reproducible input changes. FR-009,
FR-011, SC-006.

## `ProvenanceJson.fsi`

```fsharp
namespace FS.GG.Governance.ProvenanceJson

open FS.GG.Governance.CommandKind.Model      // AuditSnapshot

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ProvenanceJson =

    /// "fsgg.provenance/v1". Fixed.
    val schemaVersion: string

    /// Project the audit snapshot to deterministic JSON. Identical input -> byte-identical text; differs only
    /// when a reproducible input (commit, base/head, rule hash, generator version, an artifact digest, the
    /// environment, the builder, or a command run) differs. Wall-clock duration is emitted ONLY as clearly
    /// sensed metadata that never affects the document's `identity` field.
    val ofSnapshot: snapshot: AuditSnapshot -> string
```

## Document shape

```jsonc
{
  "schemaVersion": "fsgg.provenance/v1",
  "identity": "<Provenance.canonicalId>",         // F033 identity verbatim — the reproducibility fingerprint
  "sourceCommit": "<Revision>",
  "base": "<Revision>",
  "head": "<Revision>",
  "ruleHash": "<RuleHash>",
  "generatorVersion": "<GeneratorVersion>",
  "environment": "<EnvironmentClass>",
  "builder": "<BuilderIdentity>",
  "artifactDigests": ["<ArtifactHash>", …],        // SET in identity (order/dup-invariant), rendered sorted
  "commandRuns": [                                  // order as carried (order-significant in F033 identity)
    {
      "kind": "build|test|pack|templateInstantiation|gitDiff|packageInspection|visualCapture",
      "identity": "<CommandRecord.canonicalId>",    // F032 identity; duration-invariant
      "exitCode": 0,
      "durationNanos": 123456                        // SENSED metadata only — NOT part of any identity
    }
  ]
}
```

## Rules

- **Identity is F033, reused verbatim.** The top-level `identity` is `Provenance.canonicalId snapshot.Provenance`
  and each run's `identity` is `CommandRecord.canonicalId run.Record`. The projection computes no new
  fingerprint.
- **Reproducible vs sensed is visible.** Everything under the identity-bearing fields is reproducible; the only
  sensed field is each run's `durationNanos`, which is rendered for human/audit value but is explicitly the
  F032 `SensedDuration` that never affects `identity` (SC-006). Two snapshots differing only in durations have
  the same top-level `identity` and per-run `identity`.
- **Fixed field order**, raw-text-verified: `schemaVersion` < `identity` < `sourceCommit` < … <
  `artifactDigests` < `commandRuns`; within a run `kind` < `identity` < `exitCode` < `durationNanos`.
- **Closed-enum kind token** (exhaustive, no wildcard). **No-hide**: every command run is rendered; an empty
  run list yields `"commandRuns": []` (well-formed). A failed/timed-out run is rendered with its F051 sentinel
  `exitCode` (never dropped — spec edge).
- **No clock/host-path/username leakage.** No wall-clock timestamp, absolute path, or environment value appears
  beyond the declared opaque `EnvironmentClass`/`BuilderIdentity` tokens carried in (FR-011).
