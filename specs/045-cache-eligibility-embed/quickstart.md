# Quickstart: Embed Cache-Eligibility Verdicts in route.json and audit.json (F045)

A validation/run guide for the embed. It proves both projections render the F041 verdict per gate (with and
without a report), stay additive and byte-stable, and leave F042/F044 untouched. Implementation bodies live in
the `.fs` files and the tests; this guide is how you exercise and verify them.

## Prerequisites

- .NET `net10.0` SDK (repo standard). From the repo root: `dotnet build FS.GG.Governance.sln`.
- The cores are all merged: F019 `Route`, F024 `Ship`, F041 `CacheEligibility` (+ `evaluate`), F030
  `EvidenceReuse`, F029 `FreshnessKey`.

## 1. FSI design-first proof (Principle I) — `scripts/prelude.fsx`

Append an F045 section that loads `RouteJson`, `AuditJson`, and `CacheEligibility`, builds a real report with
`CacheEligibility.evaluate`, and projects both documents twice — once `Some report`, once `None` — then prints
the bytes. Confirm by eye:

- `Some report` ⇒ each selected gate / gate item carries a `reusable` / `mustRecompute` / `notEvaluated`
  verdict; `cacheEligibilityEvaluated: true`.
- `None` ⇒ every gate is `{ kind:"notEvaluated" }`; `cacheEligibilityEvaluated: false`; every other field
  matches the pre-embed output.
- `schemaVersion` reads `fsgg.route/v2` / `fsgg.audit/v2`.
- audit.json finding items carry **no** `cacheEligibility` field.

This must read cleanly in FSI **before** the `.fs` bodies are written.

## 2. Build

```bash
dotnet build src/FS.GG.Governance.RouteJson/FS.GG.Governance.RouteJson.fsproj
dotnet build src/FS.GG.Governance.AuditJson/FS.GG.Governance.AuditJson.fsproj
```

Each `.fsproj` now has a `ProjectReference` on `../FS.GG.Governance.CacheEligibility` (F041); no new third-party
package. The build also recompiles the two host callsites (`RouteCommand/Loop.fs`, `ShipCommand/Loop.fs`), which
now pass `None`.

## 3. Run the semantic tests

```bash
dotnet test tests/FS.GG.Governance.RouteJson.Tests
dotnet test tests/FS.GG.Governance.AuditJson.Tests
```

Coverage to expect (Principle V, real typed values — no mocks of the cores):

| Concern | Spec | What it asserts |
|---|---|---|
| Verdict shapes | US1/US2, SC-001/SC-002 | each gate carries its report verdict matched by `GateId`; evidence/cause verbatim |
| Not-evaluated | US1.4/US3.2, SC-005 | gate absent from report, and `None`, render `notEvaluated`; never `reusable` |
| No-hide cause | US3.1, SC-005 | every `mustRecompute` names its full cause; `inputsChanged` categories in report order |
| Gate-scoped | US2.2, SC-002 | route findings + audit finding items carry no verdict |
| Additivity | US3, SC-004 | every non-cache field byte-identical to the pre-embed projection (modulo section + version) |
| Orphan / duplicate | Edge, FR-006/FR-007 | orphan report entry adds nothing; duplicate `GateId` → first-by-report-order |
| Determinism / order | US4, SC-003 | byte-identical repeats; value-equal differently-ordered upstreams identical; existing gate order |
| Totality | SC-006 | `None`, empty report, empty route, clean empty decision, finding-only route → a document, never throws |

## 4. Re-bless the surface baselines (Tier 1)

The two signatures changed, so the surface baselines drift intentionally:

```bash
BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.RouteJson.Tests
BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.AuditJson.Tests
git diff surface/FS.GG.Governance.RouteJson.surface.txt surface/FS.GG.Governance.AuditJson.surface.txt
```

Expected diff: each `ofRouteResult` / `ofShipDecision` method now takes a second
`FSharpOption<CacheEligibilityReport>` parameter. Review that the diff is **only** that.

## 5. Re-bless the F028 audit golden snapshots

The 7 `fixtures/enforcement/audit-snapshots/*.audit.json` are `ofShipDecision` byte snapshots (projected with
`None` after the generator update):

```bash
BLESS_FIXTURES=1 dotnet test tests/FS.GG.Governance.EnforcementFixtures.Tests
git diff fixtures/enforcement/audit-snapshots/
```

Expected per-file diff: `schemaVersion` → `fsgg.audit/v2`, a trailing top-level `cacheEligibilityEvaluated:
false`, and each gate item gains `cacheEligibility: { kind:"notEvaluated" }` — **nothing else** (the additivity
check, SC-004). If any other byte moved, the embed touched an existing field — fix it, do not bless it.

## 6. Confirm F042/F044 untouched (SC-008, FR-015)

```bash
dotnet test tests/FS.GG.Governance.CacheEligibilityJson.Tests
dotnet test tests/FS.GG.Governance.CacheEligibilityCommand.Tests
git status src/FS.GG.Governance.CacheEligibilityJson src/FS.GG.Governance.CacheEligibilityCommand \
           surface/FS.GG.Governance.CacheEligibilityJson.surface.txt
```

These must pass with **zero edits** to the F042/F044 cores and baselines.

## 7. Whole-suite gate

```bash
dotnet test FS.GG.Governance.sln
```

Green across the board, with `RouteCommand`/`ShipCommand` end-to-end tests re-blessed to the v2 +
not-evaluated shape, confirms the embed is additive, deterministic, and contained.
