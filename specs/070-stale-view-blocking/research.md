# Phase 0 — Research: Block Stale Generated Views at the Configured Governance Boundary

This row introduces **no new staleness detection, no new severity, and no new truth-table branch** (FR-003,
FR-007). Every decision below is a *reuse* decision: which existing core to consume, and where to wire it.

## D1 — Pattern: mirror F067 surface-check folding, not a new decision core

**Decision.** Model a stale generated view as a **finding** in a pure finding-vocabulary leaf
(`FS.GG.Governance.CurrencyEnforcement`, the analogue of `SurfaceChecks.Model`), bridge it into the F023 truth
table with `enforcementInputOf` (verbatim the surface-check shape), and let the host fold it through the
existing `deriveEffectiveSeverity` into the existing F024 verdict — adjusting `Verdict`/`ExitCodeBasis` and
threading the finding into an additive detail array.

**Rationale.** F067 already established this exact path for surface checks
(`SurfaceChecks.Model.enforcementInputOf : SurfaceFinding -> RunMode -> Profile -> EnforcementInput`, then
`VerifyCommand.Loop.foldSurfaceVerdict profile findings decision : ShipDecision`, then
`VerifyJson.ofVerifyDecisionWithPreview folded … findings preview`). The spec (Assumptions, line 224) names
F067 as the precedent to mirror. Reusing it means **zero** new enforcement semantics: the only enforcement call
is the existing `deriveEffectiveSeverity`.

**Alternatives considered.**
- *A new pure decision core for staleness* — rejected: FR-003 forbids a new truth-table branch; F023 already
  maps `(BaseSeverity, Maturity, RunMode, Profile) → (EffectiveSeverity, Reason)` totally and the currency
  finding fits that shape unchanged.
- *The F25 cost-finding fixed-`Advisory` floor* — rejected as the model: F25 is fixed-advisory by construction
  (`BaseSeverity = Advisory`, `Maturity = Warn`) and can never block; this row's entire point is to **let it
  block when configured**, which is the F067 shape (variable maturity dial), not the F25 shape.

## D2 — The finding never becomes an `EnforcedItem`

**Decision.** The currency finding adjusts only `ShipDecision.Verdict`/`ExitCodeBasis` and rides in an
**additive** `generatedViews` detail array on `verify.json` / `ship.json` / `audit.json`. It is **not** added
to the `Blockers`/`Warnings`/`Passing` partition lists.

**Rationale.** `Ship.Model.EnforcedItemId` is a **closed** DU (`GateItem of GateId | FindingItem of FindingId
* GovernedPath`) and `Findings.Model.FindingId` is a **closed** two-case DU
(`UnknownGovernedPath | UnknownProtectedBoundaryPath`). A stale view is neither a catalog gate nor an
unknown-governed-path finding, so it cannot be expressed as an `EnforcedItem` without adding a case to a closed
core — which FR-003 ("no new … value") and FR-010 ("existing verdict partition") forbid. F067 surface findings
have the same constraint and resolve it identically: they ride in the additive `surfaceChecks` array and only
fold the verdict (see `foldSurfaceVerdict` + `ofVerifyDecisionWithPreview … model.SurfaceFindings`). The
partition stays the closed F024 partition; the verdict still reflects the finding via `Verdict`/`ExitCodeBasis`.

## D3 — Maturity-dial home: additive field on the F057 generation manifest

**Decision.** Add `CurrencyEnforcement: Maturity option` (default `None`) to
`RefreshJson.RefreshModel.GenerationManifest`, parsed by `RefreshCommand.Declaration.parse` from a new
manifest-level `currency-enforcement:` key in `.fsgg/refresh.yml`. One dial governs all declared views; absent
key ⇒ `None` ⇒ advisory/opt-in.

**Rationale.** The spec (Assumptions, lines 215–217) explicitly **locks the config home in the plan** and names
`.fsgg/refresh.yml` as the leading candidate. `refresh.yml` already declares the generated views and their
sources (`GenerationEntry`), so the currency *gate* lives co-located with the view *declarations* it gates —
one file, one parse, no cross-reference. The maturity vocabulary is the existing F014 `Config.Model.Maturity`
(`Observe`/`Warn`/`BlockOnPr`/`BlockOnShip`/`BlockOnRelease`) reused verbatim (FR-001). The field is **not**
projected into `refresh.json` (the projection renders only the fields it already renders), so `refresh.json`
stays byte-identical; `Declaration.parse`'s signature is unchanged (only the `.fs` body reads the new key), so
its `.fsi`/baseline do not move.

**Alternatives considered.**
- *`.fsgg/policy.yml`* — rejected: policy.yml does not declare the generated views, so a per-row dial there
  would need a second parser surface and a cross-reference from gate → view declaration. F023's `.fsi`
  explicitly notes the enforcement core does **no** `policy.yml` parsing; keeping the dial on the manifest
  honors that separation.
- *Per-entry maturity override* — deferred: the spec's Key Entities calls it "the maturity dial" (singular);
  one manifest-level dial is the MVP. A per-`GenerationEntry` override is an additive follow-up (a bounded
  deferral, Constitution Development Workflow), not needed for the roadmap row.

## D4 — Currency-determination source: reuse `FreshnessKey` at the verify/ship edge

**Decision.** Obtain per-view currency at the verify/ship edge exactly as `fsgg refresh` does: parse
`.fsgg/refresh.yml` via `RefreshCommand.Declaration.parse`, read each view's recorded provenance from its
generated lock, sense the declared sources' digests + the generator version, and decide currency by reusing the
F029 `FreshnessKey.matches`/`diff` comparator **verbatim** (recorded vs current `FreshnessInputs` differing
only in the source-digest set + generator version, revisions held equal — F057 research D1). The result is the
existing `RefreshModel.CurrencyStatus`/`ViewDecision`. Surface this decision once as a pure
`CurrencyEnforcement.decideCurrency` in the leaf.

**Rationale.** FR-007 requires consuming the determination that already exists — "no new staleness detection,
sensing, or currency representation." `FreshnessKey.matches`/`diff` and `CurrencyStatus` are precisely that
machinery; reusing the same comparator and the same closed `CurrencyStatus` representation at a new host edge
is wiring, not new detection (the F067 precedent: F067 wired existing surface-check sensing into verify). The
verify/ship hosts must sense **live** currency (the `refresh.json` artifact may itself be stale or absent), so
they cannot simply read a prior artifact.

**Alternatives considered.**
- *Reuse RefreshCommand's per-view currency helper directly* — not possible cleanly: that helper is **private**
  to `RefreshCommand.Loop.fs` (the `.fsi` documents "the per-view currency helper … live ONLY in the .fs").
- *Widen RefreshCommand's surface to export it* — rejected: it would create a host→host dependency and reopen a
  command surface for a leaf concern. Instead `decideCurrency` re-expresses the *same* `FreshnessKey`-based
  decision once in the leaf (the pure place it belongs), available to both hosts and to RefreshCommand later.
- *Read `refresh.json` and trust it* — rejected: it can be stale/absent; the gate must sense live state.

## D5 — Opt-in gate: `findingsOf`

**Decision.** `findingsOf : Maturity option -> ViewDecision list -> CurrencyFinding list`:
- `None` ⇒ `[]` (unconfigured ⇒ no finding ⇒ byte-identity).
- `Some m` ⇒ one `CurrencyFinding` per view whose status is stale — `WouldRegenerate`/`Regenerated` (drift) or
  `StaleUnresolved` — carrying `BaseSeverity = Blocking`, `Maturity = m`, the `ViewId`/`Kind`, and a typed
  `StaleCause` (digest-mismatch with the drifted `InputCategory` list, or undeterminable-with-reason).
- `Current`/`NotEvaluated` ⇒ **no** finding (FR-007: a current view produces nothing; an out-of-scope view is
  never assumed stale).

**Rationale.** Default-`None` is the opt-in/default-advisory contract (FR-004): no config ⇒ empty list ⇒
identity fold ⇒ omitted detail array ⇒ byte-identical existing artifacts (SC-002, SC-006). `BaseSeverity =
Blocking` (not the F25 fixed-`Advisory`) is what lets the maturity dial actually block when configured; the
truth table then relaxes it to a warning at lower boundaries (D6).

## D6 — No-hide warning (US3, FR-006)

**Decision.** Each finding's `decisionOf finding mode profile = deriveEffectiveSeverity (enforcementInputOf …)`
is carried into `generatedViews` with **both** base and effective severity. The host's `foldViewCurrencyVerdict`
fails the verdict only for effective-`Blocking` findings; effective-`Advisory` findings stay visible warnings.

**Rationale.** `deriveEffectiveSeverity` already derives `Advisory` for `Observe`/`Warn` regardless of
mode/profile, and for a base-blocking finding whose run mode has not reached the maturity's profile-adjusted
boundary. So a finding configured `block-on-ship` is effective-`Advisory` under `RunMode.Verify` (a warning,
FR-009) and a profile that relaxes it stays a visible warning — both without touching the underlying
`CurrencyStatus`. The projection renders both severities so the warning is self-explaining (SC-004).

## D7 — Additive projection: omitted-when-empty

**Decision.** Add a new overload to each finding-bearing projection — e.g.
`VerifyJson.ofVerifyDecisionWith…`, the `ship.json` analogue, `AuditJson.ofAuditDecisionWith…` — that threads
the currency findings + their decisions and emits an additive `generatedViews` array. The existing
`ofVerifyDecision`/`ofShipDecision`/`ofAuditDecision` entry points are **untouched**. The array is **omitted
entirely** (not emitted as `[]`) when there are no currency findings.

**Rationale.** FR-010: no existing projection signature changes; new JSON detail is additive. Omitting the
field when empty (rather than emitting `"generatedViews":[]`) guarantees the existing goldens are byte-identical
when unconfigured (SC-002) — the same discipline F067 used so `verify.json` with no surface findings is
byte-identical (Loop.fs comment "With no findings it is byte-identical verify.json (FR-004)").

## D8 — Undeterminable currency & operational failure (FR-008)

**Decision.** A view whose currency cannot be determined (no declared sources, missing manifest entry,
unreadable provenance lock) is the existing `StaleUnresolved of reason` — emitted as a finding (so it never
silently passes when configured) and never coerced to `Current`. A genuine operational sense failure (manifest
or lock unreadable at the I/O edge) surfaces through the host's existing exit-code/diagnostic path as
input-unavailable, never as a fabricated "all current" pass.

**Rationale.** FR-008 and Constitution VI forbid fabricating currency or swallowing input failures.
`StaleUnresolved` already carries WHY (RefreshModel `.fsi`: "carries WHY (FR-016) and is NEVER coerced to
`Current`"); reusing it preserves that guarantee. The host distinguishes missing/bad input from a tool defect
on its diagnostic channel (Principle VI), exactly as the refresh and verify hosts already do.

---

**No `NEEDS CLARIFICATION` markers remain.** Every decision reuses an existing core; the only new code is the
pure finding-vocabulary leaf and the F067-shaped host wiring.
