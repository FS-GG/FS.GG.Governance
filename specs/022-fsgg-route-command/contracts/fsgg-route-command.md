# Command Contract: `fsgg route`

The observable user/CI contract for the `fsgg route` host command (F022): its flags, its exit codes, the
on-disk artifacts it writes, and its stdout summary. The JSON *document* shapes are owned by F020
(`fsgg.route/v1`) and F021 (`fsgg.gates/v1`) and are not restated here — this command writes those
projections **byte-for-byte unchanged** (FR-005/FR-006); see `specs/020-*/contracts/` and
`specs/021-*/contracts/`.

## Invocation

```
fsgg route [--repo <dir>] [--paths <p> ...] [--since <rev>] [--json] \
           [--gates-out <path>] [--route-out <path>]
```

| Flag | Meaning | Default |
|---|---|---|
| `--repo <dir>` | Repository root to operate on. | current directory (`.`) |
| `--paths <p> ...` | Route exactly these paths; **bypasses git diff** (US2 AS1, D4). | — |
| `--since <rev>` | Route the paths changed since `<rev>` (Snapshot `Since`). | — |
| (neither) | Route the default sensed base/head change. | — |
| `--json` | Emit the machine-readable JSON summary on stdout; suppress text. | text |
| `--gates-out <path>` | Override the `gates.json` destination. | `<repo>/.fsgg/gates.json` |
| `--route-out <path>` | Override the `route.json` destination. | `<repo>/readiness/route.json` |

`--paths` and `--since` together is a **usage error** (D4/D8). An empty `--paths` list is a usage error.

## Exit codes (D6)

| Code | Decision | When |
|---|---|---|
| `0` | Success | Repo sensed, catalog valid, gates selected, both artifacts written — **regardless of gate or finding count** (FR-009, FR-011). |
| `2` | UsageError | Bad invocation: unknown flag, missing flag value, `--paths` + `--since`, empty `--paths`. No artifact written. |
| `3` | InputUnavailable | Not a git repo / git unavailable; `--since` revision does not resolve; required `.fsgg` files missing or failing validation. No artifact written. |
| `4` | ToolError | Output location unwritable after a valid route; any unexpected reified failure. No partial artifact left (D9). |

There is **no** "blocking" exit code (FR-008): this command emits no ship/merge verdict.

## Written artifacts

| Path (default) | Document | Bytes |
|---|---|---|
| `<repo>/.fsgg/gates.json` | whole-catalog gate registry | `GatesJson.ofGateRegistry registry` verbatim (F021) |
| `<repo>/readiness/route.json` | per-change selected gates + trace + findings + cost | `RouteJson.ofRouteResult result` verbatim (F020) |

Both are **byte-for-byte identical** for identical repository inputs (FR-006, SC-002), carry their
declared `schemaVersion` (`fsgg.gates/v1` / `fsgg.route/v1`), and contain **none** of: a ship/merge
verdict, severity, profile, mode, enforcement state, cache-eligibility verdict, blockers, warnings,
exit-code basis, wall-clock value, machine-absolute path, or environment-derived value (SC-005). Parent
directories are created as needed; each file is written via temp-file + atomic rename (no partial file).

## stdout summary (D7)

Separate from the persisted artifacts and deterministic for fixed inputs (FR-007, SC-002).

- **Text (default)** — lists each selected gate by id with its selecting path and per-tier cost, the cost
  rollup (`cheap`/`medium`/`high`/`exhaustive` counts), the unknown-governed-path findings, and the two
  written artifact paths. A change selecting no gates says so explicitly (US1 AS3, SC-006).
- **JSON (`--json`)** — the machine-readable form of the same summary; the text form is suppressed.

The summary is a convenience view; the **artifacts on disk are the contract** for downstream consumers.

## Worked example (text)

```
$ fsgg route --repo /tmp/demo
route: 2 gate(s) selected for 1 changed path

  build:format   ← src/Lib/Thing.fs   (cheap)
  build:lint     ← src/Lib/Thing.fs   (medium)

cost: cheap=1 medium=1 high=0 exhaustive=0
findings: none

wrote .fsgg/gates.json        (fsgg.gates/v1, 4 gate(s))
wrote readiness/route.json    (fsgg.route/v1, 2 selected)
```

```
$ echo $?
0
```
