# Quickstart / Validation: Centralize an intentional VersionPrefix

**Feature**: 109-version-prefix-centralize · **Date**: 2026-07-03

Runnable validation that the change satisfies the [contract](./contracts/version-baseline.md). All
commands run from the repo root. This is real-evidence verification (Constitution V): the change is a
single build property, so the proof is a graph-wide before/after effective-version diff, not a new
test.

## Prerequisites

- .NET SDK `10.0.x`; restore working (org-feed token if restoring cross-repo — see the private-feed
  note in memory).
- A clean tree on branch `109-version-prefix-centralize`.

## Step 0 — Capture the BEFORE map (do this first, before editing)

```sh
git ls-files '*.fsproj' | sort | while read f; do
  v=$(dotnet msbuild "$f" -getProperty:Version -nologo 2>/dev/null | tr -d '[:space:]')
  printf '%s\t%s\n' "$v" "$f"
done | tee /tmp/versions.before.txt
```

Expected highlights before the change: the four version-less packable projects and all ~100
non-packable projects read `1.0.0`; `Cli` reads `1.2.0`; `Kernel` `0.1.1`; the ~66 libs `0.1.0`.

## Step 1 — Apply the change

Add the centralized prefix to `Directory.Build.local.props` (a commented `<PropertyGroup>` with
`<VersionPrefix>0.1.0</VersionPrefix>`). No other file changes. See [contract C1](./contracts/version-baseline.md).

## Step 2 — Capture the AFTER map and diff it

```sh
git ls-files '*.fsproj' | sort | while read f; do
  v=$(dotnet msbuild "$f" -getProperty:Version -nologo 2>/dev/null | tr -d '[:space:]')
  printf '%s\t%s\n' "$v" "$f"
done | tee /tmp/versions.after.txt

diff <(cut -f1- /tmp/versions.before.txt) <(cut -f1- /tmp/versions.after.txt)
```

**Expected diff**: every project that read `1.0.0` before now reads `0.1.0`; **no other line
changes**. In particular `Cli` stays `1.2.0`, `Kernel` stays `0.1.1`, and every explicitly-pinned
`0.1.0` library is unchanged (identical line before/after). → [C2](./contracts/version-baseline.md),
[C3](./contracts/version-baseline.md).

## Step 3 — Prove the published artifacts are invariant

```sh
# CLI published version = its fsproj <Version> (publish.yml resolve-version reads exactly this)
dotnet msbuild src/FS.GG.Governance.Cli/FS.GG.Governance.Cli.fsproj -getProperty:Version   # → 1.2.0

# ReferenceGateSet published version is injected by the pack script, not the fsproj default:
grep -n 'Version' pack-reference-gate-set.fsx    # confirm -p:Version is derived from schemaVersion
```

Expected: CLI `1.2.0`; ReferenceGateSet packed version still driven by `pack-reference-gate-set.fsx`.
→ [C4](./contracts/version-baseline.md).

## Step 4 — Prove org-synced config is byte-identical

```sh
git diff --exit-code main -- \
  Directory.Build.props Directory.Packages.props .config/dotnet-tools.json \
  && echo "OK: org-synced build config byte-identical"
```

Expected: empty diff, exit 0. → [C5](./contracts/version-baseline.md).

## Step 5 — Full build + test green

```sh
dotnet build FS.GG.Governance.sln -c Release
dotnet test  FS.GG.Governance.sln
```

Expected: build and the full suite green (no code/API/JSON change). → [C6](./contracts/version-baseline.md).

## Step 6 — (Optional) confirm the api-baseline gate is unaffected

```sh
dotnet fsi pack-and-apicheck.fsx | grep -iE 'fsgg|no-baseline|RouteCommand|EvidenceCommand|CacheEligibility'
```

Expected: the three lowered tools report `no-baseline` (they were never published), before and after —
lowering their version does not change the gate outcome. → [C6](./contracts/version-baseline.md).

## Acceptance summary

| Check | Contract | Success criterion |
|---|---|---|
| Version-less → `0.1.0` only | C3 | SC-001 |
| No published version change | C4 | SC-002 |
| Org-synced byte-identical | C5 | SC-003 |
| Build + test green | C6 | SC-004 |
| One documented source | C1 | SC-005 |
