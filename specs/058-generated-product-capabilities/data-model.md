# Phase 1 Data Model: Generated-Product Capabilities (F23)

**Branch**: `058-generated-product-capabilities` | **Spec**: [spec.md](./spec.md) |
**Research**: [research.md](./research.md)

All new vocabulary lives in **`FS.GG.Governance.Config.Model`** (the catalog) and the new product-facing
adapter **`FS.GG.Governance.ProductSurfaces`** — never in the kernel (FR-014, SC-007). Every collection is
emitted in deterministic id/path-sorted order, as today (FR-012). Signatures below are the intended `.fsi`
shapes; F# `.fsi` is the sole visibility declaration (Constitution II).

---

## 1. Extended catalog vocabulary (`FS.GG.Governance.Config.Model`)

### 1.1 `SurfaceClass` — extended closed DU (D2)

```fsharp
type SurfaceClass =
    // — MVP (unchanged) —
    | Routine
    | GovernedRoot
    | ProtectedSurface
    | GeneratedView
    | ReleaseSurface
    // — F23 product kinds —
    | PackageSurface          // kind: package          — package/API surface (Baseline pins it)
    | DocsSurface             // kind: docs             — docs/examples set
    | SkillSurface            // kind: skill            — a skill
    | DesignSurface           // kind: design           — design artifact
    | SampleAppSurface        // kind: sampleApp        — a sample app
    | GeneratedProductRoot    // kind: generatedProduct — a generated root + its TemplateProfile
```

YAML `kind` ↔ case mapping is total and single-sourced in `Schema` (parse) + a `surfaceClassToken` helper
(render). Any token outside the closed set ⇒ `MalformedValue` (FR-012).

### 1.2 `GeneratedProductTier` — new closed, ordered DU (D4)

```fsharp
/// The declared depth of a generated-product check (FR-005). Ordered:
/// StructuralScan < RestoreBuild < FocusedTests < FullVerify < ReleaseValidation.
type GeneratedProductTier =
    | StructuralScan          // structuralScan
    | RestoreBuild            // restoreBuild
    | FocusedTests            // focusedTests
    | FullVerify              // fullVerify
    | ReleaseValidation       // releaseValidation
```

A `generatedProductTierRank: GeneratedProductTier -> int` (1..5) + `generatedProductTierToken` give
deterministic ordering and rendering. Separate from the unchanged generic `Cost` DU.

### 1.3 New single-string newtypes (D3)

```fsharp
type EvidenceTag = EvidenceTag of string       // label tying a surface to currency evidence (F24 produces it)
type TemplateProfile = TemplateProfile of string // the template a generatedProduct root was instantiated from
type Baseline = Baseline of string              // the pin fixing a package surface (drift check is F24)
```

### 1.4 `Surface` — extended record (D3)

```fsharp
type Surface =
    { Id: SurfaceId
      Class: SurfaceClass
      Paths: GovernedPath list
      Owner: Owner
      Maturity: Maturity
      // — F23 optional product attributes (all None for an MVP-shaped surface) —
      EvidenceTag: EvidenceTag option        // any kind
      TemplateProfile: TemplateProfile option // meaningful on GeneratedProductRoot
      Baseline: Baseline option }             // meaningful on PackageSurface
```

Optional ⇒ a subset declaration is valid, not an error (edge case "subset declaration"). The fields are
parsed only under `capabilities.yml` v2; an unknown field still ⇒ `UnknownField`.

### 1.5 `Check` — extended record (D3/D4)

```fsharp
type Check =
    { Id: CheckId
      Domain: DomainId
      Command: CommandId option
      Owner: Owner
      Cost: Cost
      Environment: EnvironmentClass
      Maturity: Maturity
      // — F23 —
      Tier: GeneratedProductTier option }     // present on cost-tiered generated-product checks
```

`CapabilityFacts` is structurally unchanged (it already holds `Surfaces`/`Checks`); only the element records
grow. `SchemaVersion` of `capabilities.yml` is now `2` (D1).

---

## 2. Versioning & validation (`FS.GG.Governance.Config.Schema`)

### 2.1 Per-file supported version (D1)

```fsharp
// REPLACES `val supportedSchemaVersion: SchemaVersion`
val supportedVersionFor: file: FsggFile -> SchemaVersion
// Capabilities -> SchemaVersion 2 ; Project/Policy/Tooling -> SchemaVersion 1
```

`validate` checks each present file's `schemaVersion` against `supportedVersionFor file`. A `capabilities.yml`
that is not `2` ⇒ `UnsupportedSchemaVersion` whose `Message` names the version **and** the migration guidance
(`contracts/migration.md`); SC-006.

### 2.2 Diagnostics — the CLOSED set is unchanged (FR-012/FR-015)

No new `DiagnosticId`. The new fields reuse the existing vocabulary:

| Condition (new field)                                        | Diagnostic |
|--------------------------------------------------------------|------------|
| field not in v2 schema                                       | `UnknownField` |
| bad `kind` / `tier` / malformed evidence-tag scalar          | `MalformedValue` |
| duplicate surface/check/domain id (any kind)                 | `DuplicateId` |
| `capabilities.yml` `schemaVersion ≠ 2`                       | `UnsupportedSchemaVersion` (+ migration pointer) |
| declared source path escapes the governed root (monorepo-only)| `PathEscapesRoot` (FR-010 standalone diagnostic) |
| `Check.Domain` / `PathMapEntry.Capability` not a declared domain | `DanglingReference` (unchanged) |

Determinism, duplicate-id rejection, and unknown-field rejection extend to the new fields exactly as the MVP
fields (FR-012, SC-005).

---

## 3. Classification + tier selection (`FS.GG.Governance.ProductSurfaces.Model`)

```fsharp
/// The cheaper-local-alternative outcome for a selected tier (FR-007), modeled on
/// RouteExplain.AlternativeOutcome — always present, never null/option.
type TierAlternative =
    | CheaperLocalTier of GeneratedProductTier   // a strictly-cheaper, locally-runnable declared tier
    | NoCheaperLocalTier

/// Why this surface won when a path fell under more than one (D6) — deterministic, order-independent.
type ClassificationReason =
    | OnlySurface                                // exactly one surface covered the path
    | HighestPrecedenceKind                      // won on the documented SurfaceClass order
    | OrdinalSurfaceTiebreak                     // co-kind, ordinal-first SurfaceId won

/// One routed path classified to a product surface with its selected cost tier (FR-002/FR-006/FR-007).
type ProductClassification =
    { Path: GovernedPath
      Capability: DomainId                       // the F015 routed domain
      Surface: SurfaceId                         // the winning declared surface
      Class: SurfaceClass                        // its classification (package/docs/skills/design/release/…)
      SelectedTier: GeneratedProductTier         // D7
      TierIsDeclared: bool                       // false ⇒ the F24-pending non-error note (FR-016)
      Alternative: TierAlternative
      Reason: ClassificationReason
      Explanation: string }                      // names capability, class, tier, and (when known) the alternative

/// The deterministic aggregate (FR-008): one entry per routed path that fell under a declared surface,
/// sorted by normalized Path then SurfaceId token. A path under no declared surface produces NO entry
/// (light-by-default, FR-004). An empty report is a valid success.
type ProductSurfaceReport =
    { Classifications: ProductClassification list }
```

Entry point (`ProductSurfaces.fsi`):

```fsharp
val classify:
    facts: TypedFacts -> report: RouteReport -> profile: ProfileId -> ProductSurfaceReport
```

Pure/total; consumes upstream values verbatim (no re-parse/re-route/sense). See
[contracts/classification.md](./contracts/classification.md) for the precedence order and the
(kind × profile) → tier table.

---

## 4. Unknown-protected-boundary extension (`FS.GG.Governance.Findings`)

No public type change — `FindingId` / `FindingZone` / `UnknownGovernedPathFinding` are unchanged. The
**escalating-boundary set** widens (FR-003, SC-002): an `UnmatchedInRoot` path under any of

```
ProtectedSurface ∪ PackageSurface ∪ ReleaseSurface ∪ GeneratedProductRoot
```

yields `Id = UnknownProtectedBoundaryPath` / `Zone = ProtectedBoundaryUnknown sid` (was: `ProtectedSurface`
only). Docs/skill/design/sampleApp/generatedView boundaries remain ordinary (`UnknownGovernedPath`). This
closes the "new protected surface hidden under a governed root" hole. Behavior change only; the
`Findings.surface.txt` baseline is unchanged.

---

## 5. Route surfacing (`FS.GG.Governance.RouteJson`, `.RouteCommand`)

- `RouteJson.schemaVersion`: `"fsgg.route/v1"` → **`"fsgg.route/v2"`** (D8).
- `RouteJson` wire object gains a deterministic `productSurfaces` array; per entry:
  `{ path, capability, surface, class, tier, tierDeclared, alternative }`. `ofRouteResult` takes the
  `ProductSurfaceReport` (threaded from the command edge).
- `RouteCommand` renders the per-path classification + selected tier + cheaper-local alternative in its
  human output; printed machine output equals the persisted `route.json` (the existing one-source-of-truth
  contract). Baselines for `RouteJson` (and any `RouteCommand` Model change) update.

---

## Entity ↔ requirement / spec-key-entity trace

| Spec entity / FR                          | Model element |
|-------------------------------------------|---------------|
| Capability catalog (expanded) / FR-001    | `CapabilityFacts` (v2) + extended `Surface`/`Check` |
| Product surface / FR-002                  | `SurfaceClass` new cases + `Surface` |
| Template profile / FR-001                 | `Surface.TemplateProfile` |
| Package pin / baseline / FR-001           | `Surface.Baseline` |
| Cost tier / FR-005                        | `GeneratedProductTier`, `Check.Tier` |
| Evidence tag / FR-001, FR-016             | `Surface.EvidenceTag` (+ `TierIsDeclared=false` note) |
| Route/classification result / FR-002,7,8  | `ProductClassification` / `ProductSurfaceReport` |
| Unknown-governed-path / FR-003            | Findings escalating-boundary widening |
| Schema version & migration / FR-011,13    | `supportedVersionFor`, `contracts/migration.md` |
| Standalone / FR-009,10                    | per-dir `Loader` (reused) + `PathEscapesRoot` |
