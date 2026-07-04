# Feature Specification: Deferred tail of the 2026-07-02 code review

**Feature Branch**: `111-review-deferred-tail`

**Created**: 2026-07-03

**Status**: Draft

**Input**: Governance issue #83 (last open child of epic #44) — the **deferred low-severity
tail** split from #56/#110. Spec 110 landed the low-risk correctness/dead-code core (#82,
`main` @ 43e3469) and explicitly deferred the findings that each need a **broader
type/`.fsi`/surface change**, a **new shared home** disproportionate to a single Low finding,
or are **cosmetic across many files**. This spec scopes exactly that deferred set. Rationale
per item lives in [`specs/110-low-severity-backlog/spec.md` §Deferred](../110-low-severity-backlog/spec.md).

## Why this is one feature, delivered as several PRs

The thirteen deferred items fall into four natural, independently-testable groups —
**type/API-shape** (B4, B5, B6, B7, B9), **dedup into shared homes** (A1, A4, A6),
**dead-code removal** (C1a, C1b), and **cosmetic/docs hygiene** (C1g, C2f, C2g). They share one
roadmap item (#83) but no coupling, so each user story below is a standalone PR-sized slice
that leaves the tree green. The 110 discipline — keep each PR reviewable, never balloon a Low
finding into a mega-diff — is preserved by *sequencing*, not by deferring further.

**Surface impact:** four items change a published `.fsi` surface and are therefore **Tier 1**
(the surface baseline moves in lockstep, per the constitution): B4 (`GateOutcome` /
`commandFor`), B6 (`ComparisonSample.Agreement`), B7 (`decideMatrix` `boundary`), and B9 iff it
touches `RawSensing`/`assemble`. B5 (`finish` is `private`) and everything in User Stories 4–7
are **Tier 2** (no surface change) unless a dedup deliberately promotes a helper to a shared
`.fsi` (A6-Kernel `Verdict.combineReasons`).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Gate outcomes cannot represent an impossible state (Priority: P1)

The gate-run vocabulary must make "a gate executed but has no exit code" **unrepresentable**,
and the routine that resolves a gate's command must report *why* it found nothing rather than
collapsing three distinct failure modes into a single silent `None`.

**Why this priority**: This is the one deferred item that closes a *type-safety* hole rather
than a cosmetic one. Today `GateOutcome` carries `Disposition`, `ExitCode option`, and
`Passed option` as independent fields (`GateRun/Model.fs:19-23`), so
`{ Disposition = Executed; ExitCode = None; Passed = None }` type-checks even though the `.fsi`
doc-comment says it must never happen. A future edit can silently construct that state and no
compiler or test catches it. Separately, `commandFor` (`GateRun/Plan.fs:95-121`) returns
`GateCommand option`, folding "no `RequiresCommand` prerequisite", "command id resolves to no
spec", and "command line lexes to nothing" all into `None` — a caller cannot tell a
misconfiguration from an absent gate.

**Independent Test**: Attempt to construct an `Executed` outcome without an exit code — it must
fail to compile (or be rejected by a smart constructor). Drive `commandFor` through each of its
three no-command paths and assert a *distinct, typed* reason for each.

**Acceptance Scenarios**:

1. **Given** a gate that executed, **When** its outcome is built, **Then** the exit code and
   pass/fail are carried *by the `Executed` case itself* so an executed-without-exit-code value
   cannot be expressed; the `Reused`/`NotExecuted` cases carry only the fields that apply to
   them.
2. **Given** a gate with no `RequiresCommand` prerequisite, an unresolvable command id, and a
   command line that lexes to nothing, **When** `commandFor` runs on each, **Then** it returns a
   distinguishable typed result per case, not an undifferentiated `None`.
3. **Given** the change, **When** the surface baseline is regenerated, **Then** the
   `GateOutcome`/`GateDisposition` and `commandFor` deltas in `Model.fsi`/`Plan.fsi` are the
   only surface moves, reviewed and accepted in lockstep.

---

### User Story 2 - Public signatures state only what they actually use (Priority: P2)

Three exported surfaces advertise shape they do not honour: a matrix decision takes a parameter
it ignores, a calibration sample records a per-sample agreement nobody reads, and a snapshot
branch hand-builds a result record that the shared assembler already produces. Each should
shrink to its real contract.

**Why this priority**: Latent-correctness and honesty of the API, not a live bug. A signature
that carries a dead parameter or an unread field invites a future caller to depend on it, and a
branch that re-derives a record by hand can drift from the canonical one (different digest sort,
missing field) exactly when it matters least — an error path.

**Independent Test**: (B7) call the matrix decision — the result is identical with the parameter
removed; the `.fsi` no longer names it. (B6) the calibration decision is byte-identical after
the per-sample field is dropped/derived. (B9) the git-unavailable snapshot is byte-identical to
one produced through the shared assembler, including digest ordering.

**Acceptance Scenarios**:

1. **Given** `decideMatrix` (`ValidationMatrix/Matrix.fs:14-27`) whose `boundary` argument is
   only `ignore`d, **When** the parameter is removed from both `Matrix.fs` and `Matrix.fsi`,
   **Then** every caller compiles and the `MatrixPlan` output is unchanged.
2. **Given** `ComparisonSample.Agreement` (`Calibration/Model.fs:24-27`) which the calibration
   `decide` never reads (it uses only sample *count* and the aggregate `ObservedAgreement`),
   **When** the per-sample field is dropped (or made a derived function, not stored), **Then**
   the calibration decision is unchanged and the `Model.fsi` surface moves in lockstep.
3. **Given** the Snapshot `GitUnavailable` branch (`Snapshot/Interpreter.fs:186-198`) which
   hand-builds a `RepoSnapshot` with its own `sortDigests`, **When** it instead routes through
   `Snapshot.assemble` (as the sibling "not a work tree" path already does), **Then** the emitted
   `RepoSnapshot` — diagnostics, digest order, every field — is byte-identical, and any change to
   `RawSensing`/`assemble` needed to express "git unavailable" is reflected in `Snapshot.fsi`.

---

### User Story 3 - The config loader threads parsed values instead of force-unwrapping (Priority: P2)

The typed-config `finish` combinator relies on the runtime invariant "no diagnostics ⇒ every
required field is `Some`" and force-unwraps each field with `.Value` / `Option.get` inside its
build thunk. Threading the parsed `Some` payloads directly removes the force-unwraps so the
happy path is total by construction.

**Why this priority**: `Config/Schema.fs` is the heavily-tested private config loader; the fix
is zero behaviour change but removes a class of latent `NullReference`/`Option.get` throws if
the diagnostics-vs-values invariant is ever broken by an edit. Deferred from #82 precisely
because restructuring the loader is regression-risky and wanted its own PR.

**Independent Test**: The full existing config-loader suite stays green (byte-identical parse
results and diagnostics for every fixture); a review confirms no `.Value`/`Option.get` remains
in the `finish` build thunks (`Schema.fs:540-547, 570-574, 588-590, 622-623` and the
`List.map Option.get` at `Schema.fs:372`).

**Acceptance Scenarios**:

1. **Given** a valid config, **When** it is parsed, **Then** the produced record is identical to
   today's for every fixture.
2. **Given** an invalid config, **When** it is parsed, **Then** the same diagnostics are produced
   and no `.Value`/`Option.get` is evaluated on a `None`.

---

### User Story 4 - Duplicated logic lives in one shared home (Priority: P2)

Byte-identical helpers copied across projects — command-host guard/drive loops, four JSON writer
pairs, and a family of small utilities (`mkFinding`, `safe`, `valuesFor`, `sha256Hex`, the
`stakesOf`/`combineReasons` pipeline, `SddHandoff.buildGate`) — collapse to a single definition,
with any new project-reference edge validated against the dependency-fence suite.

**Why this priority**: Every duplicate is a place where a fix lands in one copy and not the
others (the review found exactly this pattern). Deferred from #82 because some copies need a new
shared home and a dependency-fence review, which is a design step beyond a Low fix.

**Independent Test**: `grep` shows one definition per helper after the change; each consuming
project builds; the dependency-fence tests
(`tests/FS.GG.Governance.DependencyFences.Tests/`) stay green with the new edges; JSON/audit
outputs are byte-identical for real inputs.

**Acceptance Scenarios**:

1. **(A1)** **Given** `EvidenceCommand` and `Scaffold` interpreters carrying local `guard`/`drive`
   copies (`EvidenceCommand/Interpreter.fs:119,143`; `Scaffold/Interpreter.fs:26,164`), **When**
   they adopt `CommandHost.guard`/`CommandHost.drive`, **Then** behaviour is unchanged;
   `EvidenceCommand` already references `CommandHost`, and `Scaffold` gains a `CommandHost`
   project reference recorded as an intended fence edge.
2. **(A4)** **Given** the four byte-identical JSON writer pairs — `writeFreshnessKey`/
   `writePrerequisite` (`GatesJson.fs:42,58` ↔ `RouteJson.fs:61,77`), `writeCacheEligibility`
   (`AuditJson.fs:99` ↔ `RouteJson.fs:120`), `writeGeneratedView(s)` (`AuditJson.fs:194,216` ↔
   `VerifyJson/GeneratedViews.fs:23,45`), and the attestation-ref unwrapper (`ReleaseJson.fs:280`
   ↔ `VerifyJson/ReleaseReadiness.fs:133`) — **When** they move into the existing shared
   `JsonWriters` module, **Then** each projection emits byte-identical JSON and the shared writer
   subsumes both the `option` and non-`option` attestation callers.
3. **(A6)** **Given** the cross-project copies — `mkFinding` ×4, `safe` ×5, `valuesFor` ×2 (the
   `*Checks` packs), `sha256Hex` ×4 (Command/Sensing projects), `Route.stakesOf`'s inline
   re-spelling of `Verdict.combineReasons` (Kernel), and `SddHandoff.buildGate` ×2 — **When**
   each is consolidated to a single source in the *natural* shared home (SurfaceChecks for the
   `*Checks` helpers; Kernel for `combineReasons`, exported via `Verdict.fsi`; a shared hashing
   home for `sha256Hex`; one `buildGate` inside `Adapters.SddHandoff`), **Then** every consumer
   builds, outputs are byte-identical, and the fence review documents each new edge (notably the
   `sha256Hex` home and the 5th `safe` in `ReleaseFactsSensing`, which does not reference
   SurfaceChecks).

---

### User Story 5 - Dead code is removed with no output change (Priority: P3)

Two provably-unreachable elements are deleted: the DocsChecks example-freshness path that folds
over an always-empty list, and the VerifyCommand `SurfacesPending` field that is written four
times and never read.

**Why this priority**: Pure cleanup; no behaviour change. Deferred from #82 only because each
removal ripples through a model and its `.fsi`.

**Independent Test**: `grep` confirms the removed vocabulary is gone; the DocsChecks and
VerifyCommand suites stay green with byte-identical outputs.

**Acceptance Scenarios**:

1. **(C1a)** **Given** `DocsChecks.exampleFindings` (`DocsChecks.fs:61-70`) which folds over
   `facts.Examples`, hardcoded to `[]` at both `senseDocs` return sites
   (`DocsChecks/Interpreter.fs:110,139`) so no `ExampleFact` is ever constructed, **When** the
   dead example path and its now-unreachable `ExampleOutcome`/`ExampleFact` vocabulary
   (`Model.fs`/`Model.fsi`) are removed, **Then** DocsChecks output is unchanged for every input.
2. **(C1b)** **Given** `VerifyCommand.SurfacesPending` (`Loop.fs:179`, `Loop.fsi:258`) written at
   `Loop.fs:337,776,784,898` and read nowhere, **When** the field is removed, **Then** the
   readiness gate and `verify.json` projection are unchanged.

---

### User Story 6 - Stale headers and dead opens no longer mislead a reader (Priority: P3)

File headers that claim "no access modifiers" while the file uses `let private`, and `open`
statements left dead by the 073 / ADR-0007 extractions, are corrected so the source documents
its actual state.

**Why this priority**: Cosmetic; F# does not even warn on an unused `open`, so this is
reader-trust hygiene. Deferred from #82 as a cosmetic change spanning many files.

**Independent Test**: Each named header matches the file's actual access modifiers; the removed
`open`s are provably unused (build stays green); `grep` confirms no `System.IO`/`System.Text`
identifier remains in a file whose `open` was removed.

**Acceptance Scenarios**:

1. **(C1g-headers)** **Given** the six files whose header claims "no access modifiers" while
   using `let private` — `ReleaseReport/Report.fs:14`, `Gates/Gates.fs:3`,
   `HumanRender/Capability.fs:2`, `CostBudget/Findings.fs:15`, `Findings/Findings.fs:3`,
   `AttestationJson/AttestationJson.fs:22` — **When** each header is corrected, **Then** it
   describes the file accurately.
2. **(C1g-opens)** **Given** the dead `open System.IO` left by the command-host extraction in
   `VerifyCommand/Interpreter.fs:14`, `ShipCommand/Interpreter.fs:13`,
   `EvidenceCommand/Interpreter.fs:14` (and the wider set of dead `open System.IO`/`System.Text`
   left by the 073 JSON consolidation across the projection projects), **When** they are removed,
   **Then** the build stays green.

---

### User Story 7 - The docs record the conventions they currently omit (Priority: P3)

Two documentation gaps close: the reason a host project declares far more `ProjectReference`s
than it transitively needs, and the missing index over the local decision records.

**Why this priority**: Documentation only; no code change. Deferred from #82 because pruning the
references (the alternative) would risk the build for a doc-only finding.

**Independent Test**: A reader can find, in-repo, why `VerifyCommand` declares 43 references, and
an index listing the local decision records.

**Acceptance Scenarios**:

1. **(C2f)** **Given** `VerifyCommand.fsproj` declaring 43 `<ProjectReference>`s while ~32 are
   transitively reachable, **When** the convention "a top-of-tree host wires its full surface
   explicitly rather than relying on transitive flow" is documented, **Then** the discrepancy is
   explained and no reference is pruned.
2. **(C2g)** **Given** `docs/decisions/` holding `0001`–`0008` with no index, and `docs/adr/README.md`
   already pointing to org-level ADR-0012/0013 in `FS-GG/.github`, **When** a local index for
   `docs/decisions/` is added, **Then** the index lists every local decision and cross-links the
   org-ADR pointer, resolving the "0012/0013 stubs" ask by pointing at the org records rather
   than minting conflicting local numbers.

---

### Edge Cases

- **B9 / B4 output invariance:** the surface-changing fixes MUST NOT alter emitted JSON or
  human-rendered output for any real input — only the *type* and *diagnostic shape* change. The
  git-unavailable snapshot, in particular, must keep identical diagnostics and digest ordering.
- **A6 fence edges:** if the natural shared home for a helper would introduce a *disallowed*
  dependency edge (e.g. centralizing `sha256Hex` where the consuming projects share no hashing
  home, or moving `safe` to SurfaceChecks when `ReleaseFactsSensing` does not reference it), the
  helper stays duplicated and the item is re-deferred **with a recorded rationale** rather than
  forcing an unsound edge.
- **C1g scope creep:** the wider 073 dead-`open` list is *bonus* — sweeping it is allowed but
  must not turn the cosmetic PR into a broad refactor; the minimum deliverable is the six headers
  + three command-host `open`s.

## Requirements *(mandatory)*

### Functional Requirements

**Type / API-shape (Tier 1 except FR-003)**

- **FR-001** (B4): `GateOutcome` MUST be restructured so an `Executed` gate carries its exit code
  and pass/fail *within the case*, making "executed without exit code" unrepresentable; the
  `GateRun` `.fsi` surface baseline MUST move in lockstep.
- **FR-002** (B4): `commandFor` MUST return a typed result that distinguishes its three
  no-command outcomes (no prerequisite / unresolved id / empty lex) instead of a single `None`;
  callers MUST handle the typed result and the `Plan.fsi` surface MUST move in lockstep.
- **FR-003** (B5): The `Config/Schema.fs` `finish` build thunks MUST thread parsed `Some`
  payloads instead of force-unwrapping with `.Value`/`Option.get`; parse results and diagnostics
  MUST be byte-identical for every fixture. (No `.fsi` surface change — `finish` is `private`.)
- **FR-004** (B6): The unread per-sample `ComparisonSample.Agreement` field MUST be dropped (or
  replaced by a derived function, not stored); the calibration decision MUST be unchanged and the
  `Calibration/Model.fsi` surface baseline MUST move in lockstep.
- **FR-005** (B7): The dead `boundary` parameter MUST be removed from `decideMatrix` in both
  `Matrix.fs` and `Matrix.fsi`; all callers compile and `MatrixPlan` output is unchanged.
- **FR-006** (B9): The Snapshot `GitUnavailable` branch MUST produce its `RepoSnapshot` through
  `Snapshot.assemble` (shared digest sort), not an inline hand-rolled record; output MUST be
  byte-identical, and any `RawSensing`/`assemble` change needed is reflected in `Snapshot.fsi`.

**Dedup (Tier 2 except the intentional FR-009 export)**

- **FR-007** (A1): `EvidenceCommand` and `Scaffold` interpreters MUST use `CommandHost.guard`/
  `CommandHost.drive` instead of their local copies; `Scaffold` gains a `CommandHost` project
  reference recorded as an intended fence edge.
- **FR-008** (A4): The four byte-identical JSON writer pairs (`writeFreshnessKey`/
  `writePrerequisite`, `writeCacheEligibility`, `writeGeneratedView(s)`, the attestation-ref
  unwrapper) MUST move into the shared `JsonWriters` module; each projection MUST emit
  byte-identical JSON and `GatesJson`/`ReleaseJson` gain a `JsonWriters` reference as needed.
- **FR-009** (A6): Each cross-project duplicate (`mkFinding`, `safe`, `valuesFor`, `sha256Hex`,
  the `stakesOf`/`combineReasons` pipeline, `SddHandoff.buildGate`) MUST collapse to one source in
  its natural shared home; `Verdict.combineReasons` MUST be exported via `Verdict.fsi` (surface
  baseline updated) so `Route.stakesOf` reuses it; every new project-reference edge MUST keep the
  dependency-fence suite green, and any edge that would be unsound leaves that helper duplicated
  with a recorded rationale.

**Dead code / cosmetic / docs (Tier 2)**

- **FR-010** (C1a): The unreachable DocsChecks example-freshness path and its now-dead
  `ExampleOutcome`/`ExampleFact` vocabulary MUST be removed with byte-identical DocsChecks output.
- **FR-011** (C1b): The write-only `VerifyCommand.SurfacesPending` field MUST be removed from
  `Loop.fs`/`Loop.fsi` with no change to the readiness gate or `verify.json`.
- **FR-012** (C1g): The six stale "no access modifiers" headers MUST be corrected and the three
  command-host dead `open System.IO` (plus, at implementer discretion, the wider 073 dead-`open`
  set) MUST be removed with the build staying green.
- **FR-013** (C2f): The `VerifyCommand` full-surface `ProjectReference` convention MUST be
  documented; no reference is pruned.
- **FR-014** (C2g): A local index over `docs/decisions/` (0001–0008) MUST be added, cross-linking
  the `docs/adr/README.md` org-ADR pointer table; the "0012/0013 stubs" ask is resolved by
  pointing at the org ADRs, not by minting local numbers.

**Cross-cutting**

- **FR-015**: Every behaviour-or-type change (FR-001, FR-002, FR-004, FR-005, FR-006) MUST have a
  test that fails before and passes after (RED→GREEN, or a compile-fail demonstration for the
  make-illegal-unrepresentable items). Every output-preserving change (FR-003, FR-007…FR-014)
  MUST rely on an unchanged-output assertion or the existing suite proving no drift.
- **FR-016**: The surface baseline MUST move only for the intentional Tier-1 deltas (FR-001,
  FR-002, FR-004, FR-005, FR-006 iff it touches `Snapshot.fsi`, and FR-009's `combineReasons`
  export); every other item leaves the surface untouched.

### Key Entities

- **GateOutcome / GateDisposition** (`GateRun/Model.fs`) — restructured so exit code and pass/fail
  live inside the `Executed`/`Reused` cases; illegal combinations become unrepresentable.
- **commandFor result** (`GateRun/Plan.fs`) — a typed outcome replacing `GateCommand option`.
- **ComparisonSample** (`Calibration/Model.fs`) — loses its unread `Agreement` field.
- **decideMatrix** (`ValidationMatrix/Matrix.fs`) — loses its dead `boundary` parameter.
- **RawSensing / assemble** (`Snapshot`) — the git-unavailable case flows through the shared
  assembler.
- **JsonWriters** (`FS.GG.Governance.JsonWriters`) — gains the four consolidated writer pairs.
- **Verdict.combineReasons** (`Kernel`) — promoted to `Verdict.fsi` so `Route.stakesOf` reuses it.
- **SurfaceChecks** — natural shared home for `mkFinding`/`safe`/`valuesFor` (domain + maturity
  parameterized).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Full test suite, surface-drift, and api-compat gates green; the *only* surface
  baseline deltas are the intentional Tier-1 changes named in FR-016 — no unplanned surface move.
- **SC-002**: New RED→GREEN (or compile-fail) tests cover FR-001, FR-002, FR-004, FR-005, FR-006;
  each fails on `main` and passes on the branch.
- **SC-003**: `grep` confirms exactly one definition remains for each consolidated helper
  (`guard`/`drive` in EvidenceCommand/Scaffold, the four JSON writer pairs, `mkFinding`, `safe`,
  `valuesFor`, `sha256Hex`, `SddHandoff.buildGate`) and that the removed dead vocabulary
  (`ExampleFact`, `SurfacesPending`) is gone; JSON/audit/snapshot outputs are byte-identical for
  real inputs.
- **SC-004**: Issue #83's checklist reflects every landed item checked and every item that hit an
  unsound fence edge annotated with its re-deferral rationale; epic #44 can close once #83 closes.
- **SC-005**: Each user story lands as its own reviewable PR that leaves the tree green, so no
  single diff bundles a surface change with an unrelated cosmetic sweep.

## Assumptions

- **One roadmap item, several PRs.** #83 is a single Coordination-board item; this spec covers all
  of it, but User Stories 1–7 are expected to land as separate PRs (SC-005) to preserve the 110
  reviewability discipline. Story priority (P1 → P3) is the suggested landing order.
- **Byte-identical output for legitimate inputs.** Every dedup, dead-code, and cosmetic change
  keeps output identical for real inputs; only the *types* and *diagnostic shapes* of the
  surface-changing items move, and even those preserve emitted JSON/human output.
- **Natural shared homes, sound fences only.** A6 consolidates into the home that adds no unsound
  dependency edge (SurfaceChecks for the `*Checks` helpers, Kernel for `combineReasons`); a helper
  whose only viable home would break a fence stays duplicated with a recorded rationale rather than
  forcing the edge. The dependency-fence suite is the gate.
- **Deferred items stay honest on #83.** Anything this spec cannot land soundly (an unsound fence
  edge, an output-changing surprise) is re-annotated on #83, not silently dropped, so epic #44
  closes on a truthful record.
- **C2g numbering.** Local `docs/decisions/` (0001–0008) and org-level ADRs (…0012/0013 in
  `FS-GG/.github`) are distinct sequences; the index cross-links rather than reconciling them.
