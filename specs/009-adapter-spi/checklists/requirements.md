# Specification Quality Checklist: The Adapter SPI & Composition Root

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-06-18
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
- **Domain-vocabulary caveat (intentional):** the spec names kernel surface
  already shipped and merged (F01–F07) — `ArtifactRef`, `Check`, `CheckTier`,
  `Severity`, `CheckRule<'fact>`, `Rule<'fact>`/`FactSet<'fact>`,
  `FixedPoint.evaluate`, `Implies`, `Opaque`, `RuleOutcome`, the cache key, and
  the candidate lifting name `Rule.contramapFacts`. These are the **existing
  domain nouns this feature composes**, not new implementation choices; naming
  them keeps the dependency boundary precise (which kernel facilities an adapter
  reuses vs. supplies). The actual new shapes — the adapter record/interface, the
  `ProjectFact` coproduct, the lift combinator, and the precedence function — are
  deferred to the plan and the curated `.fsi`, as the Assumptions state. This
  mirrors the depth of the merged F08 (`008-effects-interpreter`) spec.
- **Two clarification candidates were resolved by reasonable default** rather than
  raised as markers: (1) where the lift combinator lives (kernel `Rule` module vs.
  the new SPI component) — left to the plan/`.fsi` per the constitution's
  FSI-first principle; (2) the precise default cross-domain precedence — fixed to
  the already-established Cedar model the kernel's route/merge use (blocking wins;
  default allow-unless-fenced), so no new decision is opened here.
</content>
