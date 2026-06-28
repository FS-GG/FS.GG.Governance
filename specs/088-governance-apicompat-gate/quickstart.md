# Quickstart: Validating the Breaking-Change (API-Compat) Gate

A run/validation guide. Implementation detail belongs in `tasks.md`; this proves the feature end-to-end against real evidence.

## Prerequisites

- .NET SDK `10.0.x` (matches `gate.yml`); repo restores in locked mode.
- The local folder feed `~/.local/share/nuget-local/` (constitution-mandated pack output / baseline source).
- Build the solution once: `dotnet fsi build.fsx` (or `dotnet build FS.GG.Governance.sln -c Debug`).

## 1. Pure verdict — the heart of the feature (fastest signal)

Exercise `apiCompatibilityFact` directly (the D4 table). Real-evidence: feed real `ApiBreakSignal` × `VersionDelta`, assert `FactState`.

```bash
dotnet test tests/FS.GG.Governance.PackEvidence.Tests   -c Debug
dotnet test tests/FS.GG.Governance.ReleaseRules.Tests   -c Debug
```

Expected — the verdict table holds:
- `BreakingChanges _` + `MinorOrPatchBump` ⇒ `Unmet` ⇒ finding in **Blockers** (at `BlockOnRelease`) / **Warnings** (advisory).
- `BreakingChanges _` + `MajorBump` ⇒ `Met` ⇒ **Passing**.
- `NoBreakingChanges` ⇒ `Met`. `NoBaseline` ⇒ `Met` (reported `NoBaselineYet`). `Indeterminate _` ⇒ `Unrecoverable` ⇒ Violated (fail-safe).
- `NotPackable` ⇒ no fact, listed `NotCovered`.

## 2. FSI transcript (Principle I — design-through-use)

```fsharp
// scripts/prelude.fsx context
open FS.GG.Governance.PackEvidence
open FS.GG.Governance.ReleaseRules.Model
apiCompatibilityFact (BreakingChanges [ { Member = "Foo.bar"; Kind = MemberRemoved } ]) MinorOrPatchBump
//  ⇒ Some Unmet
apiCompatibilityFact (BreakingChanges [ ... ]) MajorBump        //  ⇒ Some Met
apiCompatibilityFact NoBaseline NoBaselineDelta                 //  ⇒ Some Met
apiCompatibilityFact (Indeterminate "feed unreachable") MajorBump //  ⇒ Some Unrecoverable
apiCompatibilityFact NotPackable NoBaselineDelta                //  ⇒ None
```

## 3. Detector against real packages (sensor, real evidence where feasible)

```bash
# pack Release + compare each .nupkg against its baseline on the feed
dotnet fsi pack-and-apicheck.fsx --json
```

Expected:
- Packages with a prior version on the feed ⇒ `NoBreakingChanges` / `BreakingChanges [...]`.
- Packages never published ⇒ `NoBaseline` (the common case at rollout — reported, not hidden).
- Feed missing / unreadable assembly ⇒ `Indeterminate` (NOT clean).

Sensor tests that need a known break use a real packed fixture; if a real baseline is unavailable in CI, the fixture is `Synthetic`-tagged and disclosed (Principle V).

## 4. End-to-end via the governance command (advisory phase)

```bash
fsgg verify --repo .     # or: fsgg release --repo .
```

Expected (advisory `Maturity`): any breaking-under-bump or indeterminate package shows in **Warnings** with a reason naming the package, the break, and "requires MAJOR bump or revert"; `Verdict` is unaffected; exit code unchanged. Coverage (Checked / NoBaselineYet / NotCovered) is printed (FR-007/FR-012).

## 5. Prove the required phase (after SC-005)

Flip the declared `ApiCompatibility` rule `Maturity` → `BlockOnRelease` (and add the CI job to required checks), then:

1. Introduce a deliberate breaking change to one package **without** a major bump → `fsgg release` exits `Blocked`; the CI job fails with the finding.
2. Bump that package **major** (or revert the break) → passes.
3. A purely additive change with a minor bump → passes (no finding).

## 6. Surface baselines stay coherent (FR-010)

For any touched packable project, refresh its reflective snapshot:

```bash
BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.<Project>.Tests
```

The `surface.txt` drift guard and the ApiCompat gate must not contradict — the single remediation path for a legitimate change is: update `.fsi` → `BLESS_SURFACE=1` refresh → bump version (MAJOR if breaking).

## Current status (advisory MVP)

The advisory gate is delivered through the **detector script + advisory CI job**, grading through the REAL
pure library:

- `dotnet fsi pack-and-apicheck.fsx --selftest` — proves the pure grading path (the D4 table) end-to-end.
- `dotnet fsi pack-and-apicheck.fsx [--json]` — packs every `IsPackable=true` project, compares each against
  its baseline on the feed via ApiCompat, and prints per-package coverage (`Checked`/`NoBaselineYet`/
  `NotCovered`) + any breaking-under-bump findings with the remediation text ("requires a MAJOR version bump
  or revert"). Exit 0 (advisory, D7). At rollout every package resolves `NoBaselineYet` (research D5).
- CI job **"API compatibility gate (breaking-change → SemVer major) [advisory]"** in `gate.yml` runs the
  detector on every PR (non-required, `continue-on-error`).

The **in-product `fsgg verify`/`release` Warnings integration** (the MVU host overlay routing the
`ApiCompatibility` fact through the `ReleaseCommand` Loop) and the **required-phase promotion** (flip the
declared rule `Maturity` → `BlockOnRelease` + add the CI job to required checks) are the **US2 / required**
work — they change a verdict only once baselines accrue (SC-005) and are tracked there. `deriveFacts` already
emits `ApiCompatibility = Unrecoverable` (fail-safe), so a declared rule is `Violated` until the host overlay
supplies the real value.

The single remediation path that clears BOTH guards (the `surface.txt` drift guard and this gate) for a
legitimate breaking change: **update `.fsi` → `BLESS_SURFACE=1` refresh → bump the version MAJOR** (or revert).

## Success-criteria mapping

| Check | Criteria |
|---|---|
| Step 1/2 verdict table | SC-002 (no false negatives), SC-003 (no false positives) |
| Step 3 coverage output | SC-001 (100% covered-or-reported, zero silent passes) |
| Step 4 advisory output | SC-005 (advisory reports without blocking) |
| Step 5 required behavior | SC-004 (no breaking-under-bump release completes) |
| Step 6 single remediation | SC-006 (one documented path through both guards) |
