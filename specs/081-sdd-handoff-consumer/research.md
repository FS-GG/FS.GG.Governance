# Phase 0 Research: SDD→Governance Handoff Consumer

**Feature**: `081-sdd-handoff-consumer` · **Date**: 2026-06-27

This document resolves the open technical questions before design. Each decision records
what was chosen, why, and the alternatives rejected. The dominant finding (D3) reshaped the
integration mechanism away from the spec's *assumed* "kernel adapter SPI" wording.

---

## D1 — Where the consumer lands (project placement)

**Decision**: A new sibling library project **`FS.GG.Governance.Adapters.SddHandoff`** under
`src/`, `.fsi`-first, registered in `FS.GG.Governance.sln`, with its own surface-area baseline
at `surface/FS.GG.Governance.Adapters.SddHandoff.surface.txt`. It depends on the kernel
(`Evidence`), `Config.Model` (`GovernedPath`, `Gate`/`GateId`/`Maturity`/`Cost`/`DomainId`,
etc. — these live in `Gates.Model`/`Config.Model`), `Gates.Model`, `Route.Model`, and the
`Enforcement` vocabulary it must produce inputs for. It is **not** referenced by any rendering
or SDD code, and imports **no** SDD source (FR-013, SC-006).

**Rationale**: It sits in the established `Adapters.*` family (parallel to `Adapters.Spi` F009,
`Adapters.SpecKit` F010, `Adapters.DesignSystem` F011), matching the spec's placement
assumption. A separate project keeps the pure-core dependency rules clean (Constitution
Engineering Constraints) and lets the three host commands depend on one new edge.

**Alternatives rejected**:
- *Fold into an existing Adapters project* — would entangle two domains' surfaces and
  baselines; the family convention is one project per integration domain.
- *Put it in the host command projects directly* — the parse/map logic is pure and shared
  across three hosts; duplicating it violates Phase-A/B de-duplication discipline.

---

## D2 — JSON parsing primitive

**Decision**: Parse the handoff with **`System.Text.Json`** (`JsonDocument.Parse` →
`JsonElement`), the same BCL-only approach as `Kernel.Json` and `Cli.ArtifactReading.readJson`.
**No new package dependency** (System.Text.Json is in the BCL; `Directory.Packages.props` is
unchanged). Parsing is fail-fast on malformed input (Constitution VI), returning a typed
diagnostic rather than throwing.

**Rationale**: The handoff is a JSON document (`governance-handoff.json`). `YamlDotNet` is the
`.fsgg` (YAML) loader's tool and is scoped to `Config` only — it must not spread. The kernel
already round-trips JSON via `System.Text.Json`, so the idiom and determinism guarantees exist.

**Alternatives rejected**:
- *YamlDotNet* — wrong format, and would couple a new module to a dependency reserved for
  `Config`.
- *Hand-rolled tokenizer over `JsonTokens`/`JsonText`* — those leaves are emit-only
  (write-side); there is no reason to build a reader when `JsonDocument` is total and present.

---

## D3 — The verdict seam (the central design decision)

**Question**: How does a handoff-declared evidence/readiness state actually reach a
`route`/`ship`/`verify` verdict (FR-007, FR-008, FR-009)?

**Finding (verified)**: The `route`/`ship`/`verify` hosts produce their verdict through the
**Config → Gates → Routing → Route.select → Ship.rollup** pipeline. They do **not** consume
the kernel `Adapter` rule catalogs (F009–F011): a repo-wide grep shows `Catalog.adapter` /
`Composition.compose` / `SpecKitFact` are referenced only by the CLI (`Project.fs`) and
`EvidenceCommand`, and **no** route/ship/verify host references `Adapters.*` at all.
`Ship.rollup : RouteResult -> RunMode -> Profile -> ShipDecision` derives the verdict purely
from `RouteResult.SelectedGates` + `RouteResult.Findings`; a selected gate becomes a
`Blocking` enforced item iff its `Maturity` is a `block-on-*` (then relaxed/kept by
`Enforcement.deriveEffectiveSeverity` over mode/profile).

**Decision**: Integrate through the **gate pipeline**, not the kernel `Adapter` SPI. The
consumer maps the handoff into **typed `Gate` registry entries** plus the corresponding
**`SelectedGate` entries**, which the host folds into the `GateRegistry` and
`RouteResult.SelectedGates` *before* `Ship.rollup`. Then evidence and readiness are enforced by
the **same** machinery as every other gate — selection, `deriveEffectiveSeverity`, roll-up
(this is exactly FR-009's "participates … like any other gate", and the uniform mechanism that
makes FR-008's "demonstrably affects the verdict" true). Blocking-ness is encoded in each
handoff gate's `Maturity`:
- **Evidence gate** (US1): `block-on-*` maturity when the taint-closed effective evidence
  contains a `Failed` or `AutoSynthetic` state; an advisory (`warn`) maturity when all
  effective states are satisfied (`Real`/`Skipped`). This is what moves the verdict between a
  satisfied and a failing handoff (SC-001).
- **Readiness gate** (US3): `block-on-*` maturity when the readiness block declares a blocking
  state (non-shippable `shipDisposition` or non-empty `blockingDiagnosticIds`); advisory
  otherwise (SC-005).

**Selection of handoff gates**: handoff gates are **pre-selected by the host** (unioned into
`RouteResult.SelectedGates`) because their relevance is the *declared work item*, not a sensed
changed path. `governedReferences[*]`, when present, supply the `SelectingPaths` provenance
(enrichment); when absent, the gate is still selected with empty/synthetic selecting-path
provenance — correctness does not depend on `governedReferences` (FR-010).

**Recorded deviation (flagged)**: The spec's *Assumptions* say the consumer "lands as a new
sibling adapter … conforms to the established adapter SPI." The binding requirements are
FR-007/FR-013, and the spec itself notes "the binding requirement is FR-007/FR-013, not the
project name." Because the kernel `Adapter` rule-catalog SPI is **not** on the verdict path,
conforming to it literally would not satisfy FR-008. We keep the *project* in the `Adapters.*`
family (D1) but integrate via the gate/route/ship pipeline + the `Evidence` kernel. This is
the only mechanism by which the hosts form a verdict.

**Alternatives rejected**:
- *Kernel `Adapter<'fact,'artifact,'change>` rule catalog* — its `RuleOutcome`s are not read by
  route/ship/verify; would not affect the verdict (fails FR-008).
- *Post-process `ShipDecision` (mirror `applyExecution`)* — possible, but it would re-implement
  severity resolution outside `deriveEffectiveSeverity` and skip route's gates.json/route.json;
  folding gates in *before* rollup reuses all enforcement for free and covers `route` too.

---

## D4 — Evidence mapping (ADR-0002 rows → `Evidence.build`/`effective`)

**Decision**: A pure `Mapping` module maps `evidence.nodes[].state` → `Kernel.EvidenceState`
token-for-token, builds the graph with `Evidence.build nodes dependencies`, and runs
`Evidence.effective` for the taint closure — exactly as `Adapters.SpecKit.Catalog`'s
`evidenceNotSynthetic` does over `TaskState`/`TaskDependsOn`. Row handling:

| Handoff input | Mapped result | Source |
|---|---|---|
| `pending`/`real`/`synthetic`/`failed`/`skipped` | same `EvidenceState` token | FR-003, ADR-0002 |
| `deferred` / `accepted-deferral` | `Skipped` (recorded-rationale `[-]`), **not** `Pending` | FR-004, ADR-0002 |
| `autoSynthetic` declared | **reject** with a diagnostic; never enforce a mapped result | FR-005, ADR-0002 |
| `stale` | underlying declared state **+** a `staleEvidence` diagnostic | FR-006, ADR-0002 |

`autoSynthetic` rejection is enforced twice over: the consumer rejects it on read, and
`Evidence.build` independently returns `Error (AutoSyntheticDeclared id)` (defence in depth).
The mapped graph's `Evidence.effective` result drives the evidence gate's maturity (D3).

**Rationale**: This is ADR-0002's accepted mapping reproduced row-for-row (FR-014); the
`Evidence` kernel is domain-neutral and reused verbatim (FR-007).

---

## D5 — Diagnostics for unsafe reads (version mismatch / malformed / autoSynthetic)

**Decision**: A handoff that is malformed, missing required contract fields, or whose
`contractVersion` **major** is unrecognized (≠ `1`), or that declares `autoSynthetic`, yields a
descriptive **diagnostic** and a **blocking handoff-integrity gate** — and **no** mapped
evidence/readiness gate is emitted for that document (FR-002, FR-011: "does not enforce a
mapped result", "no partial mapping"). The diagnostic text is distinct per cause
(version-mismatch vs malformed vs autoSynthetic) so SC-004's "distinct, descriptive finding"
holds. The **F017 `Findings` model surface is left frozen** — these are handoff-domain
diagnostics, not governed-path findings, so they ride on the handoff gate + a diagnostics list,
not on `FindingId`.

**Rationale**: The word "finding" in the spec (US2) means "a surfaced, descriptive diagnostic",
not specifically an F017 `FindingId` (which is path-scoped: `UnknownGovernedPath` /
`UnknownProtectedBoundaryPath`). Realizing the diagnostic as a blocking gate keeps one verdict
mechanism (D3) and avoids widening an unrelated module's frozen surface.

**Alternatives rejected**:
- *Extend `Findings.Model` with new `FindingId` cases* — widens a frozen, path-scoped surface
  for a non-path concern; more baseline churn for no behavioural gain.
- *Throw / fail the whole loop on a bad handoff* — violates FR-011 (no crash) and Constitution
  VI (a malformed external input is not a tool defect).

---

## D6 — Host wiring (MVU edge for the I/O)

**Decision**: Reading handoff files is I/O, so it crosses the existing MVU boundary in each of
`RouteCommand`, `ShipCommand`, `VerifyCommand` (Constitution IV). Each host adds, additively:
- a new `Effect` case `LoadHandoffs of repo: string`;
- a new `Msg` case `HandoffsLoaded of HandoffRead list` (raw `(path, json)` reads);
- a new `Interpreter.Ports` field `Handoffs: string -> HandoffRead list` (locates
  `readiness/<id>/governance-handoff.json` in **stable sorted order**, reads bytes — the *only*
  impure step; FR-012);
- in `update`, after `Route.select`, a pure fold that parses+maps each read (the adapter, D4/D3)
  and unions the derived gates into the registry + selected gates **before** `Ship.rollup`
  (ship/verify) and before the gates.json/route.json projection (route).

`update` stays pure (parse/map are pure); interpretation (file location + read) is at the edge.
Absence of any handoff ⇒ the port returns `[]` ⇒ the fold is identity ⇒ byte-identical output
(FR-001, SC-003).

**Rationale**: Mirrors how every other sensed input (scope, freshness, provenance, view
currency, release, surfaces) is wired — a port + effect + msg + pure fold. Three hosts change
their public `Loop.fsi`/`Interpreter.fsi` additively → three surface baselines re-blessed
ADDITIVELY (Tier 1).

**Alternatives rejected**:
- *Read files inside `update`* — violates Constitution IV (I/O in the pure transition).
- *A single shared host helper* — the three loops already keep their own typed `Effect`/`Msg`;
  the shared pure work lives in the new adapter, called identically from each fold (no host
  helper needed beyond `CommandHost` if a fold helper proves shareable — a bounded follow-up,
  not required here).

---

## D7 — Multiple / zero handoffs (determinism)

**Decision**: The port returns **all** present `readiness/<id>/governance-handoff.json`
documents sorted by `<id>` (ordinal). Each document is parsed/mapped independently; their gates
are unioned and the union is sorted by `GateId` (the gate pipeline's existing stable key). Zero
handoffs ⇒ empty list ⇒ no-op (D6). Empty `evidence.nodes` with a present `readiness` (or
vice-versa) ⇒ each block is consumed independently; one empty block does not invalidate the
other (spec Edge Cases).

**Rationale**: FR-012 demands stable ordering and defined aggregation; sorting by `<id>` then
`GateId` is deterministic and matches the repo's "sort by stable composite key" convention.

---

## D8 — Contract field names (cross-repo provenance)

**Decision**: The in-memory model's field names are taken from **ADR 0002** + the handoff
tutorial (`docs/tutorials/sdd-governance-handoff.md`): `contractVersion`, `schemaVersion`,
`evidence.nodes[].{id?,state,rationale?}`, `evidence.dependencies`, `readiness.{shipDisposition,
verificationReadiness, blockingDiagnosticIds, counts, perViewState}`, `governedReferences[*]`.
The consumer pins **`contractVersion` major `1`** and ignores unknown additive (minor) fields
(ADR-0002 versioning posture). The authoritative JSON key spellings are SDD-owned; the
implementer cross-checks them against the sibling **`FS.GG.SDD`** repo's `017-governance-handoff`
`contracts/integration-requirements.md` (a cross-repo reference, not build-verifiable here) and
captures a committed example fixture. **Risk/flag**: if a spelling differs from ADR/tutorial,
the fixture + model are the single point to adjust; no SDD code is imported (FR-013).

**Rationale**: ADR 0002 and the tutorial are the locally build-verifiable anchor; the
versioning posture is already decided there. Cross-repo divergence is the one residual unknown,
contained to the fixture + model.

---

## D9 — ADR-0002 + tutorial update (readiness becomes a gate)

**Decision**: This feature **changes** ADR-0002's recorded position on one row. ADR 0002 and
the tutorial currently say merge-boundary readiness is *"advisory declared inputs … never an
enforcement verdict"* with queue item #4 ("gate-registry entry vs merge-fence") **open**.
FR-009/FR-015 resolve item #4 in favour of the **gate-registry binding**. Because FR-014
forbids silent divergence between code and the documented contract, the implementation MUST, in
the same change, update (a) ADR-0002's readiness row + close queue item #4 (or add a superseding
note), and (b) the tutorial's readiness mapping row, to read "first-class gate-registry entry".
All other ADR-0002 rows are unchanged.

**Rationale**: FR-014 + FR-015 make the docs part of the contract. This is the recorded
decision the spec asks for (FR-015).

**Note**: No cross-repo contract change is required — the `governance-handoff@1` registry entry
and the document shape are unchanged; only Governance's *consumption* posture for the readiness
block changes (a consumer-side decision ADR 0002 explicitly left to Governance).

---

## Tier & constitution summary

**Tier 1 (contracted change)**: new public project + surface baseline; additive public
`Loop.fsi`/`Interpreter.fsi` changes on three host commands (three re-blessed baselines);
ADR + tutorial updated in lockstep (FR-014). Real-evidence tests via the existing
Config→Gates→Routing→Route→Enforcement pipeline over on-disk fixtures (Constitution V). MVU
boundary respected for the I/O (Constitution IV, D6). No new runtime dependency (D2).
