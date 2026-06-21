# Phase 1 Data Model: Freshness Key Computation Core

All types live in `FS.GG.Governance.FreshnessKey.Model` (sole public declaration: `Model.fsi`). They are
product-neutral, comparable values carrying no raw bytes, host paths, clock readings, or product vocabulary.
Reused F014 newtypes are `open`ed from `FS.GG.Governance.Config.Model`; nothing in `Config`/`Gates`/`Snapshot`
is modified (FR-009). Names are the recommended spelling; minor identifier adjustments at implementation are
allowed as long as the contracts in `contracts/` hold.

## Reused vocabulary (from `Config.Model`, F014 — verbatim)

| Type | Form | Role in this feature |
|---|---|---|
| `CheckId` | `CheckId of string` | The gate-identity check id (one of the carried freshness-identity fields). |
| `DomainId` | `DomainId of string` | The gate-identity owning domain. |
| `CommandId` | `CommandId of string` | The gate's declared command id; optional (absent ⇒ no command). |
| `EnvironmentClass` | `Local \| Ci \| LocalOrCi \| Release` | The environment class the gate runs in. |

These are exactly the newtypes the F018 `Gates.Model.FreshnessKey` is built from. The edge reads them off a
gate's carried key; this core consumes them as data.

## New opaque newtypes (this feature)

Each is single-case `of string`, opaque and comparable; the actual digests/versions/revisions are computed
at the edge and supplied as data (FR-008). No validation, no parsing — an empty string is a literal value
(FR-011).

| Type | Form | Represents |
|---|---|---|
| `RuleHash` | `RuleHash of string` | A supplied digest of the rule that produced the evidence. |
| `ArtifactHash` | `ArtifactHash of string` | A supplied digest of one artifact the evidence covers. |
| `CommandVersion` | `CommandVersion of string` | A supplied version stamp of the gate's command. |
| `GeneratorVersion` | `GeneratorVersion of string` | A supplied version stamp of the generator/tool. |
| `Revision` | `Revision of string` | A resolved revision identity (base or head); the edge maps `Snapshot.Model.CommitId` → `Revision` (research D3). |

## Key entity — `FreshnessInputs`

The closed set of inputs that determine reuse (FR-001). A record so every category is named and
type-checked; covered artifacts are a list compared as a **set** (FR-004).

```text
type FreshnessInputs =
    { // ── carried gate identity (F014 newtypes, research D1/D5) ──
      Check: CheckId
      Domain: DomainId
      Command: CommandId option          // None ⇒ the gate declares no command
      Environment: EnvironmentClass
      // ── Phase-11 additions ──
      RuleHash: RuleHash
      CoveredArtifacts: ArtifactHash list // compared as a SET: order + duplication ignored (FR-004)
      CommandVersion: CommandVersion option // None ⇔ Command = None (no command ⇒ no command version)
      GeneratorVersion: GeneratorVersion
      Base: Revision
      Head: Revision }
```

Notes:
- **Cost is deliberately absent** (research D5): cost does not affect reuse validity.
- `Command` / `CommandVersion` are `None` together for a command-less gate. The contracts treat them as two
  separate categories so a test can flip either independently; the edge keeps them consistent.
- A `CoveredArtifacts = []` value is valid (zero covered artifacts; Edge case) and is not an error.

## Key entity — `Key`

The deterministic, byte-stable, comparable fingerprint produced from `FreshnessInputs`.

```text
type Key = Key of string   // the canonical encoding (contracts/freshness-key-format.md)
```

> **Naming note (avoid confusion).** This computed-fingerprint type is `Key` — **not** `FreshnessKey`.
> The name `FreshnessKey` is taken twice in the wider codebase: it is the operations **module** here
> (`FS.GG.Governance.FreshnessKey`) and it is also F018's carried MVP identity record
> (`Gates.Model.FreshnessKey`, the check/domain/cost/environment/command the gate carries). Those are
> different concepts from this computed `Key`. The `Model.fsi` doc comment for `Key` MUST state this
> distinction explicitly so a reader is never unsure which "key" is meant.

- Equal `Key`s ⇒ "same world, reuse permitted"; different ⇒ "reuse forbidden".
- The wrapped string is the canonical tagged, length-prefixed rendering (research D2), so equality is exact
  byte equality and the value is portable across runs/machines (what the later cache row stores/looks up).
- Inspectable: the structure is parseable, and `diff` (below) explains a non-match over the inputs.

## Key entity — `InputCategory`

The closed enumeration of comparable categories, returned by `diff` to name what changed (FR-007, the
no-hide requirement). One case per comparable field.

```text
type InputCategory =
    | CheckIdentity
    | DomainIdentity
    | CommandIdentity
    | EnvironmentClassCat
    | RuleHashCat
    | CoveredArtifactsCat
    | CommandVersionCat
    | GeneratorVersionCat
    | BaseRevisionCat
    | HeadRevisionCat
```

A stable token function (`categoryToken : InputCategory -> string`) renders each for `diff` output and
messages, mirroring F016's `*Token` helpers. It returns the **human-readable** vocabulary
(e.g. `RuleHashCat → "ruleHash"`, `CoveredArtifactsCat → "coveredArtifacts"`), which is deliberately
**distinct** from the terse internal key-encoding tags (`rule`, `art`, …) in
[contracts/freshness-key-format.md](./contracts/freshness-key-format.md). The committed token table is in
[contracts/freshness-key-api.md](./contracts/freshness-key-api.md).

## Relationships & invariants

- `compute : FreshnessInputs -> Key` is **total** and **deterministic**: defined for every value; identical
  inputs (covered artifacts compared as a set) always yield byte-identical keys (FR-002/FR-003/FR-004).
- `matches a b  ⇔  compute a = compute b` — bound by definition so the predicate and the key never disagree
  (FR-005).
- `diff a b = []  ⇔  matches a b` — the explainer is exhaustive and consistent with the predicate (FR-007,
  SC-005). When non-empty, `diff` lists exactly the categories whose values differ, in a fixed order.
- **Injective across categories** (FR-006): for any two inputs that place the same opaque string in different
  categories, `compute` yields different keys (guaranteed by the length-prefixed tagged encoding).
- **No I/O / no clock** (FR-008): the only data read is the argument; the result is independent of time,
  cwd, environment, filesystem, and git state (SC-006).

## State transitions

None. All three operations are pure value transforms; there is no mutable state, lifecycle, or workflow
(Principle IV N/A).
