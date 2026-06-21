# Phase 0 Research: Ship Verdict Rollup (Pure Core)

**Feature**: `024-ship-verdict-rollup` | **Date**: 2026-06-21

This row carries no NEEDS CLARIFICATION on *technology* — the stack, test framework, and packing
shape are fixed by F014–F023. The open questions are the **plan-time reconciliations** the spec
deferred (its *Assumptions* block): project home, the gate/finding → F023-enforcement-input mapping,
and the result shape. Each is resolved below in the Decision / Rationale / Alternatives format.

---

## D1 — Project home & surface spelling

**Decision**: A new optional, packable **pure-leaf** project `FS.GG.Governance.Ship`, namespace
`FS.GG.Governance.Ship`, with two compile-ordered module pairs:

- `Model.fsi` / `Model.fs` — the result vocabulary (`Verdict`, `ExitCodeBasis`, `EnforcedItemId`,
  `EnforcedItem`, `ShipDecision`).
- `Ship.fsi` / `Ship.fs` — the single entry point `Ship.rollup : RouteResult -> RunMode -> Profile ->
  ShipDecision`.

`IsPackable=true`, `PackageId=FS.GG.Governance.Ship`, `Version=0.1.0`, mirroring F023's `.fsproj`.

**Rationale**: This is the exact one-row-one-project rhythm the repo has used since F014, and it
mirrors how the `route` lineage split: `FS.GG.Governance.Route` was the pure selection core (F019),
`...RouteJson` the projection (F020), `...RouteCommand` the host edge (F022). The ship lineage is the
sibling: **`...Ship`** is the pure rollup core (this row), the `audit.json` projection is the next row
(the F020/F021 sibling), and the `fsgg ship` host command is the row after (the F022 sibling). The
internal `Model` + entry-point module split is exactly F019's `Route.Model` + `Route.Route` shape.

**Alternatives considered**:
- *Extend `FS.GG.Governance.Enforcement` (F023).* Rejected: F023 is deliberately the per-finding
  derivation with **one** inward reference (`Config`). Adding a whole-change rollup would pull `Route`
  (and transitively `Gates`/`Findings`) into Enforcement, breaking its leaf shape and the clean
  "derive one finding" contract the later rows compose. The repo's precedent is a new sibling, not a
  fattened leaf.
- *Name it `ShipVerdict` (the branch slug) or `Verdict`.* Rejected: project names in this repo are the
  short conceptual noun (`Route`, `Enforcement`, `Gates`), not the branch slug; `Verdict` collides
  conceptually with the kernel's verdict lineage. `Ship` is the design's word for this edge
  (`fsgg ship`) and reads cleanly against the `audit.json`/`ShipCommand` siblings to come.

---

## D2 — Project references (and what stays transitive)

**Decision**: `FS.GG.Governance.Ship` takes **two** direct project references:

- `FS.GG.Governance.Enforcement` (F023) — for `RunMode`, `Profile`, `Severity`, `EnforcementInput`,
  `EnforcementDecision`, and `deriveEffectiveSeverity`.
- `FS.GG.Governance.Route` (F019) — for `RouteResult`, `SelectedGate`, `CostRollup`.

`Gates.Model` (`Gate`, `GateId`, `gateIdValue`), `Findings.Model` (`FindingId`, `FindingZone`,
`UnknownGovernedPathFinding`, `FindingReport`), and `Config.Model` (`Maturity`, `GovernedPath`) arrive
**transitively** through `Route`. **No new third-party `PackageReference`** — the rollup is pure value
logic over closed DUs and lists; it needs no serialization, git, clock, or filesystem primitive
(`System.*`/FSharp.Core only), exactly as F023.

**Rationale**: Enforcement is a separate leaf that `Route` does not reference, so it must be direct.
Everything else the rollup `open`s is already on `Route`'s reference graph and flows transitively under
the SDK default. This is the minimal-dependency posture the constitution's Engineering Constraints
require, and it matches F023 taking only `Config`.

**Alternatives considered**: Adding explicit `Gates`/`Findings`/`Config` references "for clarity."
Rejected as redundant — they are transitive, and the repo doesn't list transitive refs explicitly
(F019's `Route` opens `Gates.Model`/`Findings.Model`/`Config.Model` without re-declaring them all). The
test project, by precedent (F023's test `.fsproj` re-listed `Config`), MAY re-list the projects whose
types its fixtures construct directly; that is a tasks.md detail.

---

## D3 — Gate → `EnforcementInput` mapping (FR-013)

A `Gate` (F018) carries a declared `Maturity` and `GateId`/`Domain`/`Cost`/… but **no explicit base
severity**. The rollup must obtain a base severity and a maturity for it deterministically from carried
facts — introducing no new fact source.

**Decision**: For each selected gate, build the F023 input as:

| `Gate.Maturity` | → `BaseSeverity` | → `Maturity` (passed verbatim) |
|---|---|---|
| `Observe` | `Advisory` | `Observe` |
| `Warn` | `Advisory` | `Warn` |
| `BlockOnPr` | `Blocking` | `BlockOnPr` |
| `BlockOnShip` | `Blocking` | `BlockOnShip` |
| `BlockOnRelease` | `Blocking` | `BlockOnRelease` |

i.e. a `block-on-*` maturity implies a base-`blocking` gate; `observe`/`warn` imply base-`advisory`.
`Mode`/`Profile` come from the rollup's two arguments. The gate's `Maturity` is passed to
`deriveEffectiveSeverity` **verbatim** — no translation.

**Rationale**: This is the only mapping consistent with F023's own `maturityFloor`: `Observe`/`Warn`
have `None` floor (withhold blocking forever) and `block-on-*` have an integer floor (`gate`/`release`).
Pairing `observe`/`warn` with base-`advisory` and `block-on-*` with base-`blocking` makes the gate's
declared maturity and its base severity mutually consistent, so a gate never lands in the contradictory
state "base blocking but withheld forever." It is the same kind of fact-derived heuristic F018 used for
`ProductCheck` (`Environment = Release`). An independent per-gate base severity is the deferred
`policy.yml` dial layer (F023 FR-015), explicitly out of scope here.

**Alternatives considered**: (a) Treat every gate as base-`blocking` and let maturity/profile relax it.
Rejected — `observe`/`warn` gates would then surface as *warnings* (base-blocking-relaxed) forever,
overstating the change's risk and contradicting their "advisory rule" intent. (b) Read a base severity
from `policy.yml`. Rejected — that file is out of scope this row (FR-012); no parsing here.

---

## D4 — Finding → `EnforcementInput` mapping (FR-013)

A finding (F017) carries a `FindingId` and a `FindingZone` but no severity/maturity. The edge cases
require an **escalated protected-boundary finding to be able to block even when the change selected no
gate**, while an ordinary governed-root unknown stays advisory.

**Decision**: Map on the finding's `Zone`:

| `FindingZone` | → `BaseSeverity` | → maturity-equivalent | Effect |
|---|---|---|---|
| `GovernedRootUnknown` | `Advisory` | `Warn` | always a **passing** item (never escalated — FR-011) |
| `ProtectedBoundaryUnknown _` | `Blocking` | `BlockOnShip` | **blocks at `gate`+** (and warns below it) |

`Mode`/`Profile` come from the rollup arguments. The escalating `SurfaceId` carried by
`ProtectedBoundaryUnknown` is preserved in the item identity (D6) but does not change the mapping.

**Rationale**: `GovernedRootUnknown` is the design's advisory "unknown path" signal; pairing it with
`Advisory` + `Warn` means F023 derives `Advisory` under every mode/profile (its `Warn` floor is `None`)
— a passing item that this core never escalates (FR-011), consistent with F023's no-escalation rule and
the design's `light.unknownPaths: warn`. `ProtectedBoundaryUnknown` is the *escalated* flavor (F017's
own distinction); pairing it with base-`Blocking` + `BlockOnShip` (floor = `gate`, ordinal 4) makes it
block at `--mode gate` — the design's ship run position — independent of whether any gate was selected,
which is precisely the edge case "a protected boundary's escalated finding can block even when the
change selected no gate." Below the gate boundary (e.g. `inner`) it relaxes to a *warning*, so the
escalation is always visible, never hidden.

**Alternatives considered**: (a) Both zones base-`blocking`. Rejected — governed-root unknowns are
advisory by design (`unknownPaths: warn` at `light`); making them blockers misrepresents the verdict.
(b) Map by `FindingId` instead of `Zone`. Rejected — `Id` and `Zone` are 1:1 in F017
(`UnknownGovernedPath`↔`GovernedRootUnknown`, `UnknownProtectedBoundaryPath`↔`ProtectedBoundaryUnknown`),
and the spec's reconciliation language keys on **zone**; using `Zone` keeps the carried `SurfaceId`
in hand for identity. (c) `BlockOnRelease` for protected boundaries. Rejected — that floor is `release`
(ordinal 5), so the finding would *not* block at `--mode gate`, defeating the edge case.

---

## D5 — Result shape: how items are partitioned

**Decision**: `ShipDecision` exposes the verdict, the exit-code basis, and a **three-way partition** of
every enforced item:

- `Blockers: EnforcedItem list` — items whose `EffectiveSeverity = Blocking`.
- `Warnings: EnforcedItem list` — items that are base-`Blocking` but `EffectiveSeverity = Advisory`
  (relaxed by mode/maturity/profile).
- `Passing: EnforcedItem list` — base-`Advisory` items (effective `Advisory`).

The three lists are **mutually exclusive and jointly exhaustive**: `|Blockers| + |Warnings| +
|Passing| = N gates + M findings` (SC-006). Every `EnforcedItem` carries its full F023
`EnforcementDecision` (base severity, effective severity, mode, profile, maturity, reason) plus its
identity — so the "full per-item enforcement detail" the spec's key entity names is the union of the
three lists; there is no separate, redundant "all items" field.

**Rationale**: The partition makes the 1:1 accounting (FR-010, SC-006) and the no-hide rule (FR-005,
SC-003) directly checkable, and it satisfies FR-004's explicit "blockers list" and "warnings list"
without storing any item twice. `Verdict = Fail` iff `Blockers` is non-empty, else `Pass` (FR-002);
`ExitCodeBasis = Blocked` iff `Verdict = Fail`, else `Clean` (FR-007). Both are total functions of the
partition.

**Alternatives considered**: A single flat `Items` list plus `blockers`/`warnings` as accessor
functions. Rejected — FR-004 requires the lists to be *exposed values* in the decision (the later
`audit.json` projection serializes them), and a flat list defers the partition to every consumer. An
`Items` + `Blockers` + `Warnings` triple was rejected for storing blockers/warnings twice (drift risk);
`Passing` as the third partition is the non-redundant complement.

---

## D6 — Item identity & deterministic ordering (FR-009)

**Decision**: Each `EnforcedItem` carries an `EnforcedItemId`:

```
type EnforcedItemId =
    | GateItem of GateId
    | FindingItem of FindingId * GovernedPath
```

A finding's identity is `(FindingId, Path)` because the same `FindingId` can recur on several paths;
the path disambiguates. Within each of the three lists, items are sorted by a **stable composite key**:
gates before findings, gates by `gateIdValue`, findings by `(normalized Path, findingIdToken)` —
i.e. the ordinal string key `"gate:" + gateIdValue id` for a gate and
`"finding:" + path + ":" + findingIdToken` for a finding, sorted by ordinal string comparison. This is
the kind of stable per-item key FR-009/SC-004 require, and it reuses F018's `gateIdValue` and F017's
`findingIdToken` rather than inventing rendering.

**Rationale**: A list may contain **both** gates and findings (e.g. a blocker set with an escalated
finding and a `block-on-ship` gate), so a single total order across both kinds is needed; the
kind-tagged composite key gives one. Sorting by identity (not input-arrival order) makes the output
byte-identical for identical inputs regardless of how `RouteResult.SelectedGates` / `Findings` were
ordered upstream (they are already F019/F017-deterministic, but the rollup must not *depend* on that for
its own ordering — FR-009 forbids input-arrival influence).

**Alternatives considered**: Preserve F019/F017 input order. Rejected — that couples the rollup's
ordering to upstream ordering and mixes two independently-ordered lists (gates by `GateId`, findings by
path) with no defined interleave; an explicit composite key is the contract FR-009 demands.

---

## D7 — Gate de-duplication and 1:1 accounting (FR-010, edge case)

**Decision**: The rollup evaluates `RouteResult.SelectedGates` as given. F019 has **already**
union-deduped selected gates by `GateId` (one `SelectedGate` per distinct gate, however many paths
reached it), so the rollup performs **no** further de-dup — it maps the gate→input→decision pipeline
over each `SelectedGate` exactly once and over each `UnknownGovernedPathFinding` in
`RouteResult.Findings.Findings` exactly once. Result count is therefore exactly `N + M`.

**Rationale**: FR-010 requires each distinct selected gate evaluated once and no item dropped; F019's
contract already guarantees `SelectedGates` is distinct-by-`GateId`, so re-deduping would be redundant
and re-deriving classification this core must not do (the spec's "re-derives, re-sorts, re-classifies
nothing" rule). Mapping 1:1 and never filtering guarantees the SC-006 accounting.

**Alternatives considered**: Defensive re-dedup by `GateId` inside the rollup. Rejected — it would
re-implement F019's dedup (spec forbids re-deriving upstream-fixed facts) and could only ever be a
no-op given F019's contract.

---

## D8 — Worked-example reproduction (SC-002)

**Decision**: The design's worked example (`docs/initial-design.md:516` — base `blocking`, maturity
`block-on-ship`, mode `inner`, profile `light` ⇒ effective `advisory`) is reproduced by a **gate with
`Maturity = BlockOnShip`** (which D3 maps to base-`Blocking`) evaluated by `rollup` at `Mode = Inner`,
`Profile = Light`: F023 derives `Advisory` (floor `gate`/4, `inner` ordinal 1 < 4) with the documented
relaxed reason, so the item lands in `Warnings`. The single-rollup "one warning **and** one blocker,
`fail` verdict" fixture (SC-002's second half) is realized at `Mode = Gate`, `Profile = Light`, where a
`BlockOnShip` gate **blocks** (floor 4, `gate` ordinal 4 ≥ 4 → `Blockers`) while a `BlockOnRelease`
gate **warns** (floor 5 > 4 → relaxed `Advisory`).

**Rationale**: The rollup takes a single `Mode`/`Profile` for the whole change, so a single call cannot
both reproduce the example *at inner* and show a sibling blocker (at `inner`, no maturity reaches its
floor under any profile). The faithful realization is two pinned calls: (1) the exact worked example at
`inner`/`light` (warning), and (2) a same-rollup blocker+warning pair at `gate`/`light`. Both are pure
consequences of D3 + F023's published floors; no new derivation is introduced. The precise fixture
construction is a tasks.md concern; this decision fixes that the mapping makes both reproducible.

**Alternatives considered**: Forcing the example and a blocker into one `inner` rollup. Rejected as
impossible under F023's floors — documented here so the test author doesn't chase it.

---

## Resolved unknowns summary

| # | Question | Resolution |
|---|---|---|
| D1 | Project home / module spelling | New leaf `FS.GG.Governance.Ship`; `Model` + `Ship` module pairs; `rollup` entry |
| D2 | Project references | Direct: `Enforcement`, `Route`; transitive: `Gates`/`Findings`/`Config`; no new packages |
| D3 | Gate → enforcement input | `block-on-*` ⇒ base `Blocking`; `observe`/`warn` ⇒ base `Advisory`; maturity verbatim |
| D4 | Finding → enforcement input | `GovernedRootUnknown` ⇒ `Advisory`/`Warn`; `ProtectedBoundaryUnknown` ⇒ `Blocking`/`BlockOnShip` |
| D5 | Result shape | Three-way `Blockers`/`Warnings`/`Passing` partition; verdict + exit-code basis |
| D6 | Identity & ordering | `EnforcedItemId` (gate id / finding-id+path); stable composite-key sort |
| D7 | Dedup / accounting | No re-dedup (F019 already distinct-by-`GateId`); 1:1 map ⇒ N+M items |
| D8 | Worked example | Reproduced via `BlockOnShip` gate at inner/light (warning) and gate/light (blocker) |

All NEEDS CLARIFICATION resolved. No constitution gate is implicated by any decision (see plan
Constitution Check). Proceed to Phase 1.
