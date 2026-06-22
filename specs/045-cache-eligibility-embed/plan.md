# Implementation Plan: Embed Cache-Eligibility Verdicts in route.json and audit.json

**Branch**: `045-cache-eligibility-embed` | **Date**: 2026-06-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/045-cache-eligibility-embed/spec.md`

## Summary

Relax the two deliberate exclusions F020 (`route.json`, FR-014) and F025 (`audit.json`, FR-012) carried — *"no
cache-eligibility verdict on the canonical artifacts"* — and **embed** the F041 per-gate
`CacheEligibilityReport` verdict into both documents, matched per gate by `GateId`, so the cache step F020/F025
anticipated finally appears on the artifacts consumers already read instead of on a separate F042/F044 sidecar.

This row edits **only the two merged pure projections** — `FS.GG.Governance.RouteJson.ofRouteResult` (F020) and
`FS.GG.Governance.AuditJson.ofShipDecision` (F025) — continuing the pure-projection-first rhythm. Each gains a
**second input, an `option`-wrapped F041 `CacheEligibilityReport`** (maintainer-confirmed this session): the new
public signatures are `ofRouteResult: RouteResult -> CacheEligibilityReport option -> string` and
`ofShipDecision: ShipDecision -> CacheEligibilityReport option -> string`. `None` is the *not-evaluated* state
today's `fsgg route` / `fsgg ship` produce (they resolve no freshness inputs yet); `Some report` is the
evaluated state the later host row will supply. One honest contract per module — there is no second function to
forget, and the cache input is now an explicit, mandatory-to-consider parameter.

The embed renders **two additive things** and **changes no existing field**:

1. A **top-level `cacheEligibilityEvaluated` boolean** — the always-present *cache-eligibility section* (FR-012,
   the empty-route edge): `false` for `None`, `true` for `Some _`. It distinguishes *no cache step ran* from
   *an evaluated report with no reusable gate*, and survives the empty-gate-list case where there is no per-gate
   entry to carry the signal.
2. A **per-gate inline `cacheEligibility` verdict object** on each route.json `selectedGates` entry and each
   audit.json **gate** item (`kind:"gate"` only — **finding** items carry none, FR-004), reusing F042's verbatim
   closed vocabulary: `{ kind:"reusable", evidence:<ref> }`, `{ kind:"mustRecompute", cause:{…} }`, or the new
   `{ kind:"notEvaluated" }` for a gate the document lists but the report does not (FR-005) or for the no-report
   case (FR-012). Matching is by the rendered `GateId` string (`gateIdValue`), verbatim, never re-parsed.

The verdict vocabulary, the cause shape (`noPriorEvidence` vs `inputsChanged` with the changed-category tokens
in report order — no-hide), and the opaque-evidence-reference rendering are **F042's, reused verbatim** via the
same public upstream accessors F042 uses (`EvidenceReuse.referenceValue`, `FreshnessKey.categoryToken`,
`Gates.gateIdValue`). The projections stay **pure, total, deterministic, byte-stable**, compute no hash / no
freshness key / no cache decision, resolve nothing, and never dereference the evidence reference (FR-010,
FR-011). Embedding a verdict alters **no** existing field, severity, enforcement, route trace, finding, cost, or
ship verdict — it is additive information only (FR-008, US3); a `reusable` verdict on a base-`Blocking` gate
leaves that gate a blocker.

Both documents **bump their schema version** — `fsgg.route/v1 → fsgg.route/v2`, `fsgg.audit/v1 → fsgg.audit/v2`
(FR-013, decision D6) — so a consumer detects the new contract. The standalone F042 `cache-eligibility.json`
projection and the F044 sidecar are **left untouched** (FR-015, SC-008).

Because the two public signatures change (Tier 1), the row also: updates the two `.fsi` files and the two
`surface/*.surface.txt` baselines (`BLESS_SURFACE=1`); fixes the two host callsites
(`RouteCommand/Loop.fs:248 → ofRouteResult result None`, `ShipCommand/Loop.fs:286 → ofShipDecision decision
None`) and the F028 fixture generator to pass `None`; and re-blesses the F028 `audit.json` golden snapshots
(`BLESS_FIXTURES=1`), whose non-cache content stays byte-identical save the bumped `schemaVersion`, the new
top-level flag, and the per-gate `notEvaluated` marker (SC-004). No committed `route.json` fixtures exist (only
gitignored `.tmp/`), so none are re-blessed there.

The wire deltas this row commits live in [contracts/route-json-document.md](./contracts/route-json-document.md)
and [contracts/audit-json-document.md](./contracts/audit-json-document.md); the new signatures in
[contracts/RouteJson.fsi](./contracts/RouteJson.fsi) and [contracts/AuditJson.fsi](./contracts/AuditJson.fsi);
the shared verdict vocabulary, the `GateId` match, and the duplicate-`GateId` reconciliation in
[data-model.md](./data-model.md); the build/exercise/test/re-bless walkthrough in
[quickstart.md](./quickstart.md); and the nine decisions in [research.md](./research.md).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (repo standard; `Nullable=enable`, `TreatWarningsAsErrors=true`
inherited from `Directory.Build.props`). Two edited libraries — `RouteJson.fsi/fs` and `AuditJson.fsi/fs` — each
a single pure total projection function; no new files in `src/`. The whole surface stays a pure total function:
no MVU ceremony (Principle IV does not apply — these are pure cores, not stateful/I/O workflows).

**Primary Dependencies**: `ProjectReference`s only. Each edited project gains **one** new reference — on
**`FS.GG.Governance.CacheEligibility`** (F041) for the `CacheEligibilityReport` / `CacheEligibilityVerdict` /
`RecomputeCause` types and the `CacheEligibility.entries` accessor. That reference transitively supplies the
three public token accessors the render reuses verbatim — F030 `EvidenceReuse.referenceValue`, F029
`FreshnessKey.categoryToken`, F018 `Gates.gateIdValue` — exactly as F042's `CacheEligibilityJson` already does
(the precedent: it references only `CacheEligibility` and gets the rest transitively). RouteJson keeps its
`Route` (F019) reference; AuditJson keeps its `Ship` (F024) reference. **No new third-party `PackageReference`**:
serialization remains the net10.0 shared-framework `System.Text.Json` (`Utf8JsonWriter`) both projections
already use (FR-014). Test frameworks unchanged (Expecto, Expecto.FsCheck, FsCheck, Microsoft.NET.Test.Sdk,
YoloDev.Expecto.TestSdk on the central feed).

**Storage**: None. Both functions are pure string projections — no file, process, clock, network, or git access
(FR-010). They read no cache store and never dereference the opaque evidence reference (FR-011).

**Testing**: Expecto + FsCheck over the **public** projection surface (Principle V), extending the existing
`FS.GG.Governance.RouteJson.Tests` and `FS.GG.Governance.AuditJson.Tests` three-tier shape with the embed
concerns: (1) **verdict-shape tests** — project a real `RouteResult` / `ShipDecision` with a real
`CacheEligibility.evaluate`-built report covering each verdict (`Reusable`, `MustRecompute NoPriorEvidence`,
`MustRecompute (InputsChanged cats)`) and assert each gate entry/item carries its verdict matched by `GateId`,
evidence/cause verbatim (US1/US2, SC-001/SC-002); (2) **no-hide / not-evaluated tests** — a gate listed in the
document but absent from the report, and the `None` case, render `notEvaluated`, never `reusable`; every
`mustRecompute` names its full cause (US3, SC-005, SC-007); (3) **additivity tests** — every non-cache field is
byte-identical to the pre-embed projection of the same input (compared against the committed/recomputed F020/
F025-only bytes modulo the new section + version), and a finding item carries no verdict (SC-004, FR-008,
FR-004); (4) **determinism tests** — byte-identical across repeated projection and value-equal differently-
ordered upstreams; cache entries follow the document's existing gate order; duplicate-`GateId` reconciliation is
deterministic (US4, SC-003); (5) **totality tests** — empty route / clean empty decision / finding-only route /
`Some (CacheEligibilityReport [])` / `None` all return a document, never throw, always with the section present
(SC-006). All evidence is **real** typed values built through the public F041 `evaluate` and F030/F029 newtypes
— no mocks of the cores. The F028 `EnforcementFixtures.Tests` golden-snapshot suite re-runs against the bumped
projection and is re-blessed.

**Target Platform**: Developer / CI .NET SDK running `dotnet test`. No OS-specific surface.

**Project Type**: Two pure library projections (not a host/edge, not an MVU workflow). The change is a signature
extension + an additive render, layered in the existing projection projects (constitution: serialization layers
on the pure core; these projects ARE that serialization layer).

**Performance Goals**: N/A. The contract is determinism, byte-stability, no-hide attribution, and additivity —
not latency. The embed adds one `Map` build over the report entries and one `Map` lookup per document gate.

**Constraints**: Deterministic / byte-stable (FR-007, SC-003): identical inputs ⇒ byte-identical text;
value-equal inputs from differently-ordered upstreams ⇒ identical text; cache entries follow the document's
existing gate order (route.json `GateId` ordinal; audit.json `ShipDecision` composite order), with the
duplicate-`GateId` reconciliation a deterministic total rule (first-by-report-order, D4). Pure / total (FR-010):
no I/O, never throws for well-typed inputs; `None`, empty report, empty route, and clean empty decision are all
valid successes with the section present. Additive / no-hide (FR-008, FR-009): no existing field changes (modulo
the section + version bump); every `mustRecompute` names its full cause; an unevaluated gate is `notEvaluated`,
never silently `reusable`. No derivation (FR-011): no freshness key, hash, or cache decision computed; no raw
freshness input rendered; the evidence reference echoed verbatim, never dereferenced. Standalone preserved
(FR-015): F042/F044 untouched.

**Scale/Scope**: Edits to four files (`RouteJson.fsi/fs`, `AuditJson.fsi/fs`); two `.fsproj` reference
additions; two surface baselines re-blessed; two host callsites + the RouteCommand/ShipCommand
**test** callsites + the F028 fixture generator updated to pass `None` (the command tests recompute their
expected route.json/audit.json live, so this is a compile-fix, not a golden re-bless); seven F028
`audit.json` golden snapshots re-blessed; the two test projects extended; a short
`scripts/prelude.fsx` FSI section (design-first proof, Principle I); the `CLAUDE.md` plan pointer. Zero new
projects, zero new third-party dependencies, zero edits to F041/F042/F044 cores or baselines.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | The two extended public signatures are drafted in `RouteJson.fsi` / `AuditJson.fsi` and exercised in a new `scripts/prelude.fsx` F045 section (projecting a real report through both) before any `.fs` body changes; semantic tests call the public `ofRouteResult` / `ofShipDecision`, never private writers. |
| II. Visibility in `.fsi` | **PASS** | The two `.fsi` files remain the sole public-surface declaration; the new per-gate verdict/cause/section writers live only in the `.fs`, hidden by absence (the F042 `writeVerdict`/`writeCause` precedent). Both `surface/*.surface.txt` baselines are updated for the new parameter and guarded by the existing `SurfaceDrift` tests with the `BLESS_SURFACE=1` path. |
| III. Idiomatic Simplicity | **PASS** | A `Map<string, CacheEligibilityVerdict>` built by a first-wins `List.fold` over `CacheEligibility.entries`, then a per-gate `Map.tryFind` rendering `reusable`/`mustRecompute`/`notEvaluated`; exhaustive wildcard-free `match`es over the closed `CacheEligibilityVerdict` / `RecomputeCause` (reusing F042's exact render). The `option` second parameter is matched once for the top-level flag. No SRTP, reflection (outside the surface test), custom operators, type providers, or non-trivial CEs. |
| IV. Elmish/MVU is the boundary for stateful/I/O | **N/A (PASS)** | Both functions are PURE TOTAL projections with no state, I/O, retries, or workflow — Principle IV explicitly exempts "simple pure functions … a single rule evaluation, an explanation formatter." No MVU is introduced or warranted. |
| V. Test Evidence Is Mandatory | **PASS** | Real `RouteResult` / `ShipDecision`, real F041 `CacheEligibility.evaluate` reports built over real F030 `ReuseStore` / F029 `FreshnessInputs`, real F018 `Gate`s — no mocks of the cores. Tests fail before the render carries the verdict and pass after; the additivity tests fail if any non-cache byte drifts. No synthetic evidence needed (the inputs are pure typed values). |
| VI. Observability & Safe Failure | **N/A (PASS)** | Pure total projections emit no operational events and cannot fail on well-typed input (FR-010); there is no I/O path to degrade. The no-hide guarantee (every `mustRecompute` names its cause; no silent `reusable`) is the projection-level analogue of "no swallowed failure," verified by US3 tests. |
| Change Classification | **Tier 1 (contracted change — alters two public signatures + two committed document contracts)** | Changes `ofRouteResult` / `ofShipDecision` arity, the `route.json` / `audit.json` wire shape (new section + per-gate verdict), and the schema versions (v1→v2) ⇒ full chain: spec, plan, `.fsi`, surface baselines, re-blessed golden baselines, tests, docs. **No new third-party dependency.** F041/F042/F044 and every other merged core consumed verbatim and unedited (SC-008). |
| Engineering Constraints | **PASS** | F#/.NET `net10.0`; one new `ProjectReference` each (F041, sibling governance project) and no new third-party `PackageReference`; `.fsi` curated per module; surface baselines updated; compatibility/migration captured in the contracts (v1→v2, the `None` not-evaluated path). Genericity preserved — no rendering package IDs/paths/templates assumed. Pack output unaffected. |

**Gate result: PASS — no unjustified violations. Complexity Tracking is empty.** The one structural choice (an
`option` second parameter rather than a sibling function) is the maintainer-confirmed single-contract shape and
adds no complexity beyond a `match`. All from-the-spec invariants (no-hide, additivity, determinism, purity,
F042/F044 untouched) map to concrete render rules and tests.

## Project Structure

### Documentation (this feature)

```text
specs/045-cache-eligibility-embed/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 — decisions D1–D9 (option-param single contract; one new F041 ProjectReference
│                        #            each + transitive token accessors; top-level cacheEligibilityEvaluated flag +
│                        #            per-gate inline verdict object reusing F042 vocab verbatim; GateId-string
│                        #            match; first-by-report-order duplicate reconciliation; schemaVersion v1→v2;
│                        #            re-bless scope = surface baselines + F028 audit snapshots + 2 callsites;
│                        #            additivity-by-construction; F042/F044 untouched)
├── data-model.md        # Phase 1 — the embed vocabulary (the option input, the verdict/cause/notEvaluated render
│                        #            shapes, the GateId match + duplicate reconciliation), the two enriched
│                        #            documents field-by-field, the reused F041/F042 cores, the laws
├── quickstart.md        # Phase 1 — build, FSI-exercise both projections with/without a report, test, re-bless the
│                        #            surface baselines + F028 snapshots, confirm byte-stability and additivity
├── contracts/           # Phase 1 — the contracts this row commits
│   ├── RouteJson.fsi               # the new ofRouteResult signature (RouteResult -> CacheEligibilityReport option -> string)
│   ├── AuditJson.fsi               # the new ofShipDecision signature (ShipDecision -> CacheEligibilityReport option -> string)
│   ├── route-json-document.md      # the route.json v2 wire delta (top-level flag + per-selected-gate verdict) + the shared verdict vocabulary
│   └── audit-json-document.md      # the audit.json v2 wire delta (top-level flag + per-gate-item verdict; findings carry none)
└── tasks.md             # Phase 2 — /speckit-tasks output (NOT created here)
```

### Source Code / deliverable layout (repository root)

```text
src/FS.GG.Governance.RouteJson/
├── RouteJson.fsi                                       # EDIT — ofRouteResult: RouteResult -> CacheEligibilityReport option -> string; schemaVersion -> "fsgg.route/v2"; doc the embed
├── RouteJson.fs                                        # EDIT — new option param; build the first-wins verdict Map; emit top-level cacheEligibilityEvaluated + per-selected-gate cacheEligibility verdict (reuse F042 render); no other field changes
└── FS.GG.Governance.RouteJson.fsproj                   # EDIT — add ProjectReference on ../FS.GG.Governance.CacheEligibility (F041); no third-party package

src/FS.GG.Governance.AuditJson/
├── AuditJson.fsi                                       # EDIT — ofShipDecision: ShipDecision -> CacheEligibilityReport option -> string; schemaVersion -> "fsgg.audit/v2"; doc the embed
├── AuditJson.fs                                        # EDIT — new option param; same verdict Map; emit top-level flag + per-GATE-item cacheEligibility (finding items carry none); no other field changes
└── FS.GG.Governance.AuditJson.fsproj                   # EDIT — add ProjectReference on ../FS.GG.Governance.CacheEligibility (F041); no third-party package

src/FS.GG.Governance.RouteCommand/Loop.fs               # EDIT — line ~248: RouteJson.ofRouteResult result None (behavior preserved; adds the not-evaluated section + v2)
src/FS.GG.Governance.ShipCommand/Loop.fs                # EDIT — line ~286: AuditJson.ofShipDecision decision None (behavior preserved; adds the not-evaluated section + v2)

tests/FS.GG.Governance.RouteJson.Tests/                 # EDIT — extend with CacheEmbedTests (verdict shapes, notEvaluated, additivity, finding-free, determinism, totality); pass None where pre-embed
tests/FS.GG.Governance.AuditJson.Tests/                 # EDIT — same embed coverage; gate items carry verdict, finding items none (SC-002)
tests/FS.GG.Governance.RouteCommand.Tests/                      # EDIT — pass None at every ofRouteResult callsite (Support.fs:259 projectExpected → EndToEndTests/InterpreterTests; LoopTests.fs:50); compile-fix, expected route.json recomputed live
tests/FS.GG.Governance.ShipCommand.Tests/                       # EDIT — pass None at every ofShipDecision callsite (LoopTests.fs:50; any EndToEnd/Support expected); compile-fix, expected audit.json recomputed live
tests/FS.GG.Governance.EnforcementFixtures.Tests/Generator.fs   # EDIT — ofShipDecision (rollup …) None (the snapshots are projected with no report)
fixtures/enforcement/audit-snapshots/*.audit.json       # RE-BLESS — 7 golden snapshots gain schemaVersion v2 + cacheEligibilityEvaluated:false + per-gate notEvaluated; non-cache content byte-identical (BLESS_FIXTURES=1)

surface/FS.GG.Governance.RouteJson.surface.txt          # RE-BLESS — ofRouteResult now (RouteResult, FSharpOption<CacheEligibilityReport>) (BLESS_SURFACE=1)
surface/FS.GG.Governance.AuditJson.surface.txt          # RE-BLESS — ofShipDecision now (ShipDecision, FSharpOption<CacheEligibilityReport>) (BLESS_SURFACE=1)
scripts/prelude.fsx                                     # EDIT — append a short F045 FSI section (project both with Some report and None)
CLAUDE.md                                               # EDIT — point the SPECKIT plan reference at this plan
```

**Structure Decision**: No new project. The embed lives in the two existing projection projects, each gaining a
single `ProjectReference` on F041 `CacheEligibility` (the F042 precedent for getting `CacheEligibilityReport` +
the transitive token accessors with no third-party package) and an extended pure signature. The cache verdict
is rendered **inline per gate** (each `selectedGates` entry / each `kind:"gate"` audit item) — the spec's
"attaches to each selected-gate entry / each gate item" — plus a **top-level `cacheEligibilityEvaluated`
boolean** that is the always-present *cache-eligibility section* distinguishing the `None` not-evaluated state
from an evaluated report (load-bearing for the empty-gate-list edge). The verdict/cause render reuses F042's
exact tokens via the same public upstream accessors, so the two sidecar shapes (F042 `cache-eligibility.json`
and the embedded section) speak the same vocabulary, and F042/F044 stay untouched (FR-015).

## Complexity Tracking

> No Constitution Check violations. This section is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
