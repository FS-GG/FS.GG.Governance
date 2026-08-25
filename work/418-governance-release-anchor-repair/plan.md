---
schemaVersion: 1
workId: 418-governance-release-anchor-repair
title: Governance v1.12.1 release anchor repair
stage: plan
changeTier: tier1
status: planned
sourceSpec: work/418-governance-release-anchor-repair/spec.md
sourceClarifications: work/418-governance-release-anchor-repair/clarifications.md
sourceChecklist: work/418-governance-release-anchor-repair/checklist.md
publicOrToolFacingImpact: true
---

# Governance v1.12.1 release anchor repair Plan

Prose status: planned

## Source Snapshot
- spec: work/418-governance-release-anchor-repair/spec.md sha256:9de246dff54a55d9f1d907062a881d8c25b541072b71529a70cb885083676dc6 schemaVersion:1
- clarifications: work/418-governance-release-anchor-repair/clarifications.md sha256:9b4614020fb9cf22254622261d8e90ad23298cc58de3fca7aef4de10c8bd45b0 schemaVersion:1
- checklist: work/418-governance-release-anchor-repair/checklist.md sha256:1f6195fa85b97b93bd0652f196249ef21823f57acd20d37a3edf48467326a353 schemaVersion:1

## Plan Scope
- Produce reviewable SDD/readiness evidence and a typed, failure-atomic operational delivery recipe; do not change product or workflow source.
- Establish artifact provenance from both authoritative feeds, with explicit signing-only exclusions when comparing archive entries.
- Deliver one lightweight tag consistent with the existing `v1.12.0` tag shape, fenced by temporary workflow disablement and complete before/after censuses.

## Plan Decisions
- PD-001 [AC-001] [FR-001] complete: Download both package identities from GitHub Packages and nuget.org, parse each nuspec, and require the same exact repository URL, version, and 40-character source commit.
- PD-002 [AC-001] [FR-002] complete: Compare archive entry bytes per identity after excluding only nuget.org's signing additions; report any other entry or digest difference as blocking.
- PD-003 [AC-002] [FR-003] complete: Fetch the complete remote tag namespace, use known-present `v1.12.0` as a positive control, require `v1.12.1` absent, and create a lightweight ref from `388819d0e060c11f53e5ca2df3277a54a13f9e75` directly.
- PD-004 [AC-002] [FR-004] [DEC-001] [DEC-002] complete: Read workflow id `303994065` and its current state through the Actions API; require `active`, disable it, re-read `disabled_manually`, and guarantee `gh workflow enable publish.yml` runs on every exit after disablement.
- PD-005 [AC-003] [FR-005] [DEC-002] complete: Enumerate all pages of workflow `303994065` runs immediately before disablement and after re-enablement, bind each census to its terminal pagination state, and reject any new run whose head branch/tag or head SHA corresponds to the repair.
- PD-006 [AC-003] [FR-006] [DEC-003] complete: Re-download and compare package indexes and unsigned payload digests after delivery; never invoke pack, publish, release creation, or version editing.

## Contract Impact
- PC-001 [PD-003] [PD-004] release metadata: add only `refs/tags/v1.12.1` as a source anchor for two already-published 1.12.1 tool packages; no package or executable contract changes.

## Verification Obligations
- VO-001 [PD-001] [PD-002] [PD-003] [PD-004] [PD-005] [PD-006] [PC-001] releaseAnchor: Preserve raw, machine-readable pre-delivery evidence for package metadata, unsigned entry digests, remote tags, commit object, workflow trigger/state, and paginated run census; after authorization, repeat each measurement and prove the only intended delta is `v1.12.1` resolving to the exact commit.

## Performance Intent
No performance intent is declared for this work item.

## Migration Posture
- PM-001 [PC-001] metadataOnly: Consumers and package pins do not migrate; the tag repairs repository provenance for artifacts already in use.

## Generated View Impact
- GV-001 [PD-001] [PD-005] workModel: SDD work-model and verification evidence bind the exact package and workflow observations; no product-generated projection changes.

## Accepted Deferrals
No accepted plan deferrals recorded.

## Planning Findings
No blocking planning findings recorded.

## Advisory Notes
- The operational delivery must not begin until fresh independent review and host authorization approve the exact recipe and immutable target.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd tasks --work 418-governance-release-anchor-repair`.
