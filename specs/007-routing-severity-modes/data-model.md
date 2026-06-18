# Phase 1 — Data Model (F07 · 007-routing-severity-modes)

The types, the precedence/partition rules, the render shape, and the invariants for the light
routing layer. The authoritative shape lives in [`contracts/Route.fsi`](./contracts/Route.fsi);
this document explains it — the `.fsi` is the source of truth for the surface.

## 1. New public types (all in `Route.fsi`)

### `Stakes` (DU, 2 cases)
| Case | Meaning |
|---|---|
| `Routine` | The light default — no declared fence matched; routed with no blocking gates in any run mode (FR-006). |
| `Fenced of name: string` | High-stakes — ≥1 fence tripped. `name` is the tripped fence names de-duplicated, ordinal-sorted, `"; "`-joined (a function of the *set*, hence order-independent — FR-005). |

### `Fence<'change>` (record)
| Field | Type | Meaning |
|---|---|---|
| `Name` | `string` | The declared boundary's name (carried into `Fenced`). |
| `Trips` | `'change -> bool` | Pure predicate: does this change trip the boundary? Only ever RUN — never rendered/hashed. |

`'change` is the abstract `ChangeSet` (research **D1**) — a generic parameter, supplied by the
adapter; the kernel never inspects its shape.

### `RunMode` (DU, 3 cases)
| Case | Meaning |
|---|---|
| `Sandbox` | Free experimentation — advisory only; never a merge basis. |
| `Inner` | Local development loop — advisory only; nothing blocks. |
| `Gate` | The merge boundary — blocking-severity requirements on a fenced change are enforced. |

### `Route` (record) — non-generic (drops `'change` and `'fact`)
| Field | Type | Meaning |
|---|---|---|
| `Stakes` | `Stakes` | The change's classification (identical across run modes — FR-009). |
| `Advisory` | `ContractEntry list` (F06) | Requirements that inform but do not block, in catalog order. |
| `Blocking` | `ContractEntry list` (F06) | The short, filterable blocking-gate subset (FR-013). |
| `Reason` | `string` | Mandatory non-empty explanation of the decision (FR-011). |

### Reused types (no new declaration)
- `ContractEntry`, `Contract.ofRules`, `Contract`-rendered statement (F06) — each requirement
  IS a `ContractEntry` whose `Statement = Check.render` (drift-proof, research **D3**).
- `CheckRule<'fact>`, `Severity` (F04) — the applicable rules folded by `route`.
- `RuleId` (F01), and transitively `Check`/`Verdict` (F03/F02).

## 2. Functions (folds, all pure & total)

| Function | Signature | Role |
|---|---|---|
| `Route.stakesOf` | `Fence<'change> list -> 'change -> Stakes` | order-independent forbid-trumps-permit classifier |
| `Route.route` | `Fence<'change> list -> CheckRule<'fact> list -> RunMode -> 'change -> Route` | classify + fold applicable rules + partition + reason |
| `Route.renderRoute` | `Route -> string` | deterministic, execution-free explanation |

## 3. Precedence & partition rules

### Stakes (FR-004/005/006, SC-001/002/003)
- **R-S1 (any-trips)** `stakesOf fences change = Fenced n` iff `∃ f ∈ fences. f.Trips change`;
  else `Routine`. A single matching fence is sufficient.
- **R-S2 (order-independent name)** `n` = the `Name`s of *all* tripped fences, de-duplicated,
  ordinal-sorted, joined with `"; "`. Hence `stakesOf (permute fences) change = stakesOf fences
  change` for every permutation (closes hazard 5 / decision #4).
- **R-S3 (empty / no match)** `stakesOf [] change = Routine`; no fence tripping ⇒ `Routine`.
- **R-S4 (purity)** runs each `Trips` predicate and nothing else — no I/O, no probe, no clock.

### Route partition (FR-006/008/009/010, SC-001/004/007)
- **R-R1 (compute stakes)** `stakes = stakesOf fences change` — independent of `mode` (FR-009).
- **R-R2 (fold requirements)** `entries = Contract.ofRules rules` — one `ContractEntry` per
  applicable rule, in catalog order, `Statement = Check.render` (drift-proof).
- **R-R3 (enforcement gate)** `enforced = (stakes is Fenced) && (mode = Gate)`.
- **R-R4 (partition)** an entry is **Blocking** iff `entry.Severity = Blocking && enforced`;
  otherwise **Advisory**. So: `Routine` ⇒ `Blocking = []` in every mode (FR-006); a `Blocking`
  rule on a `Fenced` change is Advisory in `Sandbox`/`Inner` and Blocking only in `Gate`
  (FR-008); `Advisory`-severity rules are always Advisory.
- **R-R5 (short subset)** `Blocking ⊆ entries ⊆` the applicable rules (the caller passes only
  applicable rules — research **D5**), never the whole catalog (FR-013, SC-007).
- **R-R6 (mandatory reason)** `Reason` is always non-empty (see §4).
- **R-R7 (total & deterministic)** total for every input incl. empty fences / empty rules
  (FR-015, SC-009); byte-for-byte identical across repeated and fence-reordered evaluation
  (SC-008); runs no probe / no review (FR-016, SC-010).

## 4. `renderRoute` shape (FR-012/014, SC-005/006)

Deterministic text; stable line and field order; execution-free. Illustrative shape (exact
wording fixed in `Route.fs`, mirroring `docs/governance-design/routing-and-modes.md`):

```text
<stakes line>                         # "light — no gates (advisory only)" | "gate — Fenced (<names>)"
reason: <Reason>
blocking (<n>):
  - [Blocking] <ruleId> — <statement>   (<document> §<section>)   ← fence <names>
advisory (<m>):
  - [Advisory] <ruleId> — <statement>   (<document> §<section>)
```

- **R-D1** Each gate line names the rule id, severity, the rendered `Statement` (byte-for-byte
  `Check.render` of the rule's check — no drift, SC-006), and the spec source; blocking gates
  also name the fence (the `Fenced` names) that raised the stakes (FR-012).
- **R-D2** Every section header is **always rendered with its count** — `blocking (0):` /
  `advisory (0):` when empty (the header is never omitted), so the shape is fixed and
  deterministic regardless of how many requirements apply. A `Routine` route with no requirements
  still renders a **non-empty** block (the stakes + reason lines plus the two zero-count
  headers), so no route lacks an explanation (FR-011, SC-005).
- **R-D3** `renderRoute` folds the `Route` value only — no probe, no review (FR-014, SC-010).

## 5. Edge cases (from spec → behaviour)

| Edge case | Behaviour |
|---|---|
| No fences declared (`[]`) | `Routine`; route with empty `Blocking` in every mode. |
| Empty applicable-rule set | `Advisory = []`, `Blocking = []`; route still well-formed with a reason. |
| Multiple fences match | `Fenced` once; names are a deduped, sorted set — not double-counted, order-independent. |
| Fenced but not at `Gate` | `Blocking = []` (requirements appear as Advisory); the stake is recorded, not enforced. |
| Routine at `Gate` | `Blocking = []` — run mode escalates *enforcement of existing stakes*, never manufactures them. |
| Reason mandatory | Every route — routine/fenced, blocking/advisory — has a non-empty `Reason`. |
| Render execution-free | `renderRoute` names rule/fence/check without running any probe or review. |
| Fence-list reordered | identical `Stakes` and identical `Route` (and `renderRoute`) — never positional. |

## 6. Dependencies & ordering
`Route` reuses F06 `ContractEntry`/`Contract.ofRules` (→ F04 `CheckRule`/`Severity` → F03
`Check.render` → F02 `Verdict`) and F01 `RuleId`. It is compiled **after** `Contract.*`;
`Route` and `Json` are independent, so `Route.*` is appended after `Json.*` (append-only
`.fsproj` edit). **Zero new dependency** — standard library only (research **D6/D7**).
