# Quickstart: verify the Config re-typing (behavior + surface parity)

This guide proves the feature end-to-end: the four `.fsgg` supported versions are single-sourced
from `FS.GG.Contracts`, and nothing observable changed. All commands run from the repo root
(`/home/developer/projects/FS.GG.Governance`). See [contracts/fsgg-contracts-consumption.md](./contracts/fsgg-contracts-consumption.md)
for the consumed-symbol contract and [data-model.md](./data-model.md) for the shared-vs-local
boundary.

## Prerequisites

- .NET SDK with `net10.0`, F# (FSharp.Core 10.1.301).
- Authenticated access to the org GitHub Packages feed (so `FS.GG.Contracts 1.0.1` restores).
- A clean working tree on branch `087-retype-config-onto-contracts`.

## 1. Restore at the pinned version (C1 / SC-005, FR-001/FR-010)

```bash
dotnet restore FS.GG.Governance.sln            # locked-mode restore (default for this repo)
```

**Expected:** restore succeeds; `src/FS.GG.Governance.Config/packages.lock.json` contains
`FS.GG.Contracts` resolved `1.0.1` (Direct) and `FSharp.Core` (CentralTransitive). A restore
error here means the feed/credential is missing or the lockfile is stale — regenerate with
`dotnet restore FS.GG.Governance.sln --force-evaluate` and re-commit the lockfiles.

## 2. Full build (FR-011)

```bash
dotnet fsi build.fsx                            # builds all projects of FS.GG.Governance.sln
```

**Expected:** every project compiles, including the 50+ downstream consumers of `Config`
(SC-007 compile evidence).

## 3. Full test suite — the delivery gate (SC-001, FR-005/006/008/011)

```bash
dotnet fsi build.fsx test
```

**Expected:** same project + test-pass counts as before the change. Specifically green:
- Config validation tests — identical typed facts for the valid `.fsgg` fixtures (SC-002).
- Config diagnostic tests — identical id/locator/message for every malformed/edge fixture,
  including `capabilities.yml schemaVersion: 1` → `UnsupportedSchemaVersion` + migration pointer
  (SC-003, FR-005).
- Determinism property tests — reordered YAML still yields byte-identical output (FR-006).
- Command/projection golden + snapshot tests — byte-identical downstream output (SC-006, FR-008).

## 4. Surface parity (C5 / FR-009, SC-007)

```bash
dotnet fsi pack-and-apicheck.fsx --json         # or the Config SurfaceDriftTests in step 3
git diff --exit-code surface/FS.GG.Governance.Config.surface.txt
```

**Expected:** the surface-drift test passes and `git diff` reports **no change** to
`surface/FS.GG.Governance.Config.surface.txt` — the public Config surface is byte-identical (no
re-export, no moved symbol).

## 5. Single-source guard (C2/C3 / SC-004, FR-002)

```bash
# The four supported versions are read from the package, not literals:
grep -n "Schemas\.\(governance\|policy\|capabilities\|tooling\)Version" \
  src/FS.GG.Governance.Config/Schema.fs
# And no stray supported-version literal remains in supportedVersionFor:
grep -n "SchemaVersion [12]" src/FS.GG.Governance.Config/Schema.fs   # expect: no matches
```

**Expected:** the first grep shows all four `Schemas.*Version` reads; the second shows no
hard-coded `SchemaVersion 1` / `SchemaVersion 2` in `supportedVersionFor`. Resolved values:
`capabilities → 2`, `project/policy/tooling → 1` (C2).

## 6. Forward-compat spot check (US3 / FR-001) — optional, by inspection

Confirm that the *only* edit a future version bump needs is the `fsgg-contracts` registry pin +
the central `PackageVersion`, never `Schema.fs`: the supported version flows from
`Fsgg.Schemas.*`, so re-pinning `FS.GG.Contracts` and re-restoring changes the resolved value
with no Governance source edit.

## Done when

- [ ] Steps 1–5 pass with the expected results.
- [ ] `git diff` is empty for `surface/FS.GG.Governance.Config.surface.txt` and for every
      command/projection golden + snapshot fixture.
- [ ] `packages.lock.json` files reflect the new dependency and restore in locked mode.
