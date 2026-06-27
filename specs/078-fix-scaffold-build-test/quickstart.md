# Quickstart / validation: bounded scaffold real-evidence build

Validation scenarios that prove the feature. They map to the spec's user stories and
success criteria. See [data-model.md](./data-model.md) for the outcome type and
[contracts/test-harness-contract.md](./contracts/test-harness-contract.md) for the
env-var/outcome contract.

**Project under test**:
`tests/FS.GG.Governance.Sample.SddReferenceProvider.Tests` (Expecto via `dotnet test`).

Prerequisites: the .NET `net10.0` SDK on PATH (for the real-evidence scenarios only).

---

## Scenario 1 — The routine default run is fast and never hangs (US2 / SC-002, SC-001)

```bash
dotnet test tests/FS.GG.Governance.Sample.SddReferenceProvider.Tests \
  /FS.GG.Governance.Sample.SddReferenceProvider.Tests.fsproj
```

Expected:
- Completes in **< 30 s** (no real `dotnet build`).
- The scaffold-correctness test and the golden/determinism test **pass**.
- The build test is reported as a **named opt-out skip** (`REAL-EVIDENCE OPT-OUT: …`).
- The new bound test passes (a real sleeper is cut off within its sub-second budget).
- The run terminates — no hang.

## Scenario 2 — The bound holds under a forced stall (US1 / SC-001, SC-004)

The forced-stall test (`BoundedBuildTests.fs`) runs in **every** configuration, including
the default above. It spawns a real long-running sleeper under a ~1 s budget and asserts:

- the outcome is `TimedOut <budget>`,
- the call returns within `budget + margin` (a `Stopwatch` assertion),
- the spawned process is no longer alive (the tree was killed).

On a platform with no sleeper it reports a **named** `PLATFORM:` skip — never a silent
green. This proves SC-001 deterministically without a real hanging `dotnet build`.

## Scenario 3 — Real evidence runs and is bounded under opt-in / CI (US2, US3 / FR-005, SC-003)

```bash
FSGG_REAL_EVIDENCE=1 dotnet test \
  tests/FS.GG.Governance.Sample.SddReferenceProvider.Tests \
  /FS.GG.Governance.Sample.SddReferenceProvider.Tests.fsproj
# CI runners get the same behavior automatically via the truthy CI env var.
```

Expected (SDK present):
- The emitted `MyApp.sln` really builds with `-maxcpucount:1 --disable-build-servers`.
- A correct skeleton ⇒ the build test **passes** first-attempt (exit 0).
- The build is cut off as a **timeout skip** only if it exceeds the budget
  (`FSGG_BUILD_BUDGET_SECONDS`, default 120) — never an indefinite wait.

## Scenario 4 — A genuine compile failure still fails (US3 / FR-003, SC-003)

With the real build enabled and a deliberately-broken skeleton (e.g. inject a syntax
error into the emitted `Program.fs` before the build step in a local experiment, or via the
failure-path harness), the build returns non-zero within budget and the test **fails** with
the captured output — it is **not** absorbed by the timeout or missing-SDK skip paths.

## Scenario 5 — Missing-SDK skip stays distinct (FR-004)

On a machine without `dotnet` on PATH, with the real build enabled, the build test reports
the **named missing-SDK skip** (`PREREQUISITE: .NET SDK not available …`), distinct from the
timeout skip and the opt-out skip.

## Scenario 6 — No golden / surface drift (FR-008 / SC-005)

```bash
git diff --stat fixtures/sdd-reference/scaffold-manifest.golden.json surface/
```

Expected: **empty** — the committed manifest golden and all surface baselines are
byte-identical. The two non-build worked-example tests are behavior-unchanged. The project's
own `SurfaceDriftTests` (core baselines byte-identical) stays green.

## Whole-suite guarantee (SC-001, SC-005)

```bash
dotnet test FS.GG.Governance.sln
```

Expected: the full solution test run **terminates** (no scaffold-build hang); per-project
test counts are unchanged except the additive `+1` forced-stall test and the build test's
configuration-dependent skip/pass.
