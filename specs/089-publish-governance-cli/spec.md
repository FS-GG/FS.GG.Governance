# Feature Specification: Publish the Consumer-Bearing Governance CLI to the Org Feed

**Feature Branch**: `089-publish-governance-cli`

**Created**: 2026-06-29

**Status**: Draft

**Input**: User description: "start the next governance item on the coordination board." → Coordination board item **FS-GG/FS.GG.Governance#28** (Status: *Ready*, Phase: *P3 Governance*) — a cross-repo request from FS.GG.Templates: *"Publish FS.GG.Governance.Cli (with the 081 SDD-handoff consumer) to the org feed."* Blocks **FS-GG/FS.GG.Templates#25**. Contract: `governance-handoff` (consumer-side verification; no surface change).

## Why This Feature

The SDD→Governance enforcement loop is **shipped in source but not in any installable tool**. Spec `081-sdd-handoff-consumer` added the consumer (`FS.GG.Governance.Adapters.SddHandoff`) and wired it through `route`, `ship`, and `verify`, so a produced `readiness/<id>/governance-handoff.json` should drive the governance verdict. Epic #8's child #10 ("Ship the handoff CONSUMER") closed 2026-06-28, and the board and registry now read as **done**.

But nothing downstream can actually run that loop:

- The org GitHub Packages feed has **no `FS.GG.Governance.Cli` at all** — it returns 404, whereas sibling tools (`FS.GG.SDD.Cli`) and libraries (`FS.GG.Contracts`) are published. There is no publish path for the CLI in this repository today.
- The only installable builds (local dev feed, `1.0.0`/`0.1.1`) **predate the consumer merge** — they ship the older adapters but not `Adapters.SddHandoff`, so `route --mode gate` against a product with a deliberately **failing** handoff exits 0: the produced handoff is silently ignored and enforcement passes **green-by-omission**.

This blocks **FS.GG.Templates#25**, whose gated composition test must exercise the enforcement loop end-to-end through a composed product (`scaffold → fsgg-sdd ship → fsgg-governance route --mode gate → assert strict blocks / light passes`). With no consumer-bearing CLI reachable on the feed, that stage can only **SKIP** (it probes the installed CLI and refuses to assert against a tool that does not enforce a failing handoff). The cross-repo "done" is therefore not honest: a downstream consumer cannot exercise the very loop the registry claims is delivered.

This feature closes that gap by **publishing the Governance CLI — carrying the spec-081 consumer — to the org feed under a coherent, tagged version**, and recording the publish as a consumer-side coherence verification of `governance-handoff@1.0.0` (no contract surface changes). Once a downstream product can `dotnet tool install` it and have a produced `governance-handoff.json` drive the verdict, the Templates composition stage flips from SKIP to asserting the full strict-blocks / light-passes matrix, and issue #28 resolves.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A downstream product can install a Governance CLI that actually enforces the handoff (Priority: P1)

A composed product (or any consumer) installs the Governance CLI from the org feed as a dotnet tool and runs `fsgg-governance route --mode gate` against a product whose SDD step emitted a **failing** `governance-handoff.json`. The installed CLI consumes that handoff and **blocks** (strict mode), while the same run under light mode does not block. The handoff is no longer ignored — it drives the verdict.

**Why this priority**: This is the entire value of the item. Until an installable CLI both *exists on the feed* and *carries the consumer*, the enforcement loop cannot be exercised downstream and Templates#25 stays skipped. It delivers the MVP on its own: a reachable tool that enforces a produced handoff.

**Independent Test**: From a clean environment with only the org feed configured, install the published CLI and run it in `--mode gate` against (a) a product with a failing handoff and (b) the same product under light mode; confirm (a) blocks with a non-success outcome attributable to the handoff and (b) does not block.

**Acceptance Scenarios**:

1. **Given** the published CLI installed from the org feed, **When** it runs `route --mode gate` against a product whose `governance-handoff.json` reports failure, **Then** it blocks (non-success / strict-failure outcome) and the verdict is attributable to the consumed handoff.
2. **Given** the same installed CLI and the same failing handoff, **When** it runs under light (non-strict) mode, **Then** it does not block.
3. **Given** a product whose handoff reports success, **When** the installed CLI runs `route --mode gate`, **Then** it passes — confirming the gate distinguishes pass from fail rather than always blocking.

---

### User Story 2 - The publish is version-coherent, discoverable, and registry-recorded (Priority: P2)

A maintainer (or the registry/auto-update fabric) can identify exactly which published Governance CLI version carries the consumer. The package appears on the org feed under a version that is distinct from and orderable after the predecessor builds, resolves within the version range consumers pin, is tagged in the repository, and is recorded in the cross-repo registry as a **coherence verification** of `governance-handoff@1.0.0` (consumer side) — not as a contract surface change.

**Why this priority**: Existence on the feed (US1) is necessary but not sufficient: the registry and board read "done" today while the truth is otherwise, so the publish must leave an auditable, coherent record that ties the version to the verified handoff contract. This depends on US1 (something must be published first), so it follows as P2.

**Independent Test**: Inspect the org feed and confirm the published version is strictly greater than the last predecessor and resolves within the consumer's pinned range; inspect the registry compatibility projection and confirm a `governance-handoff@1.0.0` consumer coherence entry references this publish and issue #28; confirm the release is tagged.

**Acceptance Scenarios**:

1. **Given** prior predecessor builds, **When** the new CLI is published, **Then** its version is strictly greater than every predecessor and resolves within the registry-pinned range consumers use.
2. **Given** the publish, **When** the registry is consulted, **Then** it records a consumer-side coherence verification of `governance-handoff@1.0.0` (no contract surface bump) linking this publish and FS.GG.Governance#28.
3. **Given** the published version, **When** the repository is inspected, **Then** a matching release tag for that version exists.

---

### User Story 3 - Publishing is guarded and repeatable, never green-by-omission (Priority: P3)

The publish runs through a repeatable, automated path rather than a one-off manual push, and that path **refuses to publish a CLI that does not carry the consumer** under the consumer-bearing version. A future release (e.g. a later fix) reaches the feed the same way, and at no point can a consumer-less build be published as though it enforces the handoff.

**Why this priority**: The immediate unblock (US1/US2) can be delivered by a single publish, but the failure mode this whole item exists to fix — a "done" signal sitting atop a tool that silently doesn't enforce — must not be reintroducible. Making the path repeatable and self-guarding protects the fix going forward, so it is P3 (valuable hardening, not required for the first unblock).

**Independent Test**: Run the publish path against a build that is missing the consumer and confirm it refuses to publish (clear, attributable failure); run it against a consumer-bearing build and confirm it publishes; trigger it a second time for a new version and confirm the new version reaches the feed without manual artifact handling.

**Acceptance Scenarios**:

1. **Given** a build that does not contain the spec-081 consumer, **When** the publish path runs, **Then** it fails with a clear reason and does not push a package under the consumer-bearing identity.
2. **Given** a consumer-bearing build, **When** the publish path runs, **Then** it publishes successfully.
3. **Given** a subsequent version, **When** the publish path runs again, **Then** the new version reaches the feed through the same automated path without one-off manual steps.

---

### Edge Cases

- **Version immutability on the feed**: the GitHub Packages NuGet feed rejects re-pushing an existing version. The publish must use a fresh version each release and surface a clear failure (not a partial/ambiguous state) if a collision occurs.
- **Authentication / credential failure to the org feed**: must surface clearly and leave nothing half-published or mislabeled — never a partially-pushed artifact presented as a successful release.
- **Tool-package dependency completeness**: an installed dotnet tool must run standalone; if the consumer's assemblies are not actually carried in the published tool package, installation may "succeed" yet enforcement silently regress to green-by-omission — this must be caught by the publish guard, not discovered downstream.
- **Predecessor builds on other feeds**: older `1.0.0`/`0.1.1` builds exist on the local dev feed without the consumer; the published org-feed version must be unambiguously identifiable as the consumer-bearing one despite sharing a package id.
- **Templates probe handshake**: until the consumer-bearing CLI is reachable, the Templates#25 stage SKIPs (never green, never false-fail); the published artifact must satisfy that probe so the stage flips to asserting automatically — a publish that the probe still rejects is not a successful resolution of #28.
- **Pre-release vs stable semantics**: if a pre-release version is used, it must still resolve within the range consumers pin (or the resolution must be made explicit), so the consumer is not silently excluded from picking it up.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A `FS.GG.Governance.Cli` package MUST be published to the org GitHub Packages NuGet feed (which currently returns 404 for it), installable as a dotnet tool that exposes the `fsgg-governance` command.
- **FR-002**: The published package MUST contain the spec-081 SDD-handoff consumer reachable from `route`, `ship`, and `verify`, such that a produced `governance-handoff.json` drives the resulting verdict.
- **FR-003**: When the installed CLI runs `route --mode gate` against a product with a **failing** handoff, it MUST block (strict-failure / non-success outcome attributable to the handoff); under light mode it MUST NOT block; against a **passing** handoff it MUST pass.
- **FR-004**: The published version MUST be distinct from and strictly orderable after every predecessor build, and MUST resolve within the version range that consumers pin for the Governance CLI.
- **FR-005**: The release carrying the published version MUST be tagged in the repository with that version.
- **FR-006**: The cross-repo registry (and its compatibility projection) MUST record this publish as a **consumer-side coherence verification** of `governance-handoff@1.0.0` — not as a contract surface change or version bump of the contract.
- **FR-007**: The publish path MUST fail safe: credential/authentication errors, version collisions, or push failures surface clearly and never leave a partially-published or mislabeled artifact.
- **FR-008**: The publish path MUST guard that the artifact being pushed actually carries the consumer, and MUST refuse to publish a consumer-less build under the consumer-bearing package identity (no green-by-omission release).
- **FR-009**: Cross-repo issue **FS-GG/FS.GG.Governance#28** MUST be responded to and closed, and its Coordination board item moved to **Done**, once the consumer-bearing CLI is reachable on the org feed.
- **FR-010**: Publishing MUST be repeatable through an automated path so that subsequent versions reach the feed without one-off manual artifact handling.

### Key Entities

- **Governance CLI tool package**: the installable `FS.GG.Governance.Cli` dotnet tool (command `fsgg-governance`) that, once published, carries the consumer; identified by package id + version on the org feed.
- **`governance-handoff.json`**: the SDD-produced artifact the consumer reads; its pass/fail state must drive the CLI's gate verdict. Contract `governance-handoff@1.0.0`, unchanged by this feature.
- **Org GitHub Packages NuGet feed**: the org-scoped feed already hosting sibling FS-GG packages; the destination that currently lacks `FS.GG.Governance.Cli`.
- **Registry coherence entry**: the cross-repo registry record asserting the consumer side of `governance-handoff@1.0.0` is verified by this publish, linking issue #28.
- **Coordination board item / cross-repo issue #28**: the request-and-tracking unit that resolves (response + close + board → Done) when the publish lands.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Querying the org feed for `FS.GG.Governance.Cli` returns the package (no longer 404) with at least one version that carries the consumer.
- **SC-002**: From a clean environment with only the org feed configured, installing the published CLI as a dotnet tool succeeds and exposes a runnable `fsgg-governance` command.
- **SC-003**: Against a product with a failing handoff, the installed CLI blocks in strict gate mode and passes in light mode (and passes against a passing handoff) — and the FS.GG.Templates#25 composition stage flips from SKIP to asserting the strict-blocks / light-passes matrix and passes.
- **SC-004**: The published version is strictly greater than every predecessor build and resolves within the range consumers pin.
- **SC-005**: The registry compatibility projection records a `governance-handoff@1.0.0` consumer coherence verification that references this publish and issue #28, with no contract surface change.
- **SC-006**: Issue FS-GG/FS.GG.Governance#28 is closed and its Coordination board item shows **Done**.
- **SC-007**: Running the publish path against a consumer-less build is rejected with a clear reason (no package pushed), while a consumer-bearing build publishes.

## Assumptions

- **Scope is the CLI tool package**, the reachable enforcement surface. A general publish pipeline for the ~70 `FS.GG.Governance.*` library packages is out of scope here except insofar as the CLI's required dependency assemblies must be carried inside the installable tool package.
- **No new enforcement logic is required.** The spec-081 consumer is already implemented and wired through `route`/`ship`/`verify`; this feature concerns *reachability via a published artifact* and *coherent versioning/recording*, not new consumer behavior. If the consumer turns out not to be reachable in a built/packed CLI, making it reachable is in scope as part of "publish a CLI that carries the consumer."
- **The `governance-handoff` contract surface is unchanged** (`@1.0.0`); this is a consumer-side coherence verification, recorded as such, not a contract version bump.
- **The org GitHub Packages NuGet feed** is the same org-scoped feed that already hosts `FS.GG.SDD.Cli` and `FS.GG.Contracts`; publishing reuses the repository's existing CI credential/permission model for that feed.
- **The Templates#25 probe is the downstream acceptance signal**: a publish is "successful" for this item only if that probe accepts the published CLI and the composition stage flips from SKIP to asserting.
