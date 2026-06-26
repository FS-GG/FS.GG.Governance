# Contract — Release Outputs & Byte-Identity Anchors

**Scope**: the two release-host writes (`release.json` v2 + `attestation.json`) and the byte-identity invariants
that prove the wiring is additive.

## Writes (both from the immutable `ReleaseReport`)

```
ReleaseDoc     = ReleaseJson.ofReleaseReport report           // "fsgg.release/v2"
AttestationDoc = AttestationJson.ofAttestation report.Attestation  // "fsgg.attestation/v1"
```

Emitted as two `WriteArtifact` effects distinguished by `ArtifactKind` (`ReleaseArtifact | AttestationArtifact`),
both through the host's **existing** temp+rename `ArtifactWriter`. A write failure ⇒ `Wrote(kind, Error)` ⇒
`ToolError` (exit 4) — **never** a blocked verdict. `EmitSummary` fires only after both writes succeed.

## release.json v1 → v2 (additive)

`ofReleaseReport` renders the v1 fields verbatim from `report.Decision` + `report.Sensed` (schemaVersion, verdict,
exitCodeBasis, rules, evidence — the publish-plan/posture/template-pin precondition state + reason surface through
the existing `rules` array, **no new field**), then appends exactly three fields in fixed order: `packageEvidence`,
`versionPolicy`, `attestation` (a self-contained identity reference; the full summary lives in the sidecar).

## Byte-identity anchors (SC-005)

| Document | Invariant | How proven |
|---|---|---|
| `route.json`, `ship.json` | byte-identical | untouched hosts; compared to frozen baselines |
| `release.json` v1→v2 | the **one** existing release golden re-blessed v1→v2 (schemaVersion bump + the three empty additive fields) | the F26-blessed v2 golden; an empty-additive run is byte-identical to it |
| `attestation.json` | new sidecar; deterministic | re-run byte-identity fixture |
| `verify.json` (no declaration) | byte-identical, no schema bump | covered in `verify-preview.md` |

## Guarantees

- **GR-1** The `ReleaseReport` is the single source of truth; both projections render from it (FR-012).
- **GR-2** No existing write path changes; the two writes are additive through the existing port (FR-004, FR-010).
- **GR-3** `release.json` v2 + `attestation.json` byte-identical on re-run with unchanged inputs (FR-007, SC-003).
- **GR-4** The release verdict + `ExitCodeBasis` are the F53 `ReleaseDecision` verbatim, blocking and distinct
  from the ship merge verdict (FR-005); a mergeable product can be not releasable.
