# Phase 0 Research: Deterministic audit.json Projection (F025)

This row layers a **pure JSON projection** on top of the F024 `ShipDecision`, exactly as F020
(`route.json`) and F021 (`gates.json`) layered serialization on top of `RouteResult` / `GateRegistry`.
The technical questions are therefore the same eight the F020/F021 research resolved; each decision
below states what F025 chose and why, citing the established precedent where it simply replicates it.

No `NEEDS CLARIFICATION` markers remain after this document (see **Resolved Technical Context**).

## D1 — Project home: a new sibling library `FS.GG.Governance.AuditJson`

**Decision.** Create a new packable library `src/FS.GG.Governance.AuditJson` (with
`tests/FS.GG.Governance.AuditJson.Tests`), a sibling of `FS.GG.Governance.RouteJson` (F020) and
`FS.GG.Governance.GatesJson` (F021). It references **only** `FS.GG.Governance.Ship`; every other type
it renders (`EnforcementDecision`, `Severity`, `Maturity`, `RunMode`, `Profile` from Enforcement;
`GovernedPath`/`Maturity` from Config; `GateId`/`gateIdValue` from Gates; `FindingId`/`findingIdToken`
from Findings) arrives transitively through Ship.

**Rationale.** The constitution's Engineering Constraints require heavier capabilities (serialization)
to "layer on top in separate projects, not into the core." F024's `Ship` surface stays free of any
serialization concern, mirroring how F018's `Gates` stayed serialization-free under F021. A new library
keeps the dependency direction one-way (`AuditJson → Ship → {Enforcement, Route} → …`) and lets the
projection be packable on its own, exactly as RouteJson/GatesJson are.

**Alternatives considered.**
- *Add the projection into `FS.GG.Governance.Ship`.* Rejected: pulls `System.Text.Json` serialization
  into the pure rollup core, against the layering rule and breaking the F020/F021 precedent.
- *A single shared `…Json` library hosting all three projections.* Rejected: each projection is
  independently packable and versioned; merging them couples three contracts' release cadences and was
  already declined at F021.

## D2 — Serialization mechanism: hand-driven `System.Text.Json.Utf8JsonWriter`, no new package

**Decision.** Produce the document with a hand-driven `Utf8JsonWriter` over a `MemoryStream`, decoded
to a `string` — the identical `writeToString` shape used by `Kernel/Json.fs`, `RouteJson.fs`, and
`GatesJson.fs`. Default writer options (no indentation) give compact, deterministic bytes. **No new
`PackageReference`**: `System.Text.Json` is in the `net10.0` shared framework (FR-014).

**Rationale.** This is the proven repo mechanism for deterministic JSON; reusing it keeps the library
`System.*`/`FSharp.Core`-only and inherits the writer's JSON-escaping for free-text (`Reason`),
satisfying FR-012 without manual escaping.

**Alternatives considered.**
- *`JsonSerializer` with attribute/POCO mapping.* Rejected: field order would depend on reflection /
  attribute upkeep rather than an explicit, auditable call order; harder to prove byte-determinism
  (FR-007). Declined at F020/F021 for the same reason.
- *Hand-rolled string concatenation.* Rejected: re-implements escaping and invites injection/escaping
  bugs in the free-text `Reason`.

## D3 — Closed-enum wire tokens: local hidden helpers, exhaustive, no wildcard

**Decision.** AuditJson owns six hidden token helpers (absent from the `.fsi`, like GatesJson's
`costToken`/`maturityToken`): `verdictToken` (`pass`|`fail`), `basisToken` (`clean`|`blocked`),
`severityToken` (`advisory`|`blocking`), `maturityToken` (`observe`|`warn`|`blockOnPr`|`blockOnShip`|
`blockOnRelease`), `modeToken` (`sandbox`|`inner`|`focused`|`verify`|`gate`|`release`), and
`profileToken` (`light`|`standard`|`strict`|`release`). Each is an **exhaustive** `match` over its
closed DU with **no wildcard**, so a future case is a compile error here, never a silently mis-tokened
field. The two **identity** renderers are *reused*, not re-implemented: `gateIdValue` (Gates) and
`findingIdToken` (Findings); `GovernedPath` is unwrapped by pattern match.

**Rationale.** Enforcement's own token functions (`severityToken`/`modeToken`/`profileToken`) are
**hidden** in `Enforcement.fs` — they are absent from `Enforcement.fsi`, so they cannot be reused
across the assembly boundary. GatesJson hit the same wall and rolled its own hidden helpers; F025
follows that precedent. The token spellings are chosen to match the repo-wide vocabulary already
emitted by `Enforcement.fs`, `Kernel/Json.fs`, and `Cli.fs` (advisory/blocking, sandbox…release,
light…release), so audit.json reads consistently with every other artifact. Identity tokens **are**
public (`gateIdValue`, `findingIdToken`) and MUST be reused so the document re-derives nothing
(FR-004, FR-010).

**Alternatives considered.**
- *Expose Enforcement's token helpers in its `.fsi` and reuse them.* Rejected: widens an upstream
  public surface for a downstream serializer's convenience; the GatesJson precedent keeps wire tokens
  local to each projection (the wire vocabulary is the projection's contract, not Enforcement's).
- *A public shared token module.* Rejected: no such module exists; introducing one is out of scope and
  would couple three artifacts' token contracts.

## D4 — Contract shape: emit-only `ShipDecision -> string`, no round-trip parser

**Decision.** The public surface is exactly two members: `schemaVersion: string` and
`ofShipDecision: decision: ShipDecision -> string`. No reader/parser, no typed document record is
exposed (the writer plumbing is hidden).

**Rationale.** Identical to RouteJson/GatesJson. The consumers (CI, branch protection, agents) read the
JSON with their own tooling; this row owns *emission* only. A parser would be an unused, separately
testable surface with no requirement behind it.

**Alternatives considered.**
- *Expose a typed `AuditDocument` record + a serializer.* Rejected: adds a public type and a
  round-trip obligation no FR asks for; the JSON text *is* the contract (fixed in
  `contracts/audit-json-document.md`).

## D5 — Document field structure: tagged item entries under three always-present sections

**Decision.** One top-level object, field order `schemaVersion`, `verdict`, `exitCodeBasis`,
`blockers`, `warnings`, `passing`. Each of the three sections is an **always-present** array (empty
array when the section is empty — FR-005/FR-009). Each item entry is a **tagged** object: field order
`kind`, `id`, then `path` (findings only), then a nested `enforcement` object carrying the six F023
fields in record order `baseSeverity`, `maturity`, `mode`, `profile`, `effectiveSeverity`, `reason`.
A gate item is `{ "kind":"gate", "id":<gateId>, "enforcement":{…} }`; a finding item is
`{ "kind":"finding", "id":<findingIdToken>, "path":<governedPath>, "enforcement":{…} }`.

**Rationale.** The `kind` discriminator makes each entry self-describing and lets one consumer parser
handle both shapes (mirrors how GatesJson's gate entry is a fixed-order object). Nesting the six
enforcement fields under `enforcement` groups the identity (what) from the enforcement detail (why),
and keeps both base and effective severity adjacent so the no-hide rule (US3) is legible at a glance.
Findings carry a `path` because their identity is `(FindingId, GovernedPath)`; gates have no path, so
the field is *absent* on gate entries rather than `null` — a tagged shape, not a nullable one.

**Alternatives considered.**
- *Flatten the six fields onto the entry (no `enforcement` object).* Rejected: blurs identity vs.
  enforcement and makes the no-hide grouping less obvious; nesting matches the `EnforcementDecision`
  record boundary one-to-one.
- *Always emit `path` as `null` for gates.* Rejected: a gate has no governed path; `null` invites a
  consumer to treat it as a real-but-empty path. The `kind` tag already disambiguates.
- *A single `items` array with a `section` field per item.* Rejected: the spec's contract is three
  mutually-exclusive sections (FR-005); three arrays make "present-and-empty" and
  "no item in two sections" structurally obvious.

## D6 — Ordering: inherit the `ShipDecision`'s composite order verbatim; re-sort nothing

**Decision.** Emit each section's items in the order they already appear in the `ShipDecision`
(`Blockers`, `Warnings`, `Passing` are F# `list`s). F024 already sorted every list by the stable
composite key — gates before findings, gates by `GateId`, findings by `(path, finding-id token)`
(`Ship.fsi` lines 51–54). The projection adds **no** ordering decision beyond the fixed field
sequence, so two decisions equal as values but assembled from differently-ordered route inputs project
identically (SC-003).

**Rationale.** Re-sorting here would duplicate F024's responsibility and risk diverging from it; the
list order is part of the already-validated typed value (FR-007). This is exactly RouteJson/GatesJson
inheriting the registry/route order verbatim.

**Alternatives considered.**
- *Re-sort the items inside the projection.* Rejected: redundant, and any drift from F024's key would
  make the two layers disagree. Determinism is F024's guarantee; F025 preserves it.

## D7 — Test inputs: real upstream chain via `Ship.rollup`, never mocks

**Decision.** Tests build a **real** `ShipDecision` by calling `Ship.rollup route mode profile` over a
**real** F019 `RouteResult` (assembled from real gates/findings the way the F024 Ship tests do), then
inspect the **emitted bytes** with a read-only `System.Text.Json.JsonDocument` parse. Expecto + FsCheck
supply the determinism, permutation-invariance, and totality properties. No mocks, no private helpers
(Principle V).

**Rationale.** Driving the real `rollup` re-exercises the F023→F024 pipeline, catching any
projection-time field mismatch a hand-built `ShipDecision` literal would hide — the F020/F021 rationale
verbatim. The test project references `Ship`, `Route`, `Enforcement`, `Config`, `Gates`, and `Findings`
to assemble that real chain.

**Alternatives considered.**
- *Hand-construct `ShipDecision` record literals.* Acceptable for a few totality edge cases (the empty
  decision), but the primary projection/carry tests drive the real `rollup` so the carried enforcement
  detail is genuine, not author-invented.

## D8 — Edge cases: empty decision, relaxed-blocker warning, duplicate finding id, separator-in-id

**Decision.** Handle the spec's edge cases by construction:
- *Empty/clean decision* → `{ schemaVersion, verdict:"pass", exitCodeBasis:"clean", blockers:[],
  warnings:[], passing:[] }` — a success, never an error (FR-009).
- *Relaxed base-`Blocking` item in `warnings`* → its `enforcement` object shows `baseSeverity:"blocking"`
  **and** `effectiveSeverity:"advisory"` together with mode/profile/maturity/reason (FR-011, US3).
- *Same `FindingId` on several governed paths* → each `(id, path)` renders as a distinct finding entry
  with its own `path`; no dedup across paths (FR-004).
- *`GateId`/path string containing the id separator (e.g. a colon)* → rendered verbatim via
  `gateIdValue`/`GovernedPath` unwrap; never re-parsed (FR-008, FR-010).
- *Free-text `Reason`* → written through the writer's string API; JSON-escaping is the writer's job
  (FR-012).

**Rationale.** Each case is a direct consequence of D5/D6 and the reuse of upstream identity
accessors; none requires special-casing in the writer beyond the closed-DU exhaustive matches.

**Alternatives considered.** None — these are the behaviors the spec's Edge Cases and FRs mandate.

## Resolved Technical Context

No `NEEDS CLARIFICATION` remain. Language/stack: **F# on .NET `net10.0`**, the exclusive constitutional
stack. New project: **`FS.GG.Governance.AuditJson`** (packable) + its Expecto/FsCheck test project.
Serialization: **shared-framework `System.Text.Json` `Utf8JsonWriter`**, **no new package**
(FR-014). Dependency: **one `ProjectReference` to `FS.GG.Governance.Ship`** (all rendered types arrive
transitively). Surface: **`schemaVersion: string`** and **`ofShipDecision: ShipDecision -> string`**,
governed by `AuditJson.fsi` + a committed surface baseline. Schema version token: **`"fsgg.audit/v1"`**.
The wire contract (field order, tokens, exclusions, worked sample) is fixed in
[`contracts/audit-json-document.md`](./contracts/audit-json-document.md).
</content>
</invoke>
