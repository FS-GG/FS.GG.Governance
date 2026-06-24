# Phase 1 Data Model: Release-Facts Sensing

**Feature**: `054-release-facts-sensing` | **Date**: 2026-06-24

This row adds three modules to a new library `FS.GG.Governance.ReleaseFactsSensing`:

- **`Model`** — the caller inputs (`ReleaseExpectations`, `SourceLayout`), the structured
  per-family recovered evidence, the gathered `RecoveredEvidence` bundle, the observed-evidence
  `ReleaseSnapshot`, and the combined `SensedRelease` output. It **reuses** the F053 `ReleaseFacts` /
  `FactState` / `ReleaseRuleKind` and the F014 `SurfaceId` — no new fact or family vocabulary.
- **`Sensing`** — the **pure** derivation `deriveFacts : ReleaseExpectations -> RecoveredEvidence -> SensedRelease`.
- **`Interpreter`** — the **edge**: the injected `RepositoryPort`, the production `realPort`, `gather`,
  and the single composition `senseRelease`.

The flow mirrors F016 Snapshot exactly: **`RepositoryPort` (impure reads) → `RecoveredEvidence`
(gathered) → `deriveFacts` (pure) → `SensedRelease`**, with `Facts` going straight into F053 `evaluate`.

---

## Reused types (not redefined)

| Type | Source | Role here |
|------|--------|-----------|
| `ReleaseRuleKind` (`VersionBump`…`Provenance`) | F053 `ReleaseRules.Model` | the closed six-family key, used everywhere |
| `FactState` (`Met` / `Unmet` / `Unrecoverable`) | F053 `ReleaseRules.Model` | the per-family classification this row produces |
| `ReleaseFacts` (`{ States: Map<ReleaseRuleKind, FactState> }`) | F053 `ReleaseRules.Model` | the **output** facts value, handed straight to `evaluate` |
| `releaseRuleKindOrdinal` / `releaseRuleKindToken` | F053 `ReleaseRules.Release` | deterministic family ordering + diagnostic tokens |
| `SurfaceId` | F014 `Config.Model` | the caller-supplied governed identity (no hardcoded id) |

---

## Input entities (caller-supplied — D5, FR-011)

### `ReleaseExpectations`
The product-neutral criteria that define "met" per family. Each family's criterion is **optional**: an
absent criterion ⇒ that family is `Unrecoverable` (fail-safe, never an assumed `Met`).

| Field | Type | Meaning |
|-------|------|---------|
| `Surface` | `SurfaceId` | the governed release surface (carried onto the snapshot) |
| `VersionBaseline` | `string option` | the version the declared version must be bumped **past** |
| `RequiredMetadataFields` | `string list option` | the field names the package metadata must contain |
| `ExpectedPins` | `Map<string,string> option` | the template→version pins that must be resolved |
| `RequiredPublishPosture` | `string list option` | the posture tokens the publish plan must observe |
| `RequiredTrustedPublishing` | `string list option` | the trusted-publishing config tokens required |
| `RequiredProvenance` | `string list option` | the provenance/attestation tokens required |

### `SourceLayout`
The per-family **relative paths** the `realPort` reads — caller-supplied so the library hardcodes no
directory layout. One path per family.

| Field | Type |
|-------|------|
| `VersionPath`, `MetadataPath`, `PinsPath`, `PublishPlanPath`, `TrustedPublishingPath`, `ProvenancePath` | `string` |

---

## Recovered-evidence entities (structured by the port — D3)

The structured value each port read function yields on success. These are what the pure core compares
against the expectation; they also feed the snapshot.

| Type | Fields | Family |
|------|--------|--------|
| `VersionEvidence` | `{ Declared: string }` | VersionBump |
| `MetadataEvidence` | `{ PresentFields: string list }` | PackageMetadata |
| `PinsEvidence` | `{ Resolved: Map<string,string> }` | TemplatePins |
| `PostureEvidence` | `{ Observed: string list }` | PublishPlan, TrustedPublishing, Provenance |

### `RecoveredEvidence` (the gathered bundle — Snapshot's `RawSensing` precedent)
One `Result<_, string>` per family; `Error` carries the read/parse failure reason (⇒ `Unrecoverable`).

| Field | Type |
|-------|------|
| `Version` | `Result<VersionEvidence, string>` |
| `Metadata` | `Result<MetadataEvidence, string>` |
| `Pins` | `Result<PinsEvidence, string>` |
| `PublishPlan` | `Result<PostureEvidence, string>` |
| `TrustedPublishing` | `Result<PostureEvidence, string>` |
| `Provenance` | `Result<PostureEvidence, string>` |

---

## Output entities

### Snapshot evidence (observed + comparison detail — US2)
Each is `Some` when evidence was recovered (`Met`/`Unmet`), `None` when `Unrecoverable`. All collections
are emitted sorted (D7).

| Type | Fields | Notes |
|------|--------|-------|
| `VersionFact` | `{ Observed: string; Baseline: string }` | both the version read and the baseline compared |
| `MetadataFact` | `{ Present: string list; Missing: string list }` | sorted; `Missing = required \ present` |
| `PinsFact` | `{ Resolved: (string*string) list; Expected: (string*string) list; Drifted: string list }` | key-sorted assoc lists |
| `PostureFact` | `{ Observed: string list; Required: string list; Missing: string list }` | sorted; reused by the three posture families |

### `SensingDiagnostic`
Why a family is `Unrecoverable` (absent source, unreadable, unparseable, or absent expectation).

| Field | Type |
|-------|------|
| `Family` | `ReleaseRuleKind` |
| `Reason` | `string` (product-neutral) |

### `ReleaseSnapshot`
The typed observed-evidence value behind the facts (US2). Diagnostics ordered by `releaseRuleKindOrdinal`.

| Field | Type |
|-------|------|
| `Surface` | `SurfaceId` |
| `Version` | `VersionFact option` |
| `Metadata` | `MetadataFact option` |
| `Pins` | `PinsFact option` |
| `PublishPlan` | `PostureFact option` |
| `TrustedPublishing` | `PostureFact option` |
| `Provenance` | `PostureFact option` |
| `Diagnostics` | `SensingDiagnostic list` |

### `SensedRelease` (the combined output — D8)
| Field | Type | Role |
|-------|------|------|
| `Facts` | `ReleaseFacts` | the **F053 input value**, handed straight to `evaluate` (FR-002, SC-001) |
| `Snapshot` | `ReleaseSnapshot` | the auditable observed evidence (US2) |

---

## The derivation (pure — `Sensing.deriveFacts`)

For each family, in `ReleaseRuleKind` order:

```
match (expectation-for-family, recovered-for-family) with
| None,    _            -> Unrecoverable, snapshot None, diagnostic "no expectation declared for <family>"
| _,       Error reason -> Unrecoverable, snapshot None, diagnostic "<family> evidence unrecoverable: <reason>"
| Some e,  Ok evidence  ->
    if satisfies e evidence then Met,   snapshot (Some observed)
    else                         Unmet, snapshot (Some observed)
```

- `satisfies` per family is the D6 comparison (version dotted-numeric "bumped past"; metadata field
  containment; pin resolution; posture subset).
- `Facts.States` is assembled as `Map.ofList` over all six `(kind, state)` pairs — **always six**.
- The snapshot's per-family `option` is `Some` exactly when the state is `Met` or `Unmet`.
- `deriveFacts` is **total** (defined for every expectation/recovered combination, including all-absent
  ⇒ all-`Unrecoverable`), **never throws**, and **deterministic** (D7).

## The edge (impure — `Interpreter`)

- `realPort repoDir layout` — builds a `RepositoryPort` whose six read functions read the
  `layout` paths under `repoDir` via `System.IO`, parse them into the structured evidence, and return
  `Error` on a missing/unreadable/unparseable file. **Only place the feature touches the filesystem**;
  no process, no socket, no provider SDK (D4).
- `gather port` — runs the six read functions, **catching any thrown exception** and reifying it as
  `Error` (FR-004), and bundles a `RecoveredEvidence`.
- `senseRelease port expectations` — `gather port |> Sensing.deriveFacts expectations`. The single
  composition the future `fsgg release` host row wires (sense → F053 `evaluate` → exit code). TOTAL and
  SAFE: never throws, always returns all six families, reaches no network.

## Hand-off to F053

```
let sensed   = Interpreter.senseRelease port expectations      // this row
let findings = Release.evaluate rules sensed.Facts             // F053, unchanged — SC-001
let decision = Release.rollup findings                          // F053, unchanged
```
`sensed.Facts` is the F053 `ReleaseFacts` type, so it type-checks into `evaluate` with no adaptation.
