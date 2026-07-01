---
description: "Task breakdown for 094-nuget-org-publish"
---

# Tasks: Publish Governance packages to public nuget.org

**Input**: Design documents from `/specs/094-nuget-org-publish/`

**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅

**Tier**: Tier 1 (contracted change — package distribution contract). No F# public surface
changes; `.fsi` files and surface baselines are intentionally untouched (plan Constitution Check).

**Organization**: Tasks are grouped by phase (sequential) and user story (`[US1]`/`[US2]`/`[US3]`).
Phases run in order; tasks within a phase marked `[P]` have no dependency on another incomplete
task in the same phase and may run in parallel.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: parallel-safe (different file, no dependency on an incomplete task in this phase)
- **[US#]**: the user story this task serves (traceability)
- Every task names an exact file path.

## Evidence discipline (Principle IV / V)

- **Elmish/MVU (Principle IV): N/A.** This feature adds no stateful or I/O-bearing F# product
  code — the deliverables are GitHub Actions YAML and MSBuild pack metadata. The only touched
  script (`pack-reference-gate-set.fsx`) is reused as-is; its `Process.Start` I/O edge is unchanged.
- **Test evidence (Principle V): real CI only.** The existing gates stay authoritative
  (`cli-tests` + `enforcement-smoke`; G1–G7 for the gate set). Acceptance is a real dual-feed
  publish, a dry-run that pushes nothing, and a fail-closed run — no synthetic evidence. Mark a task
  `[X]` only against a real run/inspection; never green a failing gate.

---

## Implementation status (2026-07-01)

**Landed with real/local evidence (21/25):** T001–T010, T012–T017, T019–T023. All code and config are
in place — icon + shared/scoped listing metadata, both `.fsproj` files, and both `publish.yml` publish
legs (CLI + a new `publish-reference-gate-set` job) with Trusted Publishing. Local evidence: both
packages pack with the full §5 listing (license/readme/repo/icon), the gate set stays content-only
(no `lib/`, no dependency group) with the G1–G7 guard green, the packed CLI installs and runs as a
tool, and the api-check gate sweep is clean.

**⚠️ Contract premise corrected (T005).** The plan/contract/research assumed the other ~70
`FS.GG.Governance.*` projects are `IsPackable=false` and "ignore pack metadata". They are actually
`IsPackable=true` and are packed by `gate.yml → pack-and-apicheck.fsx` against org-feed baselines, so a
**shared** `<PackageIcon>` broke all of them with `NU5046`. Fix: only the file-independent identity
fields (license, repo/project URL, authors) are shared in `Directory.Build.local.props`; `PackageIcon`
(and `PackageReadmeFile`) live in the two publishable `.fsproj` files next to their `<None>` items.
Verified: full `pack-and-apicheck.fsx` sweep exits 0 with zero NU5046; both targets keep the icon.
The `contracts/package-listing-metadata.md` "Placement rules" (icon in shared props) is superseded by
this scoping — its rationale rested on the false premise.

**Deferred to a real CI release (4/25):** T011, T018 (dual-feed publish + public-feed consumer
install), T024 (CI quickstart S3/S4/S6/S7 + SC-003 set-completeness), T025 (cross-repo coherence
follow-up, blocked on the packages actually resolving on nuget.org). These need an actual
`release: published` run against the live Trusted Publishing policy and cannot be produced from the dev
environment.

---

## Phase 1: Setup (packaging assets)

**Purpose**: Land the shared assets both packages' listings reference, before any metadata wires
them in.

- [X] T001 [P] Add the package icon `packaging/assets/icon.png` — a small square PNG (≤128×128,
  under the nuget.org 1 MB icon limit). This is the file `PackageIcon` will name; no icon exists in
  the repo yet (research D6, metadata contract "Assets to add"). **DONE**: added `packaging/assets/icon.png`,
  128×128, 8-bit sRGB, 1136 bytes (well under 1 MB).
- [X] T002 [P] Confirm the README to pack: reuse the repo root `README.md` (metadata contract
  allows root README or a short packaging README). Record the decision inline in this tasks file if
  a dedicated packaging README is added instead; downstream `PackageReadmeFile` tasks (T007, T011)
  name whichever is chosen. **DONE — decision**: reuse the repo root `README.md` (no dedicated
  packaging README added). Both `.fsproj` files pack `../../README.md` at the package root and set
  `PackageReadmeFile=README.md`.

**Checkpoint**: Icon asset committed; README-to-pack decided.

---

## Phase 2: Foundational (shared listing metadata) — BLOCKS US1 & US2

**Purpose**: The identity-level listing metadata shared by both packages. FR-009 / SC-004 for both
US1 and US2 depend on this; the per-package `.fsproj` tasks in Phase 3/4 layer on top of it.

**⚠️ CRITICAL**: The shared metadata must be inert for the ~70 non-packable projects and must not
touch any org-synced, drift-checked file.

- [X] T003 [US1][US2] Add shared, packable-scoped listing metadata to `Directory.Build.local.props`
  (repo-owned, drift-exempt — imported last): `PackageLicenseExpression=MIT`,
  `RepositoryUrl=https://github.com/FS-GG/FS.GG.Governance` (+`RepositoryType=git`),
  `PackageProjectUrl=https://github.com/FS-GG/FS.GG.Governance`, `PackageIcon=icon.png`,
  `Authors=FS-GG`. License is MIT per the repo `LICENSE` (`Copyright (c) 2026 EHotwagner`).
  (research D6; metadata contract "Placement rules".) **DONE — with a scoping correction**:
  `Authors`, `PackageLicenseExpression`, `PackageProjectUrl`, `RepositoryUrl`, `RepositoryType` are
  shared in `Directory.Build.local.props`. **`PackageIcon` was MOVED to each of the two `.fsproj`
  files instead** — see the ⚠️ finding below: the ~70 other projects are `IsPackable=true` (not
  false), so a shared `PackageIcon` breaks all of them with NU5046. The file-independent identity
  fields stay shared; the file-dependent `PackageIcon`/`PackageReadmeFile` are per-`.fsproj`.
- [X] T004 [US1][US2] Confirm no org-synced file was edited — only `Directory.Build.local.props`
  changed; `Directory.Build.props`, `Directory.Packages.props`, and `.config/dotnet-tools.json` are
  byte-identical to their synced source (the `gate.yml` drift check must still pass; spec 088 D6).
  **DONE**: `git status --porcelain` shows none of the three org-synced files changed; only
  `Directory.Build.local.props` (drift-exempt) is modified.
- [X] T005 [US1][US2] Verify the shared metadata is inert for non-packable projects: build a
  representative `IsPackable=false` project and confirm no pack/NU5046 icon-missing warnings arise
  from the new properties (only the two `IsPackable=true` projects consume them; research D6).
  **⚠️ PREMISE CORRECTED + DONE**: the task/contract/research premise ("~70 non-packable projects")
  is **factually wrong** — all ~70 `FS.GG.Governance.*` projects declare `<IsPackable>true</IsPackable>`
  and are packed by `gate.yml → pack-and-apicheck.fsx` against their org-feed API baselines. A global
  `<PackageIcon>` made every one of them fail `NU5046` (reproduced by packing `FS.GG.Governance.Kernel`).
  **Fix**: scoped `PackageIcon` to the two publishable `.fsproj` files only. **Evidence**: after the
  fix, packing `Kernel` succeeds with the shared identity fields present and no icon; the full
  `dotnet fsi pack-and-apicheck.fsx` sweep exits 0 with **zero NU5046**; both target packages still
  carry the icon. (Documented inline in `Directory.Build.local.props` and both `.fsproj` files.)

**Checkpoint**: Shared listing metadata is in place, drift-safe, and inert everywhere except the two
packable projects.

---

## Phase 3: User Story 1 - External adopter installs the governance CLI from the public feed (Priority: P1) 🎯 MVP

**Goal**: `FS.GG.Governance.Cli` publishes to nuget.org (byte-identical, org-feed-first, Trusted
Publishing) with a complete public listing, installable with no FS-GG credential.

**Independent Test**: From a clean machine with only the default public feed,
`dotnet tool install --global FS.GG.Governance.Cli --version <released>` succeeds and
`fsgg-governance` enforces a fixture handoff (quickstart Scenario 5).

### Implementation for User Story 1

- [X] T006 [US1] Add CLI-specific metadata to `src/FS.GG.Governance.Cli/FS.GG.Governance.Cli.fsproj`:
  `Description`, `PackageTags`, and `PackageReadmeFile` (the README chosen in T002). Keep the
  existing `PackAsTool`/`ToolCommandName=fsgg-governance` (metadata contract; FR-009). **DONE**:
  added `Description`, `PackageTags`, `PackageReadmeFile=README.md` (and `PackageIcon` per the T003
  scoping fix); `PackAsTool`/`ToolCommandName` unchanged — the packed nuspec still declares
  `packageType DotnetTool`.
- [X] T007 [US1] In `src/FS.GG.Governance.Cli/FS.GG.Governance.Cli.fsproj`, add `<None Pack="true"
  PackagePath="\">` items for the README and `packaging/assets/icon.png` so both land at the package
  root and satisfy `PackageReadmeFile` / `PackageIcon` (metadata contract). Depends on T001, T002, T003.
  **DONE**: both `<None>` items added; verified `README.md` and `icon.png` are at the `.nupkg` root.
- [X] T008 [US1] Extend the existing `publish` job in `.github/workflows/publish.yml`: add
  `id-token: write` to its `permissions` (keeping `contents: read` + `packages: write`), and after
  the existing org-feed push add a `NuGet/login@v1` step (`user: ${{ secrets.NUGET_USER }}`, profile
  `Paradigma11` — may be hardcoded if the secret is absent) producing `NUGET_API_KEY`
  (contract "CLI path"; ADR-0013). **DONE**: `id-token: write` added; `NuGet/login@v1` step
  `id: nuget-login` with `user: ${{ secrets.NUGET_USER || 'Paradigma11' }}`, gated on `push=='true'`.
- [X] T009 [US1] In the same `publish` job, add the nuget.org push of the **same** already-packed
  `artifacts/packages/FS.GG.Governance.Cli.*.nupkg` — `dotnet nuget push --source
  https://api.nuget.org/v3/index.json --api-key ${{ steps.<login>.outputs.NUGET_API_KEY }}
  --skip-duplicate`, gated on `needs.resolve-version.outputs.push == 'true'`. No second `dotnet
  pack` (INV-1, FR-003; org-feed-first, FR-004). After T008. **DONE**: nuget.org push added after the
  org-feed push, same glob (no re-pack), `--api-key ${{ steps.nuget-login.outputs.NUGET_API_KEY }}
  --skip-duplicate`, gated on `push=='true'`.
- [X] T010 [US1] Local pack inspection (quickstart Scenario 2): `dotnet pack
  src/FS.GG.Governance.Cli/FS.GG.Governance.Cli.fsproj -c Release -o <tmp>` and confirm the `.nuspec`
  declares `license` (MIT expression), `readme`, `repository` URL, and `icon`, with README + icon
  present at the package root (FR-009; SC-004). **DONE**: packed `FS.GG.Governance.Cli.1.2.0.nupkg`;
  nuspec declares `<license type="expression">MIT</license>`, `<readme>README.md</readme>`,
  `<repository type="git" url=.../FS.GG.Governance>`, `<icon>icon.png</icon>`; `README.md` + `icon.png`
  at the package root; `packageType DotnetTool` retained. Also installed it as a tool
  (`dotnet tool install --tool-path …`) and ran `fsgg-governance` — the binary executes (structured
  usage-error 64 on the unknown `--help`), proving the metadata add did not break installability.
- [ ] T011 [US1] Real evidence (quickstart Scenarios 4 + 5): a release run pushes the CLI to the org
  feed then nuget.org byte-identically at the fsproj `<Version>`; from a public-feed-only environment
  the global-tool install resolves and `fsgg-governance` runs with no FS-GG credential
  (SC-001; US1 acceptance 1–3). **NOT DONE — requires a real CI release to nuget.org** (cannot be
  produced from the dev environment). Local proxies done: byte-identical pack + local-folder
  tool-install-and-run. Remaining: an actual `release: published` run pushing to both feeds, then a
  public-feed-only `dotnet tool install --global` at the released version.

**Checkpoint**: The CLI is publicly installable at its released version with a complete listing — MVP
deliverable, independent of US2.

---

## Phase 4: User Story 2 - Consumer pins the reference gate set from the public feed (Priority: P2)

**Goal**: `FS.GG.Governance.ReferenceGateSet` gains a real dual-feed publish path (it has none
today) and resolves from the public feed at its schema-derived version, content-only invariant intact.

**Independent Test**: From a public-feed-only environment, `dotnet add package
FS.GG.Governance.ReferenceGateSet --version <released>` restores and delivers the four `.fsgg` files
to the framework-agnostic consumer location (quickstart Scenario 5; SC-002).

### Implementation for User Story 2

- [X] T012 [US2] Add gate-set-specific listing metadata to
  `packaging/FS.GG.Governance.ReferenceGateSet/FS.GG.Governance.ReferenceGateSet.fsproj`:
  `PackageReadmeFile` (T002's README) plus `<None Pack="true" PackagePath="\">` items for the README
  and `packaging/assets/icon.png`. `Authors`/`Description`/`PackageTags` are already present; do not
  duplicate the shared bits from T003. Depends on T001, T002, T003. **DONE**: added
  `PackageReadmeFile=README.md`, `PackageIcon=icon.png` (per T003 scoping fix), and the two `<None>`
  items; existing `Authors`/`Description`/`PackageTags` left as-is.
- [X] T013 [US2] Confirm the content-only invariant survives the metadata add in that `.fsproj`:
  `IncludeBuildOutput=false` / `SuppressDependenciesWhenPacking=true` remain — the packed `.nupkg`
  MUST have no `lib/` assembly and no dependency group (README/icon are additive package-root files
  only; metadata contract "Invariant"). **DONE**: unpacked `FS.GG.Governance.ReferenceGateSet.1.2.1.1.nupkg`
  — no `lib/` assembly, no `<dependencies>` group in the nuspec; the four `.fsgg` files remain at
  `contentFiles/any/any/.fsgg/`; README/icon added at the package root only.
- [X] T014 [US2] Add a new publish job (e.g. `publish-reference-gate-set`) to
  `.github/workflows/publish.yml` with `permissions: id-token: write` + `packages: write`: checkout,
  set up .NET, then **pack via** `dotnet fsi pack-reference-gate-set.fsx --output artifacts/packages`
  (the script self-runs the G1–G7 guard and refuses to pack when red — the hard pre-push gate, FR-004;
  research D3). Reuse the script as-is (plan; Principle III). **DONE**: `publish-reference-gate-set`
  job added with `contents: read` + `packages: write` + `id-token: write`; packs via the script
  unchanged.
- [X] T015 [US2] In `publish-reference-gate-set`, add an **assert-package-produced** step that fails
  loudly if no `artifacts/packages/FS.GG.Governance.ReferenceGateSet.*.nupkg` exists — a green gate
  plus an empty pack MUST NOT report success (FR-007; contract step 3; mirrors the CLI job's guard).
  After T014. **DONE**: `Assert the gate-set package was produced` step mirrors the CLI job's guard
  (nullglob array + `::error::` + exit 1).
- [X] T016 [US2] In `publish-reference-gate-set`, push org feed **first**
  (`--source https://nuget.pkg.github.com/FS-GG/index.json --api-key ${{ secrets.GITHUB_TOKEN }}
  --skip-duplicate`), then `NuGet/login@v1` → `NUGET_API_KEY`, then push the **same** `.nupkg` to
  `https://api.nuget.org/v3/index.json --skip-duplicate`. Both pushes gated on
  `push == 'true'` (dry-run skips). Org-feed-first, byte-identical (FR-002/FR-003; contract steps 4–6).
  After T015. **DONE**: org-feed push → `NuGet/login@v1` → nuget.org push (same glob, no re-pack); all
  three gated on `push=='true'`; both pushes `--skip-duplicate`.
- [X] T017 [US2] Verify G1–G7 still pass after the metadata change (run
  `dotnet fsi pack-reference-gate-set.fsx --output <tmp>` locally; guard green) and inspect the
  `.nupkg` per T013 (no `lib/`, no dependency group). Confirms the shipped artifact is the tested one.
  **DONE**: `dotnet fsi pack-reference-gate-set.fsx --output <tmp>` ran the G1–G7 guard **green
  (8/8 passed)** then packed at the schema-derived version `1.2.1.1`; nupkg inspected per T013.
- [ ] T018 [US2] Real evidence (quickstart Scenario 5): the gate set resolves from the public feed at
  its schema-derived version and delivers the four `.fsgg` files with no FS-GG credential
  (SC-002; US2 acceptance 1–2). **NOT DONE — requires a real CI release to nuget.org** (cannot be
  produced from the dev environment). Local proxy done: gated pack + `.nupkg` content inspection.
  Remaining: an actual release pushing to both feeds, then a public-feed-only `dotnet add package`
  resolving the four `.fsgg` files.

**Checkpoint**: Both governance packages are publicly resolvable; the overlay-drift workflow is
adoptable end-to-end without org access.

---

## Phase 5: User Story 3 - A release publishes the full set or fails loudly (Priority: P3)

**Goal**: The safety envelope over US1/US2 — fail-closed on missing trust policy, dry-run pushes
nothing, re-publish is idempotent, versions stay authoritative. Most mechanisms are intrinsic to the
Phase 3/4 wiring; these tasks assert them explicitly.

**Independent Test**: On a fork / before policy activation, a real release trigger 401s at
`NuGet/login` and pushes nothing to nuget.org (quickstart Scenario 6); a re-run of an already-
published version is a `--skip-duplicate` no-op (Scenario 7).

### Implementation / verification for User Story 3

- [X] T019 [US3] Confirm **fail-closed**: neither nuget.org push in `.github/workflows/publish.yml`
  falls back to a stored key — `NuGet/login` is the only key source; a missing/mismatched trust
  policy makes it `401` and fails the run. No `NUGET_ORG_API_KEY` / stored push secret anywhere
  (FR-005/FR-006; research D1/D7). Evidence via quickstart Scenario 6 (SC-006). **DONE (inspection)**:
  both nuget.org pushes use only `${{ steps.nuget-login.outputs.NUGET_API_KEY }}`; repo-wide grep
  finds no `NUGET_ORG_API_KEY` / stored nuget.org key (the sole textual hit is a comment). Scenario 6
  (a real 401 on a missing policy) still needs CI (see T024).
- [X] T020 [US3] Confirm **dry-run safety**: every nuget.org push step (T009, T016) and the org-feed
  gate-set push (T016) is gated on `push == 'true'`; a `workflow_dispatch` with no `version` packs
  both packages but pushes to no feed. Evidence via quickstart Scenario 3 (FR-008). **DONE (inspection)**:
  all six push-path steps (CLI login/org/nuget.org, gate-set login/org/nuget.org) carry
  `if: needs.resolve-version.outputs.push == 'true'`; both packs are unconditional. A live dry-run
  dispatch is folded into T024.
- [X] T021 [US3] Confirm **idempotency**: `--skip-duplicate` is present on all four pushes (CLI org +
  nuget.org, gate set org + nuget.org); re-publishing an existing version is a no-op success on both
  feeds. Evidence via quickstart Scenario 7 (FR-007; SC-005). **DONE (inspection)**: all four
  `dotnet nuget push` commands carry `--skip-duplicate`. A live re-publish no-op is folded into T024.
- [X] T022 [US3] Confirm **version authority** stays unchanged: the CLI publishes at the evaluated
  fsproj `<Version>` (the existing `resolve-version` job fails a tag≠version mismatch); the gate set
  publishes at its schema-derived version from `pack-reference-gate-set.fsx` — no unified/overridden
  version, no re-pack (INV-3; FR-010; research D4). **DONE (inspection)**: the CLI pack still uses
  `-p:Version=${{ needs.resolve-version.outputs.version }}`; the gate-set job passes **no** `-p:Version`
  (the script derives it — locally observed `1.2.1.1`); each nuget.org push reuses the artifact from
  its own job's pack (no second `dotnet pack`).

**Checkpoint**: A release either publishes the complete in-scope set to both feeds or fails loudly —
never half-published, never gate-skipping (SC-003).

---

## Phase 6: Polish & Cross-Cutting

**Purpose**: Coherence follow-up and end-to-end validation.

- [X] T023 [P] Update the `.github/workflows/publish.yml` header comment to describe the dual-publish
  scope (CLI + reference gate set, org-feed-first then nuget.org via Trusted Publishing) so the file's
  intent matches the extended contract. **DONE**: header rewritten to cover both artifacts, org-feed-first
  → nuget.org via Trusted Publishing, the self-gated gate-set job, the "login+push in THIS file" rule,
  and the dry-run/fail-closed behavior; points at both the 089 and 094 contracts.
- [ ] T024 Run the full `specs/094-nuget-org-publish/quickstart.md` validation (Scenarios 1–7) and
  record real-run evidence against SC-001…SC-006. Include the **cross-job set-completeness** check
  for SC-003: force one package's publish job to fail (e.g. the gate-set job) while the other has
  published, confirm the overall release surfaces as **failed** (never a silent partial success),
  then confirm a re-run completes the missing package idempotently (`--skip-duplicate`) so the public
  set becomes whole (SC-003; FR-006/FR-007; "Re-run after partial failure" edge case). **NOT DONE —
  requires CI.** Local scenarios done: S1 (version + gated pack), S2 (listing metadata in both packed
  artifacts). CI scenarios remaining: S3 (dry-run pushes nothing), S4 (real dual-feed release), S6
  (fail-closed 401), S7 (idempotent re-publish), and the SC-003 cross-job set-completeness check.
- [ ] T025 [P] Coherence follow-up (FR-011; research D8): once both packages resolve on nuget.org at
  their current versions, advance the cross-repo registry id `nuget-org-published` toward
  `coherent: true` and note completion on FS.GG.Governance#41 via the cross-repo protocol (FS-GG/.github
  registry) — coordination action, not a code change in this repo. **NOT DONE — blocked on T011/T018/T024**
  (both packages must actually resolve on nuget.org first). Cross-repo coordination action, tracked
  separately from this repo's code.

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)**: no dependencies — start immediately.
- **Phase 2 (Foundational)**: depends on Phase 1 (icon/README exist) — **BLOCKS US1 & US2** (shared
  metadata).
- **Phase 3 (US1)** and **Phase 4 (US2)**: both depend on Phase 2. They edit the **same file**
  (`.github/workflows/publish.yml`), so run US1 → US2 in priority order (or coordinate edits); each
  remains **independently testable** afterward.
- **Phase 5 (US3)**: verifies properties of the US1/US2 wiring — start after both are in place.
- **Phase 6 (Polish)**: after the desired stories are complete.

### Cross-task dependencies (beyond phase ordering)

- T007, T012 depend on T001/T002 (assets) and T003 (shared props).
- T009 depends on T008 (login step must exist before the push consumes its output).
- T015 depends on T014; T016 depends on T015 (assert-produced before any push).
- T023/T024/T025 depend on US1+US2 shipping.

### Parallel opportunities

- **Phase 1**: T001 ∥ T002 (different files).
- **Phase 2**: sequential (T003 authors the props; T004/T005 verify it).
- **Within US1**: T006/T007 (fsproj) can proceed while T008/T009 (workflow) are drafted; they touch
  different files.
- **Within US2**: T012/T013 (fsproj) vs T014–T016 (workflow) touch different files.
- **US1 vs US2 workflow edits are NOT parallel** — both mutate `publish.yml`.
- **Phase 6**: T023 ∥ T025.

---

## MVP scope

**User Story 1 (Phases 1 → 2 → 3)** is the MVP: the governance CLI becomes publicly installable from
nuget.org with a complete listing and Trusted-Publishing auth, delivering ADR-0012's primary value
(remove the private-feed barrier) independently of the reference gate set. US2 completes the public
surface; US3 is the safety envelope over both.

## Task count per user story

- **Shared (Setup + Foundational)**: 5 tasks (T001–T005)
- **US1 (P1, MVP)**: 6 tasks (T006–T011)
- **US2 (P2)**: 7 tasks (T012–T018)
- **US3 (P3)**: 4 tasks (T019–T022)
- **Polish**: 3 tasks (T023–T025)
- **Total**: 25 tasks
