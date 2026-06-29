# Implementation Plan: Profile-Aware Handoff-Gate Enforcement

**Branch**: `090-profile-aware-handoff-gate` | **Date**: 2026-06-29 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/090-profile-aware-handoff-gate/spec.md`

## Summary

Spec 089 published `FS.GG.Governance.Cli@1.1.0` and wired the consumed SDD handoff into the `route` exit, but it reaches the verdict through a **profile-blind, mode-only shortcut**:

```fsharp
// src/FS.GG.Governance.Cli/Cli.fs:397-409 (the shortcut this feature removes)
let private gateBlocks (gate: GatesModel.Gate) : bool =
    match gate.Maturity with
    | ConfigModel.BlockOnPr | ConfigModel.BlockOnShip | ConfigModel.BlockOnRelease -> true
    | ConfigModel.Observe | ConfigModel.Warn -> false
let handoffBlocking (mode: RunMode) (gates: GatesModel.Gate list) : bool =
    mode = Gate && List.exists gateBlocks gates       // ← never consults the policy profile
```

A failing handoff is consumed as a `BlockOnShip` gate (`Consumer.fs:122`), so `handoffBlocking` blocks at `--mode gate` for **every** product, regardless of `defaultProfile`. That is why the FS.GG.Templates#25 Stage 6b matrix is 30/31 green: the one red cell is **light + failing → expected exit 0, got exit 2**.

**Technical approach** — redirect the handoff-gate blocking decision through the **canonical Phase-5 enforcement core** (`Enforcement.deriveEffectiveSeverity`, the same derivation `Ship.rollup` already uses for every other gate), parameterized by the active policy profile, instead of the `mode = Gate && gateBlocks` shortcut. Concretely:

1. **Resolve the active profile at the product edge.** Read `PolicyFacts.DefaultProfile` (loaded by Config, `Schema.parsePolicy`) and recognize it into an `Enforcement.Profile` via the existing total `Enforcement.recognizeProfile`. **Absent / unrecognized → `Strict`** (fail-safe, FR-004 — note this is *stricter* than the `Standard` default `ProductSurfaces` uses; the gate-blocking decision must never relax by omission).
2. **Map the route run mode to the enforcement run mode** for the handoff-gate call: `Sandbox → Sandbox`, `Inner → Inner`, **`Gate → Verify`**. This is the "verify/ship line" the spec adopts (Assumption: mode mapping): with the handoff gate's `BlockOnShip` floor at ordinal 4 and `Verify` at ordinal 3, **strict** tightens the floor to 3 (blocks) while **light** leaves it at 4 (a Verify-mode run is below it → advisory). See research D1 for the truth-table proof that `Gate → Verify` is the *unique* mapping satisfying the matrix without changing the Consumer's maturity assignment.
3. **Replace `handoffBlocking`** with a derivation that, for each consumed handoff gate, builds an `EnforcementInput { BaseSeverity = (Blocking iff maturity is a block level); Maturity = gate.Maturity; Mode = mappedMode; Profile = resolvedProfile }` (the same construction `Ship.gateToInput` performs), calls `deriveEffectiveSeverity`, and blocks iff **any** gate's `EffectiveSeverity = Blocking`. A satisfied handoff (`Warn`/`Observe`) is withheld by the core under every profile, so it still passes. The `EnforcementDecision.Reason` is surfaced so the block stays attributable to the failing handoff (Acceptance Scenario 1; Principle VI).
4. **Ship as a new immutable version.** Bump `src/FS.GG.Governance.Cli/FS.GG.Governance.Cli.fsproj` `<Version>` `1.1.0 → 1.2.0` (FR-007; minor bump — new externally-observable enforcement behavior, no contract surface change). The existing `publish.yml` resolves the version from the fsproj; no workflow change.
5. **Guard the new behavior with real evidence at publish time.** Keep the existing profile-less smoke fixtures blocking (FR-006/SC-004) and **add a light-profile fixture** to `tests/cli-publish-smoke/` whose `.fsgg/policy.yml defaultProfile: light` proves `light + failing + --mode gate → exit 0` — the local mirror of the red Templates#25 cell.
6. **Resolve the cross-repo loop.** Record consumer-side coherence (governance-handoff stays `@1.0.0`, FR-009 — a behavior change, not a contract bump), add a decision record for the `Gate → Verify` mapping + `absent → strict` fail-safe, and once `1.2.0` is on the feed and Templates#25 Stage 6b is fully green, respond on + close **FS-GG/FS.GG.Governance#34** and move its Coordination board item to **Done** (FR-010, SC-007).

This is a **Tier 1 (contracted) change** — it alters observable enforcement behavior and ships a new published version the registry range gates — but it adds **no new F# public API surface**: the decision composes already-public `Enforcement` functions and lives inside the CLI executable's existing `route`-exit path, so no new `.fsi`/`surface.txt` baseline applies (mirroring 089's Tier-1-no-surface classification). The applicable obligations are the version, real test evidence, the coherence record, the decision record, and the issue/board closure.

## Technical Context

**Language/Version**: F# on .NET, target `net10.0` (org baseline; SDK `10.0.301` present). The only behavioral source edits are within `src/FS.GG.Governance.Cli/Cli.fs` (the executable's route-exit decision) plus the fsproj `<Version>`.

**Primary Dependencies**: All already present and unchanged —
- `FS.GG.Governance.Enforcement` (`deriveEffectiveSeverity`, `recognizeProfile`, `Profile`, `RunMode`, `EnforcementInput`) — the canonical Phase-5 core, exhaustively tested (240 cross-product cases in `FS.GG.Governance.Enforcement.Tests`).
- `FS.GG.Governance.Config` (`PolicyFacts.DefaultProfile`, `ProfileId`) — the loaded policy profile.
- `FS.GG.Governance.Adapters.SddHandoff` (`Consumer.consume` → `BlockOnShip`/`Warn` gates) — unchanged; the maturity assignment is *not* touched.
- `FS.GG.Governance.Ship` (`gateToInput`/`rollup`) — the reference construction the new derivation mirrors so the handoff gate flows through the *identical* path as every other gate.
- No new library dependency. No change to the pure rule/evidence core. Build/release tooling (`dotnet pack`/`nuget push`/`tool install`) is SDK-bundled and already wired in `publish.yml`.

**Storage**: N/A. Inputs: the loaded `.fsgg/policy.yml` (`defaultProfile`), the consumed `readiness/<id>/governance-handoff.json`, and committed smoke fixtures.

**Testing**:
- **Pure decision matrix** (fails-before / passes-after): the new profile-aware handoff-blocking decision under `{strict, light, absent}` × `{failing, satisfied}` at `--mode gate`, asserting the `ExitDecision` (`GovernedBlocking` vs `Success`). Lives in `FS.GG.Governance.Cli.Tests` driving the route evaluate path against in-memory fixtures (the existing `outcomeByRule`/`resultForHost` surface). The light+failing case fails against today's shortcut and passes after.
- **Real end-to-end smoke** (Principle V): `tests/cli-publish-smoke/run.sh` packs the actual tool, installs it, and asserts exit codes against committed fixtures — existing profile-less fixtures keep blocking; a new light-profile fixture asserts no-block.
- `Enforcement.Tests` (240-case truth table) and the existing CLI tests continue to gate publish (the `cli-tests` job, scoped past `WidthResilience`).

**Target Platform**: Linux/CI (`ubuntu-latest`, .NET `10.0.x`); `publish.yml` unchanged.

**Project Type**: Single F# solution (`FS.GG.Governance.sln`). No new projects, modules, or `.fsi`.

**Performance Goals**: Deterministic, pure decision; no hot path. `deriveEffectiveSeverity` is O(gates).

**Constraints**:
- **Fail-safe direction is one-way.** Any ambiguity in profile resolution (absent policy, missing `defaultProfile`, unrecognized value) resolves to `Strict`, never to relaxation. The publish smoke (profile-less fixtures) is the regression tripwire.
- **Do not special-case the handoff back to strict-only.** The whole point is that the handoff gate honors the profile like every other gate; the derivation must be the generic enforcement path, not a handoff-specific branch (edge case: "profile relaxes a non-handoff gate too").
- **Other route modes unchanged.** Only the handoff channel changes; the F07 route's own blocking (`hasBlockingFailure host.Route host.Facts`) is a separate channel and stays as-is. `sandbox`/`inner` map below any blocking floor, preserving today's advisory-in-light-modes behavior.
- **Version immutability.** `1.1.0` cannot be amended; ship `1.2.0`. `publish.yml` reads the version from the fsproj and pushes `--skip-duplicate`.
- **Drift-locked files off-limits**: `Directory.Build.props`, `Directory.Packages.props`, `.config/dotnet-tools.json` are org-synced — untouched.

**Scale/Scope**: One decision function rewritten in one executable, one `<Version>` bump, one new smoke fixture + assertion, one decision record, one cross-repo coherence note, one issue/board closure.

## Constitution Check

*GATE: evaluated pre-Phase 0 and re-checked post-Phase 1.*

| Principle | Status | Notes |
|---|---|---|
| **I. Spec → FSI → Semantic Tests → Implementation** | PASS (N/A for new API) | No new F# public surface — the decision composes already-public `Enforcement.deriveEffectiveSeverity`/`recognizeProfile`. Verified through a semantic decision-matrix test and a real packed-tool smoke (the honest audience), not internals. |
| **II. Visibility in `.fsi`** | PASS | No new public module; the changed binding (`handoffBlocking` and its private helpers) is `private` inside the `Cli.fs` executable and absent from `Cli.fsi`. No surface-area baseline delta. Confirmed `Cli.fsi` does not export the route-exit helpers. |
| **III. Idiomatic Simplicity** | PASS | A 3→6 `RunMode` match, a profile-resolution `match`, and a `List.exists` over `deriveEffectiveSeverity`. No clever F#, no new abstraction; reuses the canonical core rather than re-deriving severity. |
| **IV. Elmish/MVU boundary** | PASS | The decision is a pure function feeding the existing MVU route-exit (`resultForHost`/`HostCompleted`); no new I/O or stateful workflow. Profile is read at the edge (Config load) and carried as a value, matching the principle. |
| **V. Test Evidence Mandatory** | PASS | Pure matrix test fails-before (light currently blocks) / passes-after; real smoke adds a light-profile fixture exercising the published behavior. No assertion weakened. |
| **VI. Observability & Safe Failure** | PASS | The block surfaces `EnforcementDecision.Reason` (attributable to the failing handoff); unrecognized/absent profile fails safe to strict and is not swallowed. |
| **Genericity / Operating rule** | PASS | No rendering package ids/paths; profile is product-supplied configuration. |
| **Dependency-minimalism** | PASS | No new dependency; nothing enters the pure core. CLI/release layer only. |
| **Change Classification** | **Tier 1** | Declared: observable enforcement-behavior change + new published version the registry gates. No `.fsi`/`surface.txt` obligation (no F# surface delta). Obligations tracked: `<Version>`, coherence record (FR-009, no contract bump), decision record, real evidence, issue/board closure. |

**No violations** → Complexity Tracking left empty.

## Project Structure

### Documentation (this feature)

```text
specs/090-profile-aware-handoff-gate/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions D1–D6 (mode mapping proof, fail-safe default, …)
├── data-model.md        # Phase 1 — entities (profile, handoff gate, enforcement input, version)
├── quickstart.md        # Phase 1 — local repro of the matrix + smoke + dry-run publish
├── contracts/
│   └── profile-aware-gate.md   # the behavioral contract: the strict/light/absent × fail/satisfy matrix
└── checklists/
    └── requirements.md   # spec quality checklist (from /speckit-specify)
```

### Source Code (repository root)

```text
src/FS.GG.Governance.Cli/
├── Cli.fs                              # route-exit decision: replace `handoffBlocking`/`gateBlocks`
│                                       #   shortcut with profile-aware `deriveEffectiveSeverity` path;
│                                       #   resolve DefaultProfile (absent→Strict); map Gate→Verify
└── FS.GG.Governance.Cli.fsproj         # <Version> 1.1.0 → 1.2.0

tests/FS.GG.Governance.Cli.Tests/
└── (new) profile-aware handoff matrix test: {strict,light,absent} × {failing,satisfied} @ gate

tests/cli-publish-smoke/
├── run.sh                              # add the light-profile assertion (no-block)
└── fixtures/
    ├── failing-handoff/  (unchanged — profile-less; still blocks = strict default)
    ├── passing-handoff/  (unchanged — profile-less; passes)
    └── light-failing-handoff/          # NEW — .fsgg/policy.yml defaultProfile: light + failing handoff → exit 0

docs/decisions/
└── 0005-profile-aware-handoff-gate-mode-mapping.md   # NEW — Gate→Verify mapping + absent→strict fail-safe
```

Cross-repo (separate PRs / actions, outside this checkout):

```text
FS-GG/.github  registry/dependencies.yml   # coherence: governance-handoff@1.0.0 consumer behavior updated (no surface bump)
FS-GG/FS.GG.Templates#25                    # Stage 6b matrix flips fully green against 1.2.0
FS-GG/FS.GG.Governance#34                   # respond + close
Coordination board                          # item #34 → Done
```

**Structure Decision**: Single existing F# solution. The only behavioral in-repo source change is the route-exit decision in `Cli.fs` (an executable with no exported route-exit surface), plus the fsproj `<Version>`. Everything else is test evidence (one matrix test + one smoke fixture/assertion), a decision record, and the cross-repo coherence/closure. No new F# projects, modules, or `.fsi`.

## Complexity Tracking

> No constitution violations — section intentionally empty.
