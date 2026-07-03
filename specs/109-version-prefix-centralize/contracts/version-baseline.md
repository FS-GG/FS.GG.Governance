# Contract: Centralized version baseline

**Feature**: 109-version-prefix-centralize · **Date**: 2026-07-03

This feature exposes no public F# API and no JSON wire contract. Its "contract" is the build-graph
invariant the change establishes and the guarantees it preserves. This document is the authoritative
statement the verification (quickstart) asserts against.

## C1 — Single centralized source

There is **exactly one** declaration of the centralized version, and it is
`<VersionPrefix>0.1.0</VersionPrefix>` in `Directory.Build.local.props`.

- No org-synced file declares a version property.
- The declaration carries a comment stating: the value (`0.1.0`), why it does not regress the two
  published packages, and why it lives in the drift-exempt `local.props`.

## C2 — Explicit versions are authoritative and unchanged

Any project that declares its own `<Version>` keeps that exact value. Concretely, after the change:

| Project | Effective `Version` |
|---|---|
| `FS.GG.Governance.Cli` | `1.2.0` |
| `FS.GG.Governance.Kernel` | `0.1.1` |
| every other explicitly-pinned library | its own pinned value (`0.1.0`) |

## C3 — Version-less projects resolve to the intentional baseline

Every project with no explicit `<Version>` resolves to `0.1.0` (not the prior `1.0.0` default). This
includes the three unpublished `PackAsTool` commands (`fsgg`, `fsgg-evidence`,
`fsgg-cache-eligibility`), which move `1.0.0 → 0.1.0` by intent.

## C4 — Published consumable versions are invariant

The two published artifacts keep their exact published version, because neither derives it from the
centralized default:

| Published package | Version source | After change |
|---|---|---|
| `FS.GG.Governance.Cli` (`fsgg-governance`) | fsproj `<Version>` (read by `publish.yml`) | `1.2.0` (unchanged) |
| `FS.GG.Governance.ReferenceGateSet` | `schemaVersion`-derived, `-p:Version` at pack time | unchanged (default is inert) |

## C5 — Org-synced build config is byte-identical

`git diff main -- Directory.Build.props Directory.Packages.props .config/dotnet-tools.json` is empty.

## C6 — No behavioral / api / gate regression

- Full `dotnet build` + `dotnet test` is green.
- The api-baseline gate (`pack-and-apicheck.fsx`) is unaffected: the three lowered tools have no feed
  baseline (→ `NoBaseline`) before and after.
