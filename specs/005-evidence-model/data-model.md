# Phase 1 Data Model: Evidence Model & Synthetic Taint (F05 · `005-evidence-model`)

The full typed shapes are the public contract in
[`contracts/Evidence.fsi`](./contracts/Evidence.fsi). This document records each entity's
meaning, the `build`/`effective` rules, and the invariants the implementation and semantic
tests must uphold. Entities map directly to the spec's Key Entities and
`docs/governance-design/kernel.md` ("The evidence model").

## Entities

### `EvidenceState` — `Pending | Real | Synthetic | Failed | Skipped | AutoSynthetic`
The quality of the evidence behind a node — the dimension tracked orthogonally to a verdict
(FR-001). EXACTLY six cases. Five are **authored** (`Pending`/`Real`/`Synthetic`/`Failed`/
`Skipped`); `AutoSynthetic` is **computed-only** — produced solely by `effective`, never a
valid declared input (FR-002). `Synthetic` is a taint's declared **root cause**; `AutoSynthetic`
is its **inherited** form, so the two stay distinguishable (FR-008).

### `GraphError<'id>` — `Cycle of 'id list | UnknownNode of 'id | AutoSyntheticDeclared of 'id`
Why `build` refused a graph — the validity guarantees that keep `effective` a terminating,
total least-fixed-point. `Cycle` carries a witnessing loop (FR-004); `UnknownNode` names a
dependency endpoint absent from `nodes` (totality); `AutoSyntheticDeclared` names a node
declared with the computed-only state (FR-002). `build` returns the FIRST violation in the
precedence below.

### `EvidenceGraph<'id when 'id : comparison>` — ABSTRACT
The acyclic dependency graph of evidence nodes — who rests on whom (FR-003). **Abstract**: no
public constructor, so the ONLY way to obtain one is `build`, which validates it; this makes a
cyclic or `AutoSynthetic`-declaring graph **unrepresentable** and so makes `effective` provably
total (research D3). Node identity is generic `'id` (FR-012); `comparison` is the standard
`Map`/`Set` key requirement. Inspectable via `nodes`/`dependencies`. Internal representation
(e.g. `{ Nodes: Map<'id, EvidenceState>; Deps: Map<'id, Set<'id>> }`) is a hidden detail.

### Evidence node (conceptual)
A piece of work or a claim, identified by a stable generic `'id` and carrying one **declared**
`EvidenceState`. The unit over which taint is tracked. Not a distinct type — a node is an entry
in the graph's node map.

### Effective taint closure — `effective`
The function from a graph to each node's **effective** state: the transitive least-fixed-point
that upgrades `Real` nodes resting on synthetic evidence to `AutoSynthetic` and leaves
everything else as declared (FR-005). Returns a `Map<'id, EvidenceState>` — canonical and
order-free.

## Constructor & function rules (behavioural contract)

### `build nodes dependencies : Result<EvidenceGraph<'id>, GraphError<'id>>` — FR-002/003/004
`nodes` is `('id * EvidenceState) list`; each dependency `(a, b)` means "node `a` depends on
(rests on) `b`". A repeated node id keeps its LAST declaration (nodes form a `Map`). Validation
returns the FIRST violation in this precedence:

| Order | Condition | Result |
|---|---|---|
| 1 | any node declared `AutoSynthetic` | `Error (AutoSyntheticDeclared id)` (FR-002) |
| 2 | any dependency endpoint not in `nodes` | `Error (UnknownNode id)` (totality) |
| 3 | any self-dependency or multi-node cycle | `Error (Cycle path)` (FR-004, decision #4) |
| — | otherwise | `Ok graph` |

Total: every input yields `Ok` or exactly one `Error` (FR-011).

### `nodes graph : ('id * EvidenceState) list` / `dependencies graph : ('id * 'id) list`
Inspection accessors for the abstract graph. `nodes` returns the declared (id, state) pairs
(de-duplicated by id); `dependencies` returns the directed edges (de-duplicated). Both ordered
deterministically by id so the accessors are themselves order-free.

### `effective graph : Map<'id, EvidenceState>` — FR-005 … FR-011
For every node `t`, computed by the documented rule (kernel.md `effective(t)`), as a memoized
DFS over the DAG:

| Declared state of `t` | Effective state |
|---|---|
| `Synthetic` | `Synthetic` (root cause — never `AutoSynthetic`, FR-008) |
| `Real`, AND some dependency's *effective* state ∈ {`Synthetic`, `AutoSynthetic`} | `AutoSynthetic` (FR-005/006) |
| `Real`, AND no dependency is effectively (auto)synthetic | `Real` |
| `Pending` / `Failed` / `Skipped` | unchanged — never tainted (FR-007) |

The recursion terminates because `build` guarantees a DAG; memoization makes it O(V + E) and
order-independent. The result is a pure function of the declared states + edges (no hidden
history), so recomputing after a declaration change is the only action needed to clear taint
(FR-009/010).

## Invariants checked by semantic tests

(INV-11 is a structural property, not a behavioural assertion — it is verified by manual/code
review at T018, not by a V-scenario; every other invariant below is pinned by a V-scenario.)

| # | Invariant | Spec ref |
|---|-----------|----------|
| INV-1 | A `Real` node with any transitive `Synthetic` ancestor ⇒ `AutoSynthetic`; a graph with no `Synthetic` anywhere ⇒ every effective state equals its declared state | FR-005/006, SC-001 |
| INV-2 | In a chain of N `Real` nodes rooted at one `Synthetic` node, all N are `AutoSynthetic`, for arbitrary N (full transitive depth) | FR-006, SC-002 |
| INV-3 | Re-declaring the sole `Synthetic` root as `Real` and recomputing ⇒ every formerly-tainted node is `Real`; with two synthetic roots, upgrading one clears only the nodes that rested solely on it | FR-009, SC-003 |
| INV-4 | A node reachable to a synthetic root by multiple paths (diamond) is `AutoSynthetic` once — the closure is idempotent and order-independent | FR-005, SC-001 |
| INV-5 | `effective` is deterministic — permuting `nodes` and/or `dependencies` of the same graph yields an identical result map | FR-010, SC-004 |
| INV-6 | `build` rejects a self-dependency and a multi-node cycle (`Cycle`); an acyclic set is accepted | FR-004, SC-005 |
| INV-7 | `build` rejects a node declared `AutoSynthetic` (`AutoSyntheticDeclared`); a declared `Synthetic` node that also depends on synthetic nodes is reported `Synthetic`, not `AutoSynthetic` | FR-002/008, SC-006 |
| INV-8 | `Pending`/`Failed`/`Skipped` nodes depending on a synthetic node retain their declared state — never upgraded | FR-007, SC-007 |
| INV-9 | `effective` is total — the empty graph yields the empty map; a `Real` node with no deps is `Real`; no valid graph throws or returns a partial | FR-011, SC-008 |
| INV-10 | `build` rejects a dependency edge naming an undeclared node (`UnknownNode`) so `effective` always has a declared state for every node | FR-011 (totality) |
| INV-11 | The model performs no I/O and reads no real artifacts — `effective`/`build` are pure functions of their arguments (**structural — verified by manual/code review at T018, not a V-scenario**) | FR-013 |
| INV-12 | Domain-neutral: with `'id` instantiated to a non-software identity, a `Real` "finding" resting on a `Synthetic` "simulated data" node is `AutoSynthetic` | FR-012, US4 AS3 |
| INV-13 | The `nodes`/`dependencies` accessors are order-free and history-free — a duplicate id keeps its last declaration, duplicate edges collapse, and both outputs are ordered by id regardless of input order | FR-003 |

## Edge-case mapping (from spec)

| Edge case | Behaviour |
|-----------|-----------|
| Empty graph | `build [] []` ⇒ `Ok` of an empty graph; `effective` ⇒ empty `Map` (INV-9) |
| `Real` node with no dependencies | reported `Real` — no synthetic ancestor to taint it (INV-9) |
| `AutoSynthetic` is computed-only | declaring it is refused at construction (`AutoSyntheticDeclared`, INV-7) |
| Synthetic outranks inherited taint | a node both declared `Synthetic` and resting on synthetic deps ⇒ `Synthetic`, not `AutoSynthetic` (INV-7) |
| Non-real states inert to taint | `Pending`/`Failed`/`Skipped` on synthetic evidence keep their declared state (INV-8) |
| Diamond / shared ancestors | tainted once; closure idempotent and order-independent (INV-4) |
| Long chains terminate | DAG ⇒ closure terminates, deterministic least-fixed-point (INV-2, INV-5) |
| Self-dependency / multi-node cycle | refused by `build` with `Cycle` (INV-6) |
| Undeclared dependency endpoint | refused by `build` with `UnknownNode` (INV-10) |
| Disclosure / bypass | OUT OF SCOPE — routing/edge concern (F07/F08); F05 computes only the honest effective state |
