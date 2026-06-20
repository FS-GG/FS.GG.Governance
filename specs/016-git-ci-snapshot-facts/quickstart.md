# Quickstart: Git/CI Snapshot Facts (F016)

This is a validation/run guide proving the feature works end-to-end. It references the contracts
([Model.fsi](./contracts/Model.fsi), [Snapshot.fsi](./contracts/Snapshot.fsi),
[Interpreter.fsi](./contracts/Interpreter.fsi), [git-sensing.md](./contracts/git-sensing.md)) and the
[data model](./data-model.md) rather than restating them. Implementation bodies live in `tasks.md` and
the implementation phase.

## Prerequisites

- .NET SDK with `net10.0` (per `Directory.Build.props`).
- A `git` executable on `PATH` (its absence is a `GitUnavailable` diagnostic, not a crash — FR-008).
- Built Config (for the typed-fact `GovernedPath` form + the exposed normalizer) and Routing (for the
  SC-001 feed-through test):

```bash
dotnet build src/FS.GG.Governance.Config
dotnet build src/FS.GG.Governance.Routing
dotnet build src/FS.GG.Governance.Snapshot
```

## FSI sketch (Principle I — exercise the surface before the .fs bodies)

`scripts/prelude.fsx` is extended to sense a fixture repo through the packed library:

```fsharp
#r "src/FS.GG.Governance.Config/bin/Debug/net10.0/FS.GG.Governance.Config.dll"
#r "src/FS.GG.Governance.Snapshot/bin/Debug/net10.0/FS.GG.Governance.Snapshot.dll"
open FS.GG.Governance.Snapshot
open FS.GG.Governance.Snapshot.Model

// (1) Pure range planning — no git, no repo (US3):
let plan = Snapshot.planResolution { Since = Some (GitRef "main"); Base = None; Head = None }
// plan.Form = Since (GitRef "main"); plan.UseMergeBase = true

// (2) Pure assembly over a hand-built RawSensing — no git (US1/US2):
//     parse a one-file diff + a dirty + an untracked path, assert categories & order.
// let snap = Snapshot.assemble rawFixture

// (3) Edge sensing over the REAL repo this prelude runs in (US1/US5):
let ports = Interpreter.realPorts "."
let snap = Interpreter.senseSnapshot ports { Since = None; Base = Some (GitRef "HEAD~1"); Head = Some (GitRef "HEAD") }
printfn "range   = %A" snap.Range
printfn "changed = %A" (snap.Changed |> List.map (fun c -> c.Kind, c.Path))
printfn "dirty   = %A" snap.WorkingTree.Dirty
printfn "diags   = %A" snap.Diagnostics
```

The shapes must typecheck against the stub `.fs` before any real body exists; that is the point of the
design pass.

## Validation scenarios (map to spec Success Criteria)

Run the suite:

```bash
dotnet test tests/FS.GG.Governance.Snapshot.Tests
```

| Scenario | How it runs | Proves |
|---|---|---|
| **Changed-path sensing** — fixture repo, known base/head differ by 2 files | `SensingTests` over a real temp repo | SC-001 (with the feed-through below), US1 |
| **Feed-through to routing** — snapshot `Changed` → `Routing.route`, no re-normalization | `RoutingFeedTests` (refs Routing) | **SC-001** |
| **Determinism** — assemble the same raw twice → identical; sense twice → identical | `DeterminismTests`, `SensingTests` | **SC-002** |
| **Permutation independence** — FsCheck shuffles raw diff/status entries → identical snapshot | `DeterminismTests` (FsCheck) | **SC-003** (and FR-009) |
| **Working-tree categories** — fixture with committed + dirty + untracked | `SensingTests`, `AssembleTests` | **SC-003**, US2 |
| **Range parity** — same options resolve identically local vs CI-shaped | `ResolutionTests` | **SC-004**, US3 |
| **Safe failure + read-only** — not-a-repo / unknown-ref diagnostics; before/after byte-identity | `SensingTests` | **SC-005**, US5 |
| **No nondeterminism in facts** — no raw output/timing/abs path; provenance is a digest | `AssembleTests` + surface review | **SC-006**, FR-010 |
| **No network** — injected `CiPort`; no provider-API call in the library | `SensingTests` + code review | **SC-007** |
| **Porcelain parsing** — rename/delete/quoted/non-ASCII via `-z` literal fixtures | `ParseTests` | FR-012 |
| **Surface drift** — `surface/FS.GG.Governance.Snapshot.surface.txt` matches | `SurfaceDriftTests` | Principle II |

## Building a real fixture repo (Support.fs, Principle V — real evidence)

`Support.fs` creates a disposable temp directory and drives the **real** `git` to set up a known state
(read-only sensing is what's under test; setup uses ordinary git):

```text
git init -q ; git config user.email/user.name
write + git add + git commit  (base)
modify a tracked file ; add an untracked file ; commit a second file (head)
→ senseSnapshot over realPorts tmpDir
assert: Changed = [the committed file]; Dirty = [the modified file]; Untracked = [the new file]
assert: tmpDir content+ref hash identical before/after sense (read-only, SC-005)
```

A `Synthetic`-named test MAY substitute a literal porcelain string for a git case that cannot be staged
on the host (e.g. a forced type-change), with a use-site `// SYNTHETIC:` disclosure (Principle V).

## Surface baseline

Regenerate and bless the new baseline plus the updated Config baseline (the new normalization `val`):

```bash
# Snapshot baseline (new)
surface/FS.GG.Governance.Snapshot.surface.txt
# Config baseline (updated for the exposed normalizer — research D7)
surface/FS.GG.Governance.Config.surface.txt
```

The `SurfaceDriftTests` (Snapshot) and the existing Config surface-drift test both go green only after
the baselines are re-blessed in the same change (Principle II / Tier 1).
