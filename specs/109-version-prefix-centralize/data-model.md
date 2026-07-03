# Phase 1 Data Model: Centralize an intentional VersionPrefix

**Feature**: 109-version-prefix-centralize · **Date**: 2026-07-03

This feature has no runtime domain model — the "entities" are the build-graph facts the change acts
on and the evidence it produces.

## Entity: Centralized version source

- **What**: a single `<VersionPrefix>0.1.0</VersionPrefix>` property in `Directory.Build.local.props`.
- **Fields**: the value (`0.1.0`); an explanatory comment (why `0.1.0`; why it does not regress the
  two published artifacts; why it lives in `local.props`, not the org-synced props).
- **Rules**:
  - MUST be the only declaration of the centralized version (SC-005).
  - MUST NOT appear in any org-synced file (`Directory.Build.props`, `Directory.Packages.props`,
    `.config/dotnet-tools.json`) (FR-005).
  - Overridden by any project's explicit `<Version>` (MSBuild precedence) — this is what protects the
    pinned projects.

## Entity: Project version classification

Each `.fsproj` falls into exactly one class; the change only affects the last class.

| Class | Count | Version source | Effect of this change |
|---|---|---|---|
| Explicit-pinned library | ~66 | own `<Version>` (0.1.0; Kernel 0.1.1) | none (explicit wins) |
| Explicit-pinned CLI | 1 | own `<Version>` 1.2.0 | none (explicit wins) |
| Version-less **packable, unpublished** tool | 3 | inherited default | `1.0.0 → 0.1.0` (intended, D2) |
| Version-less **packable, published** (ReferenceGateSet) | 1 | `-p:Version` at pack time | build-time prop `1.0.0 → 0.1.0`; **packed version unchanged** (D3) |
| Version-less **non-packable** (tests/adapters/sample) | ~100 | inherited default | `1.0.0 → 0.1.0` (harmless; never packs) |

## Entity: Effective-version map (the acceptance evidence)

- **What**: `project → dotnet msbuild -getProperty:Version` for every project, captured **before** and
  **after** the change.
- **Derived assertions**:
  - Every explicit-pinned project: `after == before`.
  - Every version-less project: `after == 0.1.0` (was `1.0.0`).
  - CLI: `after == 1.2.0`; ReferenceGateSet *published* version (from the pack script) unchanged.
  - No published artifact's consumable version changed (FR-003 / SC-002).

## Entity: Org-synced build-config invariant

- **What**: the byte contents of `Directory.Build.props`, `Directory.Packages.props`,
  `.config/dotnet-tools.json`.
- **Rule**: `git diff` against `main` for these three paths MUST be empty (FR-005 / SC-003).

## State transitions

None. This is a declarative build-config edit; there is no stateful workflow, so no Elmish/MVU
boundary applies (Constitution IV — pure build-time property, no I/O workflow).
