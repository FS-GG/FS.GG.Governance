---
description: "Task list for F015 - 015-path-capability-routing: deterministic glob precedence routing of paths to capability domains over the F014 typed facts."
---

# Tasks: Path-to-Capability Routing with Deterministic Glob Precedence

**Feature branch**: `015-path-capability-routing` (active spec; git branch currently `main`)
**Spec**: [`specs/015-path-capability-routing/spec.md`](./spec.md)
**Plan**: [`specs/015-path-capability-routing/plan.md`](./plan.md)

**Input**: Design documents from `/specs/015-path-capability-routing/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/Model.fsi](./contracts/Model.fsi), [contracts/Glob.fsi](./contracts/Glob.fsi), [contracts/Routing.fsi](./contracts/Routing.fsi), [contracts/glob-precedence.md](./contracts/glob-precedence.md), [quickstart.md](./quickstart.md)

**Tests**: REQUIRED. This is a **Tier 1** feature (new public, packable surface). Credible evidence is public-surface testing, not private helpers: the glob matcher over real fixture path/glob strings, the precedence ladder (one fixture per FR-005 rung), `Routing.route` over fixture `TypedFacts`, ambiguity/conflict/unsupported diagnostics, determinism and order-independence properties, and a surface-drift check.

**Tier**: the whole feature is **Tier 1** (plan Constitution Check). No per-task tier annotations needed; every task matches the feature tier.

**Elmish/MVU (Principle IV)**: **NOT APPLICABLE** (research D7). Routing is a pure, total function with no I/O, no multi-step state, no retries, and no convergence loop — the candidate path set and the typed facts are inputs, the `RouteReport` is a value. Like Config's pure `Schema.validate`, it uses the constitution's "simple pure functions need no Elmish ceremony" allowance. There is no `Model`/`Msg`/`Effect`/interpreter to build or test.

**Synthetic-evidence discipline (Principle V)**: tests run against real fixture path/glob strings and real in-memory `TypedFacts` (the actual input type a downstream caller passes — not a mock). No agent, network, clock, or filesystem is involved, so no synthetic evidence is anticipated. If any appears it carries `Synthetic` in the test name and a use-site disclosure comment.

**Determinism minimums (FR-012, SC-002/SC-003)**: `RouteReport.Routings` is sorted by normalized path (ordinal) and `RouteReport.Diagnostics` by `(id, path, globs)`; a glob's specificity rank is computed from the glob string alone, so re-ordering the authored path map never changes a route. No wall-clock, environment, random, or host-filesystem value enters any result.

## Status Legend

- `[ ]` pending
- `[X]` done with real evidence (or with synthetic evidence disclosed per Principle V)
- `[-]` skipped (with written rationale)

Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow the scope and document it.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in the phase.
- **[Story]**: `[US1]`..`[US3]`; omitted for setup/foundation/polish.
- Every task names an exact file path.

---

## Phase 1: Setup

**Purpose**: stand up the new optional routing library, its test project, the public contracts, and fixture/scaffolding so the feature type-checks before behavior lands. **No new third-party dependency** is added — the library references `FS.GG.Governance.Config` only (plan Technical Context).

- [X] 🟢 T001 Create `src/FS.GG.Governance.Routing/FS.GG.Governance.Routing.fsproj` targeting `net10.0`, `IsPackable=true`, `PackageId=FS.GG.Governance.Routing`, with a single `<ProjectReference Include="../FS.GG.Governance.Config/FS.GG.Governance.Config.fsproj" />` and **no** `<PackageReference>` (BCL + FSharp.Core only — research D1; the YamlDotNet dep arrives only transitively via Config and is unused by Routing's own code).
- [X] 🟢 T002 Copy `specs/015-path-capability-routing/contracts/Model.fsi` → `src/FS.GG.Governance.Routing/Model.fsi`, `contracts/Glob.fsi` → `src/FS.GG.Governance.Routing/Glob.fsi`, and `contracts/Routing.fsi` → `src/FS.GG.Governance.Routing/Routing.fsi` verbatim as the curated public surface.
- [X] 🟢 T003 Add `failwith "F015"` stub bodies in `src/FS.GG.Governance.Routing/Model.fs`, `Glob.fs`, and `Routing.fs` that satisfy the `.fsi` contracts, in compile order `Model.fs` → `Glob.fs` → `Routing.fs` in the fsproj.
- [X] 🟢 T004 Create `tests/FS.GG.Governance.Routing.Tests/FS.GG.Governance.Routing.Tests.fsproj` with centrally pinned Expecto/Expecto.FsCheck/FsCheck/VSTest packages, `IsPackable=false`, `GenerateProgramFile=false`, and a `ProjectReference` to `src/FS.GG.Governance.Routing` (which transitively brings Config).
- [X] 🟢 T005 [P] Add empty Expecto test modules `tests/FS.GG.Governance.Routing.Tests/Support.fs`, `GlobMatchTests.fs`, `PrecedenceTests.fs`, `RoutingTests.fs`, `AmbiguityTests.fs`, `DeterminismTests.fs`, `SurfaceDriftTests.fs`, and `Main.fs` (in compile order; `Main.fs` runs the assembly tests).
- [X] 🟢 T006 Add `src/FS.GG.Governance.Routing` and `tests/FS.GG.Governance.Routing.Tests` to `FS.GG.Governance.sln`.
- [X] 🟢 T007 [P] Implement fixture builders in `tests/FS.GG.Governance.Routing.Tests/Support.fs`: helpers to build `GovernedPath`/`DomainId`, a `pathMap : (string * string) list -> PathMapEntry list`, and a `facts : root:string -> pathMap:(string*string) list -> TypedFacts` that assembles a minimal valid `TypedFacts` (a `ProjectFacts` with the given `GovernedRoot` and the path map's domains; a `CapabilityFacts` with those `Domains` + `PathMap`; empty surfaces/checks; `Policy=None`, `Tooling=None`). These are REAL inputs of the type a caller passes — not synthetic.
- [X] 🟢 T008 [P] Extend `scripts/prelude.fsx` with an F015 design sketch that `#r`s the built `FS.GG.Governance.Routing` assembly, opens `FS.GG.Governance.Routing` (+ `FS.GG.Governance.Config.Model`), builds a small `TypedFacts`, calls `Routing.route facts [ ... ]`, and records the intended `RouteReport` flow before real bodies land.
- [X] 🟢 T009 [P] Create `specs/015-path-capability-routing/readiness/README.md` listing required transcripts (route→report on a fixture set, the four precedence rungs, the ambiguity/conflict/unsupported diagnostics, the determinism comparison, the surface-baseline drift check) and an SC-traceability note mapping SC-001…SC-006 to the test files that prove them.

**Checkpoint**: `dotnet build src/FS.GG.Governance.Routing` and `dotnet test tests/FS.GG.Governance.Routing.Tests` compile against stubs; the solution lists the two new projects; the Config project reference resolves.

---

## Phase 2: Foundation (Blocking Prerequisites)

**Purpose**: the routing-domain types, the pure glob matcher + syntax check, the governed-root scoping and deterministic-ordering helpers, the effective-path-map partition, and the `route` skeleton — everything US1/US2/US3 build on. **No user-story work begins until this phase is complete.**

- [X] 🟢 T010 Implement `src/FS.GG.Governance.Routing/Model.fs`: `PrecedenceReason`, `RoutingResult`, `PathRouting`, `RoutingDiagnosticId`, `RoutingDiagnostic`, `RouteReport`, and a total `routingDiagnosticIdToken` returning the stable wire token for each id — exactly matching `Model.fsi`. Reuses `FS.GG.Governance.Config.Model` for `GovernedPath`/`DomainId` (does not redefine them). Realizes the FR-013 diagnostic shape (stable id + path/glob + fix-hint message) and the FR-015 structured-result contract (a value later consumers read — no JSON/CLI here).
- [X] 🟢 T011 Implement the pure glob engine in `src/FS.GG.Governance.Routing/Glob.fs` per [contracts/glob-precedence.md](./contracts/glob-precedence.md) §1–2: `checkSyntax` (reject the reserved set `[ ] { } ! ( )` → `Error`), and `matches` (split both sides on `/`; `**` = zero-or-more whole segments via a `let rec` backtracking walk — disclose the recursion at the use site; `*` = zero+ chars within a segment; `?` = one char; ordinal/case-sensitive). No normalization (FR-003).
- [X] 🟢 T012 Implement a pure governed-root scoping helper in `src/FS.GG.Governance.Routing/Routing.fs`: `inRoot : root:GovernedPath -> path:GovernedPath -> bool` as a segment-prefix test where a path equal to the root is in-root (glob-precedence §6, FR-007/FR-008). No I/O.
- [X] 🟢 T013 [P] Implement deterministic ordering helpers in `src/FS.GG.Governance.Routing/Routing.fs`: sort `PathRouting` list by normalized `Path` (ordinal) and `RoutingDiagnostic` list by `(Id, Path, Globs)` (ordinal), reused when assembling the `RouteReport` (FR-012, SC-002).
- [X] 🟢 T014 Implement the effective-path-map partition in `src/FS.GG.Governance.Routing/Routing.fs`: split `CapabilityFacts.PathMap` into (a) usable `glob → domain` bindings and (b) catalog-shape diagnostics — `UnsupportedGlobSyntax` for any glob failing `Glob.checkSyntax`, and `ConflictingGlobBinding` for two entries whose normalized glob string is equal but bind different domains (research D6, FR-009/FR-010). Excluded globs do not participate in matching. Returns both halves; US1 consumes the usable set, US3 surfaces the diagnostics.
- [X] 🟢 T015 Implement the `Routing.route` skeleton in `src/FS.GG.Governance.Routing/Routing.fs`: scope each candidate via `inRoot` (`OutOfScope` when not in root), compute its matching usable globs, return `UnmatchedInRoot` when none match, and assemble a sorted `RouteReport` (T013). The matched-case selection is a placeholder filled by US1 (single match) and US2 (precedence); compiles and returns well-formed reports for the out-of-scope and in-root-unmatched cases. Establishes the FR-001 entry contract (accept `TypedFacts` + candidate paths, no YAML re-parse) and FR-011 purity (no I/O, no clock).

**Checkpoint**: the library builds with real Model + matcher + scoping + partition + skeleton; `route` returns correct `OutOfScope`/`UnmatchedInRoot` results and empty diagnostics for non-matching inputs; matched-path selection still placeholder.

---

## Phase 3: User Story 1 - Route a set of paths to their capability domains (Priority: P1) 🎯 MVP

**Goal**: given typed facts with a (non-overlapping) path map and a candidate-path set, each path routes to its single capability domain with the matched glob, or is reported as in-root-unmatched or out-of-scope — deterministically.

**Independent Test**: `Routing.route` over a fixture `TypedFacts` whose globs don't overlap; assert each path → expected domain + matched glob (reason `OnlyMatch`), an in-root path matching nothing → `UnmatchedInRoot`, and a path outside the governed root → `OutOfScope`.

### Tests for User Story 1 (write first; must FAIL before implementation)

- [X] 🟢 T016 [P] [US1] In `tests/FS.GG.Governance.Routing.Tests/GlobMatchTests.fs`, add `Glob.matches` tests with one accepting case per MVP construct (literal exact, `?`, `*` single-segment, `**` cross-segment) plus representative rejects (`*` not crossing `/`; `**` matching both deep `src/a/b.fs` and shallow `src/a.fs`; `src/**` not matching bare `src`) — SC-006 matcher coverage (FR-002).
- [X] 🟢 T017 [P] [US1] In `tests/FS.GG.Governance.Routing.Tests/RoutingTests.fs`, add `route` tests over a non-overlapping fixture path map asserting: a matched path → `Routed (domain, matchedGlob, OnlyMatch)`; an in-root path matching nothing → `UnmatchedInRoot`; a path outside `GovernedRoot` → `OutOfScope`; `Routings` sorted by path; and an explainability/no-leakage assertion that each `Routed` names its glob + reason and carries no raw YAML or vocabulary beyond declared domains (FR-004/007/008, SC-001/SC-005).
- [X] 🟢 T018 [P] [US1] Add an FSI transcript in `specs/015-path-capability-routing/readiness/` that loads the built library and routes a fixture candidate set, capturing the `RouteReport` (US1 independent-test evidence).

### Implementation for User Story 1

- [X] 🟢 T019 [US1] Implement single-match selection in `Routing.route` (`src/FS.GG.Governance.Routing/Routing.fs`): for each in-root candidate, gather usable globs (T014) that `Glob.matches`; exactly one match → `Routed (domain, glob, OnlyMatch)`; zero → `UnmatchedInRoot` (foundation already handles out-of-scope). Multi-match is left to US2.
- [X] 🟢 T020 [US1] Confirm the assembled `RouteReport` is deterministically sorted (T013) for US1 fixtures and that `OutOfScope`, `UnmatchedInRoot`, and `Routed(OnlyMatch)` are all reachable; diagnostics remain empty for clean non-overlapping fixtures.

**Checkpoint**: non-overlapping routing, in-root-unmatched, and out-of-scope all work and are independently testable. US1 is the MVP.

---

## Phase 4: User Story 2 - Resolve overlapping globs by a total, explainable precedence (Priority: P1)

**Goal**: when several globs match one path, exactly one wins by the total FR-005 order (exact-literal › more literal segments › single-segment `*` over `**` › ordinal tiebreak), the winner is stable across runs and path-map re-ordering, and the result records the precedence reason.

**Independent Test**: a fixture path map with deliberately overlapping globs of differing specificity; assert each overlapped path routes to the single most-specific domain with the right reason, unchanged when the path-map entries are re-ordered.

### Tests for User Story 2 (write first; must FAIL before implementation)

- [X] 🟢 T021 [P] [US2] In `tests/FS.GG.Governance.Routing.Tests/PrecedenceTests.fs`, add the exact-literal rung: an exact-literal glob and a wildcard glob both match a path → routes to the exact-literal domain with reason `ExactLiteral` (FR-005 rung 1, US2 AS2).
- [X] 🟢 T022 [P] [US2] In `PrecedenceTests.fs`, add the literal-segment rung: `src/Adapters/**` and `src/**` both match `src/Adapters/X.fs` → routes to the deeper-literal domain with reason `MoreSpecific` (rung 2, US2 AS1).
- [X] 🟢 T023 [P] [US2] In `PrecedenceTests.fs`, add the single-vs-cross-segment rung: `src/*` and `src/**` both match `src/x` → routes to the `src/*` domain (single-segment more specific than `**`) with reason `MoreSpecific` (rung 3, US2 AS3).
- [X] 🟢 T024 [P] [US2] In `PrecedenceTests.fs`, add the reorder-invariance assertion: the winning domain, matched glob, and reason are unchanged when the overlapping path-map entries are listed in a different order (US2 AS4, FR-012).

### Implementation for User Story 2

- [X] 🟢 T025 [US2] Implement `Glob.specificity` (the ascending **3-rung** key of research D3 / glob-precedence §3 — wildcard-free flag, negated literal-segment count, `**` count; literal-character length is NOT a rung — with `CustomEquality`/`CustomComparison` so equal keys ⇒ co-specific), `Glob.compare` (specificity then ordinal glob-string tiebreak; never 0 for distinct globs), and `Glob.isAmbiguousPair` (equal under `specificity`) in `src/FS.GG.Governance.Routing/Glob.fs`.
- [X] 🟢 T026 [US2] Wire multi-match precedence into `Routing.route` (`src/FS.GG.Governance.Routing/Routing.fs`): among the matching usable globs select the winner via `Glob.compare`, and derive the `PrecedenceReason` — `OnlyMatch` (one match), `ExactLiteral` (winner is wildcard-free and equals the path), else `MoreSpecific` (the `LexicographicTiebreak` case is completed in US3). A path matching ≥1 glob is never left unrouted (FR-005, SC-001).

**Checkpoint**: overlapping globs resolve to one explainable winner, stable across runs and re-ordering. US1 + US2 both pass.

---

## Phase 5: User Story 3 - Make genuinely ambiguous and out-of-scope cases explicit (Priority: P2)

**Goal**: equally-specific competitors emit `AmbiguousRoute` while still resolving to one deterministic winner; same-glob/different-domain and unsupported-syntax globs are diagnosed and excluded; out-of-scope paths never produce an ambiguity and in-root-unmatched paths carry no severity.

**Independent Test**: a fixture with two co-specific globs to different domains → one stable winner + an `AmbiguousRoute` diagnostic; a same-glob/different-domain fixture → `ConflictingGlobBinding`; a reserved-char glob → `UnsupportedGlobSyntax`; an out-of-root path → `OutOfScope` with no ambiguity.

### Tests for User Story 3 (write first; must FAIL before implementation)

- [X] 🟢 T027 [P] [US3] In `tests/FS.GG.Governance.Routing.Tests/AmbiguityTests.fs`, add the ambiguity test: two equally-specific globs binding different domains both match one path → an `AmbiguousRoute` diagnostic naming the path and both competing globs/domains, AND the path resolves to the ordinal-first glob with reason `LexicographicTiebreak` (FR-006, US3 AS1, SC-004).
- [X] 🟢 T028 [P] [US3] In `AmbiguityTests.fs`, add catalog-shape diagnostic tests: two path-map entries with the same glob string but different domains → `ConflictingGlobBinding` (glob excluded from routing, FR-009); a path-map glob containing a reserved char (`[`, `{`, …) → `UnsupportedGlobSyntax` (glob excluded, not a silent never-match, FR-010).
- [X] 🟢 T029 [P] [US3] In `AmbiguityTests.fs` (or `RoutingTests.fs`), add the scoping-explicitness tests: an out-of-root path → `OutOfScope` and produces NO `AmbiguousRoute` diagnostic (US3 AS2); an in-root path matching nothing → `UnmatchedInRoot` carrying in-root status without any finding/severity (US3 AS3, FR-016).

### Implementation for User Story 3

- [X] 🟢 T030 [US3] Complete the ambiguity path in `Routing.route` (`src/FS.GG.Governance.Routing/Routing.fs`): when the top two matching globs satisfy `Glob.isAmbiguousPair`, set the winner's reason to `LexicographicTiebreak` and append an `AmbiguousRoute` diagnostic (path + both globs + domains + fix hint) — the winner stays the `Glob.compare` ordinal-first glob so the result is total (FR-006).
- [X] 🟢 T031 [US3] Surface the foundation partition diagnostics (T014) into `RouteReport.Diagnostics`: `ConflictingGlobBinding` and `UnsupportedGlobSyntax`, each with `Path=None`, the offending glob(s), a fix-hint `Message`, and the stable id (FR-013); ensure excluded globs are absent from the matching set and the diagnostics are sorted (T013).
- [X] 🟢 T032 [US3] Add guards/assertions that `OutOfScope` never yields an `AmbiguousRoute` and `UnmatchedInRoot` carries no severity, keeping the unknown-governed-path finding semantics deferred (FR-007/FR-008/FR-016).

**Checkpoint**: ambiguity is reported with a deterministic winner; conflicting/unsupported globs are diagnosed and excluded; scoping is explicit. US1–US3 pass.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: determinism/order-independence proofs, the surface baseline, dependency/scope hygiene, and the quickstart run.

- [X] 🟢 T033 [P] In `tests/FS.GG.Governance.Routing.Tests/DeterminismTests.fs`, add: route a fixture twice and assert structural equality of the whole `RouteReport` (SC-002); and an FsCheck property that permuting the authored `PathMap` entries yields an identical `RouteReport` for a fixed candidate set (FR-012, SC-003).
- [X] 🟢 T034 Generate `surface/FS.GG.Governance.Routing.surface.txt` from the built `FS.GG.Governance.Routing` assembly using the repo's surface-baseline convention, then add `tests/FS.GG.Governance.Routing.Tests/SurfaceDriftTests.fs` asserting the built surface matches the baseline (Principle II).
- [X] 🟢 T035 [P] In `SurfaceDriftTests.fs` (or a dedicated module), add a dependency-hygiene test asserting `FS.GG.Governance.Routing` references only `FS.GG.Governance.Config` (+ FSharp.Core, + transitive YamlDotNet) and not the kernel/host/adapters/CLI (research D1). This doubles as the FR-016 scope guard: the absence of git/CI, gate-registry, enforcement, surface-classification, and command dependencies confirms no later-phase capability leaked into this feature. It also backstops FR-011 (no I/O dependency) and FR-014 (no catalog-re-validation dependency).
- [X] 🟢 T036 [P] Run [quickstart.md](./quickstart.md) end-to-end and record the transcripts named in `readiness/README.md` (route→report, the four precedence rungs, the ambiguity/conflict/unsupported diagnostics, the determinism comparison), plus the SC-traceability note mapping SC-001…SC-006 to the proving tests.
- [X] 🟢 T037 [P] Update `README.md` to list the new optional `FS.GG.Governance.Routing` library and link the glob/precedence contract ([contracts/glob-precedence.md](./contracts/glob-precedence.md)).

**Checkpoint**: full `dotnet test` green; surface baseline committed and drift-checked; determinism and precedence proven; quickstart validated.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundation (Phase 2)**: depends on Setup — BLOCKS all user stories.
- **User Stories (Phases 3–5)**: all depend on Foundation. US1 and US2 are co-equal P1; US2's multi-match selection (T026) builds on US1's single-match wiring (T019) — both edit `Routing.route`, so run US1 then US2 (or share a developer). US3 (P2) builds on US2's selection (the ambiguity branch refines T026) and the foundation partition (T014).
- **Polish (Phase 6)**: depends on all user stories.

### Within Each User Story

- Tests are written first and must FAIL before implementation.
- `.fsi` contract (Phase 1) → FSI sketch (T008) → semantic tests → implementation (Principle I).
- Model + matcher + scoping + partition + skeleton (Foundation) before single-match (US1) before precedence (US2) before ambiguity/diagnostics (US3).

### Parallel Opportunities

- Setup `[P]` tasks T005, T007, T008, T009 run in parallel (after T001–T004 exist).
- Foundation T013 is `[P]`; T011/T012/T014/T015 touch `Glob.fs`/`Routing.fs` and are largely sequential (T015 depends on T012+T014; T010 first as both modules open `Model`).
- All US1 tests (T016–T018), all US2 tests (T021–T024), and all US3 tests (T027–T029) are `[P]` within their story.
- Most Polish tasks (T033, T035, T036, T037) are `[P]`; T034 must precede T035 only if they share a file.
- The selection-logic impl tasks (T019, T026, T030, T031) all edit `Routing.route` in `Routing.fs` and are therefore sequential, not `[P]`.

---

## Suggested MVP Scope

**User Story 1** (P1) alone is the MVP: with a non-overlapping path map a product can route its candidate paths to capability domains and distinguish in-root-unmatched from out-of-scope — independently valuable before precedence exists. **User Story 2** (the deterministic precedence) is the co-equal P1 that makes overlapping path maps (the normal authoring pattern of broad default + narrow exceptions) safe, and is the literal implementation-plan deliverable. **User Story 3** (P2) hardens ambiguity, catalog-shape diagnostics, and explicit scoping; it follows as a fast third increment.

## Task Count

- Setup: 9 (T001–T009)
- Foundation: 6 (T010–T015)
- US1 (P1): 5 (T016–T020) — 3 tests, 2 implementation
- US2 (P1): 6 (T021–T026) — 4 tests, 2 implementation
- US3 (P2): 6 (T027–T032) — 3 tests, 3 implementation
- Polish: 5 (T033–T037)
- **Total: 37 tasks**

## Implementation Status — 🟢 all 37 complete (real evidence)

`dotnet test tests/FS.GG.Governance.Routing.Tests` → **23/23 green**; full-solution
`dotnet test FS.GG.Governance.sln` stays green. Evidence index and SC traceability in
[`readiness/README.md`](./readiness/README.md); FSI route→report transcript in
[`readiness/fsi-route-transcript.md`](./readiness/fsi-route-transcript.md).

**Honest deviations from the literal task wording (no scope/assertion weakening):**

1. **T002 (verbatim copy) + T010/T025 — two minimal `Glob.fsi` contract corrections.** The
   drafted `Specificity` signature did not compile as written under this repo's settings
   (`Nullable=enable`, `TreatWarningsAsErrors`): a signature-hidden custom-comparison type
   needs `[<Sealed>]` (FS0938), and the `Equals` override must declare `(obj | null)` to match
   the F# nullness inference (FS3261). Both `contracts/Glob.fsi` and the copied
   `src/.../Glob.fsi` were updated identically; the public surface (per the blessed baseline)
   is unchanged in spirit — `Specificity` remains opaque with only `IComparable` + `Equals` +
   `GetHashCode`.
2. **T003 (stub bodies) — superseded.** Rather than land `failwith "F015"` stubs and replace
   them, the real `Model.fs`/`Glob.fs`/`Routing.fs` bodies were written directly against the
   `.fsi` contracts. The stub step's intent (the library type-checks against its signatures
   before behaviour is trusted) is satisfied by the green build + test suite.
3. **T011 matcher — a trailing-`**` correction caught by a test (not weakened).** The first
   matcher let a trailing `**` consume zero segments, so `src/**` wrongly matched bare `src`.
   `GlobMatchTests` failed; the matcher now requires a trailing `**` to consume ≥1 segment (the
   `src/` prefix is mandatory per glob-precedence §1), matching the contract exactly.
