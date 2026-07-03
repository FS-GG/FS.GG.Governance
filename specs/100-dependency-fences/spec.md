# Feature Specification: Repair the repository's dependency fences

**Feature Branch**: `100-dependency-fences`

**Created**: 2026-07-03

**Status**: Draft

**Input**: User description: "Repair the repo's dependency fences (2026-07-02 review M-ARCH-1/2/3, issue #53, epic #44) — YamlDotNet fence drift, exe→exe project references, and the `fsgg` tool-command-name collision, plus low add-ons (edge-tier reference convention, centralized VersionPrefix, local ADR index)."

## Context

The 2026-07-02 code quality & architecture review ([report](../../docs/reports/2026-07-02-141008-code-quality-architecture-review.md), tracked by epic #44, this item #53) found the repository's *architecture is sound* but that three **dependency-fence** claims no longer match the build graph. A dependency fence is a documented constraint on which projects may depend on what — a third-party parser, another executable, or a shared tool identity. Each of the three fences here has drifted silently because nothing guards it: the code and the documentation disagree, and a build stays green either way. This feature restores each fence to a single true state and adds an automated guard so it cannot drift unnoticed again. The existing Spectre.Console confinement guard test is the working template for that pattern.

This is a **repository-hygiene / packaging-metadata** change. It changes project references, `.fsproj`/`.props` metadata, documentation, and adds guard tests. It does **not** change any product behavior, public F# API surface, JSON contract, or package ID.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - YAML parsing has one documented, guarded owner (Priority: P1)

A maintainer reads the README's claim about which projects may parse YAML and trusts it when reasoning about the dependency surface and the trusted-input boundary. Today the README says the YamlDotNet dependency is "isolated to Config," but five projects actually declare a direct `YamlDotNet` package reference (Config, CurrencySensing, RefreshCommand, ReleaseDeclaration, ReleaseCommand). The maintainer needs the documented fence and the real build graph to agree, and needs a test that fails the build if they ever diverge again.

**Why this priority**: This is the fence most likely to mislead a security/trust judgment (YAML is untrusted input), and it is the only set of undocumented dependency decisions in the repo. It also establishes the guard-test pattern the other stories reuse.

**Independent Test**: Inspect the set of projects that directly reference `YamlDotNet`, confirm it matches the README's stated owner(s) exactly, and confirm a guard test fails when a project outside that set gains a direct `YamlDotNet` reference.

**Acceptance Scenarios**:

1. **Given** the repository after this change, **When** the set of projects with a direct `YamlDotNet` package reference is enumerated, **Then** it equals the set the README/`.fsproj` documentation declares as YAML owners — with no undocumented member.
2. **Given** the confinement guard test, **When** a project outside the declared owner set is given a direct `YamlDotNet` reference, **Then** the guard test fails (red build).
3. **Given** the confinement guard test, **When** the declared owner set is unchanged, **Then** the guard test passes.

---

### User Story 2 - Every executable is a leaf; no executable references another executable (Priority: P2)

A maintainer or packaging engineer expects each of the eight command executables to be a standalone leaf of the build graph — installable and reasoned about on its own. Today `Cli` references `RouteCommand` (itself an `Exe`/`PackAsTool` project) and `EvidenceCommand` references `Cli`, so two executables are pulled in as dependencies of others (`fsgg evidence` bundles two other executables, through a depth-14 chain across two composition roots). The maintainer needs the reused logic factored into ordinary library projects so that no executable depends on another executable.

**Why this priority**: Exe→exe references bloat each tool package, entangle composition roots, and make the dependency graph hard to reason about, but they do not (yet) break users. Fixing them is structural cleanup that unblocks clean per-tool packaging.

**Independent Test**: Enumerate all `Exe`-output projects and their project references; confirm no `Exe` project references another `Exe` project; confirm the previously shared behavior still works via the extracted library.

**Acceptance Scenarios**:

1. **Given** the repository after this change, **When** each executable project's project-reference closure is inspected, **Then** none of the eight executables references another executable project.
2. **Given** the extracted shared library, **When** the affected commands run, **Then** they produce the same output/behavior as before the extraction (no regression).
3. **Given** a guard test over the project graph, **When** an executable is made to reference another executable, **Then** the guard test fails.

---

### User Story 3 - Exactly one project owns the `fsgg` tool command name (Priority: P2)

A user installs the governance tools as .NET global tools. Today three projects (`RouteCommand`, `EvidenceCommand`, `CacheEligibilityCommand`) all set `ToolCommandName=fsgg`, so installing any two of them collides on the same command name. Until the planned command multiplexer (README:161, ADR-0003) lands, the user needs at most one project to claim `fsgg` so that global-tool installation is unambiguous.

**Why this priority**: This is a real user-facing packaging collision, but it only bites when two of the colliding tools are installed together; it is a narrow, well-bounded metadata fix.

**Independent Test**: Enumerate every project's `ToolCommandName`; confirm at most one project claims `fsgg`; confirm the others use distinct, documented command names.

**Acceptance Scenarios**:

1. **Given** the repository after this change, **When** `ToolCommandName` values across all packable tool projects are enumerated, **Then** at most one project claims `fsgg`.
2. **Given** two of the previously-colliding tools, **When** both are installed as global tools, **Then** their command names do not collide.
3. **Given** a guard test over tool-command names, **When** a second project claims `fsgg`, **Then** the guard test fails.

---

### Low-priority add-ons (Priority: P3 — include only if cheap and low-risk)

These are documented in the review as low-severity and are in scope only where they are cheap and carry no regression risk:

- **Edge-tier reference convention**: document the intended reference convention for the command/edge tier (example: `VerifyCommand` declares 43 references, 32 transitively reachable) so an over-broad reference list is a documented choice, not accidental drift.
- **Centralized `VersionPrefix`**: give the ~13 baseline-only `.fsproj` files that lack an explicit `<Version>` a single centralized version source so their package versions are consistent and intentional.
- **Local ADR index**: add a local index/pointer for the org-level ADR references this repo cites (0007/0012/0013) so readers can resolve them without leaving the repo.

### Edge Cases

- **YAML owner set is more than one project**: if re-fencing all YAML parsing behind a single owner is disproportionate, the fence may legitimately have more than one owner — in which case the README/`.fsproj` documentation MUST enumerate the full owner set and the guard test MUST assert exactly that set (documentation and guard agree either way).
- **Extraction reintroduces a cycle or a new exe→exe edge**: extracting shared logic MUST NOT create a project cycle or a new executable-to-executable reference; the guard test must catch this.
- **A tool loses its `fsgg` name**: projects that stop claiming `fsgg` still need a usable, distinct, documented command name so no tool becomes uninstallable or unnamed.
- **Org-synced build config would need editing**: if a fix appears to require editing an org-synced file (`Directory.Build.props`, `Directory.Packages.props`, `.config/dotnet-tools.json`), the change MUST be redirected to a repo-owned file instead; the org-synced files stay byte-identical to the org baseline.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The set of projects declaring a direct `YamlDotNet` package reference MUST equal the set the repository documentation (README and/or `.fsproj` comments) declares as YAML owners — with no undocumented member.
- **FR-002**: A confinement guard test MUST fail the build when any project outside the declared YAML-owner set gains a direct `YamlDotNet` reference, and pass when the owner set is unchanged (following the existing Spectre.Console confinement guard as the template).
- **FR-003**: No executable (`Exe`-output) project MUST reference another executable project; reused logic MUST be relocated into ordinary library projects so all eight executables are leaves of the executable graph.
- **FR-004**: The behavior of the affected commands (notably `fsgg evidence` and the route pipeline) MUST be unchanged after the shared logic is extracted — no functional regression.
- **FR-005**: At most one project MUST claim `ToolCommandName=fsgg`; every other tool project MUST use a distinct, documented `ToolCommandName`.
- **FR-006**: A guard test MUST protect each restored fence (YAML owner set, no exe→exe references, single `fsgg` owner) so future drift fails the build rather than passing silently.
- **FR-007**: The change MUST NOT alter any public F# API surface (`.fsi` files / surface-area baselines), any JSON contract, or any package ID.
- **FR-008**: The change MUST NOT edit the org-synced build config files (`Directory.Build.props`, `Directory.Packages.props`, `.config/dotnet-tools.json`); any repo-owned configuration MUST live in `Directory.Build.local.props` and/or the individual `.fsproj` files, and the org-synced files MUST stay byte-identical to the org baseline.
- **FR-009**: The full build and test suite MUST pass on this change as the acceptance evidence (real evidence, no synthetic substitute).
- **FR-010** *(P3, optional)*: Where cheap and low-risk, the edge-tier reference convention SHOULD be documented, the baseline-only `.fsproj` versions SHOULD draw from a single centralized `VersionPrefix`, and a local ADR index SHOULD be added for the cited org-level ADRs (0007/0012/0013).

### Key Entities

- **Dependency fence**: a documented constraint on which projects may depend on a given third-party package, another project, or a shared identity — here: the YAML-owner set, the no-exe→exe rule, and the single `fsgg` owner. Each pairs a documented true state with an automated guard.
- **YAML owner set**: the exact set of projects permitted to parse YAML (directly reference `YamlDotNet`). Currently five projects; documentation claims one.
- **Executable (leaf) project**: an `Exe`/`PackAsTool` project that must not appear in another executable's reference closure. There are eight.
- **Tool command name**: the `ToolCommandName` a packable tool installs as; `fsgg` must have at most one owner until the multiplexer lands.
- **Confinement guard test**: an automated test asserting a fence's true state, red when the fence drifts (Spectre.Console confinement test is the template).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The number of projects directly referencing `YamlDotNet` that are *not* named in the documentation is **0**.
- **SC-002**: The number of executable projects that reference another executable project is **0** (down from 2 today: `Cli`→`RouteCommand`, `EvidenceCommand`→`Cli`).
- **SC-003**: The number of projects claiming `ToolCommandName=fsgg` is **at most 1** (down from 3 today).
- **SC-004**: Each of the three restored fences has exactly one guard test that turns the build red on drift — verified by a demonstrated red build when the fence is deliberately broken.
- **SC-005**: The full build and test suite passes, and no `.fsi`/surface baseline, JSON contract, package ID, or org-synced build-config file is changed.
- **SC-006**: A maintainer can read the documented state of all three fences and find it matches the build graph exactly (no contradicting claim remains in the README).

## Assumptions

- The README's "isolated to Config" claim is the drifted artifact; the intended end-state is a single documented owner where feasible, otherwise a fully documented multi-owner set — the guard test enforces whichever is chosen. (Resolved during planning.)
- Extracting shared route/evidence logic into a library is preferable to duplicating it; the extracted library carries no new third-party dependency.
- The command multiplexer (ADR-0003) is out of scope here; this feature only removes the `fsgg` collision until that lands, so the non-owner tools keep working under distinct names.
- The eight executable projects are: `Cli`, `RouteCommand`, `EvidenceCommand`, `CacheEligibilityCommand`, `VerifyCommand`, `ShipCommand`, `ReleaseCommand`, `RefreshCommand`.
- No product F# public surface changes, so `.fsi` files and surface-area baselines are intentionally untouched (this is appropriate, not an omission).
- The P3 add-ons may be split into a follow-up if they prove non-trivial; they do not gate P1/P2 acceptance.

## Out of Scope

- The command multiplexer / single-entry-point tool (ADR-0003, README:161) — only the `fsgg` collision is removed here.
- Any change to product behavior, public F# API surface, JSON contracts, or package IDs.
- Editing org-synced build config; sibling-repo dependency fences.
- The other epic #44 children (#54 tests/CI hygiene, #55 CLI correctness, #56 low backlog) — tracked separately.

## Dependencies

- The existing Spectre.Console confinement guard test (the template for the new guard tests).
- The org-synced build-config drift check (must continue to pass byte-identically).
- Org-level ADR-0003 (package IDs / tool identity) and the cited ADRs 0007/0012/0013 (for the local index add-on).
