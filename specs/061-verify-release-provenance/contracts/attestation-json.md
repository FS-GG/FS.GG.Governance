# Contract: `attestation.json` — `fsgg.attestation/v1`

A new deterministic sidecar projecting the `AttestationSummary` (the F25 `ProvenanceJson`/`provenance.json`
precedent). Written by the `fsgg release` host through the existing temp+rename `ArtifactWriter`. `ofAttestation`
is PURE and TOTAL: no file/process/clock/git/env access, never throws, **byte-identical** for identical inputs;
it changes only when a reproducible input changes. Identity is `Provenance.canonicalId` reused verbatim.

## `AttestationJson.fsi` (draft)

```fsharp
namespace FS.GG.Governance.AttestationJson

open FS.GG.Governance.Attestation.Model         // AttestationSummary

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AttestationJson =

    /// "fsgg.attestation/v1". Fixed; never derived from clock/env/input.
    val schemaVersion: string

    /// Project the attestation summary to deterministic JSON. Identical input -> byte-identical text; differs
    /// only when a reproducible input changes. Wall-clock duration is emitted ONLY as clearly-sensed metadata
    /// (durationNanos inside each invocation run) that never affects the document's `identity` field.
    val ofAttestation: summary: AttestationSummary -> string
```

## Wire shape (fixed field order)

```json
{
  "schemaVersion": "fsgg.attestation/v1",
  "compliance": "compatible-shape-not-formal-compliance",
  "complianceNote": "SLSA/in-toto-shaped, reproducible metadata; NOT a claim of formal SLSA-level or in-toto attestation conformance.",
  "identity": "<Provenance.canonicalId — byte-stable over reproducible facts>",
  "builder": "<BuilderIdentity opaque token>",
  "subjects": [
    { "name": "<normalized artifact path>", "version": "<packed version>", "digest": "<ArtifactHash>" }
  ],
  "materials": {
    "ruleHash": "<RuleHash>",
    "generatorVersion": "<GeneratorVersion>",
    "sourceCommit": "<Revision>",
    "base": "<Revision>",
    "head": "<Revision>",
    "artifactDigests": ["<ArtifactHash>", "…"],
    "environment": "<EnvironmentClass token>"
  },
  "invocation": {
    "runs": [
      { "kind": "pack", "identity": "<CommandRecord.canonicalId>", "exitCode": 0, "durationNanos": 123456 }
    ]
  }
}
```

## Determinism / no-overclaim rules (tested)

- Fixed field order; `schemaVersion`/`compliance`/`complianceNote` constants never derived from clock/env/input.
- `subjects` sorted by `name`; `artifactDigests` order-normalized (set semantics, D7); `invocation.runs` in the
  snapshot's order (order-significant, D7).
- `identity` = `Provenance.canonicalId`; `durationNanos` is sensed metadata only — changing only a duration
  yields a different `durationNanos` but a **byte-identical** `identity` (SC-005).
- A failed-build snapshot ⇒ `subjects: []` (no attested subject, FR-008); the failed run still appears under
  `invocation.runs` with its sentinel `exitCode`.
- Reordering the input runs/subjects ⇒ byte-identical document (SC-005, determinism-under-reordering).
- An empty/clean summary projects to a valid document (byte-identical-when-empty).
