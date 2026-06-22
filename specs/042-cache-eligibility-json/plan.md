# Implementation Plan: Deterministic cache-eligibility.json Projection

**Branch**: `042-cache-eligibility-json` | **Date**: 2026-06-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/042-cache-eligibility-json/spec.md`

## Summary

Land the **projection half** of the route/audit emission row's *"… and cache eligibility …"* line
(Phase 2 / Phase 11 of `docs/initial-implementation-plan.md`): F041 produced the typed
`CacheEligibilityReport` (the evaluated per-gate verdict); this row renders that report into a
deterministic, versioned `cache-eligibility.json` document — the stable, machine-readable per-change
cache-eligibility contract the later route/audit emission rows, CI cost dashboards, agents, and generated
readiness views read instead of an in-memory value.

Continuing this repo's maintainer-confirmed **pure-core-first** rhythm — F020 `route.json`, F021
`gates.json`, and F025 `audit.json` each landed their pure document *value* before any host wiring — this
row delivers a single new packable projection library, **`FS.GG.Governance.CacheEligibilityJson`**, whose
sole public surface is a `schemaVersion` constant and a pure, total `ofReport: CacheEligibilityReport ->
string`. It is the exact analogue of F025 `AuditJson.ofShipDecision`, applied to F041's
`CacheEligibilityReport` instead of F024's `ShipDecision`.

The projection is **emit-only**: it re-derives, re-classifies, re-runs, and re-orders nothing. The report
already fixed the per-gate verdicts and the `GateId`-ordinal collection order (with its structural
duplicate tiebreak); this row walks that already-ordered value and writes it. It re-runs no reuse
decision (FR-002), makes no cache lookup against a real store, computes no freshness key or hash, resolves
none of the freshness inputs, never dereferences the opaque evidence reference (FR-008), maps no numeric
process exit code, invents no provenance reference, and emits no severity, ship verdict, host path,
timestamp, environment value, or product vocabulary (FR-012). It honours F041's two hard rules: every
`mustRecompute` entry names its cause (**no-hide**, FR-004), and a `reusable` entry asserts only "prior
evidence may be reused" (**necessary-not-sufficient**, FR-003).

The capability provides (full vocabulary in [data-model.md](./data-model.md); the wire contract in
[contracts/cache-eligibility-json-document.md](./contracts/cache-eligibility-json-document.md); the
signatures + laws in [contracts/cache-eligibility-json-api.md](./contracts/cache-eligibility-json-api.md)):

- **`CacheEligibilityJson.schemaVersion`** = `"fsgg.cache-eligibility/v1"` — the declared contract version
  stamped into every document (FR-013). A fixed constant, never derived from a clock, environment, or
  input value.
- **`CacheEligibilityJson.ofReport`** = `CacheEligibilityReport -> string` — the pure, total projection:
  one linear `Utf8JsonWriter` walk of the already-ordered F041 report, emitting the top-level object in the
  fixed order `schemaVersion`, `entries`, and each entry in the report's verbatim order.

The library reuses the public upstream accessors verbatim for token rendering — **F018**
`Gates.gateIdValue` (the gate id string), **F030** `EvidenceReuse.referenceValue` (the opaque evidence
reference string), and **F029** `FreshnessKey.Model.categoryToken` (the changed-input-category tokens) —
and matches each closed verdict/cause case exhaustively with no wildcard, so a future F041 verdict or cause
case is a compile error here, never a silently mis-tokened field (the F025 closed-enum-token precedent).
The merged F020 `RouteJson` / F025 `AuditJson` cores and their `surface/*.surface.txt` baselines, and the
F041 `CacheEligibility` core, are **untouched**; the new project + its test project are purely additive
(SC-001…SC-007 add no behavior to existing assemblies).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (repo standard; `Nullable=enable`,
`TreatWarningsAsErrors=true` inherited from `Directory.Build.props`). One new `src/` library with one
curated `.fsi`, plus one new test project.

**Primary Dependencies**: One `ProjectReference` — **`FS.GG.Governance.CacheEligibility`** (F041, for the
`CacheEligibilityReport` / `CacheEligibilityEntry` / `CacheEligibilityVerdict` / `RecomputeCause` it
projects and the `entries` accessor). The transitive pure cores **`EvidenceReuse`** (F030 — supplies
`EvidenceRef` / `referenceValue`), **`Gates`** (F018 — supplies `GateId` / `gateIdValue`), **`FreshnessKey`**
(F029 — supplies `InputCategory` / `categoryToken`), and **`Config`** (F014) arrive transitively through
F041 and need no direct reference (the F025 "references only `Ship`, the rest arrive transitively"
precedent; transitive project references flow to the compiler — no `DisableTransitiveProjectReferences`).
**No new third-party `PackageReference`** (FR-014): serialization is the net10.0 shared-framework
`System.Text.Json` (`Utf8JsonWriter`) the kernel's `Json.fs` and F020/F021/F025's projections already use,
so the library stays `System.*`/`FSharp.Core`-only. Test frameworks already on the central feed
(`Directory.Packages.props`): **Expecto**, **Expecto.FsCheck**, **FsCheck**, **Microsoft.NET.Test.Sdk**,
**YoloDev.Expecto.TestSdk**.

**Storage**: None. No database, no files, no runtime storage — the document is an in-value string result of
the supplied report. The only test-side I/O is the surface-drift baseline read (and its `BLESS_SURFACE=1`
write), the established pattern.

**Testing**: Expecto + FsCheck, exercising the **public** surface (`CacheEligibilityJson.ofReport` /
`schemaVersion`) over real, upstream-assembled `CacheEligibilityReport`s — built by F041
`CacheEligibility.evaluate` over real candidate gates (real F018 `GateId`, real F029 `FreshnessInputs`) and
a real F030 `ReuseStore` assembled via `EvidenceReuse.record` (Principle V — no mock, no clock read, no
hand-built JSON oracle, no real cache lookup). Output is parsed back with `System.Text.Json` `JsonDocument`
to assert structure. Concerns: (1) **projection / carry** — exactly one entry per report entry, each with
its declared gate id and its verdict (reusable + evidence reference, or must-recompute + named cause)
tracing back to the report (US1, SC-001); (2) **determinism + versioned schema** — byte-for-byte identical
output for identical reports, identical output for value-equal reports assembled from differently-ordered
candidate inputs, a present `schemaVersion`, stable field/collection order (US2, SC-002/SC-003); (3)
**no-hide** — every `mustRecompute` entry carries a named cause, `noPriorEvidence` distinguishable from
`inputsChanged []` (US3, SC-005); (4) **totality** — a document for every well-typed report incl. empty,
all-reusable, all-must-recompute, duplicate-`GateId`, never throws (US4, SC-006); (5) **exclusions** — no
timestamp, host path, raw freshness input, freshness key/hash, environment value, numeric exit code,
severity, ship verdict, or provenance reference appears (SC-007); (6) **surface drift + scope hygiene** —
the assembly references only `CacheEligibility` (+ allowed transitive cores) and renders the committed
surface (Principle II, additive-only). Determinism, order-independence, totality, and the no-hide carry are
FsCheck properties; the worked document examples are pinned to
[contracts/cache-eligibility-json-document.md](./contracts/cache-eligibility-json-document.md), plus the FSI
proof.

**Target Platform**: Developer / CI .NET SDK running `dotnet test`. No host, no CLI, no OS-specific
surface.

**Project Type**: A new pure-projection F# library + its test project. No host, no CLI, no MVU.

**Performance Goals**: N/A. The contract is **determinism, totality, a versioned schema, and the no-hide
carry**, not latency; the projection is a single linear writer walk over a handful of already-typed entries
(Spec: *"Determinism … is what makes the artifact a contract"*).

**Constraints**: Pure / total / deterministic (FR-007/FR-008): no file, process, clock, network, or git
access; no cache lookup against a real store; no freshness key or hash computed; none of the freshness
inputs resolved; the opaque evidence reference rendered verbatim but never parsed or dereferenced; never
throws for any well-typed `CacheEligibilityReport`; an empty report is a valid success. Emit-only: it
re-runs no reuse decision and re-orders nothing (FR-002/FR-005); the report's already-fixed `GateId`-ordinal
order (with its structural duplicate tiebreak) is preserved verbatim. Identical reports always yield a
byte-identical document. The merged F020/F025 cores, the F041 core, and their baselines are not modified
(FR-014).

**Scale/Scope**: One new `src/` library (`CacheEligibilityJson` — `CacheEligibilityJson.fsi/fs`); one new
test project; one new surface baseline `surface/FS.GG.Governance.CacheEligibilityJson.surface.txt`; two
solution entries; a short `scripts/prelude.fsx` FSI section (design-first proof, Principle I); the
`CLAUDE.md` plan pointer. Zero changes to existing `src/`, `surface/`, or merged test projects.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | The public surface is drafted as `CacheEligibilityJson.fsi` and exercised in `scripts/prelude.fsx` (a new F042 section) before any `.fs` body exists; semantic tests call the public `ofReport` / `schemaVersion`, never private writer helpers. |
| II. Visibility in `.fsi` | **PASS** | One curated `.fsi` is the sole public-surface declaration; the `.fs` carries no access modifiers, and every writer / closed-enum token helper stays unexposed by its absence from the `.fsi` (the `Kernel.Json` / `RouteJson` / `GatesJson` / `AuditJson` precedent). A new `surface/FS.GG.Governance.CacheEligibilityJson.surface.txt` baseline is added and guarded by a reflective `SurfaceDrift` test (the F020–F041 precedent), with the `BLESS_SURFACE=1` re-bless path. |
| III. Idiomatic Simplicity | **PASS** | One linear `Utf8JsonWriter` walk + a handful of exhaustive closed-DU token `match`es; no SRTP, reflection outside the surface test, custom operators, type providers, or non-trivial CEs. The reused tokens (`gateIdValue`, `referenceValue`, `categoryToken`) are called, not re-modeled. The single `for` loop over the entries list is the plainest emit and is the F025 `writeSection` precedent. |
| IV. Elmish/MVU is the boundary for stateful/I/O | **N/A** | No state, no I/O, no workflow — a pure total projection from one typed value to a string. Like F020/F021/F025, this is a pure renderer needing no MVU ceremony. The integration into route.json / audit.json, the host wiring that resolves each gate's `FreshnessInputs`, and any real cache store are later edges (Principle IV), explicitly out of scope. |
| V. Test Evidence Is Mandatory | **PASS** | Every input is a real, upstream-assembled `CacheEligibilityReport` from F041 `CacheEligibility.evaluate` over real `GateId` / `FreshnessInputs` / `ReuseStore` (via `EvidenceReuse.record`); output is parsed back with real `System.Text.Json`, asserting against the report value — no mock, no clock read, no hand-built JSON oracle, no real cache lookup. Tests fail before the projection matches the contract and pass after. No mocks ⇒ no `Synthetic` disclosure needed. |
| VI. Observability & Safe Failure | **N/A (totality stands in)** | No operationally-significant event exists to observe — no startup, store load/save, divergence, freshness-expiry, or scan to log (a pure in-value projection, like Principle IV). The safe-failure spirit is met by **totality**: `ofReport` never throws, swallows a failure, or silently drops. Every report — empty, all-reusable, all-must-recompute, mixed, duplicate-`GateId` — renders to a valid document (Edge Cases); every `mustRecompute` entry names its cause and the entries collection is always present (the no-hide rule). |
| Change Classification | **Tier 1 (contracted change — new public API)** | Adds a new public module/assembly (`FS.GG.Governance.CacheEligibilityJson`) and a new surface baseline ⇒ full chain: spec, plan, `.fsi`, baseline, tests, docs. **No new third-party dependency.** No existing public API, baseline, or merged behavior is altered (F041 report + F018/F029/F030 accessors consumed verbatim, not modified). |
| Engineering Constraints | **PASS** | F#/.NET `net10.0`; no new third-party `PackageReference` (FR-014) — serialization is the shared-framework `System.Text.Json`; references only the sibling pure core `CacheEligibility` (F041) — and its transitive pure cores `EvidenceReuse` / `Gates` / `FreshnessKey` / `Config` — no git / filesystem / host / CLI. No rendering package IDs/paths/templates assumed — inputs are product-neutral supplied values; the document carries no product vocabulary (FR-012). Pack output + structured-logging TODOs unaffected (no runtime/host code). |

**Gate result: PASS — no unjustified violations. Complexity Tracking is empty.** Principles IV and VI are
N/A (no stateful/I/O workflow, and no operationally-significant event to observe — totality stands in for
safe failure); I, II, III, V all have concrete targets and pass. The single sibling reference (research D2)
is the F041 report this row exists to project; it pulls in nothing impure and is the only cross-core
coupling.

## Project Structure

### Documentation (this feature)

```text
specs/042-cache-eligibility-json/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 — decisions D1–D8 (one-new-projection-lib, single reference, document shape +
│                        #            schema version, verdict/cause tagged-object rendering, order preserved verbatim,
│                        #            surface, exclusions, totality)
├── data-model.md        # Phase 1 — the cache-eligibility.json document shape (schemaVersion + entries) and how each
│                        #            CacheEligibilityEntry / Verdict / RecomputeCause renders (reuses F041 + F018/F029/F030)
├── quickstart.md        # Phase 1 — how to build, FSI-exercise, test, and re-bless the surface
├── contracts/           # Phase 1 — the contracts this row commits
│   ├── cache-eligibility-json-document.md   # the observable wire contract: field order, tokens, shape, exclusions, samples
│   └── cache-eligibility-json-api.md        # the public signatures + their laws (carry, determinism, no-hide, totality) + the scope guard
└── tasks.md             # Phase 2 — /speckit-tasks output (NOT created here)
```

### Source Code / deliverable layout (repository root)

```text
src/FS.GG.Governance.CacheEligibilityJson/                  # NEW — the pure cache-eligibility.json projection library
├── CacheEligibilityJson.fsi                                # NEW — schemaVersion + ofReport (sole public surface)
├── CacheEligibilityJson.fs                                 # NEW — the pure, total writer walk + token/sub-object helpers (private by omission)
└── FS.GG.Governance.CacheEligibilityJson.fsproj            # NEW — packable; references CacheEligibility; System.* + FSharp.Core

tests/FS.GG.Governance.CacheEligibilityJson.Tests/          # NEW — semantic tests over the PUBLIC surface (Expecto + FsCheck)
├── Support.fs                                              # NEW — real upstream report builders (GateId, FreshnessInputs, ReuseStore via record, evaluate) + FsCheck generators + a JsonDocument parse helper (no mocks)
├── ProjectionTests.fs                                      # NEW — US1: one entry per report entry, declared gate id + verdict (reusable+evidence / must-recompute+cause) tracing back (SC-001)
├── DeterminismTests.fs                                     # NEW — US2: byte-identical for identical reports; identical for value-equal reports from differently-ordered inputs; schemaVersion present; stable field/collection order (SC-002/SC-003)
├── NoHideTests.fs                                          # NEW — US3: every mustRecompute names its cause; inputsChanged categories in report order; noPriorEvidence ≠ inputsChanged [] (SC-005)
├── TotalityTests.fs                                        # NEW — US4: a document for empty / all-reusable / all-must-recompute / duplicate-GateId reports, never throws (SC-006)
├── ExclusionsTests.fs                                      # NEW — SC-007: no timestamp / host path / raw inputs / freshness key/hash / env value / exit code / severity / ship verdict / provenance reference
├── SurfaceDriftTests.fs                                    # NEW — Principle II surface baseline + CacheEligibility(+transitive cores)-only scope guard
├── Main.fs                                                 # NEW — Expecto entry point
└── FS.GG.Governance.CacheEligibilityJson.Tests.fsproj      # NEW — references CacheEligibilityJson (+ CacheEligibility/EvidenceReuse/Gates/FreshnessKey for the tokens); test packages

surface/FS.GG.Governance.CacheEligibilityJson.surface.txt   # NEW — Tier-1 public-surface baseline (BLESS_SURFACE=1 generated)
scripts/prelude.fsx                                         # EDIT — append a short F042 FSI section (design-first proof)
FS.GG.Governance.sln                                        # EDIT — add the two new projects
CLAUDE.md                                                   # EDIT — point the SPECKIT plan reference at this plan
```

**Structure Decision**: One new pure-projection F# library
`src/FS.GG.Governance.CacheEligibilityJson` (the established one-new-sibling-projection-per-emission-row
rhythm — F020 `RouteJson`, F021 `GatesJson`, F025 `AuditJson` — research D1), compiling a single
`CacheEligibilityJson.fsi → CacheEligibilityJson.fs`, referencing only the sibling pure core
`CacheEligibility` (F041) — to project the report it produced and reuse the `gateIdValue` / `referenceValue`
/ `categoryToken` token accessors that arrive transitively (research D2). A sibling test project exercises
the public surface with real upstream-assembled reports, parsing the output back with `System.Text.Json`.
The library is additive: no existing `src/`, `surface/`, or merged test project changes.

## Complexity Tracking

> No Constitution Check violations. This section is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
