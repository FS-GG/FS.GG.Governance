# Contract: product-surface classification & cost-tier selection (F23)

`FS.GG.Governance.ProductSurfaces.classify : TypedFacts -> RouteReport -> ProfileId -> ProductSurfaceReport`.
Pure and total: no I/O, no git, no clock, never throws, byte-identical for identical input (the F031
`RouteExplain` discipline). It re-parses no YAML, re-routes nothing, senses no git — it consumes the F014
facts and the F015 route report verbatim.

## Per-path algorithm

For each routing in `report.Routings` (in normalized-path order):

1. **Membership.** Find every declared `Surface` whose `Paths` cover the routed path, by the **same
   segment-prefix relation** routing/findings use on the normalized `GovernedPath`. A surface `Paths` entry
   that is a glob is matched with `Glob.matches` (the F015 matcher) — never a fresh matcher.
2. **No match ⇒ no entry.** A routed path under no declared surface produces **no** `ProductClassification`
   (light-by-default, FR-004). It is not an error and not a default classification.
3. **Precedence (FR-008, D6).** When >1 surface covers the path, pick the winner by the documented total
   order over `SurfaceClass` (most-protected / highest-stakes first):

   ```
   ReleaseSurface > PackageSurface > GeneratedProductRoot > DesignSurface > SkillSurface
                 > DocsSurface > SampleAppSurface > GeneratedView > ProtectedSurface
                 > GovernedRoot > Routine
   ```

   Ties within a kind break by **ordinal-first `SurfaceId`** (`Reason = OrdinalSurfaceTiebreak`); a single
   covering surface is `OnlySurface`; a cross-kind win is `HighestPrecedenceKind`. The order is data —
   independent of declaration/evaluation order (SC-005).
4. **Tier selection (FR-006, D7).** Compute the target tier, then snap to a declared tier:

   - **Baseline tier per winning kind** (cheap-by-default):

     | kind                                   | baseline tier      |
     |----------------------------------------|--------------------|
     | `DocsSurface`, `SampleAppSurface`      | `StructuralScan`   |
     | `SkillSurface`, `DesignSurface`, `GeneratedView` | `RestoreBuild` |
     | `PackageSurface`, `GeneratedProductRoot` | `FocusedTests`   |
     | `ReleaseSurface`                       | `FullVerify`       |
     | (boundary-only: `ProtectedSurface`/`GovernedRoot`/`Routine`) | not tiered — no entry |

   - **Profile escalation** (positive-match only): a release-oriented profile raises a `ReleaseSurface`
     target to `ReleaseValidation`; a strict profile raises the target by **one** rank. A profile **never
     lowers** below the baseline and **never raises a kind that did not match** (FR-006, SC-003). Profiles
     are read from the declared `PolicyFacts.Profiles`; an unknown profile name is the caller's error
     (the edge passes a declared profile).
   - **Snap to a declared tier**: among generated-product checks (those with `Tier`) in the winning
     surface's capability domain, select the **deepest declared tier that does not exceed the target**
     (cheap-by-default). If none ≤ target is declared, select the cheapest declared; if **no** tiered check
     is declared for the domain, `SelectedTier = target` and `TierIsDeclared = false` (the F24-pending,
     non-error note, FR-016).
5. **Cheaper-local alternative (FR-007).** `CheaperLocalTier t` when a strictly-cheaper, locally-runnable
   declared tier exists for the domain (a declared tiered check with `Environment ∈ {Local, LocalOrCi}` and
   `Tier < SelectedTier`); else `NoCheaperLocalTier`. Always present, never null.
6. **Explanation.** A deterministic string naming the **matched capability, the surface classification, the
   selected tier**, and (when known) the cheaper local alternative (FR-007). No raw YAML, host path,
   timestamp, or product vocabulary beyond declared ids.

## Determinism

`ProductSurfaceReport.Classifications` is sorted by normalized `Path` (ordinal) then `SurfaceId` token.
Re-ordering the authored surfaces/checks or the input paths does not change the report (SC-005). An empty
report is a valid, successful outcome (FR-004).

## Worked selections (against the schema-v2 example)

| Change                                  | Profile  | Class                  | Selected tier        | Alternative |
|-----------------------------------------|----------|------------------------|----------------------|-------------|
| `docs/guide.md`                         | standard | `DocsSurface`          | `StructuralScan`     | `NoCheaperLocalTier` |
| `src/Foo.fsi`                           | standard | `PackageSurface`       | `FocusedTests`       | `CheaperLocalTier StructuralScan` |
| `release/notes.md`                      | release  | `ReleaseSurface`       | `ReleaseValidation`  | `CheaperLocalTier FullVerify` |
| `samples/App/Program.fs` (also in root) | strict   | `SampleAppSurface`     | `RestoreBuild` (raised 1) | depends on declared tiers |
| path under both `package` and `release` | release  | `ReleaseSurface` (precedence) | `ReleaseValidation` | … |

## CLI / JSON surfacing (`fsgg route`)

- `RouteCommand` runs `classify` after `Routing.route` and renders each `ProductClassification` (path →
  capability · class · tier · alternative) in human output.
- `RouteJson` bumps `schemaVersion` to `"fsgg.route/v2"` and emits a deterministic `productSurfaces` array:
  `{ path, capability, surface, class, tier, tierDeclared, alternative }`, sorted by path then surface.
  Printed machine output equals the persisted `route.json` byte-for-byte (existing one-source-of-truth +
  determinism contracts).
