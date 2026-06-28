# Research: Governance `.fsgg` Slot Rename

**Feature**: 084-governance-yml-rename | **Date**: 2026-06-28

This feature has no open NEEDS CLARIFICATION — the spec's Assumptions section already
resolves the design questions. Research here records the binding decisions and the
working-tree facts they rest on, so tasks.md and implementation can proceed without
re-deriving them.

## D1 — Clean break: no `project.yml` fallback read

**Decision**: The loader reads `governance.yml` outright. It does NOT fall back to
`project.yml` when `governance.yml` is absent. An absent `governance.yml` is reported as a
missing/absent primary slot.

**Rationale**: Under ADR-0005, `project.yml` is SDD-owned and coexists in the same `.fsgg/`.
A fallback read would re-introduce exactly the cross-product filename collision the rename
exists to remove, and would let Governance silently consume an SDD-owned file (violating
Principle VI's missing-vs-defect distinction). The half-done working tree already reflects
this: `Loader.fs` switches the slot string outright (`slot "project.yml"` →
`slot "governance.yml"`), it is not a dual-read.

**Alternatives considered**: (a) dual-read with `governance.yml` preferred and `project.yml`
fallback — rejected: re-creates the collision and the silent-consumption hazard. (b) a
migration warning when only `project.yml` is present — rejected as out of scope (the
ADR/registry migration story lives in FS-GG/.github#17); the absent-slot diagnostic already
tells the adopter the primary file is missing.

## D2 — Filename only; schema, contents, and `schemaVersion` unchanged

**Decision**: Only the on-disk filename/slot changes. The YAML schema, field set, parsing,
typed facts, gate set, routing facts, diagnostics, and `schemaVersion` are all unchanged.
The 36 fixture renames + golden + sample are **pure file moves** (`git diff --cached --stat`
confirms `36 files changed, 0 insertions, 0 deletions`).

**Rationale**: FR-003 requires byte-equivalent inputs to produce identical results under the
new name. The contents are byte-identical; only the file's name differs. No `schemaVersion`
bump is warranted (Assumptions: "No schema-version bump").

## D3 — Surface baselines and `.fsi`: comment-only edits, no re-bless

**Decision**: The `.fsi` edits (`Model.fsi`, `Schema.fsi`) are **doc-comment text only**
(`// ── governance.yml ──`; "comes from `governance.yml`"). No signature, type, or member
changes. Surface-area drift baselines capture **signatures, not comment text**, so no
baseline re-bless is forced.

**Action for Phase 1/implementation**: Confirm by building + running the surface-drift test
projects (e.g. `FS.GG.Governance.SurfaceChecks.Tests`, `*.Routing.Tests/SurfaceDriftTests.fs`)
that they stay green with no baseline edit. If any baseline unexpectedly captures the comment,
treat that as a Tier-1 additive re-bless and record it — but the expectation is zero baseline
change. `ProjectFacts` and all member names are retained (FR-009), so no surface symbol moves.

## D4 — Internal naming retained (`ProjectFacts`)

**Decision**: The in-memory type representing the primary slot's facts keeps the name
`ProjectFacts` (and related internal identifiers). Only the on-disk filename changes.

**Rationale**: Issue #13 and FR-009 explicitly retain internal naming; a broader internal
rename is out of scope and would inflate the diff and surface baselines for no behavioral
gain. The section comment above the type is updated (`// ── governance.yml ──`) purely for
reader orientation.

## D5 — Documentation scope is judgment-based, not a blanket sweep

**Decision**: The single binding doc target is `README.md:97` — the
`FS.GG.Governance.Config` four-`.fsgg`-files enumeration, which currently reads
`(project.yml, policy.yml, capabilities.yml, tooling.yml)` and must name `governance.yml`.
The co-located sample doc (`samples/sdd-reference-gate-set/README.md`, already edited) and
the fixtures README are Governance-owned and updated. Other `docs/` files
(`initial-design.md`, `initial-implementation-plan.md`, the 2026-06-18 capability-design
report, `governance-design/speckit-in-the-system.md`) describe either the **SDD-owned**
`.fsgg/project.yml` or historical design context and are **left unchanged** (FR-008).

**Rationale**: ADR-0005 splits ownership: `project.yml` is a real, current SDD slot, so
mentions describing SDD's identity file are correct as-is. Sweeping every `project.yml`
string would corrupt accurate SDD references. The spec fixes `README.md:97` as the one
required Governance enumeration and leaves the rest to per-mention judgment.

**Action for Phase 2/implementation**: For each remaining `docs/` hit, decide per-mention
whether the text describes *Governance's* primary slot (→ rename) or *SDD's* (→ leave). The
`specs/014-.../contracts/fsgg-schema.md` YAML authoring contract describes Governance's
config schema; evaluate it under the same rule and update only if it names Governance's
primary file. Default to leaving historical/design `docs/` prose untouched.

## D6 — `bin/`/`obj/` stale references are not source

**Decision**: The ~47 `src/**/bin/**/FS.GG.Governance.Config.xml` hits for `project.yml` are
**compiled XML-doc build artifacts**, not source. They regenerate from the edited `.fsi`
doc-comments on the next build and are not tracked targets. SC-001's "zero primary-slot
`project.yml` under tests/ and samples/" scan is satisfied (none remain on disk); the SC-005
doc scan targets source docs, not `bin/` artifacts.

**Rationale**: Editing build output is meaningless; a clean rebuild overwrites it. No action.

## D7 — New no-fallback regression test (SC-004)

**Decision**: Add one regression test (in `LoaderTests.fs`) that drives the loader with an
injected `FileReader` returning content for `"project.yml"` but `Ok None` for
`"governance.yml"`, and asserts the result is **Invalid / missing-required primary slot**
(NOT `Valid`), proving Governance does not fall back to the SDD file. This is the test that
fails before the slot switch (when the loader read `project.yml`) and passes after, and is
the concrete evidence behind SC-004 and Story-1 acceptance scenario 2.

**Rationale**: The existing edits switched the *existing* tests' reader keys to
`governance.yml`, but no test yet asserts the *negative* (project.yml present + governance.yml
absent ⇒ absent). SC-004 requires this be "demonstrated by tests." The injected-reader form
(no new on-disk fixture) is the minimal, deterministic way to express it and matches the
existing `erroringReader`/`absentReader` test style already in `LoaderTests.fs`.

## Working-tree starting state (facts)

- 36 fixture renames + 1 golden-fixture rename + 1 sample rename are **staged** as pure moves.
- `Loader.fs` slot string already switched to `governance.yml` (+ one comment).
- `Model.fs`/`Model.fsi`/`Schema.fsi` comment touches applied (unstaged).
- ~10 test-support/test files already switched to `governance.yml` (unstaged).
- **Not yet done**: `README.md:97` still says `project.yml`; the no-fallback test (D7) does
  not exist; the whole set is uncommitted (SC-006); build+test not yet verified green on the
  finished state.
