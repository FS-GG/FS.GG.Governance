# Feature Specification: Centralize an intentional VersionPrefix for the baseline-only projects

**Feature Branch**: `109-version-prefix-centralize`

**Created**: 2026-07-03

**Status**: Draft

**Input**: User description: "Centralize an intentional VersionPrefix for the baseline-only fsproj files in the repo-owned, drift-exempt Directory.Build.local.props (NOT the org-synced Directory.Build.props), deferred from #53 / epic #44 (2026-07-02 review M-ARCH low tail). … no packable tool's effective version may change unexpectedly or regress below what was already published … verify per-package before/after with `dotnet msbuild -getProperty:Version` … Org-synced build config must stay byte-identical. Closes #63."

## Overview

A maintainer building or packing the solution today gets an **accidental** version on the shipping tools. Nearly every internal project pins an explicit `<Version>` (66 projects at `0.1.0`, Kernel at `0.1.1`, Cli at `1.2.0`), but four **packable** artifacts carry no version element at all and therefore fall back to MSBuild's silent default of **`1.0.0`**:

| Packable artifact | Effective `Version` today | Where it comes from |
|---|---|---|
| `FS.GG.Governance.RouteCommand` (tool `fsgg`) | `1.0.0` | MSBuild default (no `<Version>`) |
| `FS.GG.Governance.EvidenceCommand` (tool `fsgg-evidence`) | `1.0.0` | MSBuild default |
| `FS.GG.Governance.CacheEligibilityCommand` (tool `fsgg-cache-eligibility`) | `1.0.0` | MSBuild default |
| `FS.GG.Governance.ReferenceGateSet` (content pkg) | `1.0.0` | MSBuild default |
| `FS.GG.Governance.Cli` (tool `fsgg-governance`) | `1.2.0` | explicit `<Version>` |
| ~66 internal libraries | `0.1.0` (Kernel `0.1.1`) | explicit `<Version>` |

The problem is not the *number* — it is that the number is **unowned**: a default nobody chose, sitting on artifacts that are actually published. This feature gives the version-less projects a single, **intentional** version source in the repo-owned, drift-exempt `Directory.Build.local.props`, so every project's version is deliberate and consistent, and future version movement is a reviewed edit rather than a silent MSBuild fallback.

This is a build-config hygiene change (2026-07-02 review M-ARCH low tail), deferred out of #53 precisely because verifying it demands a per-package before/after audit of effective versions worth reviewing on its own. It changes no code, no public API, and no JSON contract.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Every project's version is intentional, not an accidental default (Priority: P1)

A maintainer inspecting or packing the solution can point to a single, documented source for the version that the version-less baseline projects carry. No project's version is a silent MSBuild `1.0.0` fallback that nobody chose.

**Why this priority**: This is the whole point of the deferred #63 item — remove the "unnoticed default" so versions are consistent and deliberate. Without it, the shipping tools keep an unowned version.

**Independent Test**: Run `dotnet msbuild -getProperty:Version` on each project that lacks an explicit `<Version>`; assert every result equals the centralized value and that the value is declared in `Directory.Build.local.props`.

**Acceptance Scenarios**:

1. **Given** the centralized version source is in place, **When** the effective `Version` of any project that has no explicit `<Version>` element is queried, **Then** it resolves to the single centralized value rather than MSBuild's `1.0.0` default.
2. **Given** a project that already pins an explicit `<Version>` (e.g. Cli `1.2.0`, the `0.1.0` libraries, Kernel `0.1.1`), **When** its effective `Version` is queried, **Then** it is unchanged by this feature (explicit `<Version>` still wins over the centralized default).

---

### User Story 2 - No published tool's version regresses or moves unexpectedly (Priority: P1)

A maintainer who has already published tools to the org feed / nuget.org (`fsgg`, `fsgg-evidence` via specs 089/094; `ReferenceGateSet` via 086) is protected from a silent version *downgrade* when the centralized default lands — a naive centralized `0.1.0` would drop those artifacts from `1.0.0` to `0.1.0`, below what consumers have already resolved.

**Why this priority**: The hard constraint from #63's acceptance. A version regression on a published package is worse than the accidental default it replaces (breaks upgrade ordering / feed resolution). This must be verified per-package, not assumed.

**Independent Test**: Capture `dotnet msbuild -getProperty:Version` for every **packable** project before and after the change; diff the two maps and confirm no packable artifact's effective version decreased or changed unless that change is explicitly intended and recorded in this spec.

**Acceptance Scenarios**:

1. **Given** the before/after effective-version map for all packable projects, **When** the two are compared, **Then** no packable artifact's effective version is lower than its pre-change value.
2. **Given** any packable artifact whose effective version *does* change value, **When** the change is reviewed, **Then** the new value and its rationale are explicitly recorded (an intended re-pin), not an incidental side effect of the centralized default.

---

### User Story 3 - Org-synced build config is untouched (Priority: P1)

The centralized version lives only in the repo-owned, drift-exempt `Directory.Build.local.props`. The three org-synced files (`Directory.Build.props`, `Directory.Packages.props`, `.config/dotnet-tools.json`) stay byte-identical to the org baseline, so the shared-build-config drift gate stays green.

**Why this priority**: An edit to an org-synced file would be overwritten on the next sync and fail the drift check — the change would silently disappear and redden CI. The placement is a hard constraint, not a preference.

**Independent Test**: `git diff` the three org-synced paths against `main`; assert an empty diff.

**Acceptance Scenarios**:

1. **Given** the change is complete, **When** the three org-synced build-config files are diffed against their pre-change bytes, **Then** the diff is empty.
2. **Given** the centralized version source, **When** its file is identified, **Then** it is `Directory.Build.local.props` (drift-exempt), never an org-synced file.

---

### Edge Cases

- **Explicit `<Version>` vs centralized `<VersionPrefix>`**: an explicit `<Version>` element always wins over `<VersionPrefix>`. Projects that already pin a version keep it; only version-less projects inherit the centralized value. (This is the mechanism that makes User Story 1 & 2 co-satisfiable.)
- **Published tools currently at the `1.0.0` default**: a centralized value *below* `1.0.0` (e.g. `0.1.0`) would regress `fsgg` / `fsgg-evidence` / `ReferenceGateSet`. These artifacts must therefore keep an effective version ≥ their published value — either by choosing a centralized value that does not regress them, or by pinning them explicitly so the centralized default never lowers them. The chosen resolution is a plan-phase decision, constrained by User Story 2.
- **Test / non-packable projects with no `<Version>`**: these also inherit the centralized value. That is harmless (they never pack) and is the intended "everything is intentional" outcome, but the before/after audit should confirm no *packable* project is affected unexpectedly.
- **`VersionPrefix` + `VersionSuffix` interaction**: if any project sets a `VersionSuffix` (prerelease tag), effective `Version` becomes `prefix-suffix`. The audit compares full effective `Version`, so any such interaction is caught rather than assumed absent.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A single centralized version value MUST be declared once in the repo-owned, drift-exempt `Directory.Build.local.props`, such that every project lacking an explicit `<Version>` inherits it.
- **FR-002**: Projects that already declare an explicit `<Version>` MUST retain that exact version (the centralized default MUST NOT override an explicit pin).
- **FR-003**: No **published** artifact's consumable version MUST regress. The publish surface is exactly two packages: the CLI (`fsgg-governance`), whose published version is read from its explicit fsproj `<Version>` (1.2.0) by `publish.yml` and is therefore untouched by the centralized default; and `ReferenceGateSet`, whose published version is `schemaVersion`-derived and passed via `pack-reference-gate-set.fsx -p:Version=…`, also independent of the default. Both MUST remain byte-for-byte as published. (The three `PackAsTool` commands `fsgg`/`fsgg-evidence`/`fsgg-cache-eligibility` are **not** published — out of `publish.yml` scope — so their build-time version is not a consumable contract.)
- **FR-004**: Any packable artifact whose effective build `Version` changes MUST have that change be the *intended, recorded* outcome of the centralization, not an unreviewed side effect. Per the ratified decision, the three unpublished `PackAsTool` commands intentionally move from the accidental `1.0.0` default to the centralized `0.1.0` baseline (aligning them with the repo's 0.1.x line); this is recorded here and is not a regression of any published artifact.
- **FR-005**: The three org-synced build-config files (`Directory.Build.props`, `Directory.Packages.props`, `.config/dotnet-tools.json`) MUST remain byte-identical to their pre-change bytes.
- **FR-006**: The change MUST be verified by a per-package before/after comparison of `dotnet msbuild -getProperty:Version` across the whole project graph, and the evidence retained (real evidence, per Principle V).
- **FR-007**: The full solution build and test suite MUST stay green after the change (no code, API, or JSON-contract change is introduced).
- **FR-008**: The centralized version source and the rationale for its value (why it does not regress the published tools) MUST be documented at the declaration site so a future reader does not "fix" it into a regression.

### Key Entities

- **Centralized version source**: the single `Directory.Build.local.props` property (`<VersionPrefix>` / `<Version>`) that version-less projects inherit; carries an explanatory comment.
- **Effective-version map**: the before/after mapping of project → `dotnet msbuild -getProperty:Version` result, used as the acceptance evidence.
- **Published packable artifacts**: `fsgg` (RouteCommand), `fsgg-evidence` (EvidenceCommand), `ReferenceGateSet`, plus `fsgg-governance` (Cli) and `fsgg-cache-eligibility` (CacheEligibilityCommand) — the set whose versions must be protected from regression.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of projects that lack an explicit `<Version>` resolve their effective `Version` to the single centralized value (0 projects still resolving to an unowned MSBuild `1.0.0` default).
- **SC-002**: 0 **published** artifacts (CLI `fsgg-governance`; `ReferenceGateSet`) have a changed consumable version after the change, verified by the before/after effective-version map plus the publish-version derivation (fsproj `<Version>` for the CLI; `schemaVersion`-derived for ReferenceGateSet). The three unpublished `PackAsTool` commands move `1.0.0 → 0.1.0` by intent (recorded in FR-004), and no packable artifact ends **higher** than intended.
- **SC-003**: The three org-synced build-config files show a 0-byte diff against `main`.
- **SC-004**: The full `dotnet build` + `dotnet test` run is green after the change.
- **SC-005**: Exactly one file (`Directory.Build.local.props`) declares the centralized version, and it carries a comment explaining why the chosen value does not regress the published tools.

## Assumptions

- The centralized default is expressed via MSBuild's version-inheritance mechanism (an explicit `<Version>` element overrides it), so pinned projects are unaffected — matching MSBuild's documented `Version`/`VersionPrefix` precedence.
- The publish surface is exactly two packages (confirmed in Phase 0 from `publish.yml` + `pack-reference-gate-set.fsx`): the CLI `fsgg-governance` (version = its explicit fsproj `<Version>` 1.2.0) and `ReferenceGateSet` (version = `schemaVersion`-derived, injected at pack time). Neither derives its published version from the centralized default, so neither can regress. The `PackAsTool` commands `fsgg`/`fsgg-evidence`/`fsgg-cache-eligibility` are unpublished and carried only an accidental `1.0.0` MSBuild default; per the ratified decision they intentionally align to the centralized `0.1.0`.
- No code, `.fsi` surface, or JSON contract changes — this is a Tier 2 build-config hygiene change; existing surface-drift and semantic tests remain untouched and green.
- `Directory.Build.local.props` is the correct home (repo-owned, imported last, drift-exempt), consistent with how spec 085 relocated repo-specific properties there and how 088/094 placed repo-owned metadata there.
- The exact centralized value (and whether the four version-less packable tools are additionally pinned to preserve their published `1.x` line) is a plan-phase decision bounded by FR-002/FR-003/FR-004; the spec fixes the constraints, not the number.
