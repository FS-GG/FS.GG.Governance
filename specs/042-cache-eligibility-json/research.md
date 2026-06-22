# Phase 0 Research — Deterministic cache-eligibility.json Projection (F042)

All Technical Context items are resolved; there are **no open NEEDS CLARIFICATION**. The spec defers a
small set of shapes to planning (Assumptions: the home library and serialization mechanism; FR-004/FR-006
cause rendering; FR-011 token vocabularies; the document field order and exclusions); each is decided
below. Format per decision: **Decision / Rationale / Alternatives considered**.

---

## D1 — One new sibling projection library (the established emission-row rhythm)

**Decision**: Deliver a single new packable library **`FS.GG.Governance.CacheEligibilityJson`**, compiled
`CacheEligibilityJson.fsi → CacheEligibilityJson.fs`, rather than embedding the verdict into an existing
projection. The operations module is `CacheEligibilityJson`; the projection is named **`ofReport`** (mirrors
F025 `AuditJson.ofShipDecision` / F020 `RouteJson`'s `of…`), plus a `schemaVersion` constant.

**Rationale**: F020 `route.json`, F021 `gates.json`, and F025 `audit.json` each landed as a **separate
sibling projection library** that renders one already-typed core value into its deterministic document
before any host wiring consumed it. This row is the projection of F041's `CacheEligibilityReport` and is the
direct analogue of `AuditJson.ofShipDecision`. A new minimal library keeps the addition isolated and
additive and keeps the merged projection baselines untouched. `ofReport` reads as "render this report",
matching the sibling naming.

**Alternatives considered**: *Embed the verdict into `RouteJson` (F020) / `AuditJson` (F025).* Rejected —
the spec (Assumptions) and `docs/initial-implementation-plan.md` keep *embedding* the cache-eligibility
verdict into route.json / audit.json (and the host wiring that resolves each selected gate's full
`FreshnessInputs` — F019's route currently carries only the F018 `Gate.FreshnessKey`, not the F029 inputs
F030/F041 consume) as a **later integration row**. Rendering the standalone per-change document first, as a
sibling projection, mirrors how F020 deferred fields its upstream value did not yet carry. *Fold the
projection into `CacheEligibility` (F041).* Rejected — F041 is the pure decision core and deliberately
"serializes nothing" (spec Overview); per the repo's pure-core-first rhythm the decision value lands first,
the projection consumes it later in a separate library (the constitution: heavier capabilities layer on top,
not into the core).

---

## D2 — A single `ProjectReference` to `CacheEligibility` (F041); the rest transitive

**Decision**: `FS.GG.Governance.CacheEligibilityJson` references **only**
`FS.GG.Governance.CacheEligibility` (F041). The token accessors it needs — F018 `Gates.gateIdValue`, F030
`EvidenceReuse.referenceValue`, F029 `FreshnessKey.Model.categoryToken` — and the `RecomputeCause` /
`EvidenceRef` / `InputCategory` types arrive **transitively** through F041 (which references `EvidenceReuse`
+ `Gates`, and gets `FreshnessKey` + `Config` transitively).

**Rationale**: This is exactly the F025 precedent — `AuditJson.fsproj` references **only**
`FS.GG.Governance.Ship`, yet `AuditJson.fs` opens `Config.Model`, `Gates.Model`, `Findings.Model`,
`Enforcement.Enforcement`, and `Ship.Model`, all of which arrive transitively (no
`DisableTransitiveProjectReferences` in the repo). One reference keeps the dependency direction one-way
(`CacheEligibilityJson → CacheEligibility → EvidenceReuse/Gates/FreshnessKey/Config`) and the scope guard
(D6) crisp. The test project additionally references `EvidenceReuse` / `Gates` / `FreshnessKey` directly so
it can *build* the upstream report with real tokens.

**Alternatives considered**: *Reference `EvidenceReuse` / `Gates` / `FreshnessKey` directly from the
library.* Rejected — redundant; they arrive transitively and the library uses only their public accessors.
Adding direct references would broaden the declared dependency set the SurfaceDrift scope guard pins,
without need.

---

## D3 — Serialization mechanism: shared-framework `System.Text.Json`, hand-driven writer, no new package

**Decision**: Render with a hand-driven `System.Text.Json` `Utf8JsonWriter` walk over the report — the same
`writeToString` (default options ⇒ compact, non-indented UTF-8) helper shape F025 `AuditJson.fs` uses. **No
new `PackageReference`** (FR-014).

**Rationale**: `System.Text.Json` is in the net10.0 shared framework; the kernel's `Json.fs` and the
F020/F021/F025 projections already emit through `Utf8JsonWriter`, so the library stays `System.*` /
`FSharp.Core`-only. A hand-driven writer gives byte-exact control over field order and tokens (FR-007) and
keeps the projection a single linear walk (Principle III). Default writer options produce compact output,
which is deterministic and diff-stable.

**Alternatives considered**: *A reflection/`JsonSerializer`-based DU serializer.* Rejected — DU shapes do
not map to the wire contract's tagged objects without attributes/converters, it risks non-deterministic
member order, and it pulls reflection into a pure renderer (Principle III justification burden). *A
hand-rolled string writer.* Rejected — re-implements JSON escaping the writer already does correctly
(FR-012: free-strings escaped by the writer, never manually); the spec (Assumptions) names the
net10.0 serializer as the assumed mechanism.

---

## D4 — Verdict and cause rendered as `kind`-tagged objects (closed, branchable, exhaustive)

**Decision**: Render each entry as `{ "gate": <gateIdValue>, "verdict": <verdict-object> }`. The verdict is
a **tagged object** discriminated by `kind`:

- `Reusable ref` → `{ "kind": "reusable", "evidence": <referenceValue ref> }`
- `MustRecompute cause` → `{ "kind": "mustRecompute", "cause": <cause-object> }`

The cause is itself a tagged object discriminated by `kind`:

- `NoPriorEvidence` → `{ "kind": "noPriorEvidence" }`
- `InputsChanged cats` → `{ "kind": "inputsChanged", "categories": [ <categoryToken c> … ] }`

Each `match` is **exhaustive over the closed DU with no wildcard** (the F025 closed-enum-token precedent),
so a future F041 verdict or cause case is a compile error here, never a silently mis-tokened field.

**Rationale**: The tagged-object shape is the F025 `audit.json` item precedent (`kind:"gate"` /
`kind:"finding"`) — it lets CI / cost views / agents branch on `kind` without string-scraping free text
(FR-011) and makes the closed verdict/cause vocabularies (`reusable`/`mustRecompute`,
`noPriorEvidence`/`inputsChanged`) explicit. Crucially, a tagged cause keeps **`noPriorEvidence`
distinguishable from `inputsChanged []`** (FR-006, SC-005): the former has no `categories` field, the
latter a present empty array — they can never collapse to one another. A `reusable` verdict carries **only**
its `evidence` reference — no skip action, severity, or enforcement meaning (FR-003,
necessary-not-sufficient). The reference is rendered through `referenceValue` verbatim as an opaque string,
never parsed or dereferenced (FR-003/FR-010).

**Alternatives considered**: *A flat verdict string + parallel optional `evidence` / `cause` fields.*
Rejected — invites a consumer to read `evidence` on a `mustRecompute` entry; the tagged object makes the
mutually-exclusive payloads structural. *Rendering the cause as a bare string (`"noPriorEvidence"` vs a
joined category list).* Rejected — a joined string re-introduces string-scraping and cannot represent
`inputsChanged []` distinctly from `noPriorEvidence`.

---

## D5 — Collection + field order: the report's order preserved verbatim; fixed field sequence

**Decision**: The top-level object field order is fixed `schemaVersion`, `entries`. The `entries` array is
**always present** and walks the report's entry list **in its existing order**, re-sorting nothing — the
F041 report already fixed the `GateId`-ordinal order with its structural duplicate tiebreak. Within each
entry, fields are in the fixed order `gate`, `verdict`; within a verdict, `kind` first then its payload;
within a cause, `kind` first then `categories`. The `categories` array preserves the report's
`InputCategory` order (F041 carried F030's `diff` order verbatim).

**Rationale**: Determinism is the contract (US2). Field order is exactly the writer's call order and is part
of the wire contract (FR-007). The projection adds **no ordering decision of its own** beyond the fixed
field sequence — so two reports equal as values but assembled from differently-ordered candidate inputs
project identically (SC-003), because F041 already normalized the order. An empty report renders as a
present, empty `entries` array (FR-009) — never omitted, never a "must recompute by default" placeholder.

**Alternatives considered**: *Re-sort entries in the projection.* Rejected — the report is already ordered;
re-sorting would duplicate F041's responsibility and risk diverging from the report's documented order
(FR-005 requires the report's order *preserved verbatim*). *Sort the `categories`.* Rejected — they carry
F030's `diff` order, which the report preserved; re-sorting would lose "in the report's order" (FR-004).

---

## D6 — Surface: `schemaVersion` + `ofReport`; everything else hidden; scope-guarded

**Decision**: The `.fsi` exposes exactly two members — `val schemaVersion: string` and `val ofReport:
report: CacheEligibilityReport -> string`. Every writer helper (`writeToString`, the verdict/cause/category
token + sub-object writers) lives only in the `.fs`, hidden by absence from the `.fsi`. A reflective
`SurfaceDrift` test pins the rendered surface to
`surface/FS.GG.Governance.CacheEligibilityJson.surface.txt` and asserts the assembly references only
`CacheEligibility` (+ its allowed transitive cores) and `FSharp.Core` / BCL.

**Rationale**: Mirrors F025 `AuditJson` exactly (`schemaVersion` + `ofShipDecision`, all plumbing hidden).
Two members are the minimal surface US1/US2 need; the SurfaceDrift + scope guard is the F020–F041 Principle
II precedent, with the `BLESS_SURFACE=1` intentional-rebless path.

**Alternatives considered**: *Expose a `verdictToken` / `causeToken` helper for reuse.* Rejected —
speculative surface; no caller needs the tokens independently of the document, and exposing them would
broaden the baseline. *Expose an `ofVerdict` for a single entry.* Rejected — the document is the unit of the
contract; a per-verdict renderer is not a stated need (YAGNI, Principle III).

---

## D7 — Exclusions: only declared ids, the closed vocabularies, the named categories, and the opaque reference

**Decision**: The document contains **only** `schemaVersion`, the `entries` array, each entry's `gate`
(declared `GateId` string verbatim), its `verdict` (closed `reusable`/`mustRecompute` token), the `evidence`
reference (opaque string verbatim), the `cause` (closed `noPriorEvidence`/`inputsChanged` token), and the
`categories` (the `categoryToken` vocabulary). It carries **no** wall-clock timestamp, host/absolute path,
raw freshness input, computed freshness key or hash, environment value, numeric process exit code, severity,
ship verdict, exit-code basis, or provenance/attestation reference (FR-012, SC-007).

**Rationale**: These are exactly the fields the F041 `CacheEligibilityReport` carries — the projection
restricts itself to its upstream value (the F020/F025 "render only what the value typed" discipline). The
report itself carries no clock, path, raw inputs, key, severity, or verdict, so the projection has nothing
excluded to leak; the exclusions test pins this against the worst-case generated report.

**Alternatives considered**: *Add a generated-at timestamp or source path for provenance.* Rejected — both
break determinism (US2) and are explicitly excluded (FR-012); provenance/attestation is a later row's
concern (the F025 precedent deferred it identically).

---

## D8 — Totality: a document for every well-typed report; never throws

**Decision**: `ofReport` is **total** — it returns a document string for every `CacheEligibilityReport`,
including the empty report (a present, empty `entries` array + the schema version), the all-reusable report,
the all-must-recompute report, the mixed report, and the duplicate-`GateId` report (two distinct entries
under the same gate id, neither merged nor deduplicated). It never throws.

**Rationale**: Totality lets later rows call the projection unconditionally without error handling (the
F020/F025 totality rationale). The input is an already-validated typed value; the projection re-validates
nothing and has no failure mode of its own. The writer walk is a straight-line emit with no partial
function, division, or unchecked access. Totality is the safe-failure stand-in (Constitution Check VI N/A).

**Alternatives considered**: *Return a `Result`/`option` to signal a "degenerate" report.* Rejected — there
is no degenerate well-typed report; the empty report is a valid success, not an error (FR-009), and an
error channel would force pointless handling on every caller.
