# Quickstart — Validate Stale-View Blocking

Runnable validation for SC-001…SC-006. Build first:

```bash
dotnet build FS.GG.Governance.sln
```

All scenarios use the real interpreters over a golden-fixture repo tree that declares generated views in
`.fsgg/refresh.yml`. "Stale" = a declared source digest (or generator version) differs from the view's recorded
provenance; "fresh" = all match (`FreshnessKey.matches`).

## SC-001 — Configured blocking ⇒ Fail + Blocked + named blocker

Fixture: `.fsgg/refresh.yml` with `currency-enforcement: block-on-ship` and ≥1 view whose source digest drifted.

```bash
dotnet run --project src/FS.GG.Governance.ShipCommand -- ship --root <stale-fixture>
echo "exit=$?"   # expect a blocked (non-zero) exit
```

Expect: `ship.json` has `verdict":"fail"`, `exitCodeBasis":"blocked"`, and a `generatedViews` entry naming the
stale view (`viewId`, `kind`, `cause`) with `effectiveSeverity":"blocking"`. The same fixture dialed to
`block-on-pr` blocks under `fsgg verify` too:

```bash
dotnet run --project src/FS.GG.Governance.VerifyCommand -- verify --root <stale-fixture-pr>
```

Test: `tests/FS.GG.Governance.ShipCommand.Tests/…` and `…VerifyCommand.Tests/…` E2E over the stale fixture.

## SC-002 — Unconfigured ⇒ byte-identical (opt-in safety)

Fixture: a stale view but **no** `currency-enforcement` key.

```bash
dotnet run --project src/FS.GG.Governance.VerifyCommand -- verify --root <stale-fixture-unconfigured>
dotnet run --project src/FS.GG.Governance.ShipCommand  -- ship   --root <stale-fixture-unconfigured>
```

Expect: no `generatedViews` field; `verdict`/`exitCodeBasis` unchanged. Guard: the additivity test asserts the
existing `route.json` / `audit.json` / `verify.json` / `ship.json` goldens are byte-identical. Confirm no
golden moved:

```bash
dotnet test FS.GG.Governance.sln
git diff --stat -- tests/**/goldens   # expect: no changes
```

## SC-003 — Truth-table sweep (0 new cases)

The pure leaf test sweeps the currency finding across every maturity × run mode × profile and asserts its
effective severity equals `deriveEffectiveSeverity` for the same inputs — proving no new truth-table branch.

```bash
dotnet test tests/FS.GG.Governance.CurrencyEnforcement.Tests --filter EnforcementSweep
```

## SC-004 — No-hide warning

Fixture: `currency-enforcement: block-on-ship`, evaluated under `fsgg verify` (run mode below the boundary).

```bash
dotnet run --project src/FS.GG.Governance.VerifyCommand -- verify --root <stale-fixture-ship-dial>
```

Expect: a `generatedViews` entry with `baseSeverity":"blocking"` **and** `effectiveSeverity":"advisory"` and a
lever-naming `reason` — visible, not dropped. Test: `…CurrencyEnforcement.Tests --filter NoHide` plus the verify
E2E warning case.

## SC-005 — Self-describing blocker

From SC-001's `ship.json`, a reader identifies the stale view (`viewId`/`kind`) and why (`cause` +
`drifted`/`reason`) from the document alone — no second artifact needed.

## SC-006 — Fresh ⇒ no finding (0 false positives)

Fixture: `currency-enforcement: block-on-ship` and **all views fresh**.

```bash
dotnet run --project src/FS.GG.Governance.ShipCommand -- ship --root <all-fresh-fixture>
echo "exit=$?"   # expect clean exit
```

Expect: no `generatedViews` field, `verdict":"pass"`, `exitCodeBasis":"clean"` — byte-identical to the same
change with the feature unconfigured.

## Re-bless (only the intended additive diffs)

```bash
# New leaf surface baseline + additive baselines (verify only the additive bindings moved):
BLESS_SURFACE=1 dotnet test
git diff -- surface/   # expect: new CurrencyEnforcement.surface.txt + additive lines only

# New configured-blocking goldens (existing goldens must NOT move):
BLESS_GOLDEN=1 dotnet test tests/FS.GG.Governance.ShipCommand.Tests
BLESS_GOLDEN=1 dotnet test tests/FS.GG.Governance.VerifyCommand.Tests
git diff --stat -- tests/**/goldens   # expect: only the NEW stale/fresh fixtures
```
