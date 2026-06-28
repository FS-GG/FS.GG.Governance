# Feature Specification: Governance-side fs-gg-ui rename guard

**Feature Branch**: `083-fs-gg-ui-rename-guard`

**Created**: 2026-06-27

**Status**: Draft

**Input**: User description: "next governance item on the project coordination board."

**Board item**: P5 Versioning — *"[versioning] Rename fs-skia-ui-\* version machinery to
fs-gg-ui-\* (clean break)"* (per ADR-0003, FS-GG/.github), **governance-side slice only**. The
cross-repo item's Templates/SDD task is *"Verify no vendored/scaffold reference to `FsSkiaUiVersion`
or `fs-skia-ui/*` remains (file cross-repo asks if found)."* This feature delivers that verification
**for FS.GG.Governance** and makes it durable.

**Change classification**: Tier 2 (test + docs only). No public API surface, no `.fsi`, and no
surface-area baseline is added or changed. The repository governs itself with standard Spec Kit;
this is a narrow repo-owned check that pays for itself (Constitution → Development Workflow).

---

## Context: what is actually here

The legacy version machinery being renamed across the org (the CPM property `FsSkiaUiVersion`, the
registry contract ids `fs-skia-ui-version` / `fs-skia-ui-bom`, and the snapshot-tag namespace
`fs-skia-ui/v*`) lives in the **Rendering** template and the **.github** registry — **not** in
FS.GG.Governance. A full-tree scan of this repository confirms **zero** version-machinery
identifiers here: there is nothing to rename.

The only `fs-skia-ui` text in this repository is **historical-repository provenance prose** naming
the predecessor repo `EHotwagner/FS-Skia-UI` from which this governance design was extracted, in
four documentary files:

- `.specify/memory/constitution.md` (lineage note in the Sync Impact header)
- `docs/governance-design/index.md` (source-material links to the predecessor repo)
- `docs/initial-design.md` (source-analysis note)
- `docs/reports/2026-06-18-233718-fsgg-governance-capability-design.md` (source-analysis note)

These are **correct lineage references to a real historical repository** and are explicitly **out
of scope for the rename** — renaming them would falsify provenance and may break links. The version
machinery (a CPM property / contract id / git tag namespace) and a historical repository's name are
different things that happen to share the `fs-skia-ui` string; this feature keeps them distinct.

Because there is nothing to rename here, the governance-side deliverable is **verification made
durable**: a narrow regression guard that (a) proves the absence of legacy version-machinery
identifiers today, (b) keeps them out tomorrow, and (c) does not flag the legitimate historical
provenance.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Close the rename's governance checkbox with evidence (Priority: P1)

The contributor coordinating the cross-repo `fs-skia-ui-* → fs-gg-ui-*` rename needs to confirm that
FS.GG.Governance carries no straggling reference to the legacy version machinery, so the rename can
land as a clean break without a hidden survivor in this repo.

**Why this priority**: This is the literal governance-side task on the board item. Without a
verifiable answer, the rename coordinator either trusts an ad-hoc one-time grep (which rots) or
leaves the checkbox open. A committed, runnable guard turns "I grepped once" into durable evidence.

**Independent Test**: Run the repository's test suite (or the single guard) on a clean checkout of
`main`; it passes, demonstrating zero legacy version-machinery identifiers in the tracked tree. The
result can be cited on the cross-repo issue to close the governance-side checkbox.

**Acceptance Scenarios**:

1. **Given** the current `main` tree, **When** the guard runs, **Then** it reports zero legacy
   version-machinery identifiers (`FsSkiaUiVersion`, `fs-skia-ui-version`, `fs-skia-ui-bom`, and the
   `fs-skia-ui/v*` snapshot-tag namespace) and passes.
2. **Given** the guard has passed, **When** the rename coordinator reviews FS.GG.Governance, **Then**
   the passing guard is sufficient evidence that this repo needs no rename change.

---

### User Story 2 - Catch a future legacy identifier before it merges (Priority: P2)

A contributor later introduces a reference to the UI version machinery — for example, a sample
`.fsgg` capability, a `Directory.Packages.props`, or a template fragment that pins a UI package — and
accidentally uses the **legacy** `fs-skia-ui` root. The guard must fail and point them at the
canonical `fs-gg-ui` replacement.

**Why this priority**: The whole point of a guard over a one-time grep is the future. The staleness
bug class P5 targets is structural; the governance-side contribution is making the legacy root
un-mergeable here. Without this, the rename can silently regress.

**Independent Test**: Add a fixture/file containing `FsSkiaUiVersion` (or `fs-skia-ui-version`); the
guard turns red and names the offending file plus the `fs-gg-ui` replacement. Remove it; the guard
returns green.

**Acceptance Scenarios**:

1. **Given** a tracked file that introduces `FsSkiaUiVersion`, **When** the guard runs, **Then** it
   fails with a diagnostic naming the file, the offending identifier, and the canonical
   `FsGgUiVersion` replacement.
2. **Given** a tracked file that introduces a `fs-skia-ui-version` / `fs-skia-ui-bom` contract id or a
   `fs-skia-ui/v*` tag reference, **When** the guard runs, **Then** it fails the same way, pointing at
   the `fs-gg-ui-*` root.
3. **Given** the same content rewritten with the canonical `fs-gg-ui` root, **When** the guard runs,
   **Then** it passes.

---

### User Story 3 - Preserve historical provenance untouched (Priority: P3)

A reader of the governance design follows the lineage to the predecessor `FS-Skia-UI` repository. The
rename guard must not force those references to change and must not falsely flag them as a rename
straggler.

**Why this priority**: Correctness of the guard's scope. Conflating a historical repo name with the
version machinery would either falsify provenance (if "fixed") or produce a permanently red guard (if
flagged) — both unacceptable. This story freezes the boundary.

**Independent Test**: With the four provenance files unchanged, the guard passes; their content is
byte-identical before and after the feature.

**Acceptance Scenarios**:

1. **Given** the four historical-provenance files unchanged, **When** the guard runs, **Then** it
   passes (the provenance is allowlisted, not flagged).
2. **Given** the feature is delivered, **When** the four provenance files are diffed against `main`,
   **Then** the diff is empty (no lineage text rewritten).

---

### Edge Cases

- **Case / separator variants**: an identifier written `FsSkiaUi*`, `fs_skia_ui*`, or `fs.skia.ui*`
  in a pinning/version context should still be caught; the historical repo name `FS-Skia-UI` in prose
  must not be.
- **Canonical new name present**: a file legitimately using `FsGgUiVersion` / `fs-gg-ui-version` /
  `fs-gg-ui-bom` must pass — the guard forbids the legacy root, it does not forbid UI version
  references per se.
- **Provenance allowlist drift**: if a historical-provenance reference moves to a new file, the guard
  must be updatable to keep the lineage allowlisted without weakening the legacy-identifier ban.
- **Guard's own fixtures**: any negative-test fixture the guard uses to prove it can go red must not
  itself trip the production scan (the guard must not flag its own test scaffolding as a real
  straggler).
- **Binary / generated artifacts**: `bin/`, `obj/`, and untracked files are not part of the scanned
  surface; the guard scans the git-tracked tree only.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The repository MUST contain zero legacy version-machinery identifiers in its
  git-tracked tree: the CPM property `FsSkiaUiVersion`, the registry contract ids `fs-skia-ui-version`
  and `fs-skia-ui-bom`, and any reference to the snapshot-tag namespace `fs-skia-ui/v*`. (True today;
  this feature freezes it.)
- **FR-002**: An automated regression guard MUST fail when any legacy version-machinery identifier
  from FR-001 (including reasonable case/separator variants used in a version-pinning context)
  appears in the tracked tree.
- **FR-003**: The guard MUST allowlist the legitimate historical-repository provenance references to
  the predecessor `FS-Skia-UI` repo (the four documentary files named in Context), so genuine lineage
  prose neither needs to change nor trips the guard.
- **FR-004**: The canonical replacement identifiers MUST be the `fs-gg-ui` root — `FsGgUiVersion`,
  `fs-gg-ui-version`, `fs-gg-ui-bom`, and the `fs-gg-ui/v*` snapshot-tag namespace — and the guard's
  failure diagnostic MUST name the canonical replacement for the offending legacy identifier.
- **FR-005**: The guard's failure diagnostic MUST be actionable: it MUST name the offending file and
  the specific legacy identifier found (Constitution Principle VI — distinguish a real straggler from
  noise).
- **FR-006**: The feature MUST NOT modify the content of the historical-provenance references; their
  lineage text stays byte-identical.
- **FR-007**: The feature MUST NOT add, remove, or change any public API surface, `.fsi` signature, or
  surface-area baseline (Tier 2). No production `src/` code is changed.
- **FR-008**: The guard MUST scan only the git-tracked repository tree, excluding build outputs
  (`bin/`, `obj/`) and untracked files, so it is deterministic on a clean checkout.
- **FR-009**: The guard MUST be self-contained: it MUST NOT depend on this repository's own (or any
  external) governance platform to run — it runs under the standard repo test command, consistent
  with the repo's existing regression-guard convention.

### Key Entities

- **Legacy version-machinery identifier set**: the forbidden tokens being renamed away —
  `FsSkiaUiVersion` (CPM property), `fs-skia-ui-version` / `fs-skia-ui-bom` (registry contract ids),
  `fs-skia-ui/v*` (snapshot-tag namespace), plus their case/separator variants in a version-pinning
  context.
- **Canonical fs-gg-ui identifier set**: the allowed replacements — `FsGgUiVersion`,
  `fs-gg-ui-version`, `fs-gg-ui-bom`, `fs-gg-ui/v*` — surfaced in the guard's failure message.
- **Historical-provenance allowlist**: the four documentary files that legitimately reference the
  predecessor `FS-Skia-UI` *repository*; explicitly distinct from the version machinery.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A single run of the repository test command confirms zero legacy version-machinery
  identifiers in the tracked tree and passes on the current `main`.
- **SC-002**: Introducing any legacy version-machinery identifier (e.g. adding `FsSkiaUiVersion` to a
  sample or props file) turns the guard red in one test run; removing it returns the guard green.
- **SC-003**: The four historical-provenance files are byte-identical before and after the feature
  (empty diff), and the guard passes with them present.
- **SC-004**: The change touches no `.fsi` and no surface-area baseline — verifiable by an empty diff
  over those files (Tier 2 honored).
- **SC-005**: The cross-repo rename coordinator can cite the passing guard to close the
  governance-side checkbox of the P5 rename item, with no remaining straggler in this repository.
- **SC-006**: A reviewer can distinguish a true legacy straggler from legitimate provenance by reading
  the guard's diagnostic and its allowlist alone, without external context.

## Assumptions

- **The version machinery is owned elsewhere.** `FsSkiaUiVersion` / `fs-skia-ui-version` /
  `fs-skia-ui-bom` / `fs-skia-ui/v*` live in the Rendering template and the `.github` registry, not
  in FS.GG.Governance, which is one external customer of the UI packages and currently pins none of
  them. The governance-side rename work is therefore verification, not code change.
- **Historical provenance is preserved, not renamed.** `EHotwagner/FS-Skia-UI` is a real predecessor
  repository; its name in lineage prose is correct and must stay. Whether those links are still live
  (the repo may since have moved/renamed) is a separate documentary-accuracy concern, out of scope
  here.
- **Existing test convention is reused.** The guard follows the repository's established
  regression-guard style (the Expecto + YoloDev pattern used by the other 80+ test projects, e.g. the
  `FS.GG.Governance.ReferenceGateSet.Tests` guard from feature 079). The exact home — a small new
  guard test project versus extending an existing repo-hygiene test — is a plan-phase decision; this
  spec does not fix it.
- **No enforcement-profile semantics apply.** This is a build-time test guard, not a routed
  `.fsgg` gate; `block-on-ship` / `warn` profiles and the route/ship/verify pipeline are not involved.
- **The guard scans git-tracked files.** Determinism comes from scanning the tracked tree on a clean
  checkout; build outputs and untracked scratch files are excluded.

## Dependencies

- **Coordinated with, but not blocked by, the cross-repo rename (ADR-0003).** Rendering renames the
  CPM property and re-tags snapshots; `.github` renames the registry contract ids. This repository's
  slice has no legacy identifier to migrate, so it can land before or after the other repos without
  ordering risk. If the rename coordinator later finds a governance reference that should pin a UI
  package, that is a follow-up that MUST use the `fs-gg-ui` root (FR-004) — and the guard will enforce
  it.
