# Specification Quality Checklist: CLI production-correctness edges

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-07-03
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details leak into requirements (requirements stated as outcomes; code sites cited only as evidence)
- [x] Focused on user value (no wrong cached verdicts; no shipped backdoor; accurate help/errors)
- [x] Written for a repo-internal governance-tooling audience
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified (cache-format change, illegal-char key, fixture removal)
- [x] Scope is clearly bounded (four enumerated edges; M-CLI-2 + F10/F12/F14)
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] Each functional requirement has clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes in Success Criteria
- [x] Change classification declared (mixed; the additive `ParseError` case is the only public delta)

## Notes

- Implemented directly on `108-cli-correctness-edges` (fixes are localized, well-understood correctness bugs).
- **Discovery during grounding**: the two fixture directories were dead (no test referenced them), so US2 collapsed to deleting the backdoor + orphaned fixtures rather than a seam migration. The now-vestigial `snapshot` parameter on `ReviewStore.loadReview`/`saveReview` (its only consumer was the backdoor) was dropped — Cli is `PackAsTool`, callers are internal, surface baseline is name-level, so this is safe.
- RED→GREEN: the collision test fails against pre-fix code (both keys share one file; the second save overwrites the first, so the first key loads the wrong verdict). GREEN after the hash-suffixed filename + stored-key verification.
