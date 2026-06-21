# Quickstart: validating the branch-protection guidance (F027)

A runnable validation guide for a **documentation + workflow-template** deliverable. It proves the row's
behavior — a copyable, valid GitHub Actions gate whose documented exit-code/check mapping matches the live
`fsgg ship` (F026) contract and whose audit surfacing matches the F025 document. Pairs with
[plan.md](./plan.md), [data-model.md](./data-model.md), and the two
[contracts/](./contracts/). It references the contracts rather than duplicating them and contains no
workflow body beyond the illustrative skeleton.

## Prerequisites

- The merged F025 (`audit.json` projection) and F026 (`fsgg ship` command) — already in the solution.
- .NET SDK for `net10.0` (to run the from-source invocation and the contract cross-check).
- Optional: `actionlint` on PATH for full GitHub Actions schema validation (the script falls back to a
  YAML parse-only check when it is absent).
- For the sandbox scenario (SC-001): a throwaway GitHub repository you can configure branch protection on.

## Deliverable artifacts

| Artifact | Path | Purpose |
|---|---|---|
| Guidance | `docs/ci/github-actions-branch-protection.md` | The published how-to (blocking model, exit-code map, checkout, surfacing, invocation, honesty boundary). |
| Workflow template | `docs/ci/templates/fsgg-ship.yml` | Copyable gate; mirrored as a fenced block in the guidance. |
| Validation script | `scripts/check-ship-ci-guidance.sh` | Lints the template + cross-checks documented codes/fields vs F026/F025. |
| README pointer | `README.md` | One line linking to `docs/ci/`. |

## Validate (local, no GitHub needed)

```bash
# 1. Lint the template + cross-check the documented contract against the live command.
bash scripts/check-ship-ci-guidance.sh
#    Expected: PASS — template is valid Actions YAML; the fenced block matches the file;
#    documented exit codes 0/1/2/3/4 equal Loop.exitCode; audit field names equal the F025 set.

# 2. Confirm the solution is untouched (this row adds no F# code).
dotnet build FS.GG.Governance.sln
dotnet test  FS.GG.Governance.sln
#    Expected: identical to pre-F027 — no project added or changed.

# 3. Confirm .github/workflows/ is still empty (no self-gating of this repo).
ls .github/workflows 2>/dev/null && echo "UNEXPECTED" || echo "OK: empty/absent"
```

## Validate the documented invocation actually runs today (FR-010, D1)

From a repo with a declared `.fsgg` catalog and a base/head change (the F026 smoke):

```bash
dotnet run --project src/FS.GG.Governance.ShipCommand -- \
  ship --mode gate --profile standard --json
echo "exit=$?"   # 0 clean → green check; 1 blocked → red check; 2/3/4 tool failure → red check
```

Expected: `readiness/audit.json` is written; stdout carries the audit document; the exit code matches
[contracts/exit-code-check-mapping.md](./contracts/exit-code-check-mapping.md). This is the exact command
the template's gate step runs.

## Sandbox scenario (SC-001 — the gate actually blocks a merge)

Following **only** the guidance and template:

1. Copy `docs/ci/templates/fsgg-ship.yml` into the sandbox repo's `.github/workflows/`, substituting the
   protected-branch name.
2. In branch protection, mark the **`ship`** job a **required status check**.
3. Open PR A whose change selects a base-blocking gate → the `ship` check is **red** (exit `1`) and the
   merge is **blocked**.
4. Open PR B with only passing/advisory items → the `ship` check is **green** (exit `0`) and the merge is
   **allowed**.

## Acceptance → evidence map

| Spec item | Evidence |
|---|---|
| US1 (blocked PR can't merge; clean PR can) | Sandbox scenario steps 3–4 (SC-001); template gate step + required-check setup in the guidance |
| US2 (blocked vs tool-failure distinct; never a false pass) | [exit-code-check-mapping.md](./contracts/exit-code-check-mapping.md) invariants 1–3; cross-check in `check-ship-ci-guidance.sh` (SC-002) |
| US3 (reviewer reads verdict/partition from the run) | Surfacing step (`if: always()` artifact + job summary) in [ship-ci-workflow.md](./contracts/ship-ci-workflow.md); F025 field cross-check (SC-003) |
| US4 (only deterministic verdicts block) | Guidance "Blocking model" + "Honesty boundary" sections; FR-008/FR-013 review (SC-004) |
| Edge: shallow checkout | `fetch-depth: 0` required (FR-009, D4); guidance "Checkout requirements" |
| Edge: required-but-not-run / forks | No path filters on the gate; gate runs on forks; surfacing never fails-open (FR-005, D4) |
| Edge: empty change | Clean pass → green (inherited F026/F024); stated in guidance |
| Re-run determinism (SC-005) | Inherited F026/F025 determinism; the wiring adds no clock/env-dependent step (FR-014) |
| Template validity + substitutions (SC-006) | `check-ship-ci-guidance.sh` YAML/actionlint pass; marked `# CHANGE ME:` substitutions enumerated in the guidance |

## Principle-V note (real evidence for docs+YAML)

The validation script is the real-evidence artifact: it fails before the template/guidance are correct and
passes after, and it cross-checks the documented contract against the **live** `Loop.exitCode` (F026) and
the F025 audit field set — so the guidance cannot silently drift from the command it wires (FR-007,
FR-011, FR-014). No synthetic substitute is needed; if any illustrative `audit.json` sample is shown, it
carries the `Synthetic` token and a use-site disclosure per Principle V.
