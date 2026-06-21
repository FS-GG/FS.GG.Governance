# Quickstart: `fsgg ship` Host Command (F026)

A runnable validation guide for the protected-branch verdict command. It proves the row's behavior — a
real merge **verdict** with a real **consequence** (a distinct blocking exit code) — end to end. Pairs
with [plan.md](./plan.md), [data-model.md](./data-model.md), and
[contracts/](./contracts/fsgg-ship-command.md). It references contracts and the data model rather than
duplicating them, and contains no implementation bodies.

## Prerequisites

- .NET SDK for `net10.0` (per `Directory.Build.props`).
- The nine referenced cores already build green in the solution: `Config` (F014), `Snapshot` (F016),
  `Routing` (F015), `Findings` (F017), `Gates` (F018), `Route` (F019), `Enforcement` (F023), `Ship`
  (F024), `AuditJson` (F025).

## Build & test

```bash
# Build the new project and its tests
dotnet build src/FS.GG.Governance.ShipCommand/FS.GG.Governance.ShipCommand.fsproj
dotnet test  tests/FS.GG.Governance.ShipCommand.Tests/FS.GG.Governance.ShipCommand.Tests.fsproj

# Whole-solution regression (no other project should change)
dotnet test FS.GG.Governance.sln
```

## FSI sketch (Principle I — exercise the surface before the `.fs` body)

Load the packed surface in `scripts/prelude.fsx` and exercise the seams without I/O:

- `Loop.parse [...]` → assert the normalized `RunRequest` (scope, `Mode`/`Profile` defaults, `AuditOut`)
  and the `UsageError` cases (`PathsAndSinceTogether`, `UnrecognizedMode`, …).
- `Loop.update` over literal `Model`/`Msg` → assert the `Loaded(Valid)` step emits one
  `WriteArtifact(AuditArtifact, …)` whose content equals `AuditJson.ofShipDecision (Ship.rollup result
  mode profile)`, and that the terminal `Emitted` maps `ExitCodeBasis` to `Success`/`Blocked`.
- `Loop.exitCode` → assert `Success 0 | Blocked 1 | UsageError' 2 | InputUnavailable 3 | ToolError 4`.

## CLI smoke (real repo, real catalog)

From a repository with a declared `.fsgg` catalog and a base/head change:

```bash
# Canonical protected-branch invocation
dotnet run --project src/FS.GG.Governance.ShipCommand -- \
  ship --mode gate --profile standard --json
echo "exit=$?"   # 0 if the change is clear, 1 if it is blocked
```

Expected: `readiness/audit.json` is written; stdout carries the audit document (with `--json`); a clean
change exits 0, a base-blocking change exits 1.

## Acceptance → evidence map

| Spec item | Evidence (test file / smoke) |
|---|---|
| US1 AS1 (base-blocking ⇒ `verdict:fail`/`exitCodeBasis:blocked`/exit 1) | `InterpreterTests` (faked ports, `--paths` selecting a base-blocking gate) + `EndToEndTests` |
| US1 AS2 (passing-only ⇒ `verdict:pass`/`clean`/exit 0) | `InterpreterTests` + `LoopTests` (terminal `Clean → Success`) |
| US1 AS3 (text summary: verdict, basis, partition + findings) | `LoopTests` (`render Text`) |
| US2 AS1 (mode/profile applied + recorded per item) | `InterpreterTests` (audit bytes carry `mode`/`profile`) |
| US2 AS2 (relaxed base-blocking ⇒ warning w/ base+effective, pass/clean) | `InterpreterTests` (two lever sets; no-hide) |
| US2 AS3 (unrecognized lever ⇒ usage error, no artifact) | `ParseTests` + `FailureTests` |
| US2 AS4 (no flags ⇒ default `gate`/`standard`, recorded) | `ParseTests` (defaults) + `InterpreterTests` |
| US3 AS1 (twice-run byte-identical artifact + exit) | `InterpreterTests` (SC-002) |
| US3 AS2 (`--json` deterministic stdout = audit doc; text suppressed) | `InterpreterTests` / `LoopTests` (`render Json`) |
| US3 AS3 (schemaVersion; no clock/abs-path/env) | `InterpreterTests` (SC-005, inherited F025) |
| US4 AS1–AS4 (non-git/missing/invalid catalog/unrecognized lever/unwritable ⇒ distinct tool-failure code ≠ 1, no partial artifact) | `FailureTests` (SC-004) |
| SC-003 (same change, two lever sets ⇒ two verdicts/exit codes) | `InterpreterTests` |
| SC-007 (full composition through fakeable boundaries; bytes = F025 of F024 rollup) | `InterpreterTests` + `EndToEndTests` |

## Determinism & safe-failure checks

- Run twice over identical inputs **and levers**; `diff` the two `audit.json` and confirm identical exit
  codes (SC-002).
- Confirm a base-blocking change under a strict lever set exits **1** and the *same* change under a
  relaxing profile exits **0** with the item in `warnings` (SC-003, no-hide).
- Confirm each tool-failure case (non-git, missing/invalid catalog, unrecognized lever, unwritable
  `--audit-out`) yields a distinct stderr diagnostic and an exit in `{2,3,4}` — never `1` — and that no
  `audit.json` is written for the usage/input cases (SC-004).
