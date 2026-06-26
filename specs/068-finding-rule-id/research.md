# Phase 0 Research: Per-Finding Rule Identity

All decisions are grounded in the existing code. No `NEEDS CLARIFICATION` markers remain.

## D1 — The rule-id representation and source prefixes

**Decision**: Introduce a single newtype `type RuleId = RuleId of string` with a **source-prefixed** wire
representation, exposed via `ruleIdToken: RuleId -> string`. The prefix names the rule's source class and is
followed by the source's own stable id:

| Source | Constructor | Wire token (`ruleIdToken`) | Underlying id |
|---|---|---|---|
| Catalog typed gate | `RuleIdentity.gate (gateIdValue g)` | `gate:<domain>:<check>` | `Gates.gateIdValue` (already `"<domain>:<check>"`) |
| Kernel boundary finding | `RuleIdentity.boundary (findingIdToken id)` | `boundary:unknownGovernedPath` etc. | `Findings.findingIdToken` |
| Surface check finding | `RuleIdentity.surface domain code` | `surface:<domain>:<code>` | `SurfaceFinding.Domain` + `.Code` |
| Release-rule finding | `RuleIdentity.release (kindToken k)` | `release:<kind>` | `ReleaseRuleKind` token |
| Unattributable (FR-010) | `RuleIdentity.unattributed reason` | `unattributed:<reason>` | disclosed marker (see D6) |

**Rationale**: A flat newtype mirrors the existing `GateId`/`RuleHash`/`FindingId` precedents and keeps the
projection writers trivial (`w.WriteString("ruleId", ruleIdToken rid)`). The **prefix** is what makes a
boundary id provably distinguishable from a catalog-gate id (FR-008) using only the string — no separate "kind"
field is needed, and a downstream consumer can both group by full id and discriminate by prefix. Each underlying
id (`gateIdValue`, `findingIdToken`) is already the stable, injective wire token its module guarantees, so the
rule id inherits that stability for free and invents no parallel id scheme.

**Alternatives considered**:
- *Reuse the Kernel's `RuleId` (`FS.GG.Governance.Kernel`)*: rejected — the Kernel's `RuleId` identifies
  forward-chaining inference rules in the published contract; it is **not** carried to emitted findings (gates,
  boundary findings, surface findings, release findings have no Kernel `RuleId`). Reusing the name would conflate
  two distinct concepts and pull a heavy Kernel dependency into every projection.
- *A structured DU (`RuleId = Gate of GateId | Boundary of FindingId | …`)*: rejected — it would force every
  projection to depend on every source-type project and re-export a discriminated union into the wire layer,
  for no gain over a prefixed string that consumers can already parse.
- *Emit an unprefixed id and add a separate `ruleSource` field*: rejected — two fields where one suffices; the
  prefix already carries the source class, and a single self-describing token is the more honest "one consistent
  representation across surfaces" (FR-006).

## D2 — Where the id is derived (the projection edge), and why no signature changes

**Decision**: Derive the rule id **inside the three projection writers**, from the source value each writer
already receives — `AuditJson.writeItem` (over `EnforcedItem`/`EnforcedItemId`), `VerifyJson.writeItem` + the
`surfaceChecks` element writer (over `SurfaceFinding`), and `RouteJson.writeFinding` (over
`UnknownGovernedPathFinding`) + `writeSelectedGate` (over the gate). The new `FS.GG.Governance.RuleIdentity`
leaf is the only added dependency; the projection **function signatures do not change**.

**Rationale**: Each projection already holds everything needed to name the rule: `EnforcedItemId` is
`GateItem of GateId | FindingItem of FindingId * GovernedPath`; route findings are `UnknownGovernedPathFinding`
(carrying `Id: FindingId`); verify's `surfaceChecks` elements are `SurfaceFinding` (carrying `Domain`, `Code`).
Deriving at the edge means the cores (`Ship.rollup`, the finding classifiers, the dispatch) stay byte-for-byte
untouched, the change is contained to four small writer edits, and no caller has to thread a new argument
(keeping `ofShipDecision`/`ofVerifyDecisionWithPreview`/`ofRouteResultWithProductSurfaces` stable — a Tier-1
change with the smallest possible blast radius).

**Alternatives considered**:
- *Attach a `RuleId` field to each finding/`EnforcedItem` core type*: rejected — it would change multiple core
  `.fsi`s and their surface-drift baselines, ripple through every constructor and test, and re-bless many more
  goldens, all to carry a value that is a pure function of fields already present. The edge derivation is the
  minimal, equivalent change.
- *A god-module `RuleIdentity` that depends on Gates+Findings+SurfaceChecks+ReleaseRules and exposes `ofGate`/
  `ofFinding`/…*: rejected — it would invert the dependency direction (a leaf depending on four upstreams) and
  risk cycles; instead `RuleIdentity` stays dependency-free and each projection (which already references its
  source types) calls the matching prefixed constructor with the source's own token string.

## D3 — Per-object field placement and the byte-identity / golden strategy

**Decision**:
1. Emit `ruleId` as the **first field after `id`** in each per-finding/per-item object, since it is identity
   metadata and reads naturally beside the item's own id. Existing fields keep their relative order; the new
   field is inserted directly after `id`, not interleaved among the enforcement fields (the nested `enforcement`
   object is untouched).
   - audit.json item: `kind`, `id`, **`ruleId`**, [`path`], `enforcement`, [`cacheEligibility`], [`execution`].
   - verify.json item: same shape as audit; `surfaceChecks` element gains `ruleId` after its existing leading id
     field (per the F24 `surfaceChecks` contract order).
   - route.json finding: `id`, **`ruleId`**, `path`, `zone`, `message`; selected gate gains `ruleId` after `id`.
2. The **no-findings** path is byte-identical by construction: no finding ⇒ no object ⇒ no `ruleId`. Freeze (or
   reuse) the existing empty-case goldens as the unchanged anchors (`verify.no-declaration.json`, the empty
   `route.json`/`ship.json` goldens) and assert they do not drift (FR-007, SC-003).
3. **Re-bless** every finding-bearing golden (`verify-surfacechecks.json`, the inline `VerifyJson` golden, any
   route/ship finding goldens) deliberately, in one commit, to record the additive `ruleId` field.

**Rationale**: The repo's projections are hand-driven `Utf8JsonWriter` writes in fixed source order, so field
order is whatever the code writes (no serializer to fight). Inserting `ruleId` right after `id` preserves the
relative order of all pre-existing fields (the contract's "field ordering unchanged" reading) while keeping the
two identity tokens adjacent. The empty-case anchors are the real regression risk and are frozen
pre-change so the no-regression claim is falsifiable, mirroring the 067 golden strategy.

**Alternatives considered**:
- *Append `ruleId` as the trailing field of each object* (the F045/F052 additive-append precedent): acceptable
  and equally byte-safe, but it scatters the two identity tokens (`id` … `ruleId`) to opposite ends of the
  object; placing `ruleId` next to `id` is the more legible contract. Either is recorded in the contract; the
  plan picks after-`id`.

## D4 — The rule-id → rule-hash anchor (FR-004 / SC-002) without a new hashing scheme

**Decision**: The rule-id → rule-hash mapping anchors to the **existing catalog-wide `RuleHash`**
(`FS.GG.Governance.FreshnessKey.Model.RuleHash` — the SHA-256 over the repo's `.fsgg/*.yml` rule pack, sensed by
`CacheEligibilityCommand.Interpreter.senseCatalogHash`). FR-004/SC-002 are satisfied as follows: (a) the rule
**id** is invariant across profile/mode (D5), proven directly in the emitted JSON; (b) the catalog **rule hash**
is content-of-the-rule-pack and carries no profile/mode input, so it is invariant by construction. The mapping
"`RuleId` → `RuleHash`" is therefore the constant map from every emitted id to the single catalog hash of the
run, which is provably identical across every profile/mode combination. **No per-rule hash is introduced** and
**no per-finding hash is emitted** — the id appears per-finding; the hash anchor is the catalog hash already
present in the freshness/provenance vocabulary.

**Rationale**: The spec's Assumptions are explicit: *"The rule hash already exists (used by freshness/provenance)
and is reused as the anchor for the rule-id → rule-hash mapping; this feature does not invent a new hashing
scheme."* A search confirmed there is **no** per-rule content hash anywhere — `RuleHash` is catalog-wide and
`ContractEntry` carries a Kernel `RuleId` but no hash. The integrity guarantee FR-004 is about is *"profiles
never alter rule hashes"*; the catalog hash demonstrably does not depend on profile/mode, so the guarantee holds
at the granularity the codebase actually has. SC-002 is verified at the value/sense layer (the sensed `RuleHash`
is identical across profile/mode runs over one fixture) together with the JSON-level id invariance, rather than
by emitting a redundant per-finding hash.

**Alternatives considered**:
- *Invent a per-rule content hash and emit it per finding*: rejected — explicitly out of scope per the
  Assumptions ("does not invent a new hashing scheme"), and it would enlarge the contract beyond the additive
  `ruleId` the spec asks for.
- *Emit the catalog `RuleHash` on every finding object*: rejected — redundant (it is constant within a run and
  already available in the freshness/provenance sections) and would bloat each finding; the id is the per-finding
  datum, the hash is a run-level anchor.

## D5 — Profile / mode / message invariance (FR-002, FR-003, FR-009)

**Decision**: The rule id is a **pure function of the rule's structural identity only**: the gate's `GateId`,
the boundary finding's `FindingId`, the surface finding's `Domain`+`Code`, or the release finding's
`ReleaseRuleKind`. It reads **none** of: effective severity, profile, run mode, maturity, reason/message text,
input ordering, clock, host, or environment.

**Rationale**: All four source ids are already documented as stable, deterministic, message-independent tokens
(e.g. `GateId` is "a deterministic, injective function of the declared facts … never positional, time-derived,
or random"; `findingIdToken` is "deterministic and total"). Because the derivation consumes only these and a
constant prefix, FR-002 (determinism), FR-003 (profile/mode invariance), and FR-009 (message invariance) all
follow structurally and are checkable by an all-profiles × all-modes sweep (SC-002, SC-004) plus a
message-perturbation test (changing a finding's `Message`/`Reason` leaves `ruleId` unchanged).

**Alternatives considered**: none — any dependence on profile/mode/message would directly violate the FRs.

## D6 — The honest `unattributed` marker (FR-010)

**Decision**: Provide `RuleIdentity.unattributed: reason -> RuleId` yielding `unattributed:<reason>`, reserved
for a finding that genuinely cannot be attributed to a rule of record. In the **current** projections every
emitted finding has a concrete source (gate / boundary / surface / release), so this marker is **never produced**
on any normal path; a test asserts that no projection emits an `unattributed:` token for the standard fixtures.

**Rationale**: FR-010 + Constitution VI require that a missing attribution be **disclosed**, never a silent pass
or a guessed/empty id. Modelling the marker explicitly (rather than emitting `""`) makes the honest-failure path
a first-class, greppable value and future-proofs a later finding source that might lack a rule of record, while
the assertion keeps today's output free of it.

**Alternatives considered**:
- *Emit an empty string when unattributable*: rejected — FR-008/FR-010 forbid an empty id; an empty field reads
  as "no rule" rather than "attribution unknown", the exact silent-failure Constitution VI bars.

## D7 — The new leaf project's dependency direction (no cycle)

**Decision**: `FS.GG.Governance.RuleIdentity` references **only** `FSharp.Core` — no governance project. The
prefixed constructors take plain `string` (the source's already-computed token) rather than the source type, so
the module never needs to know about `Gates`/`Findings`/`SurfaceChecks`/`ReleaseRules`. Each projection, which
already references its source types, computes the token (`gateIdValue g`, `findingIdToken f.Id`, …) and passes it
to the matching constructor. The dependency direction stays one-way: `{AuditJson, VerifyJson, RouteJson}` →
`RuleIdentity`, with no back-edge.

**Rationale**: A dependency-free leaf is impossible to cycle and is referenceable from anywhere, matching how
small token/identity helpers already sit at the bottom of the graph. It also keeps `RuleIdentity` independently
packable like its sibling leaf libraries.

**Alternatives considered**:
- *Place the derivation helpers next to each source type (`Gate.ruleId`, etc.)*: viable and clean, but it would
  spread the `gate:`/`boundary:`/… prefix convention across four projects and make "one consistent
  representation" (FR-006) harder to keep in sync; centralising the prefixes in the leaf keeps the wire grammar
  in one file.

## Summary of reuse (no new core, one new dependency-free leaf)

| Concern | Reused from | Status |
|---|---|---|
| Gate identity token | `Gates.gateIdValue` | existing |
| Boundary finding token | `Findings.findingIdToken` | existing |
| Surface finding domain/code | `SurfaceChecks.Model.SurfaceFinding` | existing |
| Release finding kind token | `ReleaseRules` kind token | existing |
| Rule-hash anchor | `FreshnessKey.Model.RuleHash` (catalog SHA-256) | existing, profile/mode-invariant |
| Enforced item shape (audit/verify) | `Ship.Model.EnforcedItem`/`EnforcedItemId` | existing |
| JSON writers (3) | `AuditJson`/`VerifyJson`/`RouteJson` `writeItem`/`writeFinding`/`writeSelectedGate` | edited additively |
| **Rule id newtype + prefixes + token** | **`FS.GG.Governance.RuleIdentity` (NEW leaf, FSharp.Core only)** | **new** |
