# Implementation Plan: Capture A Real Evidence Reference From An Executed Gate

**Branch**: `049-evidence-reference-capture` | **Date**: 2026-06-24 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/049-evidence-reference-capture/spec.md`

## Summary

The cache/evidence-reuse thread (F029–F048) can now load, evaluate, bound, prune, serialise, and persist the
evidence-reuse store across runs — but **the store never gains a new entry**. `EvidenceReuse.record` (F030) is
called by nobody in the host path, and every `EvidenceRef` that exists today is a disclosed `Synthetic` literal
(Principle V). Nothing turns an *actually-executed* gate into a real, reproducible evidence reference, so the
persisted store can only ever *shrink* (prune/retain) and a future run's reuse decision can only match a
hand-written test world.

This row delivers the **pure capture core** — a new value-only library `FS.GG.Governance.EvidenceCapture` — that
bridges the two values already in hand: it derives a reproducible `EvidenceRef` (F030) from an *already-executed*
gate's `CommandRecord` (F032), and folds that reference into a supplied `ReuseStore` against the gate's resolved
`FreshnessInputs` (F029/F043) by reusing the F030 `record` convention **verbatim**. It mirrors how F047 delivered
the pure write half before F048 wired it; the **impure** edge (actually running gates inside `fsgg route` /
`fsgg ship`, sensing each output digest, building the `CommandRecord`, and recording during a run) is the
**following** row and is explicitly out of scope here.

The whole library is **two pure, total functions** over existing vocabulary:

1. `referenceOf : CommandRecord -> EvidenceRef` — the reproducible reference derivation. The reference string is
   exactly the F032 reproducible identity, wrapped: `EvidenceRef (CommandRecord.identityValue
   (CommandRecord.canonicalId record))`. Because `canonicalId` is computed only over `record.Reproducible` and
   **never reads `record.Duration`** (F032 D2), the derived reference is duration-invariant (FR-002, US2) and
   injective over the reproducible facts (FR-003, US2). It hashes no bytes — F032 already established the
   byte-stable canonical rendering; this row reuses it rather than inventing a second identity scheme (spec
   Assumption "the reference is the F032 reproducible identity, wrapped").
2. `capture : FreshnessInputs -> CommandRecord -> ReuseStore -> ReuseStore` — the store fold. It is exactly
   `EvidenceReuse.record inputs (referenceOf record) store`: newest-first, store in / store out, no new policy,
   no new store or evidence representation (FR-004). After capturing world `W`, `EvidenceReuse.decide W` over the
   result returns `Reuse (referenceOf record)` — the close-the-loop round-trip (FR-005, US1) — and every other
   world's verdict is exactly what F030 already decides (recompute-safety, FR-006, US3).

The library introduces **no new type** (it reuses `EvidenceRef`, `ReuseStore`, `CommandRecord`, and
`FreshnessInputs` verbatim), so it needs no `Model.fs(i)` — only `EvidenceCapture.fsi` + `EvidenceCapture.fs`.
It references **only** F030 `EvidenceReuse` and F032 `CommandRecord`; F029 `FreshnessKey` and F014 `Config`
arrive transitively. It adds **no** third-party dependency, bumps **no** schema version, edits **no** existing
core, host command, or golden baseline, and is **referenced by nothing on landing** (exactly as F047 was).

The committed contract (`EvidenceCapture.fsi`) lives in [contracts/](./contracts/); the reference-derivation and
store-fold semantics in [data-model.md](./data-model.md); the build / exercise / test walkthrough in
[quickstart.md](./quickstart.md); and the resolved decisions in [research.md](./research.md).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (repo standard; `Nullable=enable`, `TreatWarningsAsErrors=true` from
`Directory.Build.props`). This row adds one new **pure value-only library**, `FS.GG.Governance.EvidenceCapture`,
in the same packable shape as F030 `EvidenceReuse` and F047 `EvidenceReuseStore`. No new command, no MVU
boundary, no host edit.

**Primary Dependencies**: `ProjectReference`s only; **no new third-party `PackageReference`**. The library
references exactly two on-graph projects: `FS.GG.Governance.EvidenceReuse` (F030 —
`record`/`decide`/`EvidenceRef`/`ReuseStore`/`Model`) and `FS.GG.Governance.CommandRecord` (F032 —
`canonicalId`/`identityValue`/`CommandRecord`). `FS.GG.Governance.FreshnessKey` (F029 — `FreshnessInputs`) and
`FS.GG.Governance.Config` (F014) arrive **transitively** through F030/F032. Its own code is BCL + FSharp.Core
only — it builds no string, hashes no bytes, parses nothing (F032's `canonicalId` already did the rendering).
Test frameworks unchanged (Expecto, Expecto.FsCheck, FsCheck, Microsoft.NET.Test.Sdk, YoloDev.Expecto.TestSdk).

**Storage**: N/A. This core reads and writes no file. It consumes two in-memory values (a `CommandRecord` and a
`FreshnessInputs`) plus a `ReuseStore` value, and returns a `ReuseStore` value. The persistence round-trip
(FR-010, SC-007) is exercised by *re-using* the already-merged F047 `serialise` + F046 `realStoreReader` against
a store this core grew — no new persistence is introduced here.

**Testing**: Expecto + FsCheck, in a new `FS.GG.Governance.EvidenceCapture.Tests` project mirroring the F047
test project's references (EvidenceCapture, EvidenceReuse, CommandRecord, FreshnessKey, Config, and —
**for the persistence round-trip only** — EvidenceReuseStore + FreshnessSensing). The reference and capture tests run with **no**
filesystem, clock, process, or network access (SC-008): build a `CommandRecord` and `FreshnessInputs` in
memory, call `referenceOf` / `capture`, and assert over the returned values and `EvidenceReuse.decide`. The
round-trip test writes the serialised grown store to a temp file and re-reads it through the real F046
`realStoreReader` — whose only public load path takes a **file path** (`StoreReader = string ->
Result<ReuseStore option, string>`) — exactly as F047's own `readBack` helper does. The **capture core itself**
still performs no I/O: SC-008 ("no I/O in the core") covers the two pure operations; the temp file is F046
reader harness, not the core. Evidence references in tests are **derived**, not synthetic literals — that is
the whole point of this row — so the `Synthetic` token is not needed for the capture path. The new public
surface is guarded by a reflective surface-drift baseline (`surface/FS.GG.Governance.EvidenceCapture.surface.txt`).

**Target Platform**: Developer / CI .NET SDK running `dotnet test`. No OS-specific surface; no I/O at all.

**Project Type**: A single new pure value-only library. **Principle IV does not apply** (no state, no I/O, no
multi-step workflow): the entire public surface is two pure total functions, exactly as F030 `EvidenceReuse` and
F047 `EvidenceReuseStore` are — those rows also carried no MVU ceremony.

**Performance Goals**: N/A. The added cost is one `canonicalId` projection (already O(record) and merged) plus
one F030 `record` cons per capture. The contracts are determinism, byte-stability, duration-invariance,
injectivity over the reproducible identity, recompute-safety, and the lossless persistence round-trip — not
latency.

**Constraints**: Reuse-the-identity (FR-002, Assumption): the reference is `identityValue (canonicalId record)`
wrapped — no second identity scheme, no hashing here. Duration-invariant (FR-002, US2): `referenceOf` never
reads `record.Duration`; structurally it cannot, since `canonicalId` projects only `record.Reproducible`.
Injective (FR-003, US2): any single reproducible-fact perturbation changes the reference, inherited verbatim from
F032's injective `canonicalId`. Verbatim fold (FR-004): `capture` is `EvidenceReuse.record inputs (referenceOf
record)` exactly — no new reuse policy, store representation, or evidence representation. Close-the-loop (FR-005):
`decide` over the captured store serves the derived reference for the captured world. Recompute-safe (FR-006):
only the just-captured world becomes reusable; every other world's verdict is unchanged. Pure/total (FR-007):
defined for the empty store, empty digests, a failed exit code, and every captured-output outcome; never
throwing; no clock/fs/git/env/network; no process; no hashing. Deterministic/byte-stable (FR-008): identical
input → byte-identical reference and store on every run/machine. Additive (FR-009): new value-only library, no
new third-party dependency, no schema bump, zero edits to any existing core/host/golden-baseline/reader-shape,
referenced by nothing on landing. Lossless persistence (FR-010): a `capture`-grown store survives F047
`serialise` → F046 read with the world and reference preserved verbatim.

**Scale/Scope**: Additive only. New files: `src/FS.GG.Governance.EvidenceCapture/` (`.fsi`, `.fs`, `.fsproj`),
`tests/FS.GG.Governance.EvidenceCapture.Tests/` (`.fsproj` + test files),
`surface/FS.GG.Governance.EvidenceCapture.surface.txt`, a `scripts/prelude.fsx` section, the two `.sln`
entries, and the `CLAUDE.md` plan pointer. **Zero** edits to F029/F030/F032/F041–F048 cores, host commands, or
their golden baselines; **zero** new third-party dependencies; **no** schema bump; **no** new type.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — still PASS.*

| Principle | Status | Justification |
|-----------|--------|---------------|
| I. Spec → FSI → Semantic Tests → Implementation | PASS | FSI-first is satisfied by committing `contracts/EvidenceCapture.fsi` **before any `.fs` body** and writing public-surface semantic tests (driving `EvidenceCapture.referenceOf` / `capture` and `EvidenceReuse.decide` through the packed library, never private helpers) that fail before implementation. The `scripts/prelude.fsx` section is the documentation-of-record FSI transcript — the runnable honest-audience exercise of the shipped surface — not the design-time sketch (the `.fsi` is that). |
| II. Visibility lives in `.fsi` | PASS | The two functions `referenceOf` / `capture` are declared in the curated `EvidenceCapture.fsi`; the `.fs` carries no access modifiers; the new `surface/FS.GG.Governance.EvidenceCapture.surface.txt` baseline is guarded by the existing reflective drift test pattern. No new type is introduced — the `.fsi` declares only the two functions. |
| III. Idiomatic Simplicity | PASS | The plainest possible F#: two one-line function bodies composing already-merged operations (`EvidenceRef (identityValue (canonicalId record))`; `EvidenceReuse.record inputs (referenceOf record) store`). No custom operators, SRTP, reflection (outside tests), type providers, mutation, recursion, or non-trivial CEs. |
| IV. Elmish/MVU boundary | N/A (does not apply) | Pure, total, stateless, I/O-free value transformation — a reference derivation and an immutable store fold. No multi-step state, external I/O, retries, or background work. The same shape as F030 `EvidenceReuse` and F047 `EvidenceReuseStore`, which also carry no MVU ceremony. The impure gate-execution edge that *would* need an MVU boundary is the explicitly out-of-scope following row. |
| V. Test Evidence | PASS | Semantic tests fail before the bodies exist and pass after, driving the public FSI surface against **real** F030/F032 operations and a **real** F046 reader for the round-trip — all with no I/O (SC-008). Evidence references in the capture path are **derived from real command records**, not synthetic literals, so this row removes a synthetic-evidence use rather than adding one (the disclosure discipline is satisfied by absence of synthetic data on this path). |
| VI. Observability & Safe Failure | PASS | The core is pure and total — no failure mode to swallow. It never throws, distinguishes nothing it should report, and degrades nowhere (every input, including empty digests / failed exit / every captured-output outcome, yields an ordinary value, FR-007). Capture introduces no silent weakening of a prior verdict (recompute-safety, FR-006). |

**Change Classification**: **Tier 1 (contracted change)** — adds new public API surface (a new packable library
with two public functions and a new surface baseline). Requires the full artifact chain: spec, plan,
`.fsi`, surface baseline, and test evidence. **No** new third-party dependency is added; **no** schema version is
bumped; **no** existing public surface is modified (the library is referenced by nothing on landing).

**Engineering Constraints**: net10.0 ✅; the new public module carries a curated `.fsi` ✅; a surface baseline is
added ✅; no new dependency ✅ (the library is BCL + FSharp.Core, layered on the already-on-graph F030 + F032
cores); `FS.GG.Governance.*` namespace ✅; pack output of existing packages unaffected ✅ (the new library is
independently packable, like F030/F047); one-way operating rule unaffected (no rendering coupling) ✅. No
violations → **Complexity Tracking is empty**.

## Project Structure

### Documentation (this feature)

```text
specs/049-evidence-reference-capture/
├── plan.md              # This file (/speckit-plan command output)
├── spec.md              # Feature specification (input)
├── research.md          # Phase 0 output — the resolved decisions
├── data-model.md        # Phase 1 output — reference-derivation + store-fold semantics
├── quickstart.md        # Phase 1 output — build/exercise/test walkthrough
├── contracts/
│   └── EvidenceCapture.fsi   # Phase 1 output — the curated public surface of the new library
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/FS.GG.Governance.EvidenceCapture/                  # NEW (this row)
├── EvidenceCapture.fsi    # curated surface: referenceOf, capture (no new type — Model-less)
├── EvidenceCapture.fs     # two pure one-line bodies
└── FS.GG.Governance.EvidenceCapture.fsproj   # ProjectReference EvidenceReuse (F030) + CommandRecord (F032)

surface/
└── FS.GG.Governance.EvidenceCapture.surface.txt       # NEW reflective baseline (referenceOf, capture)

tests/FS.GG.Governance.EvidenceCapture.Tests/          # NEW test project
├── Support.fs             # NEW: real literally-constructible CommandRecord/FreshnessInputs/store builders + FsCheck generators (no mocks)
├── ReferenceTests.fs      # NEW: duration-invariance (US2/SC-002), reproducible-fact sensitivity (US2/SC-003), determinism (SC-005), totality over edge digests/exit/captured-output (FR-007)
├── CaptureTests.fs        # NEW: close-the-loop round-trip (US1/SC-001), recompute-safety + prior-entry preservation (US3/SC-004), newest-first duplicate capture (US3)
├── PersistenceRoundTripTests.fs  # NEW: capture-grown store → F047 serialise → temp file → F046 realStoreReader preserves world+reference (FR-010/SC-007)
├── SurfaceDriftTests.fs   # NEW: reflective surface baseline + scope-hygiene assertion (Principle II)
├── Main.fs                # NEW: Expecto entry point
└── FS.GG.Governance.EvidenceCapture.Tests.fsproj

scripts/prelude.fsx                                     # + an EvidenceCapture walkthrough section

FS.GG.Governance.sln                                    # + the new src + test project entries

# Untouched (additive guarantee): F030 EvidenceReuse, F032 CommandRecord, F029 FreshnessKey, F047
# EvidenceReuseStore, F046 FreshnessSensing + its reader, all F041–F048 cores and host commands, every
# route.json/audit.json/cache-eligibility golden baseline, the fsgg.evidence-reuse-store/v1 schema.
```

**Structure Decision**: Deliver a **new standalone pure library** layered on top of the already-merged F030 and
F032 cores (constitution: heavier capabilities layer on top, not into the core; the F042/F047 precedent), rather
than adding a function to `EvidenceReuse` or `CommandRecord` — that would edit a frozen merged surface and its
golden baseline, violating the additive guarantee (FR-009). The library is **Model-less** (introduces no new
type), so it carries only `EvidenceCapture.fsi` + `EvidenceCapture.fs`, unlike F030 which needed a `Model`
file. It is referenced by nothing on landing; the host wiring that consumes it (the F048 analogue) is the
explicitly out-of-scope following row.

## Complexity Tracking

> No Constitution Check violations. This section is intentionally empty.
