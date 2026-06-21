# GitHub Actions branch-protection guidance for `fsgg ship`

How to turn the `fsgg ship` merge verdict into an **enforced merge gate** on a GitHub
protected branch. Copy the [workflow template](#the-workflow-template), wire it as a
**required status check**, and a blocked verdict will actually prevent a merge.

This guidance **documents and wires** the existing command; it re-derives nothing. The
exit-code taxonomy lives in `Loop.exitCode` (F026) and the `audit.json` shape lives in
the F025 contract — this page consumes both. `scripts/check-ship-ci-guidance.sh`
cross-checks every code and field named here against those live sources, so the docs
cannot silently drift from the command they wire.

## Blocking model

Branch-protection blocking derives **solely** from the deterministic `fsgg ship`
process exit code. There is **no other blocking source**: not advisory findings, not
agent-reviewed findings, not heuristics, not the contents of the rendered job summary.
The required status check goes green only when the command exits `0`, and red on any
non-zero exit. Wire one thing — the exit code — and nothing else into pass/fail.

This is what makes the gate honest and reproducible: the same commit yields the same
exit code yields the same check outcome, every run, regardless of runner, clock, or
environment.

## Exit-code → check mapping

The command's process exit code *is* the GitHub Actions step result — passed through
untranslated. The authority is `Loop.exitCode` in `src/FS.GG.Governance.ShipCommand`
(F026); this table documents it.

| `fsgg ship` exit code | F026 meaning | GitHub Actions step result | Required-check status | Merge | Run category (diagnosable) |
|---|---|---|---|---|---|
| `0` | `Success` — clean verdict | success | passing (green) | **allowed** | clean |
| `1` | `Blocked` — blocked merge verdict | failure | failing (red) | **blocked** | **blocked verdict** |
| `2` | `UsageError'` — usage error (e.g. unrecognized lever, `--paths`+`--since` together) | failure | failing (red) | **blocked** | tool failure (usage) |
| `3` | `InputUnavailable` — input unavailable (e.g. not a git repo, unresolved/shallow base, catalog absent) | failure | failing (red) | **blocked** | tool failure (input) |
| `4` | `ToolError` — tool error (e.g. unwritable output) | failure | failing (red) | **blocked** | tool failure (tool) |

**Invariants the wiring preserves:**

1. **No translation.** The process exit code is the step result. Never use `|| true`,
   `continue-on-error: true`, an `if:` that swallows non-zero, or any numeric remap. A
   non-zero exit ⇒ a red required check ⇒ a blocked merge.
2. **A single blocked code.** `1` means "this change may not merge" and **only** that.
   It is distinct from every tool-failure code (`2`/`3`/`4`). A reader of the run can
   tell a *blocked verdict* from a *tool failure* (and which category) from the exit
   code and the command's diagnostic — without rerunning locally.
3. **Fail-closed.** A tool failure (`2`/`3`/`4`) is **never** reported as a passing
   merge. No exit code other than `0` yields a green check.
4. **Determinism.** Re-running over the same commit yields the same exit code and the
   same check outcome (inherited from F026/F025). The wiring adds no wall-clock- or
   environment-dependent pass/fail step.
5. **Blocking is exit-code-only.** Branch-protection blocking derives solely from this
   exit code. Advisory or agent-reviewed findings may be reported in the run but are
   never wired to fail the check until calibration exists.

## Required status check setup

Following only this guidance and the template, an adopter can enforce the gate with no
further design decisions:

1. **Copy the workflow.** Copy [`docs/ci/templates/fsgg-ship.yml`](./templates/fsgg-ship.yml)
   into your repository's `.github/workflows/` directory (any filename, e.g.
   `.github/workflows/fsgg-ship.yml`).
2. **Substitute the protected-branch name.** Change the `branches: [ main ]` value
   under the `pull_request` trigger to your protected branch (see [Substitutions](#substitutions)).
3. **Push to the default branch** so GitHub registers the workflow, then open one PR so
   the `ship` job runs at least once (a check must have run before it can be required).
4. **Mark it required.** In **Settings → Branches → Branch protection rules** for the
   protected branch, enable **Require status checks to pass before merging** and add the
   **`ship`** job (the GitHub check name matches the `jobs.<id>` key — `ship`).

Once required: a red `ship` check (exit `1`/`2`/`3`/`4`) blocks the merge; a green check
(exit `0`) allows it.

## Checkout requirements

The gate uses `actions/checkout` with **`fetch-depth: 0`** (full history) so the
command's base/head sensing has the base ref. A **shallow** checkout (the Actions
default of depth 1) leaves the base ref unresolved, and the command reports that as a
tool failure (`InputUnavailable`, exit `3`) — a red check on **every** run, not a real
verdict. Full history avoids that. An explicit base-ref fetch is a valid alternative,
but `fetch-depth: 0` is the simple default this guidance recommends.

**Empty change / empty catalog** is *not* a failure: when there is no governed change to
evaluate, the verdict rolls up to a **clean pass** (exit `0`, green) — inherited from
F024/F026. The gate does not fail closed on "nothing to govern."

## Fail-closed wiring

The required check must never be a false green and must never be bypassable for a
governed change:

- **A tool failure is never green.** Only exit `0` passes; `2`/`3`/`4` all fail the
  check (see the [mapping](#exit-code--check-mapping)). The gate step carries no
  `|| true`, no `continue-on-error`, no remap.
- **No filters on the gate.** The gate job declares **no** `paths:`/`paths-ignore:` or
  other event filter that could let a governed change skip the required check. If the
  job is skipped, GitHub records the required check as *not run* — and a change could
  slip through. Keep the gate filter-free.
- **Runs on fork PRs.** The gate step requests no permission that would make it skip on
  pull requests from forks. The pass/fail check runs regardless of fork origin.
- **Surfacing never fails open *or* flips the gate.** The audit-surfacing steps are
  best-effort and `if: always()`; a fork-restricted artifact upload or a missing job
  summary must never fail the job *and* must never turn a red gate green. The gate's
  result is the gate step's exit code, full stop.

## Audit surfacing

So a reviewer can see *why* a merge is blocked without cloning or rerunning, the
template adds a **separate** step (after the gate, `if: always()` so it fires even when
the gate failed) that:

- **Uploads** `readiness/audit.json` as a build artifact (`actions/upload-artifact` with
  `if-no-files-found: ignore`), **and**
- **Renders** the audit into the job summary by appending it to `$GITHUB_STEP_SUMMARY`.

It surfaces the **exact bytes** the command wrote — no re-sort, re-shape, or re-derive
(the F025 projection is already deterministic). It is best-effort and never gates: on a
fork PR with a restricted token, a failed upload or absent summary does not fail the job
or change the verdict.

### What the `audit.json` shows (so reviewers know what they are reading)

The surfaced document is the F025 `audit.json` projection. Authority for its shape is
[`specs/025-audit-json-projection/contracts/audit-json-document.md`](../../specs/025-audit-json-projection/contracts/audit-json-document.md);
this guidance only lists the field names so a reviewer can read the run.

- **Top level** (field order fixed by F025): `schemaVersion` (`"fsgg.audit/v1"`),
  `verdict` (`pass` | `fail`), `exitCodeBasis` (`clean` | `blocked`), then the three-way
  partition `blockers`, `warnings`, `passing` (each an array) — always present, an empty
  array when none.
- **Each item** is tagged by `kind`: a `gate` (`kind`, `id`, `enforcement`) or a
  `finding` (`kind`, `id`, `path`, `enforcement`).
- **`enforcement`** carries six fields, order fixed: `baseSeverity`, `maturity`, `mode`,
  `profile`, `effectiveSeverity`, `reason`.
- **No-hide:** a base-`blocking` item relaxed by the profile appears in `warnings`
  carrying **both** its `baseSeverity` and a differing `effectiveSeverity` — the verdict
  is never silently dropped.

<!-- SYNTHETIC: illustrative shape only; the authoritative bytes are whatever the real
     F025 projection writes at run time. Reproduced from the F025 contract's worked sample. -->
A blocked verdict surfaces (compact on the wire; indented here for readability):

```json
{
  "schemaVersion": "fsgg.audit/v1",
  "verdict": "fail",
  "exitCodeBasis": "blocked",
  "blockers": [
    {
      "kind": "gate",
      "id": "build:tests",
      "enforcement": {
        "baseSeverity": "blocking",
        "maturity": "blockOnShip",
        "mode": "gate",
        "profile": "standard",
        "effectiveSeverity": "blocking",
        "reason": "base blocking at maturity blockOnShip under profile standard in mode gate"
      }
    }
  ],
  "warnings": [],
  "passing": []
}
```

## Invocation (honest)

**Today (works now):** the command runs **from source** as it exists in the repository:

```bash
dotnet run --project src/FS.GG.Governance.ShipCommand -- \
  ship --mode gate --profile standard --json
```

This is the real current path and is exactly what the template's gate step runs. It
needs the .NET SDK on the runner (`actions/setup-dotnet`), which is why the template
includes that step.

**Future (not yet shipping):** a packed `fsgg ship …` tool is a planned F026 follow-up.
Until it ships, it appears in the template only as a **commented placeholder**:

```bash
# fsgg ship --mode gate --profile standard --json   # NOT YET SHIPPING
```

There is **no** `dotnet tool install fsgg` step and none is implied — presenting one
would claim an install path that does not exist yet. Switch to the packed line (and drop
`setup-dotnet`) once the tool actually ships.

## Honesty boundary

This gate proves exactly one thing: **the deterministic `fsgg ship` verdict is clean.**
Nothing more.

- **Advisory and agent-reviewed findings are reported, never blocking.** They may appear
  in the surfaced `audit.json` (e.g. in `warnings`/`passing`), but they do not fail the
  check and will not until calibration exists. Blocking is exit-code-only.
- **No provenance, attestation, or compliance claim.** A green check does **not** assert
  build provenance, a signed attestation, supply-chain integrity, or conformance to any
  external standard. It asserts only that the deterministic verdict for this change was
  clean. Do not read more into it, and do not advertise more from it.

## Substitutions

Every value an adopter must change is marked `# CHANGE ME:` in the template. At minimum:

| Marker | Where | Change to |
|---|---|---|
| `# CHANGE ME: your protected branch` | `on.pull_request.branches` | Your protected branch name(s), e.g. `[ main ]` → `[ release ]`. |
| `# CHANGE ME (later): remove once … packed tool` | `setup-dotnet` step | (Later) remove the step once `fsgg ship` ships as a packed tool. |
| `# CHANGE ME (later): the from-source line …` | gate step | (Later) switch the from-source `dotnet run …` line to the packed `fsgg ship …` line once it ships. |

Nothing else needs editing to enforce the gate today.

## The workflow template

The copyable template lives at [`docs/ci/templates/fsgg-ship.yml`](./templates/fsgg-ship.yml)
and is reproduced verbatim below (the validation script asserts the two match):

```yaml
# fsgg ship gate — copyable GitHub Actions workflow template.
#
# Copy this file into YOUR repository's .github/workflows/ directory, then mark the
# `ship` job a REQUIRED STATUS CHECK in branch protection. Once required, a red check
# (exit 1/2/3/4) blocks the merge and a green check (exit 0) allows it.
#
# This is a copy-me example, not an active workflow in the FS.GG.Governance repo.
# See docs/ci/github-actions-branch-protection.md for the full how-to.
name: fsgg ship gate

on:
  pull_request:
    branches: [ main ]            # CHANGE ME: your protected branch name(s)
    # Do NOT add `paths:`/`paths-ignore:` here — a governed change must never be able
    # to skip the required check (fail-closed). Keep the gate job filter-free.

permissions:
  contents: read                  # least privilege: read the code, upload an artifact

jobs:
  ship:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0          # required: base/head sensing needs the base ref;
                                  # a shallow checkout makes the command fail as a tool error.

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'  # CHANGE ME (later): remove once `fsgg ship` ships as a packed tool

      - name: fsgg ship gate       # this step's exit code IS the gate — do NOT translate it
        run: |
          # CHANGE ME (later): the from-source line below is the real current path.
          # `fsgg ship …` is NOT yet a packed tool; switch to it once it ships.
          dotnet run --project src/FS.GG.Governance.ShipCommand -- \
            ship --mode gate --profile standard --json
          # Future packed tool (NOT YET SHIPPING — do not enable yet):
          # fsgg ship --mode gate --profile standard --json
          #
          # No `|| true`, no `continue-on-error`, no exit-code remap: a non-zero exit
          # must stay non-zero so the required check goes red and the merge is blocked.

      - name: Surface audit.json   # best-effort; never gates (runs even when the gate failed)
        if: always()
        run: |
          if [ -f readiness/audit.json ]; then
            {
              echo '### fsgg ship verdict'
              echo '```json'
              cat readiness/audit.json
              echo '```'
            } >> "$GITHUB_STEP_SUMMARY"
          else
            echo 'No readiness/audit.json produced (the command may have failed before writing one).' \
              >> "$GITHUB_STEP_SUMMARY"
          fi

      - uses: actions/upload-artifact@v4
        if: always()
        with:
          name: fsgg-audit
          path: readiness/audit.json
          if-no-files-found: ignore   # best-effort: a missing/forbidden upload never fails the job
```

After copying it in, mark the **`ship`** job a **required status check** (see
[Required status check setup](#required-status-check-setup)).
