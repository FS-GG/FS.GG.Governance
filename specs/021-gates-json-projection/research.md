# Phase 0 Research: Deterministic gates.json Projection (F021)

All NEEDS CLARIFICATION from the spec's two plan-time deferrals (project home, serialization mechanism)
are resolved below, plus the supporting decisions the contract and tests rest on. Each item records the
**Decision**, **Rationale**, and **Alternatives considered**. The immediate precedent is F020
(`FS.GG.Governance.RouteJson`), the just-merged sibling pure projection — most decisions here are "do
exactly what F020 did, retargeted from `RouteResult` to `GateRegistry`."

## D1 — Project home: a new `FS.GG.Governance.GatesJson` library referencing only `Gates`

**Decision.** Create a new optional, packable class library `src/FS.GG.Governance.GatesJson` with a
single public module `GatesJson`, referencing **only** `FS.GG.Governance.Gates`
(`FS.GG.Governance.Config` arriving transitively). No kernel/host edge; no new package dependency.

**Rationale.** This continues the one-row-one-library rhythm of F014–F020 and is the direct mirror of
F020's `RouteJson → Route` decision. The constitution requires "heavier capabilities layer on top in
separate projects, not into the core" — serialization layers on top of F018's pure `Gates` assembler
without touching it. Keeping the projection out of `Gates` leaves F018's surface free of any JSON
concern, and keeps the dependency direction strictly one-way (`GatesJson → Gates → Config`). The library
is the whole-catalog sibling of the per-change `RouteJson`; both read upstream typed values and emit a
versioned JSON document.

**Alternatives considered.**
- *Add a `Json` module to `FS.GG.Governance.Gates`.* Rejected: pollutes F018's assembler surface with a
  serialization concern and a `System.Text.Json` usage it does not otherwise need; breaks the
  one-row-one-library rhythm; the constitution explicitly wants layered capabilities in separate
  projects. (Same rejection F020 made for adding a module to `Route`.)
- *Reuse the kernel `FS.GG.Governance.Kernel.Json` module.* Rejected: the gate vocabulary
  (`GateId`/`Cost`/`Maturity`/`FreshnessKey`) must not reach the kernel (FR-015); the kernel must stay
  product-neutral. Reuse the *mechanism* (`Utf8JsonWriter`), not the module.
- *Fold gates.json into `RouteJson`.* Rejected: gates.json projects a different upstream value
  (`GateRegistry`, not `RouteResult`) and `RouteJson` references `Route`, a heavier dependency than this
  row needs. A whole-catalog projection should depend only on the catalog producer (`Gates`), not on the
  per-change join. Two sibling libraries, each referencing its own minimal upstream, is the clean shape.

## D2 — Serialization mechanism: `System.Text.Json` `Utf8JsonWriter`, hand-driven, no new dependency

**Decision.** Serialize with `System.Text.Json`'s `Utf8JsonWriter` (default options ⇒ compact,
non-indented output), hand-driving the field order through explicit writer calls, exactly as
`Kernel/Json.fs` and `RouteJson.fs` do. Emit to a `MemoryStream` and `Encoding.UTF8.GetString` the
bytes (the `writeToString` helper, copied verbatim from `RouteJson.fs`).

**Rationale.** `System.Text.Json` is part of the `net10.0` shared framework — a `System.*` API, **not**
a third-party `PackageReference` — so it adds no dependency and keeps the library `System.*`/FSharp.Core
only (FR-015). Default `Utf8JsonWriter` options produce compact, deterministic output with no
indentation, so the bytes are stable for identical inputs (SC-002). Hand-driving the writer (rather than
serializing a record via reflection) gives an explicit, documented field order that is part of the wire
contract (FR-007) and avoids any attribute/property-order surprise.

**Alternatives considered.**
- *A new JSON `PackageReference` (Newtonsoft, etc.).* Rejected: violates dependency minimalism (FR-015)
  for zero benefit; the BCL writer already does everything needed.
- *Reflection-based `JsonSerializer.Serialize` over a typed document record.* Rejected: field order
  would depend on declaration/attribute order and option-handling defaults, making the contract
  implicit and harder to pin; also pulls in a parallel typed document (see D4). Hand-driven writing is
  the kernel's and F020's established, audited approach.
- *Hand-rolled string concatenation.* Rejected: re-implements JSON escaping (the description and ids
  may contain JSON-significant characters — see the spec edge case), which the writer does correctly for
  free; error-prone and not worth it.

## D3 — Closed-enum wire tokens as hidden local helpers, exhaustive with no wildcard

**Decision.** Render `Cost`, `Maturity`, and `EnvironmentClass` to their wire tokens via small local
`match` helpers (`costToken`, `maturityToken`, `environmentToken`) defined in `GatesJson.fs` and
**absent** from `GatesJson.fsi` (hidden by the `.fsi`). Each `match` is **exhaustive over the closed DU
with no wildcard**.

**Rationale.** This mirrors `Kernel/Json.fs` (`severityToken`/`stateToken`) and `RouteJson.fs`
(`costToken`/`maturityToken`/`environmentToken`) keeping token plumbing off the public surface
(Principle II). The no-wildcard exhaustive match means a future `Cost`/`Maturity`/`EnvironmentClass`
case becomes a **compile error here**, never a silently mis-tokened field — the type system guards the
wire vocabulary. The tokens MUST be identical to F020's so the shared gate fields render the same string
in both `gates.json` and `route.json`:
- `Cost`: `cheap` | `medium` | `high` | `exhaustive`
- `Maturity`: `observe` | `warn` | `blockOnPr` | `blockOnShip` | `blockOnRelease`
- `EnvironmentClass`: `local` | `ci` | `localOrCi` | `release`

**Alternatives considered.**
- *Expose the token helpers publicly / share them from a common module.* Rejected: they are internal
  rendering plumbing, not a contract; exposing them widens the surface for no consumer need. The tiny
  duplication with `RouteJson.fs` is acceptable (three one-line matches) and keeps each library
  self-contained with no shared-token coupling — the same call F020 implicitly made. A shared-token
  module would be a cross-cutting refactor out of scope for this row.
- *`ToString()` / a catch-all wildcard.* Rejected: loses the compile-time exhaustiveness guard and
  risks drift between the F# case name and the intended wire token.

## D4 — Emit-only contract: `ofGateRegistry : GateRegistry -> string` + a `schemaVersion` constant

**Decision.** The public surface is exactly two values: `ofGateRegistry : GateRegistry -> string` and
`schemaVersion : string` (`= "fsgg.gates/v1"`). No parallel typed "document" model is introduced; tests
inspect the emitted bytes by read-only `JsonDocument` parse.

**Rationale.** A typed document record would merely duplicate `GateRegistry`/`Gate` (which are already
the typed model) and add a maintenance surface with no consumer. The artifact's *contract* is the wire
shape (field order, tokens, exclusions), fixed in `contracts/gates-json-document.md` and asserted
against the parsed bytes — not a second F# type. This is F020's D4 decision verbatim. The
`schemaVersion` is a separate exported constant so consumers can branch on the contract version and
tests can assert the stamp without string-scraping.

**Schema version token.** `"fsgg.gates/v1"` — parallel to F020's `"fsgg.route/v1"`. The `fsgg.<artifact>/v<n>`
shape is the established convention; `gates` names the whole-catalog artifact, distinct from `route`.

**Alternatives considered.**
- *A typed `GatesDocument` record + serializer.* Rejected: duplicates the model, widens the surface,
  and still needs the same field-order discipline. Emit-only is simpler (Principle III).
- *Round-trip parse (`gates.json -> GateRegistry`).* Rejected: out of scope (FR-011 is exclusion-only;
  the spec's Assumptions fix "the pure projection only"). Reading gates.json back is a later consumer's
  concern.

## D5 — Per-gate field set = F020 `selectedGates[*]` minus `selectingPaths`; top-level `{ schemaVersion, gates }`

**Decision.** The top-level object is `{ schemaVersion, gates }` (field order fixed). Each `gates[*]`
entry renders, in this fixed order: `id`, `domain`, `description`, `cost`, `timeout`, `owner`,
`maturity`, `productCheck`, `prerequisites`, `freshnessKey` — the **exact** F020 `selectedGates[*]`
gate-entry field set and order, **minus** the route-specific `selectingPaths`. Sub-shapes
(`prerequisites[*] = { requiresCommand }`, `freshnessKey = { check, domain, cost, environment, command }`)
are identical to F020's.

**Rationale.** gates.json is the whole-catalog view; route.json is the per-change view. The *gate
identity and metadata* is the same in both — so the shared fields must render identically (same names,
order, tokens), making the two artifacts visually and structurally consistent and letting a consumer
reuse a gate-entry parser across both. The only difference is route's per-change additions
(`selectingPaths` per gate, plus top-level `findings` and `cost` rollup), which are absent from the
whole-catalog view by definition (FR-011: no per-change selection, route trace, or cost rollup). Mapping
each F021 field to its `Gate` source:

| Field | JSON type | Source (`Gate g`) | Token / rendering |
|---|---|---|---|
| `id` | string | `g.Id` | `Gates.gateIdValue` — declared `GateId` string verbatim (FR-002, FR-010). |
| `domain` | string | `g.Domain` | `DomainId` unwrapped verbatim (FR-010). |
| `description` | string | `g.Description` | Carried verbatim (FR-002); JSON-escaped by the writer (FR-012). |
| `cost` | string | `g.Cost` | `costToken` (D3). |
| `timeout` | number (int) | `g.Timeout` | `TimeoutLimit` seconds, verbatim — not re-derived (FR-006). |
| `owner` | string | `g.Owner` | `Owner` unwrapped verbatim. |
| `maturity` | string | `g.Maturity` | `maturityToken` — declared, **not** enforcement (FR-005/FR-011). |
| `productCheck` | boolean | `g.ProductCheck` | Carried verbatim (FR-014 §3). |
| `prerequisites` | array\<object\> | `g.Prerequisites` | Each `RequiresCommand c` → `{ "requiresCommand": "<commandId>" }`, in carried order; present-and-empty when none (FR-004). |
| `freshnessKey` | object | `g.FreshnessKey` | Carried key **inputs** only — never a cache verdict (FR-014). |

**Alternatives considered.**
- *A different/condensed gate shape than route.json.* Rejected: gratuitous divergence makes the two
  artifacts harder to consume together; the spec frames them as sibling projections, so the shared
  fields should match exactly.
- *Include `selectingPaths` / a cost rollup "for parity."* Rejected: those are per-change concepts that
  do not exist for a whole-catalog registry (a gate is not "selected by a path" in the catalog);
  emitting them would invent route semantics (FR-011).

## D6 — Determinism without a sort step: trust the registry's `GateId` ordinal order

**Decision.** Emit `gates` in `result.Gates` order with **no re-sort**, and each gate's `prerequisites`
in their carried list order. Add no ordering decision beyond the fixed field sequence.

**Rationale.** F018's `Gates.buildRegistry` already sorts `GateRegistry.Gates` by `GateId` ordinal and
guarantees re-ordering the input checks never changes the result (its FR-011/FR-012, SC-003/SC-006). The
projection inherits that determinism for free — re-sorting would be redundant work and a second place
for an ordering bug. Because the registry is a `list` (not a `Map`), there is no key-iteration order to
stabilize. Permutation-invariance (US2 scenario 2 / SC-003) is therefore a property of F018 that this
projection *preserves*, asserted end-to-end here by projecting two registries built from
differently-ordered check lists and comparing the bytes.

**Alternatives considered.**
- *Re-sort gates by id in the projection.* Rejected: redundant (F018 already did it) and introduces a
  second ordering authority that could drift from F018's.

## D7 — Test inputs: real `GateRegistry` from `Gates.buildRegistry`, never mocks

**Decision.** Build test inputs by assembling a real `TypedFacts` (governed root, domains, checks,
optional commands) and running the genuine `FS.GG.Governance.Gates.Gates.buildRegistry` — a `Support.fs`
fixture builder mirroring the F019/F020 `Support.fs`, trimmed to the F014→F018 step (no
routing/findings/route needed, since gates.json projects the registry directly). Inspect the emitted
bytes via read-only `JsonDocument` parse.

**Rationale.** Principle V prefers real evidence; the registry is cheap to assemble in-memory with no
I/O, so there is no reason to mock it. Driving `buildRegistry` transitively re-exercises F018 and
catches any projection-time mismatch (e.g. a field the registry carries that the projection drops) that
a hand-built `Gate` literal could hide. The empty registry, single-gate, many-gate, with/without
prerequisites, and with/without freshness-command cases are all reachable by varying the check list — so
**no synthetic evidence is anticipated**.

**Alternatives considered.**
- *Hand-construct `Gate`/`GateRegistry` literals.* Rejected where avoidable: a literal can drift from
  what `buildRegistry` actually produces, weakening the evidence. Real assembly is the F019/F020
  precedent. (FsCheck generators in `TotalityTests` may construct well-typed registries directly for the
  totality property — that is a property over *any* well-typed registry, the documented case where
  generated values are appropriate, and is not "synthetic evidence" of behavior.)

## D8 — Freshness key carried as inputs only; the writer escapes free text

**Decision.** Render `freshnessKey` as the five carried inputs `{ check, domain, cost, environment,
command }` with `command` a JSON string when `Some` and JSON `null` when `None` — never computing a
cache verdict (FR-014). Write `description` (and all string fields) through the writer's string API so
JSON-escaping of significant characters is the writer's responsibility, never manual.

**Rationale.** The freshness key's *inputs* are present so the later Phase-11 cache step has them; this
row evaluates nothing over them (the spec's Assumptions and FR-014). The `None` command must render as
an explicit `null` (distinguishable from a present command, FR-014 §2 / SC-004), which `WriteNull`
gives. `System.Text.Json` escapes `'`/`<`/`>`/`&` as `\uXXXX` by default — deterministic and
round-trip-faithful (F020 verified this with its JSON-special carry test); the same property covers this
row's "free-text description containing JSON-significant characters" edge case, asserted by a
round-trip-equality test on the carried description.

**Alternatives considered.**
- *Omit `command` when `None`.* Rejected: FR-014 §2 requires an explicit absent value, not a dropped
  field — a consumer must distinguish "no command" from "field missing."
- *Manual escaping.* Rejected: error-prone and redundant; the writer already escapes correctly.

## Resolved Technical Context

No NEEDS CLARIFICATION remain. Language/stack (`F#`/`net10.0`), dependency (`Gates` only, no new
package), serialization (`System.Text.Json` `Utf8JsonWriter`), testing (`dotnet test`, Expecto+FsCheck
over real `buildRegistry` inputs), and project shape (one library + one test project) are all fixed
above and recorded in the plan's Technical Context.
