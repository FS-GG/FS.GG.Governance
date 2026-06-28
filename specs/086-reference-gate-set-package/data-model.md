# Phase 1 Data Model: Publish the Reference Gate Set as a Content Package

This feature is packaging infrastructure — the "entities" are build/distribution artifacts and the
relationships that keep the shipped artifact provably equal to the validated one. No runtime domain
types are added.

## Entity: Reference Gate Set bundle (the single source — UNCHANGED)

The four governance config files under `samples/sdd-reference-gate-set/.fsgg/`, validated and frozen by
the G1–G7 guard (079). This feature reads them; it does **not** modify their content or invariants.

| File | `schemaVersion` | Role in the bundle |
|------|-----------------|--------------------|
| `governance.yml` | 1 | Root manifest: project id, governed `domains`, `src` package surface, refs to policy/capabilities |
| `capabilities.yml` | 2 | `domains`, `pathMap`, `public-api` surface, the **three checks** (build/test/evidence) |
| `policy.yml` | 1 | Profiles + load-bearing `defaultProfile: light`, branch policy, review budget |
| `tooling.yml` | 1 | The three allow-listed commands, environment classes, `dotnet` external tool |

**Invariants (owned by G1–G7, not re-implemented here)**: loads `Valid` with 0 diagnostics; exactly 3
gates `build:build`/`test:test`/`evidence:evidence`; no dangling/orphan command refs; `defaultProfile:
light`; ratchet behavior (Advisory under Light @ Verify, ≥1 Blocking under Strict). The package guard
**depends on** these passing (FR-004) but does not duplicate them.

## Entity: `FS.GG.Governance.ReferenceGateSet` content package (NEW)

The distributable `.nupkg` wrapping the bundle.

| Field | Value / Rule | Source |
|-------|--------------|--------|
| `PackageId` | `FS.GG.Governance.ReferenceGateSet` | FR-001 |
| `Version` | derived (see Version-derivation rule) | FR-006 |
| Payload | `contentFiles/any/any/.fsgg/{governance,policy,capabilities,tooling}.yml` | FR-001/FR-005 |
| Payload bytes | **byte-identical** to `samples/sdd-reference-gate-set/.fsgg/*.yml` | FR-003/SC-002 |
| `lib/` | **absent** (`IncludeBuildOutput=false`) | FR-007/SC-005 |
| Dependency group | **empty** (`SuppressDependenciesWhenPacking=true`, no `PackageReference`) | FR-007/SC-005 |
| Build output dir | `~/.local/share/nuget-local/` | constitution (Pack output location) |

**Consumer materialization location (predictable, version-stable — FR-005/FR-009)**:
`<nuget-global-packages>/fs.gg.governance.referencegateset/<version>/contentFiles/any/any/.fsgg/`.
The four files appear there after restore; a `git diff --exit-code`-style overlay gate compares its
`fs-gg-governance/.fsgg/` against this folder.

## Entity: Version-derivation rule (NEW — the registered numbering contract)

A pure, deterministic function `schemaVersions → packageVersion`:

```
derive : (gov:int, caps:int, policy:int, tooling:int) -> string
derive (g, c, p, t) = sprintf "%d.%d.%d.%d" g c p t     // fixed file order: governance, capabilities, policy, tooling
```

| Property | Guarantee | Validated by |
|----------|-----------|--------------|
| Deterministic | same inputs → same string, no clock/env input | SC-003 |
| Distinguishable | any single `schemaVersion` bump changes exactly one segment | SC-003 (temp-dir bump test) |
| Legible | each file's schema generation is independently visible | D4 rationale |
| Current value | `1.2.1.1` | computed from the table above |

Implemented once in `pack-reference-gate-set.fsx`; exposed for test via `--print-version` (no second copy
of the rule). Recorded as an ADR in `FS-GG/.github` (FR-008).

**State transition (the only one in scope)**: a maintainer bumps a contained `schemaVersion`
→ next pack derives a new, distinguishable package version → consumers see a new pinned version. The
bundle content itself only changes through 079's guard (out of scope here).

## Entity: Registry contract entry (NEW — cross-repo, FS-GG/.github)

The `registry/dependencies.yml` record naming the package as a versioned surface.

| Field | Value |
|-------|-------|
| Contract name | `FS.GG.Governance.ReferenceGateSet` |
| Producer | `FS.GG.Governance` |
| Consumer(s) | `FS.GG.Templates` (Templates#14 overlay drift gate) |
| Kind | NuGet content package (config schema bundle) |
| Version rule | per the ADR (D4) |
| Feed status | local/CI pack now; org-feed push deferred (.github#21) |
| Projection | regenerated into `docs/registry/compatibility.md` |

## Relationships (the integrity chain)

```
samples/.../.fsgg/*.yml ──(single source, no copy)──▶ .fsproj packs contentFiles/any/any/.fsgg/
        │                                                          │
        │ loaded through real pipeline                             │ unzipped, compared
        ▼                                                          ▼
   G1–G7 guard  ◀──(pack aborts if red, FR-004)── pack-reference-gate-set.fsx ──▶ byte-identity guard (SC-002)
        ▲                                                          │
        └── schemaVersion of each file ──(derive)── package Version │ (SC-003 via --print-version)
                                                                    ▼
                                              registry/dependencies.yml (FS-GG/.github) ──▶ Templates#14 consumes
```

The chain enforces the core property (US2/SC-004): **the artifact consumers receive is the artifact the
G1–G7 tests validate** — same files, gated production, byte-verified, schema-versioned.
