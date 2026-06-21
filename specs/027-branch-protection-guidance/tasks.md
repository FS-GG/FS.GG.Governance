# Tasks: GitHub Actions Branch-Protection Guidance for `fsgg ship`

**Input**: Design documents from `/specs/027-branch-protection-guidance/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/exit-code-check-mapping.md](./contracts/exit-code-check-mapping.md),
[contracts/ship-ci-workflow.md](./contracts/ship-ci-workflow.md), [quickstart.md](./quickstart.md)

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in this phase (different file, no ordering).
- **[Story]**: which user story the task serves (US1–US4).
- Tier annotation omitted throughout: every phase matches the spec's overall classification
  (**Tier 1 — contracted documentation/CI contract**), so no per-task `[T1]`/`[T2]` override is needed.

## Deliverable shape (from plan.md / data-model.md)

This row adds **no F# code, no project, no package, and no `.fsi`**. The deliverable is four artifacts:

| Artifact | Path |
|---|---|
| Guidance (Entity 1) | `docs/ci/github-actions-branch-protection.md` |
| Workflow template (Entity 2) | `docs/ci/templates/fsgg-ship.yml` |
| Validation script — Principle-V evidence | `scripts/check-ship-ci-guidance.sh` |
| README pointer | `README.md` |

**Deliberately unchanged**: `.github/workflows/` stays empty (self-hosting deferred, maintainer-confirmed
2026-06-21); `src/**`, `tests/**`, `surface/**` — `dotnet build`/`dotnet test` must remain byte-for-byte
unchanged (verified in Phase 7).

## Evidence obligations (Principle V; Elmish/MVU note)

- **Principle IV (Elmish/MVU) is N/A here.** No stateful or I/O-bearing F# workflow is authored; the
  stateful host edge (`fsgg ship`) already lives behind the merged F026 MVU boundary. This row only
  *invokes* that command as an external process and reads its exit code/artifact — no `Model`/`Msg`/
  `Effect`/`init`/`update`/interpreter is added.
- **Principle V real evidence (adapted to docs+YAML)** is the validation script
  `scripts/check-ship-ci-guidance.sh`: it (a) lints the template as valid GitHub Actions YAML, (b) asserts
  the fenced block in the guidance matches the template file, and (c) cross-checks every documented exit
  code against the live `Loop.exitCode` (F026) and every named audit field against the F025 contract. It
  fails before the docs are correct and passes after. Any illustrative `audit.json` sample shown carries
  the `Synthetic` token with a use-site disclosure.

---

## Phase 1: Setup (shared structure)

**Purpose**: Create the new `docs/ci/` home and the section skeletons every story fills in.

- [X] T001 Create the directory structure `docs/ci/` and `docs/ci/templates/` at the repository root
  (the repo's first CI-guidance home; see plan.md "Project Structure").
- [X] T002 Create `docs/ci/github-actions-branch-protection.md` with the nine required section headers
  from data-model.md Entity 1 — *Blocking model*, *Exit-code → check mapping*, *Required status check
  setup*, *Checkout requirements*, *Fail-closed wiring*, *Audit surfacing*, *Invocation (honest)*,
  *Honesty boundary*, *Substitutions* — as empty stubs (content authored per story below).
- [X] T003 [P] Create `scripts/check-ship-ci-guidance.sh` as an executable stub (`set -euo pipefail`,
  `chmod +x`) that exits non-zero with a "not yet implemented" message — the evidence harness whose
  assertions are filled in across Phases 2–7.

**Checkpoint**: Files exist; the guidance has its section scaffold and the script is runnable (and red).

---

## Phase 2: Foundational (the shared workflow template)

**Purpose**: Author the one artifact every user story references and embeds. Blocks US1–US4.

**⚠️ The template is shared by all stories** — its gate step (US1), exit-code passthrough (US2),
surfacing step (US3), and deterministic-only wiring (US4) must all land here before the stories'
guidance prose can reference exact lines.

- [X] T004 Author `docs/ci/templates/fsgg-ship.yml` per `contracts/ship-ci-workflow.md` "Required
  elements": `on: pull_request` with `branches: [ main ]` marked `# CHANGE ME:`; **no `paths:`/
  `paths-ignore:`** on the gate job (FR-005); least-privilege `permissions: { contents: read }`;
  `actions/checkout@v4` with `fetch-depth: 0` (FR-009); `actions/setup-dotnet@v4` pinned to `10.0.x`;
  the gate step running `dotnet run --project src/FS.GG.Governance.ShipCommand -- ship --mode gate
  --profile standard --json` with the packed `fsgg ship …` line as a commented placeholder (FR-010);
  and an `if: always()` surfacing step (job summary + `actions/upload-artifact@v4` with
  `if-no-files-found: ignore`) (FR-006). No `|| true`, no `continue-on-error`, no exit-code remap
  (FR-003).
- [X] T005 In `scripts/check-ship-ci-guidance.sh`, implement assertion (a): parse
  `docs/ci/templates/fsgg-ship.yml` as YAML — validate with `actionlint` when on PATH, else fall back to
  a `dotnet fsi` parse referencing the YAML package already on `Directory.Packages.props` with the pinned
  version (`#r "nuget: YamlDotNet, 16.3.0"`) so the fsi run honors the central pin; this is a
  **validation-only consumer**, not a new runtime dependency — the F# solution gains no `PackageReference`
  (FR-011, "no new package"). `log` which path actually ran (`actionlint` vs parse-only fallback) so a
  green run does not overstate validation — parse-only does not check the Actions schema (C2). Fail on
  invalid Actions content (FR-012, SC-006).
- [X] T006 [P] In `scripts/check-ship-ci-guidance.sh`, implement the fail-closed *static* checks over the
  template text: assert the gate step contains no `|| true`, no `continue-on-error`, no exit-code remap
  (FR-003); assert the gate job declares no `paths:`/`paths-ignore:` filter (FR-005). (Independent of
  T005; both edit the same script — coordinate edits, the `[P]` marks logical independence.)

**Checkpoint**: A valid, fail-closed template exists and the script proves its validity. Stories can now
cite its exact steps.

---

## Phase 3: User Story 1 — Make a blocked verdict block the merge (P1) 🎯 MVP

**Goal**: An adopter can wire the ship job as a required status check so a blocked PR can't merge and a
clean PR can.

**Independent Test** (quickstart Sandbox scenario, SC-001): copy the template into a sandbox repo, mark
the `ship` job a required status check; PR with a base-blocking gate → red check, merge blocked; clean PR
→ green check, merge allowed.

- [X] T007 [US1] Author the **Invocation (honest)** section of
  `docs/ci/github-actions-branch-protection.md`: the from-source `dotnet run --project
  src/FS.GG.Governance.ShipCommand -- ship --mode gate --profile standard --json` is the real current
  path; the packed `fsgg ship …` surface is a clearly-marked not-yet-shipping placeholder, never a
  runnable `dotnet tool install` (FR-001, FR-010; Edge: tool not packaged).
- [X] T008 [US1] Author the **Required status check setup** section: step-by-step to (1) copy
  `docs/ci/templates/fsgg-ship.yml` into the adopter's `.github/workflows/`, then (2) mark the `ship`
  job a required status check in branch protection — complete and unambiguous enough to enforce the gate
  with no further design decisions (FR-002; US1 AS3, SC-001).
- [X] T009 [US1] Author the **Substitutions** section: enumerate every `# CHANGE ME:` value an adopter
  must change — at minimum the protected-branch name in the trigger and the invocation line
  (from-source now / packed later) (FR-012, SC-006).
- [X] T010 [US1] In `scripts/check-ship-ci-guidance.sh`, assert each enumerated substitution marker in
  the guidance (T009) actually appears in `docs/ci/templates/fsgg-ship.yml`, so the guidance can't name a
  substitution the template lacks (FR-012, SC-006).

**Checkpoint**: MVP — following only the guidance + template, an adopter can make a blocked verdict block
a merge. This is the deliverable's minimum useful increment.

---

## Phase 4: User Story 2 — Distinguish a blocked merge from a broken tool (P1)

**Goal**: The wiring keeps a blocked verdict (exit `1`) diagnosably distinct from tool failures
(`2`/`3`/`4`), all of which fail the check, and never reports a tool failure as a green merge.

**Independent Test**: trigger a blocked verdict, a shallow/not-a-git failure, a missing/invalid catalog,
and an unrecognized lever; confirm each fails the check, the run identifies *which* category, and no
scenario yields green (SC-002).

- [X] T011 [US2] Author the **Exit-code → check mapping** section of the guidance by reproducing the
  table from `contracts/exit-code-check-mapping.md` (`0`→pass; `1`→fail/blocked verdict; `2`/`3`/`4`→
  fail/tool failure, distinct) and the five invariants (no translation, single blocked code, fail-closed,
  determinism, exit-code-only blocking) (FR-003, FR-004; US2 AS1–AS3).
- [X] T012 [US2] Author the **Fail-closed wiring** section: a tool failure is never a green check; the
  required check is not bypassable for a governed change — no path/event filters on the gate, and the
  gate runs on fork PRs (FR-005; Edge: required-but-not-run, fork PRs).
- [X] T013 [US2] In `scripts/check-ship-ci-guidance.sh`, implement the **contract cross-check**: parse
  the exit-code table the guidance documents and assert codes `0/1/2/3/4` and their meanings equal the
  live `Loop.exitCode` mapping in `src/FS.GG.Governance.ShipCommand/Loop.fs` — so a future F026
  renumber fails the check until the docs are updated (FR-011, FR-014, SC-002). Depends on T011.

**Checkpoint**: The blocked-vs-broken distinction is documented and machine-cross-checked against the
live command.

---

## Phase 5: User Story 3 — Show reviewers why a merge is blocked (P2)

**Goal**: A reviewer reads the verdict and the blockers/warnings/passing partition from the CI run,
without cloning or rerunning, and the surfaced content is the exact `audit.json` the command wrote.

**Independent Test**: trigger a blocked PR; confirm the verdict + partition are readable from the run
(uploaded artifact and/or job summary), a no-hide warning shows both base and effective severity, and the
surfaced bytes match what the command wrote (SC-003).

- [X] T014 [US3] Author the **Audit surfacing** section of the guidance: the `if: always()` step uploads
  `readiness/audit.json` as an artifact **and** renders it into the job summary; it surfaces the exact
  bytes the command wrote (no re-sort/re-shape/re-derive); it is best-effort and never gates — a
  fork-restricted upload or missing summary must not fail the job or flip the gate (FR-006, FR-007; US3
  AS1–AS3; Edge: fork PRs).
- [X] T015 [US3] Document the surfaced `audit.json` field set from data-model.md Entity 4 so a reviewer
  knows what they are reading — `schemaVersion`, `verdict`, `exitCodeBasis`, `blockers[]`, `warnings[]`,
  `passing[]`, item `kind`/`id`/`path`, and the six-field `enforcement` block including the no-hide
  `baseSeverity` vs `effectiveSeverity` case. Any sample shown carries the `Synthetic` token + use-site
  disclosure (FR-006, FR-007).
- [X] T016 [US3] In `scripts/check-ship-ci-guidance.sh`, assert the audit field names the guidance lists
  (T015) equal the F025 contract set in
  `specs/025-audit-json-projection/contracts/audit-json-document.md` — the docs cannot name a field F025
  does not define or drop one it does (FR-007, FR-011, SC-003). Depends on T015.

**Checkpoint**: Enforcement is legible — a reviewer can see *why* from the run, cross-checked against F025.

---

## Phase 6: User Story 4 — Honest scope: only deterministic verdicts block (P2)

**Goal**: The guidance ties branch-protection blocking solely to the deterministic exit code, excludes
uncalibrated agent/advisory findings from blocking, and overclaims nothing.

**Independent Test**: read the guidance and confirm it (a) ties blocking solely to the deterministic exit
code, (b) explicitly states advisory/agent findings are reported but not blocking until calibration
exists, and (c) makes no provenance/attestation/compliance claim (SC-004).

- [X] T017 [US4] Author the **Blocking model** section: blocking derives **solely** from the deterministic
  `fsgg ship` exit code; name no other blocking source (FR-008; US4 AS1).
- [X] T018 [US4] Author the **Honesty boundary** section: advisory/agent-reviewed findings are reported
  but never block until calibration exists; the gate proves only "the deterministic verdict is clean" —
  no provenance, attestation, or compliance claim (FR-008, FR-013; US4 AS2–AS3).
- [X] T019 [US4] Author the **Checkout requirements** section: `fetch-depth: 0` (or an explicit base-ref
  fetch) is required so base/head sensing succeeds rather than failing as a tool error every run (FR-009;
  Edge: shallow checkout). Also note the empty-change / empty-catalog case rolls up to a clean pass — the
  gate does not fail closed on "no governed change" (Edge: empty change).

**Checkpoint**: The honesty boundary is stated; the guidance neither overclaims nor wires a
non-deterministic blocking source.

---

## Phase 7: Polish & cross-cutting evidence

**Purpose**: Embed the template in the guidance, complete the evidence harness, wire the README pointer,
and prove the F# solution is untouched.

- [X] T020 Embed `docs/ci/templates/fsgg-ship.yml` verbatim as a fenced ```yaml block in the guidance,
  and in `scripts/check-ship-ci-guidance.sh` assert the fenced block byte-matches the template file —
  guidance and template cannot drift (data-model.md Entity 2; FR-012, SC-006).
- [X] T021 [P] Add a one-line pointer to `docs/ci/github-actions-branch-protection.md` from `README.md`
  (e.g. in the docs/links area) (plan.md "Source Code / deliverable layout").
- [X] T022 Finalize `scripts/check-ship-ci-guidance.sh`: ensure it exits `0` only when all assertions
  (YAML validity T005, fail-closed static T006, substitution presence T010, exit-code cross-check T013,
  audit-field cross-check T016, fenced-block match T020) pass; print a clear PASS/FAIL summary. Run it and
  capture the green output as the Principle-V evidence (quickstart "Validate (local)").
- [X] T023 [P] Verify the F# solution is byte-for-byte unchanged: run `dotnet build FS.GG.Governance.sln`
  and `dotnet test FS.GG.Governance.sln` and confirm results are identical to pre-F027 (no project added
  or changed); confirm `ls .github/workflows` is still empty/absent (quickstart steps 2–3).
- [X] T024 [P] Run the documented from-source invocation to confirm it still runs and yields a documented
  exit code matching `contracts/exit-code-check-mapping.md` — the exact command the template's gate step
  runs (quickstart "Validate the documented invocation", FR-010). **Evidence (2026-06-21):** `dotnet run
  --project src/FS.GG.Governance.ShipCommand -- ship --mode gate --profile standard --json` ran from source
  and exited **`3` (InputUnavailable)** because this repo has **no `.fsgg` catalog** — exactly the row-3
  mapping (catalog absent → tool failure → red check, fail-closed). The `0`/`1` + `readiness/audit.json`
  path requires a catalog'd repo and is covered by F025/F026's own merged tests; not re-exercised here.
- [X] T025 Walk the quickstart "Acceptance → evidence map" and confirm every spec item (US1–US4, all
  Edge cases, SC-001…SC-006) has the cited evidence present in the delivered guidance/template/script;
  fix any gap before marking the row done. Two criteria are **not** script-checkable and are confirmed by
  inspection here: **SC-001** is validated by the manual sandbox run (T026), and **SC-005 / FR-014
  (re-run determinism)** rests on inherited F026/F025 determinism — confirm by inspection that the
  template adds **no wall-clock- or environment-dependent pass/fail step** (the gate is the command's
  exit code only); record both as inspected, not automated.

---

## Phase 8: End-to-end acceptance (manual — SC-001)

**Purpose**: Validate the headline criterion that the published artifacts actually enforce a merge gate.
This is not script-automatable: it requires a live GitHub repository with branch protection, so it is a
**manual** acceptance run, disclosed as such.

- [-] T026 [US1] **SKIPPED — manual live-GitHub acceptance, infeasible in this environment.** Genuine
  execution needs a throwaway GitHub repository with branch protection and required-status-check config;
  there is no GitHub branch-protection harness in this repo, so this step is disclosed as a manual run the
  maintainer performs out-of-band (the deliverable's tasks.md/plan already flag SC-001 as the one
  not-script-checkable criterion). All script-checkable evidence (Phases 1–7) is green. Original task:
  Execute the quickstart **Sandbox scenario** in a throwaway GitHub repository using
  **only** the published guidance + template: copy `docs/ci/templates/fsgg-ship.yml` into the sandbox's
  `.github/workflows/` (substituting the protected-branch name), mark the `ship` job a required status
  check, then open PR A (base-blocking gate → check **red**, merge **blocked**, exit `1`) and PR B
  (clean → check **green**, merge **allowed**, exit `0`). Record the two outcomes as SC-001 evidence.
  **Disclosed limitation**: this is a manual run against live GitHub — genuine automation is infeasible
  (no GitHub branch-protection harness in this repo), so it carries no script assertion. Depends on the
  full deliverable (Phases 1–7).

**Checkpoint**: SC-001 is demonstrated end-to-end — the gate actually blocks a merge. The row is done.

---

## Dependencies & execution order

- **Phase 1 → Phase 2 → Phases 3–6 → Phase 7 → Phase 8**, sequentially. Phase 8 (T026) is the manual
  end-to-end acceptance and depends on the complete deliverable (Phases 1–7).
- **Phase 2 (template) blocks Phases 3–6**: every story's prose cites exact template steps authored in T004.
- Within stories: T013 depends on T011; T016 depends on T015; T010 depends on T009. Phase 7's T020 and
  T022 depend on all earlier script assertions existing.
- **Script-edit serialization**: T003, T005, T006, T010, T013, T016, T020, T022 all edit
  `scripts/check-ship-ci-guidance.sh`. They are *logically* independent (different assertions) but touch
  one file — apply edits in order rather than truly concurrently even where `[P]` marks logical
  independence.

## Parallel opportunities

- **Across stories**: Phases 3, 4, 5, 6 are independent guidance sections — once Phase 2 lands, US1–US4
  prose can be authored in parallel by different people (each appends a distinct section to the guidance;
  coordinate the single-file writes). Their script assertions must be serialized per the note above.
- **Phase 7**: T021 (README), T023 (solution-unchanged), and T024 (invocation smoke) are fully parallel —
  different files/commands, no shared state.

## Task count per user story

| Story | Priority | Tasks |
|---|---|---|
| Setup (Phase 1) | — | T001–T003 (3) |
| Foundational (Phase 2) | — | T004–T006 (3) |
| US1 — blocked verdict blocks merge | P1 🎯 MVP | T007–T010 (4) |
| US2 — blocked vs broken distinct | P1 | T011–T013 (3) |
| US3 — show reviewers why | P2 | T014–T016 (3) |
| US4 — only deterministic blocks | P2 | T017–T019 (3) |
| Polish (Phase 7) | — | T020–T025 (6) |
| E2E acceptance (Phase 8) | P1 (US1) | T026 (1) — manual sandbox |
| **Total** | | **26** |

## Suggested MVP scope

**Phases 1–3 (T001–T010)** = the MVP: the `docs/ci/` structure, the valid fail-closed workflow template,
its YAML/fail-closed validation, and the US1 guidance (invocation + required-check setup + substitutions).
At that point an adopter can, following only the published guidance and template, make a blocked `fsgg
ship` verdict block a merge (SC-001, demonstrated end-to-end by the manual sandbox run T026) — the whole
point of this closing Phase-2 row. US2 (machine
cross-check of the exit-code taxonomy) is the highest-value next increment, then US3/US4.
