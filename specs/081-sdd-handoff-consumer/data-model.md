# Phase 1 Data Model: SDD→Governance Handoff Consumer

**Feature**: `081-sdd-handoff-consumer` · **Date**: 2026-06-27

Entities the consumer introduces (in `FS.GG.Governance.Adapters.SddHandoff`) and the existing
Governance types they map onto. Field names for the SDD-owned document come from ADR 0002 + the
handoff tutorial; the exact JSON key spellings are SDD-owned and cross-checked at implementation
(research D8). Nothing here is persisted by Governance — the document is read-only input.

---

## 1. Handoff document (read-only input)

The in-memory projection of one `readiness/<id>/governance-handoff.json`.

| Field | Type | Notes |
|---|---|---|
| `ContractVersion` | `string` (semver) | Consumer pins **major `1`**; unknown major ⇒ version-mismatch diagnostic (FR-002). |
| `SchemaVersion` | `int` | Expected `1`; carried for diagnostics. |
| `Evidence` | `EvidenceBlock` | Declared nodes + dependencies (may be empty — consumed independently of readiness). |
| `Readiness` | `ReadinessBlock option` | Merge-boundary disposition (may be absent). |
| `GovernedReferences` | `GovernedReference list` | **Optional** routing enrichment only (FR-010). |

**Validation**: missing required contract field, malformed JSON, or unrecognized
`ContractVersion` major ⇒ a typed `Diagnostic` and **no** mapped result (FR-011, research D5).

---

## 2. EvidenceBlock / Declared evidence node

| Field | Type | Notes |
|---|---|---|
| `Nodes` | `DeclaredNode list` | one per `evidence.nodes[]`. |
| `Dependencies` | `(string * string) list` | "a rests on b" edges, fed verbatim to `Evidence.build`. |

**DeclaredNode**

| Field | Type | Notes |
|---|---|---|
| `Id` | `string` | node identity (graph key). |
| `State` | `DeclaredState` | the SDD-declared state (below). |
| `Stale` | `bool` | freshness flag; Governance-owned (FR-006). |
| `Rationale` | `string option` | carried for `deferred → skipped` and diagnostics. |

**DeclaredState** (closed union of the tokens a *produced* handoff may declare):
`Pending | Real | Synthetic | Failed | Skipped | Deferred | AcceptedDeferral`.
`autoSynthetic` is **not** a member — declaring it is rejected (FR-005).

---

## 3. Mapping rules → `Kernel.EvidenceState` (ADR-0002, research D4)

| `DeclaredState` (+`Stale`) | → `EvidenceState` | Diagnostic |
|---|---|---|
| `Pending`/`Real`/`Synthetic`/`Failed`/`Skipped` | same token | — (FR-003) |
| `Deferred` / `AcceptedDeferral` | `Skipped` | — (FR-004) |
| any with `Stale = true` | underlying mapped state | `staleEvidence` (FR-006) |
| `autoSynthetic` (declared in JSON) | — (rejected) | version/mapping-rejection (FR-005) |

The mapped `(id, EvidenceState) list` + `Dependencies` feed `Evidence.build`, then
`Evidence.effective` computes the taint closure. A `Failed` or `AutoSynthetic` **effective**
state ⇒ the evidence gate is blocking-capable (research D3).

---

## 4. ReadinessBlock

| Field | Type | Notes |
|---|---|---|
| `ShipDisposition` | `string` | shippable vs non-shippable disposition. |
| `VerificationReadiness` | `string` | declared verify readiness. |
| `BlockingDiagnosticIds` | `string list` | non-empty ⇒ blocking. |
| `Counts` | map / record | declared roll-up counts (carried for the gate description). |
| `PerViewState` | map | per-view declared state (carried). |

**→ Readiness gate** (FR-009, research D3): a typed `Gates.Model.Gate` whose `Maturity` is a
`block-on-*` when `ShipDisposition` is non-shippable **or** `BlockingDiagnosticIds` is non-empty,
else an advisory `warn`. It participates in selection/severity/roll-up like any other gate.

---

## 5. GovernedReference (optional enrichment)

| Field | Type | Notes |
|---|---|---|
| `WorkItem` | `string` | declared work item identity. |
| `Paths` | `GovernedPath list` | work-item→path provenance. |

Used **only** to populate `SelectingPath` provenance on handoff gates when present; correctness
never depends on it (FR-010, research D3/D7).

---

## 6. Consumer outputs (the bridge to the verdict)

`Consumer.consume : HandoffRead list -> ConsumeResult` where

| Field | Type | Notes |
|---|---|---|
| `Gates` | `Gate list` | handoff gate registry entries (evidence + readiness + integrity). |
| `Selected` | `SelectedGate list` | the same gates pre-selected (research D3). |
| `Diagnostics` | `Diagnostic list` | version-mismatch / malformed / `autoSynthetic` / `staleEvidence`. |

The host unions `Gates` into the `GateRegistry` and `Selected` into `RouteResult.SelectedGates`
**before** `Ship.rollup` (ship/verify) / the gates.json+route.json projection (route).

**Diagnostic** (handoff-domain; not an F017 `FindingId`, research D5)

| Field | Type | Notes |
|---|---|---|
| `Cause` | `DiagnosticCause` | `VersionMismatch \| Malformed \| AutoSyntheticDeclared \| StaleEvidence`. |
| `Source` | `string` | the `readiness/<id>/...` path. |
| `Message` | `string` | descriptive, distinct per cause (SC-004). |

---

## 7. Reused existing types (no redefinition)

- `Kernel.Evidence.EvidenceState`, `Evidence.build`, `Evidence.effective` (F005).
- `Gates.Model.Gate`, `GateId`, `Maturity`, `Cost`, `DomainId`; `Route.Model.SelectedGate`,
  `SelectingPath`, `RouteResult` (F018/F019).
- `Enforcement` severity machinery + `Ship.rollup` (F023/F024) — used unchanged.
- `Config.Model.GovernedPath` (F014).

---

## Aggregation & determinism (FR-012, research D7)

Multiple `readiness/<id>/...` documents are loaded in ordinal `<id>` order; each is
parsed/mapped independently; their gates are unioned and sorted by `GateId`. Zero documents ⇒
empty `ConsumeResult` ⇒ the host fold is identity ⇒ byte-identical output (SC-003). An empty
evidence block with a present readiness block (or vice-versa) is valid — blocks are independent.
