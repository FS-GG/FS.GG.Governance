# Phase 1 — Data Model: the project-graph the guards read

The guards operate on a small, read-only model derived from parsing the repo's `.fsproj`
files (via `git ls-files '*.fsproj'`). No persistence; the model is rebuilt per test run.

## Entity: `ProjectNode`

One per tracked `.fsproj`.

| Field | Type | Source | Notes |
|---|---|---|---|
| `Name` | string | file/dir name | e.g. `FS.GG.Governance.RouteCommand` |
| `Path` | string | tracked path | repo-relative |
| `OutputType` | `Exe \| Library` | `<OutputType>` | absent ⇒ `Library` |
| `PackAsTool` | bool | `<PackAsTool>` | tool packability |
| `ToolCommandName` | string option | `<ToolCommandName>` | invocation name when packed as a tool |
| `IsPackable` | bool | `<IsPackable>` | new libs set `false` |
| `PackageReferences` | string set | `<PackageReference Include=...>` | **direct** deps only (not transitive) |
| `ProjectReferences` | string set | `<ProjectReference Include=...>` | direct edges; resolved to `Name` |

## Entity: `FenceRule`

The authoritative allowlist a guard asserts (see `contracts/dependency-fences.md`).

| Rule | Predicate over the graph | Passes when |
|---|---|---|
| **YAML owner** | `{ p.Name : "YamlDotNet" ∈ p.PackageReferences }` | equals the documented owner allowlist exactly (no undocumented member, no missing member) |
| **Exe leaf** | for each `p` with `OutputType = Exe`: the `ProjectReference` closure of `p` contains no other `Exe` node | the set of offending exe→exe edges is empty |
| **Single `fsgg`** | `{ p.Name : p.ToolCommandName = Some "fsgg" }` | has cardinality ≤ 1 |

## Entity: `Violation`

Emitted by a matcher when a `FenceRule` fails; drives the actionable diagnostic.

| Field | Type | Meaning |
|---|---|---|
| `Rule` | rule id | which fence broke |
| `Project` | string | the offending project |
| `Detail` | string | e.g. `"declares direct YamlDotNet but is not in the documented owner set"` / `"Exe references Exe: Cli → RouteCommand"` / `"second project claiming fsgg"` |

## Derived: exe→exe reachability

`Exe leaf` needs the **transitive** `ProjectReference` closure (an exe must not reach
another exe even indirectly). Compute reachability over the `ProjectReferences` edges;
for each `Exe` node, assert no reachable node is `Exe` (excluding itself). Today the only
edges are `Cli → RouteCommand` (direct) and `EvidenceCommand → Cli → RouteCommand`
(direct + transitive); after extraction both must be gone.

## Invariants (asserted by the guards)

- **INV-1**: the direct-`YamlDotNet` set == documented owner allowlist.
- **INV-2**: no `Exe` node reaches another `Exe` node via `ProjectReferences`.
- **INV-3**: `|{ p : p.ToolCommandName = "fsgg" }| ≤ 1`.
- **INV-4** (sanity, not enforced by change): every parsed `.fsproj` resolves its
  `ProjectReference` targets to a known `ProjectNode` (a dangling ref fails the parse loudly).
