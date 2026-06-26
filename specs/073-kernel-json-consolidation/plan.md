# Implementation Plan: Kernel JSON consolidation

**Branch**: `073-kernel-json-consolidation` | **Date**: 2026-06-26 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/073-kernel-json-consolidation/spec.md`

## Summary

Remove the JSON-emit duplication identified as **Phase A** of the architecture/quality/
de-duplication roadmap. Three independently-shippable slices, each gated on byte-identical
golden/snapshot output:

1. **Export the canonical `writeToString`** from `FS.GG.Governance.Kernel/Json.fsi` and
   delete the ~13–14 hand-copied definitions across `src` (the `*Json` projections plus
   two non-projection copies in `EvidenceReuseStore` and `RefreshCommand/Interpreter`, and
   the `private`-modified copy in `AttestationJson`).
2. **Introduce a pure `FS.GG.Governance.JsonTokens` leaf** holding the seven
   duplicated closed-enum token helpers (`costToken`, `maturityToken`, `severityToken`,
   `environmentToken`, `dispositionToken`, `basisToken`, `profileToken`); replace the
   in-module copies across the projections. (The `Verdict` token —
   `verdictToken`/`rrVerdictToken` — is *not* one of the seven and its copies emit divergent
   strings, so it is out of scope and stays local — research D3.)
3. **Introduce a pure `FS.GG.Governance.JsonWriters` leaf** holding the duplicated
   sub-object writers and gate-map helpers (`writeCause`, `verdictByGate`, `outcomeByGate`,
   `writeExecution`, `writeEnforcement`); replace the copies. (The single-use
   `writeNullableString`/`writeNullableInt` in ReleaseJson are *not* duplicated and stay
   local — out of scope, research D3.)

**Key correction to the report's topology:** only `writeToString` belongs in `Kernel`
(it needs `System.Text.Json` and no domain types). The token/writer helpers serialize
domain enums (`Cost`, `Maturity`, `GateDisposition`, `RecomputeCause`, …) that live in
projects *above* `Kernel`, so the two shared leaves are placed **above the domain-type
projects and below the projections** and are named `FS.GG.Governance.JsonTokens` /
`FS.GG.Governance.JsonWriters`. The report's `Kernel.JsonTokens`/`Kernel.JsonWriters`
labels were namespace shorthand, not a buildable dependency position (a leaf under `Kernel`
cannot see `Cost`/`GateDisposition`). This is recorded as decision D1 in
[research.md](./research.md).

This is a **Tier 1** change: it adds public API surface (the `writeToString` export and two
new leaf `.fsi` surfaces), adds inter-project dependency edges, and requires surface-area
baseline updates — but changes **no** observable JSON output.

## Technical Context

**Language/Version**: F# on .NET `net10.0` (solution-wide via `Directory.Build.props`;
`TreatWarningsAsErrors=true`, `Nullable=enable`).

**Primary Dependencies**: `System.Text.Json` from the net10.0 shared framework only
(`Utf8JsonWriter`). **No new third-party `PackageReference`** is introduced — the new leaves
stay `System.*`/`FSharp.Core`-only, matching every existing `*Json` projection.

**Storage**: N/A (pure projections; no I/O in the changed code).

**Testing**: per-project test suites under `tests/` (xUnit-style F# projects), including
golden/snapshot `*Json.Tests` fixtures and per-project `SurfaceDriftTests.fs` validating
each module's public surface against `surface/<Project>.surface.txt`.

**Target Platform**: Linux/cross-platform CLI + libraries.

**Project Type**: Single solution of one-concern-per-project F# micro-libraries (75 `src`
projects). This feature adds **two** pure leaf libraries and exports one existing helper.

**Performance Goals**: N/A — emit paths are unchanged in behaviour; the consolidation is
referential, not algorithmic.

**Constraints**:
- **Byte-identical output is the hard acceptance test.** No golden/snapshot fixture may
  change. A moved golden means behaviour drifted and the extraction is reverted/revisited.
- **Leaves stay pure** — no host/impure dependency; depend only on already-shared domain
  types and (for `JsonWriters`) on `JsonTokens`.
- **`.fsi`-first** — each new leaf exposes exactly the shared helpers and nothing more.
- Full test suite green at every shippable increment, with unchanged test count.

**Scale/Scope**: ~13–14 `writeToString` copies removed; 7 token helpers and ~6 sub-object/
map helpers de-duplicated across the 12 `*Json` projections (+2 non-projection `writeToString`
sites). Estimated net `src` reduction ~300 LOC.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | ✅ | New leaves are drafted `.fsi`-first; the public surface is the two new `.fsi` files + the one-line `Json.fsi` addition. Existing golden/snapshot tests already exercise every shared helper through the projection surfaces; they are the semantic tests and must stay byte-identical. |
| II. Visibility lives in `.fsi` | ✅ | Each new leaf gets a curated `.fsi`; the shared helpers become public there. Opportunity: `AttestationJson`'s `let private writeToString` (a stray `private` modifier) is removed in the process. No `.fs` access modifiers added. |
| III. Idiomatic simplicity | ✅ | Pure functions over domain values; no new operators, SRTP, reflection, or CEs. Strictly *less* code. |
| IV. Elmish/MVU boundary | ✅ (N/A) | The changed code is pure projection logic with no state/I/O; no MVU surface involved. The MVU command hosts are out of scope (that is Phase B/C). |
| V. Test evidence mandatory | ✅ | Behaviour is pinned by existing goldens; the "test that fails before / passes after" is inverted here — the acceptance evidence is that **every** golden stays green and byte-identical after each slice. New leaves get `SurfaceDriftTests`. No synthetic evidence introduced. |
| VI. Observability & safe failure | ✅ | No failure paths changed; `reqString`/fail-fast parse helpers are untouched. |

**Change Classification: Tier 1.** Public API surface is added (the `writeToString` export
and two leaf surfaces) and new dependency edges/projects are introduced, so the full
artifact chain applies: spec, plan, `.fsi` updates, **surface-area baseline updates**, test
evidence, and the doc/agent-context update. A Tier 1 change that fails to update the
affected `surface/*.surface.txt` baselines is a defect even if tests pass.

**Genericity / operating rule:** ✅ The new leaves are domain-neutral JSON helpers over
already-shared governance types; they assume no rendering package IDs, template names, or
paths. **Dependency-minimalism:** ✅ no new third-party dependency; the new leaves layer
*on top of* existing domain projects (heavier capability layered out, not into a core),
consistent with the constraint that the rule/evidence core stays minimal.

**Gate result: PASS** (no violations; Complexity Tracking not required, but the two-new-
project decision is justified in Phase 0 / D2 below).

## Project Structure

### Documentation (this feature)

```text
specs/073-kernel-json-consolidation/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions (D1 placement, D2 two-leaf split, D3 token scope, D4 byte-identity strategy, D5 sequencing/edges)
├── data-model.md        # Phase 1 — the shared helpers as a "data model" of public members
├── quickstart.md        # Phase 1 — how to validate (build + golden byte-identity + surface drift)
├── contracts/           # Phase 1 — proposed .fsi surfaces (the public contracts)
│   ├── kernel-json-delta.md      # the one-line Json.fsi addition
│   ├── JsonTokens.fsi            # proposed surface of the token leaf
│   └── JsonWriters.fsi           # proposed surface of the writer leaf
└── checklists/
    └── requirements.md  # spec quality checklist (already complete)
```

### Source Code (repository root)

```text
src/
├── FS.GG.Governance.Kernel/
│   ├── Json.fsi                      # CHANGE: add `val writeToString: (Utf8JsonWriter -> unit) -> string`
│   └── Json.fs                       # unchanged body (already defines it)
│
├── FS.GG.Governance.JsonTokens/      # NEW pure leaf (Slice 2)
│   ├── FS.GG.Governance.JsonTokens.fsproj   # references: Config, Gates, Findings, FreshnessKey, Enforcement, GateRun (domain enum owners)
│   ├── JsonTokens.fsi
│   └── JsonTokens.fs
│
├── FS.GG.Governance.JsonWriters/     # NEW pure leaf (Slice 3)
│   ├── FS.GG.Governance.JsonWriters.fsproj  # references: JsonTokens + CacheEligibility, EvidenceReuse, GateRun, CommandRecord, Enforcement
│   ├── JsonWriters.fsi
│   └── JsonWriters.fs
│
└── (the 12 *Json projections + EvidenceReuseStore + RefreshCommand)
    # CHANGE: reference Kernel/JsonTokens/JsonWriters where not already on the graph;
    #         delete local writeToString / token / writer copies; call the shared helpers.

surface/
├── FS.GG.Governance.Kernel.surface.txt        # CHANGE: add the writeToString member
├── FS.GG.Governance.JsonTokens.surface.txt     # NEW baseline
├── FS.GG.Governance.JsonWriters.surface.txt    # NEW baseline
└── FS.GG.Governance.<Projection>.surface.txt    # CHANGE: drop the removed hidden helpers IF any were surfaced (they are hidden, so most are no-ops)

tests/
├── FS.GG.Governance.JsonTokens.Tests/          # NEW: SurfaceDriftTests + a small token-string table test
├── FS.GG.Governance.JsonWriters.Tests/         # NEW: SurfaceDriftTests + writer byte-shape tests
└── (all existing *Json.Tests)                  # UNCHANGED fixtures — must stay byte-identical

FS.GG.Governance.sln                            # CHANGE: register the two new src projects + two new test projects
```

**Structure Decision**: Keep the one-concern-per-project micro-library topology. Add two
pure leaf libraries (`JsonTokens`, `JsonWriters`) positioned above the domain-enum owners
and below the projections; export the existing `writeToString` from `Kernel`. No project is
collapsed or merged. `JsonWriters` references `JsonTokens` (writers call token helpers); both
are pure and acyclic since every consuming projection already references the underlying
domain projects.

## Complexity Tracking

No constitution violations require justification. The only judgment call — **adding two new
projects rather than one** — is argued in research **D2**: tokens (enum→string) and
sub-object writers (shape emission) are distinct concerns with different dependency
fan-outs (`JsonWriters` needs `CacheEligibility`/`EvidenceReuse`/`GateRun`/`CommandRecord`
that `JsonTokens` does not), and the repo's prevailing one-concern-per-project rule favors
the split. A single combined leaf is the rejected alternative.

| Decision | Why | Rejected alternative |
|---|---|---|
| Two new leaves (`JsonTokens`, `JsonWriters`) | Distinct concerns + distinct dependency fan-out; matches micro-project norm | One combined `JsonShared` leaf — would force the wider `JsonWriters` dependency set onto pure token consumers |
| Place leaves above domain projects, not under `Kernel` | Token/writer helpers need domain enums `Kernel` cannot see | `Kernel.JsonTokens` literally under `Kernel` — does not compile (no domain types in scope) |
