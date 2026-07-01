# Contract: package listing metadata (ADR-0012 §5)

Both published packages MUST present a complete, trustworthy public listing. This contract fixes the
required fields and where each is authored.

## Required fields (per package)

| Field | Value | Where authored |
|---|---|---|
| `PackageLicenseExpression` | `MIT` (repo `LICENSE`, `Copyright (c) 2026 EHotwagner`) | `Directory.Build.local.props` (shared) |
| `PackageReadmeFile` | a packed README (`README.md`) | `.fsproj` (name) + a `<None Pack="true" PackagePath="\">` item pointing at the README to pack |
| `RepositoryUrl` (+ `RepositoryType=git`) | `https://github.com/FS-GG/FS.GG.Governance` | `Directory.Build.local.props` (shared) |
| `PackageProjectUrl` | `https://github.com/FS-GG/FS.GG.Governance` | `Directory.Build.local.props` (shared) |
| `PackageIcon` | a small PNG packed into the `.nupkg` | `Directory.Build.local.props` (`<PackageIcon>`) + a `<None Pack="true" PackagePath="\">` icon item |
| `Authors` | `FS-GG` (already on the gate set; add to the CLI) | `Directory.Build.local.props` (shared) |
| `Description` | package-specific (already present on the gate set; add to the CLI) | each `.fsproj` |
| `PackageTags` | package-specific (already present on the gate set; add to the CLI) | each `.fsproj` |

## Placement rules

- **Shared, identity-level fields** (license, repository/project URL, icon, authors) live in
  **`Directory.Build.local.props`** — repo-owned and **drift-exempt**. They only affect the two
  `IsPackable=true` projects; the ~70 non-packable projects ignore pack metadata.
- **Package-specific fields** (`Description`, `PackageTags`, the `PackageReadmeFile` name and the
  README/icon `Pack` items where the paths differ) live in each `.fsproj`.
- The org-synced `Directory.Build.props` / `Directory.Packages.props` / `.config/dotnet-tools.json`
  are **byte-identity drift-checked** (`gate.yml`) and MUST NOT be edited (spec 088 D6).

## Assets to add

- **README to pack**: reuse the root `README.md` or add a short packaging README. Whichever is
  chosen must be included with `Pack="true"` and `PackagePath` so it lands at the package root, and
  `PackageReadmeFile` must name it.
- **Icon**: no icon exists in the repo yet. Add one small PNG (e.g. `packaging/assets/icon.png`),
  include it with `Pack="true" PackagePath="\"`, and set `PackageIcon` to its packed name.

## Invariant

- **Content-only stays content-only**: adding README/icon to `FS.GG.Governance.ReferenceGateSet`
  MUST NOT reintroduce an assembly or a dependency group — `IncludeBuildOutput=false` /
  `SuppressDependenciesWhenPacking=true` remain, and the G1–G7 byte-identity guard MUST still pass
  (the `.fsgg/*.yml` content is unchanged; README/icon are additive package-root files).
