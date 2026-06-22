# Phase 0 Research: Embed Cache-Eligibility Verdicts in route.json and audit.json

All NEEDS CLARIFICATION from the Technical Context are resolved below. Each decision records what was chosen,
why, and the alternatives rejected. The spec deferred several shapes explicitly to plan time ("a plan
decision"); those are D1, D3, D4, and D6.

## D1 — How the projections accept the optional `CacheEligibilityReport`

**Decision**: Extend each existing function with a **second `CacheEligibilityReport option` parameter** — one
contract per module:

- `RouteJson.ofRouteResult: RouteResult -> CacheEligibilityReport option -> string`
- `AuditJson.ofShipDecision: ShipDecision -> CacheEligibilityReport option -> string`

`None` is the *not-evaluated* state (today's `fsgg route` / `fsgg ship`, which resolve no freshness inputs);
`Some report` is the evaluated state the later host row supplies.

**Rationale**: Maintainer-confirmed this session. One honest contract — the cache input is an explicit,
mandatory-to-consider parameter that a caller cannot forget, and there is no ambiguity about which function to
call. `option` cleanly distinguishes *no cache step ran* (`None`) from *an evaluated report* (`Some _`),
including `Some (CacheEligibilityReport [])` — an evaluated-but-empty report, which is **not** the same as
`None` (D3 carries this distinction into the wire via the top-level flag).

**Alternatives rejected**: (a) An **additive sibling** `ofRouteResultWithCache` keeping the old arity — zero
host-callsite churn, but two functions on the surface, the cache input becomes forgettable, and the old function
would still have to emit the not-evaluated section (so it is not truly "unchanged"). (b) A **required
non-`option` `CacheEligibilityReport`** with callers passing `CacheEligibilityReport []` for "no report" —
loses the `None` vs evaluated-empty distinction the spec wants (FR-012 "distinct from an evaluated
must-recompute"), and forces every gate to `notEvaluated` with no top-level signal of whether a step ran.

## D2 — Project references and the reused token accessors

**Decision**: Each edited project (`RouteJson`, `AuditJson`) gains **exactly one** new
`ProjectReference` — on `FS.GG.Governance.CacheEligibility` (F041). No new third-party `PackageReference`.

**Rationale**: F041 supplies the `CacheEligibilityReport` / `CacheEligibilityVerdict` / `CacheEligibilityEntry`
/ `RecomputeCause` types and the `CacheEligibility.entries` accessor. It transitively brings the three public
token accessors the render reuses **verbatim** — F030 `EvidenceReuse.referenceValue` (the opaque evidence
reference), F029 `FreshnessKey.categoryToken` (the changed-category tokens), F018 `Gates.gateIdValue` (already
used by both projections) — exactly as F042's `CacheEligibilityJson.fsproj` references only `CacheEligibility`
and gets the rest transitively. Serialization stays the net10.0 shared-framework `System.Text.Json`
(`Utf8JsonWriter`) both projections already use, so the libraries remain `System.*`/`FSharp.Core`-only (FR-014).
`RouteJson` keeps `Route` (F019); `AuditJson` keeps `Ship` (F024); the dependency direction stays one-way.

**Alternatives rejected**: Referencing F042 `CacheEligibilityJson` to reuse its `writeVerdict`/`writeCause` —
rejected: those writers are private (absent from F042's `.fsi`, Principle II) and unreachable across the
assembly boundary; the render logic is small and is re-expressed locally over the same public token accessors,
the GatesJson/AuditJson precedent for re-declaring local token helpers.

## D3 — Wire shape of the embed (where the verdict goes)

**Decision**: Two additive renderings, **changing no existing field**:

1. **Top-level `cacheEligibilityEvaluated` boolean** — the always-present *cache-eligibility section* (FR-012).
   `false` for `None`; `true` for `Some _`. Appended after the existing top-level fields
   (route.json: after `cost`; audit.json: after `passing`).
2. **Per-gate inline `cacheEligibility` verdict object** — appended after the existing fields of each route.json
   `selectedGates` entry and each audit.json **gate** item (`kind:"gate"` only; **finding** items get nothing —
   FR-004). The object reuses F042's verbatim vocabulary plus one new case:
   - `{ "kind":"reusable", "evidence": "<referenceValue ref>" }`
   - `{ "kind":"mustRecompute", "cause": { "kind":"noPriorEvidence" } }`
   - `{ "kind":"mustRecompute", "cause": { "kind":"inputsChanged", "categories": ["<categoryToken>", …] } }`
   - `{ "kind":"notEvaluated" }` — the gate is listed but the report has no entry for it (FR-005), or `None`
     was supplied (FR-012). **Never** `reusable` without a matching report entry.

**Rationale**: The spec frames the verdict two ways that must be reconciled: FR-001/FR-002/US1/US2 say the
verdict "attaches to each selected-gate entry / each gate item" (⇒ per-gate inline), while FR-012 and the
empty-route edge say the document carries "a present cache-eligibility section marked not evaluated" (⇒ a
top-level marker that survives an empty gate list). The two-part design satisfies both: per-gate inline is where
consumers read verdicts; the top-level boolean is the section-level signal that survives zero gates and
distinguishes `None` from an evaluated-empty report. The verdict object reuses F042's exact tokens so the
embedded section and the F042 sidecar are byte-for-byte the same vocabulary. `noPriorEvidence` (no `categories`
field) stays distinct from `inputsChanged` with `categories: []` (F042's FR-006), and `notEvaluated` is a third
sibling distinct from both `reusable` and `mustRecompute`.

**Field placement**: appended at the end of each object (top-level and per-gate). This keeps the pre-embed
fields contiguous, makes the additive diff legible, and is fully deterministic. Insertion order is fixed by the
writer, so byte-stability is unaffected by placement.

**Alternatives rejected**: (a) A **single top-level `cacheEligibility` array** keyed by gate id, parallel to
`selectedGates` — rejected: forces consumers to correlate by id the very thing the embed exists to co-locate
(US1's reason to exist). (b) **Only** per-gate inline with no top-level flag — rejected: an empty route / clean
empty decision then carries no cache-eligibility signal at all, violating the empty-route edge and FR-012's
"present … section." (c) Rendering raw freshness inputs or the resolved key alongside the verdict — forbidden
(FR-011, SC-007).

## D4 — Matching gates to verdicts; duplicate-`GateId` reconciliation

**Decision**: Build a `Map<string, CacheEligibilityVerdict>` from `CacheEligibility.entries report`, keyed by
`gateIdValue entry.Gate`, with a **first-by-report-order-wins** fold on duplicate keys. Then, per document gate,
render `Map.tryFind (gateIdValue gateId) m`: `Some v` ⇒ that verdict; `None` ⇒ `notEvaluated`. Document gates
are walked in their existing order (route.json `GateId` ordinal; audit.json `ShipDecision` composite order),
re-sorting nothing.

**Rationale**: Matching is by the **rendered `GateId` string** (`gateIdValue`), verbatim, never re-parsed —
consistent with how both projections already render gate ids and with the spec's "matched by `GateId`,
rendered verbatim." The report may carry duplicate `GateId`s (F041 keeps duplicate candidate gates) while the
document lists each gate once; F041's report is already in `GateId`-ordinal order with a structural duplicate
tiebreak, so **first-occurrence-wins** over that already-deterministic order is itself deterministic and total
(FR-007). A `List.fold` adding a key only if absent realizes first-wins. A report entry whose `GateId` matches
no document gate is simply never looked up — the projection renders only the gates the document already lists,
inventing none (FR-006).

**Alternatives rejected**: last-wins (equally deterministic but less intuitive than "the first verdict the
report attributes to this gate"); erroring on duplicates (violates totality, FR-010); rendering all duplicate
verdicts on the one gate entry (the document lists each gate once — would invent structure).

## D5 — Where the verdict attaches in each document

**Decision**: route.json — on each `selectedGates` entry only (the `findings` array is untouched, FR-004).
audit.json — on each `kind:"gate"` item in any of `blockers`/`warnings`/`passing` (the `kind:"finding"` items
are untouched, FR-004). The audit render branches on the existing `EnforcedItemId` match (`GateItem g` vs
`FindingItem _`): only the `GateItem` arm appends the `cacheEligibility` field.

**Rationale**: Cache eligibility is gate-scoped (keyed on a gate's freshness inputs); findings carry no
freshness key, so no verdict (FR-004, SC-002). The audit projection already discriminates gate vs finding items
in `writeItem`, so the gate-only attachment is a one-arm addition — no new partition, no re-classification
(FR-008).

## D6 — Schema-version strategy

**Decision**: Bump both: `RouteJson.schemaVersion = "fsgg.route/v2"`, `AuditJson.schemaVersion =
"fsgg.audit/v2"`. F042 `CacheEligibilityJson.schemaVersion` stays `"fsgg.cache-eligibility/v1"` (untouched).

**Rationale**: FR-013 requires an observable signal that the contract changed; a major-version bump in the
existing `fsgg.<doc>/vN` token is the simplest unmistakable signal, and the document shape genuinely changed
(new section + per-gate field), so v2 is correct rather than an additive sub-version. Keeping F042 at v1 honours
FR-015/SC-008 (the standalone sidecar is unchanged).

**Alternatives rejected**: an additive `cacheEligibilitySchema` sub-version field while keeping the doc at v1
(more fields, less obvious to a consumer than the version they already branch on); leaving the version unchanged
(violates FR-013).

## D7 — Re-bless / caller-update scope

**Decision**: The signature change ripples to a fixed, enumerated set:

- **Surface baselines** — `surface/FS.GG.Governance.RouteJson.surface.txt` and
  `surface/FS.GG.Governance.AuditJson.surface.txt` change (the methods now take a second
  `FSharpOption<CacheEligibilityReport>` parameter). Re-blessed with `BLESS_SURFACE=1 dotnet test` against each
  project's existing `SurfaceDriftTests`.
- **Host callsites** — `RouteCommand/Loop.fs:248` → `RouteJson.ofRouteResult result None`;
  `ShipCommand/Loop.fs:286` → `AuditJson.ofShipDecision decision None`. Behavior is preserved; the emitted
  documents gain the not-evaluated section + the v2 version, exactly as the spec anticipates ("lets the existing
  host commands emit honest, valid documents until the host row supplies a real report").
- **F028 golden snapshots** — the 7 `fixtures/enforcement/audit-snapshots/*.audit.json` are
  `ofShipDecision (rollup …)` byte snapshots; the `EnforcementFixtures.Tests/Generator.fs` callsite passes
  `None`, and the snapshots are re-blessed with `BLESS_FIXTURES=1 dotnet test
  tests/FS.GG.Governance.EnforcementFixtures.Tests`. Their non-cache content stays byte-identical save
  `schemaVersion: "fsgg.audit/v2"`, the new top-level `cacheEligibilityEvaluated: false`, and a per-gate-item
  `cacheEligibility: { kind:"notEvaluated" }`.
- **No committed `route.json` fixtures** exist (only gitignored `.tmp/`), so none are re-blessed there.
- **RouteCommand/ShipCommand end-to-end tests** that byte-assert the emitted `route.json`/`audit.json` are
  re-blessed/updated to the v2 + not-evaluated shape during implementation (verified per-test in tasks).

**Rationale**: Tier 1 explicitly requires updating `.fsi`, surface baselines, and re-blessing dependent golden
baselines (constitution Change Classification). The diff is mechanical and the additivity guarantee (SC-004) is
the acceptance check on every re-blessed file: the diff must touch only the cache section + version.

## D8 — Additivity by construction (no existing field changes)

**Decision**: The implementation appends the new fields and changes no existing `WriteString`/`WriteNumber`/
section call. The additivity tests (SC-004) compare each document's non-cache fields against the pre-embed F020/
F025-only bytes (recomputed by stripping the cache section + reverting the version token, or asserted
field-by-field via `JsonDocument`).

**Rationale**: FR-008 forbids any change to existing fields, severity, enforcement, route trace, finding, cost,
or ship verdict. Because the embed only adds writer calls (a top-level flag at the end; a per-gate field at the
end of each gate object) and never touches the existing writers, additivity holds by construction; the tests
guard against accidental drift (e.g. a reordered field).

## D9 — Purity, totality, determinism preserved

**Decision**: Both functions stay pure and total: the only new logic is a `Map` build (a `List.fold` over
`CacheEligibility.entries`) and a per-gate `Map.tryFind`, both total; the `option` is matched once. No I/O, no
clock, no git, no store read, no dereference of the evidence reference. `None`, `Some (CacheEligibilityReport
[])`, empty route, clean empty decision, and finding-only route all return a valid document and never throw
(FR-010, SC-006). Determinism is unchanged: the `Map`/lookup adds no ordering decision beyond the document's
already-fixed gate order, and the first-wins reconciliation is deterministic over F041's already-ordered report
(D4), so value-equal inputs from differently-ordered upstreams project identically (SC-003).

**Rationale**: This preserves every property F020/F025/F042 already guarantee; the embed adds information, not
behavior. No MVU boundary is introduced — Principle IV exempts pure projection functions.
