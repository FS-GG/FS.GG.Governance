# Contract: Reuse Decision & Record Semantics

The decision tables that fix `decide` and `record` behavior. These are the byte-precise rules the example
and property tests assert; the API signatures live in [evidence-reuse-api.md](./evidence-reuse-api.md).

Throughout: `matches` = `FreshnessKey.matches`, `diff` = `FreshnessKey.diff`, "same gate" = the candidate
and an entry agree on **Check AND Domain** (the F018 `GateId = "<domain>:<checkId>"` identity).

## `decide candidate store` — the reuse decision

Let `es` be the store's entries, newest-first.

```text
1. full match?   m = es |> List.tryFind (fun e -> matches candidate e.Inputs)
                 ├─ Some e  ⇒  Reuse e.Evidence
                 └─ None    ⇒  go to step 2
2. same gate?    g = es |> List.tryFind (fun e -> e.Inputs.Check = candidate.Check
                                                   && e.Inputs.Domain = candidate.Domain)
                 ├─ Some e  ⇒  Recompute (InputsChanged (diff candidate e.Inputs))
                 └─ None    ⇒  Recompute NoPriorEvidence
```

Properties this guarantees:

- Step 2 is reached only when **no** entry fully matches, so the `g` found there is necessarily a partial
  match ⇒ `diff candidate g.Inputs` is **non-empty**.
- Because `g` shares Check+Domain with the candidate, that `diff` **never** contains `CheckIdentity` or
  `DomainIdentity` — it lists exactly the non-identity categories that moved.
- `tryFind` is head-first and the store is newest-first ⇒ both the reused reference (step 1) and the
  explained prior entry (step 2) are the **most-recently-recorded** qualifying entry, deterministically.

### Worked decision table (store recorded in the order shown, so newest-first = bottom-up)

Base candidate `C` = the F029 worked example (`Check=build:tests`, `Domain=build`, `RuleHash=r1`,
`Head=bbb`, …). Entries:

| Store contents (newest-first) | `decide C store` |
|---|---|
| `[]` | `Recompute NoPriorEvidence` |
| `[ {C, ref=E1} ]` | `Reuse E1` |
| `[ {C with RuleHash=r2, ref=E2}; {C, ref=E1} ]` | `Reuse E1` (the second entry fully matches) |
| `[ {C with RuleHash=r2, ref=E2} ]` | `Recompute (InputsChanged [RuleHashCat])` (same gate, rule moved) |
| `[ {C with Head=ccc, RuleHash=r2, ref=E3} ]` | `Recompute (InputsChanged [RuleHashCat; HeadRevisionCat])` |
| `[ {C with Domain=release, ref=E4} ]` | `Recompute NoPriorEvidence` (different gate — Domain differs) |
| `[ {C with RuleHash=r2, ref=E2b}; {C with RuleHash=r2, ref=E2a} ]` | `Recompute (InputsChanged [RuleHashCat])` *(non-match, both share gate; the diff is identical either way)* |

> Note the `InputsChanged` category order follows F029's fixed `diff` order
> (Check, Domain, Command, Environment, RuleHash, CoveredArtifacts, CommandVersion, GeneratorVersion, Base,
> Head) — so `[RuleHashCat; HeadRevisionCat]`, never `[HeadRevisionCat; RuleHashCat]`.

## `record inputs evidence store` — the de-duplicating insert

```text
let (ReuseStore es) = store
let kept = es |> List.filter (fun e -> not (matches inputs e.Inputs))   // drop superseded full-match
ReuseStore ({ Inputs = inputs; Evidence = evidence } :: kept)           // new entry at the head (newest)
```

Properties this guarantees:

- **No mutation** (FR-007): `store` is unchanged; a new value is returned.
- **At most one entry per matching-input class** (FR-008): any prior entry that `matches inputs` is dropped
  before the new one is consed. (Entries that merely share the gate but differ in some category are *kept* —
  they are evidence for a different world of the same gate.)
- **Most-recent-wins** (FR-008, FR-005): the just-recorded entry is at the head, so a later `decide` for a
  matching candidate returns *its* reference.
- **Independence** (FR-005): recording under inputs that match nothing leaves every prior entry in place and
  individually reusable.

### Worked record table

| Start store | Operation | Resulting store (newest-first) | A later `decide` of matching candidate |
|---|---|---|---|
| `empty` | `record C E1` | `[ {C,E1} ]` | `Reuse E1` |
| `[ {C,E1} ]` | `record C E2` (same inputs) | `[ {C,E2} ]` (E1 superseded, no dup) | `Reuse E2` |
| `[ {C,E1} ]` | `record (C with RuleHash=r2) E2` | `[ {C/r2,E2}; {C,E1} ]` | `C ⇒ Reuse E1`; `C/r2 ⇒ Reuse E2` |

## Degenerate / edge behavior (all total, no error)

| Case | Behavior |
|---|---|
| Empty store | `decide _ empty = Recompute NoPriorEvidence`. |
| Empty / unusual `EvidenceRef` string | Carried verbatim; never parsed or rejected. |
| Multiple full-match entries in a hand-built store | `decide` returns the head-most (most-recent) entry's reference, deterministically. |
| Candidate with reordered/duplicated `CoveredArtifacts` | Same decision (inherited from F029 `matches`/`diff`). |
| Base = Head in candidate or entry | Ordinary value; affects matching only via the `Base`/`Head` categories. |
| No entry shares the candidate's gate | `Recompute NoPriorEvidence` (distinct from `InputsChanged`). |
