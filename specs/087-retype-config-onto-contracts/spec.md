# Feature Specification: Re-type Config loader/schema onto FS.GG.Contracts

**Feature Branch**: `main` (feature dir `specs/087-retype-config-onto-contracts`)

**Created**: 2026-06-28

**Status**: Draft

**Change Classification**: **Tier 1** (contracted change — introduces a new package dependency, `FS.GG.Contracts`, and single-sources the `.fsgg` schema record shapes + version constants that the Config public surface exposes). Per the constitution, "introduces new dependencies" alone makes this Tier 1; it therefore carries the full artifact chain (plan, `.fsi` review, surface-area baseline review, test evidence). No *observable behavior* changes. See plan.md "Constitution Check".

**Input**: User description: "start the next unblocked item on the coordination board." → resolved to Coordination board item **FS-GG/FS.GG.Governance#14**: *"H2 · governance — Re-type Config.Loader onto FS.GG.Contracts schemas (governance/policy/capabilities/tooling)"*. The item was `Blocked` on **FS-GG/FS.GG.SDD#18** (correct `Fsgg.Schemas.capabilitiesVersion` 1→2 and republish `FS.GG.Contracts 1.0.1`); that blocker **closed 2026-06-28**, `FS.GG.Contracts` is published to the org GitHub Packages feed, and the registry pin `fsgg-contracts` is now **1.0.1** (caps=2) — so the item is now unblocked and ready. Contract: `fsgg-contracts` (governance config schema bundle). Part of epic **FS-GG/.github#16** (homogeneous build · contracts · auto-update fabric).

## Overview

The FS-GG org has extracted the machine-readable form of every cross-repo `.fsgg` schema — the typed schema records **and** the per-file `schemaVersion` constants for `governance`, `policy`, `capabilities`, and `tooling` — into a single BCL-only F# leaf package, **`FS.GG.Contracts`** (`Fsgg.Schemas`), owned by FS.GG.SDD as the schema authority and pinned in the registry as `fsgg-contracts@1.0.1`.

Today FS.GG.Governance's `FS.GG.Governance.Config` library hand-declares its own copy of those schema shapes and **hard-codes the supported `schemaVersion` for each file** (`capabilities = 2`, `project/policy/tooling = 1`). That is a second source of truth for a cross-repo contract: if SDD bumps a schema version, Governance silently keeps the old number until someone notices. The `capabilities = 1→2` correction that produced `1.0.1` (SDD#18) is exactly this class of drift.

This feature makes `FS.GG.Governance.Config` a **consumer** of the shared contract: the loader/schema validator takes its schema record shapes and its per-file version constants from `FS.GG.Contracts` instead of re-declaring them, so the version numbers are single-sourced (`capabilities = 2`, the rest `= 1`, all read from the package). It is a re-typing/de-duplication change with **zero change to validation behavior** — the same valid inputs validate, the same malformed inputs produce the same diagnostics, the loaded typed facts are identical, and the full test suite stays green. The public Config surface stays behavior-compatible; the only surface delta permitted is the kind a re-export of shared types produces, blessed additively in the surface baseline.

This is the Governance row of the H2 "typed coherence backbone" sequence; SDD already re-typed its provider registry (SDD#9) and Templates re-typed its provider descriptor/overlay (Templates#13) onto the same package.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Single-source the schema versions and shapes with no behavior change (Priority: P1)

As a Governance maintainer, I want `FS.GG.Governance.Config` to consume the `.fsgg` schema record shapes and per-file `schemaVersion` constants from `FS.GG.Contracts` rather than re-declaring them locally, so that the cross-repo schema contract has exactly one source of truth while config loading behaves exactly as before.

**Why this priority**: This is the entire item. Without it the version constants stay forked and the drift class that caused the `1.0.1` correction can recur. It must be safe (identical observable behavior) before anything else.

**Independent Test**: Run the full build and the complete test suite before and after the change; every project compiles and every test passes with the same counts. Loading the existing valid and malformed `.fsgg` fixtures produces byte-identical typed facts and byte-identical diagnostics in both runs.

**Acceptance Scenarios**:

1. **Given** the repo before the change (Config hand-declares schema shapes and hard-codes the four version constants), **When** the Config library is re-typed to consume `FS.GG.Contracts` schema records and version constants, **Then** the full solution builds and the entire test suite passes with the same results as before.
2. **Given** the re-typed Config library, **When** a valid `.fsgg/{governance,policy,capabilities,tooling}.yml` set is loaded, **Then** the resulting typed facts are identical (field-for-field) to those produced before the change.
3. **Given** the re-typed Config library, **When** each existing malformed/edge fixture is loaded (unknown field, missing required field, malformed value, duplicate id, dangling reference, path-escapes-root, unsupported/missing/malformed schema version, empty file, missing required file), **Then** the same diagnostic ids, locators, and messages are produced as before.
4. **Given** the re-typed Config library, **When** the deterministic-output property tests run, **Then** reordered YAML input still yields byte-identical output (determinism preserved).

---

### User Story 2 - The per-file schema versions are read from the shared package, not hard-coded (Priority: P2)

As a Governance maintainer, I want the supported `schemaVersion` for each `.fsgg` file to come from `FS.GG.Contracts` so that `capabilities = 2` and `project/policy/tooling = 1` are enforced from the same constants the rest of the org uses, and a future org-wide version bump flows in by updating the package pin rather than by editing Governance code.

**Why this priority**: This is the coherence payoff — it closes the specific drift that SDD#18 had to correct. It depends on P1 having adopted the package.

**Independent Test**: Inspect the resolved supported version for each file; `capabilities` resolves to `2` and `project`/`policy`/`tooling` resolve to `1`, and each value originates in the `FS.GG.Contracts` package (no local literal). Load a `capabilities.yml` declaring `schemaVersion: 1`; it is rejected as unsupported with the existing migration guidance, exactly as today.

**Acceptance Scenarios**:

1. **Given** the re-typed validator, **When** the supported version for `capabilities` is resolved, **Then** it is `2`, sourced from the shared package constant (not a Governance literal).
2. **Given** the re-typed validator, **When** the supported version for `project`, `policy`, or `tooling` is resolved, **Then** it is `1`, sourced from the shared package constant.
3. **Given** a `capabilities.yml` with `schemaVersion: 1`, **When** it is loaded, **Then** it is reported as an unsupported schema version with the same diagnostic and the existing migration-doc pointer as before the change.

---

### User Story 3 - Future schema/version changes flow in by bumping the package pin (Priority: P3)

As a Governance maintainer, I want a future change to a `.fsgg` schema shape or supported version to reach Governance by bumping the `FS.GG.Contracts` package pin (and re-restoring) rather than by re-editing Config's type and constant declarations, so that staying coherent with the org schema authority is cheap and drift-resistant.

**Why this priority**: This is the forward-looking value of consuming the package instead of forking it. Least urgent because it only matters on the *next* schema change.

**Independent Test**: With Config consuming the package, point the pin at a hypothetical newer `FS.GG.Contracts` that bumps a supported version; the new value takes effect through the package with no edit to Governance's schema/version declarations (demonstrated by inspection of where the constant is sourced).

**Acceptance Scenarios**:

1. **Given** Config consumes the shared version constants, **When** the `FS.GG.Contracts` pin is advanced to a version carrying a different supported `schemaVersion`, **Then** Governance picks up the new value via the package without editing the Config source.

---

### Edge Cases

- **A Contracts schema record is shaped differently from Governance's internal fact model.** `FS.GG.Contracts` `Fsgg.Schemas` owns the *raw on-disk schema record shapes* and version constants; Governance's `Config.Model` additionally carries a *richer typed-fact model* (e.g. `SurfaceClass`/`Cost`/`Maturity` enums, identity newtypes, the diagnostic model) that the validator derives. Only the parts that genuinely duplicate a Contracts type/constant are single-sourced; Governance-specific fact and diagnostic types that have no Contracts equivalent stay local. The boundary (what is shared vs. local) is a planning decision, but the *behavior* of validation and the *shape* of the loaded typed facts must not change.
- **Contracts declares a version Governance must keep.** The package fixes `capabilities = 2`; if a stale local literal disagreed, the package value wins and the local literal is removed (this is the drift being closed, not a regression).
- **Package not on the restore feed / wrong version.** The `FS.GG.Contracts` PackageReference must restore from the org GitHub Packages feed at the registry-pinned version (`1.0.1`); a missing feed/credential or a pin mismatch is a build/restore failure surfaced before any behavior question, not a silent fallback to the old local types.
- **New dependency under locked restore.** Adding the PackageReference changes the dependency graph, so the committed lockfile(s) must be regenerated; in locked-restore CI an un-regenerated lockfile fails restore (expected, and the signal to regenerate).
- **Public surface delta from re-export.** If a Governance Config type is replaced by a re-export of the Contracts type, the name-level surface may shift; any such shift must be additive/blessed in the surface baseline, never a silent break for the 50+ downstream test/consumer projects that compile against `Config`.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `FS.GG.Governance.Config` MUST take a PackageReference on `FS.GG.Contracts` at the registry-pinned version (`fsgg-contracts@1.0.1`), restored from the org GitHub Packages feed.
- **FR-002**: The per-file supported `schemaVersion` constants — `capabilities = 2`, `project = 1`, `policy = 1`, `tooling = 1` — MUST be sourced from `FS.GG.Contracts` (`Fsgg.Schemas`) constants, not hard-coded as literals in Governance Config.
- **FR-003**: Where `FS.GG.Contracts` (`Fsgg.Schemas`) provides a typed schema record shape for a `.fsgg` file that Governance Config currently re-declares, Config MUST consume the shared type rather than maintaining a duplicate declaration.
- **FR-004**: Governance-specific fact and diagnostic types that have **no** equivalent in `FS.GG.Contracts` (the derived typed-fact model, identity newtypes, `SurfaceClass`/`Cost`/`Maturity`/`GeneratedProductTier` enums, the `Diagnostic`/`DiagnosticId`/`Validation` model) MUST remain owned by Governance Config; this feature single-sources only the genuinely-shared schema shapes and version constants.
- **FR-005**: Config validation behavior MUST be unchanged: every input that validated before validates after (with identical typed facts), and every input that produced a diagnostic before produces the **same** diagnostic id, locator, and message after — including the `capabilities` unsupported-version case and its migration-doc pointer.
- **FR-006**: Deterministic output MUST be preserved: identical source trees yield byte-identical output and reordered input yields byte-identical output, exactly as before.
- **FR-007**: The Config I/O edge (the file-reading `Loader` boundary: `FileReader` port, filesystem reader, `readSource`, `loadAndValidate`) MUST keep its public signature and behavior — it reads the same `.fsgg` files and composes the same validation.
- **FR-008**: The change MUST NOT alter observable behavior of any downstream Governance command, projection, gate, route, ship, or verify output. All command/projection goldens and snapshots remain byte-identical.
- **FR-009**: Any change to the public Config surface (`.fsi`) MUST be limited to re-exporting shared types from `FS.GG.Contracts`; the surface-area baseline MUST be updated additively/blessed to match (no silent break), and downstream projects MUST compile unchanged. The preferred outcome is a byte-identical name-level surface via same-named re-exports.
- **FR-010**: The committed dependency lockfile(s) MUST be regenerated to include the new `FS.GG.Contracts` dependency so that locked-mode restore succeeds in CI.
- **FR-011**: The full build and the complete test suite MUST pass after the change with results equivalent to before (same projects compile, same tests pass), and no new schema/version literal may be reintroduced in Governance Config for the single-sourced values.

### Key Entities *(schema/contract artifacts)*

- **`FS.GG.Contracts` (`Fsgg.Schemas`)**: the upstream BCL-only package owning the machine-readable `.fsgg` schema record shapes and the per-file `schemaVersion` constants (`capabilities = 2`, others `= 1`); pinned as `fsgg-contracts@1.0.1`; owned by FS.GG.SDD.
- **Governance Config schema/validator**: the `FS.GG.Governance.Config` library (`Model` / `Schema` / `Loader`) that parses, validates, and version-checks the four `.fsgg` files; the consumer being re-typed.
- **Per-file version constants**: the supported `schemaVersion` per file — the specific values being single-sourced from the shared package.
- **Governance-owned fact/diagnostic model**: the derived typed facts (`TypedFacts` and its parts), identity newtypes, closed enums, and the diagnostic model that stay local because they have no Contracts equivalent.
- **Registry pin**: the `fsgg-contracts` entry in `FS-GG/.github` `registry/dependencies.yml` (`version 1.0.1`, owner `sdd`) that fixes the consumed version.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The full solution build and the complete test suite are green after the change, with the same project and test-pass counts as before (no behavior change).
- **SC-002**: Loading every existing valid `.fsgg` fixture produces typed facts identical to those produced before the change (verifiable by an unchanged typed-facts/serialization assertion over the fixtures).
- **SC-003**: Loading every existing malformed/edge fixture produces the same diagnostic id, locator, and message as before (verifiable by the unchanged diagnostic test suite passing).
- **SC-004**: The supported `schemaVersion` resolves to `2` for `capabilities` and `1` for `project`/`policy`/`tooling`, and each value is sourced from a `FS.GG.Contracts` constant — no Governance-local literal remains for those four values (verifiable by code inspection / a guard that the literals are gone).
- **SC-005**: `FS.GG.Contracts` restores at the registry-pinned `1.0.1` from the org feed, and locked-mode restore succeeds in CI with the regenerated lockfile (no restore error).
- **SC-006**: Every Governance command/projection golden and snapshot is byte-identical to before the change (verifiable by an empty diff over those fixtures).
- **SC-007**: The public Config surface change (if any) is confined to re-exported shared types and is reflected additively in the surface-area baseline; downstream projects compile without source edits (verifiable by an additive-only baseline diff and a green downstream build).

## Assumptions

- **The package is available and pinned.** `FS.GG.Contracts@1.0.1` (caps=2) is published to the org GitHub Packages feed and pinned in the registry; the blocker (SDD#18) is closed. Adoption is a consumer-side change only — no change to the `fsgg-contracts` contract surface or its registry pin is produced here.
- **`Fsgg.Schemas` owns the four files' versions and shapes.** The registry records `Fsgg.Schemas` as carrying the typed `.fsgg` schema records and version constants for `governance/policy/capabilities/tooling`; this feature consumes those. The exact set of record types that overlap Governance's current declarations is a discovery/planning detail (`plan.md`), constrained by FR-003/FR-004.
- **Single-source scope is "shapes + version constants", not Governance's whole fact model.** The richer derived typed-fact model, identity newtypes, closed enums, and diagnostic model stay Governance-owned (no Contracts equivalent); this matches the SDD#9 / Templates#13 re-typings, which adopted the shared records without surrendering repo-specific derived types.
- **Behavior parity is the gate.** "No behavior change" means identical typed facts for valid inputs, identical diagnostics for invalid inputs, preserved determinism, and byte-identical downstream goldens/snapshots — proven by the existing test suite plus the byte-identical fixture/golden checks, not by adding new behavior.
- **Surface stability is preferred over surface change.** The intended outcome is a byte-identical name-level Config surface via same-named re-exports; if a small additive baseline delta is unavoidable, it is blessed in-feature, never a silent break. The exact `.fsi` treatment is a planning decision within FR-009.
- **Lockfile regeneration is expected.** Adding the dependency requires regenerating committed lockfiles; this is mechanical and part of the feature, consistent with the org's locked-restore posture (adopted in feature 085 / `.github#19`).
- **Out of scope**: the org-wide ApiCompat/PublicApiAnalyzers gate adoption (`.github#20`, in progress) and the `FS.GG.Governance.ReferenceGateSet` packaging (#15, separate board item) are not part of this feature; this feature neither enables nor depends on them.
