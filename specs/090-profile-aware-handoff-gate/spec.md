# Feature Specification: Profile-Aware Handoff-Gate Enforcement

**Feature Branch**: `090-profile-aware-handoff-gate`

**Created**: 2026-06-29

**Status**: Draft

**Input**: User description: "start the next governance item on the coordination board." → Coordination board item **FS-GG/FS.GG.Governance#34** (Status: *Ready*, Phase: *P3 Governance*, Workstream: *Governance*) — the direct follow-on the 089 publish deferred: *"route: make handoff-gate enforcement profile-aware (light profile should relax the gate boundary)."* Unblocks the final red cell of **FS-GG/FS.GG.Templates#25**. Contract: `governance-handoff` (consumer-side; no surface change).

## Why This Feature

Spec 089 published `FS.GG.Governance.Cli@1.1.0` — the **strict-only baseline**. It wired the SDD handoff into the `route --mode gate` exit (a produced `governance-handoff.json` now drives the verdict: a failing handoff blocks with exit 2, a satisfied one passes with exit 0). But 1.1.0 reaches that verdict through a `mode = Gate` shortcut that blocks on a failing handoff **regardless of the policy profile** — it is profile-unaware.

The downstream acceptance signal is the FS.GG.Templates#25 composition probe (`tests/composition/run.sh`, Stage 6b). It holds `route --mode gate` constant and varies the **policy profile** (`.fsgg/policy.yml defaultProfile: strict → light`), expecting the profile to move the blocking boundary. Run against the published 1.1.0, that probe reports **30 passed, 1 failed**:

- ✅ strict + failing handoff → blocked (exit 2)
- ✅ strict + satisfied handoff → clean (exit 0)
- ❌ **light + failing handoff → expected exit 0, got exit 2** — 1.1.0 blocks anyway because it never consults the profile.

So the enforcement loop is shipped and reachable (089's win), but the *policy profile is inert at the gate*: a product that has deliberately relaxed its profile to `light` is still hard-blocked on a failing handoff, and the one matrix cell that proves the profile actually governs the boundary stays red. The cross-repo "profile shifts the gate" contract that Templates#25 encodes is therefore unmet.

This feature makes handoff-gate blocking **profile-aware** by routing it through the canonical Phase-5 enforcement core (the same effective-severity derivation every other gate already uses) instead of the mode-only shortcut. The policy profile is read at the product's edge and carried into the verdict; strict tightens the boundary so a failing handoff blocks, while light relaxes it so the same handoff is advisory. Because `1.1.0` is immutable on the feed, the new observable behavior ships as a fresh version. When a downstream product installs it, the Templates#25 matrix flips fully green (strict blocks / strict-satisfied passes / light does not block), the publish enforcement-smoke still blocks on its profile-less fixtures, and issue #34 resolves.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The policy profile shifts the gate's blocking boundary (Priority: P1)

A downstream product runs `fsgg-governance route --mode gate` against a build whose SDD step emitted a **failing** `governance-handoff.json`. With the product's policy profile set to **strict**, the gate **blocks** (exit 2). With the profile set to **light** — same handoff, same mode — the gate **does not block** (exit 0): the failing handoff is surfaced as advisory rather than fatal. A **satisfied** handoff passes under either profile.

**Why this priority**: This is the entire item. The 089 baseline already blocks-or-passes on the handoff; the only thing missing — and the only thing the Templates#25 matrix is still red on — is that the *profile* must govern the boundary. Delivering just this slice makes the gate profile-aware end-to-end and flips the failing matrix cell, so it is the MVP on its own.

**Independent Test**: Against a fixed product with a failing handoff, run `route --mode gate` twice — once with `defaultProfile: strict`, once with `defaultProfile: light` — and confirm strict exits 2 while light exits 0; then run both against a satisfied handoff and confirm both exit 0.

**Acceptance Scenarios**:

1. **Given** a product with profile **strict** and a failing handoff, **When** `route --mode gate` runs, **Then** the gate blocks (exit 2) and the block is attributable to the failing handoff.
2. **Given** the same product and failing handoff but profile **light**, **When** `route --mode gate` runs, **Then** the gate does not block (exit 0) and the handoff is surfaced as advisory.
3. **Given** a product with a **satisfied** handoff, **When** `route --mode gate` runs under either profile, **Then** the gate passes (exit 0) — proving the gate distinguishes pass from fail, not merely profile from profile.

---

### User Story 2 - A product with no declared profile fails safe to strict (Priority: P2)

A product that declares **no** policy profile (no `defaultProfile`, or no policy declaration at all) is gated as if it were **strict**: a failing handoff still blocks at `route --mode gate`. The new profile-awareness never weakens an undeclared product into silently passing a failing handoff.

**Why this priority**: The whole risk of making the gate profile-aware is introducing a green-by-omission path — a product that simply never set a profile suddenly stops blocking. The publish enforcement-smoke depends on exactly this: its fixtures carry no policy declaration and must keep blocking on a failing handoff. This guard protects the 089 baseline from regression, so it follows the core behavior as P2.

**Independent Test**: Run `route --mode gate` against a product that has no policy profile declared and a failing handoff; confirm it blocks (exit 2), identically to an explicit strict profile.

**Acceptance Scenarios**:

1. **Given** a product with no `defaultProfile` declared and a failing handoff, **When** `route --mode gate` runs, **Then** the gate blocks (exit 2) — the absent profile resolves to strict.
2. **Given** a product with no policy declaration at all and a failing handoff, **When** `route --mode gate` runs, **Then** the gate blocks (exit 2).
3. **Given** the existing publish enforcement-smoke fixtures (no policy declaration), **When** the smoke runs against the new behavior, **Then** a failing handoff still blocks and the smoke passes unchanged.

---

### User Story 3 - The profile-aware behavior reaches downstream as a new published version (Priority: P3)

The profile-aware enforcement is published to the org feed as a **new version** (`1.1.0` is immutable), strictly orderable after `1.1.0` and resolving within the range consumers pin. Once a downstream product installs it, the FS.GG.Templates#25 composition matrix flips **fully green** (strict blocks / strict-satisfied passes / light does not block), and issue #34 closes with its board item moved to **Done**.

**Why this priority**: The behavior change (US1/US2) is only observable downstream once an installable artifact carries it; `1.1.0` cannot be amended in place. This is the delivery/hardening slice that turns the local fix into a resolved cross-repo item, so it is P3 — valuable and required to close #34, but dependent on the behavior existing first.

**Independent Test**: Inspect the org feed and confirm a new version strictly greater than `1.1.0` that resolves within the consumer's pinned range; install it downstream and confirm the Templates#25 Stage 6b matrix reports all green (no remaining failing cell); confirm issue #34 is closed and its board item shows **Done**.

**Acceptance Scenarios**:

1. **Given** the published `1.1.0`, **When** the profile-aware build is published, **Then** its version is strictly greater than `1.1.0` and resolves within the range consumers pin.
2. **Given** the new version installed downstream, **When** the Templates#25 Stage 6b matrix runs, **Then** all cells pass (strict + failing → blocked, strict + satisfied → clean, light + failing → not blocked).
3. **Given** the resolved behavior, **When** the Coordination board is consulted, **Then** issue FS-GG/FS.GG.Governance#34 is closed and its item shows **Done**.

---

### Edge Cases

- **Profile declared but unrecognized**: a `defaultProfile` value that is neither strict nor light (typo, future profile name) must resolve deterministically and fail-safe (treated as the stricter boundary), never silently relaxing the gate.
- **Mode ≠ gate**: profile-awareness applies at the `route --mode gate` boundary the probe exercises; other route modes must retain their established blocking semantics and not be incidentally loosened by this change.
- **Multiple consumed handoffs**: if more than one handoff gate is present, the run blocks if **any** gate resolves to a blocking effective severity under the active profile — relaxing one must not mask a still-blocking other.
- **Profile relaxes a non-handoff gate too**: switching to light is a product-wide policy choice; the spec's claim is only that the *handoff* gate now honors the profile like every other gate — it must not special-case the handoff back to strict-only.
- **Immutable-version collision**: the profile-aware behavior must not be pushed under `1.1.0`; attempting to publish over an existing version must fail clearly rather than appear to "update" the strict-only baseline.
- **Satisfied handoff under light**: light must not invert the verdict — a satisfied handoff still passes; light only relaxes the *failing* case from blocking to advisory.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Handoff-gate blocking at `route --mode gate` MUST be derived from the canonical Phase-5 enforcement core (the same effective-severity derivation used by other gates), parameterized by the active policy profile — not from a profile-blind, mode-only shortcut.
- **FR-002**: Under a **strict** profile, a **failing** handoff MUST block (exit 2); under a **light** profile, the same failing handoff MUST NOT block (exit 0); under either profile a **satisfied** handoff MUST pass (exit 0).
- **FR-003**: The active profile MUST be sourced from the product's policy declaration (`defaultProfile`) read at the product's edge and carried into the verdict computation.
- **FR-004**: When no profile is declared (absent `defaultProfile`, or no policy declaration), the gate MUST default to **strict** (fail-safe) so a failing handoff still blocks.
- **FR-005**: The `route --mode gate` ↔ enforcement-boundary mapping MUST be such that the strict/light matrix holds — strict tightens the boundary enough that a ship-blocking-maturity handoff gate blocks, while light leaves it advisory (the boundary the probe calls the "verify/ship" line).
- **FR-006**: The strict-only behavior MUST be preserved exactly for products with no declared profile, so the publish enforcement-smoke (profile-less fixtures) still blocks on a failing handoff and passes unchanged.
- **FR-007**: The profile-aware behavior MUST be published as a **new** version on the org feed (`1.1.0` is immutable), strictly orderable after `1.1.0` and resolving within the consumer-pinned range; a fresh **`1.2.0`** is the suggested version (new observable enforcement behavior).
- **FR-008**: Against the new version, the FS.GG.Templates#25 Stage 6b matrix MUST be fully green (strict blocks / strict-satisfied passes / light does not block) with no remaining failing cell.
- **FR-009**: The change MUST NOT alter the `governance-handoff` contract surface (`@1.0.0`); it is a consumer-side enforcement behavior change recorded as such, not a contract version bump.
- **FR-010**: Cross-repo issue **FS-GG/FS.GG.Governance#34** MUST be responded to and closed, and its Coordination board item moved to **Done**, once the profile-aware CLI is reachable on the org feed and the Templates#25 matrix passes.

### Key Entities

- **Policy profile**: the product-declared posture (`defaultProfile`, e.g. strict | light) that selects how tightly gates block; absent → strict. The input this feature makes the handoff gate honor.
- **Handoff gate**: the consumed `governance-handoff.json`-driven gate evaluated at `route --mode gate`; its block/advisory outcome must now derive from the enforcement core under the active profile.
- **Effective-severity derivation (Phase-5 enforcement core)**: the canonical computation that turns a gate's base severity + maturity + mode + profile into a blocking/advisory effective severity; the handoff gate must flow through it rather than around it.
- **Route mode → enforcement boundary**: the mapping from `route --mode gate` to the enforcement boundary that makes the strict/light matrix hold; the relationship under confirmation in this feature.
- **Published CLI version**: the new immutable org-feed version (suggested `1.2.0`) that carries the profile-aware behavior; the artifact downstream installs.
- **Coordination board item / cross-repo issue #34**: the request-and-tracking unit that resolves (response + close + board → Done) when the profile-aware CLI lands and the probe goes green.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a fixed product with a failing handoff, `route --mode gate` exits **2 under strict** and **0 under light**; with a satisfied handoff it exits **0 under both** — the profile demonstrably shifts the blocking boundary.
- **SC-002**: A product with **no declared profile** and a failing handoff exits **2** at `route --mode gate`, identical to an explicit strict profile.
- **SC-003**: The FS.GG.Templates#25 Stage 6b matrix reports **all cells passing** (the previously failing "light + failing → exit 0" cell is now green) against the new CLI version.
- **SC-004**: The publish enforcement-smoke (`tests/cli-publish-smoke/run.sh`) passes unchanged — its profile-less fixtures still block on a failing handoff.
- **SC-005**: A new CLI version strictly greater than `1.1.0` is published to the org feed, resolves within the consumer-pinned range, and is installable as a dotnet tool.
- **SC-006**: The `governance-handoff` contract surface is unchanged at `@1.0.0` (no contract version bump recorded for this behavior change).
- **SC-007**: Issue FS-GG/FS.GG.Governance#34 is closed and its Coordination board item shows **Done**.

## Assumptions

- **No new contract or consumer wiring is required.** The handoff consumer and the Phase-5 enforcement core already exist and are wired through `route`; this feature redirects handoff-gate blocking through that core and threads the profile in — it does not add a new consumer or change the handoff contract.
- **Mode mapping default**: `route --mode gate` maps to the enforcement **verify/ship** boundary, the relationship under which the probe's matrix holds (strict tightens a ship-blocking-maturity gate to blocking; light leaves it advisory). The issue flags this as "confirm"; this spec adopts it as the working assumption, to be validated against the enforcement truth table during planning. If the intended mapping differs, the boundary that satisfies the strict-blocks / light-passes matrix governs.
- **Fail-safe bias**: any ambiguity in resolving the profile (absent, empty, or unrecognized value) resolves to the stricter boundary, never to relaxation — protecting the 089 baseline and the publish smoke from green-by-omission.
- **Version immutability**: `1.1.0` cannot be amended; the profile-aware behavior ships as a fresh version (suggested `1.2.0`), published through the same automated path 089 established.
- **The Templates#25 probe is the downstream acceptance signal**: this item is "resolved" only when that probe's matrix is fully green against the published profile-aware CLI; a build the probe still fails is not a successful resolution of #34.
- **Profile semantics are product-wide**: switching to light relaxes gates per the established policy model; this feature only ensures the handoff gate participates in that model like any other gate, not that the handoff is uniquely privileged.
