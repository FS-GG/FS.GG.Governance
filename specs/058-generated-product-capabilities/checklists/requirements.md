# Specification Quality Checklist: Generated-Product Capabilities

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-06-25
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
- **Scope boundary is the key clarification**: F23 (this feature) makes generated-product surfaces
  *declarable, routable, classifiable, cost-tiered*; the per-domain deterministic **checks themselves** are
  F24 (`024-package-docs-skills-design-checks`) and explicitly out of scope (FR-016, Assumptions). The
  requester confirmed F23 over F24 at specification time.
- **Schema evolution in scope**: unlike recent rows, this feature *expands* the F014 `.fsgg/capabilities.yml`
  schema with a new version + migration (Assumptions; FR-011–FR-013). Bounded to `capabilities.yml`; the
  other three `.fsgg` files are untouched.
- A handful of domain terms appear (capability catalog, surface classification, cost tier, evidence tag,
  governed root, profile) — these are governance **domain vocabulary** carried verbatim from the capability
  design and existing F014/F016 features, not implementation details. No project names, languages, record
  field names, or APIs leak into the spec; those are deferred to `/speckit-plan`.
