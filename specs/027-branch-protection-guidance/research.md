# Phase 0 Research: Branch-Protection Guidance for `fsgg ship`

Resolves the four plan-time reconciliations the spec deferred (Assumptions: "Invocation surface",
"Deliverable home", "Testing a documentation/template deliverable") plus the fail-closed wiring questions
the Edge Cases raise. Each decision is grounded in a merged artifact (F025/F026) or a maintainer-confirmed
scope statement, so the planning gate stays free of NEEDS CLARIFICATION.

## D1 — Invocation surface: how a CI job obtains and runs `fsgg ship` today

- **Decision**: The template invokes the command **from source as it exists today**:
  `dotnet run --project src/FS.GG.Governance.ShipCommand -- ship --mode gate --profile standard --json`,
  after `actions/setup-dotnet`. The **canonical packed surface** (`fsgg ship --mode gate --profile
  standard --json`) is shown only as a **commented, clearly-marked placeholder** the adopter switches to
  *once the packed tool ships* — never as a runnable `dotnet tool install` step.
- **Rationale**: F026 confirms `fsgg ship` is **not yet a packed tool** — the single-packed-`fsgg`-tool
  unification (one tool dispatching the `route` and `ship` verbs) is an explicitly deferred follow-up, and
  the F026 project is `IsPackable=false` this slice. The F026 quickstart already documents the
  build-from-source smoke (`dotnet run --project src/FS.GG.Governance.ShipCommand -- ship …`), so the
  template wires the **exact invocation that works today**. Presenting a `dotnet tool install fsgg`
  install path would be the overclaim FR-010 forbids.
- **Alternatives considered**: (a) *Document only the future packed command* — rejected: it would not run
  today and violates FR-010 (no not-yet-shipping install path presented as available). (b) *Publish a
  composite action / Docker image that packages the build* — rejected: out of this row's scope (it would
  add a shipped artifact and a maintenance surface beyond "guidance + template"), and it would still be
  build-from-source under the hood. (c) *Pin to a prebuilt binary release* — rejected: no release/
  provenance artifact exists yet (deferred, per spec).
- **Honesty boundary applied**: the guidance states plainly that the from-source invocation is the
  current path and the packed `fsgg ship` line is a placeholder, so an adopter is never misled about what
  ships.

## D2 — Deliverable home: where the guidance and template land

- **Decision**: Guidance → **`docs/ci/github-actions-branch-protection.md`**. Copyable workflow →
  **`docs/ci/templates/fsgg-ship.yml`** (a copy-me example file), also embedded verbatim as a fenced block
  inside the guidance. README gains a one-line pointer to `docs/ci/`. **`.github/workflows/` stays empty.**
- **Rationale**: The repo has **no `.github/` workflows at all** — this is the *first* GitHub Actions
  guidance it publishes. The maintainer confirmed on 2026-06-21 that this row ships *consumer-facing
  guidance + a copyable template only* and does **not** add a live workflow gating this repo's own `main`
  (self-hosting deferred). Putting a runnable file under this repo's `.github/workflows/` would
  immediately gate `main` against an unpacked tool — exactly what was ruled out. A template under
  `docs/ci/templates/` is copyable, lintable, and inert in this repo. Co-locating the doc and template
  under `docs/ci/` makes the first CI-guidance home discoverable and leaves room for future CI topics.
- **Alternatives considered**: (a) *`.github/workflows/fsgg-ship.yml` as a real workflow* — rejected:
  self-gates this repo (out of scope) and would fail on an unpacked tool. (b) *`.github/workflow-templates/`*
  — rejected: that location is meaningful only in an **org `.github` repo** for the "New workflow" UI, not
  for a tool repo; it would imply an org-template contract this row does not own. (c) *Template only as a
  fenced block in the doc (no standalone file)* — rejected: a standalone file is directly copyable and
  *lintable* (D3); we keep both (file is source of truth, the fenced block mirrors it and the validation
  script asserts they match).

## D3 — Testing a documentation/template deliverable (Principle V, real evidence)

- **Decision**: Add **`scripts/check-ship-ci-guidance.sh`** that fails before the deliverable is correct
  and passes after, doing two things:
  1. **YAML/Actions validity** — parse `docs/ci/templates/fsgg-ship.yml` as YAML and, when `actionlint`
     is available on PATH, run it for GitHub-Actions-schema validation; otherwise fall back to a parse-only
     check using the YAML parser already on the solution's package list. Also assert the fenced block in
     the guidance matches the template file byte-for-byte.
  2. **Contract cross-check** — assert every exit code the guidance/contract documents (`0/1/2/3/4` with
     their meanings) equals the **source-of-truth** mapping in `src/FS.GG.Governance.ShipCommand`
     (`Loop.exitCode`: `Success 0 | Blocked 1 | UsageError' 2 | InputUnavailable 3 | ToolError 4`), and
     that the audit field names the guidance surfaces (`schemaVersion`, `verdict`, `exitCodeBasis`,
     `blockers`, `warnings`, `passing`, and the six enforcement fields) match the F025 contract. This
     catches doc drift from the wired command.
- **Rationale**: The spec's "Testing a documentation/template deliverable" assumption asks for exactly
  this: prove the template is valid, copyable Actions content and that the documented exit-code/check
  mapping matches the actual F026 contract — Principle V real-evidence discipline applied to docs+YAML
  rather than F#/FsCheck of a new core (there is no new core). The cross-check is what makes FR-007/FR-011/
  FR-014 enforceable: the guidance cannot silently disagree with the command it wires.
- **Alternatives considered**: (a) *No automated check — review only* — rejected: violates Principle V
  (behavior-changing/contract-publishing work needs evidence that fails before and passes after) and lets
  the docs drift from `Loop.exitCode`. (b) *Add an F# test project that shells out to the command* —
  rejected: heavier than the row warrants, and would create a project this row's scope says it does not
  add; a shell script cross-checking the fixed contract is sufficient and idiomatic for a docs row. (c)
  *Schema-validate against a downloaded GitHub Actions JSON schema in CI* — kept as the `actionlint`
  branch when available; not made a hard dependency so the check runs offline.

## D4 — Checkout depth & fail-closed wiring (Edge Cases)

- **Decision (checkout)**: The template uses `actions/checkout` with **`fetch-depth: 0`** (full history)
  so the base ref is present for base/head sensing; the guidance calls this out as a required, non-optional
  setting. Where a shallower fetch + explicit base-ref fetch would suffice, the guidance notes it but
  defaults to `fetch-depth: 0` for correctness.
- **Decision (trigger & filters)**: The gate runs on the plain **`pull_request`** trigger targeting the
  protected branch, with **no `paths:`/`paths-ignore:` or event filters on the gate job**, so a governed
  change can never skip the required check (FR-005; Edge: required-but-not-run). The guidance explicitly
  warns against adding path filters to the gate.
- **Decision (forks)**: Use `pull_request` (not `pull_request_target`). The **pass/fail check itself
  needs no secrets** — it builds and runs the command and exits — so it runs on fork PRs and cannot be
  bypassed by opening from a fork. The **audit-surfacing** steps (artifact upload / job-summary / PR
  annotations) may be constrained for forks; the template makes surfacing **best-effort and isolated** (it
  never gates the check and never uses a permission that would make the check skip on forks). At minimum
  `audit.json` is written to the runner and a job-summary render is attempted; richer surfacing degrades
  without failing-open.
- **Decision (exit-code passthrough)**: The run step invokes the command directly so its process exit code
  becomes the step's status — **no `||`, no `continue-on-error`, no exit-code remapping** (FR-003). A
  separate, **always-run** surfacing step (`if: always()`) uploads/render the audit *after* the gate step,
  so surfacing happens on failure too without masking the gate's status.
- **Decision (clean/empty change)**: A PR that routes nothing, or a valid empty catalog, rolls up to a
  clean pass (`exit 0`) — the check is green and merge is allowed; the gate does **not** fail-closed on
  "no governed change" (Edge: empty change). This is inherited F026/F024 behavior; the guidance states it
  so adopters don't add a spurious "must touch governed paths" guard.
- **Rationale**: Each item is a direct reading of an Edge Case plus the F026 contract; together they make
  the gate fail-closed (FR-005) and non-flaky (FR-014) while keeping blocked-vs-broken legible (FR-004).
- **Alternatives considered**: (a) *Default `actions/checkout` (shallow)* — rejected: base ref often
  absent ⇒ the command fails as a tool error on every run (Edge: shallow checkout). (b)
  *`pull_request_target` for richer fork token* — rejected: it runs in the base repo's context with
  secrets against untrusted head code — a security footgun and unnecessary, since the gate needs no
  secrets. (c) *`continue-on-error` + a parsing step that decides pass/fail* — rejected: it re-derives the
  verdict the command already fixed (violates FR-003/FR-011) and risks reading a tool failure as green
  (violates FR-005).

## Consumed contracts (fixed upstream — this row re-derives none of them)

- **Exit-code taxonomy (F026)**: `Loop.exitCode` → `Success 0 | Blocked 1 | UsageError' 2 |
  InputUnavailable 3 | ToolError 4`. `Blocked = 1` is the single code reserved for a blocked merge,
  distinct from every tool-failure code. (`specs/026-fsgg-ship-command/`.)
- **Canonical invocation (F026/design)**: `fsgg ship --mode gate --profile standard --json`; defaults are
  `--mode gate --profile standard` when flags are omitted.
- **`audit.json` document (F025)**: top-level `schemaVersion` (`"fsgg.audit/v1"`), `verdict`
  (`pass`|`fail`), `exitCodeBasis` (`clean`|`blocked`), and the `blockers`/`warnings`/`passing` partition;
  each item is a tagged `gate`/`finding` carrying the six enforcement fields (`baseSeverity`, `maturity`,
  `mode`, `profile`, `effectiveSeverity`, `reason`). No-hide guaranteed: a relaxed base-blocking item
  appears in `warnings` with both base and effective severity. (`specs/025-audit-json-projection/`.)
- **Honesty boundary (design)**: protected-branch blocking is deterministic-only until calibration
  exists; advisory/agent-reviewed findings are reported, never blocking
  (`docs/initial-implementation-plan.md` Phase-11/agent-review notes).

**Output**: all four reconciliations resolved; no NEEDS CLARIFICATION remains. Ready for Phase 1.
