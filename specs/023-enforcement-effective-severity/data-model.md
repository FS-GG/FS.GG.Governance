# Phase 1 Data Model: Enforcement Levers and Effective Severity

All types live in the single public module `FS.GG.Governance.Enforcement` (curated in `Enforcement.fsi`,
implemented in `Enforcement.fs`). The module **reuses** `FS.GG.Governance.Config.Model.Maturity` and
`...Model.ProfileId` verbatim (FR-003) and introduces only the vocabulary F014 did not model: `RunMode`,
`Severity`, `Profile`, and the decision records. No type carries raw YAML, a host path, a clock, or any
environment value (FR-006, FR-014).

References: requirements in [spec.md](./spec.md); decisions in [research.md](./research.md); the wire/decision
contract in [contracts/enforcement-decision.md](./contracts/enforcement-decision.md).

---

## Closed enumerations (levers & severity)

### `RunMode` — *where* the command runs (FR-001)

Closed, **ordered** set of exactly six values, least → most protective boundary:

| Case | Ordinal | Canonical token |
|---|---|---|
| `Sandbox` | 0 | `sandbox` |
| `Inner` | 1 | `inner` |
| `Focused` | 2 | `focused` |
| `Verify` | 3 | `verify` |
| `Gate` | 4 | `gate` |
| `Release` | 5 | `release` |

`runModeOrdinal : RunMode -> int` is the total ordinal map (the order is *intrinsic* to enforcement, so it is
exposed, not hidden). New here — **not** the kernel's three-value `RunMode` (research D2).

### `Profile` — *how strict* the project chose to be (FR-002)

Closed, **ordered** set of exactly four values, least → most strict: `Light`, `Standard`, `Strict`,
`Release`. Maps to/from F014 `ProfileId` (FR-003):
- `profileToProfileId : Profile -> ProfileId` (canonical tokens `light`/`standard`/`strict`/`release`)
- `profileOfProfileId : ProfileId -> Recognized<Profile>` (total; non-canonical id ⇒ `Unrecognized`)

> Note: both `RunMode` and `Profile` have a `Release` case (the most-protective mode and the strictest
> profile). They are distinct DUs; call sites qualify (`RunMode.Release` / `Profile.Release`).

### `Severity` — base and effective (FR-004)

Closed enumeration shared by base and effective severity so the two are directly comparable: `Advisory |
Blocking`. New here — **not** the kernel's `Severity` (research D2).

### `Maturity` — reused from F014 (FR-003)

`FS.GG.Governance.Config.Model.Maturity` verbatim: `Observe | Warn | BlockOnPr | BlockOnShip |
BlockOnRelease`. Re-exported by reference (the `.fsi` `open`s `FS.GG.Governance.Config.Model`); not
redefined.

---

## Records & result types

### `EnforcementInput` — the four levers for one finding

```fsharp
type EnforcementInput =
    { BaseSeverity: Severity   // the rule's intrinsic severity — an INPUT, never altered (FR-009)
      Maturity: Maturity       // reused F014 type — whether/where the rule may block
      Mode: RunMode            // where the command is running
      Profile: Profile }       // how strict the project chose to be
```

### `EnforcementDecision` — the explainable result (FR-010)

```fsharp
type EnforcementDecision =
    { BaseSeverity: Severity        // echoed BYTE-IDENTICAL from the input (FR-009, SC-003)
      Maturity: Maturity            // echoed
      Mode: RunMode                 // echoed
      Profile: Profile              // echoed
      EffectiveSeverity: Severity   // the DERIVED value (the output)
      Reason: string }             // deterministic, NON-EMPTY, names the responsible levers (FR-010)
```

All six FR-010 fields are present. The result is a single finding's decision — no rollup, blockers, verdict,
or exit code (FR-013). The caller pairs it with its own finding/verdict, which this core never receives or
mutates (research D5).

### `Recognized<'T>` — total string recognition (FR-011)

```fsharp
type Recognized<'T> =
    | Recognized of 'T
    | Unrecognized of string   // carries the offending value; never an exception, never a default
```

---

## The derivation (FR-005 total, FR-006 deterministic)

`deriveEffectiveSeverity : EnforcementInput -> EnforcementDecision`, defined for **every** combination of the
finite input domains and never throwing. Branch order (first match wins):

1. **Withhold** — `Maturity` ∈ `{Observe; Warn}` ⇒ `EffectiveSeverity = Advisory`, withhold reason
   (FR-007). Overrides mode and profile entirely.
2. **Base-advisory** — `BaseSeverity = Advisory` ⇒ `EffectiveSeverity = Advisory`, base-advisory reason
   (research D4: this core never escalates base-advisory).
3. **Blocking-eligible** — `BaseSeverity = Blocking` and maturity permits blocking. Compute
   `effectiveFloor = clamp(maturityFloor Maturity − profileTighten Profile, 0, 5)`:
   - `runModeOrdinal Mode ≥ effectiveFloor` ⇒ `EffectiveSeverity = Blocking`, blocking reason (FR-008).
   - else ⇒ `EffectiveSeverity = Advisory`, relaxed reason.

Internal (`.fs`-only) helpers, absent from the `.fsi`:
- `maturityFloor : Maturity -> int option` — `Observe/Warn → None`; `BlockOnPr → Some 4`; `BlockOnShip →
  Some 4`; `BlockOnRelease → Some 5` (research D3).
- `profileTighten : Profile -> int` — `Light → 0`; `Standard → 0`; `Strict → 1`; `Release → 2` (research
  D4).
- the four reason builders (research D6), each a fixed sentence over typed inputs.

### Invariants (verified by tests)

| Invariant | Source | Test |
|---|---|---|
| Total over the full cross-product; never throws | FR-005, SC-001 | `TotalityTests` |
| Deterministic — twice-run byte-identical | FR-006, SC-004 | `DeterminismTests` |
| `decision.BaseSeverity = input.BaseSeverity` always | FR-009, SC-003 | `CarryTests` |
| `Observe`/`Warn` ⇒ `Advisory` under any mode/profile | FR-007 | `DerivationTests` |
| Base-blocking blocks iff `mode ≥ effectiveFloor` | FR-008 | `DerivationTests` |
| `Reason` non-empty for every result | FR-010 | `TotalityTests` |
| Worked example reproduces exactly | SC-002 | `DerivationTests` |
| Mapping over N findings yields N decisions (no drop) | FR-012, SC-006 | `CarryTests` |
| Canonical names recognized; invalid ⇒ `Unrecognized` | FR-011, SC-005 | `RecognitionTests` |
