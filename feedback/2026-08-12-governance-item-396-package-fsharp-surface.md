---
feedbackSchema: 2
date: 2026-08-12
workspace: FS.GG.Governance
cycle: item-396-package-fsharp-surface-producer
lane: legacy-spec-kit
toolVersion: n/a
commit: pending-final-handoff
---

## §1 Provenance and confidence

- **activation:** active
- **phases:** onboarding-first-build, lifecycle-authoring-or-not-used, implementation-test-evidence, verify-ship-pr
- **material events:** 2
- **zero-event reason:** n/a
- **Checkpoint:** `feedback/checkpoints/item-396-package-fsharp-surface-producer.jsonl` (1 event)
- **Boundary:** #396, draft PR #397; standard Spec Kit spec/plan/tasks were authored before implementation.
- **Confidence limits:** The coordinator's cross-owner `verify-paths` lookup defect prevents its own evidence read; direct GitHub diff remains available for the handoff.

## §2 What worked

The existing source executable already owned deterministic v1 rendering and atomic persistence, so promoting it to a global tool preserved the evaluator rather than duplicating contract logic.

## §3 What did not

The coordination client attempted to resolve the explicitly named Governance PR through the host default owner, requiring a direct-diff fallback for path evidence. Independent review also found that a locally package-consumable tool had no coherent release publication route until this repair.

## §4 Findings

#### §4.1 Cross-owner PR path verification ignores the explicit repository

- **Kind:** orchestration
- **Impact:** A cross-repository worker cannot use the canonical path verification command as direct proof for its PR.
- **Expected:** `verify-paths --repo FS-GG/FS.GG.Governance` reads PR #397 from that repository.
- **Observed:** The command queried `EHotwagner/FS.GG.Governance` and reported the PR absent.
- **Evidence:** command:scripts/fsgg-coord verify-paths --pr 397 --repo FS-GG/FS.GG.Governance --issue FS-GG/FS.GG.Governance#396
- **Version:** current coordination client, 2026-08-12
- **Owner:** FS-GG/.github coordination client
- **Recurrence:** new; no issue filed because the host owns critic finding disposition.
- **Avoidable cost:** two failed verification attempts
- **Disposition:** product fix

## §5 Did not exercise

Runtime playtest and performance are not relevant to this command-line packaging repair.

## §6 Doc-versus-behavior contradictions

The command contract documents `--repo`; the observed lookup used the host default owner instead.

## §7 Workarounds still in the tree

No product workaround. Handoff records a direct GitHub diff only until the coordinator query is repaired.

## §8 Friction and avoidable cost

Two failed coordinator path-verification attempts; all package tests ran from clean temporary directories.

## §9 Skill value and gaps

Repository-native Spec Kit supplied the lifecycle artifacts. The shared feedback tool captured the coordinator defect; it is not materialized in this repository, so it was invoked from its canonical provider source.

## §10 Outcome markers

Focused DesignChecks command suite: 2 tests passing, including real pack/install/external-consumer and dependency-removal mutation evidence.

## §11 Falsifiable improvements

Fix the coordination PR lookup to honor explicit `--repo`; acceptance is `verify-paths` successfully reads #397 from FS-GG/FS.GG.Governance in a cross-owner host session.

## §12 Development-surface coverage

| Surface | Status | Evidence and result |
|---|---|---|
| scaffolding | partial | Existing repository and isolated worktree were supplied. |
| onboarding-guidance | exercised | `AGENTS.md` and pnext/intra-repo protocol read. |
| skills | exercised | Repository Spec Kit and canonical feedback tool used. |
| sdd-authoring | partial | Not applicable; this repository mandates standard Spec Kit. |
| implementation-apis | exercised | Existing producer was packaged without evaluator duplication. |
| dependencies-build | exercised | Locked restore and packed dependency closure tested. |
| testing | exercised | Focused 2-test suite passed. |
| evidence | exercised | Package content, install, external run, malformed and dependency-removal mutations. |
| runtime-playtest | not-exercised | No interactive runtime surface. |
| performance | not-exercised | No performance claim. |
| documentation | exercised | Package command and exits documented. |
| packaging-upgrade | exercised | Global tool package version 1.12.1 packed and installed. |
| worker-git-pr | exercised | Draft PR #397 and coordinator checkpoint. |
