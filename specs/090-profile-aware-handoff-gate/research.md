# Phase 0 — Research: Profile-Aware Handoff-Gate Enforcement

All "NEEDS CLARIFICATION" from the spec's Assumptions resolve here. The central one — the
`route --mode gate` ↔ enforcement-boundary mapping the spec flags as "confirm during planning" — is
resolved analytically against the enforcement truth table (D1).

---

## D1 — The route-mode → enforcement-mode mapping (`Gate → Verify`)

**Decision**: For the handoff-gate enforcement call, map the route `RunMode` to the enforcement
`RunMode` as `Sandbox → Sandbox`, `Inner → Inner`, **`Gate → Verify`**. Keep the Consumer's existing
maturity assignment (failing → `BlockOnShip`, satisfied → `Warn`).

**Rationale (truth-table proof)**. The enforcement core blocks iff
`runModeOrdinal(mode) >= maturityFloor(maturity) - profileTighten(profile)`
(`Enforcement.fs:205`). The relevant constants:

| symbol | value |
|---|---|
| `runModeOrdinal`: Sandbox 0, Inner 1, Focused 2, **Verify 3**, **Gate 4**, Release 5 | |
| `maturityFloor`: `BlockOnPr`/`BlockOnShip` → **4**, `BlockOnRelease` → 5, `Observe`/`Warn` → none | |
| `profileTighten`: Light 0, Standard 0, **Strict 1**, Release 2 | |

A failing handoff is a base-`Blocking`, `BlockOnShip` (floor **4**) gate. The matrix the
Templates#25 probe requires, holding `--mode gate` constant:

- strict + failing → **block**
- light + failing → **no block**
- (either) + satisfied → **no block**

Let `g` be the enforcement ordinal that `--mode gate` maps to. Blocking at the gate requires:
- **strict**: `g >= 4 - 1 = 3`  (must be **true**)
- **light**: `g >= 4 - 0 = 4`  (must be **false**)

`g >= 3` ∧ `g < 4` ⟹ **`g = 3` = `Verify`**. The mapping is *uniquely determined*. Verifying every cell with `Gate → Verify` (ordinal 3):

| profile | tighten | eff. floor | `3 >= floor`? | result |
|---|---|---|---|---|
| strict | 1 | 4−1 = 3 | yes | **Blocking → exit 2** ✓ |
| light | 0 | 4−0 = 4 | no | Advisory → exit 0 ✓ |
| absent → strict (D2) | 1 | 3 | yes | **Blocking → exit 2** ✓ (FR-004/006) |
| any, satisfied (`Warn`) | — | withheld | — | Advisory → exit 0 ✓ |

This is exactly the spec's stated "verify/ship line" (Assumption: mode mapping, FR-005): strict
tightens a ship-maturity gate down to the Verify boundary so it blocks, while light leaves the floor
at the ship boundary so a Verify-mode run is advisory.

**Alternatives considered**:
- **`Gate → Gate` (ordinal 4) + change Consumer failing → `BlockOnRelease` (floor 5)**. Also
  satisfies the matrix (`g = 4 = floor − 1` with floor 5). *Rejected*: it changes the SddHandoff
  Consumer's semantics (a failed handoff is *not* release-only), ripples into `Consumer.fs` and its
  tests, and contradicts the spec's "ship-blocking-maturity handoff gate" framing (FR-005). D1 keeps
  the change inside the CLI wiring and leaves the proven Consumer untouched.
- **`Gate → Gate`, maturity unchanged (`BlockOnShip`)**. *Rejected*: light blocks (`4 >= 4`) — the
  red cell never flips. This is essentially today's behavior expressed through the core.

**Validation**: the new `Cli.Tests` matrix (Phase 1) asserts all four cells; the
`Enforcement.Tests` 240-case oracle already proves the floor/tighten arithmetic independently.

---

## D2 — Profile resolution and the absent/unrecognized fail-safe (`→ Strict`)

**Decision**: Resolve the active profile from `PolicyFacts.DefaultProfile` (loaded by
`Config.Schema.parsePolicy`) through the existing total `Enforcement.recognizeProfile`. When the
policy is absent, `defaultProfile` is missing, or the value is not recognized as an enforcement
profile, resolve to **`Profile.Strict`**.

**Rationale**: FR-004 mandates absent → strict so a failing handoff still blocks, and FR-006 makes
the publish smoke (profile-less fixtures) the regression tripwire. Note this default is deliberately
*stricter* than the `ProfileId "standard"` default `RouteCommand.Loop` uses for
`ProductSurfaces.classify` (`Loop.fs:478`): that path governs cost-tier escalation, where
no-escalation is the safe default; the *gate-blocking* path must fail toward blocking, so its safe
default is `Strict`, not `Standard`. The two defaults are intentionally different and serve opposite
fail-safe directions. (An explicitly-declared `standard` profile correctly behaves like `light` for
this gate — tighten 0 — because that is how the enforcement core treats Standard for every gate;
only *omission* fails safe to strict.)

**Edge — unrecognized declared value**: `Config.Schema` already rejects a `defaultProfile` that is
not in the declared `profiles:` list with a `DanglingReference` diagnostic (validation failure
upstream). For a value that validates but is not an enforcement-recognized profile,
`recognizeProfile` returns the not-recognized case → resolve to `Strict` (never silently relax).

**Alternatives considered**: default to `Standard` (matches `Loop.fs`). *Rejected*: `Standard`
tightens by 0, so at `Verify` mode a `BlockOnShip` gate would **not** block — profile-less smoke
fixtures would stop blocking and the 089 baseline would regress. Fails FR-006.

---

## D3 — Reuse the canonical gate→enforcement construction (no handoff special-casing)

**Decision**: Build each handoff gate's `EnforcementInput` the same way `Ship.gateToInput` does —
`BaseSeverity = Blocking` iff the gate's `Maturity` is a block level, `Maturity = gate.Maturity`,
`Mode = mappedMode` (D1), `Profile = resolvedProfile` (D2) — and block iff **any** consumed gate
derives `EffectiveSeverity = Blocking`. Prefer literally reusing the `Ship`/`Enforcement` path over a
re-implementation.

**Rationale**: FR-001 requires the handoff gate flow through "the same effective-severity derivation
used by other gates", and the spec edge case forbids special-casing the handoff back to strict-only.
`Ship.rollup : route -> mode -> profile -> ShipDecision` (`Ship.fsi:60`) already performs exactly this
mapping for every selected gate; mirroring its `gateToInput` keeps one source of truth. `List.exists`
over the per-gate decision preserves the "relax one must not mask a still-blocking other" multi-gate
rule (spec edge case) and matches the existing `List.exists gateBlocks` shape.

**Alternatives considered**: a bespoke handoff-only severity rule. *Rejected* by FR-001 and the
no-special-case edge case; it would re-introduce a parallel boundary that could drift from the core.

---

## D4 — Surfacing attribution (the block names the failing handoff)

**Decision**: Carry `EnforcementDecision.Reason` from the blocking gate(s) into the route diagnostics
/ payload so the `GovernedBlocking` exit is attributable to the failing handoff (Acceptance Scenario
1.1; Principle VI). The handoff gates are already carried on the route payload for attribution
(`Cli.fs:439`); attach the per-gate effective-severity reason alongside.

**Rationale**: Principle VI requires operationally significant blocks to be explained, and the
acceptance scenario explicitly asks that the block be "attributable to the failing handoff." The core
already produces a non-empty self-explaining `Reason` — no new diagnostic machinery needed.

---

## D5 — Versioning and publish (`1.1.0 → 1.2.0`, workflow unchanged)

**Decision**: Bump `FS.GG.Governance.Cli.fsproj` `<Version>` to **`1.2.0`** (minor — new
externally-observable enforcement behavior; no contract/API surface change). No change to
`publish.yml`: it reads the version from the fsproj (`dotnet msbuild -getProperty:Version`), gates on
`cli-tests` + `enforcement-smoke`, and pushes `--skip-duplicate`.

**Rationale**: `1.1.0` is immutable on the feed (FR-007, spec edge case "immutable-version
collision"). A minor bump is correct: behavior visibly changes (light no longer blocks) but no public
API/contract surface is added or removed. The version is strictly orderable after `1.1.0` and resolves
within the consumer-pinned range (SC-005). `--skip-duplicate` makes re-runs idempotent and turns an
accidental re-push of an existing version into a no-op rather than a misleading "update."

**Alternatives considered**: major bump (`2.0.0`). *Rejected*: no breaking surface change — strict and
profile-less consumers (the safe default) see identical blocking behavior; only an opt-in `light`
profile observes the relaxation.

---

## D6 — Real-evidence guard for the new behavior (light smoke fixture)

**Decision**: Keep the existing profile-less smoke fixtures and assertions unchanged (they must still
block on a failing handoff = strict default, FR-006/SC-004). **Add** a third fixture
`tests/cli-publish-smoke/fixtures/light-failing-handoff/` carrying `.fsgg/policy.yml` with
`defaultProfile: light` (and a `profiles:` list declaring `light`) plus a failing
`governance-handoff.json`, and assert `route --root <fixture> --mode gate → exit 0`.

**Rationale**: Principle V (real evidence) and the green-by-omission guard. The existing smoke only
proves the strict/blocking baseline; the new behavior (light relaxes) deserves the same
packed-tool-installed-and-run evidence at publish time. This fixture is the in-repo mirror of the red
Templates#25 cell, so the publish gate fails if a build regresses the relaxation — symmetric to how it
already fails a build that regresses blocking.

**Alternatives considered**: rely solely on the downstream Templates#25 probe. *Rejected*: that probe
lives in another repo and runs after publish; a local real-evidence assertion fails *before* the bad
artifact reaches the feed.

---

## Cross-repo coherence (FR-009 / FR-010)

- **No contract surface bump.** `governance-handoff` stays `@1.0.0`; this is a consumer-side
  enforcement-behavior change. Record it as a coherence note on `FS-GG/.github`
  `registry/dependencies.yml` (mirroring 089's coherence entry), not a contract version bump.
- **Issue/board closure.** The acceptance signal is the Templates#25 Stage 6b matrix going fully
  green against the published `1.2.0`. Only then respond on + close **FS-GG/FS.GG.Governance#34** and
  move its Coordination board item to **Done**. Use the `cross-repo-coordination` protocol.
- **Decision record.** Capture D1 (the `Gate → Verify` mapping) and D2 (`absent → strict`) in
  `docs/decisions/0005-profile-aware-handoff-gate-mode-mapping.md` — the mapping was flagged "confirm"
  in the issue and is the non-obvious design choice future readers will question.
