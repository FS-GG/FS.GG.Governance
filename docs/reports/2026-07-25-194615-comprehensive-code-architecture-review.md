# Comprehensive code and architecture review

- Repository: `FS-GG/FS.GG.Governance`
- Reviewed revision: `e29b0573177af382ced015e1c03481df57c7983a`
- Review completed: 2026-07-25 19:46:15 UTC (21:46:15 CEST)
- Scope: governance domain libraries, command host, interpreters, tests, project topology, workflows, and cross-repository policy role

## Executive assessment

Governance is architecturally disciplined: pure policy cores are generally separated from filesystem/process interpreters, and the default branch is green. The principal correctness concern is one command-host adapter that converts every handoff-directory I/O failure into an empty list. That behavior makes unreadable state indistinguishable from absent state and can incorrectly authorize a no-op result.

Overall risk: **medium**. The policy cores are well tested, but a fail-open edge contradicts the repository's otherwise strong absent-versus-unreadable semantics. The unusually fine project granularity also imposes material build and maintenance cost.

## Architecture

The repository favors small capability-oriented libraries with interpreters at the edges. That supports deterministic policy tests and minimizes direct I/O inside decision logic. The cost is a very large project graph—approximately 84 source and 89 test projects—which makes package references, solution operations, and coherent changes expensive.

## Evidence

| Check | Result |
|---|---|
| Full solution tests observed | 588 passed, 1 explicitly skipped, 0 failed |
| Current-revision GitHub checks | 13 succeeded, 0 failed |
| Static edge-adapter review | One broad exception-to-empty conversion confirmed |

The local `dotnet test` host lingered after the individual test processes had reported completion and was terminated; the recorded suite results and 13 green live checks are consistent. This review was not a formal policy proof or hostile-filesystem test.

## Findings

### 1. High — unreadable handoff state is treated as “no handoffs”

In `src/FS.GG.Governance.CommandHost/CommandHost.fs`, `realHandoffs` catches every exception while enumerating/reading the handoff directory and returns `[]`. Permission failures, malformed paths, transient I/O faults, and missing directories therefore collapse to the same value.

At a governance boundary, this is fail-open: downstream logic can conclude there are no handoffs or gates when the evidence was merely unreadable.

Recommendation: return a typed result that distinguishes absent, empty, unreadable, and malformed state. Surface unreadable state as a diagnostic and non-zero command outcome. Add permission/error-injection tests.

### 2. Medium — source/test project fragmentation creates coordination tax

Roughly 84 source projects and 89 test projects enforce boundaries, but also multiply restore evaluation, dependency declarations, release coherence, and solution maintenance. Some boundaries likely protect policy purity; others may no longer justify a separate assembly.

Recommendation: generate a project-dependency graph and classify boundaries as security/purity, packaging, or organizational. Merge only low-value assembly boundaries, preserving namespace and internal-module seams.

### 3. Low — documentation symbol scanning loses error specificity

`DocsChecks/Interpreter.fs` maps unreadable `.fsi` input to a negative symbol result. This remains fail-closed because it produces a finding, but the operator sees stale/missing rather than unreadable evidence.

Recommendation: propagate a typed read error so remediation points to permissions or encoding instead of source documentation.

### 4. Low — one skipped test needs explicit lifecycle ownership

The solution reports one clearly labelled skipped test. A labelled skip is preferable to silent omission, but it should not become permanent background noise.

Recommendation: attach the skip to an issue/owner and exercise it in a suitable scheduled environment if it is capability-dependent.

## Strengths

- Policy decisions are generally isolated from I/O interpreters.
- The broad test graph is green at the reviewed revision.
- The codebase explicitly models many governance failure states.
- Fine-grained libraries make dependency direction visible.
- Cross-repository governance contracts are backed by executable checks.

## Recommended order

1. Replace `realHandoffs` exception swallowing with typed fail-closed results.
2. Add hostile-filesystem tests at command-host boundaries.
3. Rationalize low-value project boundaries using dependency-graph evidence.
4. Improve documentation-check diagnostics and assign the skipped test.
