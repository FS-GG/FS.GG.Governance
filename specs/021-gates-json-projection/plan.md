# Implementation Plan: Deterministic gates.json Projection

**Branch**: `021-gates-json-projection` (active spec; git branch currently `main`) | **Date**: 2026-06-20 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/021-gates-json-projection/spec.md`

## Implementation Progress

**Status: ✅ COMPLETE** — all 29 tasks done; suite green (25 GatesJson tests; full solution builds clean). No synthetic evidence used.

| Phase | Tasks | Status | Evidence |
|---|---|---|---|
| 1 · Setup | T001–T009 | 🟢 Done | New `FS.GG.Governance.GatesJson` lib (references only `Gates`, no new `PackageReference`) + test project in `.sln`; `GatesJson.fsi` copied verbatim; real upstream-assembly (`facts`/`registryFor` over `Gates.buildRegistry`) + `JsonDocument` read helpers in `Support.fs`; prelude F021 sketch; readiness README. |
| 2 · Foundation | T010–T013 | 🟢 Done | `GatesJson.fs` matches `GatesJson.fsi`; `schemaVersion = "fsgg.gates/v1"`; hidden exhaustive closed-enum token helpers (`cost`/`maturity`/`environment`, no wildcard); hidden sub-object writers (`freshnessKey`/`prerequisite`/`gate`); pure/total `ofGateRegistry` walk via `Utf8JsonWriter` (the kernel `Json.fs` + F020 `RouteJson.fs` mechanism). |
| 3 · US1 Render to gates.json (P1, MVP) | T014–T015 | 🟢 Done | `ProjectionTests.fs` — 7 green: every declared gate by id with carried metadata verbatim; command prereq + present-and-empty prereqs; no invented gate; empty registry → empty `gates` array; separator-in-domain (`a:b:tests`) verbatim; default vs command-derived timeout verbatim; JSON-special free-text round-trip (SC-001/SC-005). |
| 4 · US2 Stable versioned schema (P1) | T016–T020 | 🟢 Done | `DeterminismTests.fs` — 7 green: FsCheck twice-identical (SC-002); permutation-invariance over check order (fixed + property, SC-003); schemaVersion + fixed top-level/gate/freshnessKey field order; exclusion sweep (deny tokens + no timestamp) + positive string-allowlist (SC-007/FR-011/FR-012). |
| 5 · US3 Carry freshness, exclude enforcement (P2) | T021–T022 | 🟢 Done | `CarryTests.fs` — 5 green: freshnessKey 5 inputs in fixed order; `Some` command string / `None` command explicit JSON null; productCheck carried verbatim (release gate = true); no cache/severity/profile/mode/enforcement field; cost/maturity declared vocabulary, no weighted scalar (SC-004/SC-005/FR-014). |
| 6 · US4 Totality (P2) | T023–T024 | 🟢 Done | `TotalityTests.fs` — 3 green: empty registry → valid empty document; mixed present/absent prereqs + Some/None command, no leak; FsCheck totality over real-`buildRegistry`-generated registries never throws and always parses (SC-006). |
| 7 · Polish | T025–T029 | 🟢 Done | `surface/FS.GG.Governance.GatesJson.surface.txt` baseline (exactly the `GatesJson` module) + drift test; `GatesJson → Gates → Config` one-way dependency assertion (no kernel/host/Route/RouteJson/CLI, no new third-party package); prelude FSI smoke + readiness transcript; this progress header. |

**Decisions held:** emit-only `ofGateRegistry` + `schemaVersion` — no parallel typed document, no round-trip parse (D4); `System.Text.Json` `Utf8JsonWriter`, the kernel/F020 mechanism reused verbatim — no new dependency (D2); closed-enum token helpers hidden in the `.fs` (the `Kernel/Json.fs` + `RouteJson.fs` precedent, D3); single pure total function — no MVU (Principle IV). The implementation landed the full projection in the Foundation walk + per-gate `writeGate`; the "confirm" tasks (T020/T022/T025) needed no change beyond it. `System.Text.Json` escapes `'`/`<`/`>`/`&` as `\uXXXX` by default — deterministic and round-trip-faithful (verified by the JSON-special carry test). The per-gate entry is exactly F020's `selectedGates[*]` minus `selectingPaths`.

## Summary

Define the Phase-2 **gates.json projection**: a single **pure, total** function that renders the F018
`GateRegistry` into a deterministic, versioned `gates.json` document — the stable, machine-readable
**whole-catalog** gate registry every downstream consumer (the later `fsgg` commands, CI, agents,
generated readiness views, humans) reads instead of an in-memory value. The entry point is
`GatesJson.ofGateRegistry : GateRegistry -> string`, returning compact UTF-8 JSON that is byte-for-byte
identical for identical inputs (FR-007), carries a declared schema-version stamp (FR-013), and lists
each gate the registry carries — by its declared `GateId`, with its domain, description, cost, timeout,
owner, maturity, product-check flag, prerequisites, and carried freshness-key inputs
(FR-002/FR-004/FR-014). It re-derives, re-sorts, and re-classifies nothing (the `GateRegistry` already
fixed the gate order — `GateId` ordinal — and every gate's carried order); it computes no severity,
profile, enforcement, cache-eligibility verdict, per-change selection, or ship verdict (FR-011), and
emits no raw YAML, host path, clock, or environment-derived value (FR-012).

Where F020's `route.json` is the **per-change** view (which gates a specific change selected, from a
`RouteResult`), this `gates.json` is the **whole-catalog** view (every gate the repository declares,
from the `GateRegistry`). They are sibling pure projections of two different upstream typed values. The
per-gate field set rendered here is exactly the F020 `selectedGates[*]` entry **minus** the
route-specific `selectingPaths` — the same shared gate fields render identically in both artifacts.

The work lands as a new optional, packable library **`FS.GG.Governance.GatesJson`** plus its test
project — the same one-library-per-row shape as Config/Routing/Snapshot/Findings/Gates/Route/RouteJson.
It references **only `FS.GG.Governance.Gates`** (Config arriving transitively) and adds **no new
third-party `PackageReference`**: serialization uses `System.Text.Json` (`Utf8JsonWriter`) from the
`net10.0` shared framework — the *exact* mechanism the kernel's `FS.GG.Governance.Kernel.Json` and
F020's `RouteJson` already use, keeping the library `System.*`/FSharp.Core-only (FR-015). The boundary
is a plain pure function — no MVU, no ports — because the feature performs no I/O, senses no git, holds
no state (FR-008); it only renders an already-typed, already-ordered value, exactly as
F015/F017/F018/F019/F020 did for their pure cores.

The feature stops at the document **string**. Held firm by FR-011, it does **not** parse gates.json
back (round-trip is a later consumer's concern; this row is the pure projection only), assign
severity/profile/mode/enforcement, evaluate a gate's carried `FreshnessKey` (the key's *inputs* are
carried, never a cache verdict), select any gate for a change (that is route.json / F019–F020), or
decide a ship verdict / blockers / exit-code basis (audit.json). Those are later Phase-2 / Phase-5 /
Phase-11 rows that read this document. It also performs no I/O: persisting the returned string to
`.fsgg/gates.json` on disk is the later `fsgg` CLI host edge.

**Confirmed during planning (the two scope reconciliations the spec deferred to plan time — research
D1/D2):**

- **Project home**: a new sibling library `FS.GG.Governance.GatesJson` → `FS.GG.Governance.Gates`
  (Config transitive); no new package dependency, no kernel/host edge (research D1). It continues the
  immediately-preceding F014–F020 one-row-one-library rhythm and keeps F018's `Gates` pure assembler
  free of any serialization surface — serialization is layered on top in a separate project, exactly
  the constitution's "heavier capabilities layer on top in separate projects, not into the core."
- **Serialization mechanism**: `System.Text.Json` `Utf8JsonWriter`, hand-driven for a fixed field
  order and compact deterministic output — the kernel's established `Json.fs` mechanism that F020's
  `RouteJson` already reuses verbatim, adding **no** dependency (research D2). The closed-enum wire
  tokens (`Cost`, `Maturity`, `EnvironmentClass`) are **local hidden helpers** in the projection's
  `.fs`, mirroring how `Kernel/Json.fs` and `RouteJson.fs` keep their token plumbing off the public
  surface (D3).
- **Contract shape**: emit-only `ofGateRegistry : GateRegistry -> string` plus a `schemaVersion`
  constant (`"fsgg.gates/v1"`) — no parallel typed "document" model that would merely duplicate
  `GateRegistry` (research D4). Tests inspect the emitted bytes by read-only `JsonDocument` parse, the
  way the kernel's and F020's JSON tests do.

## Technical Context

**Language/Version**: F# on .NET, `net10.0` from `Directory.Build.props`.

**Primary Dependencies**: **No new third-party dependency.** One new `ProjectReference` —
`FS.GG.Governance.Gates` (the F018 `GateRegistry`/`Gate`/`GateId`/`FreshnessKey`/`GatePrerequisite`, and
the `gateIdValue` renderer). `FS.GG.Governance.Config`
(`DomainId`/`Cost`/`Maturity`/`Owner`/`TimeoutLimit`/`CommandId`/`CheckId`/`EnvironmentClass`) arrives
transitively via Gates. Serialization is `System.Text.Json` (`Utf8JsonWriter`) from the `net10.0`
shared framework — the same `System.*` API the kernel's `Json.fs` and F020's `RouteJson.fs` use, so
**no `PackageReference`** is added and the library stays `System.*`/FSharp.Core-only (FR-015). Test-only
packages remain the centrally pinned Expecto/FsCheck/VSTest set in `Directory.Packages.props`.

**Storage**: None. Pure in-memory value → string; no file, process, clock, or network access of any
kind. Persisting the returned string to `.fsgg/gates.json` is a later CLI/host edge (FR-008).

**Testing**: `dotnet test` (Expecto + FsCheck via VSTest). The pure projection is exercised through its
public surface over **real upstream-assembled inputs** — a real `GateRegistry` from the genuine F018
`Gates.buildRegistry` over real `TypedFacts` (research D7): every gate present with its carried metadata
verbatim (US1); determinism (twice-identical bytes) + permutation-invariance (registries equal as
values from differently-ordered declared checks) + a declared schema version + the exclusion sweep (no
severity/enforcement/verdict/selection/raw-YAML/host-path/timestamp tokens) (US2); freshness-key inputs
present + product-check carried + no cache/enforcement field (US3); and FsCheck totality over generated
well-typed registries including the empty registry (US4). Emitted bytes are inspected by read-only
`JsonDocument` parse, mirroring the kernel's and F020's JSON tests. A surface-drift test guards
`surface/FS.GG.Governance.GatesJson.surface.txt`; an FSI/prelude transcript assembles the real registry
and prints the projected document.

**Target Platform**: Cross-platform .NET library; validated on the Linux dev host. No platform
capability is touched (no git executable, no filesystem) — like F017/F018/F019/F020, this row reaches
nothing.

**Project Type**: Optional packable F# class library plus one test project — the same shape as
Config/Routing/Snapshot/Findings/Gates/Route/RouteJson.

**Performance Goals**: Deterministic projection, not throughput. One linear walk of the already-sorted
`GateRegistry` (gates in `GateId` order, each gate's prerequisites in their carried order), writing
fixed-order object fields through a single `Utf8JsonWriter`. No `Map` iteration, so no key-sort step is
needed; byte-for-byte stable output for identical inputs (SC-002). No wall-clock, environment, or
host-path value enters the document.

**Constraints**: Pure and total (FR-008) — no I/O, git, or clock; never throws; an empty registry
projects to a valid document with an empty gate list (FR-009), never an error and never a placeholder
gate. Declared `GateId`/`DomainId`/`CommandId`/`CheckId` strings are rendered verbatim — no `GateId`
is re-parsed to recover a domain (FR-010). Maturity and cost render as the declared F014 vocabulary —
maturity is never translated to enforcement, cost never to a weighted scalar (FR-005). The carried
`TimeoutLimit` renders verbatim — no timeout is computed or re-defaulted (FR-006). The document carries
only declared id strings, the declared `Cost`/`Maturity`/`EnvironmentClass` vocabulary, the carried
gate metadata, the carried free-text description, and the carried freshness-key inputs — no raw YAML,
host paths, timestamps, environment-derived values, severity, profile, mode, enforcement,
cache-eligibility verdict, per-change selection, route trace, ship verdict, blockers, warnings, or
exit-code basis (FR-011/FR-012, SC-007). Requires no installed FS.GG package in any inspected repo
(FR-015).

**Scale/Scope**: One new production project (`src/FS.GG.Governance.GatesJson`) and one test project
(`tests/FS.GG.Governance.GatesJson.Tests`). The public module is `GatesJson` (one `val ofGateRegistry`
+ one `val schemaVersion`), with a curated `.fsi` and a single surface baseline. **No** change to any
existing project's public surface — Gates/Config are referenced as-is (their existing public types and
the `gateIdValue` renderer suffice).

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.0.0. Re-checked after Phase 1 design —
still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | [`contracts/GatesJson.fsi`](./contracts/GatesJson.fsi) fixes the public surface before any `.fs` exists; [`contracts/gates-json-document.md`](./contracts/gates-json-document.md) fixes the observable wire shape. `tasks.md` must order `.fsi` → FSI/prelude sketch → semantic tests → implementation → surface baseline. |
| II. Visibility lives in `.fsi` + surface baseline | **PASS** | `GatesJson.fsi` is the sole public surface (`ofGateRegistry` + `schemaVersion`); the `.fs` carries no top-level access modifiers and keeps every writer/token helper hidden (the `Kernel/Json.fs` + `RouteJson.fs` precedent). Add `surface/FS.GG.Governance.GatesJson.surface.txt` + a surface-drift test. No existing baseline changes (no cross-feature surface touch). |
| III. Idiomatic simplicity | **PASS** | A single pure function over a `Utf8JsonWriter`, plain `match` token helpers, one linear walk of the already-ordered registry — the simplest possible projection. **Reusing `System.Text.Json` (the kernel's mechanism, already reused by F020)** rather than a new serializer and **emitting a string rather than a duplicate typed document** are both the simplicity-via-reuse choice (research D2/D4). No SRTP, reflection, type providers, custom operators, or non-trivial computation expressions. Any `mutable`/`for` writer loop is the plain BCL `Utf8JsonWriter` idiom, disclosed at the use site. |
| IV. Elmish/MVU boundary | **PASS** | Principle IV mandates MVU only for **stateful or I/O** features and explicitly exempts "an explanation formatter" / "a single pure function." This projection performs no I/O, senses no git, holds no state (FR-008) — it renders one already-typed value to a string, the exempt case. F019's `select`, F020's `ofRouteResult`, and the kernel's `Json.ofExplanation` made the same call. |
| V. Test evidence mandatory | **PASS** | Tests run through the public surface over **real upstream-assembled inputs** — the genuine F018 `Gates.buildRegistry` `GateRegistry`, not fakes (research D7) — and inspect the real emitted bytes via read-only `JsonDocument` parse. **No synthetic evidence is anticipated**; every case (empty/single/many-gate, with/without prerequisites, with/without freshness command) is reachable from real upstream outputs. Any unavoidable literal would carry `Synthetic` in the test name + a use-site disclosure and be listed in the PR. |
| VI. Observability & safe failure | **PASS** | The document this feature *produces* is itself the observability surface — a stable, versioned, machine-readable gate catalog for CI/agents/humans. The projection is total: no swallowed exception, because there is no operation that can throw over an already-validated `GateRegistry` (FR-008). An empty registry is a distinct successful document, never an error (FR-009). A tool defect is a test failure, never a malformed document. |
| Change Classification | **Tier 1** | New public, packable surface (a gates.json projection library), a new public `.fsi`, a new surface baseline. Adds a new *project* but **no new third-party dependency** and **no change to any existing project's public surface**. |
| Engineering Constraints | **PASS** | `net10.0`; `FS.GG.Governance.*` identity; one-way dependency (`GatesJson → Gates → Config`; Kernel/Host/adapters/CLI/Route/RouteJson unaffected and do not reference GatesJson in this feature). No new third-party `PackageReference`; serialization is the shared-framework `System.Text.Json` the kernel already depends on (FR-015). A *layered* capability in a separate project — exactly the constitution's prescription. |

**Constitution alignment on the boundary (Principle IV).** Principle IV requires the
Model/Msg/Effect/update boundary for features "with multi-step state, external I/O, retries, user
interaction, background work, or operational recovery," and exempts "simple pure functions … an
explanation formatter." The gates.json projection is squarely the exempt case — a deterministic render
of one typed value to a string, with no state and no effect. F020's `ofRouteResult`, the kernel's
`Json.ofExplanation`/`ofContract` formatters, and F019's `select` took the same path; this row follows.

**Constitution alignment on dependency minimalism (Engineering Constraints).** The core constraint —
"the first useful product MUST NOT depend on FAKE, git, filesystem scanning, Skia, NuGet publishing,
template profiles, or rendering project paths" — is honored: `System.Text.Json` is a `System.*`
shared-framework API (the same one the kernel and F020 already use), not a third-party
`PackageReference`, and the projection reaches no git/filesystem/clock. The capability is layered in a
separate project above the pure F018 assembler, never folded into a core library.

**Gate result: PASS — no unjustified violations. Complexity Tracking remains empty.**

## Project Structure

### Documentation (this feature)

```text
specs/021-gates-json-projection/
├── plan.md                       # This file
├── research.md                   # Phase 0 output (D1–D8 + resolved Technical Context)
├── data-model.md                 # Phase 1 output (consumed value, emitted document, field order, determinism)
├── quickstart.md                 # Phase 1 output (validation guide + acceptance→evidence map)
├── contracts/
│   ├── GatesJson.fsi             # the pure entry point: ofGateRegistry + schemaVersion
│   └── gates-json-document.md    # the observable wire contract: field order, tokens, worked sample
├── checklists/
│   └── requirements.md           # spec quality checklist (created by /speckit-specify)
├── readiness/                    # FSI transcripts + SC traceability note (created during tasks)
└── tasks.md                      # Created by /speckit-tasks, NOT by this command
```

### Source Code (repository root)

```text
src/FS.GG.Governance.GatesJson/                     # NEW optional gates.json projection library
├── FS.GG.Governance.GatesJson.fsproj               # references Gates only; no new package (System.Text.Json is BCL)
├── GatesJson.fsi                                    # = contracts/GatesJson.fsi (ofGateRegistry + schemaVersion)
└── GatesJson.fs                                     # the pure projection: one Utf8JsonWriter walk (PURE), hidden token/writer helpers

tests/FS.GG.Governance.GatesJson.Tests/             # NEW semantic tests
├── FS.GG.Governance.GatesJson.Tests.fsproj         # references GatesJson (+ Gates/Config transitive)
├── Support.fs                                        # real upstream assembly helpers (F014 facts → F018 buildRegistry) + JsonDocument read helpers
├── ProjectionTests.fs                               # US1: every gate + carried metadata verbatim, no invented gate (SC-001/SC-005)
├── DeterminismTests.fs                              # US2: twice-identical bytes + permutation-invariance + schema version + exclusion sweep (SC-002/003/007)
├── CarryTests.fs                                     # US3: freshness-key inputs present + product-check carried + no cache/enforcement field (SC-004)
├── TotalityTests.fs                                 # US4: FsCheck totality + empty registry never throws (SC-006)
├── SurfaceDriftTests.fs                             # baseline drift + "exactly the GatesJson module, nothing private" + one-way dependency check
└── Main.fs

surface/FS.GG.Governance.GatesJson.surface.txt      # NEW public surface baseline
scripts/prelude.fsx                                 # extend with an F021 sketch: project the real registry and print the document
FS.GG.Governance.sln                                # add GatesJson project and GatesJson test project
CLAUDE.md                                            # SPECKIT block repointed to this plan
```

**Structure Decision**: a new `FS.GG.Governance.GatesJson` class library, sibling to
Config/Routing/Snapshot/Findings/Gates/Route/RouteJson, is the home for the gates.json projection. It
references **only `FS.GG.Governance.Gates`** (Config transitive) and adds no third-party dependency —
serialization is the `net10.0` shared-framework `System.Text.Json` the kernel's `Json.fs` and F020's
`RouteJson.fs` already use. This keeps the dependency direction one-way
(`GatesJson → Gates → Config`), the kernel/host untouched, and F018's `Gates` assembler surface free of
any serialization concern. A single `GatesJson` module (no `Model` split — the projection introduces no
new domain types, only `ofGateRegistry` + `schemaVersion`) mirrors the kernel's in-library `Json`
module and F020's `RouteJson` while staying a distinct, separately-baselined row in the Governance
layer, never the kernel, because the gate vocabulary must not reach the kernel (FR-015). See research
[D1](./research.md) for the home/mechanism rationale and the rejected alternatives (add a module to
`Gates`; reuse the kernel `Json` module; a hand-rolled string builder).

## Complexity Tracking

> No unjustified Constitution Check violations.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| - | - | - |
