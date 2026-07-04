# Implementation Plan: Deferred tail of the 2026-07-02 code review

**Branch**: `111-review-deferred-tail` | **Date**: 2026-07-03 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/111-review-deferred-tail/spec.md`

## Summary

Land the **deferred low-severity tail** of the 2026-07-02 review (issue #83, last open child of
epic #44) — the thirteen findings spec 110 held back from #82 because each needs a broader
type/`.fsi`/surface change, a new shared home, or is cosmetic across many files. The work is
**consolidation and honesty**, not redesign: make one impossible gate-outcome state
unrepresentable (B4), shrink three public signatures to what they actually use (B6/B7/B9), thread
parsed values in the config loader instead of force-unwrapping (B5), collapse duplicated helpers
into shared homes behind a green dependency-fence suite (A1/A4/A6), delete two provably-dead
elements (C1a/C1b), and correct cosmetic/docs residue (C1g/C2f/C2g).

It ships as **seven independently-reviewable PRs** (one per user story, P1→P3 order), so no diff
bundles a Tier-1 surface change with an unrelated cosmetic sweep. **Four items are Tier 1** and
move the surface baseline in lockstep — B4 (`GateOutcome`/`commandFor`), B6
(`ComparisonSample.Agreement`), B7 (`decideMatrix` `boundary`), B9 iff it touches
`Snapshot.fsi`, plus A6's intentional `Verdict.combineReasons` export; everything else is Tier 2
with the surface untouched and byte-identical output for real inputs.

## Technical Context

**Language/Version**: F# on .NET `net10.0`. Changes touch `.fs` bodies, several curated `.fsi`
signatures (Tier-1 items), `.fsproj` `ProjectReference`s (A1 Scaffold, A4 GatesJson/ReleaseJson,
A6 shared homes), and Markdown docs (C2f/C2g). No new NuGet dependency.

**Primary Dependencies**: existing in-repo projects only — `CommandHost` (A1),
`FS.GG.Governance.JsonWriters` (A4, already exists), `SurfaceChecks`/`Kernel` (A6 shared homes),
`System.Text.Json` `Utf8JsonWriter` (the JSON writer pairs), `System.Security.Cryptography.SHA256`
(A6 `sha256Hex`). The api-compat / surface-drift gate is the arbiter of Tier-1 correctness; the
`FS.GG.Governance.DependencyFences.Tests` suite is the arbiter of A6/A1 fence soundness.

**Storage**: N/A (no persisted-format change; all JSON/audit/snapshot output is byte-identical for
real inputs).

**Testing**: xUnit + the repo's real-evidence discipline. Behaviour/type changes (B4, B6, B7, B9)
get RED→GREEN tests or a **compile-fail demonstration** (a commented, `#if`-guarded, or
xUnit-documented negative that proves `Executed`-without-exit-code no longer type-checks).
Output-preserving changes (B5, A1, A4, A6, C1a, C1b) rely on byte-identical-output assertions and
the existing suites proving no drift. Surface deltas are pinned by the surface-drift baselines.

**Target Platform**: local `dotnet` + GitHub Actions `ubuntu-latest` (gate.yml build/test +
api-compat/surface-drift; publish.yml unaffected — no published-version change).

**Performance Goals**: N/A for behaviour, but A6's two O(n²) siblings are already fixed in #82; the
`sha256Hex`/writer consolidations are allocation-neutral. No hot-path regression permitted.

**Project Type**: Multi-project F# library + CLI (governance tooling). Single repo, no
frontend/backend split.

**Constraints**:
- **Surface baseline moves only for the five intentional Tier-1 deltas** (FR-016); any other
  surface move is a defect that fails the gate.
- **Byte-identical output for legitimate inputs** across every dedup/dead-code/cosmetic change;
  only the *types* and *diagnostic shapes* of the surface items move.
- **No unsound fence edge** — an A6 helper whose only viable shared home would break a dependency
  fence stays duplicated with a recorded rationale (spec Edge Cases; FR-009). The fence suite is
  the gate, not a judgement call.
- **PR-per-story** — each user story lands green and standalone (SC-005).

**Scale/Scope**: ~13 findings · 7 PRs · ~30 source files touched across ~20 projects · 5 `.fsi`
surfaces moved · 2 dead vocabularies removed · 1 new shared-home decision per A6 helper · 6 doc
headers + ≥3 dead opens + 1 decisions index.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Change Classification — mixed, declared per story.** User Stories 1–2 and the A6
`combineReasons` export are **Tier 1** (public `.fsi` surface moves, baselines updated in
lockstep); User Stories 3–7 are **Tier 2** (no surface change). The spec declares the tier of
every FR; this satisfies the "every feature declares a tier" rule at the FR granularity a
multi-item cleanup requires.

| Principle | Assessment |
|---|---|
| **I. Spec → FSI → Semantic Tests → Implementation** | Honoured. The Tier-1 items are *driven by* their `.fsi`: the surface delta (contracts/surface-deltas.md) is designed first, exercised as a signature, then implemented. FSI transcripts in quickstart exercise the reshaped `GateOutcome`/`commandFor`, the shrunk `decideMatrix`/`ComparisonSample`, and the `combineReasons` export through the packed surface. |
| **II. Visibility lives in `.fsi`** | Central to this feature. Every Tier-1 item edits the `.fsi` and its surface baseline together; A6 promotes `Verdict.combineReasons` to `Verdict.fsi` deliberately. No `.fs` gains an access modifier — C1g even *removes* stale "no access modifiers" headers, reinforcing the rule. |
| **III. Idiomatic simplicity** | The whole feature reduces cleverness: fewer duplicated helpers, no dead parameters, no force-unwrap thunks, illegal states unrepresentable. No SRTP/reflection/CE tricks introduced. |
| **IV. Elmish/MVU boundary for I/O** | Preserved and reinforced. A1 makes `EvidenceCommand`/`Scaffold` adopt the shared `CommandHost.guard`/`drive` MVU-loop edge instead of hand-copies; B9 routes the git-unavailable *interpreter edge* through the pure `Snapshot.assemble`, tightening the pure/edge split. No `update` gains I/O. |
| **V. Test evidence is mandatory** | RED→GREEN or compile-fail for B4/B6/B7/B9; byte-identical-output assertions for the rest. No synthetic evidence needed (all inputs are real config/snapshot/JSON fixtures already in the suites). |
| **VI. Observability & safe failure** | Improved: B4 removes an unrepresentable-but-possible silent-bad-state; `commandFor`'s typed result distinguishes misconfiguration from absence (VI's "defect vs missing input" mandate); B9 keeps the explicit `GitUnavailable` diagnostic while removing the drift-prone hand-rolled record. |

**Engineering Constraints**: `net10.0` ✅; every touched public module keeps its curated `.fsi`
(and baselines move in lockstep for Tier-1) ✅; no new dependency ✅; nothing rendering-specific —
generic governance internals only ✅; `FS.GG.Governance.*` identity preserved ✅; A6/A1 new
`ProjectReference` edges validated against the dependency-fence suite before they land ✅;
intentional deferral (unsound-fence helpers) is explicit and bounded per FR-009 ✅.

**Result: PASS.** No violations; Complexity Tracking intentionally empty. The one recurring design
question — where each A6 helper lands without breaking a fence — is resolved deterministically in
Phase 0 research against the actual project graph, not deferred as a Constitution risk.

## Project Structure

### Documentation (this feature)

```text
specs/111-review-deferred-tail/
├── plan.md              # This file
├── research.md          # Phase 0 — R1 B4 DU/commandFor reshape, R2 A6 shared-home + fence map,
│                        #           R3 B9 assemble routing, R4 B6 drop-vs-derive, R5 test/baseline mechanics
├── data-model.md        # Phase 1 — the types that change shape (before/after) + the dedup home map
├── contracts/
│   └── surface-deltas.md # Phase 1 — the authoritative per-`.fsi` before/after for every Tier-1 item
├── quickstart.md        # Phase 1 — per-story validation: RED→GREEN/compile-fail, byte-identical diff,
│                        #           fence-suite green, surface-baseline delta review
└── checklists/
    └── requirements.md  # spec quality checklist (all pass)
```

### Source Code (repository root) — touched surfaces, grouped by user story / PR

```text
# US1 (P1, Tier 1) — illegal gate-outcome states unrepresentable
src/FS.GG.Governance.GateRun/Model.fs + Model.fsi   # GateOutcome/GateDisposition reshape (ExitCode/Passed into Executed)
src/FS.GG.Governance.GateRun/Plan.fs  + Plan.fsi     # commandFor → typed result (3 no-command modes distinct)
… GateRun consumers (GateExecution, VerifyCommand, ShipCommand, RoutePipeline)  # match the new shapes

# US2 (P2, Tier 1) — signatures state only what they use
src/FS.GG.Governance.ValidationMatrix/Matrix.fs + Matrix.fsi    # drop dead `boundary` param (B7)
src/FS.GG.Governance.Calibration/Model.fs + Model.fsi           # drop unread `ComparisonSample.Agreement` (B6)
src/FS.GG.Governance.Snapshot/Interpreter.fs (+ Snapshot.fs/.fsi iff RawSensing/assemble change)  # GitUnavailable → assemble (B9)

# US3 (P2, Tier 2) — config loader threads values
src/FS.GG.Governance.Config/Schema.fs                # finish thunks: thread Some payloads, drop .Value/Option.get (B5)

# US4 (P2, Tier 2 + 1 export) — dedup into shared homes
src/FS.GG.Governance.EvidenceCommand/Interpreter.fs  # adopt CommandHost.guard/drive (A1; ref already present)
src/FS.GG.Governance.Scaffold/Interpreter.fs + .fsproj  # adopt guard/drive; NEW CommandHost ref (A1 fence edge)
src/FS.GG.Governance.JsonWriters/JsonWriters.fs (+ .fsi)  # gain the 4 writer pairs (A4)
  ↳ GatesJson/RouteJson/AuditJson/VerifyJson/ReleaseJson  # call the shared writers; GatesJson/ReleaseJson gain JsonWriters ref
src/FS.GG.Governance.SurfaceChecks/…                 # home for mkFinding/safe/valuesFor (domain+maturity params) (A6)
src/FS.GG.Governance.Kernel/Verdict.fs + Verdict.fsi # export combineReasons; Route.fs stakesOf reuses it (A6, Tier-1 export)
  ↳ a shared hashing home for sha256Hex (×4) + SddHandoff single buildGate  # per R2 fence decision

# US5 (P3, Tier 2) — dead code removal
src/FS.GG.Governance.DocsChecks/DocsChecks.fs + Model.fs/.fsi + Interpreter.fs  # remove Example* path + ExampleFact vocab (C1a)
src/FS.GG.Governance.VerifyCommand/Loop.fs + Loop.fsi  # remove write-only SurfacesPending (C1b)

# US6 (P3, Tier 2) — cosmetic hygiene
… 6 files with stale "no access modifiers" headers  # ReleaseReport/Gates/HumanRender/CostBudget/Findings/AttestationJson
… 3 command-host Interpreters with dead `open System.IO`  # VerifyCommand/ShipCommand/EvidenceCommand (+ optional 073 sweep)

# US7 (P3, Tier 2) — docs
src/FS.GG.Governance.VerifyCommand/FS.GG.Governance.VerifyCommand.fsproj  # document the 43-ref convention (comment)
docs/decisions/README.md (NEW index over 0001–0008; cross-link docs/adr/README.md org-ADR pointers)

# Untouched: published-version sources (Cli <Version>, ReferenceGateSet schemaVersion), org-synced build config.
```

**Structure Decision**: Keep the existing multi-project layout; introduce **no new project**. A6's
shared homes are *existing* upstream projects (`SurfaceChecks`, `Kernel`, the existing
`JsonWriters`) chosen because they already sit above their consumers — so the only new graph edges
are Scaffold→CommandHost (A1) and, if R2 confirms it is sound, a hashing-home edge for `sha256Hex`;
each is gated by the dependency-fence suite. Any helper whose sound home would require a *new*
project is re-deferred (FR-009) rather than justifying a project here — hence Complexity Tracking
stays empty.

## Complexity Tracking

> No Constitution Check violations — this section is intentionally empty. (If Phase 0 research finds
> that a sound A6 shared home genuinely requires a new project, that helper is re-deferred on #83
> per FR-009 rather than added here.)
