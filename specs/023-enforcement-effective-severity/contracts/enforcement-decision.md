# Decision Contract: Effective Severity, Reason Text, and Recognition (F023)

This is the behavioral contract the pure surface in [`Enforcement.fsi`](./Enforcement.fsi) must satisfy. It
is the stable decision every downstream consumer (the later `fsgg ship`, `audit.json`, CI, agents) reuses.
There is **no JSON wire format in this feature** — the derivation returns a typed `EnforcementDecision`;
serialization is a later row. The "contract" here is the truth table, the reason-text shapes, and the
recognition sets, all of which are byte-stable (FR-006, SC-004).

Canonical tokens (lower-case, from `docs/initial-design.md`):
- run modes: `sandbox` `inner` `focused` `verify` `gate` `release` (ordinals 0..5)
- profiles: `light` `standard` `strict` `release`
- maturities (F014): `observe` `warn` `block-on-pr` `block-on-ship` `block-on-release`
- severities: `advisory` `blocking`

---

## 1. Effective severity truth table

`effectiveFloor = clamp(maturityFloor − profileTighten, 0, 5)`, with
`maturityFloor`: `block-on-pr`→4, `block-on-ship`→4, `block-on-release`→5, `observe`/`warn`→none (D3); and
`profileTighten`: `light`→0, `standard`→0, `strict`→1, `release`→2 (D4).

**Rule (base = blocking):** `effective = blocking` iff `runModeOrdinal(mode) ≥ effectiveFloor`, else
`advisory`.
**Rule (base = advisory):** `effective = advisory` always (this core never escalates — D4).
**Rule (maturity observe/warn):** `effective = advisory` always, overriding mode and profile (FR-007).

Full base-blocking table (`B` = blocking, `·` = advisory; columns `sandbox inner focused verify gate
release`):

| Maturity \ Profile | sb | in | fo | ve | ga | re |
|---|---|---|---|---|---|---|
| observe / warn (any profile) | · | · | · | · | · | · |
| block-on-pr · light | · | · | · | · | B | B |
| block-on-pr · standard | · | · | · | · | B | B |
| block-on-pr · strict | · | · | · | B | B | B |
| block-on-pr · release | · | · | B | B | B | B |
| block-on-ship · light | · | · | · | · | B | B |
| block-on-ship · standard | · | · | · | · | B | B |
| block-on-ship · strict | · | · | · | B | B | B |
| block-on-ship · release | · | · | B | B | B | B |
| block-on-release · light | · | · | · | · | · | B |
| block-on-release · standard | · | · | · | · | · | B |
| block-on-release · strict | · | · | · | · | B | B |
| block-on-release · release | · | · | · | B | B | B |

**Worked example (SC-002):** `base=blocking, maturity=block-on-ship, mode=inner, profile=light` →
`effectiveFloor = 4`, `ordinal(inner)=1 < 4` → **advisory**. ✓

**Carry (FR-009, SC-003):** for every cell, `decision.BaseSeverity = input.BaseSeverity`,
`decision.Maturity/Mode/Profile = input.*`. Only `EffectiveSeverity` and `Reason` are derived.

---

## 2. Reason text (deterministic, non-empty — FR-010, D6)

One fixed sentence per branch, interpolating only the lower-case canonical tokens of the typed inputs.
`<m>` = maturity token, `<mode>` = run-mode token, `<profile>` = profile token, `<floor-mode>` = the run-mode
token at `effectiveFloor`.

| Branch | Condition | Reason text |
|---|---|---|
| withhold | maturity ∈ {observe, warn} | `maturity '<m>' withholds blocking; no run mode or profile can make it block` |
| base-advisory | base = advisory, maturity permits blocking | `base severity is advisory; '<profile>' profile does not escalate it (per-class strictness dials deferred)` |
| blocking | base = blocking, ordinal(mode) ≥ effectiveFloor | `run mode '<mode>' reaches the '<floor-mode>' blocking boundary for maturity '<m>' under '<profile>' profile` |
| relaxed | base = blocking, ordinal(mode) < effectiveFloor | `'<profile>' profile does not block this '<m>' finding outside the '<floor-mode>' boundary (run mode '<mode>')` |

Every reason is non-empty; none contains a clock, host path, or environment value. Identical inputs ⇒
byte-identical reason (SC-004).

---

## 3. String recognition (FR-011, US2, SC-005)

`recognizeMode`/`recognizeProfile`/`profileOfProfileId` are **total**:

| Input | Result |
|---|---|
| `recognizeMode "sandbox"` … `"release"` | `Recognized Sandbox` … `Recognized Release` (all six) |
| `recognizeMode "Gate"` / `"ship"` / `""` / `"  inner "` | `Unrecognized "<input>"` (exact-token match; no trim, no case-fold, no default) |
| `recognizeProfile "light"`/`"standard"`/`"strict"`/`"release"` | `Recognized Light` … `Recognized Release` (all four) |
| `recognizeProfile "lite"` / `"normal"` / `""` | `Unrecognized "<input>"` |
| `profileOfProfileId (ProfileId "strict")` | `Recognized Strict` |
| `profileOfProfileId (ProfileId "experimental")` | `Unrecognized "experimental"` |

The recognized sets are **exactly** the six modes and four profiles — no more, no fewer (SC-005). No input
ever throws and no unknown input silently maps to a default lever.

---

## 4. Out of scope (asserted by exclusion tests where practical)

No ship/merge verdict, blockers list, exit code, or cross-finding rollup (FR-013); no I/O, no
`.fsgg/policy.yml` parsing, no artifact, no CLI (FR-014); no per-class profile dial map
(`unknownPaths`/`staleEvidence`/…) — deferred (FR-015); base severity never mutated and no finding dropped
(FR-009, FR-012).
