# Implementation Plan: GitHub Actions Branch-Protection Guidance for `fsgg ship`

**Branch**: `027-branch-protection-guidance` (active spec; git branch currently `main`) | **Date**: 2026-06-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/027-branch-protection-guidance/spec.md`

## Summary

Deliver the **closing row of the Phase-2 Governance Ship Walking Skeleton**: the **published,
reusable guidance and a copyable GitHub Actions workflow template** that turn the merged `fsgg ship`
command (F026) into an **enforced merge gate** on a GitHub protected branch. F026 already produces a
pass/fail merge **verdict**, writes the deterministic `audit.json` (F025), and **exits with a numeric
code CI can block on** (`Clean → 0`, `Blocked → 1`, usage `2`, input-unavailable `3`, tool-error `4`).
What no artifact yet does is **tell an adopting project how to wire that exit code into a GitHub
protected branch** so a blocked verdict actually prevents a merge. That wiring is this row.

The deliverable is **documentation plus a workflow template — no new pure core and no new compiled host
command.** Per the repository's tooling-strategy graduation rule, shell/YAML are allowed for
documentation examples and CI wrappers; the stable contracts they wire (the exit-code taxonomy and the
`audit.json` shape) **already live in the compiled `fsgg ship` command** this guidance merely consumes.
This row **re-defines, re-derives, and re-implements nothing** about the verdict, the audit document, or
the exit-code taxonomy (FR-011). The F# solution is untouched; `dotnet build`/`dotnet test` over the
whole solution must remain byte-for-byte unchanged.

Concretely the guidance shows an adopter how to (1) run `fsgg ship --mode gate --profile standard
--json` in a GitHub Actions job against a pull request's base/head; (2) let the command's **exit code**
drive the job's pass/fail status, untranslated; (3) register that job as a **required status check** so a
non-clean result blocks the merge; (4) keep a **blocked verdict** (`1`) diagnosably distinct from a
**tool failure** (`2`/`3`/`4`) while ensuring neither is ever read as a passing merge (fail-closed); (5)
**surface the `audit.json`** verdict and its blockers/warnings/passing partition to reviewers from the CI
run (artifact upload + a rendered job summary) without a local rerun; and (6) state the **honesty
boundary** — protected-branch blocking comes only from the deterministic verdict; advisory/agent-reviewed
findings are reported but never the basis for blocking until calibration exists.

**Confirmed during planning (the plan-time reconciliations the spec deferred — research D1–D4):**

- **Invocation surface (D1)**: The template invokes the command **from source as it exists today** —
  `dotnet run --project src/FS.GG.Governance.ShipCommand -- ship --mode gate --profile standard --json`
  (the F026 quickstart's canonical smoke). Because `fsgg ship` is **not yet a packed tool** (the
  single-packed-`fsgg`-tool unification is a deferred F026 follow-up), the canonical packed surface
  (`fsgg ship …`) appears only as a **clearly-marked, commented placeholder** an adopter switches to once
  the tool ships — never presented as if it already installs (FR-010).
- **Deliverable home (D2)**: Guidance lands as **`docs/ci/github-actions-branch-protection.md`**; the
  copyable workflow lives as a **template file at `docs/ci/templates/fsgg-ship.yml`** (a copy-me example,
  not an active workflow) and is reproduced as a fenced block in the guidance. The repo's
  `.github/workflows/` **stays empty** — placing a runnable workflow there would gate *this* repo's own
  `main`, which the maintainer ruled out of scope on 2026-06-21 (self-hosting deferred). README gains a
  short pointer to the new guidance.
- **Testing approach (D3)**: Real-evidence discipline (Principle V) applied to a docs+YAML artifact, not
  F#/FsCheck. A repo-owned validation script **`scripts/check-ship-ci-guidance.sh`** (a) lints the
  template as valid GitHub Actions YAML (via the .NET YAML parser already on the `Directory.Packages`
  list, or `actionlint` when present) and (b) **cross-checks** that every exit code the guidance documents
  matches the source-of-truth F026 mapping in `Loop.exitCode` and that the audit fields it names match the
  F025 contract — so the docs cannot silently drift from the command they wire (FR-007, FR-011, FR-014).
- **Checkout & fail-closed wiring (D4)**: `actions/checkout` with **`fetch-depth: 0`** (full history) so
  base/head sensing succeeds rather than failing as a tool error every run (FR-009, Edge: shallow
  checkout). The gate uses the plain `pull_request` trigger with **no path/event filters on the gate job**
  so a governed change cannot skip the required check (FR-005, Edge: required-but-not-run); audit
  *surfacing* steps degrade gracefully on fork PRs but the **pass/fail check never skips** (FR-005, Edge:
  fork PRs).

## Technical Context

**Language/Version**: None added. The deliverable is Markdown documentation + a GitHub Actions YAML
workflow template. The *wired* contract is the existing F# `net10.0` `fsgg ship` command (F026); this row
adds no F# code, no project, and no package.

**Primary Dependencies**: The **merged `fsgg ship` command** (`src/FS.GG.Governance.ShipCommand`, F026)
and its `audit.json` projection (`FS.GG.Governance.AuditJson`, F025) — consumed, not modified. Adopter
runtime: a GitHub Actions runner with the .NET SDK (`actions/setup-dotnet`) to build/run the command from
source today. Validation-script tooling: a YAML parser (`actionlint` if installed, else a `dotnet fsi`
parse using the YAML package already referenced by the Config core) — no **new** third-party package.

**Storage**: N/A as a product surface. The workflow writes the command's `readiness/audit.json` on the
runner and uploads it as a build artifact; the guidance never re-shapes those bytes.

**Testing**: A validation script (`scripts/check-ship-ci-guidance.sh`) proving (1) the template is valid,
copyable GitHub Actions content and (2) the documented exit-code/check mapping and audit field set equal
the live F026/F025 contract. This is the Principle-V "real evidence" form for a docs+YAML artifact (spec
"Testing a documentation/template deliverable" assumption) — not F#/FsCheck unit tests of a new core.

**Target Platform**: GitHub Actions + GitHub branch protection (required status checks). Other CI systems
are explicitly out of scope for this first guidance (spec "Platform" assumption).

**Project Type**: Documentation + CI workflow template (a consumer-facing deliverable). No source project
is added or changed.

**Performance Goals**: N/A. The gate's contract is **determinism and a stable exit code** (inherited from
F026/F025), not latency.

**Constraints**: Fail-closed (FR-005): a tool failure is never a green check and the required check is not
bypassable via fork PRs or path/event filters. Exit codes are passed through untranslated (FR-003).
Blocked (`1`) stays diagnosably distinct from tool failures (`2`/`3`/`4`) (FR-004). The surfaced audit is
the **exact** `audit.json` the command wrote (FR-007). Blocking is tied **solely** to the deterministic
exit code; advisory/agent findings are reported, never blocking (FR-008). No overclaiming of
provenance/attestation/compliance (FR-013). Re-running over the same commit yields the same verdict, exit
code, and check outcome (FR-014).

**Scale/Scope**: One guidance document, one copyable workflow template (also embedded in the guidance),
one validation script, and a short README pointer. Zero changes to the F# solution; `.github/workflows/`
remains empty.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **N/A (no F# surface added)** | This row adds no `.fs`/`.fsi` and no public API. The FSI-first discipline governs F# cores; here the analogue is "contract-first": the guidance is written against the *already-FSI-validated* F026 `Loop.exitCode` surface and F025 `audit.json` contract, and the validation script cross-checks the docs against that fixed surface. |
| II. Visibility in `.fsi` | **N/A** | No F# module is added or changed; no `.fsi` to curate, no surface baseline to move. |
| III. Idiomatic Simplicity | PASS | A plain Markdown doc + a minimal, idiomatic GitHub Actions workflow (checkout → setup-dotnet → run → upload/summarize) + a small shell validation script. No cleverness; the workflow uses standard actions and the command's own exit code. |
| IV. Elmish/MVU is the boundary for stateful/I/O | **N/A** | No stateful F# workflow is authored here. The stateful host edge (`fsgg ship`) already lives behind the F026 MVU boundary; this row only *invokes* it as an external process and reads its exit code/artifact. |
| V. Test Evidence Is Mandatory | **PASS (adapted to docs+YAML)** | Evidence = the validation script: YAML validity of the template + a cross-check that documented codes/fields equal the live F026/F025 contract (fails before the docs are correct, passes after). Real evidence preferred; any synthetic sample carries the `Synthetic` token and a use-site disclosure. |
| VI. Observability & Safe Failure | **PASS — load-bearing** | The wiring is fail-closed: the run reports *which* category occurred (blocked vs each tool-failure code), never greens a tool failure, and never lets a governed change bypass the required check (FR-004/FR-005). The guidance makes the blocked-vs-broken distinction legible from the run (US2, US3). |
| Change Classification | **Tier 1 (contracted change — documentation/CI contract)** | It publishes a new **consumer-facing contract**: the exit-code→check-status mapping, the required-check wiring, and the audit-surfacing steps. No F# public API changes, so no `.fsi`/baseline updates — but it is contracted (not internal cleanup), so it carries the full doc+evidence chain: spec, plan, guidance doc, template, validation script. |
| Engineering Constraints | PASS | No new third-party `PackageReference`; no rendering package IDs/paths assumed (the template is generic, with clearly-marked adopter substitutions); honors the tooling-strategy graduation rule (YAML/shell for CI wiring of an already-compiled contract). `.github/workflows/` stays empty (no self-gating; self-hosting deferred per maintainer). |

**Gate result: PASS — no unjustified violations. Complexity Tracking remains empty.** The N/A rows are
not waivers: they record that the F#-surface principles have no applicable target in a docs+YAML row that
adds no F# code, while V and VI are honored in their docs-appropriate form.

## Project Structure

### Documentation (this feature)

```text
specs/027-branch-protection-guidance/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 — the plan-time reconciliations (D1–D4)
├── data-model.md        # Phase 1 — the entities: exit-code→check map, audit surfacing, guidance structure
├── quickstart.md        # Phase 1 — how to validate the deliverable (lint + contract cross-check + sandbox)
├── contracts/           # Phase 1 — the consumer-facing contracts this row publishes
│   ├── exit-code-check-mapping.md   # exit code → check status → merge outcome (wires F026, re-derives nothing)
│   └── ship-ci-workflow.md          # the workflow template contract: required steps, substitutions, fail-closed rules
├── checklists/
│   └── requirements.md  # (already present) spec quality checklist — all items pass
└── tasks.md             # Phase 2 — /speckit-tasks output (NOT created here)
```

### Source Code / deliverable layout (repository root)

```text
docs/ci/                                         # NEW — first CI-guidance home in the repo
├── github-actions-branch-protection.md          # NEW — the published guidance (blocking model, exit-code
│                                                 #   mapping, checkout reqs, audit surfacing, invocation,
│                                                 #   honesty boundary); embeds the template as a fenced block
└── templates/
    └── fsgg-ship.yml                             # NEW — copyable GitHub Actions workflow template
                                                  #   (a copy-me example, NOT an active workflow)

scripts/
└── check-ship-ci-guidance.sh                     # NEW — validates the template (YAML/actionlint) and
                                                  #   cross-checks documented codes/fields vs F026/F025

README.md                                         # CHANGED — short pointer to docs/ci/ guidance

# Deliberately UNCHANGED:
.github/workflows/                                # stays empty — no workflow gates THIS repo (self-hosting deferred)
src/** , tests/** , surface/**                    # no F# code, project, or surface baseline changes
```

**Structure Decision**: A documentation-and-template deliverable, not a new project. The guidance and its
copyable workflow live under `docs/ci/` (the repo's first CI-guidance home); the workflow is a template
file an adopter copies into *their* `.github/workflows/`, not a live workflow in this repo. A single small
validation script provides the Principle-V real evidence by lint-checking the template and cross-checking
the documented contract against the live F026 (`Loop.exitCode`) and F025 (`audit.json`) surfaces. The
F# solution is untouched.

## Complexity Tracking

> No Constitution violations to justify — this section is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
