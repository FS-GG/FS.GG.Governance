# Contract — Provenance Snapshot & Attestation Sidecar (`fsgg release`)

**Scope**: how the release host builds the F25 `AuditSnapshot` from the pack runs + normalized provenance senses,
projects it into the `AttestationSummary`, and writes `attestation.json` (`fsgg.attestation/v1`).

## Provenance inputs (research D2)

`Audit.auditSnapshot` is fed:

| Input | Source | Determinism note |
|---|---|---|
| `runs` | the `Pack` `KindedCommandRun`s (pack-boundary contract) | order-significant, as F033 |
| `artifactDigests` | the real `PackArtifact.Digest`s of the `Packed` outcomes | a real build artifact hash |
| `headRevision` / `baseRevision` / `sourceCommit` | one head revision via the F016 `Snapshot` port; `base = head = sourceCommit` | release attests a product state, not a diff range |
| `ruleHash` | derived from `decl.Rules` | deterministic over the declared rules |
| `generatorVersion` | normalized `fsgg` constant | no clock/build-number leakage |
| `environment` | `CI`/`Local` sense | normalized (064 precedent) |
| `builder` | `BuilderIdentity "fsgg"` | no username/host/clock (064 precedent) |

## Projection

```
snapshot    = Audit.auditSnapshot sourceCommit base head ruleHash genVer artifactDigests packRuns env builder
attestation = Attestation.summarize snapshot pack          // subjects ONLY from Packed outcomes
doc         = AttestationJson.ofAttestation attestation     // "fsgg.attestation/v1"
```

Written through the **existing** atomic `ArtifactWriter` under `ArtifactKind.AttestationArtifact`, default path
`<repo>/readiness/attestation.json` (overridable via `--attestation-out`).

## Guarantees

- **GA-1** Subjects come **only** from `Packed` outcomes — a failed/no-artifact pack yields **no** attested
  subject (FR-008).
- **GA-2** The summary always carries `Compliance = CompatibleShapeNotFormalCompliance` — never overclaims formal
  SLSA/in-toto compliance (FR-008).
- **GA-3** `attestation.json` is byte-identical for identical inputs and changes only when a reproducible input
  changes; pack duration is carried only as sensed `durationNanos` inside each invocation run and never affects
  the document `identity` (FR-007, SC-003).
- **GA-4** Order-independent: reordering the packable projects / command runs changes no attestation bytes beyond
  the F33 order-significant `Runs` (which preserve real execution order) (FR-011).
- **GA-5** An absent provenance input (no recorded pack, unreadable head) surfaces a clear input signal and blocks
  release — never a hollow attestation (FR-014).
