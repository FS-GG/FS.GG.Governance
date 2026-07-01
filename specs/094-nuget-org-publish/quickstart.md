# Quickstart / Validation: Publish Governance packages to public nuget.org

Runnable validation scenarios that prove the feature works end-to-end. See
[`contracts/publish-workflow-nuget-org.md`](./contracts/publish-workflow-nuget-org.md) and
[`contracts/package-listing-metadata.md`](./contracts/package-listing-metadata.md) for the details
these scenarios exercise.

## Prerequisites

- The Governance Trusted Publishing policy (Repository `FS.GG.Governance`, Workflow `publish.yml`)
  is **Active** at nuget.org — FS-GG/.github#103 (already done).
- Both packages carry the §5 listing metadata (see the metadata contract).
- Local pack/version checks only need the .NET 10 SDK.

## Scenario 1 — Version + pack are self-consistent (local, no push)

```sh
# CLI version source of truth
dotnet msbuild src/FS.GG.Governance.Cli/FS.GG.Governance.Cli.fsproj -getProperty:Version

# Reference-gate-set version + a real (gated) local pack into the local feed
dotnet fsi pack-reference-gate-set.fsx --print-version
dotnet fsi pack-reference-gate-set.fsx --output "$(mktemp -d)"   # runs G1–G7, then packs
```

**Expected**: the CLI version prints (e.g. `1.2.0`); the gate set prints its schema-derived version;
the pack run passes the G1–G7 guard and writes `FS.GG.Governance.ReferenceGateSet.<ver>.nupkg`.

## Scenario 2 — Listing metadata is present in the packed artifacts (local, no push)

```sh
outdir="$(mktemp -d)"
dotnet pack src/FS.GG.Governance.Cli/FS.GG.Governance.Cli.fsproj -c Release -o "$outdir"
dotnet fsi pack-reference-gate-set.fsx --no-gate --output "$outdir"
# Inspect each .nuspec inside the .nupkg for the required fields:
#   <license type="expression">MIT</license>, <readme>, <repository url=...>, <icon>
```

**Expected**: each `.nuspec` declares `license` (MIT expression), `readme`, `repository` URL, and
`icon`; the README and icon files are present at the package root. The gate set `.nupkg` still has
**no** `lib/` assembly and **no** dependency group (content-only invariant preserved).

## Scenario 3 — Dry run publishes nothing (CI)

Trigger `publish.yml` via `workflow_dispatch` with **no** `version` input.

**Expected**: both packages pack; the run logs a "packed but NOT pushing (dry run)" notice; **no**
push to either feed occurs (FR-008).

## Scenario 4 — Real release reaches both feeds (CI)

Cut a release (GitHub Release or `v<semver>` tag matching the CLI `<Version>`).

**Expected**: gates pass; each package pushes to the org GitHub Packages feed **first**, then to
nuget.org via `NuGet/login` + `dotnet nuget push`. Both packages resolve at the same version on both
feeds; the artifact bytes are identical across feeds (FR-001/FR-002/FR-003; SC-001/SC-002).

## Scenario 5 — Consumer installs from the public feed, no FS-GG credential (consumer)

```sh
# Clean environment with ONLY the default public nuget.org source configured
dotnet tool install --global FS.GG.Governance.Cli --version <released>
fsgg-governance --help                     # tool resolves and runs

# Reference gate set restores from the public feed into a consumer project
dotnet add package FS.GG.Governance.ReferenceGateSet --version <released>
```

**Expected**: both resolve from nuget.org with **no** GitHub Packages token / FS-GG membership; the
gate set delivers the four `.fsgg` files to the framework-agnostic consumer location (SC-001/SC-002).

## Scenario 6 — Fail-closed when the trust policy is absent (CI, negative)

With no matching Trusted Publishing policy (e.g. before #103 activation, or on a fork), run
`publish.yml` on a real release trigger.

**Expected**: `NuGet/login` returns `401 "No matching trust policy"`; the run **fails loudly** and
publishes nothing to nuget.org — never a silent skip (FR-006; SC-006). The org-feed push, if it ran
first, is durable and a re-run is `--skip-duplicate`-safe.

## Scenario 7 — Idempotent re-publish (CI)

Re-run a release for a version already on both feeds.

**Expected**: `--skip-duplicate` makes both pushes no-op successes; no duplicate, no mutated
artifact, no error (FR-007; SC-005).

## Coherence follow-up

Once both packages resolve on nuget.org at their current versions, advance the cross-repo registry
id `nuget-org-published` toward `coherent: true` and note completion on FS.GG.Governance#41 (FR-011).
