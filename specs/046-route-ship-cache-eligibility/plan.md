# Implementation Plan: Emit Real Cache-Eligibility Verdicts From `fsgg route` and `fsgg ship`

**Branch**: `046-route-ship-cache-eligibility` | **Date**: 2026-06-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/046-route-ship-cache-eligibility/spec.md`

## Summary

Close the cache-eligibility thread end-to-end. F041–F045 are merged, but the two host commands consumers
actually run still pass `None` to the F045 embed, so `route.json` (F022 `fsgg route`) and `audit.json`
(F026 `fsgg ship`) emit the placeholder *not-evaluated* cache section. This row teaches **both** commands
to run the cache-eligibility pipeline F044 already proved — **sense** each selected gate's freshness facts
at the effects boundary → assemble F043 `SensedFacts` → F043 `resolve` → F041 `evaluate` over a
**read-only** F030 reuse store → pass the resulting **real** `CacheEligibilityReport` as `Some report` into
F045 — so both documents finally carry genuine per-gate `reusable` / `mustRecompute` verdicts.

The shared freshness-sensing edge (the `FreshnessSensor` / `StoreReader` ports, the real SHA-256 catalog /
source sensing, the read-only store deserializer, and the `SensedFacts` assembly) is **extracted into a new
shared edge library `FS.GG.Governance.FreshnessSensing`** (maintainer-confirmed this session, over
triplicating it). `RouteCommand` and `ShipCommand` reference the new library; **F044
`CacheEligibilityCommand` is left untouched** (FR-014, SC-008) with an explicit, bounded deferred follow-up
to converge its inline copy onto the shared library later.

Two spec invariants are load-bearing and drive the **one deliberate divergence from F044's interpreter**:

1. **Cache eligibility is information, not a verdict.** The wiring changes neither command's existing
   behavior beyond filling the cache section: `fsgg route` still always exits 0; `fsgg ship`'s pass/fail
   verdict, blockers/warnings/passing partition, every enforcement field, its `ExitCodeBasis`, and its
   numeric exit code are produced exactly as before — the `Emitted` → exit-from-basis mapping is untouched
   (FR-008, FR-009).
2. **Cache sensing introduces no new way to fail.** Where F044 (a *standalone* cache command) treats
   `FreshnessSensed (Error _)` / `StoreLoaded (Error _)` as a fatal `ToolError`, the route/ship pure
   `update` instead **degrades**: a freshness-sense failure substitutes an empty `SensedFacts` (every gate
   resolves *unresolved* ⇒ `notEvaluated`), a malformed store substitutes `EvidenceReuse.empty` (every gate
   ⇒ `mustRecompute noPriorEvidence`), each records a **non-fatal cache note** surfaced in the summary
   (FR-015), and the command continues to emit its document with its exit code unchanged (FR-010, FR-011).

The cache report is assembled in a pure `tryProject` join that fires once both the sensed facts and the
store have arrived (the exact F044 shape), then `ofRouteResult result (Some report)` /
`ofShipDecision decision (Some report)` replace the `None` callsites
(`RouteCommand/Loop.fs:250`, `ShipCommand/Loop.fs:288`). After this row both commands **always** pass
`Some report` (the cache step always runs), so `cacheEligibilityEvaluated` is always `true` in their
output. The F045 wire contract is unchanged — `route.json` stays `fsgg.route/v2`, `audit.json` stays
`fsgg.audit/v2`; only the section becomes populated (no schema bump, no projection-core edit, no re-bless
of the F045 / F028 golden baselines).

The contracts this row commits live in [contracts/FreshnessSensing.fsi](./contracts/FreshnessSensing.fsi)
(the new shared edge surface), [contracts/route-command-delta.md](./contracts/route-command-delta.md) and
[contracts/ship-command-delta.md](./contracts/ship-command-delta.md) (the two commands' new `Loop` /
`Interpreter` surfaces and the degrade rules); the embed vocabulary and the
sense→resolve→evaluate→embed join in [data-model.md](./data-model.md); the build / exercise / test /
re-bless walkthrough in [quickstart.md](./quickstart.md); and the ten decisions in
[research.md](./research.md).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (repo standard; `Nullable=enable`, `TreatWarningsAsErrors=true`
from `Directory.Build.props`). One **new** library (`FS.GG.Governance.FreshnessSensing`, a pure-`.fsi`
edge over BCL crypto + `System.IO`) and edits to the two existing host commands (`RouteCommand`,
`ShipCommand`), each a `Loop` (pure MVU core) + `Interpreter` (edge) pair.

**Primary Dependencies**: `ProjectReference`s only; **no new third-party `PackageReference`**. The new
`FreshnessSensing` references `Config` (F014 `CommandId`), `Gates` (F018 `Gate`), `FreshnessKey` (F029
`RuleHash`/`ArtifactHash`/`CommandVersion`/`GeneratorVersion`/`Revision`/`FreshnessInputs`),
`FreshnessResolution` (F043 `SensedFacts`), and `EvidenceReuse` (F030 `ReuseStore` + `empty` + the store
entry types). `RouteCommand` and `ShipCommand` each add references on `FreshnessSensing`, `CacheEligibility`
(F041 `evaluate` + Model types), `FreshnessResolution` (F043 `resolve`/`entries`/`candidate`),
`EvidenceReuse` (F030 `ReuseStore` + `empty`), and `FreshnessKey` (F029 `Revision`). Sensing uses only the
net10.0 shared-framework `System.Security.Cryptography` (SHA-256), `System.IO`, and `System.Text.Json`
(the store deserializer) the F044 interpreter already uses. Test frameworks unchanged (Expecto,
Expecto.FsCheck, FsCheck, Microsoft.NET.Test.Sdk, YoloDev.Expecto.TestSdk).

**Storage**: Read-only reuse store on disk (`fsgg.evidence-reuse-store/v1`), loaded via the new
`StoreReader`; absent ⇒ `EvidenceReuse.empty`. No store is **written, evicted, or expired** (out of scope).
The atomic temp+rename artifact writer the commands already own is reused unchanged.

**Testing**: Expecto + FsCheck. New `FS.GG.Governance.FreshnessSensing.Tests` exercises the real sensor and
`loadStore` against a temp directory (real bytes; absent / present / malformed store). The existing
`RouteCommand.Tests` and `ShipCommand.Tests` are extended: faked `Freshness` / `Store` ports (fixed literal
hashes — `Synthetic`, disclosed) drive the loop; the expected `route.json` / `audit.json` are recomputed
**live** through the public `resolve`→`candidate`→`evaluate` chain over those same faked facts and passed as
`Some expectedReport` (an empty faked store ⇒ every resolved gate `mustRecompute noPriorEvidence`). New
US3 tests cover the degrade paths (unsensed fact ⇒ `notEvaluated` + note, exit unchanged; malformed store ⇒
degrade-to-empty + note, exit unchanged) and the **SC-003 ship-invariant** (verdict / partition /
enforcement fields / `ExitCodeBasis` / numeric exit identical to the pre-wiring projection of the same
input). The F045 `RouteJson.Tests` / `AuditJson.Tests` and the F028 `EnforcementFixtures.Tests` generator
**stay on the `None` path, untouched** (SC-008).

**Target Platform**: Developer / CI .NET SDK running `dotnet test`. No OS-specific surface.

**Project Type**: One new pure-`.fsi` edge library + two edited Elmish/MVU host commands (Principle IV is
load-bearing — these are stateful I/O workflows). The cache wiring extends the existing MVU: sensing is
`Effect` data, the join is pure, interpretation is at the edge, and the degrade policy lives in the pure
`update`.

**Performance Goals**: N/A. The contracts are determinism, byte-stability, no-hide attribution, and the two
invariants — not latency. The added work is one freshness sense + one store read per run, then one `resolve`
+ one `evaluate` over the selected gates.

**Constraints**: Deterministic / byte-stable (FR-012, SC-007): identical repo state ⇒ byte-identical
documents including the cache section; cache verdicts follow each document's existing gate order (the F045
embed owns this). Information-not-verdict (FR-008, FR-009): no existing `route.json` / `audit.json` field,
severity, enforcement, partition, route trace, finding, cost, ship verdict, or exit code changes (modulo
the now-populated section). No-new-failure (FR-010, FR-011): unsensed facts ⇒ `notEvaluated` (gate dropped
from candidates) + named missing facts in the summary; malformed store ⇒ empty store; neither changes the
exit code; both surface a non-fatal note (no swallowed failure — Principle VI). No derivation (FR-013): the
commands compute no freshness key / hash / cache decision, render no raw freshness input, and never
dereference the opaque evidence reference. F044/F042 untouched (FR-014, FR-015 sidecar).

**Scale/Scope**: One new library (`FreshnessSensing.fsi/.fs` + `.fsproj` + surface baseline + a focused
test project); edits to four files across the two commands (`RouteCommand/Loop.fs` + `Interpreter.fs`/`.fsi`,
`ShipCommand/Loop.fs` + `Interpreter.fs`/`.fsi`) plus the matching `Loop.fsi` updates and two `.fsproj`
reference blocks; the two command surface baselines re-blessed; the two command test projects extended
(faked sensing ports + recomputed expected docs + degrade/invariant tests); a short `scripts/prelude.fsx`
F046 section; the `CLAUDE.md` plan pointer. **Zero** edits to F041/F042/F043/F045 or F044, **zero** new
third-party dependencies, **zero** re-bless of F045/F028 golden baselines, **no** schema bump.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | The new `FreshnessSensing.fsi` and the extended command `Loop.fsi`/`Interpreter.fsi` are drafted and exercised in a new `scripts/prelude.fsx` F046 section (a faked-port loop run projecting a real `Some report`) before any `.fs` body changes; semantic tests drive the public `Interpreter.run` / `Loop.update`, never private helpers. |
| II. Visibility in `.fsi` | **PASS** | `FreshnessSensing` ships a curated `.fsi` (the sensor/reader ports + `realSensor`/`realStoreReader`/`senseFreshness`/`loadStore`); the commands' new `Effect`/`Msg`/`Model`/`RunRequest`/`Ports` members are added to their existing `.fsi`. Three surface baselines (`FreshnessSensing`, `RouteCommand`, `ShipCommand`) are guarded by the existing `SurfaceDrift` tests and re-blessed with `BLESS_SURFACE=1`. RouteJson/AuditJson surfaces are **unchanged**. |
| III. Idiomatic Simplicity | **PASS** | A pure `tryProject` join (`match model.Sensed, model.Store with Some, Some -> …`), `List.choose candidate`, exhaustive wildcard-free `match`es over `Result`, and the degrade substitutions are plain F# — the exact F044 shape. No SRTP, reflection, custom operators, type providers, or non-trivial CEs. The one structural addition (a shared edge library) reduces duplication, not adds cleverness. |
| IV. Elmish/MVU is the boundary for stateful/I/O | **PASS (load-bearing)** | Both commands ARE MVU host edges; this row extends them correctly — sensing/store-load are new `Effect`s, the result `Msg`s feed pure `update`, the cache join and the **degrade policy** are pure (testable as value transitions), and interpretation stays at the edge against fakeable ports. The new library is the I/O edge for sensing. |
| V. Test Evidence Is Mandatory | **PASS** | Real `RouteResult`/`ShipDecision`, real F041 `evaluate` over real F030 `ReuseStore`/F043 `SensedFacts`; the new library's sensor is tested against a real temp directory. The command tests' faked sensor uses fixed literal hashes — **`Synthetic`**, disclosed at the use site and in the PR (Principle V). Tests fail before the wiring carries `Some report` and pass after; the SC-003 invariant test fails if any non-cache byte / the exit code drifts. |
| VI. Observability & Safe Failure | **PASS** | The degrade paths **emit** a non-fatal cache note (no swallowed failure) and distinguish missing/malformed input (unsensed facts, absent/malformed store ⇒ recompute-by-default) from a genuine defect; fatal command failures (git/catalog/write) keep their existing categories. The no-hide guarantee (every `mustRecompute` names its cause; unresolved ⇒ `notEvaluated`, never silent `reusable`) is preserved by the F045 embed and the summary. |
| Change Classification | **Tier 1 (contracted change — new project + new public dependency + new public surface on two commands)** | Adds a new library and `ProjectReference`s; extends `RouteCommand`/`ShipCommand` `Loop` (`Effect`/`Msg`/`Model`/`RunRequest.StorePath`) and `Interpreter` (`Ports.Freshness`/`Ports.Store`) public surfaces ⇒ full chain: spec, plan, `.fsi`, three surface baselines, tests, docs. **No third-party dependency. No wire-contract change** (route/v2, audit/v2 unchanged — the section is merely populated). F041/F042/F043/F045 **and F044** consumed verbatim and unedited (SC-008). |
| Engineering Constraints | **PASS** | F#/.NET `net10.0`; only `ProjectReference`s added, no new third-party `PackageReference`; `.fsi` curated per module; surface baselines updated; compatibility captured in the contracts (the section flips from *not-evaluated* to *evaluated*; same v2 schema). Genericity preserved — sensing assumes no rendering paths/IDs; `src/**` + `.fsgg/*.yml` are this repo-shape's MVP coverage carried verbatim from F044, a documented later refinement. Pack output unaffected (`RouteCommand` stays the packable `fsgg` tool; the new library is `IsPackable=false`). |

**Gate result: PASS — no unjustified violations. Complexity Tracking is empty.** The two structural choices
(a shared sensing library; the degrade-in-`update` policy that diverges from F044) are maintainer-confirmed
and from-the-spec respectively, each adds only a small, well-patterned amount of code, and both map to
concrete contracts and tests. Every from-the-spec invariant (information-not-verdict, no-new-failure,
no-hide, determinism, F044/F042 untouched, no schema bump) maps to a render/transition rule and a test.

## Project Structure

### Documentation (this feature)

```text
specs/046-route-ship-cache-eligibility/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 — decisions D1–D10 (new shared FreshnessSensing lib; degrade-not-fail in the
│                        #            pure update; always Some report; pure tryProject join; keep snapshot for
│                        #            base/head; mirror F044 --store + default; non-fatal CacheNotes in summary;
│                        #            MVU phase additions; test strategy; no schema bump / F044+F045+F028 untouched)
├── data-model.md        # Phase 1 — the sense→resolve→evaluate→embed join, the FreshnessSensing surface, the two
│                        #            commands' enriched Model/Msg/Effect, the degrade substitutions + invariants, laws
├── quickstart.md        # Phase 1 — build, FSI-exercise both commands with faked ports (Some report + degrade),
│                        #            test, re-bless the three surface baselines, confirm SC-003/SC-004/SC-007
├── contracts/           # Phase 1 — the contracts this row commits
│   ├── FreshnessSensing.fsi        # the new shared edge surface (FreshnessSensor/StoreReader/realSensor/realStoreReader/senseFreshness/loadStore)
│   ├── route-command-delta.md      # RouteCommand Loop/Interpreter surface delta + the degrade rules + the Some-report join
│   └── ship-command-delta.md       # ShipCommand Loop/Interpreter surface delta + the SC-003 invariant + the Some-report join
└── tasks.md             # Phase 2 — /speckit-tasks output (NOT created here)
```

### Source Code / deliverable layout (repository root)

```text
src/FS.GG.Governance.FreshnessSensing/                  # NEW shared edge library (maintainer-confirmed D1)
├── FreshnessSensing.fsi                                # NEW — FreshnessSensor/StoreReader ports; realSensor/realStoreReader; senseFreshness (SensedFacts assembly); loadStore (absent⇒empty, malformed⇒Error)
├── FreshnessSensing.fs                                 # NEW — real SHA-256 catalog/src sensing + store deserializer (the F044 logic, now shared); NO third-party dep
└── FS.GG.Governance.FreshnessSensing.fsproj            # NEW — IsPackable=false; refs Config, Gates, FreshnessKey, FreshnessResolution, EvidenceReuse

src/FS.GG.Governance.RouteCommand/
├── Loop.fsi                                            # EDIT — new Effect (SenseFreshness/LoadStore), Msg (FreshnessSensed/StoreLoaded), Model (Snapshot/SelectedGates/Sensed/Store/CacheNotes), RunRequest.StorePath; doc the always-Some-report + degrade
├── Loop.fs                                             # EDIT — Loaded(Valid) emits SenseFreshness+LoadStore; pure tryProject join builds the report; ofRouteResult result (Some report); degrade FreshnessSensed/StoreLoaded Errors (no fail); render cache notes
├── Interpreter.fsi                                     # EDIT — Ports gains Freshness/Store; realPorts wires FreshnessSensing.realSensor/realStoreReader
├── Interpreter.fs                                      # EDIT — step handles SenseFreshness (FreshnessSensing.senseFreshness) + LoadStore (FreshnessSensing.loadStore); realPorts wiring
└── FS.GG.Governance.RouteCommand.fsproj               # EDIT — add refs: FreshnessSensing, CacheEligibility, FreshnessResolution, EvidenceReuse, FreshnessKey

src/FS.GG.Governance.ShipCommand/
├── Loop.fsi                                            # EDIT — same Effect/Msg/Model/RunRequest additions; Emitted exit-from-basis UNCHANGED
├── Loop.fs                                             # EDIT — Loaded(Valid) emits SenseFreshness+LoadStore; join builds report; ofShipDecision decision (Some report); degrade; render cache notes; exit mapping untouched
├── Interpreter.fsi                                     # EDIT — Ports gains Freshness/Store; realPorts wiring
├── Interpreter.fs                                      # EDIT — step handles SenseFreshness + LoadStore via FreshnessSensing
└── FS.GG.Governance.ShipCommand.fsproj                # EDIT — add refs: FreshnessSensing, CacheEligibility, FreshnessResolution, EvidenceReuse, FreshnessKey

src/FS.GG.Governance.CacheEligibilityCommand/          # UNTOUCHED this row (FR-014); converge onto FreshnessSensing is a deferred bounded follow-up (research D1)

tests/FS.GG.Governance.FreshnessSensing.Tests/         # NEW — realSensor over a temp dir (catalog/src bytes); loadStore absent⇒empty / present⇒parse / malformed⇒Error
tests/FS.GG.Governance.RouteCommand.Tests/             # EDIT — faked Freshness/Store ports; recompute expected route.json with Some report (Support.fs:259, LoopTests.fs:50); + US3 degrade tests; surface re-bless
tests/FS.GG.Governance.ShipCommand.Tests/              # EDIT — faked ports; recompute expected audit.json with Some report (Support.fs:265, LoopTests.fs:50); + US3 degrade tests + SC-003 ship-invariant; surface re-bless
tests/FS.GG.Governance.RouteJson.Tests/                # UNTOUCHED (stays None — F045's own tests, SC-008)
tests/FS.GG.Governance.AuditJson.Tests/                # UNTOUCHED (stays None — F045's own tests, SC-008)
tests/FS.GG.Governance.EnforcementFixtures.Tests/      # UNTOUCHED (Generator.fs stays None; F028 golden audit.json snapshots unaffected, SC-008)

surface/FS.GG.Governance.FreshnessSensing.surface.txt  # NEW — blessed for the new public edge (BLESS_SURFACE=1)
surface/FS.GG.Governance.RouteCommand.surface.txt      # RE-BLESS — new Effect/Msg/Model/RunRequest/Ports members (BLESS_SURFACE=1)
surface/FS.GG.Governance.ShipCommand.surface.txt       # RE-BLESS — same (BLESS_SURFACE=1)
surface/FS.GG.Governance.RouteJson.surface.txt         # UNCHANGED
surface/FS.GG.Governance.AuditJson.surface.txt         # UNCHANGED
scripts/prelude.fsx                                    # EDIT — append a short F046 FSI section (faked-port loop run; assert Some report + a degrade path)
FS.GG.Governance.sln                                   # EDIT — add FreshnessSensing + its test project
CLAUDE.md                                              # EDIT — point the SPECKIT plan reference at this plan
```

**Structure Decision**: One new shared edge library, two edited host commands. The freshness-sensing edge
(`FreshnessSensor`/`StoreReader` ports + real SHA-256 catalog/source sensing + the read-only store
deserializer + the `SensedFacts` assembly) is **extracted once** into `FS.GG.Governance.FreshnessSensing`
and referenced by `RouteCommand` and `ShipCommand`, rather than duplicated into each (maintainer-confirmed
D1). The cache report is built in a pure `tryProject` join mirroring F044, then handed to the F045 embed as
`Some report`. The single deliberate divergence from F044 — degrade-not-fail on sense/store errors — lives
in the pure `update` so it is testable as a value transition, satisfying the spec's no-new-failure
invariant for the safety-critical commands. F044 keeps its inline copy this row (FR-014); converging it onto
the shared library is an explicit, bounded deferred follow-up.

## Complexity Tracking

> No Constitution Check violations. This section is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
