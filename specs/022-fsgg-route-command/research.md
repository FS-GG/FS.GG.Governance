# Phase 0 Research: `fsgg route` Host Command

This feature ships **no new algorithm** — every routing/selection/serialization decision is already
fixed by F014–F021. Research therefore resolves the *composition and host-edge* questions the spec
deferred to plan time, plus the boundary shape the constitution requires. Each decision is recorded as
Decision / Rationale / Alternatives considered. No `NEEDS CLARIFICATION` remain after this phase.

---

## D1 — Project home

**Decision**: A new packable project **`FS.GG.Governance.RouteCommand`** (OutputType `Exe`,
`PackAsTool=true`, `ToolCommandName=fsgg`) referencing the eight cores it composes. It is the
composition/edge tier, parallel to `Host`/`Cli`, not a new pure leaf and not an extension of the
kernel-era projects.

**Rationale**: The one-row-one-project rhythm (F014–F021) plus the constitution's "heavier capabilities
layer on top in separate projects, not into the core" both point to a fresh project. Putting the edge
here keeps every referenced core free of any host/CLI surface. The existing `FS.GG.Governance.Cli` is an
explicitly distinct *kernel-era* lineage (route/explain/contract/evidence over the kernel MVU with its
own double-layered `Cli` MVU and `fsgg-governance` tool name); reusing its internals would entangle the
new Phase-2 cores with the kernel boundary the spec says not to assume. A new project is the clean cut.

**Alternatives considered**: (a) *Extend `FS.GG.Governance.Cli`* — rejected: distinct lineage, wrong
MVU, would couple Phase-2 cores to the kernel shell. (b) *Extend `FS.GG.Governance.Host`* — rejected:
`Host` is the kernel governance-loop driver, not a command host. (c) *A pure library + a separate exe* —
rejected as premature; one project with `Loop`/`Interpreter`/`Program` mirrors `Host`+`Cli` collapsed to
the single command this row ships, and the test project references the Exe exactly as `Cli.Tests` does.

---

## D2 — Boundary shape (MVU vs pure function)

**Decision**: A **local MVU/effect algebra** — pure `Model`/`Msg`/`Effect`/`init`/`update` in `Loop`,
plus an edge `Interpreter` that executes effects through injected ports. Not a pure function, and not the
full Elmish `Program` runtime.

**Rationale**: Constitution Principle IV *mandates* an MVU boundary once behavior includes multi-step
state or I/O — and this command senses git, reads a catalog, and writes two-plus files in sequence, with
early-exit failure paths between steps. That is precisely the case Principle IV names. A pure function
(the F020/F021 shape) is therefore not permissible here. The lighter local algebra (the shape
`Host.Loop`/`Host.Interpreter`, `Snapshot`, and `Config.Loader` already use) is the idiomatic choice for
a small tool (Principle III, Principle IV's "local MVU/effect algebra is acceptable for libraries, CLIs,
and small tools"): `update` stays pure and exhaustively testable; I/O is `Effect` data; interpretation
happens only at the edge.

**Alternatives considered**: (a) *Pure function* — rejected: violates Principle IV for an I/O workflow.
(b) *Elmish `Program`/`Cmd`/subscriptions* — rejected: no long-running loop, subscriptions, or user
interaction; the fixed sense→load→…→persist pipeline has no need for the runtime, and adding the Elmish
package would breach dependency-minimalism.

---

## D3 — Port composition (reuse vs new)

**Decision**: The interpreter's injected `Ports` bundle **reuses the existing edge ports** and adds one
new write port:
- catalog reads via **`Config.Loader.FileReader`** (and `Config.Loader.readSource` + `Schema.validate`,
  or `loadAndValidate` with the real reader) — not a new file-read path;
- git sensing via **`Snapshot.Interpreter.Ports`** + `Snapshot.Interpreter.senseSnapshot` — not new git;
- a **new `ArtifactWriter = path:string -> content:string -> Result<unit,string>`** port for persistence;
- a **new `OutputSink = string -> unit`** port for the summary (stdout).

**Rationale**: The spec's "Reuse, don't re-derive" assumption is explicit: compose the existing sensing
edges verbatim, add only the persistence edge. `Config` and `Snapshot` already isolate their I/O behind
injected function-record ports, exercised in tests with in-memory/temp-repo fakes; consuming those same
ports means the new command's git/catalog I/O is *already* fakeable, and the only genuinely new impure
primitive is a file *write*. Modeling the write as an injected `Result`-returning port (mirroring
`Config.Loader.FileReader`'s `Ok/Error` discipline) keeps "unwritable output" a value, not an exception,
satisfying FR-010/FR-012/SC-007.

**Alternatives considered**: (a) *Call `realPorts`/`loadAndValidate`/`senseSnapshot` directly with no
re-injection* — rejected: the convenience functions bind real I/O and aren't fakeable; tests need the
port-level seams. (b) *A single god-port* — rejected: separate `FileReader`/git `Ports`/`ArtifactWriter`/
`OutputSink` keep each failure category and each fake independent.

---

## D4 — Scope selection semantics

**Decision**: Three mutually-exclusive scope selectors resolve to a `GovernedPath list` of candidate
changed paths:
- `--paths p1 p2 …` → the candidate set **is** that explicit list (normalized via the same
  `Config.Model.normalizePath` the cores use); git diff sensing is **not** consulted for the changed set.
- `--since <rev>` → `Snapshot.senseSnapshot` with `SnapshotOptions.Since = Some <rev>`; candidate set =
  the resolved `RepoSnapshot.Changed |> List.map (fun c -> c.Path)`.
- neither → `Snapshot.senseSnapshot` with default `SnapshotOptions`; candidate set = sensed base/head
  `Changed` paths.

`--paths` and `--since` together is a **usage error** (distinct exit code), not a silent precedence.

**Rationale**: US2 AS1 requires the explicit list to be honored exactly and to "ignore the working
tree's other changes" — so `--paths` must bypass git diff entirely (it still needs the repo only insofar
as the catalog lives there; it does not need a resolvable range). US2 AS2/AS3 map directly onto
`Snapshot`'s `Since`/`Default` resolution, which already produces a normalized, sorted `Changed` set
byte-identical to what `Routing.route` consumes (the F016 `RoutingFeedTests` proof). Rejecting the
both-given case up front honors Principle VI's "distinguish bad input" over guessing.

**Alternatives considered**: (a) *`--paths` intersected with the git diff* — rejected: contradicts "ignore
the working tree's other changes." (b) *Precedence (one silently wins)* — rejected: ambiguous input
should fail loudly (Principle VI). (c) *Support `--base/--head` now* — deferred: `SnapshotOptions` carries
`Base`/`Head`, but the spec/plan row names only `--paths`/`--since`/default; explicit base/head is a
trivial later addition and out of this slice.

---

## D5 — Output locations

**Decision**: Defaults `gates.json` → **`<repo>/.fsgg/gates.json`**; `route.json` →
**`<repo>/readiness/route.json`**. Both overridable: `--gates-out <path>`, `--route-out <path>`. Parent
directories are created as needed by the writer.

**Rationale**: The design fixes the whole-catalog gate registry at `.fsgg/gates.json`
(`docs/initial-design.md:431`, `docs/initial-implementation-plan.md:191`) and the per-change view at
`readiness/<id>/route.json` (`:433`/`:193`). `<id>` derives from the SDD work-item model, which does not
exist in this Governance-only skeleton (spec "Output location" assumption), so the default drops the
`<id>` segment to `readiness/route.json` — deterministic, and an override lets a caller that *does* have
an id supply `readiness/<id>/route.json`. Keeping `gates.json` under `.fsgg/` co-locates the generated
whole-catalog artifact with the declared catalog it projects.

**Alternatives considered**: (a) *Both under `.fsgg/`* — rejected: conflates the per-change view with the
whole-catalog/declared area; the design separates them. (b) *Synthesize an `<id>` (hash/branch)* —
rejected: would inject an environment/branch-derived value, risking the determinism contract for no gain;
an explicit override is cleaner. (c) *stdout only, no files* — rejected: persistence to disk is the whole
point of the row.

---

## D6 — Exit-code taxonomy

**Decision**: `ExitDecision = Success | UsageError | InputUnavailable | ToolError`, mapped to numeric
codes **`0 / 2 / 3 / 4`** (mirroring `FS.GG.Governance.Cli.exitCode`, minus the excluded
`GovernedBlocking`). Mapping:
- **Success (0)** — repo sensed, catalog valid, gates selected, artifacts written (any gate count / any
  finding count).
- **UsageError (2)** — bad invocation: both `--paths` and `--since`, unknown flag, missing value.
- **InputUnavailable (3)** — not a git repository / git unavailable; `--since` revision does not resolve;
  required `.fsgg` files missing or failing validation.
- **ToolError (4)** — output location unwritable (a genuine tool/environment failure after a successful
  route), or any unexpected reified failure.

**Rationale**: FR-008 forbids a ship/merge verdict, so the `Cli`'s `GovernedBlocking` code is deliberately
absent — "your change selects gates" is *never* a non-zero exit (FR-009, FR-011). The remaining four
categories are exactly the failure classes FR-010/SC-004 enumerate, and Principle VI requires each to be
distinct and to separate bad-input (2/3) from tool-defect (4). Reusing the `Cli` numeric mapping keeps the
tool family's exit contract consistent.

**Alternatives considered**: (a) *Single non-zero code* — rejected: SC-004 demands category-distinct
codes. (b) *Treat missing catalog as `ToolError`* — rejected: a missing/invalid catalog is bad *input*
(InputUnavailable), not a tool defect (Principle VI). (c) *Invent new numbers* — rejected: align with the
existing `Cli` taxonomy.

---

## D7 — Summary rendering (stdout)

**Decision**: A deterministic summary, **separate from the persisted artifacts**, rendered by a pure
`render` in `Loop`: human-readable **text by default**, machine-readable **JSON on `--json`** (which
suppresses the text). It reports the selected gates (id + selecting path + tier cost), the cost rollup,
and the unknown-governed-path findings — and the two written artifact paths. It is **not** the F020/F021
documents echoed; those are the on-disk contract.

**Rationale**: FR-007 requires a deterministic human-or-JSON summary; US1 AS2 fixes its content (each
selected gate by id with selecting path and per-tier cost, plus findings). Keeping it a *separate*,
smaller projection avoids implying the stdout summary is the artifact contract and keeps it free to be
terse. Determinism comes from rendering already-ordered values (the `RouteResult`/`FindingReport` the
cores fixed) with no clock/path/env — the same discipline as the artifacts.

**Alternatives considered**: (a) *Echo the route.json string to stdout* — rejected: conflates the summary
with the artifact and is needlessly verbose for the human path. (b) *Text only* — rejected: US3/FR-007
require a machine-readable form for CI/agents. (c) *Render in the interpreter* — rejected: rendering is
pure and belongs in `Loop` (`render : Model -> OutputFormat -> string`), with the interpreter only
*emitting* the produced string through `OutputSink`.

---

## D8 — Flag surface / argv parsing

**Decision**: `fsgg route [--repo <dir>] [--paths <p> ...] [--since <rev>] [--json] [--gates-out <path>]
[--route-out <path>]`. `--repo` defaults to the current directory. Parsing is a small explicit matcher in
`Loop` (`parse : string list -> Result<RunRequest, UsageError>`), pure and total; `Program` only supplies
`argv` and maps the result to an exit. Unknown flags, a missing value, or `--paths` + `--since` together
yield a `UsageError` with a rendered diagnostic.

**Rationale**: The spec/plan row names `--paths`, `--since`, `--json`, and (assumption) an output
override; `--repo` makes the root explicit for tests and CI without `cd`. A pure parser keeps usage
errors testable as values (the `Cli.parse`/`ParseError` precedent) and keeps `Program` a one-liner edge.

**Alternatives considered**: (a) *An argv-parsing package (Argu/System.CommandLine)* — rejected: a new
dependency for a handful of flags breaches dependency-minimalism; an explicit matcher is plainer
(Principle III) and matches `Cli.parse`. (b) *Positional paths* — rejected: `--paths` is explicit in the
design and avoids ambiguity with a future subcommand grammar.

---

## D9 — No partial / no malformed artifact

**Decision**: The pure `update` computes **both** document strings (`GatesJson.ofGateRegistry` and
`RouteJson.ofRouteResult`) *before* any write effect is emitted; persistence is then two `WriteArtifact`
effects. A write `Error` from the `ArtifactWriter` port is reified to a `ToolError` diagnostic. The real
writer writes each file via **temp-file + atomic rename** so a failed write never leaves a truncated
target. For all *input/usage* failures (D6 categories 2/3), `update` reaches the persist step at all only
after a valid route, so **nothing is written** on those paths.

**Rationale**: FR-010 ("writing no partial or malformed artifact") and SC-004 ("no artifact written for
input/usage failures") require that failures before persistence write nothing, and that a write failure
itself not leave a half-file. Computing both strings first means serialization (pure, total — it never
fails) can't fail *between* writes; temp-then-rename makes each individual write atomic at the filesystem
level.

**Alternatives considered**: (a) *Stream-write directly to the target* — rejected: a crash mid-write
leaves a malformed file. (b) *Write route.json before computing gates.json* — rejected: ordering the two
pure projections before any I/O removes the only window where one could be written and the other fail.

---

## D10 — Test strategy & fakes

**Decision**: Three layers, all real-evidence-first (Principle V):
1. **Pure `update`/`parse`/`render` tests** — literal `Model`/`Msg`/`RunRequest`, assert next `Model` +
   emitted `Effect`s / parsed request / rendered string. No I/O.
2. **Interpreter tests with faked ports** — in-memory `FileReader` (literal catalog text), an in-memory
   git `Ports` backed by a literal `RawSensing`/fixed `RepoSnapshot`, and a capturing `ArtifactWriter`;
   assert the captured bytes equal `RouteJson.ofRouteResult`/`GatesJson.ofGateRegistry` of the same typed
   inputs, and that twice-run is byte-identical (SC-002).
3. **One real end-to-end** — a real temp git repo (the `Snapshot` `withTempRepo` helper) with a real
   `.fsgg` catalog on disk, run through `realPorts`, asserting both files land at their paths and match
   the projections (SC-007).

**Rationale**: Mirrors how `Snapshot` tests its edge (real temp git) and how the projection rows test
their bytes (`JsonDocument` parse). The faked-port layer proves the composition deterministically without
a git process (FR-012, SC-007); the single real run proves the wiring against actual git + filesystem.

**Alternatives considered**: (a) *All tests against real git* — rejected: slower, and the pure
composition is better asserted at the `update` seam. (b) *All tests faked* — rejected: SC-007 wants at
least one real-evidence end-to-end proof.

---

## Resolved Technical Context

No `NEEDS CLARIFICATION` remain. Confirmed: new project `FS.GG.Governance.RouteCommand` (D1); local MVU
boundary (D2); reused `Config`/`Snapshot` ports + new `ArtifactWriter`/`OutputSink` (D3); scope semantics
(D4); output locations `.fsgg/gates.json` and `readiness/route.json`, overridable (D5); exit taxonomy
`0/2/3/4` (D6); separate text/JSON summary (D7); explicit flag matcher (D8); compute-then-write with
atomic rename (D9); three-layer real-evidence-first tests (D10). No new third-party dependency.
