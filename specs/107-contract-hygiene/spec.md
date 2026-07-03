# Feature Specification: Contract hygiene — reconcile & close #52

**Feature Branch**: `107-contract-hygiene`

**Created**: 2026-07-03

**Status**: Draft (reconciliation)

**Input**: Governance issue #52 (Epic #44) — review findings M-JSON-1/2/3, M-CLI-5/6. Close the JSON-is-contract and token-divergence gaps from the 2026-07-02 review.

## Context (why this spec is a reconciliation)

A grounded audit of `main` at the start of this feature found that **four of #52's five acceptance criteria were already satisfied** by work that merged after the review but never ticked the issue checkboxes:

- **M-JSON-1** (attestation `schemaVersion`/`complianceToken` exposed via `.fsi`, referenced from both embed sites) — done in `c1ee7f9`. Evidence: `AttestationJson.fsi:16,21`; `ReleaseJson.fs:286,288`; `VerifyJson/ReleaseReadiness.fs:136,138`.
- **M-JSON-3** (divergence comments on both environment-token sites) — done in `c1ee7f9`. Evidence: `JsonTokens.fs:40-43` and `EvidenceReuseStore.fs:34-35`.
- **M-CLI-5** (Route/CacheEligibility `--json` stdout emits/stamps the artifact) — done in `6b53332`. Route emits `route.json` byte-for-byte (`RouteCommand/Loop.fs:617`); CacheEligibility is stamped `fsgg.cache-eligibility-summary/v1` and documented (`CacheEligibilityCommand/Loop.fs:471-508`).
- **M-CLI-6** (exit-code families documented) — done in `6b53332`. Evidence: `README.md §Exit codes`, both the verb-host `0–4` family and the `fsgg-governance` sysexits, including the store/freshness read-failure policy.

The single open criterion is **M-JSON-2** (release-readiness JSON writer duplication). This feature resolves M-JSON-2 and closes #52; it does not re-open the four settled criteria.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The writer-duplication finding is resolved with a durable decision (Priority: P1)

A maintainer revisiting #52's M-JSON-2 needs a single, recorded answer to "extract the release-side JSON writers into `JsonWriters`, or keep them per projection?" — so the question is not re-litigated on every future JSON change.

**Why this priority**: It is the one unresolved acceptance criterion; without it #52 cannot close.

**Independent Test**: Read `docs/decisions/0008-json-writer-duplication.md`; confirm it names M-JSON-2, states a decision, and gives consequences a future contributor can act on. Confirm the byte-identity/element tests it relies on exist and pass.

**Acceptance Scenarios**:

1. **Given** the M-JSON-2 finding, **When** a maintainer reads ADR 0008, **Then** they find an Accepted decision (ratify per-projection duplication; guard by tests) with rationale tied to independent schema-version lifecycles.
2. **Given** the ratification, **When** the guardrail tests run (`VerifyJson.Tests/ReleaseReadinessPreviewTests`, `VerifyCommand.Tests/ReleasePreviewTests` T031/T032, `AuditJson.Tests`), **Then** they pass — the duplication is proven equivalent where it overlaps.

### User Story 2 - #52's status reflects reality (Priority: P2)

A reader of issue #52 or Epic #44 needs the checklist to reflect what is actually on `main`, with commit evidence.

**Why this priority**: Board accuracy; prevents redundant re-work of already-shipped criteria.

**Independent Test**: The issue's four settled criteria are ticked with the commit that satisfied each; M-JSON-2 points to ADR 0008.

**Acceptance Scenarios**:

1. **Given** the audit evidence, **When** #52 is reconciled, **Then** M-JSON-1/3 (`c1ee7f9`), M-CLI-5/6 (`6b53332`) are ticked and M-JSON-2 is marked resolved-by-ADR-0008.

### Edge Cases

- If a reviewer disagrees with ratification and wants extraction (option A), ADR 0008 records the extraction path as the explicit "future major (SemVer)" revisit point — the decision is reversible without losing the rationale.
- If a guardrail test is later weakened, the duplication is no longer safe; ADR 0008 flags those tests as load-bearing.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The repository MUST carry an Accepted ADR resolving M-JSON-2, naming the finding, the decision, and its consequences.
- **FR-002**: The ADR MUST justify the decision in terms of the JSON wire contracts' independent schema-version lifecycles (`fsgg.release/v2`, `fsgg.verify/v1`, `fsgg.attestation/v1`).
- **FR-003**: The ADR MUST identify the existing byte-identity/element-identity tests as the guardrail that makes the retained duplication safe.
- **FR-004**: Issue #52's four already-satisfied acceptance criteria MUST be ticked with commit evidence; M-JSON-2 MUST reference the ADR.
- **FR-005**: No production `.fs`/`.fsi` behavior change is introduced (Tier 2 / docs-only); the surface baselines and full suite remain green.

### Key Entities

- **ADR 0008**: The decision record resolving M-JSON-2 (per-projection writer duplication is intentional; guarded by tests).
- **JSON projections**: `ReleaseJson` (`fsgg.release/v2`), `ReleaseReadiness` preview (`fsgg.verify/v1`), `AuditJson` (ship/audit), and the contract-agnostic `JsonWriters` mechanics — the entities whose duplication boundary the ADR fixes.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of #52's acceptance criteria are either ticked-with-evidence (4) or resolved-by-ADR (1); #52 is closable.
- **SC-002**: A maintainer can determine, from ADR 0008 alone, why the release-side writers are not extracted — without reading the code diff or the review report.
- **SC-003**: The full test suite and API-compat/surface-drift gates remain green (no source behavior change).

## Assumptions

- **Change classification is Tier 2 / docs-only.** The four settled criteria already shipped; this feature adds one ADR and reconciles the issue. No `.fsi` or baseline change.
- **The per-projection duplication convention (documented at `ReleaseReadiness.fs:22-23`) is the intended state**, and the existing byte-identity tests are its guardrail — this feature ratifies rather than reverses it.
- **The extraction path (option A) is deliberately declined, not overlooked**; ADR 0008 records it as the future-major revisit point, so the decision is reversible.
