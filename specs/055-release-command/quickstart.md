# Quickstart & Validation: The `fsgg release` Host Command

Runnable validation scenarios proving the feature end to end. Types/flags referenced here are defined in
[data-model.md](./data-model.md) and [contracts/](./contracts/). All scenarios use real temporary
repository fixtures (the F016/F054/ShipCommand `withTempRepo` precedent); no network, registry, or
publishing provider is contacted.

## Prerequisites

- .NET `net10.0` SDK; repo restored/built: `dotnet build FS.GG.Governance.sln`
- New projects present in the solution: `FS.GG.Governance.ReleaseCommand`,
  `FS.GG.Governance.ReleaseJson`, and their `.Tests` projects.

## Build & test

```bash
# Build the two new projects
dotnet build src/FS.GG.Governance.ReleaseJson/FS.GG.Governance.ReleaseJson.fsproj
dotnet build src/FS.GG.Governance.ReleaseCommand/FS.GG.Governance.ReleaseCommand.fsproj

# Run the new test suites
dotnet test tests/FS.GG.Governance.ReleaseJson.Tests/FS.GG.Governance.ReleaseJson.Tests.fsproj
dotnet test tests/FS.GG.Governance.ReleaseCommand.Tests/FS.GG.Governance.ReleaseCommand.Tests.fsproj

# Bless the surface baselines after the .fsi surfaces stabilize
BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.ReleaseCommand.Tests/...   # then commit surface/*.txt
```

## Scenario 1 — Gate a passing release (P1, SC-001)

```bash
dotnet run --project src/FS.GG.Governance.ReleaseCommand -- release --repo <compliant-fixture>
echo $?   # expect 0
```

**Expected**: passing verdict; every rule listed satisfied; exit `0`. (Acceptance US1.1.)

## Scenario 2 — Block on an un-bumped version (P1, SC-002)

```bash
dotnet run --project src/FS.GG.Governance.ReleaseCommand -- release --repo <unbumped-fixture>
echo $?   # expect 1 (Blocked) — distinct from 2/3/4
```

**Expected**: failing verdict naming the version-bump rule as a blocker, the other five passing; exit `1`.
(Acceptance US1.2.)

## Scenario 3 — Deterministic `release.json` (P2, SC-003, SC-007)

```bash
dotnet run --project src/FS.GG.Governance.ReleaseCommand -- release --repo <fixture> --format both --out /tmp/r1.json
dotnet run --project src/FS.GG.Governance.ReleaseCommand -- release --repo <fixture> --format both --out /tmp/r2.json
cmp /tmp/r1.json /tmp/r2.json && echo "byte-identical"
```

**Expected**: `r1.json` == `r2.json` byte-for-byte; the JSON contains the verdict, every rule's finding
(base + effective severity, `met/unmet/unrecoverable` fact state, outcome, reason) and the per-family
evidence snapshot; it validates against the committed golden baseline and reports the same verdict and
per-rule outcomes as the text output. (Acceptance US2.1–2.4.)

## Scenario 4 — Fail safe on missing/invalid declaration (P3, SC-004, SC-005)

```bash
# No .fsgg/release.yml (or invalid):
dotnet run --project src/FS.GG.Governance.ReleaseCommand -- release --repo <no-release-yml>
echo $?   # expect 3 (InputUnavailable) — not 1, not 4

# Valid declaration but one governing source removed:
dotnet run --project src/FS.GG.Governance.ReleaseCommand -- release --repo <missing-source-fixture>
echo $?   # completes with a full six-family verdict; affected family reported unrecoverable/unmet
```

**Expected**: actionable "declaration unavailable/invalid" diagnostic + exit `3`; or, for a removed
source, the affected family reported `Unrecoverable`/unmet (never satisfied, never a crash) with a
complete six-family verdict. Bad argv ⇒ usage guidance + exit `2`. (Acceptance US3.1–3.3.)

## Scenario 5 — Network-free guarantee (SC-008)

The `ReleaseCommand.Tests` scope-guard test asserts no network dependency in the command's reachable
surface (the F054 precedent). Sensing reads only local files via `System.IO`.

## Validation checklist (maps to Success Criteria)

- [ ] SC-001: compliant fixture ⇒ pass + exit 0 in one invocation.
- [ ] SC-002: un-bumped fixture ⇒ exit 1, names version-bump blocker, distinct from failure codes.
- [ ] SC-003: two runs ⇒ byte-identical `release.json` and identical text verdict.
- [ ] SC-004: removed/corrupted source ⇒ family unrecoverable/unmet, 0% fabricated pass / crash.
- [ ] SC-005: five outcome classes ⇒ five distinguishable exit codes (0/1/2/3/4).
- [ ] SC-006: every run ⇒ six per-rule outcomes (incl. all-unrecoverable).
- [ ] SC-007: `release.json` validates against golden baseline; matches text verdict/outcomes.
- [ ] SC-008: no network access (scope guard green).
- [ ] Surface baselines committed for both new public projects; surface-drift tests green.
