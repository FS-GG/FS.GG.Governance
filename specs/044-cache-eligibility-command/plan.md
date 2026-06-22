# Implementation Plan: Cache-Eligibility Host Command (Sense → Resolve → Evaluate → Emit)

**Branch**: `044-cache-eligibility-command` | **Date**: 2026-06-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/044-cache-eligibility-command/spec.md`

## Summary

Land the **first end-to-end composition** of the cache-eligibility thread: a standalone host command,
`fsgg cache-eligibility`, delivered as a new project **`FS.GG.Governance.CacheEligibilityCommand`** that
mirrors the merged `RouteCommand` (F022) pure-MVU-core + injected-ports shape. Every pure core it needs is
already merged — F018 gate selection, F019 route, F029 freshness inputs, F030 evidence reuse, F041
`CacheEligibility.evaluate`, F042 `CacheEligibilityJson.ofReport`, F043 `FreshnessResolution.resolve`. What no
command yet does is **sense the facts those cores consume from a real repository and run the pipeline**.

The command: (1) **reuses the F022 route composition verbatim** — the exact `Snapshot` scope sensing,
`Config` catalog load/validate, `Routing.route` → `Gates.buildRegistry` → `Findings.findUnknownGovernedPaths`
→ `Route.select` call sequence — to obtain the selected gates as full F018 `Gate` records (off
`RouteResult.SelectedGates`, each carrying its five-field `FreshnessKey`); (2) **senses each selected gate's
freshness facts** at the effects boundary behind a new injected `FreshnessSensor` port — the rule-pack hash,
generator version, per-gate covered-artifact hashes, per-command command version — while taking **base/head
revisions for free** from the already-sensed `RepoSnapshot.Range` (no new git call); (3) assembles a
`FreshnessResolution.SensedFacts` bundle and calls F043 `resolve`, **fabricating nothing** (a fact that cannot
be sensed is left `None`/absent-key, never defaulted); (4) bridges every `Resolved` gate to an F041
`CandidateGate` via F043's `candidate` accessor and runs F041 `evaluate` over them against a **read-only**
evidence-reuse store loaded from disk (absent ⇒ F030 `empty`); (5) renders the result through F042
`ofReport` **verbatim** to a deterministic, versioned `cache-eligibility.json`; (6) writes the gates F043 left
`Unresolved` to a **companion `cache-eligibility.unresolved.json`** sidecar
(`fsgg.cache-eligibility.unresolved/v1`) — named missing facts per gate, never marked reusable, never silently
dropped; and (7) prints a deterministic human-or-JSON summary distinguishing reusable / must-recompute /
recompute-by-default-unresolved gates.

Cache eligibility is **information, not a verdict**: the command exits 0 whenever it senses the repo, loads a
valid catalog, and writes the artifacts — a gate that "must recompute" or is "unresolved" is information, never
a tool failure; it assigns no severity, profile, mode, enforcement, ship verdict, exit-code-from-blockers, or
provenance. It is deliberately **standalone**: the eventual home of the verdict is inside `route.json` /
`audit.json`, but embedding it there would edit the merged F020/F025 cores and their committed baselines — this
row leaves those untouched and emits the sibling artifacts, exactly as F042 delivered the projection
standalone. The embed is the next row.

The command **computes no hash, freshness key, or cache decision of its own** (FR-012/FR-013): hashing/sensing
happens only inside the injected `Interpreter` boundary; the pure `Loop` consumes already-sensed opaque values
and calls the merged cores. The only from-scratch authored logic is (a) the `FreshnessSensor` interpreter that
senses the new facts, (b) a **minimal read-only `ReuseStore` deserializer** (none exists in the repo today —
writing/evicting evidence stays out of scope, deferred to the cache-storage row), and (c) the deterministic
`cache-eligibility.unresolved.json` renderer built from F043's public `missingFactToken` / `gateIdValue`
accessors. All merged `src/`, `surface/`, and merged test projects are **untouched** — the work is a new host
project + its test project + one new surface baseline (additive-only, SC-007/SC-008).

The contracts this row commits live in [contracts/cache-eligibility-command-cli.md](./contracts/cache-eligibility-command-cli.md)
(the verb, flags, exit codes, and the pure `Loop`/`Interpreter` surface) and
[contracts/cache-eligibility-artifacts.md](./contracts/cache-eligibility-artifacts.md) (the two on-disk
documents and the summary). The full vocabulary and the sense→resolve→evaluate→emit pipeline are in
[data-model.md](./data-model.md); the build/exercise/test walkthrough is in [quickstart.md](./quickstart.md).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (repo standard; `Nullable=enable`, `TreatWarningsAsErrors=true`
inherited from `Directory.Build.props`). One new `src/` host project — `Loop.fsi/fs` (pure MVU) then
`Interpreter.fsi/fs` (edge ports) then `Program.fs` — plus one new test project. The `PackAsTool`
`ToolCommandName` is `fsgg` (the same multi-verb tool as `RouteCommand`); this project owns the
`cache-eligibility` verb.

**Primary Dependencies**: `ProjectReference`s only — the F022 selection cores **`Config`** (F014, catalog
load/validate), **`Snapshot`** (F016, git scope sensing + `RepoSnapshot.Range` base/head), **`Routing`**
(F015), **`Findings`** (F017), **`Gates`** (F018), **`Route`** (F019) — plus the cache-eligibility cores
**`FreshnessResolution`** (F043, `resolve`/`SensedFacts`/`candidate`/`missingFactToken`), **`CacheEligibility`**
(F041, `evaluate`/`CandidateGate`), **`CacheEligibilityJson`** (F042, `ofReport`/`schemaVersion`), and
**`EvidenceReuse`** (F030, `ReuseStore`/`empty`). Several of these (CacheEligibility, EvidenceReuse,
FreshnessKey, Gates, Config) arrive transitively through FreshnessResolution/CacheEligibilityJson; the direct
ones above are listed explicitly (the F043 precedent — transitive references flow, no
`DisableTransitiveProjectReferences`). **No new third-party `PackageReference`**: the only new impure
primitives are `System.IO` reads/writes (catalog already via `Config`, the new `FreshnessSensor` hashing, the
read-only store load, the atomic `ArtifactWriter`), `System.Security.Cryptography` for the rule-pack/artifact
hashing at the boundary, and `Environment.Exit` at the `Program` edge — all BCL. Test frameworks already on the
central feed (`Directory.Packages.props`): **Expecto**, **Expecto.FsCheck**, **FsCheck**,
**Microsoft.NET.Test.Sdk**, **YoloDev.Expecto.TestSdk**.

**Storage**: Read-only catalog (via `Config`), read-only evidence-reuse store (new minimal deserializer;
absent ⇒ F030 `empty`), and two written artifacts (`cache-eligibility.json`,
`cache-eligibility.unresolved.json`) plus stdout. No database; no evidence is recorded, evicted, or expired
(out of scope, FR-006). Writes use the `RouteCommand` atomic temp-write-then-`File.Move(_, _, true)` pattern —
no partial artifact on failure.

**Testing**: Expecto + FsCheck over the **public** surface (`Loop.parse`/`init`/`update`/`render`/`exitCode`
and `Interpreter.run`), mirroring the `RouteCommand.Tests` three-tier shape (Principle V): (1) **pure Loop
tests** — `init`/`update` over fixed selected gates + fixed `SensedFacts` + fixed `ReuseStore`, asserting the
emitted effects and the computed artifact strings; (2) **interpreter tests over faked ports** — in-memory
`Config` reader + canned `Snapshot` git port + a fake `FreshnessSensor` returning fixed facts + an in-memory
store reader + capturing `ArtifactWriter`/`OutputSink`, verifying the written `cache-eligibility.json` equals a
genuine `CacheEligibilityJson.ofReport` of the expected report and the sidecar names exactly the missing facts;
(3) **one end-to-end test** — a real temp git repo + real `.fsgg` catalog + `Interpreter.realPorts`, asserting
the two artifacts validate against their schemas and are **byte-identical** when re-run from a different working
directory (SC-004). Concern coverage: US1 emit+reusable (SC-001/SC-002), US2 no-hide unresolved
(SC-003/SC-005), US3 determinism (SC-004), exit-code-as-information (SC-006), additive-only surface drift
(SC-007/SC-008), and the failure short-circuits (Edge: no/invalid catalog, not-a-git-repo, unwritable output →
non-zero, no partial artifact). The `FreshnessSensor` and `Judge`-equivalents are faked (a real `git`/hash is a
poor reproducible oracle); catalog reads and the store/artifact I/O are **real filesystem** in the interpreter
and end-to-end tiers.

**Target Platform**: Developer / CI .NET SDK running `dotnet test` and the packed `fsgg` tool. No OS-specific
surface beyond `git` (sensed only through the injected `Snapshot` port at the edge).

**Project Type**: A new host/edge composition (CLI command), modeled through an Elmish/MVU boundary
(Principle IV, load-bearing): pure `Loop` + edge `Interpreter` + thin `Program`. Not a pure core; not a library.

**Performance Goals**: N/A. The contract is **honest sensing, determinism, byte-stability, and no-hide
attribution**, not latency. Sensing is a handful of hashes and one `git` range per run; the pipeline is a
per-gate map over the selected gates.

**Constraints**: Deterministic / byte-stable (FR-008): identical repo state + store ⇒ byte-identical artifacts
and summary regardless of cwd, process, ambient ordering, or wall-clock; **no wall-clock value is surfaced** in
this MVP, so F034 `SensedMetadata` is not referenced (if a sensed timestamp is ever surfaced it MUST be F034-
marked and excluded from reproducible content). No fabrication / no-hide (FR-003/FR-005): a fact that cannot be
sensed is left unsensed (`None`/absent key), surfaced as recompute-by-default with the exact missing facts
named, never defaulted or zero-filled; **sensed-empty is distinguished from unsensed** (an empty
covered-artifact list is a present `Map` key). Information, not verdict (FR-009): exit 0 whenever sense+load+
write succeed; non-zero only on genuine sensing/catalog/write failure, writing no partial artifact (FR-010).
Reuse cores verbatim (FR-012/FR-013): no freshness key, hash, or cache decision computed outside the injected
boundary or the merged cores. Standalone (FR-011): no edit to F020 `route.json` / F025 `audit.json` cores or
baselines.

**Scale/Scope**: One new `src/` host project (`Loop.fsi/fs` + `Interpreter.fsi/fs` + `Program.fs`); one new
test project; one new surface baseline `surface/FS.GG.Governance.CacheEligibilityCommand.surface.txt`; two
solution entries; a short `scripts/prelude.fsx` FSI section (design-first proof, Principle I); the `CLAUDE.md`
plan pointer. Zero changes to existing `src/`, `surface/`, or merged test projects.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | **PASS** | The public surface is drafted as `Loop.fsi` + `Interpreter.fsi` and exercised in `scripts/prelude.fsx` (a new F044 section) before any `.fs` body exists; semantic tests call the public `parse`/`init`/`update`/`render`/`run`, never private sensing/join helpers (the `RouteCommand` precedent). |
| II. Visibility in `.fsi` | **PASS** | Two curated `.fsi` files (`Loop`, `Interpreter`) are the sole public-surface declaration; `.fs` files carry no access modifiers, and every sensing/codec/render helper stays unexposed by absence from the `.fsi`. A new `surface/FS.GG.Governance.CacheEligibilityCommand.surface.txt` baseline is added and guarded by a reflective `SurfaceDrift` test (the F018–F043 precedent) with the `BLESS_SURFACE=1` re-bless path. |
| III. Idiomatic Simplicity | **PASS** | `List.map`/`List.partition` over selected gates and resolution entries; exhaustive closed-DU `match`es over `Effect`/`Msg`/`ResolutionOutcome`/`CacheEligibilityVerdict` (wildcard-free); a small per-line read-only store parser. No SRTP, reflection (outside the surface test), custom operators, type providers, or non-trivial CEs. Records over hierarchies; `option`/`Map` over sentinels. The MVU boundary is the one justified piece of structure (Principle IV). |
| IV. Elmish/MVU is the boundary for stateful/I/O | **PASS (load-bearing)** | The command is exactly the I/O-bearing workflow Principle IV governs — it senses git/filesystem, loads a store, and persists artifacts. It is modeled as a PURE `Loop` (`Model`/`Msg`/`Effect`, total `update`, no I/O) + an EDGE `Interpreter` (injected fakeable `Ports`: `Config` reader, `Snapshot` git, the new `FreshnessSensor`, the read-only `StoreReader`, the atomic `ArtifactWriter`, the `OutputSink`; `realPorts`/`step`/`run`) + a thin `Program` — the `RouteCommand` shape verbatim. Both sides are tested (pure transition + real-edge interpreter). |
| V. Test Evidence Is Mandatory | **PASS** | Real F018 `Gate`s, real F029 newtypes, real F041 `evaluate`, real F042 `ofReport`, real filesystem catalog/store/artifact I/O at the edge, and one real-`git` end-to-end run (Principle V). Tests fail before `update`/`Interpreter.run` match the contract and pass after. The `FreshnessSensor` and the `git` port are **faked** in the unit/interpreter tiers — a real hash/`git` is a non-reproducible oracle; this is disclosed with the `Synthetic` token at the use site and in the PR (Principle V), with the real path proven once in the end-to-end tier. |
| VI. Observability & Safe Failure | **PASS** | Operationally-significant events — scope-sensing failure, catalog load/validate failure, store-load failure, unwritable output — emit structured diagnostics and map to distinct non-zero exit codes (`UsageError`/`InputUnavailable`/`ToolError`), kept **distinct** from the exit-0 "must recompute"/"unresolved" information outcome (FR-009/FR-010, Constitution VI "distinguish a tool defect from missing/malformed input"). No silent failure: every effect is guarded in the interpreter and reified to a `Msg`; a missing freshness fact is **named** (no-hide), never swallowed. No partial artifact on failure (atomic write). |
| Change Classification | **Tier 1 (contracted change — new public CLI surface + assembly)** | Adds a new public command/assembly (`FS.GG.Governance.CacheEligibilityCommand`), a new CLI verb (`fsgg cache-eligibility`), two new artifact schemas (`cache-eligibility.json` reused from F042 + the new `fsgg.cache-eligibility.unresolved/v1` sidecar), and a new surface baseline ⇒ full chain: spec, plan, `.fsi`, baseline, tests, docs. **No new third-party dependency.** No existing public API, baseline, or merged behavior altered (F014–F043 consumed verbatim). |
| Engineering Constraints | **PASS** | F#/.NET `net10.0`; no new third-party `PackageReference`; references only sibling governance projects; git/filesystem reached only through injected ports (genericity — no rendering package IDs/paths/templates assumed; the repo is one external customer supplying its own `.fsgg` catalog). `PackAsTool`/`ToolCommandName=fsgg`; pack output `~/.local/share/nuget-local/` unaffected. Structured-logging TODO unaffected (diagnostics are emitted through the `OutputSink`/stderr edge, not a logging library). |

**Gate result: PASS — no unjustified violations. Complexity Tracking is empty.** Principle IV is load-bearing
and satisfied by the `Loop`/`Interpreter`/`Program` split; I, II, III, V, VI all have concrete targets and
pass. The only from-scratch authored logic (the `FreshnessSensor`, the read-only `ReuseStore` deserializer, the
unresolved-sidecar renderer) lives behind the injected boundary or in the pure render path and computes no
freshness key / cache decision of its own — the merged cores own all of those.

## Project Structure

### Documentation (this feature)

```text
specs/044-cache-eligibility-command/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 — decisions D1–D9 (new host project mirroring RouteCommand; replicate the
│                        #            F022 selection call-sequence vs ProjectReference RouteCommand; selected
│                        #            Gate list off RouteResult.SelectedGates; FreshnessSensor port + base/head
│                        #            free from RepoSnapshot.Range; SensedFacts assembly + no-hide sensing;
│                        #            read-only ReuseStore deserializer from scratch; unresolved sidecar schema;
│                        #            verb/flags/exit codes; determinism + no surfaced wall-clock)
├── data-model.md        # Phase 1 — the Loop vocabulary (RunRequest/Model/Msg/Effect/Phase/ExitDecision), the
│                        #            sense→resolve→evaluate→emit pipeline field-by-field, the reused cores, the laws
├── quickstart.md        # Phase 1 — build, FSI-exercise, test, re-bless the surface, run the packed verb
├── contracts/           # Phase 1 — the contracts this row commits
│   ├── cache-eligibility-command-cli.md   # verb, flags, exit codes, the pure Loop + Interpreter port surface
│   └── cache-eligibility-artifacts.md     # the two on-disk documents (F042 verbatim + the unresolved sidecar) + summary
└── tasks.md             # Phase 2 — /speckit-tasks output (NOT created here)
```

### Source Code / deliverable layout (repository root)

```text
src/FS.GG.Governance.CacheEligibilityCommand/                     # NEW — the cache-eligibility host edge (fsgg cache-eligibility)
├── Loop.fsi                                                      # NEW — RunRequest/Model/Msg/Effect/Phase/ExitDecision + parse/init/update/render/exitCode (pure)
├── Loop.fs                                                       # NEW — the pure MVU composition (no access modifiers; sensing/codec private by omission)
├── Interpreter.fsi                                               # NEW — Ports (Config reader, Snapshot git, FreshnessSensor, StoreReader, ArtifactWriter, OutputSink) + realPorts/step/run
├── Interpreter.fs                                                # NEW — the edge: real freshness sensing, read-only store load, atomic write; guarded/total
├── Program.fs                                                    # NEW — thin entry: argv → parse → realPorts → run → exitCode
└── FS.GG.Governance.CacheEligibilityCommand.fsproj              # NEW — Exe; PackAsTool fsgg; references the F022 selection cores + F030/F041/F042/F043

tests/FS.GG.Governance.CacheEligibilityCommand.Tests/            # NEW — semantic tests over the PUBLIC surface (Expecto + FsCheck)
├── Support.fs                                                    # NEW — real Gate/catalog fixtures, fake FreshnessSensor + canned git port + in-memory store/reader/writer, expected-report computers (no mocks of cores)
├── ParseTests.fs                                                 # NEW — parse: verb + flags (repo/scope/store/out/format), usage errors as values
├── LoopTests.fs                                                  # NEW — pure init/update: selection → sense → resolve → evaluate → project; emitted effects + computed artifact strings (US1, SC-001/SC-002)
├── UnresolvedTests.fs                                            # NEW — US2: a gate missing a sensed fact → sidecar names exactly the missing facts, never reusable (SC-003)
├── SensedEmptyTests.fs                                           # NEW — US2 edge: sensed-empty covered set resolves; absent command ⇒ absent command version, never unresolved on that basis (SC-005)
├── InterpreterTests.fs                                           # NEW — faked ports: written cache-eligibility.json = genuine F042 ofReport; sidecar correct; absent store ⇒ empty ⇒ noPriorEvidence
├── DeterminismTests.fs                                           # NEW — US3: byte-identical artifacts across input order / cwd; GateId order (SC-004)
├── FailureTests.fs                                               # NEW — Edge: no/invalid catalog, not-a-git-repo, unwritable output → non-zero, no partial artifact (SC-006, FR-010)
├── ExitInformationTests.fs                                       # NEW — exit 0 when all must-recompute or some unresolved (FR-009, SC-006)
├── EndToEndTests.fs                                              # NEW — real temp git repo + real catalog + realPorts; schema-valid + byte-identical re-run (SC-004); the one real-git proof
├── SurfaceDriftTests.fs                                          # NEW — Principle II surface baseline + reference-scope guard (additive-only)
├── Main.fs                                                       # NEW — Expecto entry point
└── FS.GG.Governance.CacheEligibilityCommand.Tests.fsproj        # NEW — references CacheEligibilityCommand (+ the cores for expected-report computation); test packages

surface/FS.GG.Governance.CacheEligibilityCommand.surface.txt     # NEW — Tier-1 public-surface baseline (BLESS_SURFACE=1 generated)
scripts/prelude.fsx                                              # EDIT — append a short F044 FSI section (design-first proof)
FS.GG.Governance.sln                                            # EDIT — add the two new projects
CLAUDE.md                                                       # EDIT — point the SPECKIT plan reference at this plan
```

**Structure Decision**: One new host/edge project `src/FS.GG.Governance.CacheEligibilityCommand`, mirroring the
merged `FS.GG.Governance.RouteCommand` (F022) exactly: a pure `Loop` (`Model`/`Msg`/`Effect` + total
`update`), an edge `Interpreter` (injected fakeable `Ports` + `realPorts`/`step`/`run`), and a thin `Program`,
compiled `Loop → Interpreter → Program`. It **replicates the F022 selection call-sequence** (`Snapshot` scope →
`Config` load → `Routing.route`/`Gates.buildRegistry`/`Findings.findUnknownGovernedPaths`/`Route.select`)
rather than taking a `ProjectReference` on `RouteCommand` — `RouteCommand` exposes no reusable selection
function (its composition lives inside its own `Loop.update`), and referencing its `Exe`/MVU would couple this
command to `RouteCommand`'s argv and effect shape (research D2). It then adds only the new pipeline tail:
`FreshnessSensor` sensing → `SensedFacts` → F043 `resolve` → F041 `evaluate` (against the loaded F030 store) →
F042 `ofReport` + the unresolved sidecar. The project is additive: no existing `src/`, `surface/`, or merged
test project changes (SC-007/SC-008).

## Complexity Tracking

> No Constitution Check violations. This section is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
