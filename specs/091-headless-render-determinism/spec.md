# Feature Specification: Headless Render-Width Test Determinism

**Feature Branch**: `091-headless-render-determinism`

**Created**: 2026-06-29

**Status**: Draft

**Input**: User description: "start the next governance item on the coordination board." → resolved to **FS-GG/FS.GG.Governance#32** (Coordination board, P3 Governance): *Cli.Tests — make Spectre WidthResilience tests deterministic in headless CI, then drop the temporary publish-gate exclusion.*

## Context

`tests/FS.GG.Governance.Cli.Tests/WidthResilienceTests.fs` asserts that the rich-render surface (`RichRender.emit`, backed by Spectre.Console) emits lines that fit a forced console width across a matrix of widths (200, 80, 40, 20, 10). These pass on a local developer host (Release, headless, invariant-globalization, `C` locale all verified green) but **fail in GitHub Actions**: the headless CI renderer infers different terminal capabilities and wraps unbreakable tokens (e.g. `src/**`-style path globs ~17–21 chars) differently, so a line overflows the forced width and the `line.Length <= width` assertion fails.

When spec 089's `publish.yml` `cli-tests` job became the first CI to actually run `Cli.Tests` (the `gate.yml` workflow only builds), this gap blocked the publish. As a temporary unblock, `WidthResilience` is **excluded** from the publish gate via `dotnet test ... --filter "FullyQualifiedName!~WidthResilience"`. Every other test in the suite — including the other Spectre tests (RichRender / DegradeToPlain / Tui) — still gates the publish and passes in CI. The exclusion is a coverage hole tracked by issue #32.

This is a **presentation/test-determinism gap**, orthogonal to the published artifact's handoff-enforcement contract (guarded separately by the `enforcement-smoke` job).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The publish gate covers width resilience again (Priority: P1)

A maintainer cuts a release of `FS.GG.Governance.Cli`. The pre-publish `cli-tests` gate runs the **entire** `Cli.Tests` suite — including the rich-render width-resilience tests — with no per-test exclusion. A regression that breaks rich-render reflow at narrow widths blocks the publish instead of shipping silently.

**Why this priority**: Restoring full gate coverage is the whole point of the item. While the exclusion stands, any rich-render width regression can reach the published tool undetected; closing that hole is the deliverable.

**Independent Test**: Inspect the publish workflow's test invocation — it runs the suite without a `FullyQualifiedName!~WidthResilience` (or equivalent) filter — and confirm a deliberately-broken width assertion fails the gate.

**Acceptance Scenarios**:

1. **Given** the publish `cli-tests` gate, **When** it runs the `Cli.Tests` project, **Then** the WidthResilience tests are included (no exclusion filter scopes them out).
2. **Given** a hypothetical regression that makes rich render overflow a forced width, **When** the publish gate runs, **Then** the gate goes red and the publish does not proceed.

---

### User Story 2 - Width tests pass identically on every host (Priority: P1)

A contributor runs the width-resilience tests on their local machine and the same tests run in headless GitHub Actions. Both produce the **same** pass/fail outcome for the same commit — no host-conditional skips, no "green locally, red in CI."

**Why this priority**: Host-dependent test outcomes are what created the exclusion in the first place. Determinism across hosts is the precondition for re-enabling the gate (US1); the two ship together.

**Independent Test**: Run the WidthResilience tests locally and in headless CI on the same commit; assert identical results across the full width matrix.

**Acceptance Scenarios**:

1. **Given** the WidthResilience width matrix, **When** the tests run in headless GitHub Actions, **Then** every case passes — matching the local result.
2. **Given** the same tests, **When** run locally under a normal developer terminal, **Then** the results are unchanged from before the fix (no local regression).
3. **Given** the rendering capabilities that influence line wrapping (Unicode, ANSI, color system, encoding, legacy-console), **When** a test console is built, **Then** those capabilities are pinned to fixed values so wrapping does not depend on the inferred host environment.

---

### User Story 3 - Assertions reflect the real wrapping contract (Priority: P2)

The width assertions encode what the renderer actually guarantees, so the suite is honest and stable: at a forced width narrower than an unbreakable token, the line is allowed to extend to that token's boundary (the renderer folds on word/segment boundaries, not mid-token). The test does not assert an invariant stricter than the renderer provides.

**Why this priority**: A test that asserts a guarantee the renderer never made is fragile by construction — it will keep flaking on the next host or token. Aligning the assertion with the real folding contract is what makes determinism durable rather than a host-specific patch. Lower than P1 only because pinned capabilities alone may already make CI green; this hardens it against future drift.

**Independent Test**: Feed content containing an unbreakable token longer than the forced width and confirm the asserted invariant is the documented folding behavior (no spurious failure, no silently weakened check).

**Acceptance Scenarios**:

1. **Given** content whose smallest unbreakable token exceeds the forced width (e.g. a 17-char glob at width 10), **When** rich render emits, **Then** the assertion accepts a line up to the token boundary and still rejects genuinely runaway/corrupted layout.
2. **Given** content that fits within the forced width, **When** rich render emits, **Then** every line is asserted to fit the width exactly as today.

---

### Edge Cases

- **Width smaller than the smallest unbreakable token** (width 10 vs a ~17-char path glob): the asserted invariant must distinguish "folded to a token boundary" (acceptable) from "layout corrupted / runaway overflow" (failure). This is the exact case that overflows in CI today.
- **Unknown / unset width**: the safe default width is used and is a sane positive value (existing assertion preserved).
- **Color (ANSI) console path**: ANSI escape sequences must not be counted toward visible line width when an assertion is about visible columns; the width matrix uses the ANSI-free console, which must remain ANSI-free deterministically.
- **Locale / globalization differences** (invariant vs host locale): rendering width must not vary by locale.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The rich-render width-resilience tests MUST produce identical pass/fail outcomes regardless of host environment (local developer terminal, headless CI runner, invariant or host locale).
- **FR-002**: The test console used by the width-resilience suite MUST pin every rendering capability that can affect line wrapping (at minimum: ANSI support, color system, output encoding, Unicode/legacy-console behavior, and forced width) to fixed values, so wrapping does not depend on environment-inferred capabilities.
- **FR-003**: The width assertions MUST encode the renderer's real folding contract — at a forced width narrower than an unbreakable token, a line MAY extend to that token's boundary — and MUST NOT assert a stricter invariant than the renderer guarantees, while still failing on genuinely corrupted or runaway layout.
- **FR-004**: The width matrix MUST continue to exercise narrow widths (including the 10 and 20 cases that fail today) — the fix MUST NOT achieve "green" by removing or skipping the hard cases.
- **FR-005**: Once the tests are deterministic, the temporary per-test exclusion (the `FullyQualifiedName!~WidthResilience` filter) MUST be removed from the publish `cli-tests` gate so the full `Cli.Tests` suite — including WidthResilience — gates every publish.
- **FR-006**: The change MUST be confined to test code and the publish workflow's test invocation; the published tool's enforcement behavior (route/ship/verify verdicts) MUST be unaffected. No new published version is required.
- **FR-007**: The fix MUST NOT weaken any unrelated assertion in the suite, and the other Spectre tests (RichRender / DegradeToPlain / Tui) MUST continue to pass in CI.
- **FR-008**: On completion, issue #32 MUST be closed and its Coordination board item moved to **Done**, with the resolution noting the determinism approach taken.

### Key Entities

- **Width-resilience test matrix**: the set of forced console widths (200, 80, 40, 20, 10) the rich-render surface is exercised against, plus the safe-default-width case.
- **Test console capabilities**: the rendering settings (ANSI, color system, encoding, Unicode/legacy-console, forced width) whose host-dependence is the root cause; pinning them is the determinism lever.
- **Publish `cli-tests` gate**: the pre-publish CI job whose test invocation currently carries the temporary exclusion filter.
- **Folding contract**: the renderer's documented behavior of breaking lines on token/segment boundaries rather than mid-token — the invariant the assertions must reflect.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The WidthResilience tests pass in headless GitHub Actions — 0 failures across the full width matrix — in the environment where they fail today.
- **SC-002**: The same tests yield identical results when run locally, with no host-conditional branching, skipping, or `[<Ignore>]` to mask the hard cases.
- **SC-003**: The publish `cli-tests` job runs the `Cli.Tests` project with **no** WidthResilience exclusion filter; the full suite gates the publish.
- **SC-004**: Coverage is not reduced — the matrix still includes the narrow widths (10 and 20) and asserts a meaningful, non-trivial layout invariant (not merely "output is non-empty").
- **SC-005**: A deliberately introduced rich-render width regression causes the publish gate to fail (the gate demonstrably protects the behavior again).
- **SC-006**: Issue #32 is closed and its Coordination board item shows **Done**.

## Assumptions

- **Root cause is environment-inferred capabilities, not renderer non-determinism**: Spectre.Console wraps deterministically once its profile capabilities (ANSI, color, encoding, Unicode/legacy-console, width) are explicitly pinned; the observed CI/local divergence comes from differing inferred capabilities, not from internal randomness.
- **Both levers may be needed**: the issue proposes pinning capabilities *and/or* asserting the realistic folding invariant. The default approach applies **both** — pin capabilities for cross-host determinism, and align the assertion with the real folding contract so the suite is durable against future host/token drift. If pinning alone makes the matrix green at all widths, the assertion-realism change still stands as hardening (US3), not as a way to relax coverage.
- **Presentation-only scope**: this is a TTY-presentation/test-determinism fix. It does not touch the route/ship/verify enforcement contract, the consumed handoff, the config schemas, or any cross-repo versioned contract, and therefore requires no new published artifact version and no registry/contract change. (Change classification: **Tier 2 — internal change**, resolved in planning. The constitution defines only Tier 1/Tier 2; "internal test + CI only" maps to Tier 2. See plan.md → Constitution Check.)
- **Workflow edit is limited to the test invocation**: dropping the exclusion changes only the `cli-tests` job's `dotnet test` filter; the locked-restore step, the `enforcement-smoke` job, and the publish sequencing are unchanged.
- **The other Cli.Tests already pass in CI**: only WidthResilience is excluded today; re-including it is the sole coverage delta.
- **Board/issue housekeeping is in scope**: closing #32 and moving its board item to Done is part of "done," consistent with how prior governance items (e.g. #34/090) tracked closure.
