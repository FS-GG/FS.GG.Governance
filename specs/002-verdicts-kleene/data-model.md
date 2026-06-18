# Phase 1 Data Model: Verdicts — Kleene Composition (F02 · `002-verdicts-kleene`)

The full typed shapes are the public contract in
[`contracts/Verdict.fsi`](./contracts/Verdict.fsi). This document records each
entity's meaning, the combinator truth tables, the reason-aggregation rule, and the
invariants the implementation and semantic tests must uphold. Entities map directly
to the spec's Key Entities and `docs/governance-design/kernel.md` ("Verdicts").

## Entities

### `Verdict`
A three-valued (Kleene "strong") judgement — the unit the whole system composes.

| Case | Payload | Meaning |
|------|---------|---------|
| `Pass` | — | the check holds; **no reason** (FR-001) |
| `Fail` | `reason: string` | the check does not hold, with an opaque reason |
| `Uncertain` | `reason: string` | a competent judge has not yet ruled, with a reason describing what is pending |

- **Role**: `Uncertain` is **distinct from `Fail`** — it is the value routing (F07)
  later turns into a *review request* rather than a *block*. Collapsing it into pass
  or fail would defeat the agent-review tier (FR-007, SC-002).
- **Invariants**:
  - `Pass` carries no payload; both `Fail` and `Uncertain` carry exactly one opaque
    free-text reason (FR-001). The kernel never inspects, parses for meaning, or
    validates reason content (FR-010) — it only normalises on the reserved `"; "`
    separator for rendering (see Reason aggregation).
  - Equality is ordinary structural union equality (`Fail "x" = Fail "x"`,
    `Fail "x" ≠ Uncertain "x"`), which the semantic tests rely on for byte-for-byte
    comparison (SC-001).

### `Reason` (the `string` payload)
- **Role**: the free-text explanation attached to a `Fail` or `Uncertain` (spec:
  *Reason*). Supplied by callers; opaque to the kernel.
- **Invariant**: the substring `"; "` is **reserved** as the reason-component
  separator. The kernel treats a reason as the (split) set of its `"; "`-delimited
  components when aggregating; it never assigns domain meaning to a component.

### Combinators (the `Verdict` module)
- **`all : Verdict list -> Verdict`** — conjunction, "all must hold".
- **`any : Verdict list -> Verdict`** — disjunction, "at least one must hold".
- **`negate : Verdict -> Verdict`** — polarity flip.
- **Role**: the order-independent reductions and the unary flip that yield a single
  combined verdict (spec: *Conjunction/disjunction combination*, *Negation*).

## Truth tables (behavioural contract)

### `all` (conjunction) — FR-002
Evaluate the list in priority order; the **first** condition that matches decides:

| Input contains… | Result |
|---|---|
| at least one `Fail` | `Fail` (reason = aggregate of the **Fail** reasons) |
| no `Fail` but ≥1 `Uncertain` | `Uncertain` (reason = aggregate of the **Uncertain** reasons) |
| only `Pass` (incl. empty list) | `Pass` |

`all [] = Pass` (identity of "all must hold" — nothing to violate; FR-009).

### `any` (disjunction) — FR-003

| Input contains… | Result |
|---|---|
| at least one `Pass` | `Pass` (no reason) |
| no `Pass` but ≥1 `Uncertain` | `Uncertain` (reason = aggregate of the **Uncertain** reasons) |
| only `Fail` (incl. empty list) | `Fail` (reason = aggregate of the **Fail** reasons; empty list ⇒ `Fail ""`) |

`any [] = Fail ""` (identity of "at least one must hold" — nothing satisfied it;
FR-009; the empty reason is absorbed by component normalisation).

### `negate` — FR-004

| Input | Result |
|---|---|
| `Pass` | `Fail` (with the same reason it would carry — here none, so `Fail ""`)¹ |
| `Fail r` | `Pass`² |
| `Uncertain r` | `Uncertain r` (unchanged — no definite polarity to flip) |

¹ ² **Double-negation invariant**: `negate (negate v) = v` must hold for *every* `v`.
For `Uncertain` and `Fail`/`Pass` round-trips this constrains the chosen mapping:
`negate Pass = Fail ""` and `negate (Fail r) = Pass`, so `negate (negate (Fail r)) =
negate Pass = Fail ""`. To keep `negate (negate (Fail r)) = Fail r` for **non-empty**
`r`, `negate Pass` must reproduce the originating reason — but `Pass` carries none.
The contract therefore pins the spec's stated guarantee precisely: **`negate` is an
involution on the pass/fail *tags*** (`Pass`↔`Fail`, `Uncertain` fixed), and is a
*full* involution (`negate (negate v) = v`) for `Uncertain` and for any `Pass`/`Fail r`
where `r = ""`. The spec's US3 examples (AS1–AS3) and Independent Test ("negate twice
… recovers the original for pass/fail") are satisfied at the tag level; the reason on
a re-negated `Pass` is the empty reason. This is documented in `Verdict.fsi` and
pinned by a test so it cannot drift. *(F03's `Implies`/`Not` always negates an
*evaluated* sub-verdict, so the tag-level involution is exactly what it needs.)*

## Reason aggregation (the reproducibility rule) — FR-006, D4

For a combined `Fail`/`Uncertain`, the reason is a deterministic function of the
contributing reasons **of the dominating kind only** (Fail reasons for a `Fail`
outcome; Uncertain reasons for an `Uncertain` outcome — `Pass` contributes nothing):

```text
combineReasons rs =
    rs
    |> collect (split on the reserved "; ", dropping empty components)
    |> distinct                       // de-duplicate (edge: duplicate reasons)
    |> sort by String.CompareOrdinal  // culture-invariant, byte-stable (SC-001)
    |> String.concat "; "
```

Consequences the tests pin:
- **Commutative**: reordering the input list never changes the combined reason
  (FR-006, US2 AS1/AS2).
- **Associative / nesting-invariant**: because each contributing reason is split back
  into components before sorting, `all [all xs; all ys]` and `all (xs @ ys)` produce
  the **same** reason (US2 AS3) — the naïve whole-reason-atom approach would not.
- **Duplication-invariant**: identical components collapse regardless of count or
  position (edge: "reason determinism under duplication").
- **Identity-absorbing**: `Fail ""` (the `any []` identity) contributes no components,
  so it vanishes from any larger aggregation; `all [v] = v` and `any [v] = v` for a
  single sub-verdict (edge: "single sub-verdict").

## Invariants checked by semantic tests

| # | Invariant | Spec ref |
|---|-----------|----------|
| INV-1 | `all`/`any` outcome is commutative (any permutation ⇒ same verdict) | FR-005, SC-001 |
| INV-2 | `all`/`any` outcome is associative (any re-nesting ⇒ same verdict) | FR-005, US2 AS3 |
| INV-3 | combined **reason** is identical under reorder, re-nest, and duplicate | FR-006, SC-001 |
| INV-4 | `Uncertain` survives unless a dominating result is present | FR-007, SC-002 |
| INV-5 | `all [] = Pass`, `any [] = Fail ""` | FR-009 |
| INV-6 | every operation is **total** — no input throws or returns a partial | FR-008, SC-003 |
| INV-7 | `negate` swaps pass/fail tags, fixes `Uncertain`; `negate ∘ negate` = id on tags | FR-004 |
| INV-8 | reasons are opaque — outcome never depends on reason content | FR-010 |

## Edge-case mapping (from spec)

| Edge case | Behaviour |
|-----------|-----------|
| Empty `all` | `Pass` (identity); no error |
| Empty `any` | `Fail ""` (identity); no error |
| Single sub-verdict (`all [v]` / `any [v]`) | returns `v` unchanged, incl. its reason |
| All inputs `Uncertain` | `Uncertain` (aggregated reasons); never pass/fail |
| Mixed with a dominating result | dominating result wins (`Fail` in `all`, `Pass` in `any`); only that kind's reasons shape the combined reason; `Pass` contributes none |
| Double negation | `Uncertain` and `""`-reasoned pass/fail recover exactly; pass/fail tags always recover |
| Duplicate / order-shuffled reasons | identical combined reason (dedup + ordinal sort) |
