# Quickstart: `fsgg route` Host Command

A validation/run guide for the `fsgg route` composition. Implementation details (module bodies, full
test suites) live in `tasks.md` and the implementation phase — this is how to *build, run, and prove* the
feature. See [data-model.md](./data-model.md) and [contracts/](./contracts/) for the types and the
command contract.

## Prerequisites

- .NET SDK with `net10.0` (matches `Directory.Build.props`). Check: `dotnet --version`.
- A working `git` on PATH (only for the real end-to-end test; faked-port tests need no git).

## Run the tests

```bash
# This feature's project only
dotnet test tests/FS.GG.Governance.RouteCommand.Tests/FS.GG.Governance.RouteCommand.Tests.fsproj

# Full solution (confirms nothing downstream broke)
dotnet test FS.GG.Governance.sln
```

Expect: the pure `update`/`parse`/`render` suite, the faked-port interpreter suite, the four-category
failure suite, the surface-drift check, and **one real-temp-git + real-catalog end-to-end** test all
green (Expecto + FsCheck via VSTest). No `Synthetic`-tokened test unless disclosed in the PR.

## Run the command against a real repo

```bash
# Default base/head scope, text summary
dotnet run --project src/FS.GG.Governance.RouteCommand -- route --repo /path/to/repo

# Scope to an explicit slice
dotnet run --project src/FS.GG.Governance.RouteCommand -- route --repo /path/to/repo \
  --paths src/Lib/Thing.fs docs/intro.md

# Scope to a since-revision, machine-readable summary
dotnet run --project src/FS.GG.Governance.RouteCommand -- route --repo /path/to/repo \
  --since HEAD~3 --json
```

After a successful run, confirm the two artifacts and the exit code:

```bash
test -f /path/to/repo/.fsgg/gates.json && echo "gates.json written"
test -f /path/to/repo/readiness/route.json && echo "route.json written"
echo "exit: $?"   # 0 on success, regardless of how many gates were selected
```

## Prove determinism (SC-002)

```bash
dotnet run --project src/FS.GG.Governance.RouteCommand -- route --repo /path/to/repo
cp /path/to/repo/.fsgg/gates.json /tmp/a-gates.json
cp /path/to/repo/readiness/route.json /tmp/a-route.json
dotnet run --project src/FS.GG.Governance.RouteCommand -- route --repo /path/to/repo
diff /tmp/a-gates.json /path/to/repo/.fsgg/gates.json   # no output ⇒ byte-identical
diff /tmp/a-route.json /path/to/repo/readiness/route.json
```

## Exercise it in FSI (design-first, Principle I)

Load the packed surface via `scripts/prelude.fsx` and drive the pure boundary without any I/O:

```fsharp
open FS.GG.Governance.RouteCommand
// parse → init → feed faked Msgs through update → render the summary
let req = Loop.parse [ "route"; "--paths"; "src/Lib/Thing.fs" ]
// then exercise update with literal Sensed/Loaded/Wrote messages and inspect Model.Exit + emitted Effects
```

## What this feature does NOT do

- No **ship verdict** — no merge decision, severity, profile, mode, enforcement, cache-eligibility
  verdict, blockers, warnings, or exit-code-from-blockers (FR-008). Those are `fsgg ship` / `audit.json` /
  Phase 5 / Phase 11.
- No **new routing/selection/serialization logic** — it composes F014–F021 verbatim (FR-004, FR-005).
- No **new dependency** — git/catalog reads are delegated to `Snapshot`/`Config`; serialization to
  `RouteJson`/`GatesJson`; the only new I/O is a file write via `System.IO`.
- No **`fsgg ship`**, `audit.json`, or branch-protection guidance (the slice boundary).

## Acceptance scenario → evidence map

| Spec item | Proven by |
|---|---|
| SC-001 selected set = projection of routed domains | `InterpreterTests` / `EndToEndTests`: written bytes = `RouteJson.ofRouteResult` of the same inputs |
| SC-002 byte-identical across two runs | `InterpreterTests` twice-run equality; `EndToEndTests` re-run diff |
| SC-003 three scopes (paths / since / default) | `ScopeParseTests` + `InterpreterTests` per scope; `EndToEndTests` real git |
| SC-004 four failure categories → distinct diag + code | `FailureTests`: not-a-repo, missing/invalid catalog, unresolved rev, unwritable output |
| SC-005 artifacts carry no verdict/clock/abs-path/env | inherited from F020/F021; `InterpreterTests` exclusion assertion on written bytes |
| SC-006 routine-only change / empty catalog ⇒ success | `LoopTests` + `InterpreterTests`: empty selected set, exit 0, valid artifacts |
| SC-007 full composition through fakeable boundaries | `InterpreterTests` (faked ports) + one `EndToEndTests` real-git proof |
| US1/US2/US3/US4 acceptance scenarios | `LoopTests`, `ScopeParseTests`, `InterpreterTests`, `FailureTests` respectively |
