# Phase 0 Research: Generated-Product Capabilities (F23)

**Branch**: `058-generated-product-capabilities` | **Spec**: [spec.md](./spec.md)

This row **expands** the F014 `.fsgg/capabilities.yml` schema and extends F015 routing / F017 findings
to the full product-surface vocabulary, then surfaces the result through the existing `fsgg route` host.
Every decision below resolves a `NEEDS CLARIFICATION` left by the spec's Assumptions section, grounded in
the existing implementation (`FS.GG.Governance.Config`, `.Routing`, `.Findings`, `.RouteCommand`,
`.RouteJson`).

---

## D1 — Migration policy: hard bump of `capabilities.yml` to schemaVersion 2 (per-file)

**Decision.** `capabilities.yml` is hard-bumped to **`schemaVersion: 2`**; any other version (1, 3, …) is
rejected with `UnsupportedSchemaVersion` whose message names the version **and points at the migration
guidance** (`contracts/migration.md`). The bump is **per-file**: `project.yml`, `policy.yml`, and
`tooling.yml` remain on `schemaVersion: 1` (untouched, per spec). Validation becomes version-aware per file
rather than against one global constant.

**Rationale.** The requester selected the hard bump over additive `{1,2}` acceptance (planning decision).
A single supported version per file keeps one invariant ("capabilities is v2, full stop") and forces every
governed repo to adopt the documented migration rather than silently running a half-expanded catalog. The
spec confines the expansion to `capabilities.yml` (FR-013 / Assumptions: the other three files are
untouched), so the bump must be scoped to that file only — the other three keep validating against v1.

**Public-surface consequence.** `Schema.supportedSchemaVersion: SchemaVersion` (one global value) becomes
`Schema.supportedVersionFor: FsggFile -> SchemaVersion` (`Capabilities → 2`, the other three `→ 1`). This is
a Tier-1 change to the `FS.GG.Governance.Config` public surface; the `Config.surface.txt` baseline updates.

**Migration cost (this row).** 24 `capabilities.yml` fixtures carry `schemaVersion: 1` and must move to `2`.
The 29 `project/policy/tooling.yml` fixtures stay at `1`. `malformed-unsupported-schema-version` is
re-pointed at a v3 (or v1) `capabilities.yml`; a NEW `migration-v1-capabilities` fixture proves a v1
`capabilities.yml` is rejected with the migration pointer (SC-006).

**Alternatives rejected.** Additive `{1,2}` (friendlier, no forced break) — rejected by the requester.
One global bump of all four files — rejected: violates "project/policy/tooling untouched."

---

## D2 — New surface kinds extend the closed `SurfaceClass` DU

**Decision.** Extend `Model.SurfaceClass` (today `Routine | GovernedRoot | ProtectedSurface | GeneratedView
| ReleaseSurface`) with the six product kinds the spec names:

| YAML `kind` token | DU case            | Meaning (FR-001/FR-002)                        |
|-------------------|--------------------|------------------------------------------------|
| `package`         | `PackageSurface`   | package/API surface (pinned by a baseline)     |
| `docs`            | `DocsSurface`      | docs/examples set                              |
| `skill`           | `SkillSurface`     | a skill                                        |
| `design`          | `DesignSurface`    | design artifact                                |
| `sampleApp`       | `SampleAppSurface` | a sample app                                   |
| `generatedProduct`| `GeneratedProductRoot` | a generated root + its template profile    |

`release` (`ReleaseSurface`) and `generatedView` (`GeneratedView`) already exist and are reused verbatim;
`routine`/`governedRoot`/`protected` are unchanged. FR-002's classification vocabulary (package / docs /
skills / design / release / generated-product / generated-view) maps onto these cases; `sampleApp` is
declarable + classifiable as its own kind (Key Entities lists it as a product surface).

**Rationale.** Extending the one closed DU keeps surface classification single-sourced and forces every
`match` on `SurfaceClass` (F017 Findings, any later consumer) to handle the new kinds — the compiler is the
exhaustiveness check, exactly the property a governance tool wants. No parallel surface taxonomy.

**Consequence.** Every total `match` over `SurfaceClass` is updated (F017 `Findings.fs` is the only current
exhaustive consumer; the new `ProductSurfaces` module is the other). Tier-1 ripple, no new module needed
for the vocabulary itself.

---

## D3 — Product attributes are optional fields on `Surface` / `Check`, not new top-level lists

**Decision.** Carry the product attributes as **optional fields** on the existing records rather than new
top-level catalog sections:

- `Surface` gains `EvidenceTag: EvidenceTag option`, `TemplateProfile: TemplateProfile option`,
  `Baseline: Baseline option` (new single-string newtypes). Semantics: `TemplateProfile` is meaningful on
  `GeneratedProductRoot`, `Baseline` on `PackageSurface`, `EvidenceTag` on any surface (the label tying it
  to the evidence that would prove it current — whose *production* is F24).
- `Check` gains `Tier: GeneratedProductTier option` — present for the cost-tiered generated-product checks
  (D4), absent for ordinary MVP checks (which keep only `Cost`).

**Rationale.** Additive optional fields preserve the MVP record shapes, keep the YAML flat (the authored
shape the spec shows), and let a catalog **declare a subset** of kinds without ceremony (edge case "subset
declaration is not an error"). New top-level lists would duplicate the surface/check identity already
modeled and complicate cross-reference checking. A declared `EvidenceTag` whose check does not yet exist is
the expected, non-error state F23 produces (FR-016) — an `option` models that directly.

**Alternatives rejected.** Dedicated `TemplateProfiles` / `Baselines` / `EvidenceTags` top-level lists with
their own ids and cross-refs — more schema, more dangling-reference surface, no expressive gain for F23.

---

## D4 — Cost tiers are a new closed `GeneratedProductTier` DU, distinct from `Cost`

**Decision.** Add `Model.GeneratedProductTier`, a closed **ordered** DU naming the five roadmap depths:

```
StructuralScan  <  RestoreBuild  <  FocusedTests  <  FullVerify  <  ReleaseValidation
```

(YAML tokens `structuralScan` / `restoreBuild` / `focusedTests` / `fullVerify` / `releaseValidation`.) It is
**separate** from the existing generic `Cost` (`Cheap | Medium | High | Exhaustive`): `Cost` stays the
coarse per-check cost metadata; `GeneratedProductTier` is the *named depth* a generated-product check
selects on. A generated-product check carries both (`Cost` required, `Tier` optional-present).

**Rationale.** The five roadmap tiers are semantically named build stages, not a re-spelling of the four
generic cost buckets; collapsing them onto `Cost` would lose the structural-scan / restore-build / verify
distinction the spec's selection story (US2) depends on. A dedicated ordered DU gives deterministic
tier comparison and selection for free.

---

## D5 — Cost-tier *selection* lives in a new pure library, `FS.GG.Governance.ProductSurfaces`

**Decision.** Add one new pure library, **`FS.GG.Governance.ProductSurfaces`**, that consumes the already-
typed F014 facts and the already-computed F015 route report plus the active profile, and produces a
deterministic per-path classification + selected tier + explanation:

```fsharp
val classify:
    facts: TypedFacts -> report: RouteReport -> profile: ProfileId -> ProductSurfaceReport
```

It re-parses no YAML, re-routes nothing, senses no git — the F031 `RouteExplain` discipline (consume
upstream values verbatim, pure/total, byte-identical). Per routed path it finds the declared surface(s) it
falls under (segment-prefix on the normalized `GovernedPath`, the same relation routing/findings use),
classifies via the **precedence** order (D6), selects the **cost tier** (D7), and resolves a cheaper-local
alternative (reusing the `RouteExplain.AlternativeOutcome` shape — `CheaperLocalAlternative` /
`NoCheaperLocalAlternative`).

**Rationale.** Classification-of-a-routed-path + tier selection is a distinct concern from F017 Findings
(which flags *unknown* paths) and from F019 Route gate selection. A sibling pure library matches the
established decomposition (`Routing` / `Route` / `RouteExplain` / `Findings`) and keeps the kernel
product-neutral — all product vocabulary stays in `Config` (the catalog) and this product-facing adapter
(FR-014, SC-007). "Routing reuse, not a new router" (Assumptions): this composes `Routing.route`, it does
not replace it.

---

## D6 — Multi-surface precedence: a documented total order over `SurfaceClass`, ordinal-id tiebreak

**Decision.** When a path falls under more than one declared surface (FR-008), classification picks the
highest-precedence surface kind by this **documented total order** (most-protected / highest-stakes first):

```
ReleaseSurface > PackageSurface > GeneratedProductRoot > DesignSurface > SkillSurface
              > DocsSurface > SampleAppSurface > GeneratedView > ProtectedSurface
              > GovernedRoot > Routine
```

Ties within a kind break by **ordinal-first `SurfaceId`** — the exact tiebreak F017 already documents
(`contracts/precedence.md`). The order is data, independent of declaration/evaluation order (SC-005).

**Rationale.** A single, written, order-independent precedence is the determinism FR-008 demands and mirrors
F017's existing `Protected > Routine > Ordinary` rule, extended to the full kind set. Release/package/
generated-product rank above docs/sample so a high-stakes overlap never resolves to a cheap classification.

---

## D7 — Tier selection: a baseline tier per kind, raised (never lowered) by the active profile

**Decision.** The selected tier is computed deterministically, then snapped to a **declared** check tier:

1. **Baseline tier per surface kind** (cheap-by-default): `DocsSurface`/`SampleAppSurface` →
   `StructuralScan`; `SkillSurface`/`DesignSurface`/`GeneratedView` → `RestoreBuild`; `PackageSurface`/
   `GeneratedProductRoot` → `FocusedTests`; `ReleaseSurface` → `FullVerify`.
2. **Profile escalation** (positive-match only): a release-oriented profile raises a `ReleaseSurface` to
   `ReleaseValidation`; a strict profile raises by one tier. A profile **never lowers** below the baseline,
   and **never raises a kind that did not positively match** (FR-006, SC-003).
3. **Snap to a declared tier**: select the **deepest declared** generated-product check tier (in the matched
   surface's capability domain) that does **not exceed** the computed target — cheap-by-default when the
   exact target tier is undeclared; if no generated-product check tier is declared for the domain, the
   selected tier is the computed target itself and the explanation notes "no declared check at this tier"
   (the F24-pending, non-error state, FR-016).

The exact (kind × profile) → tier table is frozen in `contracts/classification.md`.

**Rationale.** Baseline-per-kind keeps local authoring cheap (a docs edit selects `StructuralScan` even
under a strict profile's other surfaces), while profile escalation demands depth only where a positive
surface match justifies it — never pulling an expensive tier in without a match (the core US2 property).
Snapping to *declared* tiers honors "select among what the catalog declared," and the undeclared-tier note
is exactly F23's "declared evidence tag without its check" non-error state.

---

## D8 — `route` CLI/JSON wiring + standalone governance (per-file loader, escape diagnostic)

**Decision.** Surface the classification through the **existing `fsgg route` host** (planning decision: wire
the CLI):

- `FS.GG.Governance.RouteCommand` calls `ProductSurfaces.classify` at the edge (after `Routing.route`) and
  renders the per-path classification + selected tier + cheaper-local alternative in its human output.
- `FS.GG.Governance.RouteJson` gains a deterministic `productSurfaces` array and **bumps its
  `schemaVersion`** (`fsgg.route/v1` → `fsgg.route/v2`); `ofRouteResult` takes the `ProductSurfaceReport`
  (or a `RouteResult` extended to carry it). Byte-identical for identical input (the existing RouteJson
  determinism contract).

**Standalone (FR-009/FR-010).** The F014 `Loader.fileSystemReader` already reads **only** the `.fsgg` parent
directory — it takes no monorepo path — so routing + classification already run from a standalone checkout
using only the product's own declared sources (US3 AC-1). The product takes no build/run-time dependency on
the tool (it is an external consumer; removing the tool changes nothing — US3 AC-2/SC-004). A declared
source **resolvable only inside the monorepo** is one whose normalized path **escapes the governed root**;
`Schema.validate` already emits `PathEscapesRoot` for it — F23 reuses that diagnostic as FR-010's "clear
input diagnostic for the unresolved reference," with message text naming the offending surface/field. **No
new diagnostic id** — the closed `DiagnosticId` set is preserved, and strict validation simply extends to
the new fields (FR-012, FR-015).

**Rationale.** Reusing the existing host edge (Assumptions: "the standalone-run entry point reuses the
existing host/CLI edge") avoids a parallel command, and the loader's existing single-directory anchoring is
already the standalone property — F23 proves it with an isolated fixture rather than building new I/O.

---

## Resolved unknowns summary

| Spec deferral                                   | Resolution |
|-------------------------------------------------|------------|
| MVP migration policy                            | D1 — hard bump `capabilities.yml` → v2 (per-file); v1 rejected + migration pointer |
| New surface vocabulary                          | D2 — six new `SurfaceClass` cases (closed DU) |
| Template profile / baseline / evidence tag shape| D3 — optional fields on `Surface`/`Check` |
| Cost-tier vocabulary                            | D4 — new ordered `GeneratedProductTier` DU |
| Where selection lives                           | D5 — new pure `ProductSurfaces` library |
| Multi-match precedence                          | D6 — documented total order + ordinal tiebreak |
| Tier-selection rule                             | D7 — baseline-per-kind, profile-raised, snapped to declared tiers |
| Standalone entry point + CLI surfacing          | D8 — wire `route` CLI/JSON; reuse per-dir loader + `PathEscapesRoot` |
