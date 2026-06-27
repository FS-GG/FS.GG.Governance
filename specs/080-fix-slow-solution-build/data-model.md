# Data Model: Fix the Slow Governance Solution Build

This feature has no runtime domain entities (no F# types, no storage). The "model" is the
small set of **build-configuration knobs** and the **wrapper contract** that bind them.

## Entity: Build Invocation

The single conceptual entity the wrapper produces and runs.

| Field | Type | Source | Rule |
|---|---|---|---|
| `solution` | path | constant | Always `FS.GG.Governance.sln` (the full solution — FR-002/SC-004; no project subset). |
| `verb` | enum | caller arg | `build` \| `test` (the two documented gates: US1 build, US2 suite). Default `build`. |
| `maxNodes` (`-m:N`) | int ≥ 1 | derived (D4) | MUST be explicit and **bounded** — never unlimited. Derived from `coreCount` (and optionally free memory). Anchor: `6` on a 24-core/64 GB host. Suggested `clamp(2, ceil(coreCount/4), 12)`. |
| `coreCount` | int | environment | Detected logical processors; the input to `maxNodes`. Echoed for observability (NFR-001). |
| `configuration` | enum | caller arg | `Debug` (default) \| `Release`. Pass-through to `dotnet`. |
| `extraArgs` | string[] | caller arg | Forwarded verbatim to `dotnet` (e.g. `--no-restore`, filters). MUST NOT let a caller silently *remove* the `-m` bound. |
| `elapsedMs` | int | computed | Wall-clock of the invocation; printed at the end (NFR-001). |

### Validation rules
- `maxNodes ≥ 2` and `maxNodes` MUST always be present on the emitted MSBuild command
  line (research D3: props/rsp do not bind it). A configuration that yields an unbounded
  build is invalid.
- `solution` is fixed; the wrapper never narrows the project set to gain speed (FR-002).
- The wrapper MUST preserve the underlying `dotnet` exit code so a real failure still
  fails (FR-009).

## Entity: Pathological Test Isolation (reference only)

Not created by this feature — documented so the model is complete (FR-010, research D5).

| Field | Value |
|---|---|
| Item | SDD `fs-gg-fullstack` template-generation integration test + worked-example real `dotnet build` |
| Mechanism | Opt-in gate `FSGG_REAL_EVIDENCE` / truthy `CI` + bounded build budget (delivered by feature 078) |
| Default behavior | Loudly **skipped** (named skip), so it does not dominate the suite |
| This feature | Keeps and documents the isolation; does not rewrite the test (out of scope) |

## State / transitions

None. A build invocation is a single pure shell-out: derive `maxNodes` → run `dotnet
<verb> <solution> -m:maxNodes <configuration> <extraArgs>` → report `elapsedMs` and the
underlying exit code. No persisted state, no MVU workflow (constitution IV — N/A,
justified in plan).
