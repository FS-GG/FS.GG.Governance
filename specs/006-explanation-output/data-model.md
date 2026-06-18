# Phase 1 — Data Model (F06 · 006-explanation-output)

The types, the JSON schema, and the derivation rules + invariants for the output layer.
Authoritative shapes live in the three contracts:
[`contracts/Freshness.fsi`](./contracts/Freshness.fsi),
[`contracts/Contract.fsi`](./contracts/Contract.fsi),
[`contracts/Json.fsi`](./contracts/Json.fsi). This document explains them; the `.fsi` is the
source of truth for the surface.

## 1. New public types

### `Freshness` (DU, 2 cases) — `Freshness.fsi`
| Case | Meaning |
|---|---|
| `Fresh` | Evidence recorded at or after the latest change to every artifact it covers. |
| `Stale` | Some covered artifact changed after the evidence was recorded. |

### `ContractEntry` (record) — `Contract.fsi`
| Field | Type | Meaning |
|---|---|---|
| `Id` | `RuleId` (F01) | The rule's identity. |
| `Severity` | `Severity` (F04) | `Advisory` / `Blocking`. |
| `Spec` | `SpecSource` (F04) | `{ Document; Section }` — the requirement it traces to. |
| `Statement` | `string` | `Check.render` of the rule's check — the **single source**, so it cannot drift (FR-006). |

The published contract is `ContractEntry list` (no wrapper type — keeps the surface minimal;
the empty contract is `[]`).

### Reused types (no new declaration)
- `Explanation`, `Outcome`, `Verdict` (F02/F03) — serialized by `Json`.
- `EvidenceState` (F05, six cases) — serialized by `Json`.
- `CheckRule<'fact>`, `Severity`, `SpecSource`, `RuleId` (F01/F04) — folded by `Contract`.

## 2. Functions (folds, all pure & total)

| Function | Signature (abridged) | Role |
|---|---|---|
| `Freshness.decide` | `'instant -> 'instant list -> Freshness` | inclusive `recorded ≥ max(covered)` |
| `Freshness.isFresh` | `'instant -> 'instant list -> bool` | boolean convenience |
| `Contract.ofRules` | `CheckRule<'fact> list -> ContractEntry list` | drift-proof fold |
| `Contract.render` | `ContractEntry list -> string` | human/agent text |
| `Json.ofExplanation` / `toExplanation` | `Explanation ⇄ string` | proof-tree JSON |
| `Json.ofContract` / `toContract` | `ContractEntry list ⇄ string` | contract JSON |
| `Json.ofEvidenceState` / `toEvidenceState` | `EvidenceState ⇄ string` | state token |
| `Json.ofEffective` / `toEffective` | `('id->string) -> Map<'id,EvidenceState> -> string` ; `string -> Map<string,EvidenceState>` | effective-state map JSON |

## 3. JSON schema (the wire shape — research D4/D5)

All objects emit their members in the **fixed order shown**; arrays preserve structural order;
the effective map's keys are **ordinal-sorted** (research D3). Emit is non-indented/compact.

### Outcome (nested object)
```
Met        → {"tag":"met"}
Unmet r    → {"tag":"unmet","reason":r}
Unknown r  → {"tag":"unknown","reason":r}
```

### Verdict (nested object)
```
Pass         → {"tag":"pass"}
Fail r       → {"tag":"fail","reason":r}
Uncertain r  → {"tag":"uncertain","reason":r}
```

### Explanation node (recursive; `verdict` present at EVERY node)
```
AtomExplained(name,outcome,verdict)      → {"kind":"atom","name":…,"outcome":<Outcome>,"verdict":<Verdict>}
OpaqueExplained(name,outcome,verdict)    → {"kind":"opaque","name":…,"outcome":<Outcome>,"verdict":<Verdict>}
AllExplained(parts,verdict)              → {"kind":"all","parts":[<node>…],"verdict":<Verdict>}
AnyExplained(parts,verdict)              → {"kind":"any","parts":[<node>…],"verdict":<Verdict>}
NotExplained(part,verdict)               → {"kind":"not","part":<node>,"verdict":<Verdict>}
ImpliesExplained(ante,cons,verdict)      → {"kind":"implies","antecedent":<node>,"consequent":<node>,"verdict":<Verdict>}
```
`atom` and `opaque` differ only by `kind` — the distinction is preserved (lossless round-trip).

### ContractEntry / contract
```
ContractEntry → {"id":<string>,"severity":"advisory"|"blocking","spec":{"document":…,"section":…},"statement":<rendered>}
contract      → [ <ContractEntry>… ]            // catalog order; empty contract → []
```
`RuleId (RuleId s)` emits the inner string `s`.

### EvidenceState token (a JSON string)
```
Pending→"pending"  Real→"real"  Synthetic→"synthetic"
Failed→"failed"    Skipped→"skipped"  AutoSynthetic→"autoSynthetic"
```

### Effective-state map
```
{ "<project id>":"<state token>", … }           // keys ordinal-sorted; parse → Map<string,EvidenceState>
```

## 4. Derivation rules & invariants

### Freshness (FR-008/009/010, SC-007/008)
- **R-F1** `decide recorded covered = Fresh` iff `∀ c ∈ covered. recorded ≥ c` (i.e.
  `recorded ≥ max covered`); else `Stale`.
- **R-F2** `covered = []` ⇒ `Fresh` (nothing to be stale against).
- **R-F3 (inclusive tie)** `recorded = c` for the maximal `c` ⇒ `Fresh` (boundary inclusive).
- **R-F4 (purity)** `decide` reads no clock/filesystem/git/network; identical inputs ⇒
  identical output regardless of evaluation time. `isFresh a b = (decide a b = Fresh)`.

### Contract (FR-005/006/007, SC-005/006)
- **R-C1** `ofRules` produces exactly one entry per input rule, in catalog order.
- **R-C2** `entry.Statement = Check.render rule.Check` — never a separately authored string
  (drift-proof). Changing a rule's `Check` changes its `Statement`; reordering the catalog
  leaves each rule's own entry unchanged (per-rule rendering).
- **R-C3** `ofRules [] = []`; `ofRules` and `render` are deterministic and total.
- **R-C4** No probe is executed (`render` folds structure only) and no I/O is performed.

### JSON (FR-001/002/003/004/011, SC-001/002/003/004)
- **R-J1 (mirror shape)** `ofExplanation` reproduces the proof tree's node structure; the
  root `verdict` equals `Check.eval` over the originating check/facts (guaranteed upstream by
  `Check.explain`); every node carries its rolled-up verdict.
- **R-J2 (no probe / opaque)** Serialization runs no `Eval`; an `OpaqueExplained` node emits
  `name` + recorded `outcome` only — no function is ever serialized.
- **R-J3 (determinism)** For a given value, `of*` emits byte-for-byte identical JSON every
  time; object keys are written in fixed order, arrays in structural order, effective-map keys
  ordinal-sorted. Output is invariant under input permutations that do not change the value
  (e.g. `Map` insertion order).
- **R-J4 (round-trip)** For every kernel-emitted JSON, `to*` reconstructs a value equal to the
  original: `toExplanation (ofExplanation e) = e`, `toContract (ofContract c) = c`,
  `toEvidenceState (ofEvidenceState s) = s`, and `toEffective (ofEffective project m)` equals
  `m` re-keyed by `project` (the six tokens are distinct, so no two states collide).
- **R-J5 (safe failure)** `to*` over kernel-emitted JSON is total; malformed/foreign JSON or an
  unknown token fails fast with an explicit exception (never a silently wrong value).

## 5. Edge cases (from spec → behaviour)

| Edge case | Behaviour |
|---|---|
| Single atomic explanation | One node object with its outcome + verdict. |
| Empty `All` (vacuous `Pass`) / empty `Any` | `parts:[]` with the inherited verdict; no special-casing. |
| Empty rule catalog | `ofRules [] = []`; `ofContract [] = "[]"`. |
| Evidence covering no artifacts | `decide r [] = Fresh`. |
| Single covered artifact | reduces to one `recorded ≥ c` comparison. |
| Tie at the same instant | `Fresh` (inclusive boundary). |
| `Opaque` node | name + recorded outcome only; no function emitted, no probe run. |
| `AutoSynthetic` in effective map | serialized to its own `"autoSynthetic"` token, visibly marked. |

## 6. Dependencies & ordering
`Freshness` (standalone) → `Contract` (needs F04 `CheckRule`/`Severity`/`SpecSource` + F03
`Check.render`) → `Json` (needs F03 `Explanation`, F05 `EvidenceState`, F06 `ContractEntry`).
Compiled after `CheckRule.*`, in that order (`Json` last). Zero new dependency — JSON via the
shared-framework `System.Text.Json` (research D1).
