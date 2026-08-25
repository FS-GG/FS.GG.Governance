---
schemaVersion: 1
workId: 418-governance-release-anchor-repair
title: Governance v1.12.1 release anchor repair
stage: clarify
changeTier: tier1
status: clarified
sourceSpec: work/418-governance-release-anchor-repair/spec.md
publicOrToolFacingImpact: true
---

# Governance v1.12.1 release anchor repair Clarifications

## Source Specification
- work/418-governance-release-anchor-repair/spec.md

## Clarification Questions
- **CQ-001**: Can a direct `v1.12.1` ref creation satisfy the no-publication-run requirement?
- **CQ-002**: What is the safe failure-atomic operational sequence?
- **CQ-003**: Does the product source or publisher need to change?

## Answers
- CQ-001 → no. The current workflow subscribes to every `v*` push, and the workflow definition is read from the historical tagged commit. Duplicate skipping prevents replacement, but it does not prevent a workflow run, package build, authentication, or publish attempts.
- CQ-002 → capture workflow state and a complete run census; disable `publish.yml`; create only the exact tag; verify the ref and absence of a new run; restore the prior workflow state in an unconditional cleanup path; then re-census runs and packages.
- CQ-003 → no. This is a metadata-only delivery obligation. The branch contains only SDD/readiness evidence for independent review; delivery creates no product-source commit, workflow edit, package, release, or version change.

## Decisions
- **DEC-001** [CQ-001] [FR-004] [FR-005]: A direct tag push while `publish.yml` is active is prohibited even though package pushes are duplicate-safe.
- **DEC-002** [CQ-002] [FR-003] [FR-004] [FR-005]: Use temporary Actions workflow disablement as the non-publishing fence, and make re-enablement an unconditional cleanup obligation.
- **DEC-003** [CQ-003] [FR-006]: Keep implementation tag-only; commit and review only the SDD/readiness proof and typed delivery recipe.

## Accepted Deferrals
No accepted deferrals recorded.

## Remaining Ambiguity
No blocking ambiguity remains.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd checklist --work 418-governance-release-anchor-repair`.
