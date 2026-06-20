# Implementation Plan: Git/CI Snapshot Facts for the Repository Boundary

**Branch**: `016-git-ci-snapshot-facts` (active spec; git branch currently `main`) | **Date**: 2026-06-20 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/016-git-ci-snapshot-facts/spec.md`

## Summary

Deliver the **sensing counterpart to F015 routing**: an edge that runs read-only `git` against a
real repository and returns a typed, deterministic **repository snapshot** — the resolved diff range
(base ref, head ref, merge base), the committed changed-path set, the working-tree dirty and untracked
sets, the current branch, optional runner-supplied CI/PR context, command-run provenance digests, and
any sensing diagnostics — with every path normalized into the **same `GovernedPath` form F015 routing
consumes**, so the snapshot's changed-path set feeds straight into `Routing.route` with no
re-normalization (SC-001). F015 only *consumed* a caller-supplied path set (its FR-011/FR-016 held
git/CI sensing out of scope); this feature *produces* that set, closing the loop.

The work lands as a new, optional, packable library **`FS.GG.Governance.Snapshot`** plus its test
project — the same shape as Config (F014) and Routing (F015). It references only
`FS.GG.Governance.Config` (for the typed-fact `GovernedPath` form and the diagnostic style) and adds
**no new third-party dependency**: read-only git is driven through BCL `System.Diagnostics.Process`,
and CI/PR context is read from BCL environment access. There is **no network** — PR labels, status
checks, and CI classification come only from runner-provided environment, never a hosting-provider API
(spec Assumptions; mirrors Host F08 SC-009).

The boundary follows the **F014 Loader pattern**, not the full F08 review-loop MVU: a thin injected-port
edge gathers all raw sensed inputs, and a **pure, total `assemble`** function parses, normalizes,
categorizes, orders, and diagnoses them into the snapshot (research D3). The pure core is unit-tested
with hand-built raw fixtures (no git needed); the edge is tested against a **real temporary git
repository** (Principle V real evidence). Read-only is guaranteed *by construction*: the edge can issue
only a closed set of read-only git subcommands (research D5).

The feature stops at the snapshot (and the resolved range). Held firm by FR-013: it does **not** route
paths to capabilities (that is F015), decide unknown-governed-path findings, assign surface classes,
build the gate registry, enforce profiles/modes, compute evidence freshness, or implement the `route` /
`ship` commands or their JSON. Those are later Phase-2 / Phase-5 / Phase-11 rows that consume this
snapshot.

**Confirmed during planning:**

- **Project home**: a new sibling library `FS.GG.Governance.Snapshot` → `Config` only; no new package
  (research D1). The snapshot model is a pure value; consumers of the *whole* snapshot are the later
  IO-edge `route`/`ship` commands (Cli), so a Config-only library keeps the dependency direction
  one-way and the kernel untouched.
- **Boundary shape**: injected-port edge (`GitPort`/`CiPort`) + pure `assemble`, justified under
  Principle III/IV exactly as F014's Loader was (research D3). Not the heavier Model/Msg/Effect/update
  loop — git sensing is request/response gather, not a convergence/retry workflow.
- **Path form**: changed/dirty/untracked paths are emitted as repo-relative `GovernedPath`s in F014's
  normalized form; the **governed-root prefix classification is routing's job (F015), not the
  snapshot's** — out-of-root paths are represented, never dropped (research D6, FR-002).
- **Normalization is single-sourced**: F014's path normalization is exposed as a public `Config`
  function and reused, so the snapshot's `GovernedPath`s are byte-identical to what routing expects
  (research D7) — a small, justified cross-feature Tier-1 touch on `FS.GG.Governance.Config`.

## Technical Context

**Language/Version**: F# on .NET, `net10.0` from `Directory.Build.props`.

**Primary Dependencies**: **No new third-party dependency.** One new `ProjectReference` —
`FS.GG.Governance.Config` — for the shared typed-fact model (`GovernedPath`) and the exposed
normalization function. Read-only git is run through BCL `System.Diagnostics.Process`; CI/PR context is
read through BCL `System.Environment`. (Snapshot does not use YamlDotNet; the transitive YamlDotNet
edge arrives only via Config and is unused by Snapshot's own code.) Test-only packages remain the
centrally pinned Expecto/FsCheck/VSTest set already in `Directory.Packages.props`.

**Storage**: None of its own. The feature reads an existing git repository **read-only** (FR-006): it
runs only `rev-parse`, `merge-base`, `diff --name-status`, `status --porcelain`, `ls-files --others`,
and `symbolic-ref`/`rev-parse --abbrev-ref` — never a command that writes the index, working tree,
refs, stash, or config. It writes nothing back.

**Testing**: `dotnet test` (Expecto via VSTest). The pure `assemble`/`planResolution`/parsers are unit-
tested with literal raw-sensing fixtures (no git): porcelain parsing, rename/delete rules, quoted-path
decoding, repo-relative `GovernedPath` normalization, change/dirty/untracked categorization, range
resolution per option form, deterministic ordering, empty-vs-failure, and FsCheck permutation-
independence. The edge `senseSnapshot` is exercised against a **real temporary git fixture repository**
(init → commit → modify/add → sense) for US1/US2/US5: changed/dirty/untracked categories, base/head
parity, detached HEAD, not-a-repo and unknown-ref diagnostics, and a before/after byte-identity check
proving read-only behavior. A `RoutingFeedTests` asserts SC-001 (the snapshot's changed paths route
through `Routing.route` unchanged). A surface-drift test guards
`surface/FS.GG.Governance.Snapshot.surface.txt`, and an FSI/prelude transcript senses a fixture repo.

**Target Platform**: Cross-platform .NET library; validated on the Linux dev host. Requires a `git`
executable on `PATH` at the edge (its absence is a sensing diagnostic, FR-008 — never a crash).

**Project Type**: Optional packable F# class library plus one test project — the same shape as Config
and Routing.

**Performance Goals**: Deterministic, bounded sensing rather than throughput. A snapshot runs a small,
fixed number of read-only git subcommands once and assembles a single value; pure assembly is
O(changed + dirty + untracked) with one sort per collection. Byte-for-byte stable snapshot for
identical repository state and resolved range (SC-002). No wall-clock, environment, process, or
absolute-host-path value enters the deterministic facts (FR-010).

**Constraints**: Read-only (FR-006), no network (no hosting-provider API; spec Assumptions), pure
deterministic assembly with deterministically-ordered collections (FR-009, SC-002/SC-003). A sensing
failure is a stable-id diagnostic, never a throw and never an empty-looking success (FR-008, FR-011).
Nondeterministic git output stays out of the facts; retained provenance is a digest kept separate
(FR-010). Snapshot requires only a working git repo + the F014 governed root, never an installed FS.GG
package in the inspected repo (FR-014). Out of scope held firm by FR-013.

**Scale/Scope**: One new production project (`src/FS.GG.Governance.Snapshot`) and one test project
(`tests/FS.GG.Governance.Snapshot.Tests`). Public modules are `Model`, `Snapshot`, and `Interpreter`,
each with a curated `.fsi` and a single combined surface baseline. Closed read-only git command set;
one closed `SensingDiagnostic` id set (`NotARepository`, `UnknownRef`, `GitUnavailable`,
`GitCommandFailed`, `UnreadableWorkingTree`). Plus one small Tier-1 surface addition to
`FS.GG.Governance.Config` (a public path-normalization `val`) so the `GovernedPath` form is
single-sourced.

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.0.0. Re-checked after Phase 1
design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | [`contracts/Model.fsi`](./contracts/Model.fsi), [`contracts/Snapshot.fsi`](./contracts/Snapshot.fsi), [`contracts/Interpreter.fsi`](./contracts/Interpreter.fsi), and [`contracts/git-sensing.md`](./contracts/git-sensing.md) define the public surface, the read-only command set, the porcelain-parse rules, and the range-resolution contract before implementation. `tasks.md` must order `.fsi` → FSI/prelude sketch → semantic tests → implementation → surface baseline. |
| II. Visibility lives in `.fsi` + surface baseline | **PASS** | `Model.fsi`, `Snapshot.fsi`, `Interpreter.fsi` are the sole public surface; `.fs` files carry no top-level access modifiers. Add `surface/FS.GG.Governance.Snapshot.surface.txt` + a surface-drift test. The Config normalization `val` is added to `Config`'s `.fsi` and its baseline is re-blessed in the same change. |
| III. Idiomatic simplicity | **PASS** | Plain records/DUs, a hand-written porcelain parser (split on the `-z` NUL delimiter), list maps/sorts for ordering. The injected-port edge over a pure `assemble` is the *lighter* boundary (vs full Model/Msg/Effect) and is justified in research D3 exactly as F014's Loader was. Any `mutable` accumulator in a parse/fold is disclosed at the use site. No SRTP, reflection, type providers, custom operators, or non-trivial computation expressions. |
| IV. Elmish/MVU boundary | **PASS** | This is an I/O feature, so the boundary is mandatory and is honored: I/O is represented as injected ports (`GitPort`/`CiPort`), `assemble`/`planResolution` are pure and total, and interpretation happens only at the edge (`senseSnapshot`), tested against a real fixture repo. Per Principle IV's explicit allowance, a **local port/effect algebra** is used rather than the Elmish `Program`/full Model-Msg-Effect loop, because sensing is fixed request/response gather with no multi-step convergence, retry, or interaction (research D3) — the same call F014's Loader made and the constitution blesses. |
| V. Test evidence mandatory | **PASS** | The edge runs against a **real temporary git repository** (init/commit/modify/add) — real evidence, no fake git. The pure core is tested with explicit literal raw fixtures. No network or agent is reached (no hosting API). No synthetic evidence is anticipated; if a raw porcelain literal stands in for an un-runnable git case it carries `Synthetic` in the test name and a use-site disclosure. |
| VI. Observability & safe failure | **PASS** | Every sensing failure (git missing, not-a-repo, unknown ref, unreadable tree, nonzero git exit) is a stable-id `SensingDiagnostic` with the failed operation and a fix hint (FR-008); a genuinely empty diff is a distinct successful outcome (FR-011). The edge never throws out of itself — a thrown `Process`/parse error is caught and reified (mirrors F08 Interpreter). Diagnostics distinguish absent/bad input from a tool defect (a tool defect is a test failure, never a `SensingDiagnostic`). |
| Change Classification | **Tier 1** | New public, packable surface (a sensing library), new public `.fsi`, new surface baseline; plus a small additive Tier-1 change to `FS.GG.Governance.Config` (one new public `val` + baseline). Adds a new *project* but **no new third-party dependency**. |
| Engineering Constraints | **PASS** | `net10.0`; `FS.GG.Governance.*` identity; one-way dependency direction (Snapshot → Config → YamlDotNet + FSharp.Core; Kernel/Host/adapters/Routing/CLI unaffected and do not reference Snapshot in this feature). No new third-party `PackageReference`; the kernel stays BCL-only. Git access is a *layered* capability in a separate project (exactly the constitution's prescription: heavier capabilities — git, filesystem scanning — layer on top in separate projects, not into the core). |

**No-new-dependency note (Engineering Constraints):** This feature adds **no** third-party
`PackageReference`. It references the existing `FS.GG.Governance.Config` project and drives read-only
git through BCL `System.Diagnostics.Process` and CI context through BCL `System.Environment`. The
constitution forbids git/filesystem-scanning **in the core rule/evidence library**; it explicitly
allows such capabilities to "layer on top in separate projects." `FS.GG.Governance.Snapshot` is that
separate layered project. The kernel and the F014 core stay clean.

**Constitution alignment on the boundary (Principle IV):** Principle IV requires that I/O features
model I/O as data behind a pure `update`/core with edge interpretation, and *permits* a local
effect/port algebra in place of the Elmish `Program`. F014's Loader took this path for reading four
files; this feature takes it for running a fixed set of read-only git commands. The justification is
recorded in research D3 so the reviewer treats the lighter boundary as a deliberate, blessed choice,
not an omission.

**Gate result: PASS — no unjustified violations. Complexity Tracking remains empty.**

## Project Structure

### Documentation (this feature)

```text
specs/016-git-ci-snapshot-facts/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   ├── Model.fsi        # snapshot-domain types: range, paths, working-tree, CI context, diagnostics, digests
│   ├── Snapshot.fsi     # pure: planResolution + RawSensing + parse/assemble + deterministic ordering
│   ├── Interpreter.fsi  # the edge: GitCommand/GitPort/CiPort/Ports, realPorts, senseSnapshot
│   └── git-sensing.md   # read-only command set + porcelain parse rules + range-resolution contract
├── checklists/
│   └── requirements.md  # spec quality checklist (already created by /speckit-specify)
├── readiness/           # FSI transcripts + SC traceability note (created during tasks)
└── tasks.md             # Created by /speckit-tasks, NOT by this command
```

### Source Code (repository root)

```text
src/FS.GG.Governance.Snapshot/                    # NEW optional sensing library
├── FS.GG.Governance.Snapshot.fsproj              # references FS.GG.Governance.Config only; no new package
├── Model.fsi                                      # = contracts/Model.fsi
├── Model.fs                                       # RepoSnapshot/DiffRange/ChangedPath/WorkingTreeState/CiContext/SensingDiagnostic/CommandRunDigest
├── Snapshot.fsi                                   # = contracts/Snapshot.fsi
├── Snapshot.fs                                    # planResolution + RawSensing + porcelain parsers + assemble + ordering (PURE)
├── Interpreter.fsi                                # = contracts/Interpreter.fsi
└── Interpreter.fs                                 # GitCommand/GitPort/CiPort/Ports, realPorts (Process+Environment), senseSnapshot (EDGE)

tests/FS.GG.Governance.Snapshot.Tests/            # NEW semantic tests
├── FS.GG.Governance.Snapshot.Tests.fsproj         # references Snapshot + Routing (for the SC-001 feed-through)
├── Support.fs                                     # raw-sensing fixture builders + temp git-repo helper (real git)
├── ResolutionTests.fs                             # planResolution per option form: --since / --base+--head / default (US3)
├── ParseTests.fs                                  # porcelain --name-status + status -z parsing; rename/delete; quoted/non-ASCII paths
├── AssembleTests.fs                               # normalize→GovernedPath; change/dirty/untracked categorization; empty-vs-failure (US1/US2/US5 pure)
├── DeterminismTests.fs                            # assemble twice → identical; FsCheck permutation of raw entries (SC-002/SC-003)
├── SensingTests.fs                                # edge senseSnapshot over a REAL temp git fixture repo; read-only byte-identity (US1/US2/US5 real)
├── RoutingFeedTests.fs                            # SC-001: snapshot changed paths consumed by Routing.route with no re-normalization
├── SurfaceDriftTests.fs                           # baseline drift check
└── Main.fs

src/FS.GG.Governance.Config/Model.fsi (+ Model.fs) # CHANGED: expose the public path-normalization val (single-source the GovernedPath form)
scripts/prelude.fsx                               # extend with a Snapshot sense sketch
surface/FS.GG.Governance.Snapshot.surface.txt      # NEW public surface baseline
surface/FS.GG.Governance.Config.surface.txt        # UPDATED for the new Config normalization val
FS.GG.Governance.sln                              # add Snapshot project and Snapshot test project
CLAUDE.md                                          # SPECKIT block repointed to this plan
```

**Structure Decision**: a new `FS.GG.Governance.Snapshot` class library, sibling to
Kernel/Host/adapters/Config/Routing, is the home for git/CI sensing. It references only
`FS.GG.Governance.Config` and adds no third-party dependency, keeping the dependency direction one-way
(Snapshot → Config) and the kernel/host/routing untouched. Splitting `Snapshot` (the pure
parse/assemble core) from `Interpreter` (the read-only git/CI edge) places the bug-prone parsing and
normalization under literal fixtures while the edge is a thin, real-git-tested shell — the same
pure-core / IO-edge split F014 used (Schema vs Loader). The model lives here, not in Config, because it
is *sensed* (a use of git), not *parsed from YAML*; it lives here, not in Host, because the whole-
snapshot consumers are the later `route`/`ship` CLI commands, and a Config-only library lets them
reference the snapshot without dragging in the F08 review-loop surface.

## Complexity Tracking

> No unjustified Constitution Check violations.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| - | - | - |
