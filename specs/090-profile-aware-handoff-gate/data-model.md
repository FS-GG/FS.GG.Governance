# Phase 1 — Data Model: Profile-Aware Handoff-Gate Enforcement

No new types are introduced. This feature **composes existing types**; the model below names the
entities the spec calls out and pins each to its already-shipped definition and how it flows through
the new decision.

## Entities (all pre-existing)

### Policy profile — `Enforcement.Profile`
`src/FS.GG.Governance.Enforcement/Enforcement.fs:27` · `.fsi` surfaced.
- Cases: `Light | Standard | Strict | Release`. Tokens: `"light" | "standard" | "strict" | "release"`.
- Source: `PolicyFacts.DefaultProfile : ProfileId` (`Config/Model.fsi`), loaded from `.fsgg/policy.yml`
  by `Config.Schema.parsePolicy`.
- Recognition: `Enforcement.recognizeProfile : string -> Recognized<Profile>` (total, never throws).
- **Resolution rule (this feature, D2)**: `DefaultProfile` → `recognizeProfile`; **absent / missing /
  unrecognized → `Strict`** (fail-safe). Distinct from the `Standard` default `ProductSurfaces` uses.

### Handoff gate — `Gates.Model.Gate`
`src/FS.GG.Governance.Gates/Model.fsi:74`. Produced by `Adapters.SddHandoff.Consumer.consume`.
- Relevant field: `Maturity : Config.Model.Maturity`.
- Consumer mapping (`Consumer.fs:122`, **unchanged**): failing/non-shippable/bad-doc → `BlockOnShip`;
  satisfied → `Warn`.
- Multiplicity: a run may consume **many** handoff gates; the decision blocks if *any* derives Blocking.

### Maturity — `Config.Model.Maturity`
`Observe | Warn | BlockOnPr | BlockOnShip | BlockOnRelease`. Floors (`Enforcement.fs:142`):
`Observe`/`Warn` → withheld (never block); `BlockOnPr`/`BlockOnShip` → 4; `BlockOnRelease` → 5.

### Run mode — route `RunMode` vs enforcement `RunMode`
- Route/Kernel `RunMode` (3 cases): `Sandbox | Inner | Gate` (`Kernel/Route.fs:17`; parsed in
  `Cli.fs:145`).
- Enforcement `RunMode` (6 cases): `Sandbox | Inner | Focused | Verify | Gate | Release` with ordinals
  0–5 (`Enforcement.fs:59`).
- **Mapping (this feature, D1)**: `Sandbox → Sandbox`, `Inner → Inner`, **`Gate → Verify` (ord 3)**.

### Enforcement input/decision — `Enforcement.EnforcementInput` / `EnforcementDecision`
`Enforcement.fs:43`. Built per handoff gate like `Ship.gateToInput`:
`{ BaseSeverity = (Blocking iff maturity is a block level); Maturity = gate.Maturity;
   Mode = mapped route mode; Profile = resolved profile }`.
Output carries `EffectiveSeverity : Severity (Advisory | Blocking)` and a non-empty `Reason`.

### Exit decision — `Cli.ExitDecision`
`Cli.fs:48`. `Success → 0`, `GovernedBlocking → 2`. The handoff channel yields `GovernedBlocking` iff
any gate's `EffectiveSeverity = Blocking` (replacing the `mode = Gate && gateBlocks` shortcut).

### Published CLI version
`FS.GG.Governance.Cli.fsproj` `<Version>`: `1.1.0 → 1.2.0`. Immutable on the org feed once pushed.

## Decision flow (the one path that changes)

```
.fsgg/policy.yml ──Config.parsePolicy──▶ PolicyFacts.DefaultProfile
                                              │ recognizeProfile, absent/unknown → Strict   (D2)
                                              ▼
route --mode gate ──map──▶ Enforcement RunMode = Verify                                     (D1)
                                              │
governance-handoff.json ──Consumer.consume──▶ Gate{ Maturity = BlockOnShip | Warn }
                                              │  per gate: gateToInput (BaseSeverity, Maturity, Mode, Profile)
                                              ▼
                          Enforcement.deriveEffectiveSeverity  ──▶ EffectiveSeverity, Reason  (D3)
                                              │  block iff ANY gate ⇒ Blocking
                                              ▼
                          ExitDecision = GovernedBlocking (exit 2) | Success (exit 0)         (+Reason, D4)
```

## State transitions

None. Every entity is an immutable value; the decision is a pure total function of
`(profile, mode, handoff gates)`. No persisted state, no workflow.

## Validation rules (carried, not added)

- `defaultProfile` must reference a declared `profiles:` entry, else `Config.Schema` emits a
  `DanglingReference` diagnostic (upstream validation failure) — unchanged.
- Profile recognition is total; an unrecognized recognized-result resolves to `Strict` (D2).
- `Consumer.consume` is pure/total: a malformed handoff document becomes a blocking integrity gate,
  never a throw — unchanged.
