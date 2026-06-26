# Contract: Additive Per-Finding `ruleId`

**Applies to**: `audit.json` (`fsgg ship`, `fsgg.audit/v2`), `verify.json` (`fsgg verify`, `fsgg.verify/v1`),
`route.json` (`fsgg route`, `fsgg.route/v2`).

**Posture**: purely additive. No `schemaVersion` bump. No existing field, value, ordering, verdict, or exit code
changes. The output is **byte-identical to the pre-feature output for any input that produces no findings**.

## C1 — Byte-identical when there are no findings (FR-007, SC-003)

When a document contains no per-finding/per-item objects of a given kind, **no `ruleId` field appears anywhere**
for that kind, and the document is byte-for-byte identical to the pre-feature output. The empty-case goldens are
frozen as the regression anchors and MUST NOT drift:

- `verify.json`: `verify.no-declaration.json` (and any empty `surfaceChecks` golden).
- `route.json`: the no-finding route golden.
- `audit.json` / `ship.json`: the no-finding ship golden.

## C2 — The `ruleId` field: value grammar and placement

**Value grammar** — a non-empty, source-prefixed token (`ruleIdToken`):

```
ruleId      = gate-id | boundary-id | surface-id | release-id | unattributed-id
gate-id     = "gate:"        <domain> ":" <check>      ; from Gates.gateIdValue
boundary-id = "boundary:"    <findingIdToken>          ; e.g. "boundary:unknownGovernedPath"
surface-id  = "surface:"     <domain> ":" <code>       ; from SurfaceFinding.Domain + .Code
release-id  = "release:"     <kind>                    ; from ReleaseRuleKind token
unattributed-id = "unattributed:" <reason>            ; disclosed marker only (C3.4); never empty
```

The five prefixes are disjoint: a consumer discriminates the source class by the leading segment and groups by
the full token (FR-008, SC-006).

**Placement** — `ruleId` is emitted as the **first field after `id`** in each object; all pre-existing fields
keep their relative order; the nested `enforcement` object is unchanged:

- `audit.json` enforced item: `kind`, `id`, **`ruleId`**, [`path` for findings], `enforcement`,
  [`cacheEligibility` for gates], [`execution`].
- `verify.json` enforced item: same shape as audit. `surfaceChecks` element: **`ruleId`** follows the element's
  leading id field (per the F24 `surfaceChecks` order), before the existing detail fields.
- `route.json` finding: `id`, **`ruleId`**, `path`, `zone`, `message`. Selected gate: `id`, **`ruleId`**, then
  the existing gate fields.

## C3 — Invariance guarantees

1. **Deterministic (FR-002, SC-001)**: identical inputs ⇒ byte-identical `ruleId`. No clock/host/env/order input.
2. **Profile/mode-invariant (FR-003, SC-002, SC-004)**: the same finding under any profile or run mode carries
   the identical `ruleId`. A relaxing profile may change `enforcement.effectiveSeverity` but never `ruleId` and
   never drops the finding.
3. **Message-invariant (FR-009)**: changing a finding's message/reason text does not change its `ruleId`.
4. **Cross-surface stable (FR-006, SC-005)**: a finding common to `verify` and `ship` carries the same `ruleId`
   on both surfaces.
5. **Honest (FR-008, FR-010, SC-006)**: every emitted `ruleId` is non-empty and source-prefixed; the
   `unattributed:` token is reserved for a finding with no rule of record and MUST NOT appear on any normal path.

## C4 — Rule-id → rule-hash anchor (FR-004, SC-002)

The rule-id → rule-hash mapping anchors to the **existing catalog-wide `RuleHash`** (the `.fsgg/*.yml` rule-pack
SHA-256 used by freshness/provenance). For a given fixture, every emitted `ruleId` maps to the run's single
catalog `RuleHash`, which is content-of-rule-pack and therefore **identical across every profile/mode
combination**. No per-rule hash is introduced and no per-finding hash is emitted; the per-finding datum is the
`ruleId`, the hash is the run-level anchor already present in the freshness/provenance vocabulary.

## C5 — Test obligations

- Empty-case byte-identity goldens unchanged (C1).
- Each finding-bearing golden re-blessed to show `ruleId` at the contracted position (C2).
- All-profiles × all-modes sweep over one finding-bearing fixture: `ruleId` set identical, no finding dropped,
  only `effectiveSeverity` may differ (C3.2, SC-002, SC-004).
- Message-perturbation test: altering `Message`/`Reason` leaves `ruleId` unchanged (C3.3).
- Cross-surface test: a finding in both `verify.json` and `audit.json` has matching `ruleId` (C3.4, SC-005).
- Negative test: no projection emits an `unattributed:` token for the standard fixtures (C3.5, SC-006).
- Sensed catalog `RuleHash` identical across profile/mode runs (C4, SC-002).
