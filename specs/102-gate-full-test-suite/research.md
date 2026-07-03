# Phase 0 Research: The gate runs the full test suite on every PR

Feature: 102-gate-full-test-suite · Finding H1 · Issue #45 · Epic #44

All Technical-Context unknowns from the spec are resolved below. The spec deferred exactly one open decision — single job vs. shards — which D3 settles on measured evidence.

## D1 — Invocation: `dotnet fsi build.fsx test`, not a raw `dotnet test`

**Decision**: The new job runs the suite via `dotnet fsi build.fsx test -c Debug --no-restore`.

**Rationale**: `build.fsx` (spec 080) is the checked-in bounded entrypoint that exists precisely because the 162-project solution over-subscribes MSBuild under default parallelism (MSB6003/MSB6006). It clamps node count to `max 2 (min 12 (ceil cores/4))` and passes it on the MSBuild command line — the only place it is honored. A raw `dotnet test FS.GG.Governance.sln` would reintroduce the thrash the repo already engineered away, and would diverge from the documented local command (`dotnet fsi build.fsx test`). Using the same entrypoint keeps CI == local (Principle III) and inherits the bound automatically as the runner's core count changes.

**Alternatives considered**:
- *Raw `dotnet test <sln>`* — rejected: unbounded parallelism risk; also a second, divergent way to run the suite.
- *An enumerated per-project matrix* — rejected: a new test project would be silently omitted until someone remembered to add it (reopens H1 for that project). `build.fsx test` covers the whole solution by construction (FR-002).

## D2 — Restore/build sequencing: locked restore step, then `build.fsx test --no-restore`

**Decision**: Two steps mirroring the existing `gate` job — (1) `dotnet restore FS.GG.Governance.sln --locked-mode` with the actionable regenerate-hint on failure, then (2) `dotnet fsi build.fsx test -c Debug --no-restore`. The `test` verb builds-then-tests, so no separate build step is needed; `--no-restore` guarantees the locked restore in step 1 is the single restore.

**Rationale**: The repo's enforcement point is the locked restore (Directory.Build.props sets `RestoreLockedMode` under `GITHUB_ACTIONS`, and `--locked-mode` forces it with one clear place to point at the regenerate command). Running `build.fsx test --no-restore` after it preserves exactly one restore, in locked mode — a graph drift still fails (FR-003), and NuGet caching only warms that restore, never bypasses the lock (FR-006). This is byte-for-byte the pattern the `gate` and `reference-gate-set-pack` jobs already use.

**Alternatives considered**:
- *Let `build.fsx test` restore implicitly* — rejected: that restore would not be `--locked-mode`, weakening the graph-drift gate.
- *Add a `--no-build` and a separate build step* — rejected: `build.fsx test` already builds; a separate step is redundant and risks the two diverging in configuration.

## D3 — Single job, no sharding (settles the spec's deferred decision)

**Decision**: One job. No `matrix` shards.

**Rationale**: Measured locally on 12 cores against latest `main`: build ~89 s + full suite ~102 s, all 83 projects green, tall pole `ReleaseCommand.Tests` ~33 s. `ubuntu-latest` has ~4 cores and is slower, and one `build.fsx test` folds build+test — call it a 2–3× multiplier → an order of ~10–13 min worst case, comfortably inside `timeout-minutes: 30`. Sharding would add real complexity (partition maintenance, N required checks, FR-007's no-overlap/no-gap obligation) to solve a wall-time problem that does not exist. FR-007 is therefore vacuously satisfied; if the suite later grows past the bound, D3 is the documented place to revisit with a project-partitioned matrix.

**Alternatives considered**:
- *2–3 shards now* — rejected: unjustified complexity (Principle III); every shard becomes a separately-named required check to register and maintain.
- *A very tight bound (e.g. 15 min)* — rejected as the initial value: 30 min leaves headroom for a cold cache and runner variance without masking a genuine hang; it can be tightened later once CI-observed times are known.

## D4 — `timeout-minutes: 30`

**Decision**: `timeout-minutes: 30` on the new job.

**Rationale**: Bounds a hang far below the 360-min platform default (FR-004/SC-003) while leaving generous headroom over the ~10–13 min expected worst case, so cold-cache runs and runner variance don't flake red. Consistent-in-spirit with the existing gate jobs' explicit bounds (10/20/25/30 across the file). A future tightening is cheap and non-breaking.

## D5 — Job name is pinned by an already-registered required check

**Decision**: `name: Full test suite (dotnet fsi build.fsx test)` — exactly.

**Rationale**: The active repo ruleset `main branch protection` (id `18430843`, enforcement `active`, targets `~DEFAULT_BRANCH`) already lists four required status-check contexts. Three map one-to-one to existing `gate.yml` job names; the fourth — `Full test suite (dotnet fsi build.fsx test)` — has **no producing job today**. So the required check was registered ahead of the job (this is the branch-protection half of #45, pre-wired). The new job's `name:` is therefore a fixed contract: match it exactly and FR-008 is satisfied on landing with **zero ruleset edits**; a typo means the context never reports and every PR blocks on a perpetually-pending check. This also means FR-009 (keep name and required-checks in sync) is honored by construction — we are conforming the job to the existing required name, not introducing a new one.

**Alternatives considered**:
- *Pick a fresh descriptive name and add it to the ruleset* — rejected: needs ruleset write access, is a second source of truth, and orphans the already-registered context. Conforming to the existing contract is strictly simpler and lower-risk.
- *Edit the ruleset to rename the context* — rejected: out of scope for a Tier-2 workflow change; unnecessary since the existing name is fine.

## D6 — Headless-fragility risk on the CI runner (H2/H3 class)

**Decision**: Expect green (it is green locally, and `Cli.Tests` already passes on CI via publish.yml); if the runner surfaces a genuine headless failure (redirected stdin, process-spawn, filesystem/git edges — the #46/#47/091 class), fix it if trivial or quarantine it *narrowly, named, and tracked* per FR-010 — never blanket-suppress.

**Rationale**: Enabling the suite in CI is exactly what would surface a pre-existing environment-dependent failure the build-only gate was hiding — that is a feature, not a regression (spec Edge Cases). The local run shows zero failures, and the CLI suite (the most headless-sensitive family, incl. the `route --watch` and Spectre width-resilience tests) already runs green on `ubuntu-latest` in publish.yml after the 091 determinism fix. So a full-suite red on first CI run is possible but not expected. The safe-valve is a scoped `Expecto` skip with a rationale comment and a tracking issue (Principle V: "mark skipped with written rationale; never weaken an assertion to green a build"); issues #46 and #47 already exist to receive any such finding. This keeps the gate protecting the other 82 projects immediately rather than blocking on every headless fix.

**Alternatives considered**:
- *`continue-on-error: true` until all headless issues are fixed* — rejected: that makes the entire guarantee advisory (defeats US1/FR-010).
- *Block this feature behind #46/#47* — rejected: the gate's value is immediate and broad; a narrow tracked quarantine (if even needed) is the proportionate response.

## Resolved unknowns summary

| Unknown | Resolution |
|---|---|
| How to run the suite | `dotnet fsi build.fsx test -c Debug --no-restore` (D1) |
| Restore/build order | locked restore → `build.fsx test --no-restore` (D2) |
| Single job vs shards | single job, measured-safe (D3) |
| Timeout value | 30 min (D4) |
| Job name / branch protection | exact existing required-check context; no ruleset edit (D5) |
| Headless failure risk | expect green; narrow tracked quarantine if not (D6) |
