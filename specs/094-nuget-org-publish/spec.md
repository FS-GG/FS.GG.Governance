# Feature Specification: Publish Governance packages to public nuget.org

**Feature Branch**: `094-nuget-org-publish`

**Created**: 2026-07-01

**Status**: Draft

**Input**: User description: "start the next governance item on the coord board" → resolves to FS.GG.Governance#41 · P3 Governance · *[cross-repo] Publish FS.GG.Governance.Cli + ReferenceGateSet to nuget.org (ADR-0012)*

## Overview

Today the two publishable governance artifacts — the `FS.GG.Governance.Cli` tool (installed as `fsgg-governance`) and the content-only `FS.GG.Governance.ReferenceGateSet` gate set — are only obtainable from the private-by-history FS-GG **org GitHub Packages feed**, which requires an authenticated token even for read. That is a barrier for any external adopter who wants to install the governance CLI or pin the reference gate set for their overlay drift gate.

Per the cross-repo decision [ADR-0012](https://github.com/FS-GG/.github/blob/main/docs/adr/0012-dual-publish-to-nuget-org.md) (dual-publish) and its auth successor [ADR-0013](https://github.com/FS-GG/.github/blob/main/docs/adr/0013-trusted-publishing-oidc-for-nuget-org.md) (Trusted Publishing / OIDC), each producer repo adds a **public nuget.org** publish leg alongside its existing org-feed push, so the coherent set becomes installable from the default public feed with no FS-GG access. This feature delivers the Governance repo's half of that fabric.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - External adopter installs the governance CLI from the public feed (Priority: P1)

A developer outside the FS-GG organization wants to enforce governance on their product. They run the standard global-tool install against the default public package feed and get a working `fsgg-governance` tool — no org membership, no GitHub Packages token, no custom NuGet source.

**Why this priority**: This is the whole point of ADR-0012 — remove the private-feed barrier so the governance tool is adoptable by anyone. Without it, every other consumer story is blocked behind org auth.

**Independent Test**: From a clean machine with only the default public feed configured, install the CLI at its released version and run a governance command against a fixture handoff; the tool resolves, installs, and enforces. Delivers standalone value even if the reference gate set (Story 2) is not yet public.

**Acceptance Scenarios**:

1. **Given** a released version of the governance CLI, **When** an external consumer installs the tool globally from the default public feed at that version, **Then** the install succeeds without any FS-GG credential and the tool runs.
2. **Given** the CLI's public listing, **When** a consumer views it, **Then** it shows a license, a readme, a repository link, and an icon (a complete, trustworthy listing).
3. **Given** a release that published the CLI, **When** the same package is inspected on both the org feed and the public feed, **Then** it is the byte-identical artifact at the same version (no divergent re-pack).

---

### User Story 2 - Consumer pins the reference gate set from the public feed (Priority: P2)

A consumer maintaining an FS-GG governance overlay wants their drift gate to compare against the canonical published reference gate set. They restore `FS.GG.Governance.ReferenceGateSet` at a pinned version from the default public feed, with no private-feed credentials.

**Why this priority**: Completes the public availability of the governance surface. The CLI (P1) is usable on its own; the reference gate set makes the overlay-drift workflow adoptable end-to-end without org access.

**Independent Test**: From an environment with only the default public feed, restore the reference gate set content package at its published version and confirm the four `.fsgg` files land in the framework-agnostic consumer location. Testable independently of the CLI.

**Acceptance Scenarios**:

1. **Given** a released reference gate set version, **When** a consumer restores it from the default public feed, **Then** the content-only package resolves without any FS-GG credential.
2. **Given** the reference gate set's schema-derived version, **When** it is published to the public feed, **Then** its version equals the version derived from its four contained `schemaVersion` declarations (unchanged from the org-feed artifact).

---

### User Story 3 - A release publishes the full set or fails loudly (Priority: P3)

A maintainer cuts a release. Either every in-scope package reaches both feeds, or the release fails with a clear error — there is never a half-published coherent set and never a package that skipped its quality gates.

**Why this priority**: Protects consumers from a partially-published or unvetted set. It is a safety property over the P1/P2 mechanics rather than new user-facing capability, hence P3.

**Independent Test**: Force the public-feed auth to be unavailable and run a release; confirm it fails closed with a pointer to the governing ADR and publishes nothing to the public feed. Separately, re-run a release for an already-published version and confirm it completes as an idempotent no-op.

**Acceptance Scenarios**:

1. **Given** public-feed authentication is unavailable or unconfigured, **When** a release runs, **Then** it fails with an explicit error referencing the governing decision and does not silently skip the public leg.
2. **Given** a package that failed a pre-publish quality gate, **When** the release proceeds, **Then** the failing package is never pushed to either feed.
3. **Given** a version already present on the public feed, **When** a release re-publishes it, **Then** the push is an idempotent success (no duplicate, no mutation, no error).
4. **Given** a maintainer runs a dry-run release (manual dispatch with no version), **When** it executes, **Then** packages are produced but nothing is pushed to any feed.

---

### Edge Cases

- **Version-source divergence**: The CLI's version comes from its evaluated project `Version`; the reference gate set's version is derived from its schema declarations. A release must publish each at its own authoritative version — a mismatch between a version-bearing tag and the CLI version must fail rather than mislabel an artifact.
- **Public indexing latency**: A freshly pushed package may not be immediately resolvable on the public feed's index; "published" means accepted by the feed, not instantly indexed. Success criteria account for eventual availability, not instantaneous.
- **Prefix not yet reserved**: The `FS.GG.` ID-prefix reservation is a follow-on anti-squat step; the first publish claims the IDs and must not be blocked on the reservation.
- **Re-run after partial failure**: If a prior run pushed to the org feed but failed before the public push, a re-run must complete the public push idempotently without duplicating the org-feed artifact.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A release of this repo MUST publish `FS.GG.Governance.Cli` to the public nuget.org feed in addition to the existing org GitHub Packages feed (dual-publish).
- **FR-002**: A release MUST publish the content-only `FS.GG.Governance.ReferenceGateSet` to the public nuget.org feed. (ADR-0012 scopes this content package as intentionally public.)
- **FR-003**: The public-feed push MUST use the byte-identical package artifact already produced for the org-feed push — the same version, no separate re-pack.
- **FR-004**: The public push MUST occur only AFTER all existing pre-publish quality gates pass — the CLI test suite and the enforcement (green-by-omission) smoke for the CLI, and the reference-gate-set guard (the G1–G7 checks) for the gate set. A package that fails its gate MUST never reach either feed.
- **FR-005**: Publishing to the public feed MUST authenticate via Trusted Publishing (OIDC) per ADR-0013; it MUST NOT depend on a long-lived push API key or stored secret.
- **FR-006**: If public-feed authentication is unavailable or unconfigured, the release MUST fail closed with an explicit error that points to the governing decision (ADR-0012 / ADR-0013). It MUST NOT silently skip the public leg and MUST NOT report success.
- **FR-007**: Re-publishing an already-published version MUST be an idempotent no-op success (respecting version immutability). Any other push failure (auth, network, malformed artifact) MUST fail the release.
- **FR-008**: A dry-run release (manual dispatch with no version) MUST produce packages but MUST NOT push to any feed.
- **FR-009**: Each published package MUST carry the listing metadata required for a complete public listing: a license expression, a readme, a repository URL, and an icon.
- **FR-010**: Package IDs MUST remain unchanged (no rename — ADR-0003), and each package's version MUST remain its existing authoritative value: the CLI's project `Version` and the reference gate set's schema-derived version (ADR-0007).
- **FR-011**: On successful public availability of the in-scope packages, the cross-repo coherence id `nuget-org-published` MUST be advanced toward `coherent: true` (the coordination outcome this item blocks).

### Key Entities

- **FS.GG.Governance.Cli**: The governance tool package (command `fsgg-governance`). Versioned by its project `Version`. Currently org-feed-only; this feature adds its public-feed listing.
- **FS.GG.Governance.ReferenceGateSet**: The content-only, assembly-free reference `.fsgg` gate set (governance / capabilities / policy / tooling — the fixed, positional order the version segments are derived in). Versioned by its four contained `schemaVersion` declarations. Intentionally public per ADR-0012.
- **`nuget-org-published` coherence id**: The cross-repo registry marker (currently `coherent: false`) that flips toward coherent once every in-scope FS-GG package — including these two — resolves on the public feed.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user with no FS-GG organization access can install the governance CLI from the default public feed at its released version and run a governance command successfully — zero private-feed credentials required.
- **SC-002**: A consumer overlay drift gate can restore the reference gate set from the default public feed with no private-feed credentials, and receives the same four `.fsgg` files at the same version as the org-feed artifact.
- **SC-003**: 100% of releases either publish the complete in-scope set to both feeds or fail — zero half-published or gate-skipping releases across observed runs. Because the two packages publish in independent jobs, set-completeness is not an atomic cross-job publish: it is achieved by each package failing loudly on its own push failure (FR-006) plus the idempotent re-run that completes any package left behind (FR-007, "Re-run after partial failure" edge case). A run where one package's job fails MUST surface as a failed release, never a silent partial success.
- **SC-004**: Both packages' public listings display a license, a readme, a repository link, and an icon.
- **SC-005**: Re-running a release for an already-published version completes successfully with no duplicate and no mutated artifact.
- **SC-006**: A release run with public-feed auth removed fails with an actionable, decision-referencing error and publishes nothing to the public feed.

## Assumptions

- **Auth model is Trusted Publishing (OIDC), not an API key.** The originating issue body (#41) described an `NUGET_ORG_API_KEY`; ADR-0013 supersedes ADR-0012 §6 and the admin gate (#103) confirms the Trusted Publishing policy for this repo's `publish.yml` workflow is **Active**. This feature therefore assumes OIDC-based auth and no stored push secret.
- **The admin provisioning gate is satisfied for authentication.** Per #103, the per-producer Trusted Publishing policies exist and are Active, so the public push is unblocked to authenticate. The `FS.GG.` ID-prefix reservation is a follow-on anti-squat step and is not a prerequisite for the first publish.
- **Dual-publish, not migration.** The existing org GitHub Packages feed push remains; the public nuget.org push is added alongside it. Consumers may use either feed.
- **The reference gate set publish path is in scope.** The CLI currently has an org-feed publish path (`publish.yml`); the reference gate set is currently guarded but has no feed-publish wiring. Establishing its publish to both feeds — org and public — is within this feature's scope so both packages become publicly resolvable.
- **Public intent for the content package is confirmed.** ADR-0012 explicitly scopes `FS.GG.Governance.ReferenceGateSet` as intentionally public; this feature treats that as the confirmed intent.
- **Listing metadata does not change identity.** Adding license / readme / repository / icon metadata is additive presentation; it does not change package IDs or versions.
- **Package identity is already ratified.** The constitution's TODO(PACKAGE_IDENTITY) — ratify the `FS.GG.Governance.*` namespace in a decision record when the first package is published — is satisfied by ADR-0003 (permanent package IDs, no rename) and ADR-0007 (schema-derived version). This first *public* publish claims the IDs under those existing decisions; it does not itself introduce or rebrand any identity.
- **"Published" means feed-accepted.** Success is measured on the feed accepting the artifact; the public index may take time to surface it, and the acceptance criteria allow for that eventual-consistency window.

## Dependencies

- **FS-GG/.github#103** — admin provisioning (Trusted Publishing policies + prefix). Policies **Active** (authentication unblocked); prefix reservation is a follow-on.
- **ADR-0012** — dual-publish to nuget.org (the decision this item implements).
- **ADR-0013** — Trusted Publishing (OIDC) for nuget.org auth (supersedes the API-key approach in #41's body).
- **ADR-0003** — permanent package IDs (no rename). **ADR-0007** — schema-derived version for the reference gate set.
- **Sibling producer items** (same fabric, out of scope here): FS.GG.SDD#56, FS.GG.Rendering#40.
