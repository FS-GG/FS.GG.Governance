# Feature Specification: `.fsgg` Project, Policy, Capability, and Tooling Schemas

**Feature Branch**: `014-fsgg-project-policy-capability-schemas`

**Created**: 2026-06-20

**Status**: Draft

**Input**: User description: "start the next item in the implementation plan" — the next Governance-owned item in `docs/initial-implementation-plan.md` is Phase 2, *Governance Ship Walking Skeleton And Catalog MVP*, whose foundational deliverable is the versioned `.fsgg` source schemas (also tracked as `014-fsgg-project-policy-capability-schemas` in the repo's kernel Spec Kit plan).

## Overview

A product that wants Governance to inspect it must first be able to *declare itself*: its identity, its enforcement policy, the capabilities and protected surfaces that live in its tree, and the tools/commands Governance is allowed to run. Today no such declaration exists, so route/ship reasoning has nothing typed to stand on.

This feature delivers the **source-of-truth schemas** for four versioned files — `.fsgg/project.yml`, `.fsgg/policy.yml`, `.fsgg/capabilities.yml`, and `.fsgg/tooling.yml` — together with strict parsing, validation, deterministic diagnostics, and the conversion of valid declarations into typed facts. It deliberately stops short of *using* those facts: git/CI sensing, path-to-capability routing, the gate registry, and the `ship` command are later features in the same phase.

This is the minimum that lets a governed product answer, declaratively: *who am I, what is governed here, what is protected, and what may Governance run?*

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Declare a governed product and get typed facts (Priority: P1)

A maintainer adds the four `.fsgg` files to their repository to declare project identity, the default enforcement profile, a capability catalog (domains, path map, surfaces, checks, owner, cost, maturity, environment), and a tooling policy (allowed commands, timeouts, environment classes). They run the Governance validation surface against the directory and receive a single, deterministic, typed result describing the declared configuration.

**Why this priority**: Without a parsed, typed declaration nothing else in the capability/ship phase can be built. This story is the MVP: it is independently valuable because it gives a product a machine-checked statement of what it governs even before any routing or gate exists.

**Independent Test**: Point the validation surface at a fixture product that contains all four valid `.fsgg` files; assert it reports success and emits typed facts (project identity, default profile, capability entries, protected surfaces, tooling policy) with stable ordering and no leakage of raw YAML or product-specific vocabulary into the typed result.

**Acceptance Scenarios**:

1. **Given** a directory with valid `.fsgg/project.yml`, `.fsgg/policy.yml`, `.fsgg/capabilities.yml`, and `.fsgg/tooling.yml`, **When** the product is validated, **Then** the result is "valid" and exposes typed facts for project identity, domains, path map, protected surfaces, checks (with owner, cost, maturity, environment), default profile, command allow-list, environment classes, and timeouts.
2. **Given** the same valid directory validated twice, **When** the typed facts are compared, **Then** they are byte-for-byte identical (deterministic ordering of domains, surfaces, checks, commands).
3. **Given** a valid directory, **When** the typed facts are inspected, **Then** they contain no raw YAML fragments and no product-specific names that the schemas do not define.

---

### User Story 2 - Reject malformed declarations with actionable diagnostics (Priority: P1)

A maintainer makes a mistake — a duplicate capability id, an unknown field, a malformed or unsupported schema version, a path that escapes the governed root, a check that references a capability domain that does not exist. The validation surface refuses the file and returns specific, stable diagnostics naming the file, the offending location, the diagnostic id, and what to fix — never a partial or silently-corrected result.

**Why this priority**: Strictness is the whole point of a machine contract. A schema that accepts ambiguous input cannot be a source of truth, and quiet acceptance of malformed config would poison every downstream gate. This is co-equal P1 with Story 1.

**Independent Test**: Feed a battery of malformed fixtures (one defect per fixture) and assert each produces its expected diagnostic id and a non-success result, with no typed facts emitted for the rejected file.

**Acceptance Scenarios**:

1. **Given** a `.fsgg/capabilities.yml` with two capabilities sharing an id, **When** validated, **Then** a duplicate-id diagnostic identifies both occurrences and the file, and validation fails.
2. **Given** a file containing a field the schema does not define, **When** validated, **Then** an unknown-field diagnostic names the field and path, and validation fails.
3. **Given** a file whose `schemaVersion` is missing, malformed, or newer than the supported version, **When** validated, **Then** a schema-version diagnostic explains the supported range and validation fails.
4. **Given** a path-map entry or surface path that normalizes outside the governed root (e.g. via `..`), **When** validated, **Then** a path-normalization diagnostic rejects it.
5. **Given** a check that references a capability domain not declared in the catalog, **When** validated, **Then** a dangling-reference diagnostic names the check and the missing domain, and validation fails.

---

### User Story 3 - Cover the real product surface shapes with fixtures (Priority: P2)

A maintainer of a non-trivial product needs the schemas to express the surface kinds the design names: routine/unmanaged docs and drafts, governed roots, protected package/API surfaces, generated views, and release surfaces. Fixture products demonstrate each shape so a new adopter can copy a realistic example rather than guess.

**Why this priority**: The MVP must be expressive enough for the first real protected boundary, but the catalog's full product-adapter expansion is a later phase. P2 because Stories 1–2 prove the mechanism; this proves the mechanism covers the MVP surface vocabulary.

**Independent Test**: Validate fixture products that each declare one of the MVP surface classes (routine docs, governed root, protected package/API surface, generated view, release surface) and assert each is accepted and classified into the correct typed surface category.

**Acceptance Scenarios**:

1. **Given** a fixture declaring a protected package/API surface, **When** validated, **Then** the typed facts classify it as a protected surface distinct from routine paths.
2. **Given** a fixture declaring a generated view and a release surface, **When** validated, **Then** each is represented as its own typed surface class with its owner and maturity preserved.
3. **Given** a fixture whose tree contains only routine, undeclared files, **When** validated, **Then** no protected-surface or governed-root facts are produced for those files.

---

### Edge Cases

- **Missing files**: What happens when one or more of the four `.fsgg` files is absent? The surface MUST distinguish "file absent" from "file present but invalid," and MUST state which of the four are required for a minimally valid declaration versus optional.
- **Empty but present file**: An empty or whitespace-only `.fsgg` file is treated as malformed (missing required `schemaVersion`), not as an empty-but-valid declaration.
- **Cross-file references**: A check or surface in `capabilities.yml` referring to a profile defined in `policy.yml`, or a command in `tooling.yml` referenced by a check — dangling cross-file references MUST be diagnosed, not silently dropped.
- **Path shape portability**: Paths authored with `\` or `/` separators, leading `./`, or differing case MUST normalize deterministically so the same logical path produces the same typed fact regardless of how it was written.
- **Ordering of authored input**: Re-ordering domains, surfaces, checks, or commands in the source file MUST NOT change the typed facts or their serialized order.
- **Duplicate ids across files**: The same id reused in two different files where uniqueness is required (e.g. capability ids) MUST be diagnosed.
- **Unsupported newer schema version**: A file declaring a schema version newer than the tool supports MUST fail with a clear "upgrade the tool" style diagnostic rather than a generic parse error.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST define MVP schemas for `.fsgg/project.yml`, `.fsgg/policy.yml`, `.fsgg/capabilities.yml`, and `.fsgg/tooling.yml`, each carrying an explicit, validated `schemaVersion`.
- **FR-002**: `project.yml` MUST express project identity, the declared domain list, the default work/governed root, package surfaces, and pointers to the policy and capability declarations.
- **FR-003**: `policy.yml` MUST express the available enforcement profiles, the default profile, and the policy fields needed for later enforcement (branch policy and review-budget placeholders MAY be declared but are not enforced by this feature).
- **FR-004**: `capabilities.yml` MUST express, at minimum, capability domains, a path map, protected surfaces, checks, and per-entry owner, cost, environment, and maturity.
- **FR-005**: `tooling.yml` MUST express a command allow-list, per-command timeout limits, environment classes, and external tool/version expectations.
- **FR-006**: The system MUST parse each file strictly: unknown fields, missing required fields, malformed values, and duplicate ids MUST cause validation failure, never partial acceptance or silent correction.
- **FR-007**: The system MUST support schema versioning, accepting only versions it understands and emitting a specific diagnostic for missing, malformed, or unsupported (too-new) versions.
- **FR-008**: The system MUST normalize all declared paths deterministically and reject paths that resolve outside the declared governed root.
- **FR-009**: The system MUST validate cross-references (e.g. a check referencing a declared capability domain, a surface or check referencing a declared profile or command) and emit a dangling-reference diagnostic when a referenced id does not exist.
- **FR-010**: The system MUST emit typed facts for valid declarations — project identity, profiles/default profile, capabilities, path map, classified surfaces, checks, command allow-list, environment classes, and timeouts — without leaking raw YAML or product-specific vocabulary into the typed model.
- **FR-011**: The system MUST classify declared surfaces into the MVP surface categories named by the design: routine/unmanaged, governed root, protected package/API surface, generated view, and release surface.
- **FR-012**: Typed facts and any serialized/rendered output MUST be deterministic: identical source trees produce byte-stable results, and re-ordering equivalent authored entries does not change the result.
- **FR-013**: Every diagnostic MUST carry a stable diagnostic id, the offending file, a locating reference (field/path/id), and a human-readable explanation of what to fix.
- **FR-014**: The feature MUST provide fixture products covering: all-four-valid declarations, each malformed/diagnostic case, and each MVP surface class (routine docs, governed root, protected package/API surface, generated view, release surface).
- **FR-015**: The system MUST clearly distinguish "file absent" from "file present but invalid," and MUST document which files are required for a minimally valid declaration.
- **FR-016**: The system MUST NOT implement git/CI sensing, path-to-capability routing, gate-registry assembly, profile-adjusted enforcement, or the `ship` command; it provides only the declarations and typed facts those later features consume.

### Key Entities *(include if feature involves data)*

- **Project declaration**: The product's identity, declared domains, governed/work root, package surfaces, and pointers to policy and capabilities. Source: `project.yml`.
- **Policy declaration**: The set of enforcement profiles and the default profile (plus declared-but-not-yet-enforced branch/review-budget fields). Source: `policy.yml`.
- **Capability**: A named, uniquely-identified governed concern with a domain, an owner, a cost, an environment class, and a maturity level. Source: `capabilities.yml`.
- **Path map**: The mapping from declared path patterns to capabilities, used later for routing; here only declared, normalized, and validated.
- **Surface**: A classified region of the tree — routine/unmanaged, governed root, protected package/API, generated view, or release surface — with its owner and maturity.
- **Check**: A declared verification associated with a capability, carrying owner, cost, environment, and maturity, and referencing only declared ids.
- **Tooling policy**: The command allow-list, per-command timeouts, environment classes, and external tool/version expectations. Source: `tooling.yml`.
- **Schema version**: The explicit, validated version stamp present on every `.fsgg` file.
- **Diagnostic**: A stable-id, located, explained record of why a declaration was rejected.
- **Typed facts**: The product-neutral, YAML-free typed result handed to later Governance features.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A product author can write the four `.fsgg` files and confirm a valid declaration, or learn exactly what is wrong, in a single validation run — with no need to read source code to interpret the result.
- **SC-002**: Validating an identical source tree any number of times yields byte-identical typed facts and diagnostics (100% reproducibility).
- **SC-003**: Every malformed-input class named in this spec (unknown field, missing required field, missing required file, duplicate id, missing/malformed/unsupported schema version, out-of-root path, dangling reference, empty file) is covered by at least one fixture and produces its own distinct, stable diagnostic id.
- **SC-004**: Each MVP surface class (routine, governed root, protected package/API, generated view, release surface) has at least one accepted fixture that classifies into the correct typed category.
- **SC-005**: No raw YAML text or product-specific identifier that the schemas do not define appears anywhere in the typed facts (verified by inspecting the typed output of every valid fixture).
- **SC-006**: A reviewer can confirm, from the typed facts alone, that "what changed, why a gate would run, and which governed path is unknown" is *answerable in principle* — i.e. the declarations contain every input those later questions require.

## Assumptions

- **Change classification: Tier 1.** This feature adds a new public, packable surface (config schemas, validation, typed facts) and its `.fsi` signatures, consistent with the kernel plan's tier for `014`.
- **Layering.** Per the kernel plan and the constitution's operating rule, schema parsing/validation/typed-fact production lives in Host or a light configuration library; the Kernel receives only typed facts and never sees YAML or product vocabulary.
- **YAML is the authoring format** for `.fsgg` files (the design and source-model tables specify `.yml`). The machine contract is the typed facts, not the YAML text.
- **MVU boundary.** Reading files from disk is I/O at the edge; per Constitution Principle IV the file-reading/validation workflow is wrapped behind an explicit effect/interpreter boundary, while pure parse/validate/normalize functions need no MVU ceremony.
- **Scope is the MVP catalog only.** Full product-adapter expansion (generated-product check cost tiers, package/docs/skills/design facts, baselines, evidence tags) is Phase 10 and explicitly out of scope here.
- **Profiles are declared, not enforced.** This feature parses profiles and the default profile but does not compute effective enforcement — that is Phase 5 (`Route Parity, Profiles, And Enforcement Fixtures`).
- **Required vs optional files.** Assumption for the MVP: `project.yml` and `capabilities.yml` are required for a meaningful declaration; `policy.yml` and `tooling.yml` are optional but, when present, fully validated. (Confirmable during `/speckit-plan`.)
- **Numbering.** This feature is `014` to match the repo's kernel Spec Kit plan, which reserves `013` for `run-against-external-repo`; spec directory numbering in this repo follows that plan rather than strict next-available.

## Out of Scope

- Git/CI snapshot facts (base/head, changed/dirty/untracked paths, branch, PR labels, CI context) — later feature in Phase 2.
- Deterministic glob precedence and path-to-capability *routing* — declarations are validated and normalized here, but route resolution is a later feature.
- `.fsgg/gates.json` gate-registry assembly and `GateId` metadata emission.
- The `fsgg route` and `fsgg ship --mode gate --profile standard --json` commands and their route/audit JSON.
- Profile/mode/maturity *effective enforcement* computation and truth-table fixtures.
- GitHub Actions branch-protection guidance.
