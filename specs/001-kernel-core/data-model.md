# Phase 1 Data Model: Kernel Core (F01 · `001-kernel-core`)

The full typed shapes are the public contract in
[`contracts/Kernel.fsi`](./contracts/Kernel.fsi). This document records each
entity's meaning, fields, invariants, and the validation/derivation rules that the
implementation and semantic tests must uphold. Entities map directly to the spec's
Key Entities and the design model in `docs/governance-design/kernel.md`.

## Entities

### `FactId` (`FactId of string`)
- **Role**: stable handle for a fact; the unit of deduplication and the way inputs
  are named in provenance (spec: *Fact identity*).
- **Source**: produced *only* by the caller's `identify : 'fact -> FactId` (D3).
- **Invariant**: within any returned `FactSet`, each `FactId` appears **at most
  once** (FR-007). Equality is ordinary structural string equality.

### `RuleId` (`RuleId of string`)
- **Role**: identity of a rule, recorded in every `ProvenanceStep` it creates.
- **Invariant**: treated as opaque by the kernel; used for the provenance tie-break
  ordering (D2). Uniqueness across a rule set is a caller responsibility (not
  enforced), but non-unique ids only affect which equal-id step "wins" a tie.

### `ProvenanceStep`
| Field | Type | Meaning |
|------|------|---------|
| `Rule` | `RuleId` | the rule that fired to produce the fact |
| `Inputs` | `FactId list` | the ids of the facts that rule consumed |
| `Note` | `string` | short human-/agent-readable description of the inference |
- **Role**: one justification step (spec: *Justification (provenance)*), FR-004.
- **Invariant**: every `Inputs` id refers to a fact present in the result, so an
  auditor can reconstruct the chain back to asserted facts with no gaps (SC-002).

### `FactAssertion<'fact>`
| Field | Type | Meaning |
|------|------|---------|
| `Id` | `FactId` | canonical identity, assigned by the kernel via `identify` (D3) |
| `Value` | `'fact` | the opaque, domain-supplied value; never inspected by the kernel (FR-009) |
| `Provenance` | `ProvenanceStep list` | justification; **empty** ⇔ supplied/asserted (FR-005) |
- **Role**: a fact plus its identity and justification.
- **Invariants**:
  - Supplied facts ⇒ `Provenance = []` (FR-005); derived facts ⇒ `Provenance` has
    ≥1 step (FR-004).
  - The kernel canonicalizes `Id := identify Value` on entry, so a caller- or
    rule-supplied `Id` that disagrees with `identify` is overridden (D3). `'fact` is
    never pattern-matched or branched on by the kernel (FR-009).

### `FactSet<'fact>` (`FactAssertion<'fact> list`)
- **Role**: the working/returned set of facts.
- **Invariants**: deduplicated by `FactId` (FR-007); in any value the kernel
  returns, ordered canonically by `FactId` for byte-for-byte reproducibility
  (SC-001). The *input* `supplied` set need not be sorted or deduplicated — the
  kernel normalizes it.

### `Rule<'fact>`
| Field | Type | Meaning |
|------|------|---------|
| `Id` | `RuleId` | the rule's identity |
| `Description` | `string` | what the rule infers (for `Note`/diagnostics) |
| `Apply` | `FactSet<'fact> -> FactAssertion<'fact> list` | maps the current facts to newly asserted facts |
- **Role**: a named, add-only unit of inference (spec: *Rule*).
- **Precondition (documented, not enforced — FR-012)**: `Apply` is **monotonic** —
  given a larger input set it never retracts a previously producible fact. Negated,
  aggregated, or recursively-negated facts are *supplied* from a lower stratum, not
  derived here.
- **Contract for a well-behaved rule**: each returned assertion should carry a
  `ProvenanceStep` naming `Id = this rule` and `Inputs =` the ids it consumed; the
  kernel records that step (subject to the D2 tie-break) and (re)assigns the fact's
  own `Id` via `identify`.

### `EvaluationResult<'fact>`
| Field | Type | Meaning |
|------|------|---------|
| `Facts` | `FactSet<'fact>` | supplied + derived, deduplicated, sorted by `FactId` |
| `Rounds` | `int` | count of rounds that committed ≥1 new fact before quiescence (D4) |
- **Role**: the outcome of running the engine (spec: *Evaluation result*).
- **Invariant**: `Rounds ≥ 0`; `Rounds = 0` ⇔ nothing was derived.

## Derivation / evaluation rules (behavioral contract)

1. **Normalize supplied** (FR-001, FR-007): assign `Id := identify Value` to each
   supplied fact, force `Provenance := []`, dedup by `FactId` (first occurrence
   wins). This seeds the known set.
2. **Synchronous round** (D1, FR-002): apply *every* rule's `Apply` to the *same*
   immutable snapshot of the current known set. Collect all produced assertions.
3. **Canonicalize & select** (D2, D3): for each produced assertion compute
   `Id := identify Value`; discard any whose `Id` is already known. Among remaining
   candidates that are *new this round*, group by `FactId` and keep the single step
   chosen by the total order on `(FactId, RuleId)`.
4. **Commit**: add the new facts (with their selected provenance) to the known set.
   If ≥1 was added, increment `Rounds` and go to (2); otherwise stop (quiescence,
   FR-003).
5. **Emit** (SC-001): return `Facts` sorted by `FactId`, and `Rounds`.

## State transitions

A single `FactId`'s lifecycle within one `evaluate` call:

```text
            supplied                         derived (rule fires, id is new)
absent ───────────────► known(Provenance=[]) ▲
   │                                          │
   └──────────────────────────────────────────┘
                derived (rule fires, id is new) ──► known(Provenance=[step])

known(...) ──re-derived / re-asserted (same id)──► known(...)   // no change, no loop
```

Once a `FactId` is known it never changes its recorded value or provenance for the
rest of the run (monotonic; re-derivation is a no-op — edge cases "duplicate
assertions", "re-derivation of an existing fact", "self-referential chains").

## Edge-case mapping (from spec)

| Edge case | Behavior |
|-----------|----------|
| No rules | result = normalized supplied; `Rounds = 0`; no error |
| No supplied, rules need inputs | result = empty `Facts`; `Rounds = 0`; no error |
| Duplicate assertions (same id) | collapse to one entry (step 1) |
| Re-derivation of a known fact | recognized as known; adds nothing; no loop |
| Self-referential monotone chain | terminates (re-derive adds nothing) |
| Non-monotonic / aggregated / negated | **precondition**, not handled at runtime; supply such facts as ordinary asserted facts (FR-012) |
