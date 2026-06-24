# Implementation Plan: Release-Facts Sensing for the Repository Boundary

**Branch**: `054-release-facts-sensing` | **Date**: 2026-06-24 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/054-release-facts-sensing/spec.md`

## Summary

F053 landed the pure release-gate core (`evaluate`/`rollup`/`evaluateRelease`) but took the release facts
it governs as **provided typed input** and sensed nothing — its "Out of Scope" named *"sensing real
release facts from a governed repository"* as the **next** row. This feature is that row: it senses, from
a real governed repository behind a **single injected effects boundary**, the current state of each of the
six release rule families and produces **exactly the F053 `ReleaseFacts` value** (the `Map<ReleaseRuleKind,
FactState>`) the core consumes — handed straight to `evaluate` with no adaptation — plus a typed
**`ReleaseSnapshot`** of the observed evidence behind each fact.

The whole feature is **one small new library** — `FS.GG.Governance.ReleaseFactsSensing` — layered on the
merged thread (the constitution's "heavier capabilities layer on top, not into the core"). It mirrors the
established sensing shape (F016 Snapshot, FreshnessSensing) exactly:

1. **A pure derivation core** (`Sensing.deriveFacts : expectations -> recovered -> SensedRelease`, US1+US2)
   — for each of the six families it classifies one `FactState`: `Met` only when the recovered evidence
   satisfies the caller's declared expectation, `Unmet` when recovered-but-unsatisfied, and `Unrecoverable`
   when the source is absent/unreadable **or** the caller declared no expectation for it (fail-safe — never
   a fabricated `Met`, never a thrown error). It always returns all six families, surfaces the observed
   evidence per family in the snapshot, and is structurally identical for identical input. It **reuses the F053
   `ReleaseFacts`/`FactState`/`ReleaseRuleKind` verbatim** as its output vocabulary (research D1).
2. **A thin impure edge** (`Interpreter`) — a single injected `RepositoryPort` (a record of six per-family
   read functions, the FreshnessSensing precedent), a production `realPort repoDir layout` that reads only
   **local files** via BCL `System.IO`, a `gather` that runs the port catching every exception, and the
   single composition `senseRelease = gather >> deriveFacts`. The edge is the **only** impure code; it
   reaches **no** network, registry, or publishing provider (FR-007) — proven by a dependency scope-guard
   test that bans `System.Net.Http`/`Octokit`/`LibGit2Sharp` (SC-004, F016 precedent).

The one genuinely-new design decision (resolved in [research.md](./research.md), D3) is that the port
recovers **structured** evidence (`VersionEvidence`, `MetadataEvidence`, …) and the pure core only
**classifies** it against the expectation — rather than the port handing back raw file text the pure core
parses. This keeps `deriveFacts` genuinely pure and unit-testable without disk, keeps all on-disk **format**
knowledge in the swappable port (so the generic library invents no product manifest schema — the operating
rule, D5), and matches the closest sensing sibling (FreshnessSensing's `FreshnessSensor`). The host row's
port can later read real manifests — additive, no core change.

This row stops at the sensed `SensedRelease` (facts + snapshot). It does **not** evaluate rules into
findings, roll up a verdict (F053), run the `fsgg release` host command, or emit `release.json` — those are
F053 and following rows (FR-012). The committed contracts live in [contracts/](./contracts/); the entities
and flow in [data-model.md](./data-model.md); the build/exercise/test walkthrough in
[quickstart.md](./quickstart.md); the resolved decisions in [research.md](./research.md).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (repo standard; `Nullable=enable`, `TreatWarningsAsErrors=true`,
`WarnOn=3390;1182`, `--nowarn:57` from `Directory.Build.props`). This row adds **one** small new library
(`FS.GG.Governance.ReleaseFactsSensing`) and its test project. No existing project is edited; no command, no
schema, no document.

**Primary Dependencies**: `ProjectReference`s only; **no new third-party `PackageReference`** (Assumptions,
constitution dependency-minimalism). The new library references exactly the merged cores whose vocabulary it
reuses: `FS.GG.Governance.ReleaseRules` (F053 — `ReleaseRuleKind`, `FactState`, `ReleaseFacts`, and
`releaseRuleKindOrdinal`/`releaseRuleKindToken` for deterministic ordering/diagnostics) and
`FS.GG.Governance.Config` (F014 — `SurfaceId` for the caller-supplied governed identity). It does **not**
reference `Snapshot`, `Route`, `Gates`, or any hosting/registry SDK. Test frameworks unchanged (Expecto,
Expecto.FsCheck, FsCheck, Microsoft.NET.Test.Sdk, YoloDev.Expecto.TestSdk).

**Storage**: Reads **only local repository files** at the edge (the six caller-located governing sources);
writes nothing (FR-007). No reuse store, no `release.json`, no schema, no schema-version bump. The pure core
reads and writes no file; facts and snapshot are in-memory values.

**Testing**: Expecto + FsCheck. New `FS.GG.Governance.ReleaseFactsSensing.Tests` drives the packed public
surface: (US1) `senseRelease` over a real temp fixture repo ⇒ exactly six families, each correctly
classified, and `sensed.Facts` accepted by the F053 `Release.evaluate` with no adaptation (one finding per
rule); (US1.2/SC-005) not-bumped version ⇒ `Unmet` with the other five `Met`, and an all-violating fixture
⇒ each `Unmet`; (US2) the snapshot names present/missing metadata fields and resolved-vs-expected pins
matching the `Unmet` families; (US3/SC-002) an evidence-removed and an evidence-corrupted family both ⇒
`Unrecoverable` (not `Met`, not a crash) with all six still returned; (US3.2/SC-003) structurally identical
`SensedRelease` over a repeated sense including every collection's order; (SC-006) the output family set
equals `Sensing.releaseFamilies` across satisfied/violated/all-unrecoverable fixtures. A `SurfaceDriftTests`
carries the **dependency scope guard** asserting no `System.Net.Http`/`Octokit`/`LibGit2Sharp` reference
(SC-004). An FsCheck property asserts `|Facts.States| = 6` over random expectation/evidence sets. **Real
fixtures (Principle V)**: the edge is tested through `realPort` over a real temp fixture repository (the
F016 Snapshot / FreshnessSensing `withTempDir` precedent); the pure `deriveFacts` is unit-tested with
hand-built `RecoveredEvidence`. No network, no registry, no publishing provider (SC-004). New surface
baseline `surface/FS.GG.Governance.ReleaseFactsSensing.surface.txt` (generated via `BLESS_SURFACE`).

**Target Platform**: Developer / CI .NET SDK running `dotnet test`. The pure core is platform-agnostic; the
edge uses only BCL `System.IO` file reads (no OS-specific surface).

**Project Type**: A small library: one pure classification core + a thin injected-port edge. It is the
established sensing shape (F016 Snapshot, FreshnessSensing) — **not** a full Elmish `Program` (research D2;
see Constitution Check IV).

**Performance Goals**: N/A. The cost is six local file reads at the edge plus one classification per family
— constant in the six-family count. No hot path, no measured budget.

**Constraints**: Single injected effects boundary (FR-006): all impurity is the one `RepositoryPort`; the
derivation over recovered evidence is pure. Fail-safe (FR-004): an absent/unreadable/unparseable source or
an absent expectation ⇒ `Unrecoverable`, never a fabricated `Met`, never a swallowed error, never a thrown
exception. Network-free (FR-007, SC-004): only local reads; no registry/provider; proven by the scope
guard. Deterministic (FR-008, SC-003): identical state + identical expectations ⇒ structurally identical facts and
snapshot (compare equal), every collection ordered. Total (FR-009): all six families on every run, including all-missing ⇒
all-`Unrecoverable`. Exact F053 shape (FR-002, SC-001): the output `Facts` IS the F053 `ReleaseFacts`,
handed straight to `evaluate`. Product-neutral (FR-011): expectations and source layout are caller-supplied
— no hardcoded id, path, field, pin, posture, or layout.

**Scale/Scope**: Additive only. **New**: `src/FS.GG.Governance.ReleaseFactsSensing/`
(`Model.fsi/.fs`, `Sensing.fsi/.fs`, `Interpreter.fsi/.fs`, `.fsproj`),
`tests/FS.GG.Governance.ReleaseFactsSensing.Tests/`,
`surface/FS.GG.Governance.ReleaseFactsSensing.surface.txt`, two `.sln` entries, a `scripts/prelude.fsx`
F054 section, the `CLAUDE.md` plan pointer. **Edited**: none of the merged cores. **Untouched (frozen)**:
F053 `ReleaseRules`, F016 `Snapshot`, F014 `Config`, and every other core/golden/schema.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — still PASS.*

| Principle | Status | Justification |
|-----------|--------|---------------|
| I. Spec → FSI → Semantic Tests → Implementation | PASS | FSI-first is satisfied by committing the `contracts/Model.fsi` + `contracts/Sensing.fsi` + `contracts/Interpreter.fsi` and a runnable `scripts/prelude.fsx` F054 transcript (sense an all-met fixture, a not-bumped version, a missing field, a removed source; feed `sensed.Facts` to F053 `evaluate`) **before any `.fs` body**, then writing public-surface semantic tests that fail before implementation and pass after — driving the packed surface, not private helpers. |
| II. Visibility lives in `.fsi` | PASS | Every new public symbol is declared in the curated `Model.fsi` (`ReleaseExpectations`, `SourceLayout`, the four evidence records, `RecoveredEvidence`, the four `*Fact` records, `SensingDiagnostic`, `ReleaseSnapshot`, `SensedRelease`), `Sensing.fsi` (`releaseFamilies`, `deriveFacts`), and `Interpreter.fsi` (`RepositoryPort`, `realPort`, `gather`, `senseRelease`). The `.fs` files carry no `private`/`internal`/`public` modifiers — the per-family classifiers (version compare, metadata/pin/posture checks), the snapshot builders, and the per-source file readers/parsers live unexported, kept off-surface by absence from the `.fsi`. A new `surface/FS.GG.Governance.ReleaseFactsSensing.surface.txt` baseline is guarded by the reflective drift test plus a dependency scope guard. |
| III. Idiomatic Simplicity | PASS | The plainest F#: `deriveFacts` is a per-family `match` over `(expectation option, recovered Result)` building a `(kind, FactState)` pair and a snapshot field, assembled with `Map.ofList` and sorted collections; the edge is six `try`/`with`-guarded reads gathered into a record. No `mutable`, no custom operators, no SRTP, no reflection (outside tests), no type providers, no recursion-for-state, no non-trivial CEs. The one ordering helper (a dotted-numeric version compare) is a small total function, disclosed at its use site. |
| IV. Elmish/MVU boundary | PASS | The feature **is** an I/O-bearing sense, and it honors Principle IV via the established **sensing-port boundary**, not a full `Program` loop: I/O is represented as data behind a single injected `RepositoryPort` (the effect contract), the derivation (`deriveFacts`) is pure, and interpretation (`realPort`/`gather`/`senseRelease`) happens **only at the edge** — exactly the separation Principle IV requires and exactly what the sibling sensing features F016 `Snapshot` (`Ports`→`RawSensing`→`assemble`→`senseSnapshot`) and FreshnessSensing (`FreshnessSensor` record → pure `senseFreshness`) do. A full `Model`/`Msg`/`update` loop is unwarranted: this is a single-shot sense with no durable workflow state, no retries, no user interaction, and no background work (research D2). Semantic tests cover both sides of the boundary: pure-derivation tests over hand-built `RecoveredEvidence`, and interpreter tests executing `realPort` against a **real** temp fixture repository (Principle V). |
| V. Test Evidence | PASS | Semantic tests fail before the library exists and pass after, exercising the public FSI surface. The edge is exercised against a **real** temp fixture repository (real files read by `realPort`), the F016/FreshnessSensing precedent — no synthetic substitute for the filesystem. Determinism is proven by a genuine repeated sense asserting a structurally identical `SensedRelease` (SC-003); the no-fabrication guarantee by an evidence-removed/corrupted fixture asserting `Unrecoverable` not `Met` (SC-002); the network-free guarantee by the dependency scope guard (SC-004). The few fake-`RepositoryPort` unit tests of the pure core feed it its own real declared input (`RecoveredEvidence`), not a substitute for an unavailable dependency, so no `Synthetic` disclosure token is required; any fixture standing in for a real release artifact is disclosed per Principle V. |
| VI. Observability & Safe Failure | PASS | The feature fails safe by construction: an absent/unreadable/unparseable source or an absent expectation becomes the explicit `Unrecoverable` `FactState` with a `SensingDiagnostic` naming the family and reason (FR-004) — never a swallowed exception, a throw, or a silent `Met`. The diagnostics distinguish a missing/malformed **input** (absent file, unparseable source, unrecognized layout) from a tool defect, as Principle VI requires. Every family produces a visible fact and, where recovered, surfaced evidence; nothing is dropped (FR-009). |

**Change Classification**: **Tier 1 (contracted change)** — adds public API surface (a new
`ReleaseFactsSensing` library with three curated `.fsi` modules and a surface baseline). It introduces a
new library but **no** new third-party dependency, **no** schema, **no** schema-version bump, and **no**
edit to any frozen merged core or golden baseline. Requires the full artifact chain: spec, plan, `.fsi`,
surface baseline, test evidence, and docs (this plan + the design artifacts).

**Engineering Constraints**: net10.0 ✅; each new public module carries a curated `.fsi` ✅; a surface
baseline is added ✅; no new third-party dependency ✅ (FSharp.Core + the F053/F014 ProjectReferences);
`FS.GG.Governance.*` namespace ✅; the library layers on top of the merged thread, not into a frozen core
✅; the I/O-bearing sense honors the Elmish/MVU effects-boundary discipline via the injected port ✅
(research D2); one-way operating rule honored — the library assumes no rendering package id, template name,
target name, or layout; the governed identity is a caller-supplied F014 `SurfaceId`, and both the
expectations and the source layout are caller input (FR-011) ✅. No violations → **Complexity Tracking is
empty**.

## Project Structure

### Documentation (this feature)

```text
specs/054-release-facts-sensing/
├── plan.md              # This file (/speckit-plan command output)
├── spec.md              # Feature specification (input)
├── research.md          # Phase 0 output — the resolved decisions (D1–D8)
├── data-model.md        # Phase 1 output — entities, the port→recovered→derive flow, the F053 hand-off
├── quickstart.md        # Phase 1 output — build/exercise/test walkthrough (sense → feed F053)
├── contracts/
│   ├── Model.fsi          # NEW surface — expectations, layout, recovered evidence, snapshot, SensedRelease
│   ├── Sensing.fsi        # NEW surface — releaseFamilies, deriveFacts (the pure core)
│   └── Interpreter.fsi    # NEW surface — RepositoryPort, realPort, gather, senseRelease (the edge)
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/FS.GG.Governance.ReleaseFactsSensing/              # NEW (this row) — the release-facts sensing library
├── Model.fsi          # curated surface: ReleaseExpectations, SourceLayout, *Evidence, RecoveredEvidence,
│                       # *Fact, SensingDiagnostic, ReleaseSnapshot, SensedRelease (reuses F053 + F014 types)
├── Model.fs           # the domain declarations (no access modifiers)
├── Sensing.fsi        # curated surface: releaseFamilies, deriveFacts
├── Sensing.fs         # the pure per-family classify + snapshot build + Map.ofList over all six families;
│                       # the version/metadata/pin/posture comparison helpers live unexported
├── Interpreter.fsi    # curated surface: RepositoryPort, realPort, gather, senseRelease
├── Interpreter.fs     # the edge: realPort (System.IO reads + parse), gather (exception-reifying), senseRelease;
│                       # the per-source file readers/parsers live unexported
└── FS.GG.Governance.ReleaseFactsSensing.fsproj   # ProjectReferences: ReleaseRules, Config;
                                                   # compile order Model→Sensing→Interpreter (.fsi before .fs)

surface/
└── FS.GG.Governance.ReleaseFactsSensing.surface.txt  # NEW reflective baseline (generated via BLESS_SURFACE)

tests/FS.GG.Governance.ReleaseFactsSensing.Tests/      # NEW — US1 sense+classify, US2 snapshot, US3 fail-safe/
                                                       #   determinism, the F053 hand-off, the dependency scope
                                                       #   guard, an FsCheck six-families property; real temp-repo
                                                       #   fixtures for the edge + hand-built RecoveredEvidence for
                                                       #   the pure core

scripts/prelude.fsx                                    # + an F054 release-facts-sensing walkthrough section
FS.GG.Governance.sln                                   # + the new ReleaseFactsSensing src + test project entries

# Untouched (frozen): F053 ReleaseRules, F016 Snapshot, F014 Config, and every other core/golden/schema.
```

**Structure Decision**: Put the new sensing in **one small new library**
(`FS.GG.Governance.ReleaseFactsSensing`), the repo idiom (≈40 focused libraries) and the constitution's
"heavier capabilities layer on top, not into the core." It references **only** the two cores whose
vocabulary it reuses — F053 `ReleaseRules` (the facts/family/state types it produces) and F014 `Config`
(`SurfaceId`) — and deliberately **not** `Snapshot`/`Route`/`Gates`, which it only *mirrors* in shape, not
consumes. The three-module split (`Model` / `Sensing` / `Interpreter`) is the F016 Snapshot precedent
(`Model` / `Snapshot` / `Interpreter`): the impure edge isolated in one module behind one injected port, the
pure derivation in another, the vocabulary in a third. The host command and the `release.json` projection
that consume this sensing are separate following rows (FR-012), exactly the cadence the snapshot/route and
cache threads followed (sense a real boundary → pure core consumes it → host wiring → projection).

## Complexity Tracking

> No Constitution Check violations. This section is intentionally empty.
