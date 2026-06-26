# Contract — Mergeable-but-not-Releasable, with Named Preconditions (US2)

**Closes** `065` T023 · **Covers** FR-003, FR-004, FR-008, SC-002.

A **test contract** proving the release boundary is genuinely distinct from the ship/merge boundary, and
that the publication preconditions are reported first-class. No product code changes.

## Fixture pair

| State | Description |
|-------|-------------|
| Mergeable-but-not-releasable | `fsgg ship` gates pass, but one publication precondition (publish plan, trusted-publishing posture, or template pin) is unmet. |
| Fully-releasable | ship passes and all three publication preconditions are satisfied. |

## Required behaviour (asserted)

1. **Boundary distinction (FR-003, SC-002).** For the mergeable-but-not-releasable product:
   - `fsgg ship` ⇒ exit `0`.
   - `fsgg release` ⇒ exit `1`, with a release exit-code basis **distinct** from the ship verdict.
2. **Named preconditions, unmet (FR-004).** Inspecting `release.json` v2 for that product, the publish
   plan, trusted-publishing posture, and template pins each appear as named `PreconditionEvidence`
   entries; the failing one is in an **unmet** state carrying a named reason (not a bare verdict).
3. **Named preconditions, satisfied (FR-004).** For the fully-releasable product, `fsgg release` ⇒ exit
   `0` clean, and the same three preconditions appear in a **satisfied** state in `release.json` v2.

## Surface consumed (verbatim)

- `ReleaseReport.PreconditionEvidence` / `Preconditions` (the existing first-class projection).
- The existing `release.json` v2 named entries (`publishPlan` / `trustedPublishing` / `pins`).
- The existing ship/release exit-code schemes — no new code, verdict, or exit basis is introduced
  (FR-007).

## Anti-requirements

- MUST NOT add a new precondition type, verdict, or exit code — assert the **states** of the existing
  ones only.
- MUST NOT fake the ship producer — run the real `fsgg ship` host for the ship outcome.
