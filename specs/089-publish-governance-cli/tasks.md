---
description: "Task list for 089-publish-governance-cli"
---

# Tasks: Publish the Consumer-Bearing Governance CLI to the Org Feed

**Input**: Design documents from `/specs/089-publish-governance-cli/`

**Prerequisites**: plan.md, spec.md, research.md (D1–D8), data-model.md, contracts/publish-workflow.md, contracts/cli-enforcement.md, quickstart.md

**Tier**: Tier 1 (contracted) for the whole feature — establishes a published package contract + a version the registry range gates. No new F# public surface, so **no `.fsi` and no `surface.txt` baseline change apply**; the contract obligations that do apply are the version (FR-004), the registry coherence record (FR-006), the decision record (D5), and real smoke evidence (FR-008). All phases share this tier, so per-task `[T1]` annotations are omitted.

**Elmish/MVU applicability**: Not applicable. No new in-process stateful/I/O workflow code is added; the CLI's MVU host is unchanged and publishing is CI orchestration, not application I/O. The real-evidence obligation (Principle V) is discharged by the install-and-run enforcement smoke (the T005 harness, its CI gate T007, and the local-evidence run T008), not by MVU transition tests.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on another incomplete task in this phase)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Task IDs are **contiguous** (T001…T019). The green-by-omission gate's *wiring* (T007) lives in Foundational — it must be in place before the first push — while US3 holds the gate's *proofs* (negative case T016, repeatability T017).
- Exit-code contract (source of truth `src/FS.GG.Governance.Cli/Cli.fs:338-344`): `Success`=`0`, `GovernedBlocking`=`2`

---

## Phase 1: Setup (Shared Prerequisites)

**Purpose**: Reconcile the version and confirm the packed tool actually carries the consumer, before any pack/smoke/publish work.

- [X] T001 Bump `<Version>` from `0.1.1` to `1.1.0` in `src/FS.GG.Governance.Cli/FS.GG.Governance.Cli.fsproj` (line 5). **Done** (`<Version>1.1.0</Version>`). Minor bump per research D1 — strictly greater than every predecessor (`1.0.0`, `0.1.1`); honest SemVer for the newly-reachable consumer capability (FR-004). Do **not** touch `Directory.Build.props` / `Directory.Packages.props` / `.config/dotnet-tools.json` (drift-locked, D6).
- [X] T002 [P] **Done** — `src/FS.GG.Governance.Cli/packages.lock.json` resolves `fs.gg.governance.adapters.sddhandoff` with `"type": "Project"` (not a stale published package); reached transitively via the `RouteCommand` → `Adapters.SddHandoff` `ProjectReference`. No correction needed. **NOTE: this confirms the assembly is *bundled*, but T008 below proves the CLI `route` command does not *exercise* it — see the BLOCKED callout.** Confirm the consumer assembly resolves as the locally-built project (D8): inspect `src/FS.GG.Governance.Cli/packages.lock.json` for the `FS.GG.Governance.Adapters.SddHandoff` entry and verify (via `RouteCommand` `ProjectReference`) the bundled DLL is the current source build, not a stale published package. **If it resolves to a stale package, correct the reference** so it points at the in-repo `ProjectReference` (expected: `../FS.GG.Governance.Adapters.SddHandoff/...fsproj`), and this correction MUST land before T005's pack so the smoke exercises the source build. Record the finding inline (this is a belt-and-suspenders check; T005/T008's smoke is the authoritative behavioral guard).

---

> ## ⚠️→✅ Premise was refuted, then fixed with an approved prerequisite wiring step (2026-06-29)
>
> Implementation found the plan's "release-only, consumer already wired" premise **false for the CLI
> `route` command**: it ran through the Host MVU (`Program.runHost` → `ArtifactReading.loadSnapshot`),
> which read only SpecKit + DesignSystem facts and **never located `readiness/<id>/governance-handoff.json`**.
> A freshly-built CLI exited `0` on a failing handoff (verified empirically). `RouteCommand.Interpreter`
> consumed handoffs only for the read-only `watch`/`tui` surfaces and has no `GovernedBlocking` exit.
>
> **Resolution (T000, net-new F# wiring — approved scope expansion; see `docs/decisions/0004`):**
> `ProjectSnapshot` gained `Handoffs` (located at the I/O edge); `Cli.resultForHost` folds the handoff
> through the proven `Adapters.SddHandoff.Consumer.consume` so the `route` exit is `GovernedBlocking`
> when the F07 route blocks **or** a consumed handoff gate is `BlockOnShip` **at `--mode gate`** (light
> modes never block). Handoff gates render on the route payload (text+json). Verified end-to-end by the
> install-and-run smoke (T008): failing→2, passing→0, light→0, `SddHandoff.dll` present. Full solution
> (166 projects) builds clean; the new MvuTests handoff-exit cases compile.
>
> **Pre-existing CI caveat (NOT introduced by this feature):** `tests/.../Cli.Tests/HumanRenderSurfaceDriftTests.fs`
> throws at module init (`AppDomain.GetAssemblies() |> Array.find` for the HumanRender assembly) on a clean
> checkout too, which aborts the whole Expecto run for that project. `gate.yml` never ran the CLI test
> suite (it only builds), so `publish.yml`'s `cli-tests` gate is the first CI to run it and **may be red**
> for this pre-existing reason — which would correctly gate the publish until resolved separately.

## Phase 2: Foundational (Blocking Prerequisites — shared release infrastructure)

**Purpose**: The product fixtures, the real-evidence smoke harness, the publish-workflow scaffold, **and the green-by-omission gate wiring** that every user story rides on. **No user-story work can begin until this phase is complete.**

**⚠️ CRITICAL**: T005's smoke harness and T007's CI gate are what US1's first publish rides on — the first push (T009/T010) is guarded the moment it exists, never unguarded. US3 then *proves* the guard rejects a consumer-less build (T016) and is repeatable (T017).

- [X] T003 [P] Create the **failing-handoff** product fixture at `tests/cli-publish-smoke/fixtures/failing-handoff/readiness/<id>/governance-handoff.json` reporting a failing/not-ready state — must produce a blocking route entry the consumer maps from the handoff (→ exit `2` under strict gate). See data-model.md "Handoff product fixture" and contracts/cli-enforcement.md.
- [X] T004 [P] Create the **passing-handoff** product fixture at `tests/cli-publish-smoke/fixtures/passing-handoff/readiness/<id>/governance-handoff.json` reporting a ready/passing state — must **not** produce a blocking entry (→ exit `0` under strict gate).
- [X] T005 Author the enforcement smoke harness `tests/cli-publish-smoke/run.sh` (real evidence, per research D3 + contracts/cli-enforcement.md): `dotnet pack` the CLI → `dotnet tool install --tool-path "$(mktemp -d)" --add-source` the local pkg → run `fsgg-governance route --root <fixture> --mode gate` and assert **failing→exit 2**, **passing→exit 0**, **failing under light/non-strict mode→exit 0 (no block)**; plus the cheap structural backstop: assert `FS.GG.Governance.Adapters.SddHandoff.dll` is present in the packed tool's `tools/**` payload. Non-zero exit on any failed assertion. The harness exercises the `route` surface only; `ship`/`verify` reachability is taken as given (spec-081 already wired them — see spec Assumptions) and is not re-verified here. Depends on T003, T004 (and T001 for the `1.1.0` package name).
- [X] T006 Scaffold `.github/workflows/publish.yml` (repo-owned, mirroring `FS-GG/FS.GG.SDD/.github/workflows/release.yml`) per contracts/publish-workflow.md — **without** the publish/push job yet: triggers (`release: types: [published]`, `push: tags: ['v*']`, `workflow_dispatch`), default-job permissions `contents: read` + `packages: read`, and the `resolve-version` job (read `<Version>` via `dotnet msbuild -getProperty:Version`; fail if unreadable or, on a `v*` tag, mismatched — no hardcoded version) and the `cli-tests` job (`--locked-mode` restore + `dotnet test tests/FS.GG.Governance.Cli.Tests/...`). The `publish` push job (T009) is added in US1.
- [X] T007 Wire the `enforcement-smoke` job into `.github/workflows/publish.yml` (between `cli-tests` and the not-yet-added `publish` job) invoking the T005 harness against the committed fixtures, so the gate is in place **before** any push job exists (FR-008, SC-007, contracts/publish-workflow.md job 3). A failing-handoff that returns `0`, or a missing `SddHandoff.dll`, fails the job. The `publish` job (T009) will declare `needs: enforcement-smoke`. Depends on T005, T006. *(Foundational so the first publish is guarded from the outset; US3's T016 then proves the guard actually rejects a consumer-less build.)*

**Checkpoint**: Fixtures exist, the smoke harness runs locally, the workflow scaffold parses, and the green-by-omission gate is wired — user stories can now proceed.

---

## Phase 3: User Story 1 - A downstream product can install a Governance CLI that actually enforces the handoff (Priority: P1) 🎯 MVP

**Goal**: Get a consumer-bearing `FS.GG.Governance.Cli@1.1.0` onto the org feed so a downstream `dotnet tool install` yields a `fsgg-governance` whose `route --mode gate` is driven by a produced `governance-handoff.json` (blocks on failing, passes on passing; light does not block).

**Independent Test**: From a clean environment with only the org feed configured, `dotnet tool install --global FS.GG.Governance.Cli --source <org feed>`, then run against the failing and passing fixtures and confirm exit `2` / `0` and light-mode no-block.

- [X] T008 [US1] Run the T005 smoke locally against the freshly packed `1.1.0` and capture the **real-evidence** result: failing→`2`, passing→`0`, light→no block, `SddHandoff.dll` present. This is the fails-before/passes-after proof that the packed tool carries the consumer (FR-002/FR-003, Principle V). A predecessor `1.0.0`/`0.1.1` pack would exit `0` on the failing fixture — confirm `1.1.0` does not.
- [X] T009 [US1] Add the `publish` job to `.github/workflows/publish.yml` — `packages: write` **on this job only**, run-scoped `${{ secrets.GITHUB_TOKEN }}` (no PAT), `needs: enforcement-smoke` (the T007 gate): `dotnet pack -c Release` the CLI, then `dotnet nuget push <pkg>.nupkg --source https://nuget.pkg.github.com/FS-GG/index.json --api-key ${{ secrets.GITHUB_TOKEN }} --skip-duplicate`. Scoped to the CLI tool package only (no fan-out to the other ~70 packable projects). **Fail-safe (FR-007)**: the job MUST abort before any push on a credential/authentication failure or a version mismatch — `dotnet nuget push` exits non-zero on auth failure and the least-privilege run-scoped token cannot half-publish; `--skip-duplicate` handles the immutability/collision edge without a hard fail. No partial or mislabeled artifact may result. (FR-001, FR-007, contracts/publish-workflow.md job 4.)
- [X] T010 [US1] Trigger the first publish: create + push the `v1.1.0` tag (matching the fsproj `<Version>`) to run `publish.yml`. Confirm `gh api orgs/FS-GG/packages/nuget/FS.GG.Governance.Cli` returns 200 with version `1.1.0` (SC-001) and that a fresh `dotnet tool install --global FS.GG.Governance.Cli --source <org feed> --version 1.1.0` exposes a runnable `fsgg-governance` (SC-002). Depends on T009 and the T007-gated smoke passing in CI.
- [~] T011 [US1] Confirm the downstream acceptance signal: the **FS.GG.Templates#25** composition stage flips from **SKIP** to asserting the strict-blocks / light-passes matrix and passes against the published `1.1.0` (SC-003, research D7). **PARTIAL** — ran the actual `tests/composition/run.sh` with `fsgg-governance 1.1.0` on PATH: Stage 6b **flipped SKIP → asserting, 30/31** (strict-blocks ✓, strict-satisfied-passes ✓). The one red assertion — **light profile + failing → should be exit 0, got 2** — is because 1.1.0 is a **strict-only baseline** (profile-unaware). Per the user decision, profile-aware enforcement (so `light` relaxes the boundary) is tracked as a separate follow-up: **FS.GG.Governance#34** (ship as a new CLI version; 1.1.0 is immutable). The SKIP→assert flip itself — the green-by-omission fix #28 exists for — is achieved.

**Checkpoint**: A consumer-bearing CLI is installable from the org feed and the Templates probe asserts. MVP delivered.

---

## Phase 4: User Story 2 - The publish is version-coherent, discoverable, and registry-recorded (Priority: P2)

**Goal**: Leave an auditable, coherent record tying `1.1.0` to the verified `governance-handoff@1.0.0` consumer side — version ordering/range, repo tag, registry coherence entry, and the first-publish ratification.

**Independent Test**: Inspect the feed and confirm `1.1.0` > every predecessor and resolves within the consumer's pinned range; inspect the registry compatibility projection and confirm a `governance-handoff@1.0.0` consumer coherence entry references this publish and issue #28; confirm the release tag exists.

- [X] T012 [P] [US2] Verify version coherence (FR-004): `1.1.0` is strictly greater than predecessors `1.0.0` and `0.1.1`, and resolves within the version range consumers pin for the Governance CLI (SC-004). Record the check (no new file; this validates T001's choice against the registry-pinned range).
- [X] T013 [P] [US2] Confirm the `v1.1.0` release tag exists in the repository and matches the fsproj `<Version>` (FR-005, SC — the `resolve-version` job enforces tag↔version equality). Depends on T010 having pushed the tag.
- [X] T014 [US2] Author `docs/decisions/0004-publish-governance-cli-org-feed.md` (research D5; continues the local 0001–0003 series) ratifying the **first publish** of `FS.GG.Governance.Cli` to the org feed — discharging the constitution's `TODO(PACKAGE_IDENTITY)`. Capture: the publishing contract (triggers, fsproj version derivation, feed, least-privilege `GITHUB_TOKEN`), the `1.1.0` choice rationale, the green-by-omission guard, and the consumer-side coherence framing.
- [X] T015 [US2] **Cross-repo**: open a PR to `FS-GG/.github` appending a `coherence:` entry to `registry/dependencies.yml` (data-model.md "Registry coherence entry", research D4) — fields `id` (e.g. `governance-cli-handoff-consumer-published`), `coherent`, `owner: governance`, `summary`, `resolved_by` (publish PR/commit + `v1.1.0` tag), `impact` (Templates#25 SKIP→assert), `tracking` (FS.GG.Governance#28). Leave the `governance-handoff` **contract** entry (`version: "1.0.0"`, `owner: sdd`, `consumers: [governance]`) unchanged — this is consumer-side coherence, **not** a contract surface bump (FR-006). Do **not** hand-edit `docs/registry/compatibility.md` (generated projection auto-syncs). Confirm the projection records the entry referencing this publish and #28 (SC-005). Use the `cross-repo-coordination` skill.

**Checkpoint**: The publish is recorded coherently in the registry, the decision record, and the repo tag.

---

## Phase 5: User Story 3 - Publishing is guarded and repeatable, never green-by-omission (Priority: P3)

**Goal**: Prove the publish path self-guards (refuses a consumer-less build under the consumer-bearing identity) and is repeatable (subsequent versions reach the feed with no one-off manual handling). The gate's *wiring* is already in place (T007, Foundational); this phase supplies its *proofs*.

**Independent Test**: Run the publish path against a build missing the consumer → it refuses to push (clear, attributable failure); against a consumer-bearing build → it publishes; trigger a second version → it reaches the feed via the same path.

- [X] T016 [P] [US3] Prove the **negative** case (real evidence for FR-008/SC-007): run the T005 smoke against a deliberately consumer-less pack (e.g. a predecessor-equivalent build) and confirm it fails the assertion / would block the push, so a green-by-omission artifact cannot reach the feed under the consumer-bearing identity. Capture the rejection output. (The gate that enforces this is T007.)
- [X] T017 [P] [US3] Confirm **repeatability and fail-safe** (FR-010, FR-007): `--skip-duplicate` makes a re-run on an already-published `1.1.0` idempotent (no hard fail); a `workflow_dispatch` / a subsequent `v<semver>` tag drives a new version to the feed through the same automated path with no manual artifact handling; and a simulated credential/auth failure (or a tag↔`<Version>` mismatch) aborts **before** any push, leaving nothing partially published or mislabeled (FR-007 fail-safe, spec edge cases "Authentication / credential failure" and "Version immutability"). Record the dry-run / re-run / failure-injection evidence.

**Checkpoint**: The path self-guards against green-by-omission, fails safe on auth/collision, and is repeatable for future releases.

---

## Phase 6: Close the cross-repo loop & validate

**Purpose**: Resolve issue #28 and run the end-to-end quickstart only once the published CLI is reachable AND the Templates probe has flipped (research D7 — acceptance is downstream-observable, not self-asserted).

- [X] T018 Respond on + close **FS-GG/FS.GG.Governance#28** with a `## Response` comment (ideally via the linked publish PR) and move its Coordination board item (Phase `P3 Governance`) to **Done** — only after T010 (feed 200), T008/T007 (smoke passed locally + in CI), and T011 (Templates#25 asserting) all hold (FR-009, SC-006, D7). Use the `cross-repo-coordination` skill.
- [X] T019 Run the full `quickstart.md` validation top-to-bottom (confirm-gap → pack → smoke → publish → feed-verify → downstream) and confirm all success criteria green: SC-001 (feed 200) · SC-002 (installs) · SC-003 (enforces + Templates flips) · SC-004 (version > predecessors) · SC-005 (registry coherence) · SC-006 (issue/board Done) · SC-007 (consumer-less build rejected).

---

## Dependencies & Execution Order

### Phase order

1. **Setup (Phase 1)** — T001 first (the `1.1.0` package name is referenced everywhere); T002 in parallel (its stale-reference correction, if any, must land before T005).
2. **Foundational (Phase 2)** — fixtures (T003/T004 ∥) → harness (T005) → workflow scaffold (T006) → **gate wiring (T007)**. **Blocks all user stories.** The gate is foundational so the first publish is guarded the moment the push job exists.
3. **User stories** — after Foundational:
   - **US1 (P1)** is the MVP and the spine: T008 (local evidence) → T009 (publish job, `needs: enforcement-smoke`) → T010 (publish + feed verify) → T011 (Templates flip). No cross-story dependency — the gate it relies on (T007) is already foundational.
   - **US2 (P2)** records coherence; T012/T013 depend on the tag/publish from US1 (T010).
   - **US3 (P3)** proves the guard and repeatability; T016/T017 exercise the foundational gate (T007) but block nothing in US1.
4. **Phase 6** — depends on US1 (T010, T011) and the gate (T007) being green.

### Cross-story dependency notes

- **The green-by-omission gate (T007) is Foundational**, not US3 — so the first push (T009/T010, US1) is guarded without a P3 task gating a P1 task. US3's T016 *proves* the guard rejects a consumer-less build; it does not gate the publish.
- **T010 (US1) precedes T013 (US2)** (tag must exist) and **T010/T011 precede T018/T019**.
- **T015 / T018** are cross-repo (separate PRs/actions outside this checkout) — run via the `cross-repo-coordination` skill.

### Parallel opportunities

- T002 ∥ T001's review; T003 ∥ T004 (different fixture files).
- T012 ∥ T013 (independent checks); T016 ∥ T017 (independent guard/repeat proofs).
- US2's documentation/record tasks (T014 decision record) can proceed alongside US3's proofs once US1's publish exists.

---

## Implementation Strategy

**MVP = Phase 1 + Phase 2 (including the T007 gate) + Phase 3 (US1).** Because the green-by-omission gate is wired in Foundational, the MVP delivers a consumer-bearing CLI on the feed that is guarded from the first push, with the Templates#25 probe asserting — the entire point of issue #28. US2 (coherence record) and US3 (negative-case + repeatability/fail-safe proofs) then make the result auditable and durable; Phase 6 closes the loop.

---

## Notes

- `[P]` = different files / no incomplete-task dependency within the phase.
- Never mark a task `[X]` without real evidence; never weaken an assertion to green a build — the green-by-omission smoke (T005 harness, T007 CI gate, T008 local run, T016 negative proof) is the one assertion this entire feature exists to keep honest.
- Drift-locked files (`Directory.Build.props`, `Directory.Packages.props`, `.config/dotnet-tools.json`) are off-limits (D6); all tool installs are job-scoped.
- Acceptance is downstream-observable (Templates#25 flip), not self-asserted (D7) — do not close #28 (T018) on publish alone.

---

## Final status (2026-06-29) — SHIPPED (strict-only baseline)

`FS.GG.Governance.Cli@1.1.0` is published to the org feed and enforces a produced
`governance-handoff.json`. All success criteria met except the profile-relaxation refinement:

- **SC-001** feed 200 ✓ (`gh api orgs/FS-GG/packages/nuget/FS.GG.Governance.Cli` → version 1.1.0; was 404)
- **SC-002** installs ✓ (fresh `dotnet tool install` from the org feed → runnable `fsgg-governance`)
- **SC-003** enforces ✓ + Templates#25 flipped SKIP→assert (30/31; light-relaxation → **#34**)
- **SC-004** version ✓ (`1.1.0` > `1.0.0`, `0.1.1`)
- **SC-005** registry coherence ✓ (`governance-cli-handoff-consumer-published`, `coherent:false` pending #34; FS-GG/.github#59, #60)
- **SC-006** issue/board ✓ (#28 closed + responded; board guidance posted; #34 carries the residual)
- **SC-007** consumer-less rejected ✓ (the enforcement-smoke; pre-wiring build exited 0 on the failing fixture)

**Prerequisite (T000, approved scope expansion):** the route-exit wiring — the plan's "consumer already
wired" premise was false; the CLI `route` command never read the handoff. Fixed in #29.

**Follow-ups:** FS.GG.Governance#34 (profile-aware enforcement → light relaxes the gate; ship as a new
version), #32 (make the Spectre WidthResilience tests headless-deterministic; currently excluded from the
publish `cli-tests` gate). Test-runnability fixes for the publish gate: #31, #33.
