# Phase 0 Research: Evidence Model & Synthetic Taint (F05 · `005-evidence-model`)

All Technical Context unknowns are resolved below. The behavioural model is fixed by
[spec.md](./spec.md) and `docs/governance-design/kernel.md` ("The evidence model" — the
six-case `EvidenceState`, the `effective(t)` taint rule, and the DAG/auto-clear prose); the
roadmap (`docs/2026-06-18-governance-kernel-speckit-implementation-plan.md`, §F05) fixes the
public surface (`EvidenceState`, `EvidenceGraph`, `effective`) and assigns the
**reinforcement of decision #4** (DAG only, no cycles). No `NEEDS CLARIFICATION` markers
remain. The decisions here are the *engineering* choices the spec deliberately left to
planning (spec Assumptions: "the exact representation of node identity, the precise shape of
the graph constructor and its cycle-rejection result, and the exact signature of `effective`
are implementation/design details fixed in the plan and the `.fsi`").

## D1 — Where the model lives, compile order, and the standalone shape

- **Decision**: Add the evidence model to the **existing `FS.GG.Governance.Kernel` assembly**
  as a new `Evidence.fsi`/`Evidence.fs` pair in `src/FS.GG.Governance.Kernel/`, compiled
  **after** `Kernel.*` (hence after `Verdict.*`) and **before** `Check.*`. No new project.
  The module is `Evidence` (a type-companion module on no single type — it owns `build`,
  `nodes`, `dependencies`, `effective`).
- **Rationale**: F05 is `[P after F01]` in the roadmap and depends **only** on F01 — and in
  practice on F01's *discipline* (monotone least-fixed-point, determinism) rather than its
  code: `Evidence.fs` references no `Verdict`/`Check`/`CheckRule`/`Kernel` type (node identity
  is its own generic `'id`; states are its own `EvidenceState`). It is independent of
  F02/F03/F04. Compiling it right after the F01 core and *before* the `Check`/`CheckRule` rule
  stack documents that "parallel after F01" relationship in the build order itself, without
  implying any dependency on the rule stack (`Check.*`/`CheckRule.*` continue to compile after
  it and do not reference it). Keeping it **in the kernel** (roadmap §3) is what lets every
  adapter (F09–F11) — notably the F10 self-dogfood adapter, whose `TaskDependsOn` graph runs
  through this very model — reuse it with **zero new dependencies**.
- **Alternatives considered**: a separate `FS.GG.Governance.Evidence` namespace/assembly —
  rejected (a new project for a small value algebra is against Principle III / SC-009 and
  breaks the one-namespace convention F01–F04 established). Compiling it *last* (after
  `CheckRule.*`) — works (it depends on nothing later), but misrepresents the dependency story
  by suggesting it sits atop the rule stack; placing it adjacent to F01 is the honest order.
  Compiling it *first* (before `Verdict.*`) — technically possible since it is self-contained,
  but "after the F01 core" reads as "builds on F01", which is the roadmap's framing.

## D2 — Node identity: generic `'id : comparison`

- **Decision**: Node identity is a **generic type parameter** `'id` with a `comparison`
  constraint: `EvidenceGraph<'id when 'id : comparison>`, and every `Evidence` function is
  generic over `'id`. It is NOT the F01 `FactId`.
- **Rationale**: FR-012 states the requirement twice over — "node identity MUST be generic
  (parameterised, carrying no domain vocabulary)" — and a generic `'id` is the only thing that
  satisfies it: the F01 `FactId` is *not* parameterised (it is a fixed single-case `string`
  wrapper) and it names a *fact*, whereas an evidence node is a *piece of work or a claim*, so
  reusing `FactId` would both fail the "parameterised" clause and inject fact-vocabulary into a
  work/claim identity (the wrong coupling). Genericity is exactly what the F10 dogfood adapter
  needs (it supplies its own task-id type) and what makes the model domain-neutral across
  software, research, and writing (US4). The `comparison` constraint is the standard,
  no-cost requirement for using `'id` as a `Map`/`Set` key, which both the closure's result
  map and the cycle/visited-set detection rely on — and it is satisfied by every ordinary id
  type (`string`, `int`, single-case DUs, records of comparable fields).
- **On FR-012's "reuse the in-assembly F01 kernel types"**: this is satisfied by living in the
  **same `FS.GG.Governance.Kernel` assembly and namespace** as F01 and **reusing its
  evaluation discipline** — the same monotone, deterministic least-fixed-point contract F01's
  `FixedPoint.evaluate` established (D4) — rather than spinning up a parallel framework or a
  new dependency. The clause is about not reinventing the kernel's scaffolding, not about
  forcing a non-parameterised identity; the explicit "generic (parameterised)" clause governs
  identity and wins.
- **Alternatives considered**: node identity = `FactId` — rejected (violates the parameterised
  clause; wrong vocabulary; would force adapters to stringify their ids). An `interface`/SRTP
  identity abstraction — rejected (over-engineering; `comparison` is the idiomatic F# answer,
  Principle III).

## D3 — `EvidenceGraph<'id>` is ABSTRACT; `build` is the only constructor

- **Decision**: `EvidenceGraph<'id>` is declared **abstract** in the `.fsi` (a `[<Sealed>]`
  type with no visible representation and no public constructor). The only way to obtain one
  is `Evidence.build`, which returns `Result<EvidenceGraph<'id>, GraphError<'id>>`. Inspection
  is via the `nodes` and `dependencies` accessors. Internally the `.fs` represents it as a
  record of `{ Nodes: Map<'id, EvidenceState>; Deps: Map<'id, Set<'id>> }` (or equivalent) —
  an implementation detail hidden by the signature.
- **Rationale**: The DAG invariant (FR-004) and the "no `AutoSynthetic` declared" invariant
  (FR-002) are what make `effective` a **terminating, total** least-fixed-point. If the graph
  were a public record, a caller could hand-build a cyclic or `AutoSynthetic`-declaring value
  and `effective` would either loop or be partial — defeating FR-011. Making the type abstract
  renders an invalid graph **unrepresentable**: validation happens once, at the single
  `build` gate, exactly as F04 put its reified-ness guardrail in the `rule` constructor rather
  than in `toRule`. This is the "make illegal states unrepresentable" discipline applied to a
  structural invariant the type system cannot otherwise express. Adjacency stored as
  `Map<'id, Set<'id>>` makes the closure's per-node dependency lookup O(log V) and dedups
  parallel edges for free.
- **Alternatives considered**: a public `EvidenceGraph` record validated only by convention —
  rejected (the invariant would be unenforceable; `effective` could not promise totality). A
  `validate : EvidenceGraph -> Result<…>` that callers must remember to run — rejected (an
  unvalidated graph would still typecheck into `effective`; the abstract type makes validation
  unskippable). Returning the graph *and* a separate "is it acyclic" flag — rejected (a graph
  that exists but might be cyclic is exactly the unsafe state the abstract type forbids).

## D4 — `effective`: the taint closure as a memoized least-fixed-point

- **Decision**: `effective : EvidenceGraph<'id> -> Map<'id, EvidenceState>` computes, for
  every node, the documented `effective(t)` rule (kernel.md), implemented as a **memoized
  depth-first fold** over the DAG:
  - declared `Synthetic` ⇒ `Synthetic` (root cause; never re-labelled — FR-008);
  - declared `Real` ⇒ `AutoSynthetic` if ANY dependency's *effective* state ∈ {`Synthetic`,
    `AutoSynthetic`}, else `Real` (FR-005/006);
  - any other declared state (`Pending`/`Failed`/`Skipped`) ⇒ itself, unchanged, regardless of
    dependencies (FR-007).
  A per-call memo table (a `Dictionary<'id, EvidenceState>` or threaded `Map`) caches each
  node's effective state so each node and edge is visited once (O(V + E)); the recursion is a
  `let rec` graph walk (Principle III endorses `let rec` for graph walks). Because `build`
  guarantees acyclicity, the DFS always terminates and never needs cycle-guarding inside
  `effective` itself.
- **Rationale**: This is the **least-fixed-point** the spec insists on ("ordinary
  least-fixed-point dataflow, not a bespoke rules engine") — the same monotone closure shape as
  F01's `FixedPoint.evaluate`, specialised to the taint lattice. Determinism (FR-010, SC-004)
  is immediate: the result is a pure function of the declared-state map and the edge set, with
  no dependence on node or edge *order* (a `Map`/`Set` representation is itself order-free, and
  DFS memoization visits each node exactly once with an order-independent combine — "any
  dependency tainted"). **Auto-clear (FR-009, SC-003) falls out for free**: `effective` carries
  no state between calls, so re-declaring a `Synthetic` root as `Real` and recomputing yields a
  graph in which the former descendants have no synthetic ancestor and are reported `Real`
  again — no bookkeeping. **Transitivity to full depth (FR-006, SC-002)** and **diamond
  idempotence** are inherent in the closure ("any dependency tainted" is a set membership, not
  a count). **Totality (FR-011, SC-008)**: every node has a declared state (`build` rejected
  undeclared endpoints), the empty graph yields the empty map, and the DAG guarantees the walk
  finishes — `effective` never throws or returns a partial.
- **Alternatives considered**: implement `effective` by literally **reusing
  `FixedPoint.evaluate`** — model each node as a `'fact`, the taint rule as a monotone `Rule`,
  and let the F01 evaluator compute the closure. *Considered and rejected*: `FixedPoint.evaluate`
  keys facts by `FactId` (a `string`), so it would force a `'id → string` projection that
  either constrains `'id` beyond `comparison` or risks identity collisions, and it adds a layer
  of fact/rule plumbing without improving totality — the direct memoized closure IS the same
  least-fixed-point, simpler to read, and trivially total over a DAG (Principle III). The
  reuse remains *conceptually* exact (D2's discipline note), just not literal. Returning a
  sorted `('id * EvidenceState) list` instead of a `Map` — rejected: a `Map` is the canonical,
  order-free result a consumer (F06) indexes by id, and is the natural determinism witness.
  Propagating taint from `Failed`/`Skipped` as well — rejected: the spec is explicit that ONLY
  `Real` nodes are upgraded and ONLY a synthetic ancestor taints (FR-007, FR-005); extending it
  would be a spec change.

## D5 — `build` validation: order, `GraphError`, and the three refusals

- **Decision**: `build : nodes:('id * EvidenceState) list -> dependencies:('id * 'id) list ->
  Result<EvidenceGraph<'id>, GraphError<'id>>` validates and returns the **first** violation in
  this precedence, via `GraphError<'id> = Cycle of 'id list | UnknownNode of 'id |
  AutoSyntheticDeclared of 'id`:
  1. **`AutoSyntheticDeclared id`** — any node whose declared state is `AutoSynthetic` (FR-002,
     SC-006): `AutoSynthetic` is computed-only, so a graph can never be *authored* with it.
  2. **`UnknownNode id`** — any dependency edge endpoint (either side of an `(a, b)`) that is
     not present among the declared nodes: rejected so every node `effective` folds has a
     declared state (totality, FR-011).
  3. **`Cycle path`** — any self-dependency (`(a, a)`) or multi-node loop, detected by a DFS
     over the dependency edges; `path` witnesses one such cycle (FR-004, SC-005, decision #4).
  A node id repeated in `nodes` keeps its **last** declaration (the nodes form a `Map`); this
  is the least-surprising total resolution and matches `Map.ofList`.
- **Rationale**: Three refusals are exactly the validity guarantees that make the abstract
  graph safe (D3): no computed-only declaration (FR-002), no dangling dependency (totality),
  no cycle (FR-004). Carrying the offending node / cycle witness in the error makes the failure
  **actionable** (Principle VI: distinguish a real defect from malformed input, with context),
  and is what SC-005's "rejected" tests assert against. `Result` (not `option` or an
  exception) is the honest total encoding (FR-011) and reads cleanly with `Result.map`/`bind`,
  needing no computation expression (Principle III). The fixed precedence makes `build`
  deterministic about *which* error it reports. **`UnknownNode` is included beyond the spec's
  letter** (the spec mandates only cycle rejection): a dependency to an undeclared node would
  otherwise leave `effective` without a declared state for that node, so rejecting it is the
  cheapest way to keep `effective` total — an honest engineering addition, documented here, not
  a behaviour the spec forbids.
- **Alternatives considered**: silently treating an undeclared endpoint as an implicit
  `Pending` node — rejected (it invents a node the consumer never declared; explicit rejection
  is more honest, Principle VI). Rejecting duplicate node ids as a fourth `GraphError` —
  rejected as surface bloat; last-wins via `Map` is total and unsurprising, and a consumer that
  cares can de-dup before calling. Enforcing FR-002 with a *separate* 5-case "declared state"
  type (so `AutoSynthetic` is unrepresentable as input) — rejected: FR-001 and the roadmap fix
  **one** six-case `EvidenceState`, so the single-type-plus-`build`-refusal is the faithful
  encoding (and gives SC-006 a concrete tested behaviour).

## D6 — Test approach

- **Decision**: One new `EvidenceTests.fs` reusing **Expecto + FsCheck** (F01 D5). A concrete
  node-id type in tests (e.g. `string` or a small `int`) supplies real graphs — the inputs ARE
  real declared-state graphs, so no synthetic fixtures and no `// SYNTHETIC:` disclosure are
  needed (Principle V). Headline checks map to the validation scenarios (V21–V29,
  [quickstart.md](./quickstart.md)): transitive propagation incl. the no-synthetic-anywhere
  case (V21); **FsCheck** chain-depth property — a chain of N `Real` nodes on one `Synthetic`
  root ⇒ all N `AutoSynthetic`, for arbitrary N (V22, SC-002); auto-clear on `Synthetic → Real`
  and the two-root partial-clear case (V23, SC-003); diamond idempotence (V24, SC-001);
  **FsCheck** determinism property — shuffling the `nodes` and `dependencies` lists of the same
  graph yields an identical `effective` map (V25, SC-004); cycle rejection — self and
  multi-node — plus the `AutoSyntheticDeclared` and `UnknownNode` refusals (V26, SC-005/SC-006);
  real-only inertness — `Pending`/`Failed`/`Skipped` on a synthetic dependency unchanged, and a
  declared `Synthetic` reported `Synthetic` not `AutoSynthetic` (V27, SC-006/SC-007); totality —
  the empty graph yields the empty map, a `Real` node with no deps is `Real`, nothing throws
  (V28, SC-008); plus a domain-neutral example (a research finding on simulated data ⇒
  `AutoSynthetic`, US4). Compile order in `.fsproj`: `Evidence.fsi`/`Evidence.fs` **after**
  `Kernel.*`; `EvidenceTests.fs` before `Main.fs`. The reflective V11 surface-drift test extends
  to the `Evidence` surface once re-blessed; V12 re-confirms BCL/FSharp.Core-only (no `System.*`
  at all) — **no new drift/hygiene test needed**.
- **Rationale**: Real `EvidenceGraph` values exercise the public surface a downstream adapter
  (F10) and F06 would use (Principle I/V); FsCheck is the natural tool for the *properties*
  SC-002 (arbitrary depth) and SC-004 (order-independence) are built around — both quantify
  over a space too large for example tests. Auto-clear is proven by *recomputation* over a
  re-declared graph (the spec's own independent test), which is exactly real evidence.
- **Alternatives considered**: hand-rolled permutation loops instead of FsCheck for SC-004 —
  clumsier and less thorough; rejected (mirrors F02 D5 / F03 D7 / F04 D7). Testing `effective`
  only through the F10 adapter end-to-end — deferred to F10; F05's tests target the kernel
  surface directly so the model is proven independent of any adapter.

## Deferred / out of scope (confirmed, not unknowns)

- **Reading a node's true declared state from real artifacts** (filesystem/git/test output),
  the **disclosure logging** around a bypass, and the rule that a **bypass flag never changes a
  verdict** — **F08** (the MVU/effects edge) and **F07** (routing). F05 is a pure derivation
  over *declared* states (FR-013); it discovers nothing and logs nothing.
- **Evidence-freshness predicates, JSON serialization of evidence/explanations, and rendering
  effective states** for human/agent consumption — **F06** (explanation output). F05 only
  computes the effective taint that F06 will render and that routing's merge fence
  (`evidenceNotSynthetic`) will gate on.
- **Combining a verdict (F02) with an evidence state** (e.g. "passed but on synthetic evidence
  is a blocker") — a **routing** concern (F07/F10), orthogonal to F05. The two dimensions are
  defined separately; F05 defines only the evidence dimension.
- **Structured logging** (`TODO(STRUCTURED_LOGGING)`) — no I/O in F05; the model emits nothing
  to a log. Choice still deferred to an ADR before F08.
