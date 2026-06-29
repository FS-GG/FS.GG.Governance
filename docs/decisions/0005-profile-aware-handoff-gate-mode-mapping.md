# ADR 0005 — Profile-aware handoff-gate enforcement: the `Gate → Verify` mapping and the `absent → Strict` fail-safe

**Status**: Accepted · **Date**: 2026-06-29 · **Feature**: `specs/090-profile-aware-handoff-gate`

**Resolves**: the cross-repo request FS-GG/FS.GG.Governance#34 (a downstream `light`-profile
product's failing handoff must be advisory, not blocking, at `route --mode gate`) and the
FS.GG.Templates#25 Stage 6b red cell (`light + failing → expected exit 0, got exit 2`).

## Context

Spec 089 shipped `FS.GG.Governance.Cli@1.1.0`, which folds a consumed
`readiness/<id>/governance-handoff.json` into the `route` exit. But it reached the verdict through a
**profile-blind, mode-only shortcut**:

```fsharp
let handoffBlocking (mode: RunMode) (gates: Gate list) : bool =
    mode = Gate && List.exists gateBlocks gates       // never consults the policy profile
```

A failing handoff is consumed as a `BlockOnShip` gate, so this blocked at `--mode gate` for **every**
product regardless of `defaultProfile`. Every other gate in the system already derives its blocking
decision through the canonical Phase-5 core (`Enforcement.deriveEffectiveSeverity`, as `Ship.rollup`
uses); only the handoff channel bypassed it. That is why Templates#25 Stage 6b was 30/31 green — the
one red cell was the light-profile relaxation that the shortcut could not express.

## Decision

Route the handoff-gate blocking decision through `Enforcement.deriveEffectiveSeverity`, parameterized
by the active policy profile — the same derivation every other gate uses — instead of the
`mode = Gate && gateBlocks` shortcut. Two non-obvious sub-decisions:

### D1 — Map the route run mode `Gate → Verify` (the unique mapping)

The enforcement core blocks iff `runModeOrdinal(mode) ≥ maturityFloor(maturity) − profileTighten(profile)`.
Relevant constants: ordinals `Sandbox 0, Inner 1, Focused 2, Verify 3, Gate 4, Release 5`;
`BlockOnShip` floor `4`; tighten `Light 0, Standard 0, Strict 1, Release 2`.

A failing handoff is a base-`Blocking`, `BlockOnShip` (floor 4) gate. Holding `--mode gate` constant,
the required matrix is: strict+failing → block, light+failing → no block, (either)+satisfied → no
block. Let `g` be the ordinal `--mode gate` maps to:

- strict requires `g ≥ 4 − 1 = 3` (true)
- light requires `g ≥ 4 − 0 = 4` (false)

`g ≥ 3 ∧ g < 4 ⟹ g = 3 = Verify`. The mapping is **uniquely determined**. `Sandbox → Sandbox`,
`Inner → Inner` map below any blocking floor, so a failing handoff stays advisory in those modes under
every profile (the 089 light-mode behavior is preserved).

| profile | eff. floor | `3 ≥ floor`? | result at `--mode gate` |
|---|---|---|---|
| strict | 4 − 1 = 3 | yes | **Blocking → exit 2** |
| light | 4 − 0 = 4 | no | Advisory → exit 0 |
| absent → strict (D2) | 3 | yes | **Blocking → exit 2** |
| any, satisfied (`Warn`) | withheld | — | Advisory → exit 0 |

**Rejected**: `Gate → Gate` (ordinal 4) + changing the Consumer's failing maturity to `BlockOnRelease`
(floor 5) also satisfies the matrix, but it changes SddHandoff Consumer semantics (a failed handoff is
not release-only), ripples into `Consumer.fs` + its tests, and contradicts the "ship-blocking-maturity
handoff gate" framing. D1 keeps the change inside the CLI wiring and leaves the proven Consumer
untouched. `Gate → Gate` with the maturity unchanged is rejected because light would still block
(`4 ≥ 4`) — the red cell never flips.

### D2 — Resolve the profile, `absent / unrecognized → Strict` (one-way fail-safe)

Resolve the active profile from `PolicyFacts.DefaultProfile` (loaded by `Config`) through the total
`Enforcement.recognizeProfile`. When the policy is absent, `defaultProfile` is missing, or the value is
not an enforcement-recognized profile, resolve to **`Profile.Strict`** — never to relaxation.

This is deliberately **stricter** than the `Standard` default the cost-tier path
(`ProductSurfaces.classify`) uses: that path governs cost-tier escalation, where no-escalation is the
safe default; the *gate-blocking* path must fail toward blocking, so its safe default is `Strict`. The
two defaults serve opposite fail-safe directions. The publish smoke's profile-less fixtures (which must
still block) are the regression tripwire. An unrecognized-but-declared value (e.g. a custom
`"balanced"` that validates upstream because it is listed in `profiles:`) resolves to `Strict` via
`recognizeProfile`'s not-recognized branch — never silently relaxing.

**Rejected**: default to `Standard` (matching the cost-tier path). `Standard` tightens by 0, so at the
`Verify` boundary a `BlockOnShip` gate would not block — profile-less fixtures would stop blocking and
the 089 baseline would regress.

## Implementation note — the snapshot edge-read (scope, mirroring 089)

The plan framed the only behavioral source change as `Cli.fs`. In practice the CLI `route` path did not
load `.fsgg/policy.yml` at all (it read only SpecKit + DesignSystem facts), so the profile was not
available to the pure decision. Mirroring exactly the 089 scope correction that added `Handoffs` to
`ProjectSnapshot` (ADR 0004), this feature adds `ProjectSnapshot.DefaultProfile`, populated at the
same I/O edge (`ArtifactReading.loadSnapshot` via `Config.Loader.loadAndValidate`, total/safe — an
absent or invalid `.fsgg` degrades to `None` ⇒ the `Strict` fail-safe of D2). The pure
`Cli.resultForHost` then resolves and applies the profile. No new F# public surface is added: the
decision composes already-public `Enforcement` functions inside the executable's `route`-exit path, and
the route-exit helpers remain absent from `Cli.fsi`.

## Consequences

- **Tier 1, no contract-surface bump.** Observable enforcement behavior changes (light no longer
  blocks a failing handoff at gate) and a new immutable version ships, but no F# API/`governance-handoff`
  contract surface changes. `governance-handoff` stays `@1.0.0` — a consumer-side behavior change, not a
  contract bump. Shipped as `FS.GG.Governance.Cli@1.2.0` (minor).
- **Strict and profile-less consumers are unaffected** — they see identical blocking behavior; only an
  opt-in `light` (or `standard`) profile observes the relaxation.
- The behavior is guarded by a fails-before/passes-after decision matrix in
  `FS.GG.Governance.Cli.Tests` and a real packed-tool smoke fixture
  (`tests/cli-publish-smoke/fixtures/light-failing-handoff/`).
