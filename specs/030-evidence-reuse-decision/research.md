# Phase 0 Research: Evidence-Reuse Decision Core

The spec left three home decisions to planning ("new core vs. extend F029", "opaque evidence-reference
representation", "reuse-store representation + which prior-entry diff to surface"). This file records those
and the supporting facts. There were **no `NEEDS CLARIFICATION` markers** in the Technical Context; the
decisions below close the deferred home questions.

## D1 — New pure core, FreshnessKey-only dependency (Tier 1)

**Decision**: Add a new packable library `src/FS.GG.Governance.EvidenceReuse` that references **only**
`FS.GG.Governance.FreshnessKey`. It reuses F029's `FreshnessInputs`, `matches`, `diff`, and `InputCategory`
verbatim. The F014 newtypes arrive transitively *through* F029 (modern .NET SDK project references flow
transitively for compile); this core names no `Config` type and adds no direct `Config` project reference —
it only touches `FreshnessInputs` fields via structural equality.

**Rationale**: The Constitution Engineering Constraint keeps the rule/evidence helper core minimal and free
of git/filesystem scanning; F029 already established the "depend on the smallest upstream that carries the
vocabulary" pattern (it referenced only `Config`). The reuse decision's entire vocabulary is F029's, so
F029 is the smallest sufficient dependency. A new core (rather than extending F029) keeps F029's surface
frozen — F029 is merged, its baseline committed — and keeps each pure core single-purpose (compute-the-key
vs. decide-reuse), matching F018→F019→F020 (each a new core consuming the prior).

**Alternatives considered**:
- *Extend F029 with `decide`/`record`.* Rejected: mutates a merged core's surface/baseline (avoidable
  Tier-1 churn) and conflates "fingerprint the world" with "decide reuse over a store" — two concerns the
  repo has consistently split into separate cores.
- *Reference `Gates` to reuse its carried `FreshnessKey` record.* Rejected for the same reason F029 avoided
  it (research D1 there): the gate record wrapper pulls in more than the vocabulary needs, and F029 already
  exposes the exact `FreshnessInputs` shape.

## D2 — Reuse is exactly F029 `matches`; the explanation is exactly F029 `diff`

**Decision**: *Reuse* fires **iff** some recorded entry `FreshnessKey.matches` the candidate (i.e. their
F029 keys are byte-equal). The `InputsChanged` cause carries `FreshnessKey.diff candidate priorEntry`. No
new notion of partial, weighted, or fuzzy match is introduced.

**Rationale**: The plan line is literally "cache reusable evidence only when all freshness inputs match" —
F029's `matches` *is* "all freshness inputs match", and its `diff` *is* the no-hide differing-category
explainer. Reusing them verbatim means the reuse decision and the freshness key can never disagree, and the
explanation vocabulary (`InputCategory`) is already committed and tested.

**Alternatives considered**: A bespoke field-by-field comparison inside this core — rejected as duplicated
logic that could drift from F029's set-semantics for covered artifacts (FR-004 is already guaranteed by
`matches`/`diff`).

## D3 — Opaque evidence reference newtype

**Decision**: `type EvidenceRef = EvidenceRef of string` — a thin, opaque, comparable single-case newtype.
The core carries it back on *Reuse* and never parses, validates, produces, or dereferences it. An empty or
unusual string is a literal value, not an error.

**Rationale**: Mirrors F029's `Revision` (research D3 there): an edge-minted token the pure core treats as
data. It keeps the "what evidence" payload opaque so this row stays the *decision*, not the *store/verify*
of evidence content (the later cache-write row owns dereferencing and integrity).

**Alternatives considered**:
- *Reference the kernel `Evidence` model.* Rejected: couples this minimal decision core to a heavier model
  and to evidence *content*, which this row deliberately does not inspect (FR-001, FR-011).
- *Make the core generic over the payload type.* Rejected (Principle III): a concrete opaque newtype is the
  plainest thing that works; generics buy nothing for a carried-through token and complicate the surface.

## D4 — The reuse store is an ordered list, newest-first

**Decision**: `type ReuseStore = ReuseStore of RecordedEvidence list`, with
`RecordedEvidence = { Inputs: FreshnessInputs; Evidence: EvidenceRef }`. `record` **prepends** the new entry
after filtering out any prior entry that `matches` the new inputs (so the head is most-recent and there is
at most one entry per matching-input class). `decide` scans **head-first** (`List.tryFind`), so when several
entries could match, the most-recently-recorded one wins, deterministically — even for a hand-built store
that happens to contain duplicates.

**Rationale**: Determinism, not lookup latency, is the contract (the store is small — a handful of entries
per gate). A list is the plainest representation (Principle III), keeps the store trivially inspectable
(`entries`), and avoids a "compute the F029 `Key` at insert" coupling that a `Map`-keyed-by-`Key` store
would force. Prepend + head-first scan gives an obvious, total "most-recent-wins".

**Alternatives considered**:
- *`Map<Key, RecordedEvidence>` keyed by `FreshnessKey.compute`.* Rejected: forces key computation into the
  store layer, makes "most-recent-wins" implicit in map-replace semantics, and is an optimization the scale
  doesn't need. (A later persistence row may choose an indexed representation; this pure decision core does
  not need it.)
- *Append (oldest-first) + `List.tryFindBack`.* Equivalent in behavior; prepend + `tryFind` reads more
  directly as "newest first".

## D5 — "Prior evidence for the candidate's work" = same F018 `GateId` (Check + Domain)

**Decision**: When no entry fully matches, the `Recompute` cause is selected as:
- if some entry has **Check = candidate.Check && Domain = candidate.Domain** (the `GateId =
  "<domain>:<checkId>"` identity), take the most-recent such entry `e` and return
  `InputsChanged (FreshnessKey.diff candidate e.Inputs)`;
- otherwise return `NoPriorEvidence`.

Because this branch is reached only when **no** entry fully matches, the surfaced `diff` is always non-empty
and never lists `CheckIdentity`/`DomainIdentity` (those are equal by construction) — it names exactly the
non-identity categories that changed (e.g. `[RuleHashCat; HeadRevisionCat]`).

**Rationale**: This resolves the spec's deferred "which prior entry's diff to surface" question with the
identity the whole catalog is already keyed by — `GateId` (F018). "Same work" = "same gate" is the auditor's
mental model ("the previous run of *this* gate differed in …"), and it makes the two recompute causes
crisply distinct: `NoPriorEvidence` ("never recorded this gate") vs `InputsChanged` ("recorded this gate,
but the world moved"). The no-hide requirement (FR-006) is satisfied: every recompute has a located,
non-ambiguous cause.

**Alternatives considered**:
- *Use the full carried identity (Check + Domain + Command + Environment) as "same work".* Rejected:
  Command/Environment are themselves freshness categories that can legitimately change between runs of the
  same gate; folding them into "same work" would misclassify a command-version bump as "no prior evidence".
  `GateId` (Check + Domain) is the stable identity.
- *Report the diff against every near-miss entry.* Rejected: noisier than needed; the most-recent same-gate
  entry is the relevant "last run of this gate". (FR-006 requires *a* located cause, not all of them.)
- *Return an empty `InputsChanged []` when no same-gate entry exists.* Rejected: an empty category list is
  the "they match" signal (`diff = [] ⇔ matches`); reusing it for "nothing recorded" would be ambiguous.
  A distinct `NoPriorEvidence` case keeps the negative answer honest (Principle VI).

## D6 — "Output digest" remains out of scope (inherited from F029 D4)

**Decision**: The output digest is **not** a reuse-decision input. It is a *result* of running a gate (a
cache-write/verify concern), recorded alongside reused evidence to confirm integrity, and belongs to the
later cache-write row — not to this input-identity decision.

**Rationale**: Identical to F029 research D4; the freshness *inputs* decide reuse, the output *digest*
verifies a reused result. Keeping it out preserves the clean "decide reuse from inputs" boundary.

## Supporting facts

- **F029 surface consumed** (verbatim, from `surface/FS.GG.Governance.FreshnessKey.surface.txt`):
  `FreshnessInputs` (record), `matches : FreshnessInputs -> FreshnessInputs -> bool`,
  `diff : FreshnessInputs -> FreshnessInputs -> InputCategory list`, `InputCategory` (10-case DU),
  `Model.categoryToken`. `compute`/`Key`/`value` are available but not required by this core (the decision
  uses `matches`/`diff` directly).
- **Set semantics inherited**: because reuse uses `matches` and the explanation uses `diff`, covered-artifact
  order and duplication never affect a decision — F029 already proves this (FR-004, SC-002 here is a thin
  re-assertion at the decision layer).
- **Totality**: `decide` and `record` are total — `List.tryFind`/`List.filter`/cons never throw, and every
  `FreshnessInputs`/`EvidenceRef`/`ReuseStore` is a valid input (FR-003, FR-012).
- **Purity / scope guard**: the assembly references only `FSharp.Core`, `FS.GG.Governance.FreshnessKey`,
  `FS.GG.Governance.Config` (transitive), and the BCL — pinned by the `SurfaceDrift` scope-hygiene test
  (the F029 precedent), which also forbids `Gates`/`Snapshot`/`Route`/`Routing`/`Findings`/host/adapters/CLI.
