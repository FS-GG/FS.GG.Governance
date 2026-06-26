# Feature Specification: Runtime Project Templates

**Feature Branch**: `071-runtime-project-templates`

**Created**: 2026-06-26

**Status**: Draft

**Input**: User description: "for 1" — the first deferred item from the roadmap review: Phase 9's two ⬜ rows, "Add project templates for a new SDD-governed product skeleton" and "Optionally call a template provider for runtime code while keeping runtime ownership outside SDD."

## Overview

Today `fsgg-sdd init` creates the lifecycle skeleton a product needs to be *governed*: the `.fsgg/` policy pointers, the `work/` authoring root, and the initial `readiness/` directories. It deliberately stops there. It does **not** create the runtime project the team will actually build and ship — the buildable source/test layout, package manifest, and entry points. A new team must hand-craft that boilerplate before the lifecycle has any real code to govern.

This feature closes that gap **without** the lifecycle tool taking ownership of runtime code. It adds an optional, pluggable **template-provider** seam: an operator may point bootstrap at a template provider that lays down a runtime project skeleton, while the lifecycle tool itself never hardcodes any provider's template names, package identifiers, target names, or directory layout. Runtime ownership stays with the provider and the product team; the lifecycle tool owns only the delegation, the safety rules around it, and a deterministic record of what was generated.

The seam is strictly opt-in. With no provider selected, bootstrap behaves exactly as it does today, so existing projects and the no-provider path are unaffected.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Scaffold a buildable runtime skeleton from a chosen provider (Priority: P1)

A team starting a new product runs bootstrap and selects a template provider. In addition to the existing `.fsgg/`/`work/`/`readiness/` lifecycle skeleton, the tool delegates to the provider, which lays down a runtime project skeleton (source project, test project, package/manifest files, and entry points appropriate to that provider). The team then has a project that both builds and is ready to be governed, without writing boilerplate by hand.

**Why this priority**: This is the entire point of the feature — turning "an empty governed directory" into "a buildable, governed product." Every other story exists to make this one safe and reusable.

**Independent Test**: Run bootstrap against a temporary directory with a known provider selected; confirm the runtime skeleton appears alongside the lifecycle skeleton, the result builds with the provider's own toolchain, and a deterministic manifest records every generated path. Delivers value on its own even if no second provider exists.

**Acceptance Scenarios**:

1. **Given** an empty target directory and a selected template provider, **When** the operator runs bootstrap with scaffolding requested, **Then** the lifecycle skeleton **and** the provider's runtime skeleton are created, and a manifest lists every path the provider generated.
2. **Given** scaffolding completed, **When** the operator inspects the result, **Then** the runtime files are recorded as provider-owned (not as lifecycle-authored sources), and the lifecycle's own generated views do not claim ownership of or validate the runtime code's internal shape.
3. **Given** a successful scaffold, **When** the operator builds the runtime skeleton with the provider's documented toolchain, **Then** the build succeeds without further hand-editing of boilerplate.

---

### User Story 2 - Bring your own template provider without changing the tool (Priority: P2)

A team needs a runtime stack the tool's authors never anticipated. They author a template provider that satisfies the documented provider contract and register it, then select it at bootstrap time. The lifecycle tool delegates to it identically to any other provider — no change to the tool's own code, and no provider-specific knowledge baked into the tool.

**Why this priority**: The design's boundary rule ("runtime ownership stays outside the lifecycle tool") is only real if third parties can supply providers. Without this, "provider" is just a hardcoded built-in and the seam is fiction.

**Independent Test**: Author a minimal custom provider, register it, and run bootstrap selecting it; confirm the tool resolves and invokes it through the same path as any built-in provider, with no provider name, package id, or layout assumption appearing in the tool's behavior.

**Acceptance Scenarios**:

1. **Given** a provider that conforms to the documented contract, **When** it is registered and selected, **Then** bootstrap resolves and invokes it through the same seam used for any provider.
2. **Given** two different registered providers, **When** each is selected in turn, **Then** the tool's delegation behavior differs only in what the provider emits — the tool applies the same safety, recording, and reporting rules to both.
3. **Given** a provider whose declared contract version is incompatible with the tool, **When** it is selected, **Then** the tool refuses with an explicit, actionable diagnostic rather than partially scaffolding.

---

### User Story 3 - No provider: today's behavior, unchanged (Priority: P3)

An operator runs bootstrap without selecting any template provider. The tool produces exactly the lifecycle skeleton it produces today, with no runtime scaffolding, no new prompts, and byte-identical lifecycle output.

**Why this priority**: The feature must not regress the existing, shipped no-Governance / no-provider bootstrap path. This guards the "strictly optional" promise.

**Independent Test**: Run bootstrap with no provider selected against a temp directory and diff the result against the current `fsgg-sdd init` output; confirm there is no difference.

**Acceptance Scenarios**:

1. **Given** no provider selected, **When** bootstrap runs, **Then** only the existing lifecycle skeleton is created and no template-provider step executes.
2. **Given** no provider selected, **When** the operator compares the output to the pre-feature baseline, **Then** the lifecycle artifacts are byte-identical.

---

### Edge Cases

- **Target not empty / collisions**: A provider would write a file that already exists. The tool MUST NOT silently overwrite operator or prior-run content; it reports the collision and leaves existing files intact (safe, inspectable re-run).
- **Provider failure mid-scaffold**: The provider errors partway. The lifecycle skeleton (which is created first and owned by the tool) MUST remain valid; the partial runtime output is reported explicitly so the operator can clean up or retry, and the failure does not corrupt the governed state.
- **Provider not found / unresolvable**: A selected provider can't be resolved. The tool refuses with an explicit diagnostic and does not fabricate a default runtime skeleton.
- **Provider emits paths outside the target**: A provider attempts to write outside the project target. The tool rejects this as an unsafe provider rather than honoring it.
- **Re-run after a prior scaffold**: Running scaffolding again over an already-scaffolded project is safe — existing runtime files are preserved and reported, not clobbered.
- **Governance/Rendering absent**: Scaffolding works with neither Governance nor FS.GG.Rendering installed; no provider is assumed to be present by default.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The tool MUST be able to delegate runtime-skeleton creation to a selected template provider as an explicitly opt-in step during bootstrap.
- **FR-002**: With no provider selected, the tool MUST produce exactly today's lifecycle skeleton, with byte-identical lifecycle artifacts and no template-provider step.
- **FR-003**: The tool MUST NOT hardcode any provider's template names, package identifiers, target names, runtime toolchain, or directory layout; all such knowledge MUST live in the provider.
- **FR-004**: The tool MUST resolve and invoke any conforming provider — built-in or third-party — through a single documented provider contract, applying identical safety, recording, and reporting rules regardless of provider.
- **FR-005**: The tool MUST record every path a provider generates in a deterministic manifest, marking those paths as provider-owned runtime artifacts rather than lifecycle-authored sources.
- **FR-006**: The tool MUST NOT subject provider-generated runtime code to the lifecycle artifact rules (spec/plan/task/evidence shape, generated-view currency); runtime code is governed later by capability/route rules, not by the lifecycle authoring contract.
- **FR-007**: The tool MUST refuse to overwrite existing files; on a path collision it MUST report the collision and leave existing content intact, keeping bootstrap safe to re-run.
- **FR-008**: The tool MUST treat a provider failure as explicit and recoverable: the tool-owned lifecycle skeleton MUST remain valid, the partial result MUST be reported, and governed state MUST NOT be corrupted.
- **FR-009**: The tool MUST reject a provider that attempts to write outside the project target or that declares an incompatible contract version, with an actionable diagnostic, rather than partially scaffolding.
- **FR-010**: The tool MUST emit a deterministic, machine-readable report of the scaffold outcome (provider identity and contract version, generated paths, collisions, and success/failure) suitable for automation, alongside human-readable output.
- **FR-011**: The feature MUST function with neither FS.GG.Governance nor FS.GG.Rendering installed, and MUST NOT assume any provider is present by default.
- **FR-012**: The selected provider, its contract version, and the generated-path manifest MUST be discoverable after bootstrap so later lifecycle and Governance steps can attribute provenance without re-deriving it.

### Key Entities *(include if feature involves data)*

- **Template Provider**: An external, selectable producer of a runtime project skeleton. Identified by a stable identifier and a declared provider-contract version. Owns all runtime template content; the lifecycle tool knows it only through the contract.
- **Provider Contract**: The versioned agreement describing how the tool selects, invokes, and bounds a provider, and how the provider declares what it emits. The single seam across which all delegation happens.
- **Scaffold Manifest**: A deterministic record of one scaffold run — provider identity and contract version, every generated path (marked provider-owned), any collisions, and the outcome. The provenance record consumed by later steps and automation.
- **Lifecycle Skeleton**: The existing tool-owned `.fsgg/`/`work/`/`readiness/` output. Created before any provider runs and never owned or overwritten by a provider.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A new team can go from an empty directory to a buildable, governed product in a single bootstrap run with a provider selected — no hand-authored runtime boilerplate required before the first successful build.
- **SC-002**: The no-provider bootstrap output is byte-identical to the pre-feature baseline (100% of lifecycle artifacts unchanged).
- **SC-003**: A third-party provider authored solely against the documented contract can be selected and invoked with zero changes to the lifecycle tool.
- **SC-004**: Scaffolding the same provider against the same empty target twice produces an identical manifest (deterministic outcome).
- **SC-005**: Every failure mode (collision, provider error, unresolvable provider, out-of-target write, version mismatch) leaves the lifecycle skeleton valid and produces an explicit, actionable diagnostic — zero silent overwrites or silent partial failures across the failure-mode test set.
- **SC-006**: 100% of provider-generated paths are attributable to a provider and contract version from the manifest after the run, without re-inspecting the provider.

> **Validation boundary (this slice = generic seam core only).** Per [plan.md](./plan.md)
> "Deferred / Out of Scope" and research [D0](./research.md), this feature delivers the
> pluggable seam, its safety rules, and the deterministic manifest projection — **not** a
> concrete buildable provider or the `fsgg-sdd init` host wiring. Consequently:
> **SC-001's "buildable"** (a runtime skeleton that compiles with the provider's toolchain)
> and **SC-002's "byte-identical lifecycle skeleton"** are validated **downstream** in the
> sibling `FS.GG.SDD` repo once a concrete provider + host land; here they are exercised at
> the seam level only — SC-001 via the no-hand-authoring delegation mechanics against a
> disclosed fake provider, SC-002 via the seam's verified no-op on the no-provider path
> (the host owns the actual lifecycle-skeleton bytes). SC-003…SC-006 are fully validated by
> this slice.

## Assumptions

- **Provider trust is operator-authorized.** Selecting a provider is an explicit operator decision (analogous to choosing a project template in mainstream scaffolding tools); the tool does not silently fetch or execute unselected providers. Sandboxing/verification of third-party provider *content* beyond path-boundary and contract-version checks is out of scope for this feature.
- **Scaffolding is a distinct opt-in step, not a change to default init.** The minimal `fsgg-sdd init` lifecycle skeleton remains the default; runtime scaffolding is requested explicitly so the existing path is untouched.
- **Provider distribution mechanism is provider-side.** How a provider is packaged, fetched, or registered is the provider ecosystem's concern; this feature specifies only the selection-and-invocation contract and its safety rules, not a registry or distribution format.
- **The lifecycle skeleton is authored first and is tool-owned.** A provider only adds runtime files; it never creates, mutates, or owns lifecycle artifacts.
- **Runtime governance happens later, elsewhere.** Generated runtime code becomes subject to capability/route/Governance rules through the normal lifecycle, not through this bootstrap step; this feature does not define those rules.
- **No monorepo, Rendering, or Governance assumption.** Consistent with the Phase 9 exit criteria, bootstrap with a provider must not assume a sibling checkout, FS.GG.Rendering, or Governance is present.

## Dependencies

- Builds on the existing bootstrap/init slice (`003-native-sdd-lifecycle-commands` `fsgg-sdd init` and `016-bootstrap-migration`), which owns and creates the lifecycle skeleton this feature scaffolds alongside.
- Aligns with the constitution's operating rule that generic tooling MUST NOT assume rendering's package ids, template names, target names, or layout — the provider contract is the mechanism that keeps that rule satisfied.

## Out of Scope

- Authoring any concrete runtime template content (a specific F#/library/CLI skeleton). This feature defines the seam and its guarantees; shipping a particular built-in provider is a separate decision.
- A provider registry, distribution format, or network fetch mechanism.
- Verifying or sandboxing third-party provider behavior beyond path-boundary and contract-version enforcement.
- Governing the generated runtime code (capability classification, route/ship rules) — that happens through the normal lifecycle after scaffolding.
