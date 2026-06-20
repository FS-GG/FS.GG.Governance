# Feature Specification: Git/CI Snapshot Facts for the Repository Boundary

**Feature Branch**: `016-git-ci-snapshot-facts`

**Created**: 2026-06-20

**Status**: Draft

**Input**: User description: "start the next item in the implementation plan" — the next Governance-owned, unchecked row of Phase 2 (*Governance Ship Walking Skeleton And Catalog MVP*) in `docs/initial-implementation-plan.md` is **"Add git/CI snapshot facts for base ref, head ref, changed paths, dirty paths, untracked paths, branch, PR labels, status checks, and CI context."** It is the sensing feature that *produces* the real changed-path set the path→capability routing of `015-path-capability-routing` (F015) only consumes.

## Overview

F015 answered "which capability domain does each path belong to?" for a set of repository-relative paths — but it deliberately took that path set as a caller-supplied input and **did not sense which files actually changed** (F015 FR-011/FR-016 held git/CI sensing out of scope as "a later Phase-2 row"). This feature is that later row: it senses, from a real git repository, a **typed repository snapshot** of the change boundary — base ref, head ref, the merge base between them, the paths that changed, the dirty and untracked paths in the working tree, the current branch, and any optional pull-request / status-check / CI context — and normalizes the changed-path set into the exact governed-path form that F015 routing consumes.

Together, F015 and this feature close the loop: this feature turns a real repository boundary into the candidate-path set, and F015 routes that set to capabilities. Everything downstream — unknown-governed-path findings, the gate registry, `route`, and `ship` — can then stand on a reproducible, sensed snapshot instead of caller-provided prose.

Sensing git is **impure**: it runs read-only `git` operations and reads runner-provided environment context. That impurity is isolated behind an injected port at the existing effects boundary (the same discipline the F08 Host interpreter uses), so the snapshot is produced against a **real fixture repository** in tests and reaches **no live network or hosting-provider API**. The snapshot value the sensing yields is, by contrast, **pure deterministic data**: identical repository state yields a byte-identical snapshot.

This feature stops at the typed snapshot (and the resolved diff range). It does **not** route paths to capabilities (that is F015), decide whether an unmatched in-root path is a blocking finding, assign surface classes, build the gate registry, enforce profiles or modes, or implement the `route` / `ship` commands or their JSON. Those are later Phase-2 and Phase-5 rows that consume this snapshot.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Sense the changed-path set of a change boundary (Priority: P1)

A maintainer (or CI) names a base ref and a head ref — for example "what changed on this branch relative to `main`?" The feature senses the set of repository paths that changed between them and returns them as a typed snapshot, with each path already normalized into the governed-path form, so the set can be handed straight to F015 routing as its candidate paths.

**Why this priority**: This is the missing half of routing. F015 can only route paths it is given; until a real changed-path set can be sensed from the repository, nothing in the capability/ship phase can route, scope, or gate an *actual* change. A sensed changed-path set is the MVP and is independently valuable even before working-tree state, range options, or CI context exist.

**Independent Test**: Build a small fixture git repository with a known base commit and head commit that differ by a known set of files; sense a snapshot for that base/head; assert the snapshot's changed-path set equals the expected set, normalized to the governed-path form, and that the set is accepted by F015 routing with no further normalization.

**Acceptance Scenarios**:

1. **Given** a fixture repository whose head differs from its base by `src/Kernel/Eval.fs` and `docs/intro.md`, **When** a snapshot is sensed for that base/head, **Then** the snapshot's changed-path set is exactly those two paths in normalized governed-path form, deterministically ordered.
2. **Given** a base and head that point at the identical commit, **When** a snapshot is sensed, **Then** the changed-path set is empty and the snapshot is a successful empty result, not a sensing failure.
3. **Given** the same fixture repository state sensed twice, **When** the two snapshots are compared, **Then** they are byte-for-byte identical, including the ordering of every path collection.

---

### User Story 2 - Sense working-tree state (dirty and untracked paths) (Priority: P1)

A local change is not only committed history: the working tree has tracked-but-modified ("dirty") files and brand-new untracked files. A maintainer running a local preview needs those reflected, so the route they see matches what is actually on disk — not only what is committed.

**Why this priority**: Local authoring is the cheap, common loop the design wants to keep fast and honest. If a snapshot ignored uncommitted work, a local preview would disagree with reality and with the eventual committed diff. Co-equal P1 with Story 1: a snapshot that only saw committed history would mislead every local route preview.

**Independent Test**: In a fixture repository, modify one tracked file and add one untracked file without committing; sense a snapshot; assert the modified file appears in the dirty set, the new file appears in the untracked set, each normalized to governed-path form, and neither appears in the committed changed-path set.

**Acceptance Scenarios**:

1. **Given** a tracked file modified but not committed, **When** a snapshot is sensed, **Then** that path appears in the dirty set and not in the untracked or committed-changed sets.
2. **Given** a new file that git is not yet tracking, **When** a snapshot is sensed, **Then** that path appears in the untracked set and in no other set.
3. **Given** a clean working tree, **When** a snapshot is sensed, **Then** the dirty and untracked sets are both empty and the snapshot is still a successful result.

---

### User Story 3 - Resolve a loose range into a concrete diff range (Priority: P2)

A caller names the range loosely — `--since <rev>`, or an explicit `--base <ref>` / `--head <ref>`. The feature resolves those options into one concrete, unambiguous diff range (a resolved base ref, head ref, and merge base) by a deterministic, documented contract, so a local preview and a CI run that pass the same options sense the same range and therefore the same changed paths.

**Why this priority**: Base/head parity between local previews and CI is a stated design goal (the route/ship gate must recompute, not trust local output). The resolution contract is what makes that parity possible. It builds on Story 1's sensing, so it is P2: required for a trustworthy machine contract, but only after a concrete base/head can be sensed at all. The `route` / `ship` commands that *carry* these options are a later row; this feature defines and tests the resolution contract itself.

**Independent Test**: Against a fixture repository, resolve each option form (`--since`, `--base`/`--head`, and the documented default when none is given) and assert each yields the expected concrete base ref, head ref, and merge base; assert the same options resolve identically whether invoked in a local-shaped or a CI-shaped fixture context.

**Acceptance Scenarios**:

1. **Given** `--base main --head feature`, **When** the range is resolved, **Then** the snapshot records `main` as the resolved base, `feature` as the resolved head, and their merge base, and the changed set is computed against the merge base so unrelated upstream commits on `main` are not reported.
2. **Given** `--since <rev>`, **When** the range is resolved, **Then** it resolves to a base of `<rev>` and a head of the current working position, deterministically.
3. **Given** the same option set resolved against a local-shaped context and against a CI-shaped context over the same commits, **When** the two resolutions are compared, **Then** the resolved base, head, and merge base are identical.

---

### User Story 4 - Capture branch and optional CI / PR context (Priority: P3)

Beyond paths, a snapshot carries the current branch name and — when a CI runner supplies it — optional pull-request labels, required status-check identities, and a CI environment classification. A maintainer or later gate can read this context without it ever being fabricated when it is unavailable.

**Why this priority**: This context shapes later profile/mode and branch-policy decisions, but none of those decisions are made in this feature, and the path facts (Stories 1–3) are useful without it. It is P3: a real but additive enrichment that must not block or distort the deterministic path snapshot.

**Independent Test**: Sense a snapshot with a CI-shaped context supplied (branch, labels, status-check ids, environment class) and assert each is captured faithfully; sense the same repository with no CI context supplied and assert those fields are explicitly absent (not empty-as-present, not fabricated), while the branch name — derivable from git alone — is still captured.

**Acceptance Scenarios**:

1. **Given** a runner that supplies PR labels and required status-check identities, **When** a snapshot is sensed, **Then** those labels and status-check identities are captured in deterministic order.
2. **Given** no CI context is supplied, **When** a snapshot is sensed, **Then** the PR/status/CI-environment fields are explicitly absent and the snapshot is still successful, with the branch name captured from git.
3. **Given** a CI environment classification is supplied, **When** a snapshot is sensed, **Then** it is recorded as a closed, documented classification rather than free-form environment text.

---

### User Story 5 - Fail safe and stay read-only (Priority: P2)

Sensing can fail: git may be missing, the directory may not be a repository, or a named ref may not exist. A maintainer needs every such failure surfaced as an explicit, stable diagnostic — never an unhandled crash and never a silently empty snapshot that looks like "nothing changed." And inspecting a repository must never change it.

**Why this priority**: A governance tool that mutated the repo it inspects, or that reported a sensing failure as an empty diff, would be untrustworthy at exactly the protected boundary it exists to protect. P2: it hardens the corners that otherwise turn a sensing error into a wrong gate decision.

**Independent Test**: Point sensing at a non-repository fixture directory and at a repository with an unknown ref; assert each yields a stable-id diagnostic naming the failed operation with a fix hint, that no exception escapes, and that an empty-but-successful snapshot is never produced for a failure; separately, sense a clean fixture repository and assert it is byte-identical (index, working tree, refs, config) before and after sensing.

**Acceptance Scenarios**:

1. **Given** a directory that is not a git repository, **When** a snapshot is sensed, **Then** a stable diagnostic identifies the not-a-repository condition with a fix hint, and no empty-looking snapshot is returned.
2. **Given** a base ref that does not exist, **When** the range is resolved, **Then** a stable diagnostic names the unknown ref, distinct from an empty-diff result.
3. **Given** a clean fixture repository, **When** a snapshot is sensed, **Then** the repository's index, working tree, refs, and configuration are unchanged afterward.

---

### Edge Cases

- **Governed root is a subdirectory**: When the declared governed root is a subdirectory of the repository, changed paths outside that subtree are still represented in the snapshot (so routing can classify them out-of-scope); this feature normalizes and reports them, it does not drop or decide them.
- **Renames and deletions**: A renamed file and a deleted file are real changes; the snapshot's changed-path semantics for renames (old vs new path) and deletions MUST be documented and applied consistently.
- **Empty diff vs sensing failure**: An empty changed-path set (base ≡ head) MUST be a distinct, successful outcome from a sensing failure (FR-011); the two are never conflated.
- **Detached HEAD / no current branch**: When there is no symbolic branch (detached HEAD), the branch name is explicitly absent rather than a fabricated value; sensing still succeeds.
- **Path outside the governed root in the working tree**: A dirty or untracked file outside the governed root is normalized and reported the same way as a changed committed path outside the root — represented, not dropped, for routing to classify.
- **Non-ASCII / quoted paths**: Git may quote or escape unusual path bytes; the snapshot MUST present each path in its normalized governed-path form, not git's raw quoted form.
- **Submodule / nested repository entries**: A nested repository or submodule boundary MUST be handled deterministically (either represented as a single boundary entry or excluded by a documented rule), never producing nondeterministic or partial output.
- **Repeated sensing in CI vs local**: The same commits sensed with the same resolved range in a CI-shaped and a local-shaped context MUST yield the same path facts (CI context may add optional fields but never changes the path sets).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The feature MUST sense, from a real git repository, a typed **repository snapshot** capturing a change boundary: a resolved base ref, a resolved head ref, the merge base between them, and the set of repository paths that changed between base and head. The snapshot MUST be a structured value (not prose) consumable directly by routing and later gate/ship features.
- **FR-002**: The changed-path set in the snapshot MUST be expressed in the **same normalized governed-path form** that F015 path→capability routing consumes (separators unified, resolved relative to the declared governed root), so the snapshot's changed paths can be fed straight into routing as its candidate-path set WITHOUT any re-normalization. Paths that fall outside the governed root MUST still be represented (so routing can classify them out-of-scope); this feature MUST NOT drop them or decide their disposition.
- **FR-003**: The snapshot MUST capture **working-tree state** beyond committed history: the set of tracked-but-modified ("dirty") paths and the set of untracked paths, each normalized exactly as in FR-002. A path MUST appear in at most one of the changed / dirty / untracked sets per the documented categorization, so a local preview reflects what is actually on disk.
- **FR-004**: The feature MUST resolve a caller's loose range options — `--since <rev>`, an explicit `--base <ref>`, and an explicit `--head <ref>`, plus a documented default when none is supplied — into one concrete, unambiguous diff range (resolved base ref, head ref, and merge base) by a **deterministic, documented resolution contract**, so identical options sense an identical range. The changed-path set MUST be computed against the merge base by default, so unrelated upstream commits on a stale base branch are not reported. This feature defines and tests the resolution contract; wiring it into the `route` / `ship` commands is a later row (FR-013).
- **FR-005**: The snapshot MUST capture the current **branch name** when one exists (explicitly absent under a detached HEAD), and MAY carry optional **CI / PR context** — pull-request labels, required status-check identities, and a closed CI environment classification — supplied by the runner's environment. Optional context MUST be modeled as clearly-optional fields that are **absent (never fabricated)** when unavailable.
- **FR-006**: All repository access MUST be **read-only**: sensing a snapshot MUST NOT modify the index, working tree, refs, stash, or configuration of the repository under inspection.
- **FR-007**: Every git and CI-context sensing operation MUST be isolated behind an **injected sensing port** at the existing impure effects boundary, so a snapshot is produced against a real fixture repository in tests and **no live network or hosting-provider API** is reached during sensing. (The pure snapshot value carries no such ports.)
- **FR-008**: A failed, missing, or unparsable sensing operation (git not installed, not a repository, an unknown ref, an unreadable working tree) MUST be reified as a **stable-id diagnostic** carrying the failed operation and a fix hint — never an unhandled exception and never a silently empty snapshot that resembles "nothing changed."
- **FR-009**: Every collection in the snapshot — changed paths, dirty paths, untracked paths, PR labels, status-check identities, and any diagnostics — MUST be emitted in a **deterministic, documented order**, so the same repository state and resolved range yield a byte-identical snapshot value.
- **FR-010**: Nondeterministic or environment-specific sensing output (raw command stdout/stderr, wall-clock timing, process ids, absolute host paths) MUST NOT be embedded in the deterministic snapshot facts. Where the provenance of a sensed git command must be retained, it MUST be recorded as a stable **command-run digest** kept separate from the deterministic path facts. (Full freshness-key / evidence-cache policy is a later phase, FR-013.)
- **FR-011**: The snapshot MUST distinguish a **genuinely empty changed-path set** (base and head resolve to the same content) from a **sensing failure** (FR-008); the two MUST never be conflated, in the type and in any rendered form.
- **FR-012**: The snapshot MUST present each path in its **normalized governed-path form**, not git's raw, quoted, or escaped output; renames and deletions MUST be represented by a documented, consistent rule so a downstream consumer never has to re-interpret git's wire format.
- **FR-013**: The feature MUST NOT itself route paths to capabilities, decide unknown-governed-path findings, assign surface classes, build the gate registry, enforce profiles or modes, compute evidence freshness, or emit route/audit JSON or any CLI command. It produces the typed snapshot and the resolved range that those later rows consume.
- **FR-014**: Sensing MUST require only a working git repository and the declared F014 governed root as inputs. It MUST NOT re-parse `.fsgg` YAML or re-validate the capability catalog, and it MUST NOT require any FS.GG package to be installed in the repository under inspection (governance inspects a project; a project never depends on governance).
- **FR-015**: The snapshot MUST be a structured value sufficient for a later route report, gate selector, and ship audit to consume directly — resolved range, changed/dirty/untracked path sets, branch, optional CI/PR context, command-run digests, and any sensing diagnostics — WITHOUT this feature emitting route/audit JSON or providing any CLI command.

### Key Entities *(include if feature involves data)*

- **Repository snapshot**: The typed result of sensing a change boundary — resolved range, changed paths, working-tree state, branch, optional CI/PR context, command-run digests, and diagnostics. A pure, deterministic value produced by impure sensing.
- **Diff range**: A resolved base ref, head ref, and merge base, derived deterministically from the caller's range options.
- **Git ref**: A reference (branch, tag, or commit-ish) named by the caller or resolved during range resolution.
- **Changed path**: A repository path that differs between base and head, presented in normalized governed-path form, with a documented rule for renames and deletions.
- **Working-tree state**: The dirty (tracked-but-modified) and untracked path sets sensed from the working tree, each in normalized governed-path form.
- **Snapshot options**: The caller's loose range inputs (`--since`, `--base`, `--head`, and the documented default) that the resolution contract turns into a concrete diff range.
- **CI / PR context**: Optional, never-fabricated metadata supplied by the runner — branch (also derivable from git), PR labels, required status-check identities, and a closed CI environment classification.
- **Sensing diagnostic**: A stable-id finding raised when a sensing operation fails (not-a-repository, unknown ref, git unavailable, unreadable tree), carrying the operation and a fix hint.
- **Command-run digest**: A stable, deterministic provenance record of a sensed git command, kept separate from the path facts so nondeterministic output never enters the deterministic snapshot.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a fixture repository with a known base and head, the snapshot's changed-path set equals the expected set in normalized governed-path form and is accepted by F015 routing with no further normalization.
- **SC-002**: Sensing the same repository state and resolved range twice produces a byte-for-byte identical snapshot, including the ordering of every path collection and diagnostic.
- **SC-003**: A fixture with committed diffs plus uncommitted dirty and untracked changes reports each path in exactly the correct category — committed-changed, dirty, or untracked — with no path appearing in the wrong set or in more than one set.
- **SC-004**: The same range options resolve to the same base ref, head ref, and merge base across a local-shaped and a CI-shaped fixture context over the same commits, and the resulting changed-path sets are identical.
- **SC-005**: A sensing failure (not-a-repository or unknown-ref fixture) produces a stable diagnostic with a fix hint, never an unhandled exception and never an empty-but-successful snapshot; a clean fixture repository is byte-identical (index, working tree, refs, configuration) before and after sensing.
- **SC-006**: No deterministic snapshot field contains raw command output, timestamps, process specifics, or absolute host paths; any retained command provenance is a digest kept out of the path facts.
- **SC-007**: No test reaches a live network or hosting-provider API — every sensing operation runs through the injected port over a real local fixture repository.

## Assumptions

- This feature is the sensing counterpart to F015: it *produces* the candidate-path set that F015 path→capability routing only *consumes*, closing the gap F015 left open ("git/CI sensing is a later feature").
- **No network in this feature**: PR labels, required status-check identities, and CI environment classification are read only from runner-provided environment context, never by querying a hosting provider's API. Live provider integration (e.g. querying check-run status over the network) is deferred to a later feature.
- Sensing is impure and lives at the existing effects boundary behind an injected port (the same discipline the F08 Host interpreter uses); the snapshot value it yields is pure, deterministic data with no ports embedded. Tests run against a real fixture git repository (real evidence; no live agent or network).
- The declared governed root comes from the F014 `ProjectFacts.GovernedRoot`; a single governed root is assumed and multi-root scoping is out of scope, consistent with F015.
- The default diff range used when no options are supplied is documented here as a resolution-contract default; the `route` / `ship` commands that ultimately *choose and carry* the range are a later row and own any policy beyond this contract.
- Command-run digests are recorded for provenance, but the full freshness-key and evidence-cache policy (rule hash, artifact hash, environment class, base/head) is a later phase (Phase 11) and is not implemented here.
- The supported sensing surface is the MVP set named by the plan row (base ref, head ref, merge base, changed/dirty/untracked paths, branch, optional PR labels / status checks / CI context). Richer git facts (per-hunk diffs, blame, history walks) are out of scope.
- The snapshot model and the impure sensing live in the product-neutral Governance layer (Config/Host), not the kernel; the kernel never sees git, refs, or process execution.
