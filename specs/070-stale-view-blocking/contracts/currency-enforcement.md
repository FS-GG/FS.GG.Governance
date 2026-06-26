# Contract — Stale-View Currency Enforcement

The wire/behavior contract F070 must satisfy. Every clause reuses an existing core; nothing here adds a new
severity, run mode, profile, maturity value, or truth-table branch.

## C1 — Configuration key (the maturity dial)

`.fsgg/refresh.yml` MAY carry a manifest-level key:

```yaml
currency-enforcement: block-on-ship   # one of: observe | warn | block-on-pr | block-on-ship | block-on-release
views:
  - id: route-projection
    kind: route-projection
    output: docs/route.generated.json
    sources: [ ".fsgg/route.yml", "src/**/*.fs" ]
    generator: [ "fsgg", "route", "--json" ]
    generator-basis: tool-version
```

- The value parses through the **existing** `Config.Model.Maturity` vocabulary (no new value).
- **Absent ⇒ `None` ⇒ advisory/opt-in.** With the key absent, behavior is byte-identical to today (C5).
- The key is parsed into `GenerationManifest.CurrencyEnforcement: Maturity option` by
  `RefreshCommand.Declaration.parse`; `Declaration.parse`'s signature is unchanged. The field is **not**
  projected into `refresh.json`.

## C2 — Verdict fold rule (`foldViewCurrencyVerdict`)

For the active run mode + profile, a stale-view finding's effective severity is
`deriveEffectiveSeverity (enforcementInputOf finding mode profile)` — the **existing** truth table, called
verbatim.

- If **any** stale-view finding has effective severity `Blocking`, the `ShipDecision` becomes
  `Verdict = Fail`, `ExitCodeBasis = Blocked`.
- Otherwise the `ShipDecision` is **unchanged** (identity) — including the unconfigured case, where the finding
  list is empty.
- The finding is **never** added to `Blockers`/`Warnings`/`Passing` (those carry the closed `EnforcedItem`;
  D2). The verdict still reflects it via `Verdict`/`ExitCodeBasis`, exactly as `foldSurfaceVerdict` does for
  surface findings.

`fsgg verify` folds at `RunMode.Verify`; `fsgg ship` folds at `RunMode.Gate`. A finding configured
`block-on-ship`/`block-on-release` is therefore effective-`Advisory` under verify (a warning, FR-009) and
effective-`Blocking` under ship. A finding configured `block-on-pr` blocks under `fsgg ship` and — because the
`block-on-pr` floor is the **gate** run mode — blocks under `fsgg verify` **only under a `strict` (or
`release`) profile**, which tightens the floor down to the verify run mode; under the default `standard`
profile it is a warning under verify (C1 / FR-009).

## C3 — `generatedViews` wire shape (additive detail)

A new projection overload (`ofVerifyDecisionWith…` / the `ship.json` analogue / `ofAuditDecisionWith…`) emits
an additive top-level array. **Per finding**, in this field order:

```json
{
  "generatedViews": [
    {
      "viewId": "route-projection",
      "kind": "route-projection",
      "cause": "source-drift",
      "drifted": ["coveredArtifacts", "generatorVersion"],
      "baseSeverity": "blocking",
      "effectiveSeverity": "blocking",
      "reason": "<the EnforcementDecision.Reason naming the responsible levers>"
    }
  ]
}
```

- `viewId` / `kind` name the stale view (FR-005); `kind` uses `RefreshModel.viewKindToken`.
- `cause` is `staleCauseToken` (`source-drift` | `undeterminable`). For `source-drift`, `drifted` is the
  `InputCategory` token list (verbatim from `CurrencyStatus`); for `undeterminable`, a **`detail`** string
  replaces `drifted` and names why currency could not be determined (FR-008). (`detail` — not `reason` — so it
  never collides with the trailing `reason` enforcement-lever field below; this fixes the duplicate-key in an
  earlier draft of this example.)
- `baseSeverity` **and** `effectiveSeverity` are **both** present (no-hide, FR-006); `reason` is the
  `EnforcementDecision.Reason`.
- The array is sorted by `viewId` (stable, deterministic).
- **Omitted-when-empty**: when there are no stale-view findings, the `generatedViews` field is **absent
  entirely** (not `[]`). This is what makes existing goldens byte-identical when unconfigured or all-fresh (C5).

## C4 — No-hide warning grammar

When the configured maturity / run mode / profile relaxes a finding (effective `Advisory`):

- the finding **still appears** in `generatedViews` with `effectiveSeverity: "advisory"` alongside its
  `baseSeverity: "blocking"` and the lever-naming `reason`;
- it is **never** dropped;
- the carried `cause`/`drifted` (the underlying `CurrencyStatus`) is **unchanged** by the relaxation.

A view that is `Current` produces **no** entry in any partition (FR-007/SC-006).

## C5 — Determinism & unconfigured byte-identity

- For identical repository state, the `generatedViews` array and the whole document are **byte-identical** —
  no wall-clock, git re-sensing, env, absolute path, locale, or collection-order leakage. `decideCurrency` and
  the projection sort every collection by a stable key and read no clock/env/path.
- With `currency-enforcement` **absent** (or all views `Current`), **0 bytes** of every existing `route.json`
  / `audit.json` / `verify.json` / `ship.json` golden change, and no verdict or exit-code basis changes
  (SC-002). Guarded by an explicit additivity test that freezes the existing goldens.

## C6 — Additivity

- **No** existing public projection signature changes — every new entry point is an additive overload; the
  existing `ofVerifyDecision`/`ofShipDecision`/`ofAuditDecision` are untouched.
- The closed cores (`EnforcedItemId`, `FindingId`, `Severity`, `Maturity`, `RunMode`, `Profile`, the F024
  partition, `deriveEffectiveSeverity`) are reused verbatim — **never reopened**.
- The only additive type edit is `GenerationManifest.CurrencyEnforcement: Maturity option`; existing fields are
  neither removed nor reordered.
- No new artifact file is produced — the stale-view detail rides inside the existing documents.
