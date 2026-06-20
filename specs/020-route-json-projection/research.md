# Phase 0 Research: Deterministic route.json Projection (F020)

All Technical Context unknowns are resolved below. No `NEEDS CLARIFICATION` remains. The two scope
questions the spec explicitly deferred to plan time — the **project home** (a new library vs. an
addition to F019's `Route`) and the **serialization mechanism** (a BCL serializer vs. a hand-rolled
writer) — are settled here (D1, D2), grounded in spec assumptions, the existing `Kernel/Json.fs`
mechanism, and the F014→F019 one-row-one-library precedent.

## D1 — Project home: a new `FS.GG.Governance.RouteJson` library

**Decision.** Land the feature as a new optional, packable library `FS.GG.Governance.RouteJson` (plus
its test project), sibling to Config/Routing/Snapshot/Findings/Gates/Route. It references **one**
upstream project — `FS.GG.Governance.Route` (the F019 `RouteResult` and its `SelectedGate`/
`SelectingPath`/`CostRollup`) — with Gates (`Gate`/`GateId`/`FreshnessKey`/`GatePrerequisite`,
`gateIdValue`), Findings (`FindingReport`/`FindingId`/`FindingZone`, `findingIdToken`), and Config
(the `GovernedPath`/`Cost`/`DomainId`/`Owner`/`Maturity`/`EnvironmentClass`/`CheckId`/`CommandId`/
`SurfaceId`/`TimeoutLimit` newtypes) arriving transitively through Route. **No new third-party
`PackageReference`** — its own code is `System.*` (`System.Text.Json`) + FSharp.Core only.

**Rationale.** Mirrors the established one-library-per-row shape (F014 Config, F015 Routing, F016
Snapshot, F017 Findings, F018 Gates, F019 Route) and continues it for the next implementation-plan
row. Keeping the projection in its own project (a) leaves F019's just-merged pure join (`Route`)
**free of any serialization surface** — Route stays a pure value-join whose surface-drift test still
reads "exactly the two modules Model and Route, nothing private"; (b) matches the constitution's
"heavier capabilities … layer on top in separate projects, not into the core"; and (c) gives the later
`fsgg route`/`fsgg ship` and audit.json rows a clean, named reference target, with audit.json's
projection able to sit beside this one. The dependency direction stays one-way
(`RouteJson → Route → {Gates, Routing, Findings} → Config`); the kernel/host stay untouched (FR-015).

**Alternatives rejected.**
- *Add a `Json`/`RouteJson` module to `FS.GG.Governance.Route`.* Tenable (it is the kernel's own
  pattern — `Json.fs` lives inside the kernel beside the types it serializes) and noted in the spec as
  an option. Rejected for this row to (a) keep the just-merged F019 pure-join surface and its
  "exactly Model and Route" hygiene test unchurned, and (b) preserve the one-row-one-library rhythm so
  each Phase-2 row keeps its own packable identity and surface baseline. The kernel's in-library `Json`
  is explained by the kernel being a single cohesive multi-module assembly (F03/F05/F06 together); the
  Governance Phase-2 rows are deliberately separate siblings — a different organizing principle that
  this row follows.
- *Put it in Host/Cli.* Rejected: the projection is a product-neutral pure value→string function. The
  whole-document consumers (`fsgg route`/`fsgg ship`, the readiness writers) live in Cli/Host and will
  *reference* RouteJson, exactly as they will reference Route.
- *Reuse the kernel's `Json` module directly.* Rejected: `Kernel.Json` is `System.*`-only by design
  and references **no** Governance domain types (Route/Gates/Findings/Config) — wiring those into the
  kernel would violate the kernel-stays-domain-neutral rule (the route vocabulary must not reach the
  kernel, FR-015). RouteJson instead *reuses the same mechanism* (D2), not the same module.

## D2 — Serialization mechanism: `System.Text.Json` `Utf8JsonWriter`, the kernel's mechanism reused

**Decision.** Emit the document by driving a `System.Text.Json` `Utf8JsonWriter` over a
`MemoryStream`, then `Encoding.UTF8.GetString` the bytes — the *identical* mechanism the kernel's
`FS.GG.Governance.Kernel.Json` already uses (`writeToString`, `WriteStartObject`/`WriteString`/
`WriteStartArray`). Default `Utf8JsonWriter` options ⇒ compact (non-indented) output ⇒ deterministic
bytes. The single public entry point is `RouteJson.ofRouteResult : RouteResult -> string`.

**Rationale.** `System.Text.Json` is part of the `net10.0` shared framework, so it adds **no**
`PackageReference` and keeps the library `System.*`/FSharp.Core-only (FR-015) — the same reasoning the
kernel recorded for choosing it over Newtonsoft. Hand-driving the writer (rather than reflection-based
`JsonSerializer.Serialize`) gives an explicit, **fixed field order** independent of record-declaration
order or serializer settings, which is what makes the output a byte-stable *contract* (FR-007, SC-002)
and lets the projection emit exactly the declared fields and nothing else (no accidental serialization
of an excluded field, FR-011/FR-012). Determinism is structural: the only ordering decisions are the
fixed object-field sequence (the writer's call order) and the collection orders, which are **already
fixed by `RouteResult`** (gates by `GateId`, selecting paths by normalized path, findings in F017
order) — so the projection re-sorts nothing and iterates no `Map`, so there is no key-sort step.

**Alternatives rejected.**
- *Reflection-based `JsonSerializer.Serialize` with attributes/options.* Rejected: field order and
  enum/newtype rendering would depend on serializer configuration and F# record/DU reflection shape
  (e.g. DU cases serialize awkwardly), making the wire contract fragile and the exclusion guarantees
  (FR-011/FR-012) hard to audit. The explicit writer is both simpler to reason about and to test.
- *A hand-rolled string builder.* Rejected: it would have to re-implement JSON escaping (the
  domain-neutral `Description`/`Message`/`Owner` strings can contain quotes, backslashes, control
  chars). `Utf8JsonWriter` does correct escaping for free and is already the house mechanism.

## D3 — Wire tokens for closed enums: local hidden helpers (the `Kernel/Json.fs` precedent)

**Decision.** Render the closed enums the document needs — `Cost`
(`cheap`/`medium`/`high`/`exhaustive`), `Maturity`
(`observe`/`warn`/`blockOnPr`/`blockOnShip`/`blockOnRelease`), `EnvironmentClass`
(`local`/`ci`/`localOrCi`/`release`), and the finding `Zone` tag — with **local, total `match`
helpers hidden in `RouteJson.fs`**, not on any public surface. Reuse the **existing** owning-model
renderers where they exist: `Gates.gateIdValue` for the `GateId` string and
`Findings.findingIdToken` for the finding id. Unwrap the single-case newtypes (`DomainId`, `Owner`,
`CheckId`, `CommandId`, `SurfaceId`, `GovernedPath`, `TimeoutLimit`) by pattern match at the use site.

**Rationale.** This is exactly how `Kernel/Json.fs` handles its enums: `severityToken`, `stateToken`,
`writeOutcome`, and `writeVerdict` are defined *inside* `Json.fs` and are absent from `Json.fsi` —
the JSON layer owns its own wire tokens as hidden plumbing. route.json is the **first** consumer to
serialize `Cost`/`Maturity`/`EnvironmentClass`, so defining the tokens locally is the minimal,
YAGNI-correct choice (Principle III): it adds no surface to Config and invents no shared abstraction
before a second consumer exists. If audit.json later needs the same tokens, it can promote them to the
owning Config model then (where `diagnosticIdToken` already lives), exactly as the pattern anticipates
("for messages, tests, and any later JSON"). Reusing `gateIdValue`/`findingIdToken` honors FR-002/
FR-010 (the `GateId` string is rendered verbatim by the owning model, never re-parsed) and FR-005
(the finding id token is the owning model's, not re-derived).

**Closed-match completeness.** Each token helper matches the closed DU exhaustively (no wildcard), so
adding a `Cost`/`Maturity`/`EnvironmentClass`/`FindingZone` case later is a compile error here — the
projection cannot silently drop or mis-token a new tier. This is the same "closed so tests assert
exactly the cases that exist" discipline the upstream models use.

## D4 — Contract shape: emit-only `ofRouteResult : RouteResult -> string` (no parallel document type)

**Decision.** The public surface is a single projection `ofRouteResult : RouteResult -> string` plus a
`schemaVersion : string` constant. **No** typed "route.json document" model is introduced; the
"document" is the emitted JSON value, and its sections (schema version, selected gates, findings, cost)
are fields within it. The spec's "Key Entities" (document / selected-gate entry / route trace /
findings section / cost rollup) are realized as the JSON object shape, **not** as new F# types.

**Rationale.** A typed intermediate `RouteJsonDocument` would merely **duplicate** `RouteResult`'s
shape (selected gates + findings + cost) with a schema-version field bolted on — two sources of truth
for the same data, and a second public type family to baseline and keep in sync. The kernel's
`ofExplanation`/`ofContract` return `string` directly for the same reason. Determinism is asserted by
comparing emitted **bytes**; structural assertions (gate present, metadata carried, trace recorded,
findings unchanged, no excluded field) are made by a **read-only `JsonDocument` parse in the test
project** — real evidence over the actual emitted document, the way the kernel's JSON tests inspect
their output. Round-trip *parsing back to a `RouteResult`* is **out of scope** for this row (the spec
is "the pure projection alone"); a later consumer that needs to read route.json can add `toRouteResult`
when it has a use, mirroring how the kernel pairs each `of*` with a `to*` only because it needed both.

**`schemaVersion` exposed as a constant.** The version token stamped into every document is also
exposed as `val schemaVersion : string` so consumers and snapshot tests can reference the declared
version without string-scraping the output (FR-013) — the small public convenience the kernel omitted
only because its tokens are inline.

## D5 — The document shape (the observable wire contract)

**Decision.** A single top-level JSON object with fields emitted in this **fixed order**:
`schemaVersion`, `selectedGates`, `findings`, `cost`. Each collection is rendered in the order
**already fixed by `RouteResult`** (selected gates by `GateId`; each gate's selecting paths by
normalized path; findings in F017 order). The per-field shape, tokens, and a worked sample are fixed
in [`contracts/route-json-document.md`](./contracts/route-json-document.md). Summary:

- **`selectedGates`** — array, one object per `SelectedGate`, fields in order: `id`, `domain`,
  `description`, `cost`, `timeout`, `owner`, `maturity`, `productCheck`, `prerequisites`,
  `freshnessKey`, `selectingPaths`. `prerequisites` is an array of `{ "requiresCommand": "<id>" }`;
  `freshnessKey` is `{ check, domain, cost, environment, command }` with `command` rendered as the
  JSON `null` when `None`; `selectingPaths` is an array of `{ path, matchedGlob }`.
- **`findings`** — array, one object per carried `UnknownGovernedPathFinding`, fields in order: `id`
  (the `findingIdToken`), `path`, `zone`, `message`. `zone` is the JSON string `"governedRootUnknown"`
  or the tagged object `{ "protectedBoundary": "<surfaceId>" }` (FR-005 carries it unchanged).
- **`cost`** — object `{ cheap, medium, high, exhaustive }`, every declared tier present with its
  integer count including zero (FR-006).

**Rationale.** The order and shape trace one-to-one onto `RouteResult` + the embedded `Gate`/
`FreshnessKey`/`FindingReport`, so the projection re-derives nothing (FR-002/FR-010) and the document
records exactly what the upstream rows typed. Every value is a declared id string, a declared-enum
token, a carried metadata scalar, or a carried finding — never raw YAML, a host path, a timestamp, an
environment-derived value, or any severity/enforcement/verdict field (FR-011/FR-012, SC-007).

## D6 — What is NOT consumed, and what is NOT produced

**Not consumed.** Nothing beyond the `RouteResult` value: no git, no `.fsgg`, no clock, no
`RouteReport.Diagnostics` (the F015 routing diagnostics are not on `RouteResult` and are not read).

**Not produced (held by FR-011/FR-012).** No `toRouteResult` parse (D4); no severity / profile / mode
/ maturity-as-enforcement / profile-adjusted enforcement; no evidence-freshness or cache-eligibility
**verdict** (the gate's `FreshnessKey` *inputs* are carried inside the gate object, never evaluated —
FR-014); no ship verdict / blockers / warnings / exit-code basis; no expected-artifacts field (the
`Gate` carries none); no file write (persisting to `readiness/<id>/route.json` is the later CLI/host
edge); no `fsgg route`/`fsgg ship` command. Those are the remaining Phase-2 rows and Phase 5/11 that
*consume* this document.

## D7 — Testing strategy: real upstream values, end-to-end through the public surface

**Decision.** Tests drive `RouteJson.ofRouteResult` over a **real** `RouteResult` assembled by the
genuine upstream functions — `Gates.buildRegistry` → `Routing.route` → `Findings.findUnknownGovernedPaths`
→ `Route.select`, all from real `TypedFacts` built via Config — and inspect the emitted bytes with a
read-only `JsonDocument` parse in the test project. No mocks, no hand-forged `RouteResult`, no private
helpers (Principle V). FsCheck supplies the permutation-invariance and totality properties (D8).

**Rationale.** This row's whole value is a faithful render of F019's output, so the most honest
evidence runs the actual F015→F017→F018→F019→F020 chain over real fixtures — the exact value the future
`fsgg route` will project — which also transitively re-exercises the upstream rows. The `Support.fs`
fixture is the F019 chain (the prelude already builds `f19Facts`/`f19Result`), extended with an
empty-change route and a findings-only route (a governed path with no selected gate) so US4's edge
cases are real, not literals. **No synthetic evidence is anticipated**: every document case is
reachable from real upstream outputs. Any unavoidable literal would carry `Synthetic` in the test name
with a use-site disclosure and be listed in the PR.

## D8 — Determinism & permutation invariance

**Decision.** The projection imposes **only** the fixed object-field order (the writer's call
sequence); it preserves `RouteResult`'s already-fixed collection orders verbatim and iterates no `Map`.
Proven by FsCheck properties: (a) `ofRouteResult r = ofRouteResult r` byte-for-byte over generated
well-typed results (twice-identical, SC-002); (b) two `RouteResult`s produced from differently-ordered
upstream inputs (permuted candidate paths and registry gate list) — equal as values by F019's
determinism — project to identical strings (SC-003); (c) totality: `ofRouteResult` returns a string and
never throws for any generated result, including the empty and findings-only routes (SC-006).

**Rationale.** A non-deterministic document cannot back a byte-stable route.json snapshot. Because
F019 already guarantees value-equality is order-independent and the projection adds no new ordering
decision beyond the fixed field sequence, permutation-invariance of the output follows from F019's
permutation-invariance of the value — the property is inherited, and the test pins it.

## D9 — Resolved Technical Context

| Unknown | Resolution |
|---|---|
| Language/Version | F# on .NET, `net10.0` (`Directory.Build.props`). |
| Primary dependencies | No new third-party package. One ProjectReference: Route (Gates/Routing/Findings/Config transitive). Serialization via shared-framework `System.Text.Json`. |
| Storage | None — pure in-memory value → string; persisting the string is a later edge. |
| Testing | `dotnet test` (Expecto + FsCheck via VSTest) over a real F015→F019 `RouteResult`, inspected by read-only `JsonDocument` parse (D7). |
| Target platform | Cross-platform .NET library; validated on Linux dev host. No git/filesystem/clock touched. |
| Project type | Optional packable F# class library + one test project (same shape as Config/Routing/Findings/Gates/Route). |
| Performance | One linear walk of the already-ordered `RouteResult` through a single `Utf8JsonWriter`; no `Map` iteration, no re-sort. |
| Constraints | Pure & total (FR-008/FR-009): no I/O, git, clock; never throws; empty route is a valid document. Declared strings verbatim (FR-010); findings unchanged (FR-005); exclusions held (FR-011/FR-012). |
| Scale/Scope | One production project + one test project; public module `RouteJson` (`ofRouteResult` + `schemaVersion`); one surface baseline. |
