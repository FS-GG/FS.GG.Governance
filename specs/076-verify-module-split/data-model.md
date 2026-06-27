# Phase 1 Data Model: Verify god-module split (Phase C)

This is an internal refactor (Tier 1 only because it adds module surface). **No
domain data model changes** — every `Model`/`Msg`/`Effect`/`ShipDecision`/
`CacheEligibilityReport`/`VerifyReleasePreview`/`SurfaceFinding`/`CurrencyFinding`
type is untouched, and no record field, DU case, or schema version is added, removed,
or renamed. The "data model" of this feature is therefore the **module inventory** and
the **call seams** between them.

## Module inventory (after the split)

### VerifyCommand project

| Module | Kind | Compile order | Owns |
|---|---|---|---|
| `…VerifyCommand.SurfaceFold` | NEW public (`.fsi`+`.fs`) | before `Loop` | surface-check verdict fold |
| `…VerifyCommand.ViewCurrencyFold` | NEW public | before `Loop` | stale-generated-view fold + detail |
| `…VerifyCommand.ReleasePreview` | NEW public | before `Loop` | advisory release-readiness preview assembly |
| `…VerifyCommand.Loop` | EXISTING, surface byte-identical | after the folds | parse/init/**update**/render/exitCode + base pipeline; calls the three folds |
| `…VerifyCommand.Interpreter` / `Program` | UNCHANGED | unchanged | edge I/O |

### VerifyJson project

| Module | Kind | Compile order | Owns |
|---|---|---|---|
| `…VerifyJson.Core` | NEW public | first | verdict/enforcement/cache/execution/item/section/currency writers + `writeCore` |
| `…VerifyJson.SurfaceChecks` | NEW public | after Core | `writeSurfaceFinding` |
| `…VerifyJson.ReleaseReadiness` | NEW public | after Core | pack/version/attestation/`releaseReadiness` writers |
| `…VerifyJson.GeneratedViews` | NEW public | after Core | `writeGeneratedViews` |
| `…VerifyJson.VerifyJson` | EXISTING, surface byte-identical | last | `schemaVersion` + 4 entry points; thin composition over the seams |

## Seam contracts (the only NEW public surface)

Each new module's curated `.fsi` exposes the **minimal** entry set its consumer calls;
all token helpers / `rr*` / per-writer plumbing stay absent (⇒ private). Detailed
signatures live in `contracts/`. Invariants the implementation MUST hold:

1. **Existing surfaces frozen.** `Loop.fsi` and `VerifyJson.fsi` are byte-identical to
   their committed form (no line changed). Verified by the two reflective drift tests
   after the one additive re-bless.
2. **Order-preserving composition.** Each entry point appends seam writers to the same
   `Utf8JsonWriter` in the same order as today → byte-identical `verify.json` and every
   projection golden (FR-005, SC-002).
3. **Host-`Model`-free folds.** The three host folds take decomposed domain inputs, not
   `Loop.Model` (the Phase B `baseHeadOf` precedent); a one-line wrapper in `Loop`
   projects `Model` fields into each call. This keeps the fold modules pure and
   independently testable, and keeps `update` the sole owner of `Model` (Principle IV).
4. **No new edges.** No new `ProjectReference`, no new third-party dependency, no new
   cyclic edge; the dependency graph stays acyclic (FR-007). Seam modules reference
   only types the project already references.

## State / behavior transitions

None. `update`'s state machine (`Phase` ladder, `Msg` handling, exit/diagnostic
rollup) is unchanged; the folds are pure functions invoked at the existing call sites.
The GateRunHost decision (US3) produces **only** a documentation artifact (ADR 0003);
no host state model changes.
