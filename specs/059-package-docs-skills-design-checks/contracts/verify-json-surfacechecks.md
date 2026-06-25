# Contract: Additive `surfaceChecks` section in `verify.json` (F24, D8)

**Library**: `FS.GG.Governance.VerifyJson` (+ `VerifyCommand` wiring) | **FR**: 008, 009, 011, 014

`fsgg verify` surfaces the F24 findings additively, the exact F23/F052 precedent for `productSurfaces` in
`route.json`. This is the only change to a host's observable output in this row.

## C1 — Additive, byte-identical when empty (D8)

- `VerifyJson.ofVerifyResult` is **unchanged** — byte-identical output, existing goldens untouched.
- A new overload `ofVerifyResultWithSurfaceChecks … findings` emits a `surfaceChecks` array **only when
  `findings` is non-empty**. When `findings = []`, its output is byte-identical to `ofVerifyResult` on the
  same inputs. `schemaVersion` is unchanged.

## C2 — Section shape (deterministic, FR-010)

Each element (sorted by the `Composition.run` order — surface id, domain, location, code):

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

- No absolute path, timestamp, username, or environment value (FR-010).
- `evidenceTag` is emitted only when the surface declared one (FR-009); omitted otherwise.
- `severity` is the base severity (`advisory` | `blocking`); advisory entries appear but never change the
  exit code (FR-011).

## C3 — Enforcement unchanged (FR-014)

- The verify exit code and verdict are computed by the existing rollup at `RunMode.Verify` over the combined
  findings via `deriveEffectiveSeverity`. A run whose only surface findings are advisory keeps the same exit
  code it would have had without them (SC-006). The truth table is not re-opened.

## Acceptance

1. A repo with no declared product surfaces ⇒ `verify.json` is byte-identical to the pre-F24 golden (empty ⇒
   additive section omitted).
2. A repo with a package surface whose baseline drifted ⇒ `verify.json` contains a `surfaceChecks` entry
   `package.baseline-drift` with the drift detail and (if declared) the evidence tag; the verdict reflects the
   Blocking finding at `RunMode.Verify`.
3. A repo whose only surface finding is advisory ⇒ the entry appears with `"severity":"advisory"` and the
   exit code is unchanged from a clean run (SC-006).
