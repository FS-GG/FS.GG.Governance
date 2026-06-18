# Phase 0 Research: Kernel Core (F01 · `001-kernel-core`)

All Technical Context unknowns are resolved below. The kernel's behavioral model is
fully specified by [spec.md](./spec.md) and `docs/governance-design/kernel.md`; the
roadmap (`docs/2026-06-18-governance-kernel-speckit-implementation-plan.md`, §F01)
fixes the public surface. No `NEEDS CLARIFICATION` markers remain. The decisions
here are *engineering* choices the spec deliberately left to planning.

## D1 — Fixed-point evaluation strategy

- **Decision**: **Synchronous (naïve) forward chaining**. Each round applies *every*
  rule to the *same* immutable snapshot of the current fact set, collects all
  produced facts, then commits the new ones in a single batch before the next round.
  A `mutable` accumulator (a `Dictionary<FactId, FactAssertion>`) holds the known
  set; the loop runs until a round commits nothing new. Result facts are returned
  sorted by `FactId`.
- **Rationale**:
  - **Order-independence of provenance comes for free.** Because every round reads
    the same snapshot, the *round* at which any fact first becomes derivable depends
    only on the facts and rules, never on the order rules are listed or fire
    (FR-006, SC-001, User Story 3). An incremental/semi-naïve scheme that lets a
    fact derived earlier in a round be visible to a later rule in the *same* round
    would make provenance order-sensitive — rejected for that reason.
  - **Termination is structural.** Rules only add facts (monotonic, FR-012) over a
    bounded fact space, and re-deriving a known `FactId` adds nothing, so the known
    set grows by ≥1 each productive round and is bounded above — it must quiesce
    (FR-003, SC-003). Self-referential chains (edge case) terminate for the same
    reason.
  - **Mutation is explicitly blessed** by Constitution Principle III for "a
    fixed-point rule-evaluation pass"; disclosed at the use site with a one-line
    `// mutable: fixed-point iteration to convergence` comment.
- **Alternatives considered**:
  - *Semi-naïve / delta-driven evaluation* (only re-fire rules whose inputs grew):
    faster, but introduces within-round visibility and a scheduler whose behavior
    can depend on fire order — more code, more risk to the determinism guarantee.
    Deferred; the kernel's job is correctness and explainability, not throughput,
    and the fact space per run is small (SC-005 "light by default"). Revisit only
    if a measured hot path demands it.
  - *Recursive `let rec` worklist*: rejected per Principle III ("recursion is for
    branching structure, not for hiding state") — a mutable accumulator over rounds
    is the plainer expression of this loop.

## D2 — Deterministic provenance tie-break (multi-chain facts)

- **Decision**: When more than one rule produces the *same* new `FactId` in the same
  round, the recorded `ProvenanceStep` is chosen by a **total order on
  `(FactId, RuleId)`** among that round's candidates (lowest wins). The first round
  in which a fact appears is already order-independent (D1); this tie-break makes the
  *justification* reproducible too (User Story 2, Scenario 3; SC-002).
- **Rationale**: `FactId` and `RuleId` are caller-supplied strings, giving a stable,
  total, rule-list-order-independent comparison. "First established it" is thus a
  deterministic function of content, not of evaluation incident.
- **Alternatives considered**: keep *all* producing steps (a fact justified by every
  rule that derives it) — richer, but the spec asks for "the derivation that first
  established it" (singular) and multi-justification belongs to the later evidence
  model (F05); rejected to keep F01 minimal.

## D3 — `identify` as the sole identity authority

- **Decision**: The kernel applies `identify : 'fact -> FactId` to **every** fact —
  supplied and derived — to assign `FactAssertion.Id`, deduplicate, and name inputs
  in provenance. A caller (or rule) need not pre-fill `Id` correctly; `identify`
  wins. Supplied duplicates and re-derived facts collapse by id (FR-007, edge cases
  "duplicate assertions", "re-derivation").
- **Rationale**: Two sources of truth for identity invites drift (mirrors Principle
  II's reasoning about visibility). Soundness of dedup and provenance is "only as
  good as `identify`" (spec Assumptions) — so make `identify` the *only* source.
- **Alternatives considered**: trust the `Id` field supplied on each assertion —
  rejected; it lets a buggy rule split or alias facts and silently corrupt the
  fixed point.

## D4 — `Rounds` semantics (FR-008)

- **Decision**: `Rounds` = **the number of rounds that committed at least one new
  fact**. No rules / nothing derivable ⇒ `Rounds = 0`. A two-step chain
  (`A` supplied, `A⇒B`, `B⇒C`) ⇒ `Rounds = 2`. The final no-op detection round is
  not counted.
- **Rationale**: Maps directly to "rounds *required* to reach quiescence" and gives
  a clean, monotone signal a consumer can watch climb to detect a non-converging
  run in development (FR-008, VI). Pinned by a contract test so it cannot drift.
- **Alternatives considered**: counting the final empty round (off-by-one, less
  intuitive empty case) — rejected for clarity.

## D5 — Test framework & evidence (test project only)

- **Decision**: **Expecto + FsCheck** for the test project. Order-independence is a
  natural *property* ("for any permutation of the rule list, the result is
  identical") — FsCheck shrinks counterexamples; Expecto is idiomatic F# and runs
  the same `dotnet test`/`dotnet run` path.
- **Rationale**: These are **test-project** dependencies only. The kernel assembly
  stays BCL-only (`System.Text.Json` is the only BCL surface it may touch, and F01
  needs none), so SC-005 ("zero heavy dependencies") and the constitution's
  light-kernel constraint are unaffected. Real evidence throughout: real facts, real
  rules, real evaluation — no synthetic fixtures needed (Principle V), so no
  `// SYNTHETIC:` disclosures are expected in F01.
- **Alternatives considered**: *xUnit* — fine and dependency-light, but manual
  shuffling loops are clumsier than an FsCheck property for the headline
  order-independence guarantee. *No property testing* — rejected; SC-001/User Story
  3 are exactly the case property testing exists for.

## D6 — API surface-drift baseline mechanism (FR-011, Principle II)

- **Decision**: Stand up the surface-baseline mechanism alongside F01 (plan §5). A
  test in the test project loads the built Kernel assembly, reflects over its
  **public** types and members, renders them to a **canonical, sorted text** form,
  and asserts equality against a committed baseline at
  `surface/FS.GG.Governance.Kernel.surface.txt`. On mismatch it fails with a diff and
  the instruction to regenerate the baseline intentionally (a `dotnet run`/env-gated
  "bless" path writes the file). Reflection is confined to this **test**, never the
  kernel.
- **Rationale**: A real-evidence drift check (it inspects the actual compiled
  surface, not a hand-list), satisfying FR-011 and Principle II's "surface-area
  baseline … validated by an automated test." Reusable for every later public module
  (F02+). The baseline file is produced during implementation, not in this planning
  phase.
- **Alternatives considered**: diffing the `.fsi` text directly — rejected; the
  compiled surface is the truth (an `.fsi`/`.fs` mismatch is itself a defect the
  reflective check catches). A third-party API-approval package — unnecessary
  dependency for a one-screen reflective walk.

## D7 — Build / packaging shape

- **Decision**: `net10.0`. `src/FS.GG.Governance.Kernel/` (library, `.fsi`+`.fs`),
  `tests/FS.GG.Governance.Kernel.Tests/`, `scripts/prelude.fsx`,
  `surface/`, central `Directory.Build.props` + `Directory.Packages.props`. Package
  identity `FS.GG.Governance.Kernel`. **F01 does NOT pack** — per roadmap §F06 the
  Kernel is packed to `~/.local/share/nuget-local/` only after the first useful
  product is complete; F01 builds and tests in place.
- **Rationale**: Matches the constitution's Engineering Constraints and the
  roadmap's solution layout; central package management keeps the single test-side
  dependency set pinned in one place.
- **Alternatives considered**: pack the kernel at F01 so tests load the `.nupkg` —
  rejected; premature (surface still churns through F02–F06) and the roadmap
  explicitly defers packing to F06. Tests load the built library / prelude instead.

## Deferred / out of scope (confirmed, not unknowns)

- **Structured logging** (`TODO(STRUCTURED_LOGGING)`): no IO in F01; the logging
  choice is recorded in an ADR before F08 (plan §5). `Rounds` is F01's observability
  signal for (non-)convergence.
- **Runtime stratification enforcement**: rejecting non-monotonic rule sets is
  explicitly out of scope (spec Assumptions, FR-012) — F01 documents the
  precondition; analysis features may enforce it later.
- **Cycle rejection**: belongs to the evidence model (F05); within this engine a
  "looping" chain simply re-derives a known fact and adds nothing.
