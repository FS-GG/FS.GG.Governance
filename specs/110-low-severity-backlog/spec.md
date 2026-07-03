# Feature Specification: Residual low-severity backlog (2026-07-02 review)

**Feature Branch**: `110-low-severity-backlog`

**Created**: 2026-07-03

**Status**: Draft

**Input**: Governance issue #56 (Epic #44) — the grouped **Low-severity** findings of the
2026-07-02 code quality & architecture review. This is the last open child of #44; every
High and Medium child (#45–#55, #63) has already landed. A triage of #56 against current
`main` found roughly a third of its bullets **already resolved** by those intervening PRs
(#49/#69/#70/#71 command-host consolidation, #53/#61 fences, #54/#65 CI hygiene,
#55/#77 CLI edges, #63/#80 VersionPrefix). This spec scopes only the **residual open**
findings, and — to stay reviewable — lands the low-risk correctness/dead-code core here,
explicitly deferring the type-redesigns and cross-project dedup that would balloon the PR.

## Triage outcome (why this scope)

**Already FIXED on main** (verified, no action): EvidenceCommand copy of ArtifactReading
(A2), dead `CommandHost.ExitDecision` (A3), Ship/Verify `Wrote(Ok)` drift + Done-inertness
(A7-drift/B14), Kernel `Json.fs` boundary doc (B8), ReviewStore collisions (B10),
`ArtifactReading` `present` no-op (C1c), `MissingCommand` help lists watch/tui (C1h),
workflow `timeout-minutes` (C2a), non-semver tag guard (C2b), de-duplicated NuGet fallback
user (C2c), VersionPrefix centralization (C2e).

**Documented-as-wontfix** (a comment/ADR already owns the decision, no code change wanted):
permanently-empty `currency.unresolved[].missing` contract field (C1e), per-PR api-compat
job kept deliberately (C2d), divergent CLI format-flag vocabularies — ADR-0006 (C2h).

**In scope (this PR)** — residual open, low-risk, self-contained (no new public surface beyond
the two intentional additions `Config.costRank` and `SkillChecks.MirrorUnreadable`):
- Correctness edges: B1, B2, B3, B4-lexer, B11, B12, B13
- Dead code / perf: C1d, C1f

**Deferred (tracked as remaining checkboxes on #56)** — each either needs a broader
type/`.fsi`/surface change, a new shared project disproportionate to a Low finding, or is
purely cosmetic across many files; called out with rationale in
[Deferred](#deferred-tracked-on-56):
B4-`GateOutcome`/`commandFor` DU redesign, B5, B6, B7, **B9** (extends the public
`Snapshot.RawSensing`/`assemble` contract), A1-residual, A4, A5, A6, A7-fold, **C1a**/**C1b**
(dead-code removal with model/`.fsi`/surface ripple), **C1g** (cosmetic header/opens across
9 files — F# does not even warn on unused opens), C2f, C2g.

> **A5 note:** the byte-identical `AuditJson.ofShipDecision` → `…WithGeneratedViews`
> delegation is small and self-contained, so it DID land here (FR-013) even though the rest of
> the dedup family is deferred.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A long timeout never overflows into a leaked process (Priority: P1)

A gate configured with a very large `TimeoutLimit` (or a non-positive one) must behave
predictably: it must not overflow the millisecond math into a negative wait that
misreports as a start failure while orphaning the process it already spawned.

**Why this priority**: This is the only residual bullet with a real resource leak.
`GateExecution/Interpreter.fs` computes `seconds * 1000` as `int`; for `seconds >
2_147_483` the product overflows to negative, `proc.WaitForExit` throws, the `with ex ->`
arm reifies a start-failure verdict, and the started process is never killed.

**Independent Test**: Drive the timeout math with `seconds = Int32.MaxValue` and assert a
non-negative wait (or `Timeout.Infinite`) with no exception; assert a spawned process is
disposed/killed on the timeout path.

**Acceptance Scenarios**:

1. **Given** a `TimeoutLimit` whose `seconds * 1000` would overflow `int`, **When** the
   wait is computed, **Then** it clamps to `Timeout.Infinite` (or an `int64` ms wait), never
   a negative value.
2. **Given** a non-positive `TimeoutLimit`, **When** it is constructed, **Then** it is
   rejected/normalized at the boundary rather than silently flowing through.
3. **Given** the timeout elapses, **When** the wait returns, **Then** the started process is
   killed/disposed (no orphan).

### User Story 2 - Determinism-critical sense hashes are injective (Priority: P1)

The freshness/rule-hash cache inputs must not be spoofable by the exact name/content
concatenation ambiguity the repo's own `FreshnessKey` length-prefixing discipline exists to
prevent, and a rename/move of source must be observable.

**Why this priority**: `senseCatalogHash` is a `RuleHash` cache input; a collision silently
serves a stale rule verdict. `senseSrcHashes` dropping the path makes a rename invisible to
the cache.

**Independent Test**: Two catalogs `{name:"ab",content:"c"}` and `{name:"a",content:"bc"}`
must hash differently; a file renamed with identical content must change `senseSrcHashes`.

**Acceptance Scenarios**:

1. **Given** two (name,content) pairs whose naive concatenation is equal, **When** hashed,
   **Then** the digests differ (length-prefixed pre-image).
2. **Given** identical content at a different path, **When** `senseSrcHashes` runs, **Then**
   the hash reflects the path (rename-sensitive).

### User Story 3 - The check sensors never fail open and never mislabel (Priority: P1)

The `*Checks` sensors promise "never throw / degrade explicitly." A design-catalog read that
throws, a skill path like `notes..md`, or an unreadable declared mirror must each produce the
*correct* explicit disposition — not an escaped exception, an over-rejection, or a wrong
"no mirror declared" verdict.

**Why this priority**: Fail-open/mislabel in a governance sensor yields a wrong verdict, the
worst outcome for the tool.

**Independent Test**: (a) a throwing design port surfaces as a sensed error, not an escape;
(b) `notes..md` is accepted while `../escape` is still rejected; (c) an unreadable declared
mirror reports a distinct `MirrorUnreadable`, not `NoMirrorDeclared`.

**Acceptance Scenarios**:

1. **Given** any of the five DesignChecks port calls throws, **When** `senseDesign` runs,
   **Then** it returns a sensed error (all five wrapped in `safe`), never an escaped throw.
2. **Given** a claimed skill file `notes..md`, **When** SkillChecks validates the path,
   **Then** it is accepted; **Given** `a/../b`, **Then** it is still rejected (segment check,
   not substring).
3. **Given** a declared mirror whose read errors, **When** SkillChecks senses it, **Then**
   the disposition is a distinct unreadable/error case, not `NoMirrorDeclared`.

### User Story 4 - Cost ordering survives DU reordering; totality is fuel-bounded (Priority: P2)

Comparing gate `Cost` must reflect intended cheap→expensive order regardless of DU
declaration order, and the kernel fixed-point must carry a fuel bound consistent with the
totality it promises elsewhere.

**Why this priority**: Latent correctness; a future reorder of the `Cost` cases would
silently invert "cheaper", and an unbounded convergence loop is a latent hang if an
invariant ever breaks.

**Independent Test**: A `costRank` orders the cases explicitly (a property test pins the
intended order); `FixedPoint.evaluate` stops after a bounded number of rounds.

**Acceptance Scenarios**:

1. **Given** the `Cost` cases, **When** compared via `costRank`, **Then** ordering matches
   intent and is invariant to case declaration order.
2. **Given** a rule/fact set, **When** `evaluate` iterates, **Then** it terminates within
   `(#rules|#facts)+1` rounds and does not loop unbounded.

### User Story 5 - Robustness & hygiene: lexer, snapshot path, dead code, perf, stale docs (Priority: P3)

**Why this priority**: Robustness and cleanliness; no user-visible behavior change beyond
rejecting malformed input.

**Independent Test / Acceptance**:

1. `lexCommandLine` returns `None` on an unterminated quote instead of silently accepting it.
2. The Snapshot `GitUnavailable` branch routes through `Snapshot.assemble` (same digest sort
   as every other branch), not a hand-built record.
3. Dead code removed with identical output: DocsChecks `Example*` path (C1a), VerifyCommand
   `SurfacesPending` (C1b), `pack-and-apicheck.fsx` diagnostic loop (C1d).
4. The two O(n²) folds (`VerifyJson/Core.fs`, `SddHandoff/Reader.fs`) become O(n) with
   byte-identical output.
5. Stale headers claiming "no access modifiers" are corrected and the dead
   `System.IO`/`System.Text` opens from the 073 extraction are removed (C1g).
6. Safe dedup: `AuditJson.ofShipDecision` delegates to the `…WithGeneratedViews` variant
   (A5); the three Kernel `Severity -> string` sites and `Route.stakesOf` reuse a single
   source (A6-Kernel); EvidenceCommand/Scaffold adopt `CommandHost.guard`/`drive` (A1).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001** (B3): The gate-timeout wait MUST NOT overflow to a negative value; a
  `seconds * 1000` that exceeds `Int32.MaxValue` MUST clamp to `Timeout.Infinite` (or use
  `int64` ms). A non-positive `TimeoutLimit` MUST be rejected/normalized at its boundary.
- **FR-002** (B3): The timeout path MUST kill/dispose the started process (no orphan) when
  the wait ends or throws.
- **FR-003** (B11): `senseCatalogHash` MUST length-prefix each (name,content) pre-image so
  distinct pairs never collide; `senseSrcHashes` MUST incorporate the path so a rename with
  identical content changes the hash.
- **FR-004** (B13): All five DesignChecks port calls MUST be wrapped so a throw becomes a
  sensed error; `senseDesign` MUST NOT let an exception escape.
- **FR-005** (B12): SkillChecks path validation MUST reject `..` only as a **path segment**
  (accepting `notes..md`), and MUST report an unreadable declared mirror as a distinct
  unreadable/error disposition, not `NoMirrorDeclared`.
- **FR-006** (B1): `Cost` comparisons MUST go through an explicit `costRank` (mirroring
  `generatedProductTierRank`) so ordering is invariant to case declaration order; the fix
  MUST NOT change any public `.fsi` surface (kept private to the call sites or, if exported,
  the surface baseline is updated in lockstep).
- **FR-007** (B2): `FixedPoint.evaluate` MUST bound its convergence loop by a fuel/round cap
  of at least `(#rules|#facts)+1`, preserving current results for all converging inputs.
- **FR-008** (B4-lexer): `lexCommandLine` MUST return `None` (or a typed error) on an
  unterminated quote rather than silently accepting it.
- **FR-009** (B9): The Snapshot `GitUnavailable` branch MUST build its `RepoSnapshot` through
  `Snapshot.assemble` (shared digest sort), not an inline hand-rolled record.
- **FR-010** (C1a/C1b/C1d): The unreachable DocsChecks `Example*` path, the write-only
  VerifyCommand `SurfacesPending` field, and the dead `pack-and-apicheck.fsx` diagnostic loop
  MUST be removed with no output change.
- **FR-011** (C1f): The O(n²) dedup in `VerifyJson/Core.fs` and the O(n²) list-append fold in
  `SddHandoff/Reader.fs` MUST be replaced with O(n) equivalents producing byte-identical
  output.
- **FR-012** (C1g): Headers claiming "no access modifiers" while the file uses `let private`
  MUST be corrected, and the dead `System.IO`/`System.Text` opens left by the 073 extraction
  MUST be removed (build stays green — they are provably unused).
- **FR-013** (A5): `AuditJson.ofShipDecision` MUST delegate to `ofShipDecisionWithGeneratedViews`
  with an empty views list (byte-identical output), removing the duplicated body.
- **FR-014** (A6-Kernel): The three Kernel `Severity -> string` sites MUST share one source,
  and `Route.stakesOf` MUST reuse `Verdict.combineReasons` (exposed via `Verdict.fsi`, with
  the surface baseline updated) instead of re-implementing it.
- **FR-015** (A1): `EvidenceCommand` and `Scaffold` interpreters MUST use
  `CommandHost.guard`/`CommandHost.drive` instead of their local copies.
- **FR-016**: Every behavioral change (FR-001…FR-009) MUST have a test that fails before and
  passes after (RED→GREEN); every output-preserving change (FR-010…FR-015) MUST have an
  unchanged-output assertion or rely on the existing suite proving no drift.

### Key Entities

- **TimeoutLimit** (`Config/Model.fs`) — gains boundary validation; the ms computation in
  `GateExecution/Interpreter.fs` becomes overflow-safe.
- **costRank** (`Config`) — new total `Cost -> int` ordering, mirroring
  `generatedProductTierRank`.
- **Verdict.combineReasons** (`Kernel`) — promoted to the `.fsi` so `Route.stakesOf` reuses
  it (surface baseline updated).
- **MirrorUnreadable** (SkillChecks) — a distinct disposition for a declared-but-unreadable
  mirror.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Full test suite, surface-drift, and api-compat gates green; the only surface
  baseline delta is the intentional `Verdict.combineReasons` export (FR-014).
- **SC-002**: New RED→GREEN tests cover FR-001…FR-009; each fails on `main` and passes on the
  branch.
- **SC-003**: `grep` confirms the removed dead code (DocsChecks `Example*`, VerifyCommand
  `SurfacesPending`, pack-script loop) is gone; the JSON/audit and hash outputs are
  byte-identical for real inputs.
- **SC-004**: Issue #56 checklist reflects every landed item checked and every deferred item
  annotated with its rationale; epic #44 can close once #56 closes.

## Deferred (tracked on #56)

Each of these needs a change disproportionate to a Low finding; left as open checkboxes on
#56 with this rationale:

- **B4 `GateOutcome`/`commandFor` DU redesign** — making `Executed`-without-exit-code
  unrepresentable and giving `commandFor` a typed error DU touches `GateRun/Model.fs` types,
  every consumer, and the `.fsi`/surface; the cheap robustness win (lexer, FR-008) lands now.
- **B5 `Config/Schema.fs` `.Value` coupling** — restructuring `finish` to thread values is a
  regression-risky refactor of the heavily-tested private config loader for zero behavior
  change; deferred to avoid blast radius in this grouped PR.
- **B6 Calibration `Agreement` field** — deriving agreement from samples vs dropping the
  unused per-sample field is a modelling decision touching `Calibration/Model.fs` + `.fsi` +
  surface.
- **B7 `decideMatrix` dead `boundary` param** — removing it changes the public `Matrix.fsi`
  signature + surface for a single `ignore boundary`; batched with the other surface-touching
  items instead of one-off.
- **A4 four JSON writer pairs** — moving `writeFreshnessKey`/`writePrerequisite`,
  `writeCacheEligibility`, `writeGeneratedView(s)`, and the attestation unwrapper into the
  shared `JsonWriters` module is real churn across five projection projects; a focused
  follow-up.
- **A7-fold ViewCurrency** — Ship can't reference `VerifyCommand`; sharing the fold needs a
  `CommandHost` (or shared) home, a small design step beyond a Low fix.
- **Cross-project A6** (`mkFinding` ×4, `safe` ×5, `valuesFor` ×2, `sha256Hex` ×4,
  SddHandoff `buildGate`) — the `*Checks` packs are independent projects with deliberate
  dependency fences; deduping needs a new shared `Checks.Common`/Kernel home and a fence
  review, disproportionate to Low. (The single-project Kernel `Severity`/`combineReasons`
  dedup DOES land here — FR-014.)
- **C2f VerifyCommand ProjectReferences** — pruning 43→~11 risks the build for a
  documentation-only finding; a documented convention is the right fix, batched separately.
- **C2g `docs/decisions/` index** — the directory exists (0001–0007); adding a README index
  and the missing 0012/0013 stubs is a docs task, not code.

## Assumptions

- **Change classification: mostly Tier 2** (no surface change) except FR-014's intentional
  `Verdict.combineReasons` export (Tier 1 — surface baseline updated in lockstep) and any
  boundary-validation constructor that lands on a public `.fsi`.
- **The review store, sense hashes, and JSON writers keep byte-identical output for
  real/legitimate inputs** — only malformed/adversarial inputs (unterminated quote, colliding
  pre-image, throwing port) change behavior, which is the point.
- **Deferred items stay open on #56** rather than silently dropped; #56 closes only when its
  checklist is either done or annotated-deferred, so epic #44 closes on an honest record.
