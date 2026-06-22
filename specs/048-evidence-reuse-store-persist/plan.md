# Implementation Plan: Persist The Evidence-Reuse Store To Disk From The Host Commands

**Branch**: `048-evidence-reuse-store-persist` | **Date**: 2026-06-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/048-evidence-reuse-store-persist/spec.md`

## Summary

F047 delivered the **pure write half** of the evidence-reuse store lifecycle as a Model-free library
(`FS.GG.Governance.EvidenceReuseStore`: `serialise`, `retain`, `prune` — deterministic, total, no I/O), but
that library is referenced by nothing. The store the cache thread reads (`fsgg.evidence-reuse-store/v1`) is
loaded read-only by `fsgg route` and `fsgg ship` (F046 `FreshnessSensing.loadStore`/`realStoreReader`) and is
**never written**. This row delivers the **impure on-disk edge**: it wires F047's `prune` → `retain` →
`serialise` into the two host commands that already load the store, behind an explicit opt-in flag, and writes
the result atomically (the existing temp-write-then-rename `writeAtomic` port) back to the discovered store
path.

The wiring is a **new effect on each command's existing MVU boundary** (RouteCommand `Loop`, ShipCommand
`Loop`), not a new command and not a refactor of the cache cores:

1. A new opt-in field `PersistStore: bool` on each `RunRequest` (parsed from a new `--persist-store` flag,
   default **off**), so with the default the host writes no store file and every emitted artifact and golden
   baseline is **byte-identical** (FR-004, SC-006).
2. A new pure transition: when persistence is enabled, the `update` function derives the persisted document
   from the **loaded** store value (`model.Store`) via `EvidenceReuseStore.serialise (retain
   defaultRetentionBound (prune loaded))` and emits a new `PersistStore(path, content)` effect. **All
   decision logic — whether to write, what to write, the don't-clobber-a-malformed-file rule — lives in the
   pure `update`** (FR-010); only the byte write runs at the edge.
3. A new effect `PersistStore of path: string * content: string` interpreted at the edge by reusing the
   existing atomic `ports.Write` (the same `writeAtomic` temp+rename that already persists `route.json` /
   `audit.json`), reified to a new **non-fatal** `StorePersisted of Result<unit, string>` message (FR-001).

The store write is **decoupled from the current run's verdicts** (FR-005): the per-gate `reusable` /
`mustRecompute` verdicts embedded in `route.json` / `audit.json` are computed from the store **as loaded**
(the existing `CacheEligibility.evaluate candidates store` join is untouched); the prune/retain/serialise feeds
only the next run's persisted file. The store write introduces **no new failure mode** (FR-006): unlike the
existing `WriteArtifact`→`Wrote` path (whose `Error` maps to `ToolError`/exit 4), `StorePersisted(Error _)`
appends a non-fatal cache note and **never** changes the exit code, the emitted artifacts, or — for ship — the
verdict. A failed store write leaves no partial file (atomic rename) and the malformed-on-load store is **not**
clobbered (the pure transition suppresses the write when the load degraded).

The change is **additive** (FR-009, SC-007): it adds two `ProjectReference`s (RouteCommand → EvidenceReuseStore,
ShipCommand → EvidenceReuseStore), three new public cases on each command's `Loop.fsi`
(`PersistStore` effect, `StorePersisted` msg, `PersistStore` request field), updates the two command surface
baselines, edits **zero** F029–F047 cores, re-blesses **zero** route/audit golden baselines, bumps **no** schema
version, leaves the read-only reader's accepted shape untouched (it now consumes this row's output unmodified),
and adds **no** third-party dependency.

The committed contracts (the two `Loop.fsi` deltas) live in
[contracts/](./contracts/); the persisted-document derivation and the new MVU transitions in
[data-model.md](./data-model.md); the build / exercise / test walkthrough in
[quickstart.md](./quickstart.md); and the resolved decisions in [research.md](./research.md).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (repo standard; `Nullable=enable`, `TreatWarningsAsErrors=true` from
`Directory.Build.props`). This row touches two existing **host command** libraries that already carry a local
MVU/effect boundary (`Loop` pure transition + `Interpreter` edge): `FS.GG.Governance.RouteCommand` and
`FS.GG.Governance.ShipCommand`. No new command, no new library.

**Primary Dependencies**: `ProjectReference`s only; **no new third-party `PackageReference`**. Two new project
references are added: `RouteCommand → EvidenceReuseStore` and `ShipCommand → EvidenceReuseStore` (the F047 pure
core, currently referenced by nothing). Both commands already reference `EvidenceReuse` (F030),
`FreshnessSensing` (F046), `CacheEligibility` (F041), and `FreshnessResolution` (F043). The atomic write reuses
each Interpreter's existing `writeAtomic` (System.IO `File.WriteAllText` + `File.Move(tmp, path, true)`) — a
net10.0 shared-framework mechanism already in the two interpreters. Test frameworks unchanged (Expecto,
Expecto.FsCheck, FsCheck, Microsoft.NET.Test.Sdk, YoloDev.Expecto.TestSdk).

**Storage**: The on-disk `fsgg.evidence-reuse-store/v1` document at the discovered store path (`--store`,
default `<repo>/readiness/evidence-reuse.json`) — until now read-only, now also an atomic write target when
persistence is enabled. No schema change; the write target is exactly the shape
`FreshnessSensing.realStoreReader` already accepts.

**Testing**: Expecto + FsCheck. The persistence **decision** is exercised as pure `update` transitions in the
existing `FS.GG.Governance.RouteCommand.Tests` and `FS.GG.Governance.ShipCommand.Tests` (given `Model` + `Msg`,
assert the emitted `PersistStore` effect content and that `StorePersisted(Error _)` changes neither `Exit` nor
the emitted artifacts) — **no filesystem access** (SC-008). The **atomic write** is exercised at the
effects boundary by driving the real `Interpreter.run` with `realPorts` against a temp repo, then re-reading
the persisted file through the **real** `FreshnessSensing.realStoreReader` and asserting the loaded store
round-trips (SC-001), is byte-identical across two runs (SC-002), is bounded + pruned with every surviving
entry byte-for-byte a loaded entry (SC-003), and that an induced write failure (unwritable target) leaves the
exit code, `route.json`/`audit.json`, and (ship) verdict unchanged with no partial file and a non-fatal note
(SC-005). A persistence-off run asserts byte-identity with the pre-row baseline (SC-006). Evidence references
in fixtures are disclosed synthetic literals (`Synthetic` token, Principle V) — real evidence needs gate
execution (Out of Scope). The two command surface baselines are guarded by the existing reflective
surface-drift tests.

**Target Platform**: Developer / CI .NET SDK running `dotnet test`. No OS-specific surface; the atomic rename
relies on `File.Move(overwrite: true)`, already used by the merged route/ship interpreters.

**Project Type**: Two existing CLI host-command libraries extended at their MVU boundary. **Principle IV
applies and is satisfied** (this is I/O-bearing, stateful workflow): the persistence decision is a pure `update`
transition emitting a `PersistStore` effect, interpreted only at the `Interpreter` edge — exactly the
`Model`/`Msg`/`Effect`/pure-`update`/edge-interpreter contract the two commands already implement.

**Performance Goals**: N/A. The added cost is one `prune`+`retain`+`serialise` pass over the loaded entry list
and one atomic file write per enabled run. The contracts are determinism, byte-stability, lossless round-trip,
decoupling from verdicts, and the no-new-failure-mode guarantee — not latency.

**Constraints**: Opt-in / default-off (FR-004): the `--persist-store` flag defaults off; with the default,
zero store writes and byte-identical artifacts + golden baselines. Decoupled (FR-005): verdicts come from the
**loaded** store; the persisted document is derived independently and never feeds the current run. Non-fatal
(FR-006): `StorePersisted(Error _)` never changes exit code, never alters emitted artifacts, never flips a ship
verdict; it appends a cache note. Atomic (FR-001): reuse `writeAtomic` (temp + rename) — a failed write leaves
no partial/truncated file. Verbatim reuse (FR-002, FR-003, FR-008): the document is `EvidenceReuseStore.serialise`
output exactly; bounding/pruning are F047's `retain`/`prune` exactly; no new reuse policy, freshness-match rule,
evidence representation, serialisation format, or schema version. Additive (FR-009, SC-007): no schema bump, no
reader-shape change, zero edits to F029–F047 cores or their golden baselines, no new third-party dependency.

**Scale/Scope**: Edits confined to four files across two libraries —
`RouteCommand/Loop.fsi`+`Loop.fs`+`Interpreter.fs`+`.fsproj` and the mirror four in `ShipCommand` — plus the
argv parser in each `Loop.fs`, the two surface baselines, new tests in the two existing `*.Tests` projects, a
`scripts/prelude.fsx` section, and the `CLAUDE.md` plan pointer. **Zero** edits to F029/F030/F041–F047 cores or
their baselines, **zero** new third-party dependencies, **zero** route/audit golden-baseline re-bless, **no**
schema bump.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — still PASS.*

| Principle | Status | Justification |
|-----------|--------|---------------|
| I. Spec → FSI → Semantic Tests → Implementation | PASS | FSI-first is satisfied by committing the two `Loop.fsi` deltas (`contracts/`) **before any `.fs` edit** and writing the public-surface semantic tests (Phase 3–5, driving `Loop.update` / `Interpreter.run`, never private helpers) so they fail before implementation. The `scripts/prelude.fsx` walkthrough (Phase 6 / T031) is the **documentation-of-record FSI transcript** — the durable, runnable honest-audience exercise of the shipped surface — not the design-time sketch (the `.fsi` deltas are that). Scheduling the prelude in Polish therefore does not weaken the FSI-before-`.fs` ordering, which the contracts + failing tests already enforce. |
| II. Visibility lives in `.fsi` | PASS | The new `PersistStore` effect, `StorePersisted` msg, and `PersistStore` request field are declared in each command's curated `Loop.fsi`; the `.fs` carries no access modifiers; the two `surface/FS.GG.Governance.{Route,Ship}Command.surface.txt` baselines are updated additively and guarded by the existing reflective drift tests. |
| III. Idiomatic Simplicity | PASS | Plain additions: one record field, one effect case, one msg case, one `update` branch composing `prune >> retain >> serialise`, one interpreter arm reusing `ports.Write`. No custom operators, SRTP, reflection (outside tests), type providers, or non-trivial CEs. |
| IV. Elmish/MVU boundary | PASS (applies) | I/O-bearing/stateful: the persistence **decision** (whether/what to write, don't-clobber-malformed) is a pure `update` transition emitting a `PersistStore` effect (FR-010); the byte write executes only in `Interpreter.step` at the edge and is reified back as `StorePersisted`. Both commands already expose `Model`/`Msg`/`Effect`/pure-`update`/edge-interpreter; this extends that boundary, not bypasses it. |
| V. Test Evidence | PASS | Pure-transition tests (no I/O) assert the emitted effect content and the non-fatal degrade; effects-boundary tests drive the **real** interpreter + **real** `FreshnessSensing` reader against a temp file and fail before the wiring exists. Synthetic evidence references are disclosed (`Synthetic` token, listed in the PR). |
| VI. Observability & Safe Failure | PASS | A store-write failure degrades **explicitly** to a non-fatal cache note (no silent swallow, no exit-code change); the malformed-on-load store is not silently clobbered. Distinguishes a write failure (noted) from a verdict/tool failure (unchanged), per the principle's defect-vs-bad-input rule. |

**Change Classification**: **Tier 1 (contracted change)** — modifies public API surface (new flag = observable
CLI behavior; new public `Effect`/`Msg`/`RunRequest` cases on two `Loop.fsi` modules). Requires the full
artifact chain: spec, plan, `.fsi` updates, the two surface-baseline updates, and test evidence. No third-party
dependency is added; no schema version bumps.

**Engineering Constraints**: net10.0 ✅; each edited public module keeps a curated `.fsi` ✅; the two surface
baselines updated ✅; no new dependency ✅ (System.IO + the F047 core are already on-graph or in shared
framework); `FS.GG.Governance.*` namespace ✅; pack output unaffected ✅; one-way operating rule unaffected
(no rendering coupling) ✅. No violations → **Complexity Tracking is empty**.

## Project Structure

### Documentation (this feature)

```text
specs/048-evidence-reuse-store-persist/
├── plan.md              # This file (/speckit-plan command output)
├── spec.md              # Feature specification (input)
├── research.md          # Phase 0 output — the resolved decisions
├── data-model.md        # Phase 1 output — persisted-document derivation + MVU transitions
├── quickstart.md        # Phase 1 output — build/exercise/test walkthrough
├── contracts/
│   ├── RouteCommand.Loop.fsi.delta   # Phase 1 output — the new public cases on route's Loop
│   └── ShipCommand.Loop.fsi.delta    # Phase 1 output — the new public cases on ship's Loop
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/FS.GG.Governance.RouteCommand/                 # EDITED (this row)
├── Loop.fsi                # + PersistStore effect, + StorePersisted msg, + RunRequest.PersistStore field
├── Loop.fs                 # + --persist-store parse, + pure persist transition (prune>>retain>>serialise), + non-fatal StorePersisted handling
├── Interpreter.fs          # + PersistStore arm reusing existing writeAtomic via ports.Write
└── FS.GG.Governance.RouteCommand.fsproj   # + ProjectReference EvidenceReuseStore

src/FS.GG.Governance.ShipCommand/                  # EDITED (this row) — mirror of RouteCommand
├── Loop.fsi
├── Loop.fs
├── Interpreter.fs
└── FS.GG.Governance.ShipCommand.fsproj

surface/
├── FS.GG.Governance.RouteCommand.surface.txt      # UPDATED additively (new public cases/field)
└── FS.GG.Governance.ShipCommand.surface.txt       # UPDATED additively

tests/FS.GG.Governance.RouteCommand.Tests/         # EDITED — new persistence tests
├── PersistenceTransitionTests.fs   # NEW: pure update — effect content, decoupling, non-fatal degrade (SC-004/005/008)
├── PersistenceEdgeTests.fs         # NEW: real Interpreter.run + real reader round-trip, bounded/pruned, induced failure (SC-001/002/003/005)
└── (existing tests assert persistence-off byte-identity — SC-006)

tests/FS.GG.Governance.ShipCommand.Tests/          # EDITED — mirror persistence tests (incl. verdict/exit-code invariance, SC-004)

scripts/prelude.fsx                                 # + a persistence walkthrough section

# Untouched (additive guarantee): src/FS.GG.Governance.EvidenceReuseStore (F047, now referenced),
# src/FS.GG.Governance.EvidenceReuse (F030), src/FS.GG.Governance.FreshnessSensing (F046) + its reader,
# all F041–F047 cores, route.json/audit.json golden baselines, the F042/F044 cache-eligibility sidecar.
```

**Structure Decision**: Extend the **two existing host commands** at their established MVU boundary rather than
add a new command or a shared writer module. This mirrors exactly how F046 wired the **read** side into the same
two `Loop`/`Interpreter` pairs, keeps the F047 core frozen (referenced, never edited), and reuses each
interpreter's existing atomic `writeAtomic` port. The dedicated `fsgg cache-eligibility` command (F044) does
**not** gain the write (it does not own the store lifecycle in the route/ship sense; mirroring F046's two-command
scope — see [research.md](./research.md) D7). The three `writeAtomic` copies are deliberately **not** refactored
into a shared module here (that would touch CacheEligibilityCommand and exceeds this row's additive scope — D8).

## Complexity Tracking

> No Constitution Check violations. This section is intentionally empty.
