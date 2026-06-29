# Tasks: Profile-Aware Handoff-Gate Enforcement

> **Closeout (2026-06-29).** In-repo work **shipped**: the profile-blind `mode = Gate && gateBlocks`
> shortcut is replaced by the canonical `Enforcement.deriveEffectiveSeverity` path parameterized by the
> active policy profile (`Gate → Verify` mapping; absent/unrecognized → `Strict` fail-safe). The
> declared `defaultProfile` is read at the Config-load edge and carried on `ProjectSnapshot`
> (`DefaultProfile`) — a net-new edge wiring step mirroring 089's `Handoffs` addition (ADR 0004), since
> the CLI `route` path did not previously load `.fsgg/policy.yml`. Shipped as
> `FS.GG.Governance.Cli@1.2.0`. Evidence: 9/9 `ProfileAware…` matrix rows green (light+failing flipped),
> full scoped CLI suite (60) green, real packed-tool smoke green at `1.2.0` incl. the new
> `light-failing-handoff` no-block fixture; decision record `docs/decisions/0005`. **No F# surface delta**
> (the name-level `FS.GG.Governance.Cli.surface.txt` baseline is unchanged; the route-exit helpers stay
> absent from `Cli.fsi`). **Remaining (cross-repo / post-publish):** T020 registry coherence note on
> `FS-GG/.github`, T021 publish `1.2.0` via `publish.yml`, T022 Templates#25 Stage 6b green against
> `1.2.0`, T023 close #34 + board → Done. The Coordination board item #34 is moved to reflect
> "implementation shipped, `1.2.0` ready to publish; awaiting publish + downstream verification."

**Input**: Design documents from `/specs/090-profile-aware-handoff-gate/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/profile-aware-gate.md, quickstart.md

**Tier**: **Tier 1 (contracted)** — observable enforcement-behavior change + new published version the
registry range gates. **No new F# public surface** (the decision composes already-public
`Enforcement` functions inside the CLI executable's `route`-exit path), so no `.fsi`/`surface.txt`
baseline delta applies — mirroring 089's Tier-1-no-surface classification. Applicable obligations:
`<Version>` bump, real test evidence, coherence record, decision record, issue/board closure.

**Tests**: Required by the spec (Principle V — fails-before / passes-after matrix test + real packed-tool
smoke). Test tasks are first-class here, not optional.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in this phase (different files).
- **[Story]**: `[US1]`/`[US2]`/`[US3]` traceability. Tier matches the spec's overall Tier 1 throughout
  (no per-phase tier annotation needed).
- Each task names an exact file path.

## Elmish/MVU applicability

The changed decision is a **pure total function** of `(profile, mode, handoff gates)` feeding the
existing MVU route-exit (`resultForHost`/`HostCompleted`). Profile is read at the edge (Config load)
and carried as a value — no new I/O, no new stateful workflow, no new `.fsi`. Principle IV is satisfied
by composition; no new MVU boundary task is required (see T012 note).

---

## Phase 1: Setup (no new infrastructure)

**Purpose**: Confirm the working tree builds and the baseline gap is real before touching code.

- [X] T001 Build the solution to establish a green baseline: `dotnet build FS.GG.Governance.sln -c Release` (quickstart §1). No file changes.
- [X] T002 Confirm the profile-blind shortcut still in place at `src/FS.GG.Governance.Cli/Cli.fs:397-409` (`handoffBlocking = mode = Gate && List.exists gateBlocks gates`) — this is the exact binding US1 replaces. Read-only.

**Checkpoint**: Solution builds; the shortcut to remove is located and understood.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: None beyond Setup. This feature adds **no new types, modules, projects, or `.fsi`**
(data-model.md: "No new types are introduced"). Every dependency — `Enforcement.deriveEffectiveSeverity`,
`recognizeProfile`, `Profile`, enforcement `RunMode`, `EnforcementInput`; `PolicyFacts.DefaultProfile`;
`SddHandoff.Consumer`; `Ship.gateToInput` — is already present and surfaced.

**Checkpoint**: No foundational work — proceed directly to User Story 1.

---

## Phase 3: User Story 1 — The policy profile shifts the gate's blocking boundary (Priority: P1) 🎯 MVP

**Goal**: Make handoff-gate blocking at `route --mode gate` derive from the canonical Phase-5
enforcement core parameterized by the active policy profile — strict tightens (failing → block, exit 2),
light relaxes (failing → advisory, exit 0), satisfied passes under both (FR-001/002/003/005, SC-001).

**Independent Test**: Against a fixed product with a failing handoff, run `route --mode gate` once with
`defaultProfile: strict` (expect exit 2) and once with `light` (expect exit 0); then run both against a
satisfied handoff (expect exit 0 both). Quickstart §2.

### Tests for User Story 1 (write FIRST — must FAIL before T012) ⚠️

- [X] T003 [P] [US1] Add a new test module `ProfileAwareHandoffGateTests.fs` in `tests/FS.GG.Governance.Cli.Tests/` driving the route evaluate path (existing `outcomeByRule`/`resultForHost` surface) against in-memory fixtures; register it in `tests/FS.GG.Governance.Cli.Tests/FS.GG.Governance.Cli.Tests.fsproj` (compile order before `Main.fs`). Name tests `ProfileAware...` so quickstart's `--filter "FullyQualifiedName~ProfileAware"` selects them.
- [X] T004 [US1] In `ProfileAwareHandoffGateTests.fs`, assert the strict rows of contracts/profile-aware-gate.md at `--mode gate`: `strict + failing → GovernedBlocking` (exit 2); `strict + satisfied → Success` (exit 0). (Acceptance 1 & 3.)
- [X] T005 [US1] In `ProfileAwareHandoffGateTests.fs`, assert `light + failing → Success` (exit 0, advisory) and `light + satisfied → Success` (exit 0). The `light + failing` assertion is the **fails-before** evidence — it must fail against today's shortcut (quickstart §1). (Acceptance 2; Invariant 1, 6.)
- [X] T006 [P] [US1] In `ProfileAwareHandoffGateTests.fs`, add a multi-gate case: two consumed handoff gates where one would relax under light — assert the run still blocks iff any gate derives `Blocking` (Invariant 4; spec Edge Case "Multiple consumed handoffs").

### Implementation for User Story 1

- [X] T007 [US1] In `src/FS.GG.Governance.Cli/Cli.fs`, add a module-local mode-mapping helper `route RunMode → Enforcement.RunMode`: `Sandbox → Sandbox`, `Inner → Inner`, **`Gate → Verify`** (research D1; data-model "Mapping"). Keep it total over the 3 route cases. **Principle II**: declare it as a plain `let` with **no** `private`/`internal` modifier — visibility comes from its absence in `Cli.fsi` (the route-exit helpers are not exported), not a keyword. Do not copy the `let private` form of the `gateBlocks` binding being deleted.
- [X] T008 [US1] In `src/FS.GG.Governance.Cli/Cli.fs`, add a module-local profile-resolution helper reading `PolicyFacts.DefaultProfile` via `Enforcement.recognizeProfile`; **absent / missing / unrecognized → `Strict`** (FR-004 fail-safe; resolved in this story so the satisfied-light rows compute, hardened/asserted in US2). Carry the resolved `Profile` as a value from the Config-load edge. **Principle II**: plain `let`, no access modifier (visibility via `Cli.fsi` absence) — same as T007.
- [X] T009 [US1] In `src/FS.GG.Governance.Cli/Cli.fs`, replace `gateBlocks`/`handoffBlocking` (lines ~397-409) with a derivation that, per consumed handoff gate, builds `EnforcementInput { BaseSeverity = (Blocking iff maturity is a block level); Maturity = gate.Maturity; Mode = mappedMode; Profile = resolvedProfile }` (mirroring `Ship.gateToInput`), calls `Enforcement.deriveEffectiveSeverity`, and blocks iff **any** gate's `EffectiveSeverity = Blocking`. Do **not** add a handoff-specific branch — flow through the generic core (Invariant 5; spec Edge Case "no handoff special-casing"). This genericity is verified **by inspection** (the derivation is the generic `deriveEffectiveSeverity` path, identical to `Ship.gateToInput`), not by a test assertion — record that in the T012 evidence note.
- [X] T010 [US1] In `src/FS.GG.Governance.Cli/Cli.fs`, surface the blocking gate's `EnforcementDecision.Reason` so the `GovernedBlocking` exit stays attributable to the failing handoff (Acceptance 1; Invariant 6; Principle VI).
- [X] T011 [US1] Verify other route channels are untouched: the F07 route's own `hasBlockingFailure host.Route host.Facts` channel stays as-is, and `sandbox`/`inner` remain advisory for a failing handoff under every profile (they map below any blocking floor). Confirm by inspection in `src/FS.GG.Governance.Cli/Cli.fs` (spec Edge Case "Mode ≠ gate"; Invariant 3).
- [X] T012 [US1] Run the US1 matrix and confirm all rows pass (light+failing now green): `dotnet test tests/FS.GG.Governance.Cli.Tests/FS.GG.Governance.Cli.Tests.fsproj -c Release --filter "FullyQualifiedName~ProfileAware"`. (No new `.fsi`/MVU boundary — composition only; record this in the evidence note.)

**Checkpoint**: US1 fully functional and independently testable — the profile demonstrably shifts the
gate boundary (SC-001). MVP delivered.

---

## Phase 4: User Story 2 — A product with no declared profile fails safe to strict (Priority: P2)

**Goal**: An undeclared profile (no `defaultProfile`, or no policy at all) gates as **strict** — a
failing handoff still blocks at `route --mode gate`; no green-by-omission path (FR-004/006, SC-002/004).

**Independent Test**: Run `route --mode gate` against a product with no policy profile and a failing
handoff; confirm it blocks (exit 2), identically to explicit strict. Quickstart §3.

> The resolution logic ships in T008; this phase **asserts and protects** the fail-safe with both a
> pure case and the real publish smoke (the 089 regression tripwire).

### Tests for User Story 2 (write FIRST) ⚠️

- [X] T013 [P] [US2] In `tests/FS.GG.Governance.Cli.Tests/ProfileAwareHandoffGateTests.fs`, add the absent-profile rows: `absent + failing → GovernedBlocking` (exit 2) and `absent + satisfied → Success` (exit 0), covering both "no `defaultProfile`" and "no policy declaration at all" (contract matrix; Acceptance 1 & 2; Invariant 2).
- [X] T013b [P] [US2] In the same file, add the **unrecognized-but-declared** fail-safe row: a `defaultProfile` that is present and validates (declared in `profiles:`, so no `DanglingReference`) but is **not** an enforcement-recognized profile (e.g. a custom `"balanced"`), with a failing handoff → `GovernedBlocking` (exit 2) — `recognizeProfile`'s not-recognized result resolves to `Strict`, never relaxing (spec Edge Case "Profile declared but unrecognized"; research D2; Invariant 2). This is the only place the recognized-but-unknown branch is asserted; `absent` (T013) does not exercise it.

### Implementation / Real evidence for User Story 2

- [X] T014 [US2] Create the NEW smoke fixture `tests/cli-publish-smoke/fixtures/light-failing-handoff/` with `.fsgg/policy.yml` (`defaultProfile: light`, plus the `profiles:` entry it references so Config does not emit `DanglingReference`) and a failing `readiness/<id>/governance-handoff.json` (mirror `fixtures/failing-handoff/`).
- [X] T015 [US2] In `tests/cli-publish-smoke/run.sh`, add the light-profile assertion: `light-failing-handoff` + `--mode gate` → **exit 0** (the new behavior). Keep the existing profile-less assertions exactly: `failing-handoff` `--mode gate` → exit 2, `passing-handoff` `--mode gate` → exit 0, `failing-handoff` `--mode inner` → exit 0 (contract real-evidence table; FR-006/SC-004).
- [X] T016 [US2] Run the real packed-tool smoke and confirm all assertions pass (profile-less fixtures still block; new light fixture does not): `bash tests/cli-publish-smoke/run.sh`. Principle V real evidence.

**Checkpoint**: US1 + US2 both hold — fail-safe verified by pure case and by the published-behavior
smoke; the 089 baseline is protected from regression (SC-002/004).

---

## Phase 5: User Story 3 — The profile-aware behavior reaches downstream as a new published version (Priority: P3)

**Goal**: Ship the behavior as a new immutable org-feed version `1.2.0` (>`1.1.0`, in-range), close the
cross-repo loop: Templates#25 Stage 6b fully green, governance-handoff coherence recorded (no surface
bump), decision record written, issue #34 + board resolved (FR-007/008/009/010, SC-003/005/006/007).

**Independent Test**: Inspect the org feed for a version strictly >`1.1.0` resolving within the pinned
range; install downstream and confirm Templates#25 Stage 6b reports all green; confirm #34 closed and
its board item Done. Quickstart §4–6.

> Depends on US1+US2 (the behavior must exist and be verified before it is versioned and published).

### Version & local publish-gate parity

- [X] T017 [US3] Bump `<Version>` in `src/FS.GG.Governance.Cli/FS.GG.Governance.Cli.fsproj` from `1.1.0` → `1.2.0` (FR-007; minor bump — new observable enforcement behavior, no contract-surface change). Verify: `dotnet msbuild src/FS.GG.Governance.Cli/FS.GG.Governance.Cli.fsproj -getProperty:Version` → `1.2.0` (quickstart §4).
- [X] T018 [US3] Run the publish-gate parity locally (both gates, no push): the scoped CLI tests `dotnet test tests/FS.GG.Governance.Cli.Tests/FS.GG.Governance.Cli.Tests.fsproj -c Release --filter "FullyQualifiedName!~WidthResilience"` plus the smoke (T016). Confirm both green (quickstart §5; matches `publish.yml` `cli-tests` + `enforcement-smoke`). `publish.yml` is unchanged.

### Decision & coherence records

- [X] T019 [P] [US3] Write `docs/decisions/0005-profile-aware-handoff-gate-mode-mapping.md` recording the `Gate → Verify` mode mapping (research D1 truth-table proof) and the `absent/unrecognized → Strict` fail-safe (research D2).
- [ ] T020 [P] [US3] Record consumer-side coherence in `FS-GG/.github registry/dependencies.yml` (cross-repo): `governance-handoff` stays `@1.0.0` — a consumer-side enforcement behavior change, **not** a contract version bump (FR-009/SC-006). Separate PR outside this checkout. **STATUS: pending cross-repo PR** — the coherence decision (no surface bump) is recorded in `docs/decisions/0005` (this repo); the `FS-GG/.github` registry note is a separate PR.

### Cross-repo closure (after `1.2.0` is on the feed)

- [ ] T021 [US3] **STATUS: pending publish pipeline** — `1.2.0` is on `main`; the established `publish.yml` path packs from the fsproj and pushes `--skip-duplicate` on its trigger. Publish `1.2.0` to the org feed via the established `publish.yml` path (`--skip-duplicate`; immutable — must not push over `1.1.0`). Confirm it resolves within the consumer-pinned range (FR-007/SC-005; spec Edge Case "Immutable-version collision").
- [ ] T022 [US3] **STATUS: pending (after T021)** — Install `FS.GG.Governance.Cli@1.2.0` downstream and run FS.GG.Templates#25 `tests/composition/run.sh`; confirm Stage 6b reports **all cells passing** — the previously red `light + failing → exit 0` cell is green (FR-008/SC-003; quickstart §6). Cross-repo.
- [ ] T023 [US3] **STATUS: pending (after T022)** — Respond on and close **FS-GG/FS.GG.Governance#34**, and move its Coordination board item to **Done** — only after the Templates#25 matrix is green against `1.2.0` (FR-010/SC-007). Use the `cross-repo-coordination` skill / GitHub.

**Checkpoint**: All stories delivered — behavior shipped as `1.2.0`, downstream matrix green, #34
resolved.

---

## Phase 6: Polish & Validation

- [X] T024 Run the full quickstart "Done when" checklist (unit matrix 9/9 green incl. flipped light+failing; smoke green at `1.2.0` incl. new light no-block + profile-less still block; `<Version>` = `1.2.0`). Templates#25 / #34 / board are the post-publish cross-repo items (T021–T023). Run the full quickstart "Done when" checklist (quickstart §"Done when"): unit matrix green (light+failing flipped), smoke green incl. new light no-block + profile-less still block, `<Version>` = `1.2.0`, Templates#25 green / #34 closed / board Done.
- [X] T025 [P] Update `specs/090-profile-aware-handoff-gate/tasks.md` final status line noting the shipped version and the #34/board resolution (mirroring the 089 tasks.md closeout convention).

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: start immediately.
- **Foundational (Phase 2)**: none — no new types/projects. Does not block.
- **US1 (Phase 3)**: after Setup. The MVP and the core behavior.
- **US2 (Phase 4)**: logic ships in US1's T008; US2's assertions (T013) and smoke (T014–T016) can run
  once T008–T009 land. Independently testable.
- **US3 (Phase 5)**: depends on US1+US2 being green (behavior must exist and be verified before
  versioning/publishing). T021→T022→T023 are strictly sequential (publish → matrix green → close).
- **Polish (Phase 6)**: after US3.

### Within User Story 1

- Tests (T003–T006) written and FAILING before implementation (T007–T012). T005's light+failing row is
  the explicit fails-before signal.
- Mode mapping (T007) + profile resolution (T008) before the derivation (T009); Reason surfacing (T010)
  and channel-isolation check (T011) after the derivation; matrix run (T012) last.

### Cross-task notes

- T009 depends on T007 + T008 (uses both the mapped mode and the resolved profile).
- T015 depends on T014 (asserts the fixture it creates).
- T018 depends on T012 + T016 + T017 (parity needs the tests green and the version bumped).
- T021 depends on T018; T022 depends on T021; T023 depends on T022.
- T020 (registry coherence) and T019 (decision record) are independent of the publish — author anytime
  after US1's design is fixed.

### Parallel opportunities

- **[P] within US1 tests**: T003 and T006 touch the new test file independently of the strict/light row
  authoring; T004/T005 share the file with T003 so sequence them after T003 creates it.
- **[P] across records**: T019 (decision record) and T020 (registry coherence) are different files /
  repos — parallel.
- US1 and US2's pure-test authoring are largely independent once T008 lands, but US2's smoke evidence
  (T016) needs the implementation complete.

---

## Implementation Strategy

### MVP first (US1 only)

1. Phase 1 Setup → 2. (Phase 2 is empty) → 3. US1 (T003–T012). **STOP & VALIDATE**: profile shifts the
   boundary (SC-001), light+failing flipped green. This alone is a demonstrable, independently-testable
   increment.

### Incremental delivery

1. US1 → matrix green (MVP).
2. US2 → fail-safe asserted + real smoke (regression-protected).
3. US3 → `1.2.0` published, downstream matrix green, #34 closed.

---

## Notes

- **Drift-locked files off-limits**: `Directory.Build.props`, `Directory.Packages.props`,
  `.config/dotnet-tools.json` — untouched.
- **No assertion-weakening**: never green a build by relaxing a check. The light+failing row must fail
  before T009 and pass after.
- **One-way fail-safe**: any profile ambiguity resolves to `Strict`, never relaxation (T008).
- Only behavioral source edits live in `src/FS.GG.Governance.Cli/Cli.fs` + the fsproj `<Version>`.
- **Line numbers are a snapshot** (verified accurate at planning time: `Cli.fs:397-409` shortcut, `Consumer.fs:122` maturity, `Enforcement.fs` Verify/Gate ordinals 3/4). They will drift as code shifts — anchor on the named symbols (`handoffBlocking`/`gateBlocks`, `recognizeProfile`, `deriveEffectiveSeverity`) when locating code, not the bare line numbers.
- `publish.yml` is unchanged — it resolves the version from the fsproj and pushes `--skip-duplicate`.
