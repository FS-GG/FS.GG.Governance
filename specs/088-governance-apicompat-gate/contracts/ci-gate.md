# Contract: CI Gate Job & Advisory→Required Promotion

Wires the detector + `ApiCompatibility` rule into `.github/workflows/gate.yml`, following the established per-gate job pattern (Jobs 1–3: locked-restore build, build-config drift, reference-gate-set pack guard).

## New job: "API compatibility gate (breaking-change → SemVer major)"

| Aspect | Contract |
|---|---|
| Trigger | same as existing jobs: `push`→`main`, `pull_request`→`main` |
| Runner / SDK | `ubuntu-latest`, .NET `10.0.x` (match existing jobs) |
| Restore | `dotnet restore FS.GG.Governance.sln --locked-mode` (repo convention) |
| Detect | pack Release + run the ApiCompat detector (`pack-and-apicheck.fsx --json`, repo root) → `ApiBreakSignal` set vs the feed baseline |
| Grade | run the `ApiCompatibility` rule (via `fsgg release`/`verify`, or a thin guard test) → findings + coverage |
| Output | print per-package coverage (Checked / NoBaselineYet / NotCovered) and any breaking-under-bump findings (FR-007, FR-012) |

## Advisory phase (US1 — ships first)

- Job runs and **reports**; it is **NOT** in branch-protection required checks (non-blocking).
- The `ApiCompatibility` rule is declared with **advisory** `Maturity` → violations appear in `Warnings`, `Verdict` unaffected.
- Net effect: breaking-under-bump and indeterminate results are **visible** on every PR without blocking — the spec's advisory rollout (SC-005 baseline-gathering phase).

## Required phase (US2 — after SC-005)

Promotion is a **two-part reviewed change**, done only when SC-005 holds (zero breaking-under-bump across covered packages):

1. **In-product**: flip the declared `ApiCompatibility` rule `Maturity` → `BlockOnRelease`. Now violations land in `Blockers`, `fsgg release`/`verify` exits `Blocked`.
2. **Infra mirror**: add this job to the repo's **required status checks** (branch protection).

Both flipped together keeps the in-product verdict and the CI gate consistent. The in-product `Maturity` is the **source of truth**; the required-check setting mirrors it.

## Constraints (D6)

- The job MUST NOT edit drift-locked files (`Directory.Build.props`, `Directory.Packages.props`, `.config/dotnet-tools.json`). Any ApiCompat tool is installed **job-scoped** (`dotnet tool install`); MSBuild props live in repo-owned `*.local.props`.
- The detector step MUST NOT fail the build on a detected break while Advisory (D7) — it captures the signal; the rule decides. (A hard `dotnet build` failure is reserved for the Required phase, and even then is expressed through the governance verdict exit code, not an inline Package-Validation error.)

## Done signals

- Advisory: job green/neutral, findings + coverage printed, not required. Surfaces drift on a deliberate test break.
- Required: a breaking-under-bump change fails the job and `fsgg release`; the same change with a major bump passes; a no-break change passes with no maintainer action.
