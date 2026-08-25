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
- spec: work/418-governance-release-anchor-repair/spec.md sha256:aabffe7f7e9dc6bb18f878e35d70c2eabd4ec264a365083ee03210bef68c0625 schemaVersion:1
- clarifications: work/418-governance-release-anchor-repair/clarifications.md sha256:1f70543852f08eddae39c4eee5784a92aa2604070b65d3a69460ff78aad802ff schemaVersion:1
- checklist: work/418-governance-release-anchor-repair/checklist.md sha256:473982edc81ff4065c86bcd35d3faca95b0908a8ccad19b810a3e70f43180458 schemaVersion:1

## Plan Scope
- Produce reviewable SDD/readiness evidence and a typed, failure-atomic operational delivery recipe; do not change product or workflow source.
- Establish artifact provenance from both authoritative feeds, with explicit signing-only exclusions when comparing archive entries.
- Deliver one lightweight tag consistent with the existing `v1.12.0` tag shape, fenced by temporary workflow disablement and complete before/after censuses.

## Plan Decisions
- PD-001 [AC-001] [FR-001] complete: Download both package identities from GitHub Packages and nuget.org, parse each nuspec, and require the same exact repository URL, version, and 40-character source commit.
- PD-002 [AC-001] [FR-002] complete: Compare archive entry bytes per identity after excluding only nuget.org's signing additions; report any other entry or digest difference as blocking.
- PD-003 [AC-002] [FR-003] complete: Fetch the complete remote tag namespace, use known-present `v1.12.0` as a positive control, require `v1.12.1` absent, and create a lightweight ref from `388819d0e060c11f53e5ca2df3277a54a13f9e75` directly.
- PD-004 [AC-002] [FR-004] [DEC-001] [DEC-002] complete: Deliver through `release_anchor_runner.py`, gated by exact apply/target confirmation. After any disable attempt its `finally` path retries enablement five times, contains and records backoff-sleep exceptions, verifies workflow `303994065` is `active`, preserves a primary failure ahead of cleanup failure, and treats every recovered cleanup disturbance as a reported failure.
- PD-005 [AC-003] [FR-005] [DEC-002] complete: Persist all 36 reviewed baseline run IDs. Before mutation require the live complete set to equal them; while disabled and again after verified re-enable, reject every added or removed ID and poll to at least 60 seconds/three stable samples with a 180-second bound. Contain backoff failures inside unconditional cleanup until active-state verification succeeds or all attempts exhaust.
- PD-006 [AC-003] [FR-006] [DEC-003] complete: Re-download and compare package indexes and unsigned payload digests after delivery; never invoke pack, publish, release creation, or version editing. Track one live 6-gate verifier report, 21 production-shaped subject-mutation controls, and 16 runner cleanup/convergence controls.

## Contract Impact
- PC-001 [PD-003] [PD-004] release metadata: add only `refs/tags/v1.12.1` as a source anchor for two already-published 1.12.1 tool packages; no package or executable contract changes.

## Verification Obligations
- VO-001 [PD-001] [PD-002] [PD-003] [PD-004] [PD-005] [PD-006] [PC-001] releaseAnchor: Preserve raw, machine-readable pre-delivery evidence for package metadata, unsigned entry digests, remote tags, commit object, workflow trigger/state, and every paginated run ID. Prove all six verifier gates reject independent mutations/unreadable or non-vacuous controls, and inject failure after every post-disable runner step while proving active-state recovery and primary-error preservation.

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
