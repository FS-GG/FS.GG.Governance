# Test-harness contract: bounded real-evidence scaffold build

This project exposes no product API. Its only externally-observable contract is the
**test-harness behavior** of the SDD reference-provider worked-example suite: the
environment variables that select the run configuration and the outcome each one produces.
This is a *test* contract (Tier 2), not a shipped product surface — it is documented here
and in quickstart, and is not tracked by any surface baseline.

## Environment variables (inputs)

| Variable | Values | Default | Effect |
|---|---|---|---|
| `FSGG_REAL_EVIDENCE` | `1` to enable | unset | When `1`, the heavyweight real `dotnet build` of the emitted skeleton runs (bounded). |
| `CI` | truthy = set and (trimmed, case-insensitive) not `""` / `0` / `false` | unset | When truthy, also enables the real build (the canonical full-evidence path, FR-005). E.g. `CI=true` (GitHub Actions) or `CI=1`; `CI=0` / `CI=false` / unset do not enable. |
| `FSGG_BUILD_BUDGET_SECONDS` | positive integer | `120` | The finite budget for the real build before it is cut off as a timeout skip. Absent/non-numeric ⇒ `120`. |
| `BLESS_FIXTURES` | `1` | unset | (Pre-existing, unchanged) regenerate the manifest golden — must NOT be needed by this change (FR-008). |

The real build runs iff `FSGG_REAL_EVIDENCE=1` **OR** `CI` is truthy; otherwise it is a
named opt-out skip.

## Outcomes (observable test results)

For the test `"emitted skeleton \`dotnet build\`s first-attempt (real evidence)"`:

| Precondition | Result | Message shape (named, actionable — FR-009) |
|---|---|---|
| real build disabled (default) | **skip** | `REAL-EVIDENCE OPT-OUT: set FSGG_REAL_EVIDENCE=1 (or run under CI) to exercise the real dotnet build` |
| enabled, `dotnet` absent | **skip** | `PREREQUISITE: .NET SDK not available to build the emitted skeleton (<detail>)` |
| enabled, build exceeds budget | **skip** | `BUDGET EXCEEDED: dotnet build exceeded <budget>; partial output: <…>` |
| enabled, build exits 0 within budget | **pass** | — |
| enabled, build exits non-zero within budget | **fail** | `dotnet build MyApp.sln must succeed first-attempt:\n<output>` |

Invariants the contract guarantees:
- **Never an indefinite hang** — every path returns within `budget + small margin`
  (SC-001).
- **Never a silent green** — every not-run/cut-off path is a *named* skip; the three skip
  reasons (opt-out, missing-SDK, timeout) are mutually distinguishable (SC-004, FR-004).
- **Never a masked failure** — a genuine non-zero build fails; it is never converted to a
  timeout or SDK-missing skip (SC-003, FR-003).
- **No orphans** — on timeout the entire build process tree is terminated (FR-002).

For the test `"bounded: a stalled build is cut off within budget+margin"` (new, US1):

| Precondition | Result |
|---|---|
| a sleeper process is available for this OS | **pass** — outcome is `TimedOut budget`, returns within `budget + margin`, the spawned process is gone |
| no sleeper available for this OS | **skip** — `PLATFORM: no sleeper available to force a stall` |

## Unchanged tests (must stay byte-identical — FR-006 / FR-008)

- `"empty dir → seam scaffolds the runtime skeleton, provider-owned, no collisions"`
- `"manifest projection matches the committed golden, byte-for-byte and deterministically"`

Both run and pass on **every** configuration (they perform no subprocess build), and the
committed golden `fixtures/sdd-reference/scaffold-manifest.golden.json` is not regenerated.
