---
schemaVersion: 1
workId: 366-fsharp-surface-governance
title: "General F# gate for curated signature surfaces, explicit visibility, and API documentation"
stage: clarify
changeTier: tier1
status: clarified
sourceSpec: work/366-fsharp-surface-governance/spec.md
publicOrToolFacingImpact: true
---

# General F# gate for curated signature surfaces, explicit visibility, and API documentation Clarifications

## Source Specification
- work/366-fsharp-surface-governance/spec.md

## Clarification Questions
- CQ-001 [AMB:AMB-001] blocking open: Resolve source ambiguity AMB-001 before checklist.
- CQ-002 [AMB:AMB-002] blocking open: Resolve source ambiguity AMB-002 before checklist.

## Answers
- CQ-001 [AMB:AMB-001] decision: Model exemptions as typed policy records carrying module/path, owner, rationale, and expiry or review date; malformed or expired exemptions do not waive a finding.
- CQ-002 [AMB:AMB-002] decision: Extend the existing ProjectSensing, DesignChecks, Findings, and BuiltIn adapter seams, reusing package baseline sensing rather than duplicating it.

## Decisions
- DEC-001 [CQ-001] [AMB:AMB-001]: Model exemptions as typed policy records carrying module/path, owner, rationale, and expiry or review date; malformed or expired exemptions do not waive a finding.
- DEC-002 [CQ-002] [AMB:AMB-002]: Extend the existing ProjectSensing, DesignChecks, Findings, and BuiltIn adapter seams, reusing package baseline sensing rather than duplicating it.

## Accepted Deferrals
No accepted deferrals recorded.

## Remaining Ambiguity
No blocking ambiguity remains.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd checklist --work 366-fsharp-surface-governance`.
