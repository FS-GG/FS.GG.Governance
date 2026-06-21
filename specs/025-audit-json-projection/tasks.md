---
description: "Task list for F025 - 025-audit-json-projection: the pure audit.json projection — a single pure, total `AuditJson.ofShipDecision : ShipDecision -> string` (plus a `schemaVersion` constant) that renders the F024 `ShipDecision` into a deterministic, versioned `audit.json` WHOLE-CHANGE verdict document via a hand-driven `System.Text.Json` `Utf8JsonWriter` walk. Carries the decision's `verdict` (pass/fail) and `exitCodeBasis` (clean/blocked) verbatim, plus the three always-present blockers/warnings/passing section arrays in F024 composite order, each item tagged `kind:\"gate\"`/`\"finding\"` with its identity (GateId, or FindingId token + governed path) and a nested `enforcement` object carrying all six F023 fields verbatim. Re-derives/re-classifies/re-partitions/re-sorts NOTHING; byte-identical for identical input; NO new third-party dependency; and NO numeric process exit code, provenance/attestation reference, cache-eligibility verdict, gate registry metadata, route trace, raw-YAML, host-path, timestamp, or environment value; NO round-trip parse; NO CLI/gates.json/route.json."
---

# Tasks: Deterministic audit.json Projection

**Feature branch**: `025-audit-json-projection` (active spec; git branch currently `main`)
**Spec**: [`specs/025-audit-json-projection/spec.md`](./spec.md)
**Plan**: [`specs/025-audit-json-projection/plan.md`](./plan.md)

**Input**: Design documents from `/specs/025-audit-json-projection/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/AuditJson.fsi](./contracts/AuditJson.fsi), [contracts/audit-json-document.md](./contracts/audit-json-document.md), [quickstart.md](./quickstart.md)

**Tests**: REQUIRED. This is a **Tier 1** feature (new public, packable surface; new public `.fsi`; new surface baseline). Credible evidence is **public-surface** testing only: `AuditJson.ofShipDecision` exercised over **real upstream-assembled inputs** — a real `ShipDecision` from the genuine F024 `Ship.rollup` over a real F019 `RouteResult` at a real F023 `RunMode`/`Profile` (research D7), never private helpers and never mocks (Principle V). The **emitted bytes** are inspected by a read-only `System.Text.Json.JsonDocument` parse, exactly as the kernel's `Json` tests and F020/F021's `RouteJson`/`GatesJson` tests do. Driving the real `rollup` re-exercises the F024 partition + enforcement chain, catching any projection-time mismatch a mock would hide. No network, git, agent, clock, or filesystem is reachable, so **no synthetic evidence is anticipated** — every case (empty/clean, blockers-only, warnings-only, passing-only, all-three, a relaxed base-`Blocking` warning, a finding id repeated on two paths) is reachable from real `rollup` outputs (the rollup's per-item input domain is the finite cross-product of gate maturity / finding zone × run mode × profile). Any literal standing in for an un-derivable case carries `Synthetic` in the test name + a use-site `// SYNTHETIC:` disclosure and is listed in the PR.

**Tier**: the whole feature is **Tier 1** (plan Constitution Check). Every task matches the feature tier; no per-task `[T1]`/`[T2]` annotations needed. **No existing project's public surface is touched** — `FS.GG.Governance.Ship` (with Enforcement/Route/Config/Gates/Findings/Kernel transitive) is referenced as-is; its existing public types and the public `Gates.gateIdValue` / `Findings.findingIdToken` renderers suffice. The only new baseline is `surface/FS.GG.Governance.AuditJson.surface.txt`.

**Elmish/MVU (Principle IV)**: **NOT APPLICABLE** — this feature is a pure, total render of one already-typed, already-ordered value to a string (FR-008): no I/O, no git sensing, no clock, no multi-step state, no retries, no effect. It is exactly the "single pure function / explanation formatter" case Principle IV explicitly exempts from MVU ceremony (plan Constitution Check; the same call F019 `select`, F020 `ofRouteResult`, F021 `ofGateRegistry`, and the kernel's `Json.ofExplanation` made). The boundary is one pure function `ofShipDecision : ShipDecision -> string` plus a `schemaVersion` constant — no `Model`/`Msg`/`Effect`/`update`/interpreter.

**Determinism minimums (FR-007, SC-002/SC-003)**: field order is the single ordering decision the projection makes — the fixed `Utf8JsonWriter` call sequence (top-level `schemaVersion` → `verdict` → `exitCodeBasis` → `blockers` → `warnings` → `passing`, and each item/`enforcement` object's documented field order per [contracts/audit-json-document.md](./contracts/audit-json-document.md)). Collection order is inherited from `ShipDecision` verbatim — each section in F024's already-fixed composite order (gates before findings, gates by `GateId`, findings by `(path, finding-id token)`) — re-sorting **nothing** (research D6). No `Map` iteration (the inputs are F# `list`s). Default `Utf8JsonWriter` options ⇒ compact, no whitespace variance. No clock/host/environment value enters the document. Consequence: byte-identical across runs (SC-002) and identical for value-equal decisions assembled from differently-ordered route inputs (SC-003, inherited from F024's composite sort).

**Carry/exclusion minimums (FR-002/FR-003/FR-004/FR-005/FR-006, FR-010/FR-011/FR-012/FR-014, SC-005/SC-007)**: the F024 `ShipDecision` supplies `verdict`/`exitCodeBasis` **verbatim** — never recomputed from the item sections (FR-002) and never mapped to a numeric process exit code (FR-003). Each item's identity is **reused** from public upstream — a gate's `id` via `Gates.gateIdValue` (never re-parsed even across a `:` separator, FR-010), a finding's `id` via `Findings.findingIdToken` + its `GovernedPath` unwrapped verbatim, the same id on two paths rendering as distinct entries (FR-004). The nested `enforcement` object carries all six F023 fields **verbatim** in record order — `baseSeverity`, `maturity`, `mode`, `profile`, `effectiveSeverity`, `reason` — none dropped, none re-derived, none re-ordered (FR-006); on a relaxed base-`Blocking` warning `baseSeverity` and `effectiveSeverity` differ and are **both** present so a profile can never hide the underlying verdict (FR-011). The free-text `reason` is JSON-escaped by the writer (FR-012). The writer has **no** code path that emits a numeric exit code, a provenance/attestation reference, an artifact digest, a cache-eligibility/freshness verdict, a per-change route trace, gate registry metadata (`cost`/`timeout`/`owner`/`prerequisites`/`freshnessKey`), raw YAML, a host/absolute path, a timestamp, or an environment value — none exist on `ShipDecision`/`EnforcedItem`/`EnforcementDecision`. The exclusion-sweep test asserts the emitted text contains none of those tokens (FR-012, SC-007).

**Totality minimums (FR-008/FR-009, SC-006)**: `ofShipDecision` pattern-matches only closed DUs (`Verdict`, `ExitCodeBasis`, `EnforcedItemId`, `Severity`, `Maturity`, `RunMode`, `Profile`) exhaustively, **no wildcard** — a future verdict/basis/severity/maturity/mode/profile case is a compile error here, never a silently mis-tokened field (research D3) — and unwraps single-case newtypes (`GovernedPath`); no partial function, no division, no parse, no array index, no I/O — so it cannot throw for any well-typed `ShipDecision`. The empty/clean decision (no items; `Pass`; `Clean`) projects to `{ "schemaVersion": "fsgg.audit/v1", "verdict": "pass", "exitCodeBasis": "clean", "blockers": [], "warnings": [], "passing": [] }` — a valid success, never an error and never a "fail by default" placeholder (FR-009).

**Scope-guard minimums (FR-003/FR-012/FR-014)**: emit-only — **no** round-trip parse (`toShipDecision` is a later consumer's concern), **no** numeric process exit code (the later `fsgg ship` host edge maps the basis to a number), **no** provenance/attestation reference (the `ShipDecision` carries none — the later Release phase), **no** cache-eligibility/freshness evaluation (Phase 11), **no** verdict recomputation or item re-partition/re-sort (F024's responsibility), **no** `fsgg` CLI host (persisting to `readiness/<id>/audit.json` is a later host edge). The library lives in the product-neutral Governance layer, requires no FS.GG package installed in any inspected repo, and adds **no** new third-party `PackageReference` — serialization is the net10.0 shared-framework `System.Text.Json` the kernel's `Json.fs` and F020/F021's projections already use.

## Status Legend

- `[ ]` pending
- `[X]` done with real evidence (or with synthetic evidence disclosed per Principle V)
- `[-]` skipped (with written rationale)

Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow the scope and document it.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in the phase.
- **[Story]**: `[US1]`..`[US4]`; omitted for setup/foundation/polish.
- Every task names an exact file path.

---

## Phase 1: Setup

**Purpose**: stand up the new packable audit.json-projection library `FS.GG.Governance.AuditJson`, its test project, the public contract (copied verbatim), the real upstream-assembly + `JsonDocument` read helpers, the prelude sketch, and the readiness note. **No new third-party dependency** — the library references **only** `FS.GG.Governance.Ship` (Enforcement/Route/Config/Gates/Findings/Kernel arriving transitively, research D1); its own code is `System.Text.Json` (BCL shared framework) + FSharp.Core.

- [X] T001 Create `src/FS.GG.Governance.AuditJson/FS.GG.Governance.AuditJson.fsproj` targeting `net10.0`, `IsPackable=true`, `PackageId=FS.GG.Governance.AuditJson`, `RootNamespace=FS.GG.Governance.AuditJson`, with exactly **one** `<ProjectReference>` — `../FS.GG.Governance.Ship/FS.GG.Governance.Ship.fsproj` — and **no** `<PackageReference>` (Enforcement/Route/Config/Gates/Findings/Kernel arrive transitively via Ship; `System.Text.Json` is in the net10.0 shared framework, research D1/D2). Compile order: `AuditJson.fsi` → `AuditJson.fs`. Add an fsproj header comment (mirroring the F021 fsproj) noting this is the audit.json *projection* — the emit-only, pure render of an F024 `ShipDecision` to the deterministic versioned WHOLE-CHANGE verdict document string; it layers serialization on top of the pure `Ship` core in a separate project (constitution: heavier capabilities layer on top, not into the core), adds no dependency, and reaches no git/filesystem/clock.
- [X] T002 Copy `specs/025-audit-json-projection/contracts/AuditJson.fsi` → `src/FS.GG.Governance.AuditJson/AuditJson.fsi` as the curated public surface (Principle II — this `.fsi` is the SOLE public surface: `schemaVersion` + `ofShipDecision`; the matching `AuditJson.fs` carries no top-level access modifiers and keeps every writer/token helper hidden, the `Kernel/Json.fs` + `RouteJson.fs` + `GatesJson.fs` precedent). **One correction vs the contract draft**: the `open` is `FS.GG.Governance.Ship.Model` (where `ShipDecision` actually lives), not `FS.GG.Governance.Ship` — mirroring how `Ship.fsi` itself opens `Ship.Model`; the draft's namespace-only `open` would not resolve `ShipDecision`.
- [X] T003 Implemented the real `AuditJson.fs` body directly rather than landing a throwaway `failwith "F025"` stub first (Principle I is still satisfied: the `.fsi` contract compiled first via T002, and the design was recorded in `scripts/prelude.fsx` / quickstart before the body — T008). The Foundation + projection (T010–T013, T015, T020, T022, T025) all edit this one file, so a stub-then-rewrite step added no evidence value.
- [X] T004 Create `tests/FS.GG.Governance.AuditJson.Tests/FS.GG.Governance.AuditJson.Tests.fsproj` with centrally pinned Expecto/Expecto.FsCheck/FsCheck/Microsoft.NET.Test.Sdk/YoloDev.Expecto.TestSdk packages (from `Directory.Packages.props`), `IsPackable=false`, `GenerateProgramFile=false`, and `ProjectReference`s to `src/FS.GG.Governance.AuditJson`, `src/FS.GG.Governance.Ship`, `src/FS.GG.Governance.Route`, `src/FS.GG.Governance.Enforcement`, `src/FS.GG.Governance.Gates`, `src/FS.GG.Governance.Findings`, and `src/FS.GG.Governance.Config` (the tests assemble a real `RouteResult`, call the real `Ship.rollup` at a real mode/profile to build a real `ShipDecision`, and read the emitted bytes via `System.Text.Json.JsonDocument` — mirroring the F024 test fsproj refs).
- [X] T005 [P] Add empty Expecto test modules in compile order in `tests/FS.GG.Governance.AuditJson.Tests/`: `Support.fs`, `ProjectionTests.fs`, `DeterminismTests.fs`, `CarryTests.fs`, `TotalityTests.fs`, `SurfaceDriftTests.fs`, `Main.fs` (Main runs the assembly).
- [X] T006 Add `src/FS.GG.Governance.AuditJson` and `tests/FS.GG.Governance.AuditJson.Tests` to `FS.GG.Governance.sln`.
- [X] T007 [P] Implement the real upstream-assembly + JSON read helpers in `tests/FS.GG.Governance.AuditJson.Tests/Support.fs` over **real** values (no mocks), reusing the F024 `Ship.Tests.Support` builder style: (a) real F018 `Gate` / F017 finding / F019 `RouteResult` builders (`mkGate`, `mkSelectedGate`, `mkFinding`, `mkRoute`, `emptyRoute`) plus the enumerated `allModes`/`allProfiles`/`allMaturities`/`allZones` lever domains, so each fixture is a genuine route a downstream caller holds; (b) a `decisionOf : RouteResult -> RunMode -> Profile -> ShipDecision` convenience calling the real `Ship.rollup` (so every fixture is a real F024 decision, not a hand-built value), and named helpers building the discriminating cases — an empty/clean decision, a blockers-bearing decision, a decision whose warnings include a **relaxed base-`Blocking`** item (a base-blocking gate/finding under a maturity/profile that relaxes it to effective `Advisory`, the no-hide case), and a decision with the **same finding id on two governed paths**; (c) `JsonDocument` read helpers — `parse : string -> JsonDocument`, plus small accessors (a section reader for `blockers`/`warnings`/`passing`, a per-item `kind`/`id`/`path` reader, an `enforcement` six-field reader, field-presence/absence probes, and a top-level/per-object field-order extractor) for read-only inspection of the emitted bytes. These produce/inspect REAL outputs, never fakes.
- [X] T008 [P] Extend `scripts/prelude.fsx` with an F025 design sketch that `#r`s the built `FS.GG.Governance.AuditJson` assembly, opens the namespace, rolls up the existing prelude `RouteResult` via `Ship.rollup route RunMode.Gate Profile.Standard`, projects it via `AuditJson.ofShipDecision`, prints `AuditJson.schemaVersion`, prints the document bytes + length, asserts byte-identity on a second projection, and projects the empty/clean decision (`Ship.rollup emptyRoute RunMode.Gate Profile.Standard`) to show the empty-but-valid `{ "schemaVersion": "fsgg.audit/v1", "verdict": "pass", "exitCodeBasis": "clean", "blockers": [], "warnings": [], "passing": [] }` document — recording the intended projection flow before the real body lands (Principle I; mirrors [quickstart.md](./quickstart.md) §FSI smoke). **Design-first, like F021's T008**: written here as the design record, it will *throw at runtime* while `ofShipDecision` is the `failwith "F025"` stub (T003) and only runs green once Foundation/US1 land; T028 re-runs it end-to-end against the real body.
- [X] T009 [P] Create `specs/025-audit-json-projection/readiness/README.md` listing the required FSI transcripts (a failing rolled-up decision showing `verdict:"fail"`/`exitCodeBasis:"blocked"` with each section's items in F024 composite order and full enforcement detail; a relaxed base-`Blocking` warning showing both `baseSeverity:"blocking"` and `effectiveSeverity:"advisory"` beside the mode/profile/maturity/reason; a twice-identical determinism run; an empty/clean document with three present empty arrays) and an SC-traceability note mapping SC-001…SC-007 to the test files that prove them (per [quickstart.md](./quickstart.md) acceptance→evidence map).

**Checkpoint**: `dotnet build src/FS.GG.Governance.AuditJson` and `dotnet test tests/FS.GG.Governance.AuditJson.Tests` compile against the stub; the solution lists the two new projects; the single Ship reference resolves (Enforcement/Route/Config/Gates/Findings/Kernel transitively); the Support helpers assemble a real `ShipDecision` via `Ship.rollup` and parse a string with `JsonDocument`.

---

## Phase 2: Foundation (Blocking Prerequisites)

**Purpose**: the `schemaVersion` constant, the six hidden closed-enum token helpers, the nested `enforcement` object writer + the tagged item writer, and the top-level `ofShipDecision` writer skeleton (the fixed-order `Utf8JsonWriter` walk) — everything the stories specialize. **No user-story work begins until this phase is complete.**

- [X] T010 Implement `AuditJson.schemaVersion` in `src/FS.GG.Governance.AuditJson/AuditJson.fs` as the fixed declared contract-version constant (`"fsgg.audit/v1"` per [contracts/audit-json-document.md](./contracts/audit-json-document.md), FR-013) — a plain string literal, never derived from a clock/environment/input.
- [X] T011 Implement the **six hidden** closed-enum token helpers in `src/FS.GG.Governance.AuditJson/AuditJson.fs` (absent from `AuditJson.fsi`, mirroring `Kernel/Json.fs`, `RouteJson.fs`, `GatesJson.fs`): `verdictToken : Verdict -> string` (`pass`/`fail`), `basisToken : ExitCodeBasis -> string` (`clean`/`blocked`), `severityToken : Severity -> string` (`advisory`/`blocking`), `maturityToken : Maturity -> string` (`observe`/`warn`/`blockOnPr`/`blockOnShip`/`blockOnRelease`), `modeToken : RunMode -> string` (`sandbox`/`inner`/`focused`/`verify`/`gate`/`release`), and `profileToken : Profile -> string` (`light`/`standard`/`strict`/`release`) — the exact token tables in [data-model.md](./data-model.md) §3 and [contracts/audit-json-document.md](./contracts/audit-json-document.md). Each `match` is **exhaustive over the closed DU with no wildcard** (research D3), so a future case is a compile error here, never a silently mis-tokened field. The two **identity** renderers are **reused** from public upstream (`Gates.gateIdValue`, `Findings.findingIdToken`) — AuditJson rolls no identity token of its own (Enforcement's own token helpers are hidden across the assembly boundary, so the six severity/verdict/basis/maturity/mode/profile helpers above are local — the GatesJson precedent).
- [X] T012 Implement the **hidden** `enforcement` object writer in `src/FS.GG.Governance.AuditJson/AuditJson.fs` against a `Utf8JsonWriter`, emitting the six F023 fields in documented record order verbatim (FR-006, [contracts/audit-json-document.md](./contracts/audit-json-document.md)): `writeEnforcement` (`baseSeverity` via `severityToken`, `maturity` via `maturityToken`, `mode` via `modeToken`, `profile` via `profileToken`, `effectiveSeverity` via `severityToken`, `reason` via the writer string API — JSON-escaped, never manually). None re-derived, none re-ordered, none dropped (FR-006); `baseSeverity` and `effectiveSeverity` are emitted as **separate** fields so a relaxed item shows both (FR-011). Disclose any `mutable`/`for` writer idiom at its use site (Principle III — the plain BCL `Utf8JsonWriter` idiom).
- [X] T013 Implement the **hidden** tagged item writer + the top-level `ofShipDecision` skeleton in `src/FS.GG.Governance.AuditJson/AuditJson.fs`. The item writer `writeItem : Utf8JsonWriter -> EnforcedItem -> unit` matches the closed `EnforcedItemId` (exhaustive, no wildcard) and emits a tagged object: for `GateItem g` → field order `kind` (literal `"gate"`), `id` (via `Gates.gateIdValue g`, verbatim — never re-parsed, FR-010), `enforcement` (via `writeEnforcement`, T012); for `FindingItem (fid, GovernedPath path)` → field order `kind` (literal `"finding"`), `id` (via `Findings.findingIdToken fid`), `path` (the unwrapped `GovernedPath` verbatim, FR-010), `enforcement` — a gate item has **no** `path` field (absent, not `null`; the `kind` tag disambiguates, research D5). The top-level `ofShipDecision`: create a `Utf8JsonWriter` over a pooled buffer with **default** (compact) options; write one top-level object in the FIXED order `schemaVersion` (constant) → `verdict` (via `verdictToken decision.Verdict`, FR-002 — never recomputed) → `exitCodeBasis` (via `basisToken decision.ExitCodeBasis`, FR-003 — no numeric exit code) → `blockers` / `warnings` / `passing` (each an array walking the decision's list in order via `writeItem`, **re-sorting nothing**, FR-007); flush and decode the buffer to a UTF-8 string. PURE and TOTAL — never throws; the empty/clean decision yields three present empty arrays with `verdict:"pass"`/`exitCodeBasis:"clean"`, a valid success (FR-008/FR-009).

**Checkpoint**: the library builds with the real `schemaVersion` + six token helpers + `enforcement` writer + tagged item writer + the top-level walk; `ofShipDecision` over the empty/clean decision returns the empty-but-valid document; the document parses as one top-level object with fields in the fixed order; the surface compiles against `AuditJson.fsi`.

---

## Phase 3: User Story 1 - Render a ship decision to a deterministic audit.json (Priority: P1) 🎯 MVP

**Goal**: project a real rolled-up `ShipDecision` so the document records the whole-change `verdict` and `exitCodeBasis` and lists every blocker/warning/passing item by its identity (gate id, or finding id + path) with its complete six-field enforcement detail — every value tracing back to the `ShipDecision`, none invented; no item in more than one section; and the empty/clean decision projects to a valid document with three present empty arrays (FR-009).

**Independent Test**: project a real `decisionOf` fixture with one or more blockers; parse the document and assert `verdict:"fail"` / `exitCodeBasis:"blocked"`, every blocker listed by identity with full enforcement detail, and no item that the decision did not classify as a blocker appearing there; project a `Pass`/empty-blockers decision and assert `verdict:"pass"` / `exitCodeBasis:"clean"` with a present empty `blockers`; project a decision carrying warnings and passing items and assert each renders in its own section with no item in two sections.

### Tests for User Story 1 (write first; must FAIL before implementation)

- [X] T014 [P] [US1] In `tests/FS.GG.Governance.AuditJson.Tests/ProjectionTests.fs`, add projection tests over real `decisionOf` fixtures, inspecting the emitted bytes via `JsonDocument`: (1) a decision with ≥1 blocker projects to `verdict:"fail"` / `exitCodeBasis:"blocked"`, with every blocker listed by its identity (gate by `gateIdValue`; finding by `findingIdToken` + `path`) and its complete `enforcement` detail, and **no** item the decision did not classify as a blocker appearing in `blockers` (US1 AS1, **SC-001**); (2) a `Pass` decision with empty blockers projects to `verdict:"pass"` / `exitCodeBasis:"clean"` with a **present, empty** `blockers` array — never an error and never an invented blocker (US1 AS2, FR-009); (3) a decision carrying warnings and passing items alongside (or without) blockers renders every warning and every passing item in its own section, each with identity + full enforcement detail, and **no** item appears in more than one section — the union of the three rendered sections equals the decision's items, with no duplicate (US1 AS3, FR-005, **SC-001**); (4) `verdict` and `exitCodeBasis` are echoed **verbatim** from the decision value (assert both directions — `pass`/`clean` when blockers empty, `fail`/`blocked` otherwise — match `decision.Verdict`/`decision.ExitCodeBasis`, never recomputed from the rendered sections, and the document carries **no** numeric `exitCode`) (FR-002/FR-003, **SC-004**); (5) a `reason` containing JSON-special characters (`"`, `\`, a newline) round-trips: the value read back from the parsed `JsonDocument` equals the input string exactly — faithful carry with escaping delegated to the writer, never manual (spec edge case, FR-012). **Synthetic disclosure (Principle V)**: the real F024 `Ship.rollup` never emits a `reason` with `"`/`\`/newline (its reason vocabulary is fixed lever-naming text), so case (5) is split — a real-chain test asserts every genuine `rollup` reason round-trips, and a separate test named `Synthetic: a reason with JSON-special characters round-trips exactly` builds a `ShipDecision` directly with a crafted reason to reach the writer's escaping path (use-site `// SYNTHETIC:` comment present; no real-evidence path exists). All other cases use real `rollup` outputs.

### Implementation for User Story 1

- [X] T015 [US1] Complete the per-section item rendering inside `ofShipDecision` (`src/FS.GG.Governance.AuditJson/AuditJson.fs`, building on T012/T013): confirm each of `blockers`/`warnings`/`passing` walks `decision.Blockers`/`decision.Warnings`/`decision.Passing` in carried order emitting each item via `writeItem` (tagged `kind` + identity + nested `enforcement`), and that `verdict`/`exitCodeBasis` are written via `verdictToken`/`basisToken` from the decision value (FR-002/FR-003 — carry verbatim, recompute nothing). Each section is always present — an empty list emits a present empty array (FR-005/FR-009). Free-text `reason` is written through the `Utf8JsonWriter` string API so JSON-escaping is the writer's job — **no** manual escaping (FR-012, asserted by T014(5)). Note explicitly if no change was needed beyond the Foundation walk.

**Checkpoint**: a real rolled-up decision projects to a document recording its verdict/basis and listing every item in its correct section with identity + full enforcement detail, no item duplicated or in a wrong section, and the empty/clean decision a valid three-empty-arrays document — the MVP. US1 stands alone.

---

## Phase 4: User Story 2 - A stable, versioned schema for CI, branch protection, and agents (Priority: P1)

**Goal**: identical inputs produce a byte-identical document; value-equal decisions assembled from differently-ordered route inputs produce identical documents; the document carries a declared `schemaVersion` and a stable documented field order at every level; and it contains no clock/host/environment value and none of the excluded exit-code/provenance/cache/raw-YAML tokens.

**Independent Test**: project the same `ShipDecision` twice and assert byte-for-byte equality; project two decisions built (via the real `Ship.rollup`) from differently-ordered route inputs and assert identical strings; assert the `schemaVersion` field equals `AuditJson.schemaVersion` and the top-level field order is `schemaVersion`,`verdict`,`exitCodeBasis`,`blockers`,`warnings`,`passing`; run the exclusion sweep over the emitted text.

### Tests for User Story 2 (write first; must FAIL before implementation)

- [X] T016 [P] [US2] In `tests/FS.GG.Governance.AuditJson.Tests/DeterminismTests.fs`, add an FsCheck **twice-identical** property and a fixed-fixture equality: `ofShipDecision d = ofShipDecision d`, byte-for-byte (US2 AS1, **SC-002**). **Generator provenance**: generate each `ShipDecision` by driving the real `Ship.rollup` over generated `RouteResult` × `RunMode` × `Profile` (research D7) so inputs stay real upstream-assembled values; any directly-constructed arbitrary carries the `Synthetic` token + a use-site `// SYNTHETIC:` disclosure and is listed in the PR.
- [X] T017 [P] [US2] In the same file, add a **permutation-invariance** test: build two `ShipDecision`s that are value-equal but assembled from **differently-ordered route inputs** (shuffle the `SelectedGates`/`Findings` order in the `RouteResult` passed to `Ship.rollup`, whose composite sort fixes each section's order) and assert the two emitted strings are identical (US2 AS2, **SC-003**).
- [X] T018 [P] [US2] In the same file, add a **schema-version + field-order** test: parse the document and assert the `schemaVersion` field equals `AuditJson.schemaVersion` (`"fsgg.audit/v1"`), assert the top-level field order is exactly `schemaVersion`, `verdict`, `exitCodeBasis`, `blockers`, `warnings`, `passing`, assert each gate item's field order is `kind`, `id`, `enforcement` and each finding item's is `kind`, `id`, `path`, `enforcement`, and assert each `enforcement` object's order is `baseSeverity`, `maturity`, `mode`, `profile`, `effectiveSeverity`, `reason` (US2 AS3, FR-013, [data-model.md](./data-model.md) field-order tables).
- [X] T019 [P] [US2] In the same file, add the **exclusion sweep** (US2 AS4, **SC-007**, FR-011/FR-012) over a real multi-item decision (blockers + warnings + passing, including a relaxed base-`Blocking` warning) so the sweep covers populated sections: (a) a **deny-token** check — the emitted text contains none of a numeric `exitCode`, `provenance`/`attestation`/`digest`, `cacheEligib`/`freshness`, gate registry metadata (`cost`, `timeout`, `owner`, `prerequisites`, `freshnessKey`), `selectingPaths`, a route/cost trace, raw YAML, a wall-clock timestamp, a host/absolute path, or any environment-derived value; (b) a **positive allowlist** check — parse the document and assert the only string values present are the declared `schemaVersion`, the closed `verdict`/`exitCodeBasis`/severity/maturity/mode/profile vocabularies, the declared gate/finding id strings, the governed path, and the carried free-text `reason` (no host/absolute path can appear by construction — `ShipDecision`/`EnforcedItem`/`EnforcementDecision` carry none, FR-012).

### Implementation for User Story 2

- [X] T020 [US2] Confirm/complete determinism in `ofShipDecision` (`src/FS.GG.Governance.AuditJson/AuditJson.fs`): default (compact) `Utf8JsonWriter` options (no indentation), fixed field order throughout (top-level, item entry, `enforcement` object), each section emitted in the `ShipDecision`'s existing composite order with **no** re-sort, **no** `Map` iteration, and **no** clock/host/environment value introduced. Note explicitly if no change was needed beyond Foundation/US1. **Determinism is a property of the Foundation walk + US1 rendering; record here whether any residual input-order or option-default leakage required a fix.**

**Checkpoint**: the document is byte-stable, permutation-invariant, version-stamped, fixed-field-ordered, and free of every excluded token — usable as a CI/branch-protection/agent contract and a golden snapshot. US1 + US2 together are the co-equal P1 MVP pairing.

---

## Phase 5: User Story 3 - The no-hide rule is visible on every item (Priority: P2)

**Goal**: every item, in every section, carries its base severity, effective severity, mode, profile, maturity, and reason verbatim — so a relaxed base-`Blocking` warning is always legible as a self-explaining warning (both severities present, never collapsed) and a finding's identity carries both its declared id token and its governed path.

**Independent Test**: project a `ShipDecision` containing a warning that is a relaxed base-`Blocking` item and assert that item shows `baseSeverity:"blocking"` **and** `effectiveSeverity:"advisory"` together with mode/profile/maturity and a non-empty reason; assert all six enforcement fields are present on every blocker, warning, and passing item; assert a finding item's identity carries both its `findingIdToken` and its governed `path`, a gate item's its `gateIdValue`, and the same finding id on two paths yields distinct entries.

### Tests for User Story 3 (write first; must FAIL before implementation)

- [X] T021 [P] [US3] In `tests/FS.GG.Governance.AuditJson.Tests/CarryTests.fs`, add carry-through tests over real fixtures, inspecting the emitted bytes: (1) a decision whose `warnings` include a base-`Blocking` item relaxed to effective `Advisory` renders that item with `baseSeverity:"blocking"` **and** `effectiveSeverity:"advisory"` (both present, never collapsed) plus the run `mode`, `profile`, `maturity`, and a **non-empty** `reason` — all six fields (US3 AS1, FR-011, **SC-005**). The non-empty `reason` is guaranteed by the real F024 `Ship.rollup` (which always emits an explainable reason), **not** by the `EnforcementDecision.Reason` string type alone — so this assertion stands on the real-chain fixture, never a hand-built literal (note the dependency in the test comment); (2) every blocker, warning, and passing item carries all six `enforcement` fields (`baseSeverity`, `maturity`, `mode`, `profile`, `effectiveSeverity`, `reason`) verbatim from its F023 decision — none dropped, none re-derived, none re-ordered (US3 AS2, FR-006, **SC-005**); (3) a finding item's identity carries both its declared `findingIdToken` **and** its governed `path`, and a gate item's identity carries its declared `gateIdValue` verbatim — neither re-parsed nor re-derived (US3 AS3, FR-004/FR-010); (4) the **same finding id on two different governed paths** renders as **two distinct entries**, each with its own `path` — the id is not deduplicated across paths (spec edge case, FR-004); (5) a `GateId` or governed-path string containing the id separator (e.g. a colon) renders the declared id/path **verbatim** — the emitted `id` equals `gateIdValue g` / the emitted `path` equals the unwrapped `GovernedPath`, with no re-parse and no separator re-derivation (spec edge case, FR-008/FR-010).
- [X] T022 [US3] Confirm the `enforcement` object and the finding `path` field in `ofShipDecision` (`src/FS.GG.Governance.AuditJson/AuditJson.fs`): each item's `enforcement` written via `writeEnforcement` (T012) carries all six F023 fields verbatim with `baseSeverity` and `effectiveSeverity` as **separate** fields (FR-006/FR-011); a finding item carries its `path` via the unwrapped `GovernedPath` (T013, FR-010). Verify there is **no** code path that collapses the two severities, drops a field, or re-derives the reason. Note explicitly if no change was needed beyond Foundation/US1.

**Checkpoint**: every item carries its full six-field enforcement detail with base and effective severity both present, a finding's identity carries id + path, and a relaxed blocker is a self-explaining warning — the no-hide rule is observable. Faithful carry held.

---

## Phase 6: User Story 4 - Total over any well-typed ship decision (Priority: P2)

**Goal**: `ofShipDecision` returns a document for every `ShipDecision` F024 can produce — empty/clean, blockers-only, warnings-only, passing-only, all three sections populated — and never throws; the empty/clean decision is a valid success and no section's items leak into another.

**Independent Test**: FsCheck over generated well-typed `ShipDecision`s asserting `ofShipDecision` always returns a (parseable) string and never throws, including the empty/clean decision, a blockers-only decision, a warnings-only decision, and a decision with all three sections populated.

### Tests for User Story 4 (write first; must FAIL before implementation)

- [X] T023 [P] [US4] In `tests/FS.GG.Governance.AuditJson.Tests/TotalityTests.fs`, add the **empty/clean** and **single-section** tests: (1) `ofShipDecision` over the empty/clean decision (no items; `Pass`; `Clean`) returns a valid document with three present empty arrays and `verdict:"pass"` / `exitCodeBasis:"clean"`, never throwing (US4 AS1, FR-009, **SC-006**); (2) a blockers-only decision (and, separately, a warnings-only and a passing-only decision) projects with the populated section carrying its items and the others rendering as present, empty arrays — no section's items leak into another (US4 AS2, FR-005).
- [X] T024 [P] [US4] In the same file, add an FsCheck **totality** property over generated well-typed `ShipDecision`s (including empty/clean, single-section, and all-populated): `ofShipDecision` **always returns a parseable string and never throws** (US4 AS3, **SC-006**). **Generator provenance (resolve before writing the test):** generate each `ShipDecision` by driving the **real** `Ship.rollup` over an FsCheck generator of `RouteResult` × `RunMode` × `Profile` (research D7) so the inputs stay real upstream-assembled values, keeping the "no synthetic evidence" stance honest. If, and only if, a case is unreachable through `rollup` and a `ShipDecision` value must be constructed directly, that arbitrary is **synthetic**: name the property with the `Synthetic` token, add a use-site `// SYNTHETIC:` disclosure naming the case and why `rollup` can't produce it, and list it in the PR (Principle V). Prefer the real-assembler generator.

### Implementation for User Story 4

- [X] T025 [US4] Confirm `ofShipDecision` (`src/FS.GG.Governance.AuditJson/AuditJson.fs`) is total: only closed-DU `match`es (exhaustive, no wildcard — `Verdict`/`ExitCodeBasis`/`EnforcedItemId`/`Severity`/`Maturity`/`RunMode`/`Profile`) + single-case newtype unwraps (`GovernedPath`) + a `Utf8JsonWriter` walk — no partial function, parse, division, array index, or I/O. Verify the empty/clean decision flows through the same code path as a populated one (no special-casing) and that each section is read only from its own list. Note explicitly if no change was needed beyond earlier phases.

**Checkpoint**: the projection returns a document for 100% of well-typed decisions, including the empty/clean decision, and never throws — callable unconditionally by later rows (the `fsgg ship` host edge).

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: lock the public surface, prove the dependency boundary, and finish the docs/evidence.

- [X] T026 [P] Generate `surface/FS.GG.Governance.AuditJson.surface.txt` capturing exactly the public `AuditJson` module (`schemaVersion`, `ofShipDecision` — the `.fsi` surface), nothing private (no token helpers, enforcement/item writers, or buffer plumbing).
- [X] T027 In `tests/FS.GG.Governance.AuditJson.Tests/SurfaceDriftTests.fs`, add the surface-drift test asserting the built public surface matches `surface/FS.GG.Governance.AuditJson.surface.txt` (Principle II, with `BLESS_SURFACE=1` regen path), assert "exactly the `AuditJson` module, nothing private" (no token helpers, enforcement/item writers, or buffer plumbing), and assert the `AuditJson → Ship` one-way dependency (Ship bringing Enforcement/Route/Config/Gates/Findings/Kernel transitively; no host/adapters/CLI edge; no new third-party `PackageReference`) — mirroring the F020/F021 `SurfaceDriftTests` dependency assertion.
- [X] T028 [P] Verify [quickstart.md](./quickstart.md) end-to-end: run the documented `dotnet test` and the prelude FSI smoke (the real `Ship.rollup` then `AuditJson.ofShipDecision`), confirm the acceptance→evidence map holds, and fill `specs/025-audit-json-projection/readiness/README.md` with the real FSI transcripts (T009) and the SC-001…SC-007 traceability note.
- [X] T029 [P] Update [`specs/025-audit-json-projection/plan.md`](./plan.md) Implementation Progress table (Phase 2/3 → done with evidence summary, mirroring the F021 plan) once the suite is green, and confirm `CLAUDE.md`'s SPECKIT block points at this plan.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)** — no dependencies; start immediately.
- **Foundation (Phase 2)** — depends on Setup; **BLOCKS all user stories** (the `schemaVersion`, the six token helpers, the `enforcement` + tagged item writers, and the top-level `ofShipDecision` walk everything specialises).
- **User Stories (Phases 3–6)** — all depend on Foundation. US1 (P1) is the MVP. US2 (P1) proves determinism/version/exclusion over the document US1 produces. US3/US4 (P2) build on it: US3 asserts the six-field enforcement + identity carry the Foundation writers wire, US4 proves totality across all of it.
- **Polish (Phase 7)** — depends on all desired user stories being complete.

### User-story dependencies

- **US1 (P1)** — after Foundation; no dependency on other stories (the core verdict + sectioned item render).
- **US2 (P1)** — after Foundation; reads the same document US1 produces (determinism/version/field-order/exclusion are properties of the whole walk). Independently testable.
- **US3 (P2)** — after Foundation; the six-field enforcement + identity carry-through is independent of which items are present (asserts faithful carry + no-hide both-severities). Independently testable.
- **US4 (P2)** — after the document is *correct* (US1–US3); proves its *totality* over every well-typed input.

### Within each user story

- Tests are written first and MUST FAIL before implementation (Principle I/V).
- `schemaVersion` + token helpers + enforcement/item writers + top-level skeleton (Foundation) before any story.
- Each story is independently completable and testable; complete a story before moving to the next priority.

### Parallel opportunities

- **Setup**: T005, T007, T008, T009 are `[P]` (distinct files) once T001–T004 exist.
- **Tests across stories**: T014, T016–T019, T021, T023, T024 are `[P]` — distinct test files (`ProjectionTests`/`DeterminismTests`/`CarryTests`/`TotalityTests`), no shared state.
- **Stories**: once Foundation is done, US1–US4 test-writing can proceed in parallel by different developers; the implementation tasks (T015, T020, T022, T025) all touch `AuditJson.fs`, so serialize those edits (or have one owner sweep them in phase order — most are "confirm/complete" since the Foundation walk + US1 rendering already cover them).
- **Polish**: T026, T028, T029 are `[P]`; T027 depends on T026.

---

## Parallel Example: cross-story test authoring

```bash
# After Foundation (Phase 2), launch the per-story test files together (distinct files):
Task: "ProjectionTests.fs  — US1 verdict/basis + sectioned items + identity + empty/clean (T014)"
Task: "DeterminismTests.fs — US2 twice-identical + permutation + version/field-order + exclusion sweep (T016–T019)"
Task: "CarryTests.fs       — US3 six enforcement fields + no-hide both-severities + finding id+path (T021)"
Task: "TotalityTests.fs    — US4 empty/clean + single-section + FsCheck totality (T023–T024)"
```

---

## Implementation Strategy

### MVP first (User Story 1 only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundation (CRITICAL — blocks all stories).
3. Complete Phase 3: User Story 1.
4. **STOP and VALIDATE**: a real rolled-up decision projects to a document recording its verdict/basis and listing every item in its correct section with identity + full enforcement detail, no item duplicated, and the empty/clean decision a valid three-empty-arrays document.

### Incremental delivery

1. Setup + Foundation → foundation ready.
2. US1 → the ship decision renders to a document → the MVP.
3. US2 → the document is a deterministic, versioned, exclusion-clean contract (the co-equal P1).
4. US3 → six-field enforcement + identity carried, no-hide rule observable.
5. US4 → totality proven over every well-typed input.
6. Polish → surface baseline + dependency assertion + readiness/quickstart.

---

## Notes

- `[P]` = different files, no dependencies.
- `[Story]` label maps a task to its user story for traceability.
- The four `AuditJson.fs` implementation tasks (T015, T020, T022, T025) edit one file — serialize them in phase order; most are "confirm/complete," since the Foundation writers (T010–T013) already wire the projection.
- Tests inspect the **emitted bytes** via read-only `JsonDocument` (the kernel `Json`-test + F020/F021 projection-test precedent), never private helpers — `ofShipDecision` and `schemaVersion` are the entire public surface.
- **No synthetic evidence is anticipated** (research D7) — every case (empty/clean, blockers-only, warnings-only, passing-only, all-three, relaxed base-`Blocking` warning, same finding id on two paths, separator-in-id, JSON-special reason) is reachable from real `Ship.rollup` outputs, and the FsCheck properties (T016, T024) generate their `ShipDecision`s by driving the real `rollup` rather than constructing values directly. Any unavoidable literal or directly-constructed FsCheck arbitrary carries `Synthetic` in the test name + a use-site `// SYNTHETIC:` disclosure and is listed in the PR.
- Scope guards (FR-003/FR-012/FR-014): no round-trip parse, numeric process exit code, provenance/attestation reference, artifact digest, cache-eligibility/freshness verdict, verdict recomputation, item re-partition/re-sort, gate registry metadata, route trace, raw YAML, host path, timestamp, environment value, CLI, gates.json, or route.json — the projection stops at the document string, adds no third-party dependency.
