---
schemaVersion: 1
workId: 418-governance-release-anchor-repair
title: Governance v1.12.1 release anchor repair
stage: specify
changeTier: tier1
status: specified
publicOrToolFacingImpact: true
---

# Governance v1.12.1 release anchor repair Specification

Prose status: specified

## User Value
Restore trustworthy source provenance for the already-published Governance CLI and F# surface command 1.12.1 artifacts.

## Scope
- SB-001: Tag-only metadata repair for v1.12.1 at 388819d0e060c11f53e5ca2df3277a54a13f9e75, including safe temporary publish-workflow disablement and exact verification; no source, package, version, release-axis, or registry changes.

## Non-Goals
- SB-002: Do not pack, sign, push, or otherwise mutate either published package.
- SB-003: Do not bump any project version or create any tag except `v1.12.1`.
- SB-004: Do not repair or redesign the repository's other version-shaped tag namespaces.

## User Stories
- US-001 (P1): As a user, I can restore trustworthy source provenance for the already-published Governance CLI and F# surface command 1.12.1 artifacts.

## Acceptance Scenarios
- AC-001 [US-001] [FR-001] [FR-002]: Given the four feed artifacts are read before delivery, each nuspec names version `1.12.1` and repository commit `388819d0e060c11f53e5ca2df3277a54a13f9e75`, and the two feeds have identical unsigned payload content per package identity.
- AC-002 [US-001] [FR-003] [FR-004]: Given `v1.12.1` is absent and `publish.yml` is active, when the repair is authorized, the workflow is disabled before the ref is created, the tag resolves exactly to the artifact commit, and the workflow is restored active afterward.
- AC-003 [US-001] [FR-005] [FR-006]: Given a complete before/after workflow-run census and feed snapshot, the repair creates no publish run and changes no package version or payload.

## Functional Requirements
- FR-001: The `FS.GG.Governance.Cli` and `FS.GG.Governance.FSharpSurfaceCommand` 1.12.1 nuspecs from both GitHub Packages and nuget.org MUST identify repository commit `388819d0e060c11f53e5ca2df3277a54a13f9e75`. (Stories: US-001; Acceptance: AC-001)
- FR-002: For each package identity, the GitHub Packages archive and nuget.org archive MUST have identical unsigned package entries and bytes; nuget.org's signing-only additions are excluded explicitly. (Stories: US-001; Acceptance: AC-001)
- FR-003: Delivery MUST verify `v1.12.1` is absent, verify a known-present tag with the same complete remote-tag census, and create only `refs/tags/v1.12.1` at the exact artifact commit. (Stories: US-001; Acceptance: AC-002)
- FR-004: Because the current workflow subscribes to `push.tags: ['v*']`, delivery MUST disable `publish.yml` before creating the tag and MUST restore its prior active state after the tag is verified; any failure after disablement MUST restore the workflow before returning. (Stories: US-001; Acceptance: AC-002)
- FR-005: A complete publish-workflow run census before and after delivery MUST show no run created for the `v1.12.1` repair event. (Stories: US-001; Acceptance: AC-003)
- FR-006: The two package versions and their feed payload snapshots MUST remain unchanged; the repair MUST NOT invoke pack, publish, release creation, or a version mutation. (Stories: US-001; Acceptance: AC-003)

## Ambiguities
No material ambiguities recorded.

## Public Or Tool-Facing Impact
- This specification is an SDD lifecycle artifact and command-report contract input.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd clarify --work 418-governance-release-anchor-repair`.
