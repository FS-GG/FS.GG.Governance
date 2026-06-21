# Contract: the ship CI workflow template

Fixes the **required shape** of `docs/ci/templates/fsgg-ship.yml` (and the fenced copy embedded in the
guidance). The template MUST be valid, copyable GitHub Actions content (FR-012) that turns the `fsgg ship`
exit code into an enforced required-status-check gate (FR-001) while staying fail-closed (FR-005) and
surfacing the audit (FR-006). It wires the [exit-code mapping](./exit-code-check-mapping.md); it
re-implements nothing.

## Required elements

1. **Trigger** — `on: pull_request` targeting the protected branch (e.g. `branches: [ main ]`, where the
   branch name is a marked adopter substitution). The **gate job carries no `paths:`/`paths-ignore:` or
   other filter** that could let a governed change skip the required check (FR-005; Edge: required-but-not-run).
2. **Permissions** — least-privilege; the **gate step needs none**. Surfacing may request
   `contents: read` (artifact upload) and, where used, the minimum for a job summary. No permission is
   requested that would make the gate step **skip** on fork PRs.
3. **Checkout** — `actions/checkout@<vN>` with **`fetch-depth: 0`** so base/head sensing has the base ref
   (FR-009; Edge: shallow checkout). The guidance notes an explicit base-ref fetch as an alternative but
   defaults to full history.
4. **Toolchain** — `actions/setup-dotnet@<vN>` pinned to the repo's target SDK. Required by the
   **current** from-source invocation (D1); a comment marks it removable once the packed tool ships.
5. **Gate step** — runs the canonical invocation and lets its exit code be the step status, untranslated:
   - **Today (works now):**
     `dotnet run --project src/FS.GG.Governance.ShipCommand -- ship --mode gate --profile standard --json`
   - **Future (commented placeholder, not yet shipping):** `fsgg ship --mode gate --profile standard --json`
   - **No** `|| true`, **no** `continue-on-error`, **no** exit-code remap (FR-003). This step is the gate.
6. **Audit surfacing step** — a **separate** step with `if: always()` that runs *after* the gate so it
   fires on failure too (FR-006, US3):
   - upload `readiness/audit.json` via `actions/upload-artifact@<vN>`, **and**
   - render a job summary by appending the audit to `$GITHUB_STEP_SUMMARY`.
   - It surfaces the **exact bytes** the command wrote — no re-sort, re-shape, or re-derive (FR-007).
   - It is **best-effort and never gates**: a fork-restricted upload or a missing summary MUST NOT fail
     the job or flip the gate's result (fail-closed surfacing; Edge: fork PRs / restricted token).
7. **Adopter substitutions (clearly marked, FR-012, SC-006)** — at minimum: the protected-branch name in
   the trigger, and the invocation line (from-source now vs packed later). Each is a commented
   `# CHANGE ME:` marker the guidance enumerates.

## Honesty & scope constraints

- **No overclaiming (FR-013):** the template/guidance assert no provenance, attestation, or compliance the
  Phase-2 skeleton does not produce. The gate proves only "the deterministic `fsgg ship` verdict is
  clean."
- **Honest install path (FR-010):** the from-source step is the real current path; the packed `fsgg ship`
  line is a commented placeholder, never a runnable `dotnet tool install`.
- **Deterministic-only blocking (FR-008):** the template wires **only** the exit code to pass/fail. It
  adds no step that blocks on advisory/agent-reviewed findings.
- **No self-gating of this repo:** the file lives under `docs/ci/templates/` as a copy-me example, not in
  this repo's `.github/workflows/` (self-hosting deferred, maintainer-confirmed 2026-06-21).

## Validity & consistency (how this contract is kept honest)

`scripts/check-ship-ci-guidance.sh` (a) parses the template as YAML and, when `actionlint` is on PATH,
validates it against the GitHub Actions schema, and (b) asserts the fenced workflow block in the guidance
matches the template file. A template that is not valid Actions content, or a guidance block that has
drifted from the file, fails the check (FR-012, SC-006).

## Skeleton (illustrative; the delivered `fsgg-ship.yml` is the authority)

```yaml
name: fsgg ship gate
on:
  pull_request:
    branches: [ main ]            # CHANGE ME: your protected branch
permissions:
  contents: read
jobs:
  ship:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0          # required: base/head sensing needs the base ref
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'  # remove once `fsgg ship` ships as a packed tool
      - name: fsgg ship gate       # this step's exit code IS the gate — do not translate it
        run: |
          dotnet run --project src/FS.GG.Governance.ShipCommand -- \
            ship --mode gate --profile standard --json
          # Future packed tool (NOT YET SHIPPING):
          # fsgg ship --mode gate --profile standard --json
      - name: Surface audit.json   # best-effort; never gates
        if: always()
        run: |
          if [ -f readiness/audit.json ]; then
            { echo '### fsgg ship verdict'; echo '```json'; cat readiness/audit.json; echo '```'; } \
              >> "$GITHUB_STEP_SUMMARY"
          fi
      - uses: actions/upload-artifact@v4
        if: always()
        with:
          name: fsgg-audit
          path: readiness/audit.json
          if-no-files-found: ignore
```

Then, in branch protection for the protected branch, mark the **`ship`** job a **required status check**
(FR-002). Once required, a red check (exit `1`/`2`/`3`/`4`) blocks the merge and a green check (exit `0`)
allows it.
