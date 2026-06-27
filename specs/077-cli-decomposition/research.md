# Research: CLI render / IO decomposition (Phase E)

All decisions resolve the spec's WHAT into a concrete HOW. There are no remaining
`NEEDS CLARIFICATION` markers — the spec is fully specified; the open choices were
module names, compile order, the re-export-vs-relocate decision for the public
`Cli.render*` symbols, and the exact split of shared helpers. Each is resolved below
against the established Phase A–D precedent and the F# compile-order reality.

## D1 — Three new modules, names, and what each owns

**Decision**: Add three `.fsi`-curated modules in the existing
`FS.GG.Governance.Cli` namespace (matching the design report's `CliRender.fs`,
`ArtifactReading.fs`, `ReviewStore` names):

- **`CliRender`** (pure) — owns `renderParseError`, `renderText`, `renderJson`,
  `render`, and every sub-writer they call: `renderExplanation`, `renderEvidenceNode`,
  `renderPayloadText`, `contractEntryJson`, `stakesJson`, `routeJson`,
  `explanationJson`, `contractJson`, `optionStateJson`, `optionFreshnessJson`,
  `evidenceNodeJson`, `evidenceJson`, `payloadJson`, `reviewBudgetJson`, `requestJson`,
  `exitJson`, `budgetJson`, `failuresJson`, `errorsJson`, plus the render-only
  formatting helpers `commandName`, `modeName`, `formatName`, `exitCategory`,
  `ruleIdText`, `evidenceStateText`, `freshnessText`, `quote`, `jsonArray`,
  `failureText`, `budgetLine` (today private inside `module Cli`, used only by
  rendering). Public `.fsi` exposes exactly the four entry points
  (`renderParseError`/`renderText`/`renderJson`/`render`); the sub-writers stay hidden.

- **`ArtifactReading`** (impure edge) — owns the spec-kit/design path resolution,
  file/directory reads, regex task/dep parsing, and fact extraction:
  `tryReadAllText`, `readJson`, `stringProperty`, `activeFeatureDirectory`,
  `specKitArtifactPath`, `designFileName`, `designBase`, `designArtifactPath`,
  `readArtifact`, `phaseFromText`, `phaseFor`, `specKitArtifactOfKey`,
  `artifactPresent`, `taskStateFromMarker`, `taskStatesFrom`, `taskDependenciesFrom`,
  `constitutionFilled`, `specKitFacts`, `stateOfName`, `designFactsFromFile`,
  `designFacts`, plus the snapshot assembly `fact`, `optionsFor`, `artifactsFor`,
  `loadSnapshot`. Public `.fsi` exposes what the edge calls: `readArtifact` (used by
  `runHost`'s `ReadArtifact` effect), `optionsFor` (used by `runHost`), and
  `loadSnapshot` (the `LoadSnapshot` port). The rest stay hidden.

- **`ReviewStore`** (impure edge) — owns `safeFileName`, `reviewStoreRoot`,
  `verdictText`, `parseVerdict`, `loadReview`, `saveReview`, including the
  `review-store-unavailable` fixture short-circuit inside `loadReview`/`saveReview`.
  Public `.fsi` exposes `loadReview` and `saveReview` (called by `runHost`); the
  helpers stay hidden.

**Rationale**: This is exactly the Finding 4 / Phase E seam — render is a pure
function of `CommandResult`; artifact-reading and review-store are the two distinct
edge I/O concerns. Curating the `.fsi` to only the cross-module entry points honours
Constitution Principle II and FR-006, and matches the curated-leaf shape of 073/075.

**Alternatives considered**: (a) One combined `CliIo` module for both edge concerns —
rejected: it re-merges two concerns the spec separates (FR-003 vs FR-004) and would
not let US2's two halves land as independent commits. (b) A new project for the edge
code — rejected by FR-008 and the design's "do not add projects" stance for ~450 LOC.

## D2 — Compile order

**Decision**: `Project → Cli → CliRender → ArtifactReading → ReviewStore → Program`.

**Rationale**: F# compiles files top-to-bottom; a file may only reference symbols
declared earlier.

- The namespace-level types (`CommandKind` … `CliPorts`) and `module Cli` stay in
  `Cli.fs`. `CliRender` needs those types **and** the shared pure vocabulary
  (`exitCode`, `commandName`, `quote`, `jsonArray`, `stableStrings`, …). Placing
  `CliRender` **after** `Cli` lets it reuse every still-needed `Cli.*` helper without
  duplicating anything — `exitCode` and `stableStrings` in particular are needed by
  both the parser/MVU (which stays in `Cli`) and rendering (which moves to
  `CliRender`), so they stay public in `module Cli` and `CliRender` calls them.
- `ArtifactReading` / `ReviewStore` reference the namespace types (and `RunRequest`,
  `ProjectSnapshot`, `RecordedReview`) declared in `Cli.fs`; they compile after it.
- `Program` references all three new modules plus `Cli`, so it compiles last
  (unchanged position).

**Alternatives considered**: Hoisting the namespace type block into its own
`CliModel.fs` so `CliRender` could compile *before* `module Cli` (enabling
`module Cli` to re-export `renderText` etc.). Rejected — see D3; it adds a type-file
move that is not one of the three concerns and increases churn for no surface benefit.

## D3 — Re-export vs relocate for the public `Cli.render*` symbols

**Decision**: **Relocate** `renderParseError`/`renderText`/`renderJson`/`render`
into `CliRender` (removing them from `Cli.fsi`) and **update the call sites in the
same commit**. This is sanctioned by the spec's assumption ("preserved via a thin
re-export *or* the call site is updated in the same commit").

**Why relocation is clean here**: The CLI surface baseline
(`surface/FS.GG.Governance.Cli.surface.txt`) and the in-test `generatedSurface`
literal are **name-level only** — they list `module Cli`, `type RunRequest`, etc.,
**not** the individual `val`s inside a module. Moving `renderText` from `module Cli`
to `module CliRender` therefore changes **no existing baseline line**; it only **adds**
`module CliRender`. So the surface change is genuinely additive at the granularity the
surface-drift test checks (FR-011, SC-005), even though vals are relocated. (Unlike
`CommandHost` (075), the CLI has no reflective member-level surface test, so there is
no finer granularity to break.)

**Call sites to update (small, enumerated)**:
- `Program.fs:535` (`writeOutput`) `Cli.render result` → `CliRender.render result`.
- `Program.fs:671` (`main` stderr path) `Cli.render result` → `CliRender.render result`.
- `tests/.../Cli.Tests/OutputTests.fs:37-47` `Cli.renderJson` / `Cli.renderText` →
  `CliRender.renderJson` / `CliRender.renderText`.

These are the **only** external references (verified by grep). The watch/tui edge uses
`HumanText.render`, not `Cli.render`, so it is untouched.

**Alternatives considered**: Keeping `Cli.render*` as one-line re-exports
(`let render = CliRender.render`). Rejected because it requires `CliRender` to compile
*before* `module Cli` (D2), which forces the type-file hoist — more churn than updating
two `Program.fs` lines and one test file, with no additive-surface advantage given D3's
name-level baseline. The re-export pattern remains the house style where ordering
allows it (e.g. CommandHost's `kindOf`); here ordering does not, and relocation is
strictly simpler.

## D4 — What STAYS in `Program.fs` (thin orchestration, FR-005)

**Decision**: `Program.fs` keeps, as port orchestration / effect interpretation:
`fullPath`, the budget folds (`emptyBudget`, `addUnique`, `markRequested`/`markHit`/
`markMiss`/`markFresh`/`markPending`/`markExhausted`, `reviewBudgetLimit`), `runHost`
(the Host-effect interpreter — now calling `ArtifactReading.readArtifact`/`optionsFor`
and `ReviewStore.loadReview`/`saveReview`), `writeOutput` (the output-writing edge,
calling `CliRender.render`), the entire read-only watch/tui block
(`readOnlyRoutePorts`, `routeRequestFor`, `composeRouteView`, `runWatch`, `drawTui`,
`tuiKeyReader`, `runTui` — **out of scope for relocation**, spec edge case), and
`main`.

**Rationale**: FR-005 reduces `runHost`/`main` to "build ports, drive the MVU,
interpret effects" with **no inline file-I/O bodies**. The budget folds are pure
accumulator helpers (not file I/O) and are intrinsic to interpreting the
`LoadReview`/`DispatchReview`/`RecordVerdict` effects, so they stay with `runHost`.
The `review-dispatch-failed` fixture string lives in `runHost`'s `DispatchReview`
budget branch (host policy, not store I/O) and stays there; the
`review-store-unavailable` short-circuit moves **into** `ReviewStore` with
`loadReview`/`saveReview` (store I/O). After the split, `runHost` contains no
`File.*`/`Directory.*` bodies — only effect routing + budget folds + delegated calls.
`writeOutput` keeps its file write (it is the output-writing edge, neither artifact
reading nor review-store I/O — FR-003/FR-004 do not name it) and is unchanged except
`Cli.render` → `CliRender.render`.

**Alternatives considered**: Extracting `writeOutput` into a fourth "output" module —
rejected: not required by any FR, and it would couple the pure `CliRender` with edge
`Console`/`File` writes, defeating FR-008's "rendering stays a pure function of
`CommandResult`."

## D5 — Shared helper placement

**Decision**:
- `exitCode` — **stays public in `module Cli`** (it is in `Cli.fsi` today and called
  by `Program` as `Cli.exitCode` in the watch/tui edge and `main`; `CliRender` calls
  `Cli.exitCode` for `exitLine`/`exitJson`). No change to `Cli.fsi` for `exitCode`.
- `stableStrings` — stays in `module Cli` (used by `parseOptions` for `Scope`
  normalization **and** by `CliRender.jsonArray`). `CliRender` calls
  `Cli.stableStrings` (or, since `jsonArray` moves to `CliRender`, `jsonArray` keeps
  calling the `Cli.stableStrings` it can now see by compile order).
- `optionsFor` — **moves to `ArtifactReading`** (pure, builds `ProjectOptions`; needed
  by `loadSnapshot` there and by `runHost` which calls `ArtifactReading.optionsFor`).
  Note `Cli.fs` already has an equivalent `commandCatalog`/inline options builder used
  by `payloadFor`/`explanationsFor`; those stay in `Cli` unchanged (they build the
  catalog for payload projection, a parser/MVU concern).
- `fullPath` — stays in `Program` (one-liner `Path.GetFullPath`); `ArtifactReading`
  and `ReviewStore` use `Path.GetFullPath` directly (or a local one-liner) rather than
  taking a dependency on `Program` (which is the Exe entry and compiles last).

**Rationale**: Keep each shared helper in the lowest module that both consumers can
see, and never duplicate a non-trivial body. The only genuinely cross-cutting pure
helpers (`exitCode`, `stableStrings`) stay in `Cli`, which everything downstream can
reference.

## D6 — Surface baseline + test updates (additive)

**Decision**: In the **commit that adds each module**, append its `module` line to
both `surface/FS.GG.Governance.Cli.surface.txt` and the `generatedSurface` literal in
`tests/.../Cli.Tests/SurfaceDriftTests.fs`, in compile order:
`module CliRender`, `module ArtifactReading`, `module ReviewStore` (after `module Cli`).
Add an additive scope-guard assertion in `SurfaceDriftTests.fs` confirming the new
modules carry no unexpected runtime reference and that the "CLI remains optional"
guard still holds (the existing lower-assembly check already covers this; the new
modules add no new ProjectReference, so the existing
"CLI assembly has only expected runtime references" test stays green unchanged).

**Rationale**: FR-011 (additive baseline, surface-drift green) + FR-009 (per-commit).
The baseline grows by exactly three lines; nothing is removed or renamed.

## D7 — LOC accounting and the SC-005 figure (recorded deviation)

**Decision**: Report the actual relocation: render ≈ 200 LOC (Cli.fs ~390–700 minus
the shared `exitCode`/`stableStrings`), artifact-reading ≈ 190 LOC (Program.fs
~21–376 minus budget folds), review-store ≈ 65 LOC (Program.fs ~408–471) — ≈ **450 LOC
relocated** across three module pairs, not ~200.

**Rationale / flag**: SC-005 states "Roughly 200 LOC are relocated," which tracks the
design report's headline ("~200 LOC moved (clarity-dominated)") and the dominant
render extraction, but undercounts the two edge extractions. The **binding** criterion
is byte-identity (SC-001) + green suite (SC-002) + the structural separations
(SC-003/SC-004), all of which hold regardless of the exact count. This mirrors the
recorded SC-001 LOC deviation in Phase C (076): honour the contracted scope and report
the real number rather than gaming it. No spec change required; flagged here and in
the delivery summary.

## D8 — Commit sequencing (FR-009)

**Decision**: One concern per commit, full suite green and all goldens/snapshots
byte-identical at each:

1. **Commit 1 (US1)** — extract `CliRender`; update the 2 `Program.fs` + 1
   `OutputTests.fs` call sites; add `module CliRender` to the baseline + test literal.
2. **Commit 2 (US2a)** — extract `ArtifactReading`; repoint `loadSnapshot` port and
   `runHost`'s `ReadArtifact`/`optionsFor` calls; add `module ArtifactReading`.
3. **Commit 3 (US2b)** — extract `ReviewStore`; repoint `runHost`'s
   `loadReview`/`saveReview` calls; confirm `runHost`/`main` hold no inline file I/O;
   add `module ReviewStore`.

**Rationale**: Isolates any byte-drift to its causing commit (the Phase A–D
per-seam discipline). US1 is independently shippable (lowest risk, pure function);
US2's two halves are sequenced after it (higher-risk edge I/O).

## D9 — Risks and mitigations

- **JSON envelope drift** (highest risk): the dozen `*Json` writers build the
  `fsgg-governance.cli.v1` envelope by hand. Mitigation: move them **verbatim** (no
  reflow, no reorder); the existing JSON goldens/transcripts fail on any byte change.
- **Watch/tui regression**: those surfaces are out of scope; mitigation: leave the
  whole block in `Program.fs` and confirm by an empty `git diff` on that region across
  commits 2–3 (they only gain delegated calls if any — expected none).
- **Hidden helper capture**: a sub-writer might use a `Cli` helper not anticipated
  here. Mitigation: compile order D2 keeps every `Cli.*` helper visible to `CliRender`,
  so any missed dependency resolves by qualifying it, never by duplication.
