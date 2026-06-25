# Contract: Shared `SurfaceFinding` + `SurfaceCheckRequest` + `Composition` (F24)

**Libraries**: `FS.GG.Governance.SurfaceChecks` (`Model`) + `FS.GG.Governance.SurfaceChecks.Dispatch` (`Composition`) | **Consumers**: the four domain packs, `VerifyCommand`

This is the cross-domain contract every pack produces and the host consumes. It owns the finding shape, the
per-surface request derived from one F23 classification, and the pure composition dispatcher.

## C1 — `SurfaceFinding` is the single finding shape (FR-009, FR-011, FR-012)

- Every domain pack emits `SurfaceChecks.Model.SurfaceFinding` — no domain defines its own finding type.
- `BaseSeverity : Severity` is the **F023 `Severity`** (`Advisory | Blocking`), reused verbatim (no new DU).
  Deterministic rule ⇒ `Blocking`; judgement-heavy ⇒ `Advisory`.
- `EvidenceTag : EvidenceTag option` carries the surface's F23-declared tag so produced evidence ties back
  (FR-009). `None` when the surface declared no tag — still a valid finding, not an error.
- `IsInputState : bool` is `true` iff the finding reports a missing/malformed **input** (absent baseline,
  unlocatable transcript, unreadable source, absent catalog), never a rule violation and never a fabricated
  pass (FR-012). `Code` and `Message` name the offending source.
- `Message`, `Code`, and `Location.Detail` are deterministic: no absolute path, timestamp, username, or
  environment value (FR-010). `Location.File` is repo-relative, forward-slash normalized via `normalizePath`.

## C2 — `SurfaceCheckRequest` is derived from one `ProductClassification` (FR-008, D4)

- `Composition.requestsOf facts report` produces **one request per applicable classification** in `report`.
- "Applicable" = `Composition.domainOf classification.Class` is `Some domain`. Non-product / boundary classes
  (`Routine`, `GovernedRoot`, `ProtectedSurface`, `GeneratedView`, `ReleaseSurface`, `SampleAppSurface`,
  `GeneratedProductRoot`) map to `None` and produce **no** request (FR-015). The four product-check classes
  map as: `PackageSurface → PackageDomain`, `DocsSurface → DocsDomain`, `SkillSurface → SkillDomain`,
  `DesignSurface → DesignDomain`.
- The request's `EvidenceTag` is looked up from `facts.Capabilities.Surfaces` by `Surface` id (the declared
  tag, or `None`).

## C3 — `Composition.run` is pure, total, order-independent (FR-008, SC-008)

- Signature: `run : TypedFacts -> ProductSurfaceReport -> DomainFactBundle -> SurfaceFinding list`.
- **Pure / total / no I/O / no clock.** Empty report or empty bundle ⇒ `[]` (valid success).
- For each request, looks up the matching domain's facts in `bundle` by `SurfaceId`; if present, runs that
  pack's `evaluate request facts` and collects the findings; if the bundle has no facts for that surface
  (host did not sense it), contributes nothing.
- **Order-independence**: the result is identical regardless of the order of `report.Classifications` or of
  the bundle maps — the output is sorted by `(Surface id token, CheckDomain ordinal, Location.File,
  Location.Detail, Code)`. A single change touching package+docs+skill yields exactly three independent
  groups of findings, none depending on another (SC-008).
- **No pack depends on another**: each domain library references only `SurfaceChecks.Model`; only
  `Composition` references all four, and only to dispatch.

## C4 — Enforcement reuse (FR-014)

- `enforcementInputOf finding mode profile` builds the F023 `EnforcementInput` from a finding's
  `BaseSeverity` + `Maturity`. The verdict is computed by the existing `deriveEffectiveSeverity` — this row
  adds **no** truth-table logic.
- A run whose only surface findings are `Advisory` never blocks under any `RunMode`/`Profile` (SC-006),
  because `deriveEffectiveSeverity` echoes base-Advisory as Advisory.

## Acceptance (contract tests)

1. `requestsOf` over a report with one package + one docs + one skill classification ⇒ exactly three
   requests, each with the correct domain and the surface's declared `EvidenceTag`.
2. `requestsOf` over a report containing only boundary classes ⇒ `[]`.
3. `run` is order-independent: shuffling `report.Classifications` and the bundle maps yields byte-identical
   output (determinism + SC-008).
4. A bundle with facts producing one Advisory finding ⇒ `deriveEffectiveSeverity` returns Advisory under
   every `(RunMode, Profile)` pair (SC-006).
5. A surface declaring an `EvidenceTag` whose pack produces a finding ⇒ the finding's `EvidenceTag` equals
   the declared tag (FR-009, SC-007).
