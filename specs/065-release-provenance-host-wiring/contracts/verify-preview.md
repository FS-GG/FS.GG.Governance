# Contract — `fsgg verify` Release-Readiness Preview (advisory)

**Scope**: how `fsgg verify` surfaces an advisory `releaseReadiness` preview without packing and without changing
its exit code. Declaration-gated; byte-identical when no declaration is present.

## Gating

The verify interpreter attempts to read `.fsgg/release.yml` via its existing `Files` port. The preview is built
**only** when the declaration is present **and** parses through the shared `ReleaseDeclaration` leaf. Absent or
unparsable ⇒ `ReleasePreview = None` ⇒ `verify.json` byte-identical to its pre-wiring golden (no block, no schema
bump, exit unchanged — D4, FR-012).

## Pure composition (declaration present)

```
decision    = Release.evaluateRelease decl.Rules sensed.Facts          // sensed via F54 (no pack)
emptyPack    = { Verdicts=[]; Runs=[]; NoPackableProjects=true }        // verify does NOT pack (D4)
attestation = Attestation.summarize model.Audit emptyPack              // materials from verify's EXISTING Audit
report      = Report.assemble decision sensed emptyPack attestation
preview     = Report.preview report                                     // Advisory = true
matrix      = Matrix.decideMatrix (budgetFor profile Verify) InnerLoop decl.Matrix   // Deferred in inner loop
doc         = VerifyJson.ofVerifyDecisionWithPreview shipDecision cache execution findings (Some preview)
```

`emptyPack` ⇒ `Attestation.summarize` yields **zero** subjects (no fabricated subject — FR-008). The preview is the
`verify.json` document's last optional field.

## Guarantees

- **GV-1** The preview is **advisory** (`Advisory = true`) and never participates in verify's `Exit` — verify's
  five-code F56 scheme is unchanged (FR-006).
- **GV-2** Verify does **not** pack (the broad pack-across-every-project is the deferred matrix work); the preview
  surfaces the cheap sensed preconditions + declared-version intent only (D4).
- **GV-3** A declared matrix is recorded `Deferred` at `InnerLoop`; an undeclared matrix is `NotDeclared` —
  neither invoked (FR-009).
- **GV-4** No declaration ⇒ `verify.json` byte-identical, no schema bump (`fsgg.verify/v1` unchanged) (FR-012,
  SC-005).
- **GV-5** With a declaration, `releaseReadiness` carries `advisory: true`, the previewed verdict, and the same
  `packageEvidence`/`versionPolicy`/`attestation` shape as `release.json` v2 — rendered from the same
  `ReleaseReport` object (FR-012).
