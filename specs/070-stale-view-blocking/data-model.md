# Phase 1 — Data Model: Block Stale Generated Views at the Configured Governance Boundary

All types below are either **new** in the pure leaf `FS.GG.Governance.CurrencyEnforcement` or **reused
verbatim** from existing cores. No existing type is reopened except the one **additive** field on
`GenerationManifest` (D3).

## New — `FS.GG.Governance.CurrencyEnforcement` (pure leaf)

The analogue of `SurfaceChecks.Model`: the finding vocabulary plus the bridge into the F023 truth table. Every
binding is pure, total, deterministic — no clock, env, host path, git, or ordering influence.

### `StaleCause`

Why a view is a finding, derived from `CurrencyStatus`. Closed; product-neutral.

```fsharp
/// Why a generated view is stale, for the self-describing finding (FR-005).
type StaleCause =
    /// Source-digest mismatch / older-than-sources: the drifted input categories that drove staleness,
    /// carried verbatim from the WouldRegenerate/Regenerated CurrencyStatus (reuses F029 InputCategory).
    | SourceDrift of drifted: InputCategory list
    /// Currency could not be determined (no declared sources, missing manifest entry, unreadable lock).
    /// Carries the StaleUnresolved reason verbatim; NEVER coerced to Current (FR-008).
    | Undeterminable of reason: string
```

`Current` and `NotEvaluated` map to **no** `StaleCause` (they produce no finding).

### `CurrencyFinding`

One stale (or undeterminable) generated view, ready to fold. Mirrors `SurfaceFinding`'s carried-severity shape.

```fsharp
/// One stale-generated-view finding. ViewId/Kind name the stale view (FR-005); Cause names what makes it
/// stale. BaseSeverity = Blocking (so the configured maturity can block — D5); Maturity is the configured
/// dial. The pair (BaseSeverity, Maturity) feeds the F023 truth table verbatim — no new enforcement constant.
type CurrencyFinding =
    { ViewId: string
      Kind: ViewKind
      Cause: StaleCause
      BaseSeverity: Severity   // F023 Severity, always Blocking here (D5)
      Maturity: Maturity }     // F014 Maturity, the configured dial
```

### Functions

```fsharp
/// Decide one view's currency by reusing F029 FreshnessKey.matches/diff VERBATIM (recorded vs current
/// FreshnessInputs differing only in source-digest set + generator version, revisions held equal — F057
/// research D1). Produces the existing RefreshModel.CurrencyStatus/ViewDecision. PURE — the edge supplies the
/// recorded provenance and the freshly-sensed digests/version as data; this never reads the filesystem.
/// A sense failure for a view yields StaleUnresolved (never Current — FR-008).
val decideCurrency:
    entry: GenerationEntry ->
    recorded: (ArtifactHash list * GeneratorVersion) option ->
    sensed: Result<ArtifactHash list * GeneratorVersion, string> ->
    ViewDecision

/// The OPT-IN gate (D5). None ⇒ [] (unconfigured ⇒ byte-identity). Some m ⇒ one finding per stale/unresolved
/// view (Current/NotEvaluated ⇒ none), BaseSeverity = Blocking, Maturity = m. Deterministic order (declared
/// manifest order). TOTAL.
val findingsOf: maturity: Maturity option -> views: ViewDecision list -> CurrencyFinding list

/// Build the F023 rollup input for a finding under a run mode + profile (reuses F023 verbatim — NO truth-table
/// logic here). Mirrors SurfaceChecks.enforcementInputOf exactly.
val enforcementInputOf: finding: CurrencyFinding -> mode: RunMode -> profile: Profile -> EnforcementInput

/// The per-finding enforcement decision = deriveEffectiveSeverity (enforcementInputOf finding mode profile).
/// Carries both base and effective severity + reason for the no-hide projection (FR-006). TOTAL, deterministic.
val decisionOf: finding: CurrencyFinding -> mode: RunMode -> profile: Profile -> EnforcementDecision

/// Stable, kebab-case wire token for a StaleCause discriminant (`source-drift` | `undeterminable`), for the
/// generatedViews `cause` field. TOTAL, exhaustive (no wildcard).
val staleCauseToken: cause: StaleCause -> string
```

`ViewKind` rendering reuses `RefreshModel.viewKindToken` verbatim. **Severity rendering happens in the
projection projects** (`VerifyJson` / `AuditJson`), which already define their own exhaustive
`advisory`/`blocking` `severityToken` — the leaf does **not** reference `SurfaceChecks` (see the leaf's
ProjectReferences) and does not render severity tokens itself. No new token table.

## Reused verbatim (no reopen)

| Type / function | Source | Role in F070 |
|---|---|---|
| `Severity` (`Advisory`/`Blocking`), `RunMode`, `Profile`, `EnforcementInput`, `EnforcementDecision`, `deriveEffectiveSeverity` | `FS.GG.Governance.Enforcement` | The truth table folded through verbatim (FR-003). |
| `Maturity` (`Observe`…`BlockOnRelease`) | `FS.GG.Governance.Config.Model` | The maturity dial (FR-001). |
| `ViewKind`, `GenerationEntry`, `GenerationManifest`, `CurrencyStatus`, `ViewDecision`, `viewKindToken` | `FS.GG.Governance.RefreshJson.RefreshModel` | The existing currency representation consumed (FR-007). |
| `FreshnessInputs`, `ArtifactHash`, `GeneratorVersion`, `matches`, `diff`, `InputCategory` | `FS.GG.Governance.FreshnessKey` | The comparator re-expressed verbatim by `decideCurrency` (FR-007, D4). |
| `Declaration.parse` | `FS.GG.Governance.RefreshCommand` | Parses the manifest (incl. the new dial) at the host edge. |
| `ShipDecision`, `Verdict`, `ExitCodeBasis` | `FS.GG.Governance.Ship.Model` | The verdict `foldViewCurrencyVerdict` adjusts (FR-002, FR-010). |

## Additive edit — `GenerationManifest` (D3)

```fsharp
type GenerationManifest =
    { Entries: GenerationEntry list
      CurrencyEnforcement: Maturity option }   // NEW additive field, default None (opt-in / advisory)
```

`RefreshModel.fs` opens `Config.Model` for `Maturity`; `Declaration.parse` reads a manifest-level
`currency-enforcement: <observe|warn|block-on-pr|block-on-ship|block-on-release>` key (absent ⇒ `None`). The
`refresh.json` projection does **not** read this field, so `refresh.json` stays byte-identical. Existing fields
are neither removed nor reordered (FR-010).

## Host wiring (additive) — `VerifyCommand` / `ShipCommand`

Mirrors F067's `SenseSurfaces`/`SurfacesSensed`/`SurfaceFindings`/`foldSurfaceVerdict` triple. **Naming note:**
both hosts already own a `CurrencyNotes` / "currency section" vocabulary for *evidence-reuse* freshness
(F046/F048); the generated-view wiring below uses the distinct `SenseViewCurrency` / `ViewCurrencySensed` /
`ViewCurrencyFindings` / `foldViewCurrencyVerdict` names so the two currency concepts never conflate.

- **Effect** `SenseViewCurrency of repo: string` — the edge: `Declaration.parse` the manifest, read each view's
  recorded provenance lock, sense source digests + generator version, run `decideCurrency` per view.
- **Msg** `ViewCurrencySensed of findings: CurrencyFinding list` — fed back after the edge runs (the edge applies
  `findingsOf manifest.CurrencyEnforcement viewDecisions`).
- **Model field** `ViewCurrencyFindings: CurrencyFinding list` (initial `[]`).
- **`foldViewCurrencyVerdict`** (host-local, the analogue of `foldSurfaceVerdict`):

```fsharp
let foldViewCurrencyVerdict (mode: RunMode) (profile: Profile)
                        (findings: CurrencyFinding list) (decision: ShipDecision) : ShipDecision =
    // Any finding whose EFFECTIVE severity is Blocking fails the run; otherwise identity.
    let blocks =
        findings |> List.exists (fun f -> (decisionOf f mode profile).EffectiveSeverity = Blocking)
    if blocks then { decision with Verdict = Fail; ExitCodeBasis = Blocked } else decision
```

The empty-findings list (unconfigured) makes this **identity** ⇒ byte-identity (FR-004). The host then threads
`model.ViewCurrencyFindings` (with each finding's `decisionOf …`) into the additive projection overload (D7). The
verify host fixes `mode = Verify`; the ship host uses `Gate` — so the *same* finding configured `block-on-ship`
warns under verify and blocks under ship (FR-009), purely from the truth table.

## Determinism & no-hide invariants

- `findingsOf` emits findings in **declared manifest order** (deterministic); the projection sorts
  `generatedViews` by a stable key (`viewId`) so the array is byte-stable.
- `decideCurrency` is pure over its supplied data; all I/O (manifest/lock read, digesting, version sense) is in
  the edge interpreter (Principle IV).
- **No-hide**: a relaxed finding (effective `Advisory`) is still emitted into `generatedViews` with both
  severities (FR-006); it is never dropped, and relaxing it never changes the carried `CurrencyStatus`/`Cause`.
- **Never fabricate**: `Current` is only ever the genuine `FreshnessKey.matches` outcome; a sense failure is
  `StaleUnresolved`/`Undeterminable`, never `Current` (FR-008).
- **`NotEvaluated` vs `StaleUnresolved` (FR-008)**: `NotEvaluated` means a view deliberately **out of currency
  scope** — `findingsOf` emits no finding (a genuine pass). `StaleUnresolved` means an **in-scope** view whose
  currency could not be determined — always a finding, never a silent pass. The two are never conflated: an
  unchecked-because-unsensed in-scope view is `StaleUnresolved`, not `NotEvaluated`.
