# Research: Fix the Slow Governance Solution Build

All measurements were taken on the development machine for this feature:
**24 logical cores, 64 GB RAM (≈46 GB free), .NET SDK 10.0.301, warm NuGet cache**,
Debug configuration, `--no-restore`, `/t:Rebuild` (clean compile) unless noted.

## D1 — Root cause: MSBuild node over-subscription, not per-project cost

**Decision**: The slowness is **resource over-subscription from unbounded MSBuild
parallelism**, not a slow compiler, a pathological project, or under-utilised cores.

**Evidence**:

| Build | Parallelism | Result |
|---|---|---|
| Single leaf project (`Kernel`) clean rebuild | n/a | **~1.5 s** |
| 43-project subtree (`VerifyCommand`, 42 refs) clean rebuild | default | **14 s** |
| Same subtree with explicit `-m:24` | 24 | 13.4 s (no improvement → already parallel) |
| **Full solution (162 proj) clean rebuild** | **default (24)** | **did NOT finish in 10 min; 149 `MSB6003`/`MSB6006` errors; only ~101 projects built** |
| **Full solution clean rebuild** | **`-m:6`** | **33 s, 0 errors, all 162 built** |
| Full solution no-op incremental rebuild | `-m:6` | **3.3 s** |

The failing full build emits, repeatedly:

- `MSB6003: The specified task executable "dotnet" could not be run.
  System.ComponentModel.Win32Exception (11): ... Resource temporarily unavailable`
  → `fork`/thread creation failure (EAGAIN).
- `MSB6006: "dotnet" exited with code 134` → SIGABRT (runtime abort under
  thread/heap pressure).
- `MSB4166: Child node "N" exited prematurely. Shutting down.`

These are **not** `FS####` compile errors — the projects compile fine in isolation and
under a bounded build. The single-project (~1.5 s) and subtree (14 s) numbers prove
per-project compiler cost is small and parallelism already works; the failure appears
**only** at full width.

**Rationale (mechanism)**: `dotnet build` defaults `-maxcpucount` to the logical core
count (24). Each MSBuild worker node that compiles an F# project launches a **separate
`dotnet fsc` process** — F# has no persistent shared compiler server analogous to C#'s
`VBCSCompiler`. The .NET runtime defaults to **server GC**, which allocates roughly one
GC heap + background thread per logical CPU; so each of ~24 concurrent `fsc` processes
spins up ~24 GC threads (≈576 GC threads) on top of its thread pool. The 82 test
executables all sit at the **leaves** of the dependency graph, so once the shared core is
built, MSBuild tries to compile a large fan-out of them at once — driving the system past
its thread/heap headroom. The result is fork failures, aborts, MSBuild node death,
retries, and thrash that prevents completion.

**Alternatives considered**:
- *"One project is pathological"* — rejected: every project builds quickly alone; the
  failure is width-dependent, and it manifests as process-launch/abort errors, not slow
  compiles.
- *"Cores are under-used / build is single-threaded"* (the spec's tentative
  FR-006 framing) — rejected: the build is **over**-parallel, not under-parallel. The
  subtree already saturates cores and gains nothing from more.

## D2 — The fix: bound MSBuild parallelism with an explicit `-m:N`

**Decision**: Pass an explicit, bounded `-m:N` to `dotnet build`/`dotnet test`. This is
both **necessary and sufficient**: `-m:6` turns a >10-min failing build into a 33 s green
build with zero resource errors (D1 table).

**Rationale**: Bounding concurrent nodes caps the number of simultaneous `fsc` processes
and therefore total threads/heaps, keeping the build within system headroom while still
using multiple cores effectively (FR-006 — not single-threaded). Outputs are identical
because only scheduling changes, not what is compiled (FR-003).

**Alternatives considered**:
- *Workstation GC alone, full parallelism* (`DOTNET_gcServer=0`, no `-m` cap) — **tested,
  rejected**: still failed (56 resource errors, node crash, 17 errors, incomplete). Lower
  per-process footprint helps but does not by itself prevent over-subscription at 24-wide.
  It MAY be combined with `-m:N` as a secondary lever (lets `N` go higher) — a tasks-phase
  tuning option, not required for the target.
- *Reduce project count / merge projects* — out of scope and rejected by FR-002/SC-004
  (no project may be dropped to gain speed).

## D3 — Mechanism: a checked-in command-line `-m:N` (wrapper), NOT props/rsp

**Decision**: Deliver the bound on the **MSBuild command line** via a **checked-in build
wrapper** (`build.fsx`, run as `dotnet fsi build.fsx`) that becomes the documented
standard build/test command. Do **not** attempt to set it via `Directory.Build.props` or
`Directory.Build.rsp`.

**Evidence / why the obvious "config file" routes fail**:
- **`Directory.Build.props` cannot set `-maxcpucount`.** Node count is an MSBuild *engine*
  switch resolved when worker nodes are spawned — before any props file is evaluated.
  There is no property form of `-m`.
- **`Directory.Build.rsp` is read but does not bind `-m` for `dotnet build`.** Verified
  twice:
  - A bogus flag in `Directory.Build.rsp` *is* picked up (`MSB1001: Unknown switch ...
    came from .../Directory.Build.rsp`) → the file is honored.
  - Yet `-m:6` placed in `Directory.Build.rsp` did **not** bound the build: it still
    thrashed (28 `MSB6003`/`MSB6006`, timed out at 180 s, 121/162 built) — whereas the
    identical `-m:6` on the **command line** completes in 33 s/0 errors. `dotnet build`
    applies its own `-maxcpucount` (unlimited) that takes precedence over the
    auto-response file's value.
- **Environment variables are rejected by FR-008** (no per-machine env / local
  overrides). A wrapper script sets the flag *in the repo*, applied uniformly — that is
  checked-in configuration, not a per-machine override.

**Rationale**: Only an explicit command-line `-m:N` reliably bounds the build, and the
only checked-in, uniform, no-per-developer-tuning way to guarantee that flag is present on
every invocation (local and CI) is a wrapper that *is* the documented command. A wrapper
also naturally satisfies NFR-001 (echo chosen `N` + elapsed) and FR-006 (compute `N` from
hardware).

**Alternatives considered**:
- *Document "remember to pass `-m:6`"* — rejected: relies on humans/CI remembering a flag;
  violates "applies uniformly" (FR-008).
- *bash/cmd wrapper pair* — acceptable but `build.fsx` is preferred (F#-exclusive stack,
  matches the repo's `dotnet fsi build.fsx` idiom). A thin `scripts/build.{sh,cmd}` shim
  calling `dotnet fsi build.fsx` MAY be added for discoverability (tasks decision).

## D4 — Choosing `N`: hardware-derived, bounded (FR-006)

**Decision**: The wrapper computes `N` from the logical core count rather than hardcoding
`6`, so the build *scales with hardware* (FR-006) while staying safely bounded. Exact
formula is tuned in the tasks phase against SC-001/002/006; a sound starting heuristic is
`N = clamp(2, ceil(cores / 4), 12)` (→ 6 on this 24-core machine, the proven value),
optionally raised when combined with workstation GC (D2). The wrapper MUST allow no value
that reintroduces over-subscription (never unbounded).

**Rationale**: A fixed `6` would leave a 64-core machine under-driven (FR-006 wants
*faster on more cores*) and could still be too high on a tiny 2-core runner. Deriving `N`
from cores (and, optionally, memory) keeps the bound proportional. `6/24` is the proven
anchor point; the formula is calibrated, not invented.

**Alternatives considered**: fixed constant (simpler but fails FR-006 scaling and the
constrained-runner edge case); memory-only derivation (more robust but more complex —
folded in as an optional refinement).

## D5 — Test suite as a delivery gate (SC-003) and the pathological test (FR-010)

**Decision**: `dotnet test FS.GG.Governance.sln` runs through the **same bounded `-m:N`
wrapper**, so the suite builds without thrash and can run to completion as the delivery
gate. The one documented pathologically-slow item — the **SDD `fs-gg-fullstack`
template-generation integration test** — stays **isolated/opt-in**, not deleted.

**Evidence / rationale**: That test (and the worked-example real `dotnet build`) is
*already* gated behind real-evidence opt-in from feature 078 (`FSGG_REAL_EVIDENCE` /
truthy `CI`, with a bounded build budget) — the default run emits a named skip and does
not perform the heavyweight scaffold/build. So the default suite already excludes the
dominant cost **by design**, not by hand-editing the solution (satisfies SC-003's
"zero test projects manually excluded" — the project still builds and its fast tests run;
only the heavy real-evidence path is opt-in and loudly skipped, per FR-010). This feature
keeps that posture and documents it; rewriting that test's internals is explicitly out of
scope (spec Assumptions).

**Note**: full end-to-end `dotnet test` wall-clock was not run to completion in this
research session (it would be long); the build half is proven (33 s) and the test-run
half is governed by the existing opt-in gating. Confirming the end-to-end SC-003 number is
a tasks-phase validation step.

## D6 — Correctness / equivalence preserved (FR-002, FR-003, FR-009, SC-004)

**Decision**: The change alters only build *scheduling* (node count), never *what* is
compiled. Same `.sln`, same 162 projects, same compiler inputs/flags, same outputs.

**Evidence**: The bounded build produced all 162 `-> .../*.dll` outputs with `0
Warning(s) 0 Error(s)`; `Directory.Build.props` (with `TreatWarningsAsErrors=true`) is
unchanged, so a real compile error still fails the build with identical MSBuild detail
(FR-009). `GenerateDocumentationFile=true` is retained (measured cost ~100 ms/leaf,
negligible; removing it is *not* needed to hit the target and would change outputs — so it
is left alone, preserving FR-003).

## D7 — Measurability (NFR-001)

**Decision**: The wrapper prints the derived `N`, the detected core count, and the
build/test elapsed time on every run, so the improvement is demonstrable rather than
anecdotal. The before/after numbers in D1 are the baseline record; a small checked-in
guard asserts the wrapper exists and always passes an explicit (non-unbounded) `-m`.

## D8 — Implementation re-confirmation (T001/T002, 2026-06-27)

The decisions above were re-validated on the implementer's machine during implementation:
**24 logical cores, ~61 GB RAM (≈46 GB free), SDK 10.0.301, warm cache** — i.e. the same
class as the D1 anchor.

- **Mechanism (T002, D3 re-check)**: a bogus switch in a temp `Directory.Build.rsp`
  surfaced as `MSB1001: Unknown switch ... came from .../Directory.Build.rsp` → the rsp is
  honored; the temp file was deleted immediately (never committed). The command-line
  `-m:6` bound was confirmed *positively* by the bounded build below. Re-triggering the
  full unbounded thrash (the >10-min, EAGAIN/SIGABRT resource-exhaustion failure D1
  already captured) was deliberately **not** repeated — it is the very failure mode this
  feature removes, not a safe step to reproduce on demand.
- **Bounded build via `dotnet fsi build.fsx`** (auto `-m:6` from 24 cores):
  clean `/t:Rebuild` **33 s**, `Build succeeded`, all **162** assemblies, **0**
  `MSB6003`/`MSB6006`; no-op incremental **3.4 s**; full `build.fsx test` **65 s** total
  (81 test runs, 2287 passed, 0 failed, 1 named opt-in skip). These match the D1 anchor.
