# Contract: VerifyCommand host seam modules (3 new public folds)

These are the NEW additively-public modules extracted from `Loop.fs`. Each gets a
curated `.fsi` (Principle II), compiled **before** `Loop.fs`. Signatures below are the
intended public surface — drafted `.fsi`-first and exercised through the existing
`VerifyCommand.Tests` golden/rollup/preview suites (Principle I). Exact types are
copied verbatim from the current `Loop.fs` bindings; the point is *placement*, not new
shapes.

> Convention: `Profile` = `FS.GG.Governance.Enforcement.Enforcement.Profile`;
> `SurfaceFinding` = `FS.GG.Governance.SurfaceChecks.Model.SurfaceFinding`;
> `CurrencyFinding` = `FS.GG.Governance.CurrencyEnforcement.CurrencyEnforcement.CurrencyFinding`;
> `ShipDecision` = `FS.GG.Governance.Ship.Model.ShipDecision`.

## `FS.GG.Governance.VerifyCommand.SurfaceFold`

```fsharp
/// True when the profile makes any surface finding effectively blocking (verbatim from Loop.surfaceBlocks).
val surfaceBlocks: profile: Profile -> findings: SurfaceFinding list -> bool

/// Fold the surface findings into the verify verdict via the existing deriveEffectiveSeverity —
/// no truth-table change (FR-007). Verbatim from Loop.foldSurfaceVerdict.
val foldSurfaceVerdict: profile: Profile -> findings: SurfaceFinding list -> decision: ShipDecision -> ShipDecision
```

## `FS.GG.Governance.VerifyCommand.ViewCurrencyFold`

```fsharp
val viewCurrencyBlocks: profile: Profile -> findings: CurrencyFinding list -> bool

val foldViewCurrencyVerdict: profile: Profile -> findings: CurrencyFinding list -> decision: ShipDecision -> ShipDecision

/// The first-class currency text detail rows (verbatim from Loop.viewCurrencyDetail):
/// each finding paired with its Verify-mode EnforcementDecision under the active profile.
/// EnforcementDecision = FS.GG.Governance.Enforcement.Enforcement.EnforcementDecision.
val viewCurrencyDetail:
    profile: Profile -> findings: CurrencyFinding list -> (CurrencyFinding * EnforcementDecision) list
```

## `FS.GG.Governance.VerifyCommand.ReleasePreview`

`previewFrom` lifts verbatim. `previewOf` currently takes the host `Model`; it is
**decomposed** so the module stays host-`Model`-free (Phase B `baseHeadOf` precedent) —
`Loop` keeps a one-line wrapper that projects `model.ReleaseDecl`/`model.ReleaseSensed`
into the call.

```fsharp
open FS.GG.Governance.ReleaseDeclaration
open FS.GG.Governance.ReleaseFactsSensing.Model   // SensedRelease
open FS.GG.Governance.CommandKind.Model            // AuditSnapshot
open FS.GG.Governance.ReleaseReport.Model          // VerifyReleasePreview

val previewFrom:
    decl: Declaration.ReleaseDeclaration -> sensed: SensedRelease -> snapshot: AuditSnapshot -> VerifyReleasePreview

/// Decomposed off Model: takes the gated declaration+sensed facts directly (None ⇒ None ⇒ no preview ⇒
/// byte-identical verify.json). Loop wraps: previewOf' model.ReleaseDecl model.ReleaseSensed snapshot.
val previewOf':
    decl: Declaration.ReleaseDeclaration option ->
    sensed: SensedRelease option ->
    snapshot: AuditSnapshot ->
        VerifyReleasePreview option
```

## Invariants

- `Loop.fsi` is **byte-identical** to baseline; `update` keeps owning `Model`/`Msg`/
  `Effect` and merely calls these folds (no `Msg`/`Effect`/`update` case moves out).
- Each `.fs` body carries no `private`/`internal`/`public` on top-level bindings.
- These three modules add exactly three `…Module` types to the re-blessed
  `surface/FS.GG.Governance.VerifyCommand.surface.txt`; no existing line changes.
- Every `VerifyCommand.Tests` golden/snapshot stays byte-identical (FR-005).
