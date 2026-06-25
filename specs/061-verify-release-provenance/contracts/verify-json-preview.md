# Contract: `verify.json` — additive `releaseReadiness` preview block

`fsgg verify` surfaces an **advisory** release-readiness preview of the publication verdict using the same
evidence (FR-005). `verify.json` gains one additive `releaseReadiness` object; the existing F56 fields and the
F56 five-exit-code scheme are unchanged. The preview is **never** the blocking gate — verify's `Exit` is
decided solely by the existing `Ship.rollup`/`applyExecution` at `RunMode.Verify` (the preview does not perturb
it).

## Surface

`VerifyJson` gains a projection that carries the optional `VerifyReleasePreview` alongside the existing verify
decision (e.g. `ofVerifyDecisionWithPreview : ... -> VerifyReleasePreview option -> string`, or an added
optional parameter). When the preview is absent (`None` — no release declaration), the block is omitted and the
document is byte-identical to the pre-F26 verify golden for that run (byte-identical-when-empty, FR-015).

The `schemaVersion` is **not** bumped — it stays `fsgg.verify/v1`. The `releaseReadiness` block is **optional**
and appended only when a release declaration is present; when it is absent the block is omitted and the document
is byte-identical to the pre-F26 verify golden (the F24 `ofVerifyDecisionWithSurfaceChecks` precedent — an
optional additive field added byte-identically, with no schema bump). Every existing field keeps its shape and
order.

## Wire shape (existing fields unchanged; new block appended)

```json
{
  "schemaVersion": "fsgg.verify/v1",
  "...": "all existing F56 verify fields, unchanged and in order",

  "releaseReadiness": {
    "advisory": true,
    "verdict": "fail",
    "packageEvidence": { "...": "same shape as release.json v2 packageEvidence" },
    "versionPolicy":   { "...": "same shape as release.json v2 versionPolicy" },
    "attestation":     { "...": "same self-contained reference as release.json v2 attestation, or null" }
  }
}
```

## Rules (tested)

- `releaseReadiness.advisory` is always `true`; verify's exit code is decided without it (SC-003 / Story 2.3).
- The preview uses the **same** evidence the release boundary would (`ReleaseReport.preview`), so verify and
  release never diverge in what they report (FR-005, FR-012).
- Absent release declaration ⇒ the block is omitted; the verify golden is byte-identical to the run's pre-F26
  output (FR-015).
- Present ⇒ byte-identical for identical repository state; reordering inputs never changes it (SC-007).
