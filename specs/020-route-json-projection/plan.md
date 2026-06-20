# Implementation Plan: Deterministic route.json Projection

**Branch**: `020-route-json-projection` (active spec; git branch currently `main`) | **Date**: 2026-06-20 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/020-route-json-projection/spec.md`

## Implementation Progress

**Status: ✅ COMPLETE** — all 29 tasks done; suite green (27 RouteJson tests; full solution 13/13 test projects pass). No synthetic evidence used.

| Phase | Tasks | Status | Evidence |
|---|---|---|---|
| 1 · Setup | T001–T009 | 🟢 Done | New `FS.GG.Governance.RouteJson` lib (references only `Route`, no new `PackageReference`) + test project in `.sln`; `RouteJson.fsi` copied verbatim; real upstream-assembly + `JsonDocument` read helpers in `Support.fs`; prelude F020 sketch; readiness README. |
| 2 · Foundation | T010–T013 | 🟢 Done | `RouteJson.fs` matches `RouteJson.fsi`; `schemaVersion = "fsgg.route/v1"`; hidden exhaustive closed-enum token helpers (`cost`/`maturity`/`environment`/`zone`, no wildcard); hidden sub-object writers (`freshnessKey`/`prerequisite`/`selectingPath`/`finding`/`cost`); pure/total `ofRouteResult` walk via `Utf8JsonWriter` (the kernel `Json.fs` mechanism). |
| 3 · US1 Render to route.json (P1, MVP) | T014–T015 | 🟢 Done | `ProjectionTests.fs` — 9 green: selected gates by declared id with carried metadata verbatim; multi-path→one gate, path-ordered; no non-selected gate; empty selectedGates + all-zero cost; per-tier cost (one-tier/spread); separator-in-domain verbatim; JSON-special free-text round-trip (SC-001/SC-005). |
| 4 · US2 Stable versioned schema (P1) | T016–T020 | 🟢 Done | `DeterminismTests.fs` — 7 green: FsCheck twice-identical (SC-002); permutation-invariance in paths + registry order (SC-003); schemaVersion + fixed top-level field order; exclusion sweep (deny tokens + no timestamp) + positive path-allowlist (SC-007/FR-011/FR-012). |
| 5 · US3 Carry findings + freshness (P2) | T021–T022 | 🟢 Done | `CarryTests.fs` — 5 green: F017 findings one-to-one unchanged in F017 order; both zone shapes (string + `{protectedBoundary}`); empty → present-and-empty array; freshnessKey 5 inputs (command string/null); no cache/severity/profile/mode/enforcement field (SC-004/FR-014). |
| 6 · US4 Totality (P2) | T023–T025 | 🟢 Done | `TotalityTests.fs` — 3 green: empty route → valid empty document + all-zero cost; findings-only route coexists; FsCheck totality over real-chain-generated results never throws and always parses (SC-006). |
| 7 · Polish | T026–T029 | 🟢 Done | `surface/FS.GG.Governance.RouteJson.surface.txt` baseline (exactly the `RouteJson` module) + drift test; `RouteJson → Route` transitive one-way dependency assertion (no kernel/host/CLI, no new third-party package); quickstart FSI smoke + readiness transcript; this progress header. |

**Decisions held:** emit-only `ofRouteResult` + `schemaVersion` — no parallel typed document, no round-trip parse (D4); `System.Text.Json` `Utf8JsonWriter`, the kernel's mechanism reused verbatim — no new dependency (D2); closed-enum token/zone helpers hidden in the `.fs` (the `Kernel/Json.fs` precedent, D3); single pure total function — no MVU (Principle IV). The four `RouteJson.fs` "confirm" tasks (T020/T022/T025) needed no change beyond the Foundation walk + US1 per-gate writer, which implemented the full projection. `System.Text.Json` escapes `'`/`<`/`>`/`&` as `\uXXXX` by default — deterministic and round-trip-faithful (verified by the JSON-special carry test).

## Summary

Define the Phase-2 **route.json projection**: a single **pure, total** function that renders the F019
`RouteResult` into a deterministic, versioned `route.json` document — the stable, machine-readable
contract every downstream consumer (`fsgg route`, CI, agents, generated readiness views, optional
Governance consumers) reads instead of an in-memory value. The entry point is
`RouteJson.ofRouteResult : RouteResult -> string`, returning compact UTF-8 JSON that is byte-for-byte
identical for identical inputs (FR-007), carries a declared schema-version stamp (FR-013), lists each
selected gate with its declared `GateId` and carried F018 metadata + route trace + freshness-key
inputs (FR-002/FR-004/FR-014), carries the F017 findings unchanged in F017 order (FR-005), and renders
the per-tier cost rollup with every declared tier present (FR-006). It re-derives, re-sorts, and
re-classifies nothing (the `RouteResult` already fixed every collection's order); it computes no
severity, profile, enforcement, cache-eligibility verdict, or ship verdict (FR-011), and emits no raw
YAML, host path, clock, or environment value (FR-012).

The work lands as a new optional, packable library **`FS.GG.Governance.RouteJson`** plus its test
project — the same one-library-per-row shape as Config/Routing/Snapshot/Findings/Gates/Route. It
references **only `FS.GG.Governance.Route`** (Gates/Routing/Findings/Config arriving transitively) and
adds **no new third-party `PackageReference`**: serialization uses `System.Text.Json`
(`Utf8JsonWriter`) from the `net10.0` shared framework — the *exact* mechanism the kernel's
`FS.GG.Governance.Kernel.Json` already uses, keeping the library `System.*`/FSharp.Core-only (FR-015).
The boundary is a plain pure function — no MVU, no ports — because the feature performs no I/O, senses
no git, holds no state (FR-008); it only renders an already-typed, already-ordered value, exactly as
F015/F017/F018/F019 did for their pure cores.

The feature stops at the document **string**. Held firm by FR-011, it does **not** parse route.json
back (round-trip is a later consumer's concern; this row is the pure projection only), assign
severity/profile/mode/enforcement, evaluate a gate's carried `FreshnessKey` (the key's *inputs* are
carried, never a cache verdict), decide a ship verdict / blockers / warnings / exit-code basis, or
wire any `fsgg route`/`fsgg ship` CLI host or audit.json. Those are later Phase-2 / Phase-5 / Phase-11
rows that read this document.

**Confirmed during planning (the two scope reconciliations the spec deferred to plan time — research
D1/D2):**

- **Project home**: a new sibling library `FS.GG.Governance.RouteJson` → `FS.GG.Governance.Route`
  (Gates/Routing/Findings/Config transitive); no new package, no kernel/host edge (research D1). It
  continues the immediately-preceding F014–F019 one-row-one-library rhythm and keeps F019's just-merged
  pure join (`Route`) free of any serialization surface — serialization is layered on top in a separate
  project, exactly the constitution's "heavier capabilities layer on top in separate projects, not into
  the core."
- **Serialization mechanism**: `System.Text.Json` `Utf8JsonWriter`, hand-driven for a fixed field
  order and compact deterministic output — the kernel's established `Json.fs` mechanism, reused
  verbatim, adding **no** dependency (research D2). The closed-enum wire tokens (`Cost`, `Maturity`,
  `EnvironmentClass`, finding `Zone`) are **local hidden helpers** in the projection's `.fs`, mirroring
  how `Kernel/Json.fs` keeps `severityToken`/`stateToken`/`writeOutcome` off its public surface (D3).
- **Contract shape**: emit-only `ofRouteResult : RouteResult -> string` plus a `schemaVersion`
  constant — no parallel typed "document" model that would merely duplicate `RouteResult` (research
  D4). Tests inspect the emitted bytes by read-only `JsonDocument` parse, the way the kernel's JSON
  tests do.

## Technical Context

**Language/Version**: F# on .NET, `net10.0` from `Directory.Build.props`.

**Primary Dependencies**: **No new third-party dependency.** One new `ProjectReference` —
`FS.GG.Governance.Route` (the F019 `RouteResult`/`SelectedGate`/`SelectingPath`/`CostRollup`).
`FS.GG.Governance.Gates` (`Gate`/`GateId`/`FreshnessKey`/`GatePrerequisite`, `gateIdValue`),
`FS.GG.Governance.Findings` (`FindingReport`/`FindingId`/`FindingZone`, `findingIdToken`), and
`FS.GG.Governance.Config` (`GovernedPath`/`Cost`/`DomainId`/`Owner`/`Maturity`/`EnvironmentClass`/
`CheckId`/`CommandId`/`SurfaceId`/`TimeoutLimit`) all arrive transitively via Route. Serialization is
`System.Text.Json` (`Utf8JsonWriter`) from the `net10.0` shared framework — the same `System.*` API the
kernel's `Json.fs` uses, so **no `PackageReference`** is added and the library stays `System.*`/
FSharp.Core-only (FR-015). Test-only packages remain the centrally pinned Expecto/FsCheck/VSTest set in
`Directory.Packages.props`.

**Storage**: None. Pure in-memory value → string; no file, process, clock, or network access of any
kind. Persisting the returned string to `readiness/<id>/route.json` is a later CLI/host edge (FR-008).

**Testing**: `dotnet test` (Expecto + FsCheck via VSTest). The pure projection is exercised through its
public surface over **real upstream-assembled inputs** — a real `RouteResult` from the genuine
F015→F017→F018→F019 chain over real `TypedFacts` (research D7): selected-gate presence + carried
metadata + route trace (US1); determinism (twice-identical bytes) + permutation-invariance + a declared
schema version + the exclusion sweep (no severity/enforcement/verdict/raw-YAML/host-path/timestamp
tokens) (US2); findings carried unchanged + freshness-key inputs present + no cache/enforcement field
(US3); and FsCheck totality over generated well-typed results including the empty and findings-only
routes (US4). Emitted bytes are inspected by read-only `JsonDocument` parse, mirroring the kernel's JSON
tests. A surface-drift test guards `surface/FS.GG.Governance.RouteJson.surface.txt`; an FSI/prelude
transcript runs the whole chain and prints the projected document.

**Target Platform**: Cross-platform .NET library; validated on the Linux dev host. No platform
capability is touched (no git executable, no filesystem) — like F017/F018/F019, this row reaches
nothing.

**Project Type**: Optional packable F# class library plus one test project — the same shape as
Config/Routing/Snapshot/Findings/Gates/Route.

**Performance Goals**: Deterministic projection, not throughput. One linear walk of the already-sorted
`RouteResult` (gates in `GateId` order, each gate's selecting paths in normalized-path order, findings
in F017 order), writing fixed-order object fields through a single `Utf8JsonWriter`. No `Map` iteration,
so no key-sort step is needed; byte-for-byte stable output for identical inputs (SC-002). No wall-clock,
environment, or host-path value enters the document.

**Constraints**: Pure and total (FR-008) — no I/O, git, or clock; never throws; an empty route projects
to a valid document with empty sections and the all-zero cost (FR-009), never an error and never a
"select everything" fallback. Declared `GateId`/path strings are rendered verbatim — no `GateId` is
re-parsed and no path re-normalized (FR-010). Findings are carried unchanged from F017 (FR-005). The
document carries only declared id strings, the declared `Cost`/`Maturity`/`EnvironmentClass`
vocabulary, the carried gate metadata, the carried freshness-key inputs, and the carried findings — no
raw YAML, host paths, timestamps, environment-derived values, severity, profile, mode, enforcement,
cache-eligibility verdict, ship verdict, blockers, warnings, or exit-code basis (FR-011/FR-012,
SC-007). Requires no installed FS.GG package in any inspected repo (FR-015).

**Scale/Scope**: One new production project (`src/FS.GG.Governance.RouteJson`) and one test project
(`tests/FS.GG.Governance.RouteJson.Tests`). The public module is `RouteJson` (one `val ofRouteResult`
+ one `val schemaVersion`), with a curated `.fsi` and a single surface baseline. **No** change to any
existing project's public surface — Route/Gates/Findings/Config are referenced as-is (their existing
public types and the `gateIdValue`/`findingIdToken` renderers suffice).

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.0.0. Re-checked after Phase 1 design —
still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | [`contracts/RouteJson.fsi`](./contracts/RouteJson.fsi) fixes the public surface before any `.fs` exists; [`contracts/route-json-document.md`](./contracts/route-json-document.md) fixes the observable wire shape. `tasks.md` must order `.fsi` → FSI/prelude sketch → semantic tests → implementation → surface baseline. |
| II. Visibility lives in `.fsi` + surface baseline | **PASS** | `RouteJson.fsi` is the sole public surface (`ofRouteResult` + `schemaVersion`); the `.fs` carries no top-level access modifiers and keeps every writer/token helper hidden (the `Kernel/Json.fs` precedent). Add `surface/FS.GG.Governance.RouteJson.surface.txt` + a surface-drift test. No existing baseline changes (no cross-feature surface touch). |
| III. Idiomatic simplicity | **PASS** | A single pure function over a `Utf8JsonWriter`, plain `match` token helpers, one linear walk of the already-ordered result — the simplest possible projection. **Reusing `System.Text.Json` (the kernel's mechanism) rather than a new serializer** and **emitting a string rather than a duplicate typed document** are both the simplicity-via-reuse choice (research D2/D4). No SRTP, reflection, type providers, custom operators, or non-trivial computation expressions. Any `mutable`/`for` writer loop is the plain BCL `Utf8JsonWriter` idiom, disclosed at the use site. |
| IV. Elmish/MVU boundary | **PASS** | Principle IV mandates MVU only for **stateful or I/O** features and explicitly exempts "an explanation formatter" / "a single pure function." This projection performs no I/O, senses no git, holds no state (FR-008) — it renders one already-typed value to a string, the exempt case. F019's `select` and the kernel's `Json.ofExplanation` made the same call. |
| V. Test evidence mandatory | **PASS** | Tests run through the public surface over **real upstream-assembled inputs** — the genuine F015→F017→F018→F019 `RouteResult`, not fakes (research D7) — and inspect the real emitted bytes via read-only `JsonDocument` parse. **No synthetic evidence is anticipated**; every case (empty/single/many-gate, findings-only, shared-gate, all-tiers) is reachable from real upstream outputs. Any unavoidable literal would carry `Synthetic` in the test name + a use-site disclosure and be listed in the PR. |
| VI. Observability & safe failure | **PASS** | The document this feature *produces* is itself the observability surface — a stable, versioned, machine-readable route trace for CI/agents/humans. The projection is total: no swallowed exception, because there is no operation that can throw over an already-validated `RouteResult` (FR-008). An empty route is a distinct successful document, never an error (FR-009). A tool defect is a test failure, never a malformed document. |
| Change Classification | **Tier 1** | New public, packable surface (a route.json projection library), a new public `.fsi`, a new surface baseline. Adds a new *project* but **no new third-party dependency** and **no change to any existing project's public surface**. |
| Engineering Constraints | **PASS** | `net10.0`; `FS.GG.Governance.*` identity; one-way dependency (`RouteJson → Route → {Gates, Routing, Findings} → Config`; Kernel/Host/adapters/CLI unaffected and do not reference RouteJson in this feature). No new third-party `PackageReference`; serialization is the shared-framework `System.Text.Json` the kernel already depends on (FR-015). A *layered* capability in a separate project — exactly the constitution's prescription. |

**Constitution alignment on the boundary (Principle IV).** Principle IV requires the
Model/Msg/Effect/update boundary for features "with multi-step state, external I/O, retries, user
interaction, background work, or operational recovery," and exempts "simple pure functions … an
explanation formatter." The route.json projection is squarely the exempt case — a deterministic
render of one typed value to a string, with no state and no effect. The kernel's `Json.ofExplanation`
/ `ofContract` formatters and F019's `select` took the same path; this row follows.

**Constitution alignment on dependency minimalism (Engineering Constraints).** The core constraint —
"the first useful product MUST NOT depend on FAKE, git, filesystem scanning, Skia, NuGet publishing,
template profiles, or rendering project paths" — is honored: `System.Text.Json` is a `System.*`
shared-framework API (the same one the kernel already uses), not a third-party `PackageReference`, and
the projection reaches no git/filesystem/clock. The capability is layered in a separate project above
the pure join, never folded into a core library.

**Gate result: PASS — no unjustified violations. Complexity Tracking remains empty.**

## Project Structure

### Documentation (this feature)

```text
specs/020-route-json-projection/
├── plan.md                       # This file
├── research.md                   # Phase 0 output (D1–D8 + resolved Technical Context)
├── data-model.md                 # Phase 1 output (consumed value, emitted document, field order, determinism)
├── quickstart.md                 # Phase 1 output (validation guide + acceptance→evidence map)
├── contracts/
│   ├── RouteJson.fsi             # the pure entry point: ofRouteResult + schemaVersion
│   └── route-json-document.md    # the observable wire contract: field order, tokens, worked sample
├── checklists/
│   └── requirements.md           # spec quality checklist (created by /speckit-specify)
├── readiness/                    # FSI transcripts + SC traceability note (created during tasks)
└── tasks.md                      # Created by /speckit-tasks, NOT by this command
```

### Source Code (repository root)

```text
src/FS.GG.Governance.RouteJson/                     # NEW optional route.json projection library
├── FS.GG.Governance.RouteJson.fsproj               # references Route only; no new package (System.Text.Json is BCL)
├── RouteJson.fsi                                    # = contracts/RouteJson.fsi (ofRouteResult + schemaVersion)
└── RouteJson.fs                                     # the pure projection: one Utf8JsonWriter walk (PURE), hidden token/writer helpers

tests/FS.GG.Governance.RouteJson.Tests/             # NEW semantic tests
├── FS.GG.Governance.RouteJson.Tests.fsproj         # references RouteJson (+ Route/Gates/Routing/Findings/Config transitive)
├── Support.fs                                        # real upstream assembly helpers (F015→F019 chain over real TypedFacts) + JsonDocument read helpers
├── ProjectionTests.fs                               # US1: selected gates + carried metadata + route trace present, non-selected absent (SC-001)
├── DeterminismTests.fs                              # US2: twice-identical bytes + permutation-invariance + schema version + exclusion sweep (SC-002/003/007)
├── CarryTests.fs                                     # US3: findings unchanged + freshness-key inputs present + no cache/enforcement field (SC-004)
├── TotalityTests.fs                                 # US4: FsCheck totality + empty route + findings-only route never throw (SC-005/006)
├── SurfaceDriftTests.fs                             # baseline drift + "exactly the RouteJson module, nothing private" + one-way dependency check
└── Main.fs

surface/FS.GG.Governance.RouteJson.surface.txt      # NEW public surface baseline
scripts/prelude.fsx                                 # extend with an F020 sketch: project f19Result and print the document
FS.GG.Governance.sln                                # add RouteJson project and RouteJson test project
CLAUDE.md                                            # SPECKIT block repointed to this plan
```

**Structure Decision**: a new `FS.GG.Governance.RouteJson` class library, sibling to
Config/Routing/Snapshot/Findings/Gates/Route, is the home for the route.json projection. It references
**only `FS.GG.Governance.Route`** (Gates/Routing/Findings/Config transitive) and adds no third-party
dependency — serialization is the `net10.0` shared-framework `System.Text.Json` the kernel's `Json.fs`
already uses. This keeps the dependency direction one-way
(`RouteJson → Route → {Gates, Routing, Findings} → Config`), the kernel/host untouched, and F019's
just-merged pure `Route` surface free of any serialization concern. A single `RouteJson` module (no
`Model` split — the projection introduces no new domain types, only `ofRouteResult` + `schemaVersion`)
mirrors the kernel's in-library `Json` module while staying a distinct, separately-baselined row in the
Governance layer, never the kernel, because the route/gate-selection vocabulary must not reach the
kernel (FR-015). See research [D1](./research.md) for the home/mechanism rationale and the rejected
alternatives (add a module to `Route`; reuse the kernel `Json` module; a hand-rolled string builder).

## Complexity Tracking

> No unjustified Constitution Check violations.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| - | - | - |
