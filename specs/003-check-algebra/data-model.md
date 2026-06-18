# Phase 1 Data Model: Check — The Reified Rule Algebra (F03 · `003-check-algebra`)

The full typed shapes are the public contract in
[`contracts/Check.fsi`](./contracts/Check.fsi). This document records each entity's
meaning, the per-interpreter fold rules, the hash canonicalization, the explanation
shape, and the invariants the implementation and semantic tests must uphold. Entities
map directly to the spec's Key Entities and `docs/governance-design/rule-edsl.md`.

## Entities

### `ArtifactRef` — `{ Kind: string; Key: string }`
The single domain-specific touch-point: a structural handle naming a governed artifact
by a `Kind` and a `Key`. An adapter maps its own closed artifact union onto this; the
algebra itself stays domain-vocabulary-free (FR-001, FR-015). Renderable and hashable;
ordinary structural equality.

### `Outcome` — `Met | Unmet of string | Unknown of string`
The three-valued result a probe reports for one atomic check (FR-002). Maps **one-to-one**
onto the F02 `Verdict`:

| Outcome | Verdict | Meaning |
|---|---|---|
| `Met` | `Pass` | the atomic check holds; no reason |
| `Unmet r` | `Fail r` | the atomic check does not hold, with opaque reason `r` |
| `Unknown r` | `Uncertain r` | a competent judge has not yet ruled, with reason `r` |

Reasons are **opaque** — the algebra never inspects their meaning (FR-015); they flow
into the F02 reason aggregation unchanged.

### `ProbeArg` — `ArtifactArg of ArtifactRef | LiteralArg of string | NumberArg of double`
A declared, renderable/hashable parameter of a probe, expressed as data so it appears in
the rendered contract and the cache key. The `Args` list is **ordered** — meaning depends
on order (FR-008).

### `Probe<'fact>` — `{ Name; Reads: ArtifactRef list; Args: ProbeArg list; Eval: FactSet<'fact> -> Outcome }`
The only part an adapter supplies. Its **declared shape** (`Name`, `Args`, `Reads`) is
what `render`/`hash`/`reads` inspect; its `Eval` is only ever **run** by `eval`/`explain`,
never rendered or hashed (FR-003). This separation is the inspectable-without-execution
guarantee at the leaf level.

### `Check<'fact>` — `Atom | All | Any | Not | Implies | Opaque`
The closed, reified combinator algebra — one value foldable six ways. **Applicative, not
monadic**: no `bind`, no data-dependent sequencing (FR-012), so structure is fixed a
priori and the non-evaluating interpreters can fold without running probes. Closed by
design (third parties add probes, not cases).

### `Explanation` (non-generic) — the proof tree
Mirrors the check's **surface** structure; each node carries its rolled-up `Verdict`, and
atom/opaque nodes also record the `Outcome` observed:
`AtomExplained (name, Outcome, Verdict)`, `AllExplained (Explanation list, Verdict)`,
`AnyExplained (…)`, `NotExplained (Explanation, Verdict)`,
`ImpliesExplained (antecedent, consequent, Verdict)`, `OpaqueExplained (name, Outcome,
Verdict)`. `Explanation.verdict` reads the root verdict. Serialization is F06.

## Interpreter fold rules (behavioural contract)

### `eval : FactSet<'fact> -> Check<'fact> -> Verdict` — FR-006
Reuses the **F02 verdict combinators** (no new truth tables):

| Node | Verdict |
|---|---|
| `Atom p` | `outcomeToVerdict (p.Eval facts)` |
| `All cs` | `Verdict.all (List.map (eval facts) cs)` |
| `Any cs` | `Verdict.any (List.map (eval facts) cs)` |
| `Not c` | `Verdict.negate (eval facts c)` |
| `Implies (a, b)` | `eval facts (Any [Not a; b])` (desugared — "either not a, or b") |
| `Opaque (_, f)` | `outcomeToVerdict (f facts)` |

where `outcomeToVerdict = Met → Pass | Unmet r → Fail r | Unknown r → Uncertain r`.
Empty `All [] = Pass`, empty `Any [] = Fail ""` (inherited from F02). `Uncertain` is
preserved unless a dominating result is present (SC-003).

### `render : Check<'fact> -> string` — FR-007 (execution-free)
Structure only; never calls `Eval`, never needs facts:

| Node | Rendering |
|---|---|
| `Atom p` | `p.Name` + `(arg₁, arg₂, …)` when `Args` non-empty (args rendered by kind) |
| `All cs` | `all of [r₁; r₂; …]` |
| `Any cs` | `any of [r₁; r₂; …]` |
| `Not c` | `not (r)` |
| `Implies (a, b)` | `(rₐ) implies (r_b)` (positional) |
| `Opaque (n, _)` | `opaque "n"` |

Deterministic (same value → same string). Authoring order preserved (render does **not**
canonicalize commutative order; only `hash` does). The exact wording is an engineering
detail (research D5); the contract is determinism + execution-freedom.

### `hash : Check<'fact> -> string` — FR-008 (execution-free; Hazard 3)
SHA-256 hex digest folded over structure, computed **without** calling `Eval`. Components
are combined **prefix-free** (each leaf component is hashed to fixed-width hex first):

| Node | Hash pre-image | Order treatment |
|---|---|---|
| `Atom p` | `"atom"`, `Name`, `Args` (in order), `Reads` (in order) | **positional** |
| `All cs` | `"all"`, child hashes **ordinal-sorted** | **canonicalized** (commutative) |
| `Any cs` | `"any"`, child hashes **ordinal-sorted** | **canonicalized** (commutative) |
| `Not c` | `"not"`, `hash c` | — |
| `Implies (a, b)` | `"implies"`, `hash a`, `hash b` | **positional** |
| `Opaque (n, _)` | `"opaque"`, `n` only | name only (function never hashed) |

Ordinal sort (`String.CompareOrdinal`, culture-invariant — same discipline as F02 reason
aggregation) gives byte-for-byte identical keys across machines/locales (SC-002).

### `explain : FactSet<'fact> -> Check<'fact> -> Explanation` — FR-009
Mirrors surface structure; each node's verdict computed with the same F02 combinators as
`eval`, so the root verdict equals `eval` (SC-004). For `Implies (a, b)`: the node holds
`explain a` and `explain b` (each evaluated normally), and its verdict is
`Verdict.any [Verdict.negate (Explanation.verdict aExpl); Explanation.verdict bExpl]`.
Atom/Opaque nodes record the observed `Outcome`.

### `reads : Check<'fact> -> ArtifactRef list` — FR-010
Left-to-right structural walk collecting every `Atom`'s `Reads`; `Opaque` contributes
none. Returns the declared multiset faithfully (dedup is the F04 cache-key policy, not
this fold).

### `isReified : Check<'fact> -> bool` — FR-011
`true` iff no `Opaque` node appears anywhere; `false` if at least one does (SC-005).

## Invariants checked by semantic tests

| # | Invariant | Spec ref |
|---|-----------|----------|
| INV-1 | `render`/`hash`/`reads`/`isReified` never invoke any probe `Eval` and need no facts (a throwing-`Eval` probe still renders/hashes) | FR-007/008, SC-001 |
| INV-2 | `eval` matches the F02 Kleene composition of sub-results; `Uncertain` preserved unless dominated | FR-006/014, SC-003 |
| INV-3 | `hash (All xs) = hash (All (permute xs))` and likewise `Any` (commutative canonicalization) | FR-008, SC-002 |
| INV-4 | `hash (a ==> b) ≠ hash (b ==> a)` (a≠b), and a probe's reordered `Args` change the hash (positional) | FR-008, SC-002 |
| INV-5 | re-hashing an identical check yields an identical key; `Opaque` hashes by name only | FR-008 |
| INV-6 | `Explanation.verdict (explain f c) = eval f c` for every check and facts | FR-009, SC-004 |
| INV-7 | `explain` structure mirrors the check; each atom records its met/unmet/unknown outcome | FR-009 |
| INV-8 | `isReified c = false` ⟺ `c` contains an `Opaque` node | FR-011, SC-005 |
| INV-9 | `reads c` = exactly the `ArtifactRef`s the atoms declare; `Opaque` adds none | FR-010 |
| INV-10 | every interpreter is **total** — no check (incl. empty `All`/`Any`, any mix) throws or returns a partial | FR-013, SC-006 |
| INV-11 | `eval` order-independent for commutative nodes (verdict AND reason), inherited from F02 | FR-014 |

## Edge-case mapping (from spec)

| Edge case | Behaviour |
|-----------|-----------|
| Empty `All []` | `eval → Pass`; renders/hashes totally | 
| Empty `Any []` | `eval → Fail ""`; renders/hashes totally |
| Undecided propagation | `Unknown` atom ⇒ `Uncertain` verdict unless dominated (Fail in `All`, Pass in `Any`) — never coerced |
| Negation of undecided | `Not` over an `Uncertain` sub-verdict stays `Uncertain` (reuses F02 `negate`) |
| `Implies` desugared | verdict & explanation follow `Any [Not a; b]`; hash/render keep the two sides positional |
| Render/hash never execute | a probe whose `Eval` throws still renders and hashes cleanly (INV-1) |
| `Opaque` node | renders/hashes by name; contributes no reads; `isReified → false`; evaluates by running its function |
| Duplicate members in a commutative node | hash deterministic regardless of count/position (canonicalization) |
| Reason aggregation under commutative nodes | combined reason order-independent (inherited from F02) |
