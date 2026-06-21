# Phase 0 Research: `fsgg ship` Host Command

This feature ships **no new algorithm** — every routing/selection/enforcement/rollup/serialization
decision is already fixed by F014–F025. Research therefore resolves the *composition and host-edge*
questions the spec deferred to plan time, plus the one genuinely new lever this row introduces (the
verdict and its blocking exit code). Each decision is Decision / Rationale / Alternatives considered. The
baseline is the merged **F022 `fsgg route`** command (`specs/022-fsgg-route-command/research.md`); where a
decision is unchanged from F022 it is marked *(F022 reuse)* and only the delta is discussed. No `NEEDS
CLARIFICATION` remain after this phase.

---

## D1 — Project home

**Decision**: A new project **`FS.GG.Governance.ShipCommand`** (OutputType `Exe`) mirroring
`FS.GG.Governance.RouteCommand`, referencing the six reused F022 cores (`Config`, `Snapshot`, `Routing`,
`Findings`, `Gates`, `Route`) plus the three this row newly composes (`Enforcement` F023, `Ship` F024,
`AuditJson` F025). It is the composition/edge tier, parallel to `RouteCommand`. For this slice it is
`IsPackable=false`: the single-packed-`fsgg`-tool unification (one packed tool dispatching the `route` and
`ship` verbs) is a deferred follow-up, so this row does **not** ship a second NuGet tool that also claims
the `fsgg` `ToolCommandName` F022 already owns.

**Rationale**: The one-row-one-project rhythm (F014–F025) and the spec's "a new
`FS.GG.Governance.ShipCommand` mirroring `FS.GG.Governance.RouteCommand`" assumption both point to a fresh
project; the constitution's "heavier capabilities layer on top in separate projects, not into the core"
agrees. Keeping `ship` separate from `route` also keeps two **different exit contracts** isolated: `route`
never blocks (no `GovernedBlocking` code by deliberate FR design in F022), whereas `ship` can fail the
process. Folding the verdict cores (`Enforcement`/`Ship`/`AuditJson`) into the verdict-free `RouteCommand`
would entangle them. The `fsgg` design surface is a single tool with `route`/`ship` verbs, but a real
single packed tool needs subcommand dispatch over both hosts — that unification is its own small row;
until then only one project may own the `fsgg` `ToolCommandName`, and F022 already does, so `ShipCommand`
is non-packable and built/tested as an Exe referenced by its test project (exactly as `RouteCommand.Tests`
references its Exe).

**Alternatives considered**: (a) *Extend `RouteCommand` with a `ship` subcommand* — rejected for this
slice: mixes two exit taxonomies and two pipelines, and pulls the verdict cores into a verdict-free
command; the design's single-`fsgg`-tool surface is better delivered later as an explicit dispatcher row
over two clean host projects. (b) *Make `ShipCommand` packable with `ToolCommandName fsgg` now* —
rejected: two NuGet tools both claiming `fsgg` collide on install. (c) *A different tool name (`fsgg-ship`)*
— rejected: contradicts the design's one-`fsgg`-tool intent; deferring packaging is cleaner than minting a
throwaway name. (d) *Reuse the kernel-era `FS.GG.Governance.Cli`* — rejected (the F022 D1 reasoning):
distinct kernel lineage the Phase-2 line references nowhere.

---

## D2 — Boundary shape (MVU vs pure function) *(F022 reuse)*

**Decision**: The same **local MVU/effect algebra** as F022 — pure `Loop`
(`parse`/`init`/`update`/`render`/`exitCode` over `Model`/`Msg`/`Effect`) + an edge `Interpreter`
executing effects through injected ports + a thin `Program`. Not a pure function, not the Elmish
`Program` runtime.

**Rationale**: Identical to F022 D2 — Principle IV *mandates* an MVU boundary once behavior includes
multi-step state or I/O, and this command senses git, reads a catalog, and writes a file in sequence with
early-exit failure paths. The *new* twist (a blocking exit) is itself a pure decision: the
`ExitCodeBasis → ExitDecision` mapping lives in `update`/`exitCode`, keeping the verdict consequence
exhaustively testable at the `update` seam without a process.

**Alternatives considered**: As F022 D2 — pure function (violates Principle IV); Elmish runtime
(no long-running loop/subscriptions; breaches dependency-minimalism).

---

## D3 — Port composition (reuse vs new) *(F022 reuse)*

**Decision**: The interpreter's injected `Ports` bundle is the **identical F022 record** —
`Files: Config.Loader.FileReader` (catalog reads), `Git: Snapshot.Ports` (git sensing),
`Write: ArtifactWriter` (persist), `Out: OutputSink` (summary). `realPorts repo` builds the real four,
the same way. The only difference at runtime is **how many writes are emitted**: `ship` writes one
artifact (`audit.json`), not two.

**Rationale**: The spec's "Reuse, don't re-derive" assumption is explicit — compose the existing sensing
and persistence edges verbatim. `ship` introduces no new impure primitive over `route`: the rollup and
projection are pure cores, so the only I/O is still catalog-read + git-sense + one file-write + one
stdout-emit. Reusing the exact `Ports` shape means the same in-memory fakes (in-memory `FileReader`,
in-memory git `Ports`, capturing `ArtifactWriter`/`OutputSink`) drive the whole composition with no real
`git` (FR-013, SC-007).

**Alternatives considered**: (a) *Add a dedicated `AuditWriter` port* — rejected: the generic
`ArtifactWriter = path -> content -> Result<unit,string>` already fits; a second write port adds nothing.
(b) *Also write `gates.json`/`route.json` like `route`* — rejected: FR-005 requires only `audit.json`;
`route` already owns those two documents, and emitting them here would duplicate the route command's
contract and reference `RouteJson`/`GatesJson` for no spec requirement.

---

## D4 — Scope selection semantics *(F022 reuse, with a decision to include it)*

**Decision**: Mirror F022's scope surface exactly — three mutually-exclusive selectors:
- `--paths p1 p2 …` → the candidate set **is** that explicit normalized list; git diff is not consulted.
- `--since <rev>` → `Snapshot.senseSnapshot` with `Since = Some <rev>`; candidates = resolved `Changed`.
- neither → default base/head `Snapshot.senseSnapshot`; candidates = sensed `Changed`.

`--paths` and `--since` together is a **usage error** (exit 2), not silent precedence.

**Rationale**: The spec's Key Entities note scope sensing "reuses the route command's base/head range; an
explicit-paths/since selector is a plan-time reconciliation, not assumed here." We **decide to include**
the full F022 scope surface because (a) it is free — the `parse`/scope code is identical to F022 and
already proven; (b) `--paths` lets a test drive a *base-blocking* change deterministically (US1/US2
independent tests select "a gate that is blocking under the chosen mode/profile") without constructing a
real diff; and (c) parity with `route` keeps the `fsgg` verb family consistent. Same `Snapshot` resolution
F016/F022 proved byte-identical to what `Routing.route` consumes.

**Alternatives considered**: (a) *Default base/head only (no flags)* — rejected: would force every verdict
test through a real diff and break parity with `route`. (b) *Add `--base/--head`* — deferred exactly as
F022 D4 deferred it (the row names only `--paths`/`--since`/default).

---

## D5 — Run-mode / profile levers (the enforcement dials)

**Decision**: Two new flags `--mode <m>` and `--profile <p>`. They are parsed in the **pure `parse`**:
the argv matcher captures the raw strings, then `parse` calls `Enforcement.recognizeMode` /
`Enforcement.recognizeProfile`; a `Recognized v` becomes the typed `RunMode`/`Profile` on the
`RunRequest`, an `Unrecognized s` becomes a `UsageError` (`UnrecognizedMode s` / `UnrecognizedProfile s`)
→ exit 2, **no artifact**. When a flag is omitted the documented default is applied:
**`--mode gate --profile standard`** (the design's canonical protected-branch invocation,
`docs/initial-design.md:76`). The applied `RunMode`/`Profile` are carried on the `RunRequest` and threaded
into `Ship.rollup`; because every `EnforcedItem`'s F023 `EnforcementDecision` carries `Mode` and
`Profile`, the **chosen levers are recorded in the audit document** verbatim (FR-002, US2 AS1/AS4).

**Rationale**: F023 already provides the *total, never-throwing* string recognizers (`recognizeMode`/
`recognizeProfile` return `Recognized<_>`), so lever validation is a pure value, not an exception, and
fits the F022 "usage errors are values" discipline. Doing recognition in `parse` means an unrecognized
lever fails *before any port is built* (like a bad flag in F022's `Program`), so "no artifact for a usage
failure" (FR-010/SC-004) is trivially true. Defaulting to `gate`/`standard` satisfies US2 AS4 ("neither
flag ⇒ documented default levers") and matches the design's named canonical invocation; recording the
levers per item (already done by F024/F025) satisfies "the audit document records which levers produced
the verdict."

**Alternatives considered**: (a) *Recognize levers inside `update` (as a `Msg`)* — rejected: would
require building ports / starting work before rejecting a typo, risking partial side effects and
complicating the "no artifact on usage error" guarantee. (b) *Require explicit `--mode`/`--profile` (no
default)* — rejected: US2 AS4 documents defaults; a missing flag should not be a usage error. (c)
*Re-implement mode/profile parsing locally* — rejected: FR-002 forbids re-implementing lever logic;
`Enforcement.recognize*` is the single source of truth.

---

## D6 — Exit-code taxonomy (the new lever)

**Decision**: `ExitDecision = Success | Blocked | UsageError' | InputUnavailable | ToolError`, mapped to
numeric codes **`0 / 1 / 2 / 3 / 4`**:
- **Success (0)** — repo sensed, catalog valid, rollup `ExitCodeBasis = Clean` (`Verdict = Pass`), audit
  written.
- **Blocked (1)** — rollup `ExitCodeBasis = Blocked` (`Verdict = Fail`), audit written. The **single**
  code reserved for a blocked merge verdict; used for no other outcome.
- **UsageError' (2)** — bad invocation: both `--paths` and `--since`, unknown flag, missing value,
  **unrecognized `--mode`/`--profile`**.
- **InputUnavailable (3)** — not a git repository / git unavailable; `--since` revision does not resolve;
  required `.fsgg` files missing or failing validation.
- **ToolError (4)** — output location unwritable (a genuine tool/environment failure *after* a successful
  rollup), or any unexpected reified failure.

The terminal `update` transition maps the decision's `ExitCodeBasis`: `Clean → Success`, `Blocked →
Blocked`. `exitCode` then maps to the integer.

**Rationale**: This is the one place `ship` deviates from `route`. F022 deliberately *omitted* a
blocking code (`route` always exits 0); `ship`'s whole point is to add it (FR-008). The constraint
(FR-009/SC-004) is that the blocked code be **distinct from every tool-failure code**: `Blocked = 1` sits
between `Success = 0` and the F022 tool-failure block `2/3/4`, which `ship` keeps unchanged for parity.
`1` is the conventional "the thing you checked failed" code and reads naturally to CI as "blocked merge,"
while `2/3/4` remain "the tool itself could not run" — exactly the bad-input-vs-tool-defect-vs-blocked
separation Principle VI and FR-009 demand. A tool failure is therefore never `0` and never `1`, so it can
never be read as a pass or as a blocked merge.

**Alternatives considered**: (a) *Reuse the kernel `Cli`'s `GovernedBlocking = 2`* — rejected: `2` is
F022's `UsageError`; reusing it would collide a blocked verdict with a usage error, violating SC-004. (b)
*A high blocked code (e.g. 10)* — rejected: `1` is the idiomatic generic-failure code CI defaults to
treating as a failed check; no reason to be exotic. (c) *Fold Blocked into ToolError* — rejected: a
blocked merge is **not** a tool failure (FR-009); conflating them is the precise governance error this row
exists to prevent.

---

## D7 — Output location

**Decision**: Default `audit.json` → **`<repo>/readiness/audit.json`**; overridable via
`--audit-out <path>`. Parent directories are created as needed by the writer (the F022 `under` join +
temp-then-rename writer). Only one artifact is written.

**Rationale**: The design fixes the per-change audit view at `readiness/<id>/audit.json`
(`docs/initial-design.md:437`). `<id>` derives from the SDD work-item model that does not exist in this
Governance-only skeleton (spec "Output location" assumption), so the default drops the `<id>` segment to
`readiness/audit.json` — deterministic, and an override lets a caller that *does* have an id supply
`readiness/<id>/audit.json`. This is the exact `route.json` sibling location F022 established
(`readiness/route.json`), keeping the two host edges' outputs co-located under `readiness/`.

**Alternatives considered**: (a) *Under `.fsgg/`* — rejected: `.fsgg/` holds the declared catalog and the
whole-catalog `gates.json`; the per-change verdict belongs under `readiness/` with `route.json`. (b)
*Synthesize an `<id>`* — rejected (F022 D5 reasoning): would inject a branch/environment-derived value and
risk the determinism contract. (c) *stdout only* — rejected: persisting the document a protected branch
reads is the point of the row (FR-005).

---

## D8 — Summary rendering (stdout)

**Decision**: A deterministic summary rendered by a pure `render` in `Loop`: human-readable **text by
default**; on **`--json`**, the **F025 `AuditJson.ofShipDecision` document text verbatim** (the same bytes
written to `readiness/audit.json`). The text form states the verdict and the exit-code basis, then lists
the blockers, warnings, and passing items — each with its identity and its base/effective severity — and
reports the unknown-governed-path findings carried into the decision, plus the written path.

**Rationale**: US3 AS2 permits the JSON summary to be "the audit document and/or a verdict envelope."
Emitting the F025 document verbatim is the simplest deterministic choice and means the `--json` stdout
**equals the persisted file byte-for-byte** (SC-002), inheriting F025's byte-stability with zero new
serialization. The text form satisfies FR-007/US1 AS3 (verdict + basis + the three-way partition with
each item's identity and effective/base severity + findings) and, like F022's summary, is a *separate,
smaller* projection from the on-disk contract for the human path. Determinism comes from rendering the
already-ordered `ShipDecision`/`FindingReport` with no clock/path/env.

**Alternatives considered**: (a) *A bespoke JSON verdict envelope distinct from `audit.json`* — rejected:
a second JSON shape to keep deterministic and in sync, for no requirement; the document already *is* the
machine contract. (b) *Echo nothing on `--json` (file only)* — rejected: US3 AS2 wants a machine summary
on stdout. (c) *Render in the interpreter* — rejected (F022 D7): rendering is pure and belongs in `Loop`;
the interpreter only emits the produced string through `OutputSink`.

---

## D9 — Flag surface / argv parsing

**Decision**: `fsgg ship [--repo <dir>] [--mode <m>] [--profile <p>] [--paths <p> …] [--since <rev>]
[--json] [--audit-out <path>]`. A leading `ship` verb is tolerated and dropped (as F022 tolerates
`route`). `--repo` defaults to the current directory. Parsing is the small explicit matcher in `Loop`
(`parse : string list -> Result<RunRequest, UsageError>`), pure and total, extended from F022's with
`--mode`/`--profile`/`--audit-out` and the lever recognition of D5.

**Rationale**: The row's canonical invocation is `fsgg ship --mode gate --profile standard --json`; the
flag set is F022's scope/output/format flags plus the two enforcement dials and the renamed single output
override (`--audit-out` replacing `--gates-out`/`--route-out`). A pure matcher keeps every usage error
(unknown flag, missing value, both scope flags, unrecognized lever) a testable value and keeps `Program` a
one-liner edge (the F022 precedent). No argv-parsing package (dependency-minimalism).

**Alternatives considered**: (a) *An argv package (Argu/System.CommandLine)* — rejected (F022 D8): a new
dependency for a handful of flags. (b) *Keep `--gates-out`/`--route-out`* — rejected: `ship` writes
neither document; one `--audit-out` is the honest surface.

---

## D10 — No partial / no malformed artifact *(F022 reuse, one write)*

**Decision**: The pure `update`, on a valid catalog, computes the whole chain **before any write effect**:
`Routing.route` → `Gates.buildRegistry` → `Findings.findUnknownGovernedPaths` → `Route.select` →
`Ship.rollup` → `AuditJson.ofShipDecision`, then emits a single `WriteArtifact(AuditArtifact, auditOut,
auditDoc)`. A write `Error` from the `ArtifactWriter` is reified to a `ToolError` diagnostic (exit 4),
**never** a `Blocked` (exit 1). The real writer uses temp-file + atomic rename so a failed write never
leaves a truncated `audit.json`. All input/usage failures (D6 categories 2/3) short-circuit *before* the
write, so nothing is written on those paths. Crucially, the **verdict is decided before the write**, so a
write failure after a `Fail` rollup still surfaces as a tool error (4), not a blocked verdict (1) and not
a success — the change's mergeability and the tool's success are independent (FR-009).

**Rationale**: FR-010 ("no partial or malformed `audit.json`") and SC-004 ("no artifact for input/usage
failures") require failures before persistence to write nothing and a write failure not to leave a
half-file. The rollup and projection are pure and total — they never fail — so the only failure window is
the write itself, made atomic by temp-then-rename. Deciding the verdict before writing keeps the
exit-category honest: write success/failure does not change the verdict, and the verdict does not change
the write.

**Alternatives considered**: (a) *Stream-write to the target* — rejected (F022 D9): a crash mid-write
leaves a malformed file. (b) *Map a post-`Fail` write failure to `Blocked`* — rejected: conflates a tool
failure with a blocked merge (FR-009).

---

## D11 — Test strategy & fakes *(F022 reuse, plus verdict assertions)*

**Decision**: Four layers, all real-evidence-first (Principle V):
1. **Pure `update`/`parse`/`render` tests** — literal `Model`/`Msg`/`RunRequest`; assert next `Model` +
   emitted `Effect`s, the `--mode`/`--profile` recognition + defaults, and the terminal
   `ExitCodeBasis → ExitDecision` mapping (`Clean → Success`, `Blocked → Blocked`). No I/O.
2. **Interpreter tests with faked ports** — in-memory `FileReader`, in-memory git `Ports` over a fixed
   `RepoSnapshot`, capturing `ArtifactWriter`/`OutputSink`; assert the captured bytes equal
   `AuditJson.ofShipDecision (Ship.rollup result mode profile)` of the same typed inputs (SC-001), that a
   base-blocking change yields `verdict:fail`/`exitCodeBasis:blocked`/exit 1 and a passing-only change
   yields `verdict:pass`/`clean`/exit 0 (SC-001), that **the same change under two lever sets** yields the
   two expected verdicts/partitions/exit codes (SC-003), and that twice-run is byte-identical (SC-002).
3. **Failure tests** — non-git dir (3), missing/invalid catalog (3), unrecognized `--mode`/`--profile`
   (2), unwritable output (4); each a distinct diagnostic, each **≠ the blocked code 1**, no artifact for
   usage/input failures (SC-004).
4. **One real end-to-end** — a real temp git repo (`Snapshot` `withTempRepo`) with a real `.fsgg` catalog,
   run through `realPorts`, asserting the verdict, the persisted bytes match the projection, and the exit
   code (SC-007).

**Rationale**: Mirrors F022 D10 (three layers) and adds the verdict/exit-code assertions that are this
row's new behavior. The faked-port layer proves the composition *and the exit-code consequence*
deterministically without a git process; the single real run proves the wiring against actual git +
filesystem.

**Alternatives considered**: As F022 D10 — all-real (slower; the verdict→exit mapping is better asserted
at the `update` seam) or all-faked (SC-007 wants one real end-to-end proof).

---

## Resolved Technical Context

No `NEEDS CLARIFICATION` remain. Confirmed: new project `FS.GG.Governance.ShipCommand`, `IsPackable=false`,
tool unification deferred (D1); local MVU boundary (D2); the identical F022 `Ports`, one write (D3);
F022 scope surface, included by decision (D4); `--mode`/`--profile` recognized in `parse` via
`Enforcement.recognize*`, default `gate`/`standard`, levers recorded per item (D5); exit taxonomy
`0/1/2/3/4` with `Blocked = 1` distinct from `2/3/4` (D6); `audit.json` → `readiness/audit.json`,
overridable (D7); summary text default / F025 document verbatim on `--json` (D8); explicit flag matcher
with `ship` verb tolerated (D9); compute-then-single-write with atomic rename, verdict decided before
write (D10); four-layer real-evidence-first tests with verdict/exit assertions (D11). No new third-party
dependency.
