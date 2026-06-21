# Phase 1 Data Model: Golden Enforcement Truth-Table Fixtures

This row defines **no new public types**. The "entities" below are the *fixture shapes* the in-test
generator renders — each is a rendering of values the merged cores already produce. Every typed value is
reused verbatim from the merged `.fsi` contracts (no redefinition). The generator's only F# data structures
are small internal records used to assemble rows before rendering; they live in the test assembly and have
no `.fsi`.

## Reused typed values (consumed, never redefined)

| Concept | Source type (merged `.fsi`) | Role here |
|---|---|---|
| Base / effective severity | `Enforcement.Severity` (`Advisory` \| `Blocking`) | dial input + derived output |
| Rule maturity | `Config.Model.Maturity` (`Observe`/`Warn`/`BlockOnPr`/`BlockOnShip`/`BlockOnRelease`) | dial input |
| Run mode | `Enforcement.RunMode` (`Sandbox`/`Inner`/`Focused`/`Verify`/`Gate`/`Release`) | dial input |
| Profile | `Enforcement.Profile` (`Light`/`Standard`/`Strict`/`Release`) | dial input |
| Per-finding decision | `Enforcement.EnforcementDecision` (`EffectiveSeverity`, `Reason`, …) | pinned outcome (D5) |
| Route outcome | `Routing.Model.RoutingResult` (`Routed`/`UnmatchedInRoot`/`OutOfScope`) | route-class section (D6) |
| Finding | `Findings.Model.UnknownGovernedPathFinding` / `FindingZone` / `FindingId` | route-class section (D6) |
| Ship decision | `Ship.Model.ShipDecision` (verdict + 3-way partition) | snapshot source (D7) |
| Audit document | `AuditJson.ofShipDecision : ShipDecision -> string` | snapshot bytes (D7) |

## Entity: Truth-table row (primary cross-product)

One row per element of base severity × maturity × run mode × profile (240 total, SC-001).

| Field | Value | Source |
|---|---|---|
| `base` | the base-severity dial token | the iterated `Severity`, rendered per contract token map |
| `maturity` | the maturity dial token | the iterated `Maturity` |
| `mode` | the run-mode dial token | the iterated `RunMode` |
| `profile` | the profile dial token | the iterated `Profile` |
| `effective` | the derived effective-severity token | `(deriveEffectiveSeverity input).EffectiveSeverity` |
| `reason` | the derived reason text | `(deriveEffectiveSeverity input).Reason` (verbatim, FR-002) |

**Validation / invariants**:
- Exactly one row per combination; no missing, no duplicate (SC-001) — asserted by counting rows against
  `2*5*6*4 = 240` and against a `Set` of the four-tuple keys.
- `Observe`/`Warn` always render `effective = advisory` regardless of mode/profile (FR-007 / Edge), pinned
  because it comes straight from the core.
- A `base = advisory` row always renders `effective = advisory` (Edge: base-advisory never escalates) —
  visible, not assumed.
- Saturated/unreachable combinations (e.g. `block-on-release` under `release`/`release`) are **present**
  with their real outcome, never omitted (Edge: unreachable combinations).
- Row order is the fixed nested-iteration order defined in contracts/truth-table-format.md.

## Entity: Route-class row (separate section)

A small fixed set of rows demonstrating the routine-vs-fenced-vs-unknown dimension (FR-003), each produced
by running the genuine F015/F017 cores over minimal real `TypedFacts`.

| Field | Value | Source |
|---|---|---|
| `class` | `routine` \| `fenced` \| `unknown-governed-path` | the scenario label |
| `example path` | the candidate path used | the real `GovernedPath` fed to `Routing.route` |
| `route outcome` | rendered `RoutingResult` token | `Routing.route facts [path]` |
| `finding` | the finding id token, or `(none)` | `Findings.findUnknownGovernedPaths facts report` |
| `note` | the demonstrated property | "selects nothing / never default-deny", "routes into domain gates", "explicit finding" |

**Validation / invariants**:
- The `routine` row shows `OutOfScope` + `(none)` and a note that it never default-denies — also asserted
  true under `RunMode.Release` + `Profile.Release` (Edge: routine under strictest dials).
- The `fenced` row shows `Routed(domain,…)` + `(none)`.
- The `unknown-governed-path` row shows `UnmatchedInRoot` + an explicit finding id; a protected-surface
  variant shows the escalated `UnknownProtectedBoundaryPath` id.

## Entity: Blocking-altering snapshot scenario

A named scenario where a single dial flips a finding between blocking and non-blocking, seeding one
`audit.json` snapshot (P2). The full named set is fixed in contracts/audit-snapshot-set.md.

| Field | Value |
|---|---|
| `name` | the scenario slug = the snapshot file stem (e.g. `maturity-withholds-observe`) |
| `dial under test` | which single dial flips blocking (maturity / base severity / profile / mode) |
| `route` | the real F019 `RouteResult` assembled with the F025 `Support.fs` builders |
| `mode`, `profile` | the F023 dials passed to `Ship.rollup` |
| `expected partition` | which section (`blockers`/`warnings`/`passing`) the item must land in |
| `snapshot` | `ofShipDecision (rollup route mode profile)` — the committed bytes |

**Validation / invariants**:
- Each snapshot's bytes equal `ofShipDecision (rollup …)` exactly (FR-008), regenerated and compared.
- A relaxed-blocker snapshot shows the finding in the **warnings** section carrying **both** base and
  effective severity plus the reason — the no-hide rule (FR-009, SC-004).
- The scenario set collectively covers **every** dial that can flip blocking (FR-010) — asserted by a
  coverage test over the `dial under test` set.

## Entity: Committed fixture file

| File | Shape | Guard |
|---|---|---|
| `fixtures/enforcement/truth-table.md` | Markdown: primary 240-row table + route-class section | byte-equality drift guard + count check |
| `fixtures/enforcement/audit-snapshots/<name>.audit.json` | one `fsgg.audit/v1` document per scenario | per-file byte-equality snapshot guard |

All files are UTF-8 (no BOM), `\n` newlines, no trailing whitespace. Regeneration with `BLESS_FIXTURES=1`
rewrites them intentionally; any unblessed difference fails the build with a readable diff (FR-006,
SC-003).
