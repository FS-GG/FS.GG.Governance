# Specification Quality Checklist: Judge-vs-Human Calibration Evidence — the Beyond-Advisory-Maturity Gate

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-06-22
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

- Items marked incomplete require spec updates before `/speckit-clarify` or `/speckit-plan`.
- Deliberate deferrals to `/speckit-plan` (documented in Assumptions, not left as ambiguities): the concrete module
  home/name; which existing types are reused (F035 judge identity, F038 `RecordedVerdict`); the exact shapes of the
  comparison-sample / evidence / threshold inputs; the representation of the agreement level; whether a fixed
  minimum-sample floor is enforced; and whether a recency/staleness requirement is modelled. The *contracts* —
  uncalibrated-by-default, judge-vs-human (not self-assessment), per-judge-identity scope, the no-single-sample floor,
  the inclusive `>=` comparators, totality, determinism/purity, no-hide attribution, necessary-not-sufficient, and the
  Tier-1 additive surface — are fixed in the spec.
