# Implementation Plan: Command-host second extraction pass

**Branch**: `103-command-host-consolidation` | **Date**: 2026-07-03 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/103-command-host-consolidation/spec.md`

## Summary

Governance issue #49 (Epic #44) is the largest consolidation item from the 2026-07-02 review. The first extraction pass (`8e43c36`, already in `main`) lifted the shared `guard` and generic `drive` into `src/FS.GG.Governance.CommandHost/CommandHost.fs`. This second pass finishes the job: it removes the remaining per-host duplicated impure leaves and, in doing so, fixes the latent correctness defects the duplication was hiding.

Grounded research (see [research.md](./research.md)) confirmed and, in two cases, **corrected** the spec's initial framing:

- **M-CLI-3 (real, all 7 hosts + `Cli.requireValue`)** — every single-value option arm (`"--repo" :: v :: more`) accepts a `--`-prefixed token as the value, so `fsgg verify --repo --json` silently sets `Repo = "--json"` and drops JSON mode. The exact guard needed (`when not (t.StartsWith "--")`) already exists in these same parsers for the positional `--paths` consumer — it is just absent from the option arms. Parsers live in each host's `Loop.fs` (`go`/`loop`), not `Interpreter.fs`.
- **M-CLI-7 (inverted premise)** — JSON already always wins; `--plain` never overrides it. The genuine defect is narrower: EvidenceCommand parses `ExplicitPlain` and consumes it **nowhere** (a dead field), while every sibling and `Cli.fs` treat `--plain` as a documented additive ANSI-free signal. Fix = make Evidence's `--plain` consistent (a documented no-op, since Evidence emits no ANSI) instead of a silently-dead flag.
- **`realHandoffs` "sort drift" (not a live bug)** — the three genuine copies (Route/Ship/Verify) are byte-identical and use `String.CompareOrdinal`. The only outlier is `Cli/ArtifactReading.locateHandoffs`, which uses `Array.sortBy Path.GetFileName`; because F# string comparison is ordinal and the keys are unique, the order is identical today. Converging onto one shared `String.CompareOrdinal` implementation is a robustness/textual win, output-preserving.
- **F2 `ExitDecision` (dead canonical)** — `CommandHost.ExitDecision`/`exitCode` has **zero** call sites; all 7 hosts + `Cli.fs` re-declare an identical DU **in their `.fsi`**. Consolidating removes public surface from each host → a deliberate Tier-1 baseline change.
- **ArtifactReading copy** — `EvidenceCommand/Interpreter.fs:33-357` is a ~325-line copy that has already diverged (a dead `"present"` check exists only in the Cli original). The `.fsproj` already references Cli. But `ArtifactReading.fsi` exposes only 3 `RunRequest`-shaped functions, whereas Evidence calls unit/raw-root shapes — so dedup needs a small, deliberate `.fsi` widening (Tier-1) plus internal rewiring.
- **F15 / F13** — confirmed, both small and localized: Ship passes the pre-update model to `emitEffect` while Verify passes the post-update model (`Wrote(Ok)` micro-drift); EvidenceCommand's `update` lacks the `if model.Phase = Done then model, []` prelude every sibling has.

**Change classification is mixed.** The impure-leaf consolidation and the four bug fixes are Tier-2 (no `.fsi` change). The `ExitDecision` consolidation and the ArtifactReading dedup are Tier-1 (deliberate `.fsi` + surface-baseline updates, guarded by the SurfaceDrift tests). The plan therefore sequences the work so the Tier-2, zero-surface-risk fixes land first and the Tier-1 surface changes are isolated and explicitly baselined.

## Technical Context

**Language/Version**: F# on .NET `net10.0`. Touched projects: the seven command hosts (`FS.GG.Governance.{Route,Ship,Verify,Release,Refresh,Evidence,CacheEligibility}Command`), the shared `FS.GG.Governance.CommandHost`, and `FS.GG.Governance.Cli` (for `ArtifactReading` + `requireValue`).

**Primary Dependencies**: none added. Uses the existing shared `CommandHost` leaf, `HumanText/RenderMode`, and `Cli/ArtifactReading`. Built and tested through the bounded `dotnet fsi build.fsx test` entrypoint.

**Storage**: N/A (helpers do atomic file writes to the workspace; no schema).

**Testing**: Expecto suites per project + the consolidated SurfaceDrift tests (in `Tests.Common`, per #54). Real-evidence: drive the actual parsed command surface (`Loop.parse`/`go`) and the interpreters against a real temp filesystem. New RED→GREEN tests for M-CLI-3, M-CLI-7, F13, F15; unchanged-output + single-definition assertions for the consolidations.

**Target Platform**: local `dotnet fsi build.fsx test` and the GitHub Actions gate (full-suite job from #45/#102, deterministic gate, API-compat gate).

**Project Type**: F# library/CLI monorepo; per-command host projects over a shared effect/MVU model.

**Performance Goals**: N/A (refactor). No hot path touched; `writeAtomic` semantics unchanged.

**Constraints**:
- `.fsi` is the sole visibility source (Principle II). Impure-leaf relocation adds `val`s to `CommandHost.fsi` and deletes local `let`s — the local copies are in **no** `.fsi`, so that part is zero-surface. The `ExitDecision` and `ArtifactReading` items **do** change `.fsi`s and MUST update the matching surface baselines in the same change (Tier-1 discipline).
- No new dependency; core stays free of git/FS-scanning per the constitution (these helpers already live in the host layer, not the core).
- Behavior-preserving except the four enumerated fixes; the SurfaceDrift + full suite are the guardrail.
- The public `step` signature (`ports -> effect -> msg`) MUST NOT change — the shared snapshot/catalog arms are extracted into a helper `step` *calls*, keeping `step`'s 9 `.fsi`s untouched.

**Scale/Scope**: ~7 host projects + 2 shared projects. Estimated ~600–800 net source lines removed. ~7 `writeAtomic` copies, 3 `realHandoffs`, 3 `senseEnvironmentReal`/`senseBuilderReal`, 4 `step`-arm copies, 7+1 `ExitDecision` DUs, 1 ×325-line ArtifactReading copy.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Change Classification — mixed Tier 2 / Tier 1** (declared, not hidden).

| Principle | Assessment |
|---|---|
| **I. Spec → FSI → Semantic Tests → Implementation** | Followed. The only new public surface is `val`s added to `CommandHost.fsi` (shared leaves) and a small widening of `ArtifactReading.fsi`; each is sketched as a signature first (see [contracts/](./contracts/)) and exercised by tests through the packed/host surface. |
| **II. Visibility lives in `.fsi`** | Central. Relocating leaves adds `val`s to one `.fsi` and removes local `let`s (no `.fsi` today). The `ExitDecision` and `ArtifactReading` items intentionally *shrink*/adjust host `.fsi`s and update surface baselines in lockstep — a Tier-1 obligation the SurfaceDrift tests enforce. No `private`/`internal` keywords introduced. |
| **III. Idiomatic simplicity** | This is the principle operationalized: one definition per concern, plain functions, no new abstraction machinery. The shared argv value-guard reuses the `when not (t.StartsWith "--")` idiom already present for `--paths`. |
| **IV. Elmish/MVU boundary** | Preserved. `update` stays pure; the F13 guard is a pure short-circuit; `drive`/`step` extraction keeps effect interpretation at the edge. No boundary is crossed or blurred. |
| **V. Test evidence mandatory** | RED→GREEN tests for each of the four behavior fixes, driven through the real command surface + real temp FS. Consolidations covered by unchanged-output assertions + single-definition checks + the full suite. No synthetic evidence needed. |
| **VI. Observability & safe failure** | Improved: M-CLI-3 turns a silent flag-swallow into an explicit `MissingValue` error; F13 makes post-`Done` messages provably inert; M-CLI-7 removes a misleading dead flag. |

**Engineering Constraints**: net10.0 ✅; no new dependency ✅; no org-synced build-config edits ✅; genericity — nothing rendering-specific, helpers are host-generic ✅; `FS.GG.Governance.*` identity preserved ✅.

**Repo-owned-check justification**: no new CI check is added; existing SurfaceDrift + deterministic + API-compat gates are the guardrails. The API-compat gate is expected to report the deliberate `ExitDecision`/`ArtifactReading` surface deltas — those are intended and baselined, not regressions.

**Result: PASS** (with the Tier-1 surface changes declared and confined to Phase C below; Complexity Tracking not required — no principle violated, only a tier split that the constitution explicitly provides for).

## Project Structure

### Documentation (this feature)

```text
specs/103-command-host-consolidation/
├── plan.md              # This file
├── spec.md              # 3 prioritized stories + FR/SC (corrected per research)
├── research.md          # Phase 0 — decisions D1–D8 (the corrections above, with evidence)
├── data-model.md        # Phase 1 — the shared-leaf surface + which .fsi/baselines move
├── contracts/
│   └── shared-leaves.md # Phase 1 — signatures for the CommandHost additions + ArtifactReading widening + argv guard
├── quickstart.md        # Phase 1 — runnable RED→GREEN validation per story
└── checklists/
    └── requirements.md  # spec quality checklist (all pass)
```

### Source Code (repository root)

```text
src/FS.GG.Governance.CommandHost/
├── CommandHost.fs      # ADD shared impure leaves: writeAtomic, realHandoffs, senseEnvironmentReal,
│                       #   senseBuilderReal, and a shared snapshot/catalog step-arm helper;
│                       #   KEEP the dead ExitDecision only if adopted (else delete). Add a shared
│                       #   argv value-guard helper (requireValue that rejects a --prefixed token).
└── CommandHost.fsi     # ADD matching vals (Tier-1 for the new surface; baseline updated)

src/FS.GG.Governance.{Route,Ship,Verify,Release,Refresh,Evidence,CacheEligibility}Command/
├── Interpreter.fs      # DELETE local writeAtomic/realHandoffs/sense* copies → call CommandHost;
│                       #   extract shared step arms; Ship/Verify align Wrote(Ok) model (F15);
│                       #   Evidence: add Done-inertness guard (F13), call Cli/ArtifactReading (drop copy),
│                       #   make --plain a documented no-op (M-CLI-7)
├── Loop.fs             # Adopt the shared argv value-guard on every single-value option arm (M-CLI-3);
│                       #   remove per-host ExitDecision/exitCode in favor of CommandHost's (Tier-1);
│                       #   F9: converge format-flag vocabulary OR document each divergence
└── *.fsi               # Only where ExitDecision is removed / step surface is confirmed unchanged; baselines updated

src/FS.GG.Governance.Cli/
├── ArtifactReading.fs/.fsi  # Widen .fsi minimally so EvidenceCommand can reuse it (Tier-1); align locateHandoffs sort
└── Cli.fs                   # requireValue: reject --prefixed value (M-CLI-3) — the one shared spot in Cli

tests/…                 # New RED→GREEN tests (M-CLI-3, M-CLI-7, F13, F15); single-definition + unchanged-output checks
```

**Structure Decision**: Keep it in one feature branch, but implement in three ordered phases (A → B → C) so the risky surface work is isolated and independently revertible. See [tasks.md] (produced by `/speckit-tasks`) for the concrete ordering. The three phases:

- **Phase A — Tier-2 leaf consolidation + zero-surface bug fixes** (no `.fsi` change): relocate `writeAtomic`, `realHandoffs`, `senseEnvironmentReal`, `senseBuilderReal`, and the snapshot/catalog step arms into `CommandHost`; add the shared argv value-guard and apply it across all `Loop.fs` parsers + `Cli.requireValue` (M-CLI-3); F15 `Wrote(Ok)` alignment; F13 Evidence Done-guard; M-CLI-7 Evidence `--plain` no-op. All guarded by unchanged SurfaceDrift baselines.
- **Phase B — ArtifactReading dedup** (Tier-1, isolated): widen `ArtifactReading.fsi` minimally, rewire `EvidenceCommand` to call it, delete the ~325-line copy, update the Cli surface baseline. Biggest single LOC win.
- **Phase C — `ExitDecision` + F9 vocabulary** (Tier-1 + docs): adopt the canonical `CommandHost.ExitDecision` across hosts (or delete it if adoption proves noisier than value) and remove the per-host DUs + `.fsi` entries, updating each surface baseline; converge or explicitly document the four format-flag vocabularies (F9). This phase carries the most `.fsi`/baseline churn and is last so a time-box can stop cleanly after A/B with real value banked.

## Complexity Tracking

> No Constitution Check violation to justify. The Tier-1/Tier-2 split is an explicit constitutional provision (Change Classification), not a complexity exception; it is tracked as phases A/B/C above rather than here.
