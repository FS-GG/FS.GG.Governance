# Contract: `release.json` — `fsgg.release/v2` (additive migration from v1)

`release.json` bumps `schemaVersion` from `fsgg.release/v1` to `fsgg.release/v2`, **adding** three fields and
changing **nothing** existing (research D2, FR-015). The v1 fields (`schemaVersion`, `verdict`, `exitCodeBasis`,
`rules`, `evidence`) keep their exact shape and order; the new fields are appended after `evidence`. A v1
consumer ignoring unknown fields still reads a v2 document; the `schemaVersion` token lets a consumer branch.

The projection moves to render from the `ReleaseReport` single source of truth (FR-012): `ReleaseJson` gains
`ofReleaseReport : ReleaseReport -> string`. The existing `ofRelease : ReleaseDecision -> SensedRelease ->
string` is retained for callers that have only a decision+snapshot (it emits a v2 document with empty
`packageEvidence`/`versionPolicy` and a null `attestation` — byte-identical-when-empty), so existing call sites
compile unchanged; the host edge calls `ofReleaseReport`.

## Added `val` (additive surface)

```fsharp
    /// "fsgg.release/v2" (was "fsgg.release/v1"). Additive bump — every v1 field unchanged.
    val schemaVersion: string

    /// Project the F26 ReleaseReport (single source of truth, FR-012) into fsgg.release/v2. Renders the v1
    /// fields VERBATIM from report.Decision + the sensed snapshot, then appends packageEvidence, versionPolicy,
    /// and attestation. PURE, TOTAL, byte-identical for identical input.
    val ofReleaseReport: report: ReleaseReport -> string
```

## Wire shape (v1 fields unchanged; new fields appended)

```json
{
  "schemaVersion": "fsgg.release/v2",
  "verdict": "fail",
  "exitCodeBasis": "blocked",
  "rules": [ /* unchanged from v1 — one entry per declared family */ ],
  "evidence": { /* unchanged from v1 — the F54 ReleaseSnapshot */ },

  "packageEvidence": {
    "noPackableProjects": false,
    "projects": [
      {
        "surface": "<SurfaceId>",
        "outcome": "packed",                       // packed | packedNoArtifact | packFailed
        "artifactPath": "<normalized path>",       // null when no artifact
        "packedVersion": "1.3.0",                  // null when no artifact
        "digest": "<ArtifactHash>",                // null when no artifact
        "sentinel": null,                          // the pack exit sentinel when packFailed
        "reason": "<product-neutral reason>"
      }
    ]
  },
  "versionPolicy": {
    "projects": [
      { "surface": "<SurfaceId>", "verdict": "bumped", "baseline": "1.2.0", "packed": "1.3.0" }
      //  verdict: bumped | unbumped | downgraded | noBaseline | notPackable
    ]
  },
  "attestation": {
    "schemaVersion": "fsgg.attestation/v1",
    "identity": "<Provenance.canonicalId>",
    "compliance": "compatible-shape-not-formal-compliance",
    "subjectCount": 1
    // a self-contained reference; the full summary lives in the attestation.json sidecar
  }
}
```

## Migration & determinism rules (tested)

- Every existing `release.golden.json` (F55) is regenerated to v2 with the three new fields; the v1 fields are
  byte-identical within the document. No `route.json`/`ship.json` golden changes (FR-015).
- The publish-plan / trusted-publishing-posture / template-pin precondition state + reason (FR-006) surface
  through the **existing v1 `rules` array** — each `ReleaseRuleKind` already carries its `factState` + `reason`
  there. There is **no** separate `preconditions` field; the `ReleaseReport.Preconditions` list is the in-memory
  single source of truth that `rules` is rendered from, not a distinct wire field. Exactly **three** fields are
  added in v2: `packageEvidence`, `versionPolicy`, `attestation`.
- The retained `ofRelease: ReleaseDecision -> SensedRelease -> string` emits a v2 document with empty
  `packageEvidence`/`versionPolicy` and null `attestation`, byte-identical to `ofReleaseReport` over the
  equivalent empty report (existing call sites keep compiling).
- `packageEvidence.projects` and `versionPolicy.projects` sorted by `surface` then `artifactPath` (D7).
- `attestation` is `null` when there is no attestation input (no fabricated attestation); the full summary is in
  the sidecar (`attestation-json.md`).
- Byte-identical for identical repository state; reordering inputs never changes the document (SC-007).
