# Feature Specification: CLI render / IO decomposition

**Feature Branch**: `077-cli-decomposition`

**Created**: 2026-06-27

**Status**: Draft

**Input**: User description: "next item in docs/reports/2026-06-26-203146-architecture-quality-deduplication-design.md" — the next undelivered roadmap item is **Phase E — CLI decomposition** (Phases A/B/C/D are ✅ DELIVERED as features 073/075/076/074).

**Change Classification**: **Tier 1** (contracted change). New public modules with curated `.fsi` signatures and an additively-grown surface-area baseline are introduced. No observable CLI behavior, exit code, option, or JSON envelope changes — every existing golden and transcript stays byte-identical.

## Context (why this feature exists)

Finding 4 of the architecture/quality/de-duplication analysis records that the
optional command-line tool mixes three concerns in two large modules:

- **`Cli/Cli.fs` (829 LOC)** dispatches cleanly (one recursive `parseOptions`,
  no per-subcommand boilerplate) but conflates **parsing/normalization** with
  **all text and JSON rendering** (`renderText`, `renderJson`, `render`,
  `renderParseError`, and the dozen `*Json`/`*Text` sub-writers occupy roughly
  the second half of the file, lines ~501–700).
- **`Cli/Program.fs` (673 LOC)** is the executable edge but scatters
  **artifact reading / path resolution / fact extraction** (`readArtifact`,
  `activeFeatureDirectory`, `specKitArtifactPath`, `designArtifactPath`,
  `specKitFacts`, `designFactsFromFile`, `designFacts`, the task/dep regex
  parsers) and **review-store persistence** (`loadReview`, `saveReview`,
  `reviewStoreRoot`, `safeFileName`, `verdictText`, `parseVerdict`) inline,
  and its `runHost` couples budget tracking + effect dispatch + message routing
  + review-store I/O in one 58-line function.

The roadmap fix (Phase E) is a **structural decomposition**, not a behavior
change: split rendering out of the parser module, and split artifact-reading and
review-store I/O out of the executable edge so `runHost`/`main` become thin port
orchestration. The acceptance bar is the same byte-identity discipline that gated
Phases A–D: every CLI transcript, golden, and snapshot is untouched.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Rendering is a separate concern from parsing (Priority: P1)

A maintainer changing how a command result is rendered (text layout or the
`fsgg-governance.cli.v1` JSON envelope) edits a single focused rendering unit,
not the 829-LOC module that also owns argument parsing, command normalization,
the MVU `Model`/`Msg`/`Effect`/`init`/`update`, and the port-driven `run` loop.

**Why this priority**: This is the largest single concern in the largest CLI
module and the lowest-risk extraction (rendering is a pure function of
`CommandResult`). Delivering it alone already halves the cognitive load of
`Cli.fs` and is independently shippable.

**Independent Test**: Capture the CLI text and JSON output for a representative
set of invocations (each command × text/json × success/usage-error/blocking)
before the change; after the extraction the captured bytes are identical and the
full `*.Cli.Tests` suite is green.

**Acceptance Scenarios**:

1. **Given** any command invocation rendered as text, **When** the result is
   rendered after the extraction, **Then** the emitted bytes are identical to the
   pre-extraction output.
2. **Given** any command invocation rendered as JSON, **When** the result is
   rendered after the extraction, **Then** the `fsgg-governance.cli.v1` envelope
   (request, exit, budget, failures, errors, payload) is byte-identical.
3. **Given** a parse/usage error with no request, **When** the result is
   rendered, **Then** it still renders as text with the same error lines and exit
   category as before.
4. **Given** the rendering concern now lives in its own compilation unit with a
   curated `.fsi`, **When** the surface-drift test runs, **Then** the CLI
   surface baseline reflects the new module additively (no symbol removed, no
   rename of an existing exposed symbol that callers depend on).

---

### User Story 2 - Artifact reading and review-store I/O are separate from orchestration (Priority: P2)

A maintainer changing how Spec Kit / design-system artifacts are located and
read, or how recorded reviews are persisted, edits a dedicated module rather than
hunting through the executable edge where snapshot loading, host driving, and
output writing also live. After the split, `runHost` and `main` read as thin
orchestration: build ports, drive the MVU, interpret effects.

**Why this priority**: Higher risk than US1 (these are I/O concerns at the
process edge, exercised by the snapshot/interpreter/read-only test suites) and it
depends on the codebase already being mid-decomposition, so it lands after US1.

**Independent Test**: The snapshot-loading, review-store, and watch/tui
read-only test suites pass unchanged; a fixture run that reads spec-kit + design
artifacts produces an identical `ProjectSnapshot` (same supplied facts, change,
and artifact list), and a review load/save round-trip behaves identically
(including the `review-store-unavailable` / `review-dispatch-failed` fixture
paths).

**Acceptance Scenarios**:

1. **Given** a repository tree with spec-kit and design-system artifacts,
   **When** the snapshot is loaded after the extraction, **Then** the supplied
   facts, the `ProjectChange`, and the resolved artifact list are identical to
   the pre-extraction snapshot.
2. **Given** a recorded review on disk, **When** it is loaded and re-saved after
   the extraction, **Then** the key sanitization, verdict (de)serialization, and
   store path resolution are byte-for-byte identical.
3. **Given** the `review-store-unavailable` and `review-dispatch-failed` fixture
   roots, **When** the host runs, **Then** the same budget accounting and failure
   reasons are produced as before.
4. **Given** the extraction, **When** `runHost` is read, **Then** its body wires
   the effect interpreter and budget folds but delegates artifact reading and
   review-store persistence to the dedicated modules (no inline file I/O bodies
   remain in `runHost`/`main`).

---

### Edge Cases

- **Usage errors with no request** still render as text (never JSON), and are
  written to stderr with the parse-error exit code unchanged.
- **`--out <path>` file output** vs **stdout** both still receive the same
  rendered bytes plus the trailing newline, with parent-directory creation
  unchanged.
- **Interactive `watch`/`tui` surfaces** are dispatched at the executable edge
  before the one-shot snapshot→host→output MVU and write no artifact / carry no
  JSON contract — the decomposition MUST NOT route them through the render or
  artifact-reading concerns in a way that changes their behavior.
- **Missing / unreadable artifact paths** still degrade to the same
  `InputUnavailable` / `missing <path>` reasons rather than tool errors.
- **Directory artifacts** (e.g. `contracts/`) still concatenate their files in
  the same sorted, `### <relative-path>`-prefixed form.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The CLI's rendering concern (text rendering, the
  `fsgg-governance.cli.v1` JSON envelope, parse-error rendering, and all
  `*Json`/`*Text` sub-writers) MUST live in a dedicated compilation unit separate
  from the module that owns argument parsing, command normalization, and the CLI
  MVU.
- **FR-002**: Every existing CLI text and JSON output MUST remain byte-identical
  after the extraction — the JSON envelope schema string, field order, exit
  categories/codes, budget lines, failure/error lines, and payload projections
  are all unchanged.
- **FR-003**: The CLI's artifact-reading concern (spec-kit + design-system path
  resolution, file/directory reads, task/dependency parsing, and fact
  extraction) MUST live in a dedicated compilation unit separate from the
  executable edge.
- **FR-004**: The CLI's review-store persistence concern (store-root resolution,
  key sanitization, verdict serialization/deserialization, load/save, and the
  fixture-driven unavailable/failed paths) MUST live in a dedicated compilation
  unit separate from the executable edge.
- **FR-005**: `runHost` and `main` MUST be reduced to thin orchestration —
  building ports, driving the MVU, and interpreting effects — delegating
  artifact reading and review-store I/O to the extracted modules; no inline file
  I/O bodies remain in `runHost`/`main`.
- **FR-006**: Each new public module MUST carry a curated `.fsi` that exposes
  exactly the helpers being shared and nothing more (Constitution Principle II),
  and the `.fs` body MUST carry no top-level access modifiers.
- **FR-007**: The CLI's user-visible behavior MUST be unchanged: the command set,
  options, exit codes, the read-only `watch`/`tui` interactive surfaces, and
  stdout/stderr/`--out` routing all behave exactly as before.
- **FR-008**: No new external dependency is introduced and no new project is
  added; the new modules are added within the existing `FS.GG.Governance.Cli`
  project, the rendering concern stays a pure function of `CommandResult`, and the
  I/O concerns stay confined to the executable edge.
- **FR-009**: One concern MUST be moved per commit, with the full test suite green
  at every commit, so any byte-drift is isolated to the commit that caused it
  (mirroring the per-seam discipline of Phases A–D).
- **FR-010**: The dependency graph MUST stay acyclic and the CLI MUST remain
  optional — no lower-layer project gains a reference to it (the existing
  "CLI remains optional" scope-guard test stays green).
- **FR-011**: The CLI surface-area baseline MUST be updated additively to reflect
  the new modules (a Tier 1 surface change), and the surface-drift test MUST stay
  green; no symbol that existing callers/tests depend on is removed or renamed
  without a re-export that preserves the call site.

### Key Entities *(include if data involved)*

- **Rendering concern**: a pure projection from `CommandResult` to a string
  (text or JSON), with no filesystem or process access.
- **Artifact-reading concern**: path resolution + file/directory reads + fact
  extraction that turns a repository root into supplied facts and a
  `ProjectChange`; lives at the impure edge.
- **Review-store concern**: load/save of `RecordedReview` values keyed by review
  key, with store-root resolution and the fixture-driven unavailable paths; lives
  at the impure edge.
- **Thin orchestration (`runHost`/`main`)**: builds ports, drives the MVU, and
  interprets effects, holding no inline I/O bodies.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of existing CLI text/JSON transcripts and `*.Cli.Tests`
  goldens/snapshots are byte-identical after the change (zero golden edits other
  than the additive surface baseline).
- **SC-002**: The full test suite is green with per-project test counts at or
  above the pre-change baseline; any added tests are additive (surface drift /
  scope guard for the new modules).
- **SC-003**: A change to text or JSON rendering can be made by editing a single
  rendering unit that contains no argument-parsing or MVU code (verifiable: the
  parser/MVU module no longer contains the `render*` functions).
- **SC-004**: A change to artifact path resolution or review-store persistence
  can be made by editing a single dedicated module that contains no host-driving
  or output-writing code (verifiable: `runHost`/`main` contain no inline
  artifact-path or review-store file-I/O bodies).
- **SC-005**: Roughly 200 LOC are relocated across module boundaries with no net
  behavioral change; the CLI surface baseline grows only additively. (The headline
  ~200 figure tracks the dominant render extraction; the planned total across all
  three module pairs is ≈450 LOC — see research.md D7. The binding criteria remain
  SC-001/002/003/004, which hold regardless of the exact count.)

## Assumptions

- The "next item" in the design report is **Phase E — CLI decomposition**;
  Phases A (073), B (075), C (076), and D (074) are already DELIVERED, leaving E
  as the only undelivered roadmap phase.
- Byte-identical output is the acceptance test, exactly as in Phases A–D: if any
  golden or transcript moves, the extraction changed behavior and must be
  revisited rather than re-blessed.
- The decomposition stays **within** the `FS.GG.Governance.Cli` project (new
  modules / `.fs`+`.fsi` files), consistent with the design's "add focused units,
  do not collapse or add projects" stance; no new assembly is warranted for
  ~200 LOC of edge code.
- Where a previously-public `Cli` render symbol is relied on by tests or callers,
  it is preserved via a thin re-export (or the call site is updated in the same
  commit) so the surface change is additive, not breaking — matching the
  re-export precedent set when `CommandHost` (075) preserved per-host surfaces.
- The interactive `watch`/`tui` edge surfaces and their read-only RouteCommand
  composition are out of scope for relocation; they are only re-pointed at the
  extracted rendering helpers if and only if that leaves their behavior identical.
- Following the prior phases, this is a **Tier 1** change: new `.fsi` surfaces +
  an additively-grown surface baseline, with no behavioral drift.
