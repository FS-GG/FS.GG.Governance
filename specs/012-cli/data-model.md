# Phase 1 - Data Model (F12 - 012-cli)

The data model for the optional CLI boundary and the concrete F10+F11 project composition
root. The public signatures are [`contracts/Cli.fsi`](./contracts/Cli.fsi) and
[`contracts/Project.fsi`](./contracts/Project.fsi); this document explains the entities,
relationships, validation rules, and state transitions.

## 1. Command boundary entities

| Entity | Fields | Validation / Invariants |
|---|---|---|
| `CommandKind` | `RouteCommand`, `ExplainCommand`, `ContractCommand`, `EvidenceCommand` | Parsed only from `route`, `explain`, `contract`, `evidence`. Unknown names are `UsageError` (`64`). |
| `OutputFormat` | `Text`, `Json` | `--json` is an alias for `--format json`. JSON must be byte-for-byte stable for identical explicit inputs. |
| `RunRequest` | root, command, mode, format, scope, domains, review budget, review store, output path, judge identity | Root defaults to `.`, mode defaults to `Inner`, review budget defaults to `CacheOnly`, domains default to all shipped domains. Paths are normalized before execution. |
| `ReviewBudget` | `CacheOnly` or `FreshReviews count` | Count must be `>= 0`. Cache hits consume no budget. Fresh dispatches must never exceed count. |
| `BudgetState` | requested, cache hits, cache misses, fresh dispatches, pending, exhausted | Monotonic counters/lists over a run. `FreshDispatches.Length <= granted budget` is an invariant. |
| `ExitDecision` | success, governed blocking, usage error, input unavailable, tool error | Maps to fixed exit codes: `0`, `2`, `64`, `66`, `70`. JSON includes both category and code. |
| `CommandResult` | request, route, output payload, budget state, safe failures, exit decision | The single value rendered to text/JSON and returned to `Program.fs`. |

## 2. CLI MVU entities

| Entity | Role |
|---|---|
| `Model` | Durable CLI command state: raw argv, parsed request (if any), snapshot, host result, budget state, output, failures, exit decision. |
| `Msg` | Results/events accepted by the command boundary: parse completed, snapshot loaded/failed, host run completed, output write completed/failed. |
| `Effect` | I/O requested by the pure CLI update: load snapshot, run host loop with a budget gate, write report output, terminate with exit decision. |
| `CliPorts` | Edge functions for filesystem snapshot loading, host execution, output writing, stdout/stderr capture in tests, and optional packaged-tool edge. |

State transitions:

1. `init argv` parses arguments.
2. Parse failure moves directly to `Done` with `UsageError` and no snapshot effect.
3. Parse success emits `LoadSnapshot request`.
4. Snapshot failure produces `InputUnavailable` (`66`) unless the failure is an explicit governed safe-failure that can still be reported.
5. Snapshot success emits `RunHost (request, snapshot)`.
6. Host completion selects the command payload, computes the route/exit decision, and emits output/write effects.
7. Output write failure becomes `ToolError` (`70`) if it is outside governed safe-failure semantics.
8. The final `CommandResult` is rendered and `Program.fs` exits with `Cli.exitCode`.

## 3. Concrete project composition entities

| Entity | Fields | Relationships |
|---|---|---|
| `Domain` | `SpecKitDomain`, `DesignSystemDomain` | Used to select the shipped adapters for a run. Default is both. |
| `ProjectFact` | Spec Kit fact, design-system fact, project governance outcome, artifact content, evidence declaration, evidence dependency, freshness observation | The closed fact coproduct used by F12. The `Project.identify` function is the sole identity authority for F01. |
| `ProjectChange` | optional Spec Kit change, optional design-system change, scope paths | Narrows into F10/F11 domain changes for F09 lifted fences. |
| `ProjectSnapshot` | root, supplied facts, project change, artifact refs | Output of snapshot sensing and input to Host. |
| `ProjectOptions` | domains, judge identity, Spec Kit constitution dial | Controls adapter composition and project bridge construction. |
| `ProjectEvidenceReport` | evidence nodes, dependencies, freshness, host failures, disclosures | Source for the CLI `evidence` command payload. |

Composition flow:

```text
ProjectOptions
  -> SpecKit.Catalog.adapter judge dial
  -> DesignSystem.Catalog.adapter judge
  -> Composition.lift ... each adapter into ProjectFact / ProjectChange
  -> Composition.compose lifted crossDomainRules
  -> Project.toLoopConfig request snapshot
  -> Host.Loop / Host.Interpreter
```

The CLI may add cross-domain rules later, but F12 starts with an empty cross-domain rule list
unless tests need a named fixture rule. The absence of cross-domain rules is explicit and
does not change F09/F10/F11 behavior.

## 4. Command payloads

| Command | Payload | Source |
|---|---|---|
| `route` | `Route` plus budget/failure summary | `Route.route` through the Host model and `Route.renderRoute` for text. |
| `explain` | Explanation/proof entries for active rules | `Check.explain` over evaluated facts and active rules; JSON embeds `Json.ofExplanation`. |
| `contract` | `ContractEntry list` | `Contract.ofRules` over the composed catalog; text uses `Contract.render`; JSON embeds `Json.ofContract`. |
| `evidence` | `ProjectEvidenceReport` plus `BudgetState` | F05 `Evidence.effective`, F06 `Freshness`, F08 failures/disclosures, review budget state. |

## 5. Evidence report details

`EvidenceNodeReport` includes:

- `Id`: stable node id.
- `Declared`: the authored `EvidenceState`, when present.
- `Effective`: the computed state, including `AutoSynthetic` as a distinct value.
- `Freshness`: `Fresh`, `Stale`, or absent when freshness is not known.
- `Source`: domain/source label such as `speckit`, `design-system`, `review-cache`, or `host`.

`ReviewBudgetState` includes:

- requested review keys
- cache-hit keys
- cache-miss keys
- fresh-dispatch keys
- pending keys
- budget-exhausted keys

Safe failures from Host (`ArtifactUnavailable`, `ReviewDispatchFailed`,
`ReviewStoreUnavailable`) remain distinct from CLI parse errors and unexpected tool defects.

## 6. Validation rules

- Root option values must be syntactically valid during parsing. Root path existence and readability are checked before snapshot sensing; failures there become `InputUnavailable`, not parse/usage errors.
- `--mode` accepts only `sandbox`, `inner`, `gate`.
- `--format` accepts only `text`, `json`.
- `--review-budget` must parse as a non-negative integer.
- `--out` may write only the named file; no default report file is created inside the governed root.
- The review store defaults outside the governed root; an explicit store inside the root is allowed only when the caller asks for it.
- The same request and unchanged snapshot must produce identical JSON across repeated runs.
- A run with `CacheOnly` dispatches zero fresh reviews.
- A run with budget `n` dispatches at most `n` fresh reviews.
- Running commands without `--out` must not modify governed files.

## 7. Exit decision rules

| Condition | Exit |
|---|---|
| Advisory-only, no gates, or successful command output | `Success` / `0` |
| `Gate` mode route contains a failing blocking result | `GovernedBlocking` / `2` |
| Unknown command, bad option, invalid mode/format/budget | `UsageError` / `64` |
| Root unavailable, required snapshot input unreadable before a governed report can be produced | `InputUnavailable` / `66` |
| Unexpected exception, output-write defect, inconsistent internal state | `ToolError` / `70` |

Pending or over-budget agent reviews are visible in the report. They do not become a hidden
pass. In `Gate` mode, any rule that remains pending/uncertain is reflected in route/evidence
output according to the underlying rule severity and Host model; the CLI does not coerce it.
