# ADR 0007 — Per-projection JSON writer duplication is intentional; byte-identity is guarded by tests, not by extraction

**Status**: Accepted · **Date**: 2026-07-03 · **Feature**: `specs/107-contract-hygiene`

**Resolves**: review finding **M-JSON-2** (issue FS-GG/FS.GG.Governance#52, Epic #44) — "~105-line verbatim release-readiness writer duplication (`ReleaseJson.fs` ≡ `VerifyJson/ReleaseReadiness.fs`) plus three more byte-identical writer pairs meet the 073 `JsonWriters` extraction bar; `AuditJson.ofShipDecision` should delegate to `ofShipDecisionWithGeneratedViews`."

## Context

The 2026-07-02 review flagged several `Utf8JsonWriter`-walking helpers that write structurally similar JSON across projections, and proposed lifting them into the shared `FS.GG.Governance.JsonWriters` module (the "073 extraction bar") to remove duplication.

Since the review, the surrounding code was reorganized by the 073 (`JsonTokens`/`JsonWriters` extraction) and 076 (release-readiness Phase C seam) work, and the situation is now:

| Cited duplication | Current shape |
|---|---|
| `release.json` writers — `ReleaseJson.fs` (`writeRule`, `writeVersion`, `writeMetadata`, `writePins`, `writePosture`, `writeEvidence`, …) | Serve `release.json` (`fsgg.release/v2`). Own their token helpers. |
| release-readiness writers — `VerifyJson/ReleaseReadiness.fs` (`rr*`, `writePackProject`, `writePackageEvidence`, `writeVersionPolicy`, `writeAttestationRef`) | Serve the additive `releaseReadiness` preview block embedded in `verify.json` (`fsgg.verify/v1`). Lifted verbatim into their own module under the 076 Phase C seam; header explicitly records the convention. |
| `AuditJson.ofShipDecision` vs `ofShipDecisionWithGeneratedViews` | The genuinely shared sub-steps (`verdictByGate`, `outcomeByGate`, `writeExecution`, `writeCause`) **are** already extracted into `JsonWriters`. The two entry points share those, and `ofShipDecisionWithGeneratedViews` adds only the `generatedViews` array. |

The attestation-token half of the review's JSON findings (M-JSON-1: single-source `schemaVersion`/`complianceToken`) and the environment-token divergence (M-JSON-3) were resolved in commit `c1ee7f9`; the Route/CacheEligibility stdout contract (M-CLI-5) and the exit-code documentation (M-CLI-6) in commit `6b53332`. This ADR closes the one remaining M-JSON-2 question: extract the release-side writers too, or ratify the duplication.

## Decision

**Do not extract the per-projection release writers. Ratify the existing convention — each JSON projection owns its writers and token helpers — and rely on the existing byte-identity/element tests as the guardrail.**

The writers look similar but are **bound to independent, separately-versioned wire contracts**:

- `release.json` is `fsgg.release/v2`; the `releaseReadiness` block rides inside `verify.json` at `fsgg.verify/v1`; the attestation reference is `fsgg.attestation/v1`. These schema versions evolve on **independent lifecycles**. A shared writer would couple them: a field added to `release.json` v3 would silently reshape the `verify.json` v1 preview (or force an awkward parameterization to keep them apart), which is exactly the coupling the review's *other* findings (M-JSON-3, the format-flag vocabularies of ADR 0006) warn against.
- This is a stated codebase convention, not accidental drift. `ReleaseReadiness.fs` records it at the use site: *"the cores own their own writers — the codebase convention each projection duplicates its token helpers."* The correct guardrail for "these must stay in lockstep **where they overlap**" is a byte-identity/element-identity test, and those exist (e.g. `VerifyJson.Tests/ReleaseReadinessPreviewTests.fs`, `VerifyCommand.Tests/ReleasePreviewTests.fs` T031/T032, `AuditJson.Tests`).
- The *genuinely* shared, version-agnostic sub-steps were already extracted where extraction does not couple contracts: `JsonWriters.{writeCause, writeExecution, verdictByGate, outcomeByGate}`. That is the right boundary — mechanics shared, contract-shaped writers kept per projection.
- `AuditJson.ofShipDecision` deliberately does **not** delegate to `ofShipDecisionWithGeneratedViews` (FR-010): the base entry point is frozen and the overload is additive, their equality-when-empty asserted by test (FR-004) rather than by call-through. Collapsing them would make the frozen base surface depend on the newer overload's signature — a regression in contract stability for no byte change.

This mirrors ADR 0006's reasoning for the format-flag vocabularies: a *published, independently-versioned contract surface* is not "duplication to be DRY'd"; convergence there trades a real contract property (independent evolution) for a cosmetic one (fewer lines).

## Consequences

- The release-side writers stay per projection. Contributors should expect each JSON projection (`ReleaseJson`, `ReleaseReadiness`, `AuditJson`, the core writers) to own its writers and token helpers, and should **not** unify them without first breaking the independent-schema-version property.
- The guardrail is the existing byte-identity/element-identity test suite, not a shared module. Those tests are load-bearing: they are what makes the duplication safe. Do not weaken them.
- `JsonWriters` remains the home only for **contract-agnostic** mechanics (gate lookups, cause/execution fragments). New helpers join it only when they are not tied to a single projection's schema version.
- If a future major (SemVer) revision deliberately unifies the release/verify wire schemas onto one version, this ADR is the place to revisit the extraction; until then it is explicitly declined as a coupling not worth its cost.
- No code change accompanies this decision. M-JSON-2 is resolved as **ratified convention**; #52's four other acceptance criteria were already satisfied on `main` (M-JSON-1/3 in `c1ee7f9`, M-CLI-5/6 in `6b53332`).
