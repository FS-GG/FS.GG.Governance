# Contract: `surfaceChecks` section in `verify.json` (produced by the `fsgg verify` host)

**Producing host**: `FS.GG.Governance.VerifyCommand` (`fsgg verify`)
**Projection**: `FS.GG.Governance.VerifyJson.ofVerifyDecisionWithPreview … findings preview`
**FR**: 003, 004, 005, 006, 007, 009 | **Spec**: [spec.md](../spec.md)

This contract reaffirms the F24 `surfaceChecks` shape originally drafted in
`specs/059-package-docs-skills-design-checks/contracts/verify-json-surfacechecks.md`, now bound to the
`fsgg verify` host as the **producing surface**. The projection function and field shape are unchanged; this
feature supplies the real `findings` argument (previously `[]`). `verify.json` is the **only** host output that
changes; `route.json`/`ship.json`/`release.json` stay byte-identical (FR-009).

## C1 — Additive, byte-identical when empty (FR-004)

- The projection is `ofVerifyDecisionWithPreview decision cache execution findings preview`. With
  `findings = []` its output is **byte-identical** to the pre-wiring host output (which already passed `[]`).
  `schemaVersion` is unchanged.
- The `surfaceChecks` array is emitted **only when `findings` is non-empty**. No declared surfaces ⇒ empty
  classification report ⇒ `Composition.run` returns `[]` ⇒ section omitted.

## C2 — Section shape (deterministic, FR-005, FR-006)

Each element, in `Composition.run` order (surface id, domain ordinal, file, detail, code):

```json
{
  "domain": "package",
  "surface": "<surface-id>",
  "code": "package.baseline-drift",
  "file": "<repo-relative-forward-slash>",
  "detail": "<stable locus>",
  "severity": "blocking",
  "inputState": false,
  "evidenceTag": "<declared-tag-or-omitted>",
  "message": "<deterministic message>"
}
```

- No absolute path, timestamp, username, or environment value (FR-006).
- `evidenceTag` is emitted only when the surface declared one (FR-006); omitted otherwise.
- `severity` is the base severity (`advisory` | `blocking`); advisory entries appear but never change the exit
  code (FR-007).
- Output is identical across re-runs over unchanged inputs and across input-discovery reorderings (FR-005).

## C3 — Enforcement unchanged (FR-007)

- The verify exit code and verdict are computed by the existing rollup at `RunMode.Verify` over the combined
  inputs: the existing gate outcomes plus, for each surface finding,
  `SurfaceChecks.Model.enforcementInputOf finding RunMode.Verify profile`, all run through the existing
  `deriveEffectiveSeverity`.
- A run whose only surface findings are advisory keeps the same exit code it would have had without them
  (SC-003). The truth table is not re-opened; no new rule, severity, or enforcement constant is introduced
  (FR-008).

## C4 — Read-only sensing at verify (FR-012)

- Surface sensing writes **nothing** to the working tree and spawns **no** process. The package domain is wired
  through a read-only port: an **absent baseline** yields the existing `package.baseline-absent` blocking finding
  but is **never written**, and declared transcripts are **not executed** at verify.
- Consequence: two consecutive `fsgg verify` runs over the same tree (including the absent-baseline case) emit
  byte-identical `verify.json` (C2 determinism, SC-004), and the working tree is unchanged after a run.

## Acceptance (maps to spec SC-001..SC-004 and 059 T045 acceptance 1–3)

1. **No declared surfaces** ⇒ `verify.json` is byte-identical to the pre-wiring golden (section omitted,
   schema version unchanged). [SC-002]
2. **Package surface whose baseline drifted** ⇒ `verify.json` contains a `surfaceChecks` entry
   `package.baseline-drift` with the drift detail and (if declared) the evidence tag; the verdict/exit code
   reflect the blocking finding at `RunMode.Verify`. [SC-001]
3. **Only-advisory surface finding** ⇒ the entry appears with `"severity":"advisory"` and the exit code is
   unchanged from a clean run. [SC-003]
4. **Re-run / reordered inputs** ⇒ byte-identical `verify.json`. [SC-004]
