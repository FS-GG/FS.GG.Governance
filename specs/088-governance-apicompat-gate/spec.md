# Feature Specification: Breaking-Change (API-Compat) Gate for the Published Governance Packages

**Feature Branch**: `088-governance-apicompat-gate`

**Created**: 2026-06-28

**Status**: Draft

**Input**: User description: "next governance item on the project coordination board." → Coordination board item **FS-GG/.github#20** ("H3 · all — Adopt PublicApiAnalyzers + ApiCompat breaking-change gate … advisory→required"), Pillar 5 of the Homogeneous-build epic (FS-GG/.github#16), scoped to the FS.GG.Governance repository's published packages.

## Why This Feature

The Governance repository publishes ~70 `FS.GG.Governance.*` packages that downstream FS-GG repositories and generated products consume through **registry-pinned version ranges**. The auto-update fabric (epic Pillar 4) lets a consumer pick up a new Governance package version automatically *within its pinned range*. That fabric is only safe if a package's version number tells the truth: a release that **removes or changes** part of a package's public surface must be a **major** version bump so the registry's compatibility ranges stop it from flowing into consumers silently.

Today nothing enforces that link. Each package carries a committed public-surface snapshot (`surface/<Package>.surface.txt`) guarded by a drift test, which trips when the surface changes **in any way** — but it cannot tell an additive change (safe, minor/patch) from a breaking change (unsafe, major), and it is not tied to the version number or to the previously published package. A maintainer can therefore ship a breaking change under a non-major bump, and consumers within range will break at restore/build time with no warning at release.

This feature adds an automated **breaking-change gate**: every publishable Governance package is checked against its **last published version**, a detected break is surfaced to the maintainer, and the release is required to carry a major version bump (or revert the break). The gate lands **advisory first** (visible, non-blocking) and is then **ratcheted to required** (blocking) once the package surfaces are clean against it.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Maintainer is warned when a change breaks a published package surface (Priority: P1)

A Governance maintainer opens a pull request that removes a public function from a package (or changes its signature) without bumping the package to a new major version. The breaking-change gate compares the package's compiled public surface against the last published version of that package, recognizes the change as breaking, and reports it on the change — naming the package, the broken member, and the version bump the break requires.

**Why this priority**: This is the core value — turning an invisible, downstream-only failure into an immediate, local signal at the point the break is introduced. It delivers value even while the gate is still advisory, because the maintainer sees the problem before release.

**Independent Test**: Introduce a deliberate breaking change to one packable package on a branch, run the gate, and confirm it reports exactly that break (package + member + required-bump) while a purely additive change on another package reports clean.

**Acceptance Scenarios**:

1. **Given** a package whose last published version exposes a public member, **When** a change removes or alters that member without a major version bump, **Then** the gate reports a breaking-change finding identifying the package, the affected member, and that a major bump (or revert) is required.
2. **Given** a package, **When** a change only *adds* new public members and the version is bumped at least at minor level, **Then** the gate reports no breaking-change finding for that package.
3. **Given** a change that touches only non-published (internal/non-packable) projects, **When** the gate runs, **Then** it reports nothing for those projects.

---

### User Story 2 - Breaking changes are blocked from release unless the version is bumped accordingly (Priority: P2)

Once package surfaces are clean against the gate, the gate is promoted from advisory to **required**: a detected breaking change without a corresponding major version bump fails the release/CI path, so a mis-versioned breaking change cannot be published to the feed that the auto-update fabric draws from.

**Why this priority**: This is the enforcement that makes the version number trustworthy for the registry ranges and the auto-update fabric. It depends on US1 (detection) being in place and the existing surfaces being clean, so it follows as P2.

**Independent Test**: With the gate set to required, attempt to publish a package carrying a breaking change under a non-major bump and confirm the release path fails with the breaking-change finding; correct the version to a major bump (or revert the break) and confirm it passes.

**Acceptance Scenarios**:

1. **Given** the gate is required, **When** a package is released with a breaking change and a non-major version bump, **Then** the release path fails and the finding explains what to do (major bump or revert).
2. **Given** the gate is required, **When** the same breaking change is released with a major version bump, **Then** the release path passes.
3. **Given** the gate is required, **When** a release contains no breaking changes, **Then** the gate passes without maintainer action.

---

### User Story 3 - The intended public surface of each package is captured as a reviewable, committed baseline (Priority: P3)

Each publishable package's intended public surface is recorded as a committed baseline that lives in the repository and changes only through a reviewed diff, so that "what is public" is an explicit, auditable decision rather than an accident of which members happen to be visible, and so the gate has a stable reference for "intended" vs. "accidental" surface.

**Why this priority**: This strengthens the gate (it distinguishes a deliberate surface from drift) and improves review, but the gate's primary protection — comparing against the last published version — works without it, so it is P3. It must remain coherent with the constitution's rule that visibility is declared in signature files and with the existing surface-snapshot drift guard.

**Independent Test**: Change a package's public surface, observe that the committed baseline must be updated in the same change for the gate to pass, and that the baseline diff is human-reviewable.

**Acceptance Scenarios**:

1. **Given** a package with a committed surface baseline, **When** a public member is added or removed, **Then** the change does not pass until the baseline is updated to match, and the baseline update appears as a reviewable diff.
2. **Given** a committed baseline, **When** nothing about the public surface changes, **Then** no baseline update is required.

---

### Edge Cases

- **No previously published version**: a brand-new package (or first publish) has nothing to compare against — the gate treats it as having no breaking changes and establishes the baseline rather than failing.
- **Last published version unavailable** at gate time (feed unreachable / package yanked): the gate must fail safe — surface the inability to compare rather than silently passing as "no breaks."
- **Intentional breaking change**: a deliberate removal accompanied by a major version bump must pass cleanly; the gate gauges break-vs-bump, it does not forbid breaks.
- **Re-exported / transitive surface**: a member that becomes public only because an upstream package (e.g. `FS.GG.Contracts`) changed should be attributed clearly enough that the maintainer can tell a local break from an inherited one.
- **Relationship to the existing `surface.txt` drift guard**: both checks can fire on the same change; their messages must not contradict each other, and a maintainer must have a single clear path to make a legitimate change pass both.
- **Tooling that cannot analyze a given package** (e.g. a language/tooling limitation for some project kinds): the gate must clearly report a package as "not covered" rather than reporting a covered package as "clean," so coverage gaps are visible, not silent.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST detect, for each publishable `FS.GG.Governance.*` package, whether a change breaks that package's public surface relative to its **last published version**.
- **FR-002**: The system MUST classify a surface change as **breaking** (member removed or altered incompatibly) or **non-breaking** (purely additive), and MUST map a breaking change to the requirement of a **major** version bump.
- **FR-003**: When a breaking change is present without a corresponding major version bump, the system MUST produce a finding that identifies the package, the affected public member(s), and the required remediation (major bump or revert).
- **FR-004**: The gate MUST support an **advisory** mode (findings reported, change/release not blocked) and a **required** mode (findings block the change/release), and the mode MUST be controllable per the rollout (start advisory, ratchet to required).
- **FR-005**: In required mode, the system MUST fail the release/publish path for any package with a breaking change not accompanied by a major version bump, and MUST pass it when the bump is correct or the break is reverted.
- **FR-006**: The system MUST cover **all** packable Governance packages (`IsPackable` = true) and MUST NOT report findings for non-packable projects.
- **FR-007**: The system MUST make coverage explicit: any packable package the gate cannot analyze MUST be reported as **not covered** rather than implicitly treated as clean.
- **FR-008**: The system MUST fail safe when the last published version cannot be obtained for comparison (report the inability to compare; do not pass as "no breaks").
- **FR-009**: The system MUST treat a package with no previously published version as having no breaking changes and establish its baseline without failing.
- **FR-010**: The system MUST remain coherent with the existing per-package surface-snapshot drift guard (`surface/<Package>.surface.txt`): the two checks MUST NOT give contradictory verdicts on the same change, and the spec MUST define whether the new gate complements or supersedes the snapshot guard (see Assumptions).
- **FR-011**: The system MUST keep findings deterministic and reproducible: the same change against the same published baseline yields the same findings, with stable identification of package and member (consistent with the repository's deterministic-tooling principle).
- **FR-012**: The gate's behavior (advisory vs. required, what was checked, what was not covered) MUST be observable in the change/release output so a maintainer or CI reviewer can see the gate ran and what it concluded.
- **FR-013**: The breaking-change verdict MUST be tied to the **published package**, not merely to source visibility, so that the version number the registry ranges depend on is the thing being validated.

### Key Entities *(include if feature involves data)*

- **Publishable Governance package**: a `FS.GG.Governance.*` package marked publishable, identified by package id and version, with a public surface; the unit the gate evaluates.
- **Published baseline**: the last published version of a given package, used as the comparison reference for breaking-change detection.
- **Public-surface baseline (committed)**: the in-repo, reviewable record of a package's intended public surface (US3), kept coherent with the constitution's signature-file visibility rule and the existing surface snapshot.
- **Breaking-change finding**: a reported result naming the package, the affected member(s), the break classification, and the required version remediation.
- **Gate mode**: the advisory/required setting that governs whether findings block the change or release.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of publishable `FS.GG.Governance.*` packages are either covered by the gate or explicitly reported as not-covered — there are zero packages silently assumed clean.
- **SC-002**: A breaking change introduced to any covered package without a major version bump is detected and reported in every case (no false negatives in the project's break-detection test corpus).
- **SC-003**: A purely additive change to any covered package produces no breaking-change finding (no false positives in the project's additive-change test corpus).
- **SC-004**: With the gate in required mode, no package release that contains a breaking change under a non-major version bump can complete the publish path.
- **SC-005**: At rollout, the gate runs in advisory mode and reports the current break status of all packages without blocking; promotion to required happens only after the reported break count against existing surfaces is zero.
- **SC-006**: A maintainer who introduces a deliberate, correctly-versioned (major) breaking change can get the change through both this gate and the existing surface-drift guard via a single, documented remediation path.

## Assumptions

- **Target item**: This spec realizes the Governance-repo portion of Coordination board item FS-GG/.github#20 (Pillar 5 of epic FS-GG/.github#16). The "UI packages" portion of #20 belongs to the Rendering repo and is out of scope here.
- **Comparison reference**: "Last published version" means the most recent version of the package available on the shared package feed the auto-update fabric draws from. This feature assumes that feed is reachable in the gate's environment; unreachability is handled by FR-008 (fail safe).
- **SemVer enforcement is downstream**: This feature makes a breaking change *require* a major bump and surfaces/blocks when it doesn't; the registry's version ranges are what then actually stop a major from flowing to consumers (that range enforcement already exists / is owned by the registry, not this feature).
- **Relationship to the existing surface guard (FR-010 resolution)**: Default assumption — the new breaking-change gate **complements** the existing `surface.txt` drift guard rather than replacing it. The drift guard remains the "did the surface change at all" tripwire; the new gate adds break-vs-additive classification tied to the published version. Consolidating or retiring the snapshot guard is **out of scope** for this feature and can be revisited once the new gate is required.
- **Mechanism is a planning decision**: The specific tooling used to detect breaks and record the public-API baseline (package/assembly-level API-compat validation, a committed public-API list, or an extension of the existing surface snapshot) is left to `/speckit-plan`. The spec only requires the outcomes above. Note for planning: the issue text names "PublicApiAnalyzers + ApiCompat"; the repository's packages are F#, which constrains which of those tools apply to which packages — planning must confirm tool/language fit and use FR-007 (explicit not-covered) for any package a chosen tool cannot analyze.
- **No behavior change to the packages themselves**: This feature adds a gate and (optionally) committed baselines; it does not change the runtime behavior or public surface of any existing package.
- **Rollout**: Advisory-then-required is delivered as the US1 → US2 sequence; the ratchet to required is gated on SC-005 (zero breaks against existing surfaces).

## Dependencies

- **Coordination board**: FS-GG/.github#20 (this item), child of epic FS-GG/.github#16 (Pillar 5: homogeneous packaging).
- **Registry**: the cross-repo dependency registry's version ranges (`FS-GG/.github` → `registry/dependencies.yml`) are the downstream consumer of the SemVer guarantee this gate protects; this feature should keep that registry coherent if it changes how Governance package versions are validated.
- **Adjacent in-flight work**: 087 (re-type Config onto FS.GG.Contracts) and 085/086 (shared-build-config adoption, reference gate set package) — this gate layers on top of the shared build configuration adopted in 085.
- **Existing mechanism**: the per-package `surface/<Package>.surface.txt` snapshots and their drift tests (`tests/FS.GG.Governance.Snapshot.Tests`), which this gate must stay coherent with (FR-010).
