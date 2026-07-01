# Data Model: Publish Governance packages to public nuget.org

This feature has no runtime data model (it is a CI/packaging change). The "entities" below are the
build/release-time artifacts and their invariants — the shapes the workflow and project metadata
must satisfy.

## Entity: Publishable Package

A NuGet artifact this repo produces and pushes.

| Field | Cli | ReferenceGateSet |
|---|---|---|
| **Package ID** (permanent, ADR-0003) | `FS.GG.Governance.Cli` | `FS.GG.Governance.ReferenceGateSet` |
| **Kind** | tool (`PackAsTool`, command `fsgg-governance`) | content-only (no assembly, no dependency group) |
| **Version source** | fsproj `<Version>` (`msbuild -getProperty:Version`) | schema-derived by `pack-reference-gate-set.fsx` (`{governance}.{capabilities}.{policy}.{tooling}`) |
| **Packed by** | `dotnet pack src/FS.GG.Governance.Cli/…` in `publish.yml` | `pack-reference-gate-set.fsx` |
| **Pre-push gate** | `cli-tests` + `enforcement-smoke` (green-by-omission) | G1–G7 reference-set guard (script refuses to pack when red) |
| **Feeds** | org GitHub Packages **and** nuget.org | org GitHub Packages **and** nuget.org |

**Invariants**

- **INV-1 (byte-identical)**: exactly one `.nupkg` is produced per package per release and pushed unchanged to both feeds — no second `dotnet pack`.
- **INV-2 (gated)**: a package reaches neither feed unless its pre-push gate is green.
- **INV-3 (versions unchanged)**: publishing does not alter either version rule; a version-bearing tag must equal the CLI fsproj `<Version>` or the run fails.
- **INV-4 (listing-complete)**: each published package carries `PackageLicenseExpression`, `PackageReadmeFile`, `RepositoryUrl`, and `PackageIcon` (see `contracts/package-listing-metadata.md`).

## Entity: Feed Target

A NuGet source the release pushes to.

| Field | Org GitHub Packages | Public nuget.org |
|---|---|---|
| **Source URL** | `https://nuget.pkg.github.com/FS-GG/index.json` | `https://api.nuget.org/v3/index.json` |
| **Auth** | run-scoped `${{ secrets.GITHUB_TOKEN }}` (`packages: write`) | Trusted Publishing OIDC — `NuGet/login@v1` short-lived key (`id-token: write`) |
| **Order** | first (authoritative) | second (additive public mirror) |
| **Idempotency** | `--skip-duplicate` | `--skip-duplicate` |
| **Fail-closed trigger** | token missing/insufficient | no matching trust policy → `401` |

**State transition (per package, per release)**

```
packed ──▶ pushed:org-feed ──▶ pushed:nuget.org ──▶ published
   │             │                      │
   │             │                      └─ 401 / non-duplicate error ─▶ run fails (org-feed push already durable; re-run is --skip-duplicate safe)
   │             └─ token error ─▶ run fails (nothing on either feed)
   └─ dry-run (push=false) ─▶ packed, NOT pushed  (FR-008)
   └─ gate red ─▶ never packed / never pushed  (INV-2)
```

## Entity: Trusted Publishing Policy (external, at nuget.org)

Not authored in this repo — provisioned by admin (FS-GG/.github#103). Recorded here as the
precondition the workflow depends on.

| Field | Value |
|---|---|
| Owner / profile | `Paradigma11` (optionally surfaced as non-sensitive secret `NUGET_USER`) |
| Repository Owner | `FS-GG` |
| Repository | `FS.GG.Governance` |
| Workflow file | `publish.yml` |
| Status | **Active** (#103) |

## Entity: Coherence Marker (external, in the registry)

| Field | Value |
|---|---|
| id | `nuget-org-published` |
| current | `coherent: false` |
| flips toward coherent when | all in-scope FS-GG packages (incl. these two) resolve on nuget.org at current versions (FR-011) |
| owner | cross-repo (FS-GG/.github registry) |
