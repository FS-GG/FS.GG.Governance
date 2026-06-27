# SDD → Governance handoff: connect readiness to the Governance loop

**Audience**: an integrator connecting a scaffolded, SDD-governed product to the
Governance routing/evidence/enforcement loop. **Outcome (FR-007, SC-008)**: you can
state exactly which `readiness/<id>/governance-handoff.json` fields Governance
consumes and how each maps — matching the accepted contract row-for-row.

This tutorial uses the product you scaffolded in
[adopter-onboarding.md](./adopter-onboarding.md) as the worked subject. It is
**explanatory only** — see the scope note at the end.

## The handoff contract

`FS.GG.SDD` authors a versioned, optional, SDD-owned document —
`readiness/<id>/governance-handoff.json` (contract v1.0.0, `schemaVersion = 1`) —
that projects each work item's normalized work model, declared evidence, and
verify/ship readiness into a single document **Governance consumes**. SDD imports
no Governance code; the contract is validated against Governance's target shapes by
mapping tests on the SDD side.

The locally build-verifiable anchor for the mapping is **ADR 0002**
([`docs/decisions/0002-sdd-governance-handoff-contract.md`](../decisions/0002-sdd-governance-handoff-contract.md)),
which records Governance's accepted position.

## The readiness → Governance mapping (verbatim from ADR 0002)

| `governance-handoff.json` field / state | Governance outcome | Verified against ADR 0002 |
|---|---|---|
| `evidence.nodes[].state` ∈ `{pending, real, synthetic, failed, skipped}` | maps straight through to `Kernel.EvidenceState`; tokens identical | ✓ "States `synthetic`/`real`/`failed`/`pending` map straight through" |
| `deferred` / `accepted-deferral` (SDD) | → `skipped` (a `[-]` skip with rationale, **not** `pending`) | ✓ Decision: "Confirm `deferred → skipped`." |
| `autoSynthetic` | **invalid in a produced handoff**; Governance derives it via `Evidence.effective` (taint closure) | ✓ "SDD never emits `autoSynthetic` (computed-only)… Governance's taint closure derives [it]" |
| `stale` | underlying declared state **+** a `staleEvidence` diagnostic (Governance-owned freshness) | ✓ "`stale` maps to the underlying declared state **plus** a `staleEvidence` diagnostic" |
| `governedReferences[*].paths` | **first-class routing candidates** (F082): merged + de-duplicated with the F016 sensed change set before `Routing.route`, so a declared path selects the domain gates that own it — even with an empty sensed diff. Still a no-op when absent/empty | ✓ ADR 0002 (updated by F082): queue item #3 "**Resolved (F082)** — `governedReferences` are first-class routing candidates, merged + de-duplicated with the sensed change set before `Routing.route`" |
| `readiness.*` (`shipDisposition`, `verificationReadiness`, counts, `blockingDiagnosticIds`, `perViewState`) | a **first-class gate-registry entry** — blocking when the disposition is non-shippable **or** `blockingDiagnosticIds` is non-empty, advisory otherwise; participates in selection/severity/roll-up like any other gate | ✓ ADR 0002 (updated by F081): "become a **first-class gate-registry entry** (F018)" — supersedes the original "advisory declared inputs" wording (FR-009/FR-015) |
| unknown `contractVersion` **major** | version-mismatch finding, never a silent misread (pin v1.x) | ✓ "A consumer that does not recognize the handoff's `contractVersion` **major** MUST report a version-mismatch finding" |

**Mapping-drift guard (SC-008).** Every row above carries an explicit
ADR-0002 citation in the rightmost column; a reviewer confirms each row against
the ADR (the per-row ✓ is the checklist). If ADR 0002's accepted mapping changes,
the corresponding row must be updated in the same change — the table is not allowed
to silently diverge from the contract it documents.

## Why `deferred → skipped` (not `pending`)

Governance's kernel `EvidenceState` tokens are exactly
`pending`/`real`/`synthetic`/`failed`/`skipped`/`autoSynthetic`. The constitution
status legend defines `skipped` as "done, skipped with a recorded rationale"
(`[-]`). An SDD deferral carries exactly such a rationale, so it is a `[-]` skip,
not a `[ ]` not-started (`pending`). Mapping a deferral to `pending` would
misrepresent a deliberate, justified postponement as un-started work and would
distort taint closure at the boundary.

## Worked example: a declared governed path drives gate selection (F082)

As of `082-route-governed-refs`, the `governedReferences` a handoff declares are
promoted to first-class **routing candidates** — so the surface a work item declares
it governs selects the domain gates that own it, even when nothing was sensed as
changed. Suppose the handoff declares:

```json
{
  "contractVersion": "1.0.0",
  "schemaVersion": 1,
  "evidence": { "nodes": [ { "id": "build:lib", "state": "real" } ], "dependencies": [] },
  "governedReferences": [ { "workItem": "WI-1", "paths": [ "src/Ledger.fs" ] } ]
}
```

and the catalog's path-map routes `src/**` to a domain that owns a `block-on-ship`
build gate. Then, with an **empty** sensed change set:

1. `Consumer.candidatePaths` projects the declared `src/Ledger.fs` (already
   normalized by `Reader.parse`) as a routing candidate.
2. Each verdict host merges it with the sensed set —
   `candidates = sensed @ declared |> List.distinct` — *before* `Routing.route`.
3. `Routing.route` matches `src/Ledger.fs` against the real `src/**` path-map glob,
   and `Route.select` chooses the domain's build gate, recording a selecting path
   `{ Path = "src/Ledger.fs"; MatchedGlob = "src/**" }` — the **real** glob, not the
   self-glob the handoff's own gates carry.
4. The gate now participates in the `route`/`ship`/`verify` verdict like any routed
   gate: under a blocking mode it can flip the verdict to non-shippable, attributable
   solely to the declared surface.

A path present in **both** the sensed set and `governedReferences` is routed once
(the `List.distinct` merge). A handoff that declares no `governedReferences`, or one
that `Reader.parse` refuses, contributes **zero** candidates — output stays
byte-identical, and a bad document still fires its blocking integrity gate via the
unchanged `consume` fold (it never widens routing enforcement).

## Scope: what ships here, and what does not

> **The consumer now ships (F081).** As of `081-sdd-handoff-consumer`, the
> `governance-handoff.json` reader/parser (`Reader.parse`), the `evidence.nodes` →
> `Evidence.build` adapter (`Mapping`), and the readiness → gate projection
> (`Readiness.toGate`) ship in `FS.GG.Governance.Adapters.SddHandoff`, and the
> three verdict hosts (`route`/`ship`/`verify`) fold the derived gates into the
> verdict — a produced handoff now drives a Governance verdict. ADR 0002's queue
> items #1, #2, and #4 are resolved there. This tutorial documents the accepted
> *mapping* (the contract Governance reads against); the running code lives in
> F081, not here.

> **Production wiring is sibling-owned (FR-013).** Wiring the template-provider
> seam into a production `fsgg-sdd init` is owned by the sibling **`FS.GG.SDD`**
> repository, not this one.

## Cross-references

- **Local, verifiable**: ADR 0002 —
  [`docs/decisions/0002-sdd-governance-handoff-contract.md`](../decisions/0002-sdd-governance-handoff-contract.md).
- **External (cross-repo) pointer**: the sibling **`FS.GG.SDD`** repository's
  `017-governance-handoff` spec (`contracts/integration-requirements.md`). Note
  this is **not** this repo's `specs/017-*` (which is
  `017-unknown-governed-path-findings`) — it lives in the sibling repo and is not
  build-verifiable from here.
