# Phase 1 Data Model: Per-Finding Rule Identity

This feature adds **one** new pure type — `RuleId` — in a new dependency-free leaf
(`FS.GG.Governance.RuleIdentity`). Every source value the id is derived from already exists. No core finding,
enforcement, or projection type changes shape; the projections gain a derived `ruleId` field at emit time.

## 1. New entity — `RuleId` (`FS.GG.Governance.RuleIdentity`)

```fsharp
namespace FS.GG.Governance.RuleIdentity

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RuleIdentity =

    /// A stable, deterministic identifier for the rule (or typed gate) that produced a finding.
    /// The wrapped string is the source-prefixed wire token; the prefix names the rule's source class and
    /// makes a boundary id distinguishable from a catalog-gate id (FR-008). Profile/mode/message-independent.
    type RuleId = RuleId of string

    /// Catalog typed gate → `gate:<domain>:<check>`. `gateId` is the existing `gateIdValue` string.
    val gate: gateId: string -> RuleId

    /// Kernel boundary finding → `boundary:<findingIdToken>`. `findingToken` is `findingIdToken id`.
    val boundary: findingToken: string -> RuleId

    /// Surface-check finding → `surface:<domain>:<code>`.
    val surface: domain: string -> code: string -> RuleId

    /// Release-rule finding → `release:<kind>`. `kindToken` is the `ReleaseRuleKind` wire token.
    val release: kindToken: string -> RuleId

    /// Disclosed marker for a finding with no rule of record (FR-010) → `unattributed:<reason>`.
    /// Never produced on a normal projection path (asserted by test); never an empty id.
    val unattributed: reason: string -> RuleId

    /// The stable wire token (the prefixed string), for JSON emission, messages, and tests. Total.
    val ruleIdToken: id: RuleId -> string
```

- **Invariants**: pure and total; no I/O, clock, host, env, or ordering input (FR-002). The token is a function
  of the prefix + the supplied source token only (FR-003, FR-009). The five prefixes are disjoint, so any two
  ids from different source classes are distinguishable by their leading segment (FR-008).
- **Constitution II**: `RuleIdentity.fsi` is the sole declaration of this surface; `RuleIdentity.fs` carries no
  access modifiers. A new surface-drift baseline is added for the module.

## 2. Reused source entities (no change)

| Type | Project | Field(s) used | Yields |
|---|---|---|---|
| `Gates.Model.GateId` (+ `gateIdValue`) | `Gates` | the `"<domain>:<check>"` string | `gate` rule id |
| `Findings.Model.FindingId` (+ `findingIdToken`) | `Findings` | `UnknownGovernedPath` / `UnknownProtectedBoundaryPath` token | `boundary` rule id |
| `SurfaceChecks.Model.SurfaceFinding` | `SurfaceChecks` | `Domain`, `Code` | `surface` rule id |
| `ReleaseRules.Model.ReleaseRuleKind` (+ kind token) | `ReleaseRules` | the kind token | `release` rule id |
| `Ship.Model.EnforcedItem` / `EnforcedItemId` | `Ship` | `GateItem of GateId` \| `FindingItem of FindingId * GovernedPath` | dispatch to `gate`/`boundary` |
| `FreshnessKey.Model.RuleHash` | `FreshnessKey` | catalog SHA-256 | the rule-id → rule-hash anchor (D4) |

None of these gain or lose a field. The derivation reads only the fields above.

## 3. Per-source derivation (the mapping)

```
EnforcedItem (audit.json / verify.json items)
  GateItem g                       → RuleIdentity.gate (gateIdValue g)            → "gate:<domain>:<check>"
  FindingItem (fid, _path)         → RuleIdentity.boundary (findingIdToken fid)   → "boundary:<token>"

SurfaceFinding (verify.json surfaceChecks element)
  f                                → RuleIdentity.surface (domainToken f.Domain) (codeToken f.Code)
                                                                                  → "surface:<domain>:<code>"

UnknownGovernedPathFinding (route.json findings)
  f                                → RuleIdentity.boundary (findingIdToken f.Id)  → "boundary:<token>"

Selected gate (route.json gates)
  g                                → RuleIdentity.gate (gateIdValue g.Id)         → "gate:<domain>:<check>"

ReleaseFinding (if/where emitted via an EnforcedItem finding)
  k                                → RuleIdentity.release (kindToken k)           → "release:<kind>"
```

The dispatch is exhaustive over the closed `EnforcedItemId`/`FindingId`/source DUs (no wildcard), so a future
source case is a compile error here, never a silently mis-prefixed or dropped id.

## 4. Where the field appears in each projection (D3)

| Projection | Writer (file) | Object | `ruleId` placement |
|---|---|---|---|
| `audit.json` | `AuditJson.writeItem` | each enforced item (gate & finding) | after `id` |
| `verify.json` | `VerifyJson.writeItem` | each enforced item | after `id` |
| `verify.json` | the `surfaceChecks` element writer | each `SurfaceFinding` | after its leading id field |
| `route.json` | `RouteJson.writeFinding` | each boundary finding | after `id` |
| `route.json` | `RouteJson.writeSelectedGate` | each selected gate | after `id` |

The nested `enforcement` object (`baseSeverity`, `maturity`, `mode`, `profile`, `effectiveSeverity`, `reason`)
is **not** touched — `ruleId` sits at the finding/item level beside `id`, the other identity datum.

## 5. Determinism & byte-identity invariants

- **No-findings byte-identity (FR-007, SC-003)**: no finding ⇒ no item/element ⇒ no `ruleId`. The empty-case
  goldens (`verify.no-declaration.json`, the empty route/ship goldens) stay byte-identical; `schemaVersion`s
  unchanged.
- **Determinism (FR-002, SC-001)**: the id is a pure function of stable source tokens; two runs over identical
  inputs emit byte-identical `ruleId`s.
- **Profile/mode invariance (FR-003, SC-002, SC-004)**: the derivation reads no profile/mode/effective-severity;
  the same finding under any profile/mode emits the identical `ruleId`. A relaxing profile changes only the
  nested `enforcement.effectiveSeverity` (already separate from `baseSeverity`) and never drops the finding.
- **Rule-hash anchor (FR-004, SC-002)**: the catalog `RuleHash` is content-of-rule-pack, independent of
  profile/mode; the constant map `RuleId → RuleHash` is identical across all profile/mode combinations (D4).
- **Message invariance (FR-009)**: changing a finding's `Message`/`Reason` does not change its `ruleId`.
- **Cross-surface identity (FR-006, SC-005)**: a finding common to `verify` and `ship` derives from the same
  source value through the same constructor, so the `ruleId` token matches across surfaces.
- **Honest attribution (FR-008, FR-010, SC-006)**: every emitted id is non-empty and source-prefixed; no
  `unattributed:` token appears on any normal path (asserted).
