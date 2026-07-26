# Feature Specification: Production-Journey Governance Floor

**Feature Branch**: `item/324-production-journey-gate`

**Created**: 2026-07-26

**Status**: Complete

**Input**: FS-GG/FS.GG.Governance#324

## User Scenarios & Testing

### User Story 1 - Fail closed on unmet production journeys (Priority: P1)

A game product that declares a production-journey obligation cannot ship unless the compatible
SDD handoff reports every such obligation satisfied.

**Independent Test**: Parse producer-shaped handoffs and prove zero unmet journeys is advisory while
any non-zero, malformed, contradictory, or required-but-absent journey fact blocks consumption.

### User Story 2 - Inherit a non-lowerable organization floor (Priority: P1)

Every product bound to the `game` template profile inherits `gameplay:production-journey` at
`block-on-ship`; local configuration may strengthen but cannot lower or remove it.

**Independent Test**: Compose inherited and local gates at every maturity and assert the effective
gate remains at least `block-on-ship`.

### User Story 3 - Preserve actionable producer evidence (Priority: P2)

Operators can see the exact unmet count, producer diagnostic ids, provenance disposition, and
affected requirement/scenario ids carried by the handoff.

**Independent Test**: Consume a producer-shaped invalid-receipt handoff and assert the resulting
gate description names the count, disposition, diagnostic, and related ids.

## Requirements

- **FR-001**: Add `gameplay:production-journey` to the inherited `game` profile at
  `block-on-ship`, without changing non-game profiles or `gameplay:fr-covered`.
- **FR-002**: Preserve journey unmet counts and diagnostic/related ids as typed facts.
- **FR-003**: Accept zero unmet journeys whether no journey was declared or all were satisfied.
- **FR-004**: Make every non-zero journey unmet count a blocking, actionable gate.
- **FR-005**: Reject negative counts, a ship-ready disposition paired with unmet journeys, and zero
  unmet paired with a canonical journey-receipt failure diagnostic.
- **FR-006**: During the compatibility window, accept older producer handoffs without journey
  fields; require the field for the published SDD 0.30.x producer line and later compatible lines.
- **FR-007**: Ignore unknown additive fields while rejecting unsupported contract majors.
- **FR-008**: Consume only the published handoff/`FS.GG.Contracts` boundary; add no SDD or Game
  project/assembly reference.
- **FR-009**: Add the gate to the reference gate-set package and coherently bump its content version.
- **FR-010**: Prove the producer-generated SDD 0.30 handoff golden passes, and a Rogue-shaped
  helper-only handoff blocks. The published aggregate intentionally does not distinguish “none
  declared” from “all satisfied”; both are the producer-owned zero-unmet verdict.
- **FR-011**: Classify provenance only from SDD's canonical production-journey diagnostic ids;
  unrelated readiness diagnostics and their related ids must not contaminate the journey fact.

## Success Criteria

- **SC-001**: All adapter, inheritance, and reference-gate tests pass.
- **SC-002**: A local `observe`/`warn` declaration cannot lower the inherited journey floor.
- **SC-003**: Malformed or contradictory required journey facts never become zero by default.
- **SC-004**: The adapter assembly references neither FS.GG.SDD nor FS.GG.Game.
- **SC-005**: The reference gate-set package contains both gameplay gates at the required maturity.

## Change Classification

Tier 1: additive public typed facts and a new inherited organization-owned ship gate. Existing
ordinary gameplay coverage remains unchanged.

## Assumptions

- SDD 0.30.x is the first producer line that promises
  `readiness.counts.journeyObligationsUnmet`.
- Receipt validation remains SDD-owned. Governance enforces the versioned result and preserves its
  diagnostic provenance rather than re-reading Game receipts.
