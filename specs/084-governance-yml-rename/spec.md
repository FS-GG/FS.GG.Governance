# Feature Specification: Governance `.fsgg` Slot Rename (`project.yml` → `governance.yml`)

**Feature Branch**: `084-governance-yml-rename`

**Created**: 2026-06-28

**Status**: Draft

**Input**: User description: "next governance item on the project coordination board." → resolved to Coordination board item **FS-GG/FS.GG.Governance#13** (`H0 · governance — Finish + commit project.yml→governance.yml rename (loader+fixtures+sample+docs), build+test green`), Phase `P3 Governance`/horizon `H0`, status **Ready**, labels `contract-change` + `roadmap`, part of epic FS-GG/.github#16.

## Context & Motivation *(informative)*

The Governance configuration loader reads a four-file set from a project's `.fsgg/` directory. The primary slot has historically been named `project.yml`. Under the org-level slot-ownership decision (ADR-0005, tracked by FS-GG/.github#17), the **SDD** product owns `.fsgg/project.yml` and **Governance** owns `.fsgg/governance.yml`, and the two coexist in one shared `.fsgg/` directory. Governance must therefore stop reading the SDD-owned `project.yml` and read its own `governance.yml` slot instead, so the two products never collide on a shared filename.

A background rename is **half-done in the working tree**: 36 fixture/sample file moves are staged, and the loader/model/test-support/doc edits are partially applied but uncommitted. This feature finishes that rename coherently, gets the full build and test suite green, and commits it.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Governance loads configuration from the `governance.yml` slot (Priority: P1)

An adopter (or the SDD↔Governance handoff) has a `.fsgg/` directory containing a `governance.yml` primary file alongside the optional `policy.yml`, `capabilities.yml`, and `tooling.yml`. When any Governance command resolves configuration for that directory, the loader reads the primary slot from `governance.yml` and produces the same typed facts it previously produced from `project.yml`, with no behavioral change beyond the filename.

**Why this priority**: This is the load-bearing change. Without it, Governance either reads the wrong (SDD-owned) file or fails to find configuration. Everything else in the feature exists to support this one behavior.

**Independent Test**: Point the config loader at a `.fsgg/` directory whose primary file is named `governance.yml` and assert it loads `Valid` with the identical typed facts, gate set, and diagnostics that the equivalent `project.yml` fixture produced before the rename.

**Acceptance Scenarios**:

1. **Given** a `.fsgg/` directory containing a well-formed `governance.yml`, **When** Governance loads and validates it, **Then** the result is `Valid` with the same domains/checks/profiles/routing facts that the byte-equivalent `project.yml` produced before the rename.
2. **Given** a `.fsgg/` directory whose primary file is still named `project.yml` (the SDD-owned slot) and that has **no** `governance.yml`, **When** Governance loads it, **Then** the primary slot is treated as absent/missing (Governance does not read the SDD-owned file).
3. **Given** a malformed `governance.yml` (each existing malformed fixture), **When** Governance loads it, **Then** it produces the same diagnostic outcome the corresponding `project.yml` malformed fixture produced before the rename.

---

### User Story 2 - All fixtures, samples, and test support reference the new slot (Priority: P1)

Every on-disk `.fsgg/project.yml` artifact under the Governance repository's test fixtures and curated samples is renamed to `governance.yml`, and every test-support helper that constructs or names the primary slot uses `governance.yml`, so the full automated suite exercises the real, renamed slot rather than a stale name.

**Why this priority**: The loader change (Story 1) and the fixtures must move together. A renamed loader against unrenamed fixtures — or vice versa — leaves the suite red. They form one atomic, independently-incoherent-if-split change.

**Independent Test**: Run the full `dotnet test` suite; it compiles and passes with zero `project.yml` primary-slot fixtures remaining under `tests/` and `samples/`.

**Acceptance Scenarios**:

1. **Given** the renamed repository, **When** the config test project runs, **Then** all loader/schema/surface tests pass against `governance.yml` fixtures with no test referencing a primary `project.yml` slot.
2. **Given** the curated reference gate set under `samples/sdd-reference-gate-set/.fsgg/`, **When** its regression guard runs, **Then** it loads `Valid` from `governance.yml` with the same invariants (gate count, default profile, no dangling refs) it held under `project.yml`.
3. **Given** every test-support helper that writes or names the primary slot, **When** the suite builds, **Then** each helper emits/locates `governance.yml`.

---

### User Story 3 - Adopter-facing and design docs reflect the Governance slot name (Priority: P2)

A reader of the repository's adopter documentation sees the Governance primary slot named `governance.yml` wherever the docs describe the **Governance** four-file set, while documentation that legitimately describes the **SDD-owned** `project.yml` is left unchanged.

**Why this priority**: Documentation correctness matters for adopters and unblocks the downstream ownership split, but it does not gate the build/test suite. It is a fast-follow on the load-bearing rename, and it requires judgment (which `project.yml` mentions are Governance's vs. SDD's) rather than a mechanical sweep.

**Independent Test**: Inspect the updated docs; the Governance four-file enumeration (the `README.md` F14 four-file paragraph, line ~97) names `governance.yml`, and SDD-context `project.yml` references remain intact.

**Acceptance Scenarios**:

1. **Given** the repository `README.md` four-`.fsgg`-files line, **When** the rename is complete, **Then** it lists `governance.yml` as the Governance primary file (not `project.yml`).
2. **Given** docs that describe SDD's project identity file, **When** the rename is complete, **Then** those `project.yml` references are unchanged (they describe an SDD-owned slot, not Governance's).

---

### Edge Cases

- **Both files present**: a `.fsgg/` directory containing both `project.yml` (SDD's) and `governance.yml` (Governance's) — Governance reads only `governance.yml` and ignores `project.yml`. This is the intended steady state under ADR-0005.
- **Neither primary file present**: a `.fsgg/` directory with only optional files (e.g. `policy.yml`) and no `governance.yml` — the primary slot is treated as absent, identical to a missing-primary case before the rename.
- **Stale `project.yml` only**: a pre-rename adopter who has only `project.yml` and never created `governance.yml` — Governance now reports the primary slot as missing rather than silently consuming the SDD file. (See Assumptions: clean break, no fallback read.)
- **Internal type names**: the in-memory type representing the primary slot's facts (`ProjectFacts`) keeps its name; only the on-disk filename/slot changes. No public type rename is in scope.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The configuration loader MUST read the primary `.fsgg` slot from a file named `governance.yml`.
- **FR-002**: The loader MUST NOT read the SDD-owned `project.yml` as the Governance primary slot; an absent `governance.yml` MUST be reported as a missing/absent primary slot, not satisfied by a present `project.yml`.
- **FR-003**: For any input that is byte-equivalent except for the primary filename, the loader MUST produce identical validation results (typed facts, gate set, routing facts, and diagnostics) under `governance.yml` as it previously produced under `project.yml`.
- **FR-004**: Every on-disk primary-slot fixture under `tests/` (all 34 config fixtures under `tests/FS.GG.Governance.Config.Tests/fixtures/`, plus `tests/golden-fixture/.fsgg/` = 35 under `tests/`) MUST be renamed from `project.yml` to `governance.yml`.
- **FR-005**: The curated sample at `samples/sdd-reference-gate-set/.fsgg/` MUST use `governance.yml` as its primary slot, and its co-located docs MUST reference the new name.
- **FR-006**: Every test-support helper that constructs, writes, or names the primary `.fsgg` slot MUST use `governance.yml`.
- **FR-007**: The repository `README.md` Governance four-file enumeration (the F14 four-file paragraph, line ~97) and any docs describing the **Governance** primary slot MUST name `governance.yml`.
- **FR-008**: Documentation that describes the **SDD-owned** `project.yml` MUST remain unchanged (the rename is scoped to Governance's slot, not SDD's).
- **FR-009**: The internal type and member names for the primary slot's facts (e.g. `ProjectFacts`) MAY remain unchanged; only the on-disk filename/slot is renamed. No public-surface (`.fsi`) rename is required beyond what the slot-name change forces (e.g. doc-comment text).
- **FR-010**: The complete change MUST be committed coherently (file moves + source + test + doc edits together) so the repository is never left in a half-renamed state on the feature branch.

### Key Entities *(include if feature involves data)*

- **`.fsgg/` directory**: a project's governance configuration directory, shared between SDD and Governance. Contains a Governance primary file (`governance.yml`, after this feature), an SDD primary file (`project.yml`, owned by SDD), and Governance's optional `policy.yml` / `capabilities.yml` / `tooling.yml`.
- **Governance primary slot**: the single required Governance configuration file, renamed from `project.yml` to `governance.yml`. Carries project/governance identity, domains, default work root, package surfaces, and pointers to the optional catalogs.
- **`ProjectFacts` (internal)**: the in-memory typed representation produced from the primary slot. Name retained; only its source filename changes.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Zero primary-slot `project.yml` files remain under `tests/` and `samples/`; a repository scan finding none is the binding check. (The renamed set is 34 config fixtures + 1 golden-fixture + 1 sample = 36 `governance.yml` primary-slot files; the empty scan, not a hard count, is what SC-001 asserts.)
- **SC-002**: `dotnet build` of the full solution succeeds with no errors.
- **SC-003**: The full `dotnet test` suite passes (every previously-green test project remains green); no test regresses due to the rename.
- **SC-004**: The Governance config loader loads `Valid` from a `governance.yml`-named `.fsgg/` directory and reports the primary slot **absent** when only an SDD `project.yml` is present (no fallback), demonstrated by tests.
- **SC-005**: The repository `README.md` and Governance-describing docs name `governance.yml`; no Governance four-file enumeration still says `project.yml`.
- **SC-006**: The change lands as a coherent commit on the feature branch with a green build+test, leaving no uncommitted half-rename in the working tree.

## Assumptions

- **Clean break, no fallback read**: Governance reads only `governance.yml` and does NOT fall back to reading `project.yml`. Per ADR-0005, `project.yml` is SDD-owned and coexists in the same `.fsgg/`; a fallback would re-introduce the collision the rename exists to remove. (The half-done working tree already reflects this: the loader slot string is switched outright, not dual-read.)
- **No schema-version bump**: only the on-disk filename changes; the schema/contents and their version are unchanged, so no `schemaVersion` increment is required.
- **Internal naming retained**: `ProjectFacts` and related internal identifiers keep their names (per issue #13); a broader internal rename is explicitly out of scope.
- **Docs scope is judgment-based, not a blanket sweep**: only documentation describing the **Governance** primary slot is updated. Several `docs/` files (e.g. `initial-design.md`, `initial-implementation-plan.md`, capability-design reports) describe the **SDD-owned** `.fsgg/project.yml` and are intentionally left unchanged. The binding doc target is `README.md:97`; other doc edits are made only where the text describes Governance's own slot.
- **ADR/registry update is a separate item**: the ADR-0005 authoring and `registry/dependencies.yml` + compatibility-doc update live in FS-GG/.github#17 (a `.github`-repo item), not in this Governance-repo feature. This feature assumes that decision and only implements the Governance-side filename change it mandates.
- **Out of scope**: any change to gate semantics, routing, the SDD handoff consumer, or the optional `policy.yml`/`capabilities.yml`/`tooling.yml` slot names; those keep their current filenames and behavior.
- **Existing goldens**: command/projection golden transcripts that do not embed the primary filename remain byte-identical; any golden or fixture that embeds the literal `project.yml` primary-slot filename is updated as part of the rename.
