# Implementation Plan: Persist, Bound, And Prune The Evidence-Reuse Store

**Branch**: `047-evidence-reuse-store-write` | **Date**: 2026-06-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/047-evidence-reuse-store-write/spec.md`

## Summary

The cache-eligibility thread (F029–F046) reads an evidence-reuse store but can never write one. F046's
shared `FreshnessSensing` edge deserializes `fsgg.evidence-reuse-store/v1` (`realStoreReader`/`loadStore`)
and maps an absent file to `EvidenceReuse.empty`; F030 ships the pure insert `record`. But **nothing
serialises a `ReuseStore` back to disk**, and **nothing bounds its growth or prunes dead entries** — so every
run reads an empty store and every gate verdict is `mustRecompute noPriorEvidence`. The cache can never warm.

This row delivers the **pure write half** of the store lifecycle as three pure, total value transformations
in **one new sibling library `FS.GG.Governance.EvidenceReuseStore`**:

1. **`serialise : ReuseStore -> string`** — the deterministic, byte-stable inverse of the existing read-only
   deserializer. The round-trip `serialise` → `FreshnessSensing.realStoreReader` is lossless (the re-read
   store **equals** the input, every freshness input and opaque evidence reference preserved, in the same
   newest-first order). Hand-driven `Utf8JsonWriter` (compact, non-indented) — the exact net10.0
   shared-framework mechanism the F042 `CacheEligibilityJson` / F025 `AuditJson` projections already use.
2. **`retain : maxEntries:int -> ReuseStore -> ReuseStore`** — deterministic bounded retention: keep the
   newest `maxEntries` entries (the head of the already-newest-first list), drop the rest. Idempotent at or
   under the bound. Eviction only *removes* whole entries — never mutates or fabricates one.
3. **`prune : ReuseStore -> ReuseStore`** — deterministic dead-entry expiry: remove every entry that a
   strictly-newer entry already `FreshnessKey.matches` (the same freshness world), so the store keeps only
   the newest entry per world-class. This reuses F030/F029 supersession semantics **verbatim** (it is
   exactly `record`'s full-match dedup applied across the whole list).

All three are **pure value transformations with no I/O** (FR-009) and every one preserves
**recompute-by-default safety** (FR-008): relative to the unmodified store, the result yields reuse verdicts
that are identical-or-stricter for every candidate — it can never turn a `mustRecompute` into a `reusable`
(no spurious reuse). The **impure** on-disk persistence (atomic temp+rename write wired into
`fsgg route`/`fsgg ship`, store-path discovery, the writer port) and the production of *real* evidence
references (which needs gate **execution**) are explicit **later rows** — exactly how F042 delivered the pure
`cache-eligibility.json` projection before the F044/F046 host wiring.

The change is **additive** (FR-012, SC-007): it adds a new library + new surface baseline + a new test
project, edits **zero** merged F029–F046 cores, re-blesses **zero** golden baselines, bumps **no** schema
version, leaves the read-only reader's accepted shape untouched (now consuming serialiser output unmodified),
and adds **no** new third-party dependency.

The contracts this row commits live in [contracts/EvidenceReuseStore.fsi](./contracts/EvidenceReuseStore.fsi)
(the new library surface), the document shape and the three operations' semantics in
[data-model.md](./data-model.md), the build / exercise / test walkthrough in [quickstart.md](./quickstart.md),
and the nine decisions in [research.md](./research.md).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (repo standard; `Nullable=enable`, `TreatWarningsAsErrors=true`
from `Directory.Build.props`). One **new** library (`FS.GG.Governance.EvidenceReuseStore`) — a `.fsi`-first
pure core (Model-free; it operates over the existing F030 `ReuseStore`). No host/edge code, no MVU.

**Primary Dependencies**: `ProjectReference`s only; **no new third-party `PackageReference`**. The new library
references `EvidenceReuse` (F030 `ReuseStore`/`RecordedEvidence`/`EvidenceRef`/`record`/`entries`),
`FreshnessKey` (F029 `FreshnessInputs`/`matches`/`RuleHash`/`ArtifactHash`/`CommandVersion`/`GeneratorVersion`/
`Revision`), and `Config` (F014 `CheckId`/`DomainId`/`CommandId`/`EnvironmentClass`) — every one already on the
F030 transitive graph. Serialisation uses only the net10.0 shared-framework `System.Text.Json`
(`Utf8JsonWriter`) the F042 projection already uses — no new dependency (FR-011). Test frameworks unchanged
(Expecto, Expecto.FsCheck, FsCheck, Microsoft.NET.Test.Sdk, YoloDev.Expecto.TestSdk).

**Storage**: No I/O in scope. The library produces the `fsgg.evidence-reuse-store/v1` document **text**; it
neither opens, reads, nor writes a file. Round-trip tests write the text to a temp file purely to exercise the
**real** `FreshnessSensing.realStoreReader` (which reads a path) and assert loaded `=` input. The actual
on-disk persistence (atomic temp+rename, store-path discovery, the writer port) is a later host row
(out of scope, Assumptions).

**Testing**: Expecto + FsCheck. New `FS.GG.Governance.EvidenceReuseStore.Tests` exercises the **public**
surface (`serialise`/`retain`/`prune`/`schemaVersion`/`defaultRetentionBound`) over **real** upstream-assembled
stores built via the genuine `EvidenceReuse.record` (real F029 `FreshnessInputs`, opaque evidence refs). The
load-back leg drives the **real** `FreshnessSensing.realStoreReader` against a temp file — never a
re-implemented parser. Property tests (FsCheck): lossless round-trip (SC-001), determinism / byte-identity
(SC-002), empty round-trip (SC-003), retention bound + idempotence (SC-004), prune subset + verdict
preservation (SC-005), and the cross-cutting no-spurious-reuse property comparing `EvidenceReuse.decide`
verdicts pre/post each operation across generated candidates and stores (SC-006). Evidence references in
fixtures are disclosed synthetic literals (the `Synthetic` token, Principle V) — real evidence needs gate
execution (Assumptions). A reflective surface-drift + dependency-scope-hygiene test guards the new baseline
and the one-way dependency graph (Principle II), mirroring F042's `SurfaceDriftTests`.

**Target Platform**: Developer / CI .NET SDK running `dotnet test`. No OS-specific surface.

**Project Type**: One new pure `.fsi`-first library. No stateful or I/O workflow ⇒ **no Elmish/MVU boundary**
(Principle IV's explicit "simple pure functions" exemption — these are value transformations, the I/O is the
deferred host row). This mirrors F042's pure `CacheEligibilityJson` projection, which likewise carried no MVU.

**Performance Goals**: N/A. The contracts are determinism, byte-stability, lossless round-trip, and the
recompute-safety invariant — not latency. Each operation is one linear pass over the entry list.

**Constraints**: Deterministic / byte-stable (FR-003, SC-002): the same `ReuseStore` value produces
byte-identical output on every run and machine — stable field/entry order, no wall-clock, path, locale, or
environment leakage; no canonical re-sort (entry order and per-entry `coveredArtifacts` order are emitted
**verbatim**, since `ReuseStore` structural equality is list-order-sensitive and the round-trip must be exact).
Opaque (FR-004): the evidence reference and every freshness-input newtype string is rendered verbatim, never
parsed, re-hashed, or interpreted; the library computes no hash, key, digest, or freshness decision of its
own. Recompute-safe (FR-008, SC-006): no operation ever turns a `mustRecompute` candidate into a `reusable`
one. Reuse-verbatim (FR-010): retention/pruning reuse the F030/F029 model and `matches` semantics; no new
reuse policy, freshness-match rule, or evidence representation is introduced. Additive (FR-012, SC-007): no
edit to the reader's accepted shape, no schema bump, zero edits to merged F029–F046 cores or their golden
baselines, no new third-party dependency.

**Scale/Scope**: One new library (`EvidenceReuseStore.fsi`/`.fs` + `.fsproj` + surface baseline) added to the
solution; one new focused test project (`EvidenceReuseStore.Tests`); a short `scripts/prelude.fsx` section;
the `CLAUDE.md` plan pointer. **Zero** edits to F029/F030/F041–F046 or their baselines, **zero** new
third-party dependencies, **zero** golden-baseline re-bless, **no** schema bump.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — still PASS.*

| Principle | Status | Justification |
|-----------|--------|---------------|
| I. Spec → FSI → Semantic Tests → Implementation | PASS | This plan commits the `.fsi` (`contracts/EvidenceReuseStore.fsi`) before any `.fs`; the surface is exercised in `scripts/prelude.fsx` (FSI) and through the packed/public surface in tests, never private helpers. |
| II. Visibility lives in `.fsi` | PASS | New module ships a curated `.fsi` as the sole public-surface declaration; the `.fs` carries no access modifiers; a new `surface/FS.GG.Governance.EvidenceReuseStore.surface.txt` baseline is added and guarded by a reflective drift test. |
| III. Idiomatic Simplicity | PASS | Plain pure functions over `List`: a `Utf8JsonWriter` walk, `List.truncate` for retention, a single de-dup fold for pruning. No custom operators, SRTP, reflection (outside tests), type providers, or non-trivial CEs. |
| IV. Elmish/MVU boundary | PASS (exempt) | No multi-step state, no I/O, no retries, no user interaction. These are pure value transformations (FR-009); the I/O (on-disk write) is the explicit deferred host row. Principle IV exempts "simple pure functions" — the F042 projection precedent. |
| V. Test Evidence | PASS | Expecto + FsCheck property tests fail before the `.fs` exists and pass after; round-trip drives the **real** `FreshnessSensing` reader (real bytes via temp file), not a mock parser. Synthetic evidence references are disclosed (`Synthetic` token, listed in the PR). |
| VI. Observability & Safe Failure | PASS (N/A surface) | Pure total functions with no failure modes — `serialise` never throws, `retain`/`prune` are total over every store. No silent failure path exists to guard; the impure write that *would* emit diagnostics is the deferred host row. |

**Change Classification**: **Tier 1 (contracted change)** — adds new public API surface (a new library + module)
and a new package identity. Requires the full artifact chain: spec, plan, `.fsi`, a new surface baseline, and
test evidence. No existing public surface changes; no third-party dependency is added.

**Engineering Constraints**: net10.0 ✅; new public module has a curated `.fsi` ✅; surface baseline added ✅;
no new dependency ✅ (`System.Text.Json` is shared-framework); `FS.GG.Governance.*` namespace ✅; pack output
unaffected ✅. No violations → **Complexity Tracking is empty**.

## Project Structure

### Documentation (this feature)

```text
specs/047-evidence-reuse-store-write/
├── plan.md              # This file (/speckit-plan command output)
├── spec.md              # Feature specification (input)
├── research.md          # Phase 0 output — the nine decisions
├── data-model.md        # Phase 1 output — document shape + operation semantics
├── quickstart.md        # Phase 1 output — build/exercise/test walkthrough
├── contracts/
│   └── EvidenceReuseStore.fsi   # Phase 1 output — the committed public surface
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/FS.GG.Governance.EvidenceReuseStore/          # NEW pure library (this row)
├── EvidenceReuseStore.fsi                         # curated public surface (serialise/retain/prune/…)
├── EvidenceReuseStore.fs                          # pure total bodies (no access modifiers)
└── FS.GG.Governance.EvidenceReuseStore.fsproj     # ProjectReferences: EvidenceReuse, FreshnessKey, Config

surface/
└── FS.GG.Governance.EvidenceReuseStore.surface.txt   # NEW reflective surface baseline

tests/FS.GG.Governance.EvidenceReuseStore.Tests/  # NEW focused test project
├── Support.fs               # repoRoot + real-store builders (EvidenceReuse.record) + FsCheck generators
├── RoundTripTests.fs        # SC-001/003: serialise → real realStoreReader → loaded = input
├── DeterminismTests.fs      # SC-002: byte-identical on re-serialise
├── RetentionTests.fs        # SC-004: bounded + newest-first + idempotent
├── PruningTests.fs          # SC-005: superseded removed, subset, verdict-preserving
├── SafetyTests.fs           # SC-006: no operation turns Recompute into Reuse (decide pre/post)
├── TotalityTests.fs         # SC-008: pure / total over generated inputs, never throws
├── SurfaceDriftTests.fs     # Principle II: baseline + dependency-scope hygiene
└── Main.fs

# Untouched (additive guarantee): src/FS.GG.Governance.EvidenceReuse (F030),
# src/FS.GG.Governance.FreshnessSensing (F046) and its reader, all F041–F046 cores + golden baselines.
```

**Structure Decision**: A **new sibling library**, not an edit to the F030 `EvidenceReuse` core. This keeps the
merged F030 surface frozen (no re-bless of its baseline — additive, FR-012) and follows the constitution's
"heavier capabilities layer on top, not into the core" rule and the established `*Json` sibling precedent
(F042 `CacheEligibilityJson` layered serialisation beside F041's pure core). All three operations
(`serialise`/`retain`/`prune`) live together because they are one cohesive concern — the store's **write
half** — and grouping them avoids touching two merged cores. The library is referenced by nothing yet; the
later host row will reference it for the on-disk write. (See [research.md](./research.md) D1.)

## Complexity Tracking

> No Constitution Check violations. This section is intentionally empty.
