# Phase 0 Research: Typed Gate Registry (F018)

All Technical Context unknowns are resolved below. No `NEEDS CLARIFICATION` remains. The genuinely
open scope question — how to source the gate-metadata fields F014's MVP schema does not declare —
was settled with the maintainer at plan time (D4/D5/D6), exactly as F017's FR-007 precedence was.

## D1 — Project home: a new `FS.GG.Governance.Gates` library

**Decision.** Land the feature as a new optional, packable library `FS.GG.Governance.Gates`
(plus its test project), sibling to Config/Routing/Snapshot/Findings. It references **only**
`FS.GG.Governance.Config` (the typed-fact model: `CapabilityFacts`, `Check`, `ToolingFacts`,
`CommandSpec`, and the `DomainId`/`Owner`/`Cost`/`Maturity`/`TimeoutLimit`/`CommandId`/
`EnvironmentClass`/`CheckId` newtypes). **No new third-party `PackageReference`** — its own code is
BCL + FSharp.Core only (the transitive YamlDotNet edge arrives only via Config and is unused here).

**Rationale.** Mirrors the established one-library-per-row shape (F014 Config, F015 Routing, F016
Snapshot, F017 Findings). The registry derives entirely from *declared facts*; it does **not** need
F015 routing — gate *selection* for a route (which intersects changed paths with capability gates)
is a later Phase-2 row that will reference both Routing and Gates. Keeping the dependency direction
one-way (`Gates → Config`) and Routing-free keeps the registry a pure projection of the catalog and
the kernel untouched (FR-016, constitution operating rule).

**Alternatives rejected.**
- *Add a module to `FS.GG.Governance.Config`.* Rejected: the gate registry is a *projection* of the
  validated facts into a new gate vocabulary, not part of parsing/validating YAML; folding it in
  would bloat Config's surface and blur the "typed facts" boundary every later row consumes.
- *Add to `FS.GG.Governance.Routing`.* Rejected: routing answers "which domain owns this path"; the
  gate registry answers "what are the stable gate identities" — independent of any change. Coupling
  them would force a Routing dependency the registry does not need.
- *Put it in Host/Cli.* Rejected: the model and assembler are product-neutral pure values; the
  whole-registry consumers (the later `route`/`ship` commands, the `gates.json` emitter) live in
  Cli and will *reference* Gates, exactly as they will reference Routing/Findings/Snapshot.

## D2 — Boundary shape: a pure total function, no MVU

**Decision.** A single pure entry point `Gates.buildRegistry : TypedFacts -> GateRegistry`, over a
`Model` of gate types. No `Model`/`Msg`/`Effect`/`update`.

**Rationale.** The feature performs no I/O, senses no git, holds no multi-step state — it is a
deterministic projection of already-typed, already-validated inputs (FR-013). Principle IV mandates
the MVU boundary only for stateful or I/O-bearing features; this is neither, so the plain pure
function is the idiomatic choice (Principle III), exactly as F015 `route` and F017
`findUnknownGovernedPaths` are.

## D3 — `GateId` derivation: domain-qualified check id, injective

**Decision.** One gate per declared `Check`; `GateId = GateId "<domainText>:<checkIdText>"`, the
domain-qualified check id. Rendered back by `gateIdValue : GateId -> string`.

**Rationale.** F014 already guarantees `CheckId` uniqueness catalog-wide and `Check.Domain`
resolution, so a `GateId` built from `(domain, checkId)` is **injective over distinct checks** — no
two gates collide and none is dropped or merged (FR-003, FR-005). Qualifying by domain is belt-and-
suspenders: even a (non-F014) fact set that reused a check id across domains keeps the gates
distinct. The id is a pure function of declared ids — stable across runs, machines, and order, never
positional or time-derived. This is the "stable machine id used in route, evidence, and audit JSON"
the design's *Gate identities* table names.

**Alternative rejected.** *`GateId = checkId` (un-qualified).* Faithful (CheckId is catalog-unique)
but loses the cross-domain robustness for one character of brevity; the qualified form is the safer
default and reads clearly in route/audit output.

## D4 — Validation: preserved by construction, NOT re-emitted as diagnostics (maintainer-confirmed)

**Decision (maintainer-confirmed at plan time).** The registry assembly is **total** and emits **no
diagnostics**. `GateRegistry = { Gates : Gate list }`. The internal-consistency guarantees the spec
calls for (unique gate ids, resolved prerequisites) are **preserved by construction** and **proven by
property tests**, not detected at runtime.

**Rationale.** `buildRegistry` consumes `Valid TypedFacts`, which F014's `Schema.validate` has
*already* proven to have unique check ids (`DuplicateId`) and resolved cross-references
(`Check.Command` resolves even when `tooling.yml` is absent — `DanglingReference`). A duplicate-id /
dangling-prerequisite / cycle diagnostic layer therefore **cannot fire on any valid input** — it
would be dead machinery, exactly the unjustified complexity Principle III forbids, and a break from
the F017 precedent (which returns `FindingReport` with no diagnostics, trusting its typed inputs).
The honest design is a total function whose guarantees are verified by FsCheck over arbitrary valid
facts (US2): `List.distinct` of gate ids has length = check count; every `RequiresCommand` resolves;
assembly never throws. This is *stronger* evidence than a never-triggered diagnostic.

**Alternative rejected.** *Keep a defensive `GateDiagnostic` set, provably empty for valid facts,
exercised by disclosed adversarial inputs.* Considered and dropped: it adds public surface and a
synthetic-evidence test path for a code branch no real producer of `TypedFacts` can reach. If a
future row ever assembles a registry from *un-validated* facts, a thin validation wrapper can be
added then with a real trigger; speculative defense now is not justified.

## D5 — Prerequisites: declared command reference only; gate-to-gate deferred (maintainer-confirmed)

**Decision (maintainer-confirmed at plan time).** A gate's `Prerequisites : GatePrerequisite list`
where `GatePrerequisite = RequiresCommand of CommandId`. Populated from the check's declared
`Command` (`Some c ⇒ [RequiresCommand c]`, `None ⇒ []`). **Gate-to-gate prerequisites are deferred to
Phase 10.**

**Rationale.** The design's *Gate identities* table defines `prerequisites` as "Gates or facts
required before this gate runs." The *only* such dependency F014's MVP schema actually declares is a
check's `Command` — a genuine fact prerequisite ("this gate cannot run until command `c` is
available"), and one F014 has already proven resolvable. Real gate-to-gate dependencies need a
*declared edge* the F014 MVP does not provide; the cost tiers it *does* declare are an expense
ordering, **not** a run-prerequisite (a structural-scan gate is not a prerequisite of a full-verify
gate — they are independent checks at different costs). Conflating the two would invent governance
semantics. So this row carries the typed `Prerequisites` field, fills it from the one real declared
source, and documents the Phase-10 extension point (where the cost-tiered generated-product checks
and a real prerequisite declaration land).

**Consequence for cycles/ordering (FR-006/FR-007/FR-012).** With no gate-to-gate edges, the gate
dependency graph is trivially acyclic and the dependency-respecting order reduces to the `GateId`
sort (D7). The topological-order + cycle-detection machinery is **not built** in this MVP — it would
operate on an always-empty edge set (dead code). It is the documented extension point for Phase 10.

**Alternative rejected.** *Derive gate-to-gate prerequisites from cost tiers.* Rejected as
semantically wrong (cost ≠ prerequisite) and as inventing a dependency relation the catalog does not
declare. *Leave `Prerequisites` always empty.* Rejected as needlessly hollow when `Check.Command` is
a real, declared, useful fact prerequisite a later route/audit can surface.

## D6 — Product-check: MVP environment-class heuristic; product-domain tagging deferred (maintainer-confirmed)

**Decision (maintainer-confirmed at plan time).** `Gate.ProductCheck : bool`, set **true iff the
check's declared `EnvironmentClass = Release`**. Richer derivation deferred to Phase 10.

**Rationale.** The design defines `productCheck` as "whether the gate validates generated consumers."
F014's MVP facts contain **no check↔surface link** (surfaces carry `Id`/`Class`/`Paths`/`Owner`/
`Maturity` but no domain; checks carry a domain but no surface), so a faithful "does this check
govern a product/release surface" cannot be derived without re-introducing path routing (deliberately
excluded, D1). The closest *declared* signal is the check's environment class: a `Release`-environment
check is the catalog's way of marking release/generated-consumer validation. Using it gives the field
a live, deterministic, testable true/false split now (US4: a `Release` check ⇒ `true`, a `Local`/`Ci`
check ⇒ `false`) and is trivially refined in Phase 10, which adds product-domain/surface tagging.

**Alternative rejected.** *Default `productCheck = false` and defer all derivation.* Fully honest but
leaves the field inert and US4's true-case untestable from real facts. *Derive via surface/path
overlap.* Rejected: needs Routing (or a re-implemented path matcher), enlarging the dependency
footprint for a value Phase 10 will redo properly.

## D7 — Determinism mechanics

**Decision.** The gate list is sorted by `String.CompareOrdinal` on `gateIdValue` — the identical
ordinal-sort discipline F014/F015/F016/F017 use. Prerequisite lists (at most one element in the MVP)
are likewise ordinal-stable. Any `mutable` accumulator in the projection fold is disclosed at the use
site (Principle III).

**Rationale.** Reusing the repo-wide ordinal-sort convention guarantees byte-identity (FR-011, SC-003,
SC-006) and cross-feature consistency, and makes "re-order the inputs ⇒ unchanged output" hold by
construction rather than by test luck.

## D8 — Freshness key: declared identity inputs, carried not evaluated

**Decision.** `FreshnessKey = { Check : CheckId; Domain : DomainId; Cost : Cost; Environment :
EnvironmentClass; Command : CommandId option }` — a record of the declared identity inputs a later
freshness/cache step will hash. Carried on every gate; never evaluated here.

**Rationale.** The design defines `freshnessKey` as "inputs used to decide whether prior evidence can
be reused." The kernel's existing `Freshness.decide : recorded -> covered -> Freshness` is the
*evaluation* (over instants); this is the *key* (the input identity) — kept strictly distinct
(FR-009). The MVP key is the always-available declared identity; Phase 11 extends it with rule/artifact
hashes, command version, and base/head. No clock, no I/O, ids only (SC-004, SC-007).

## D9 — Timeout: from the referenced command, else a documented default

**Decision.** `Gate.Timeout : TimeoutLimit` = the `CommandSpec.Timeout` of the check's referenced
command when `Check.Command = Some c` and `c` resolves in `Tooling.Commands`; otherwise a documented
default `defaultTimeout = TimeoutLimit 300` (5 minutes). Never zero or unbounded (FR-010, SC-005).

**Rationale.** The command spec is where F014 declares a per-command timeout, so the gate's timeout is
its command's timeout. A check with no command still needs a bounded timeout class for the later
route/audit; a single documented default keeps the field total without inventing per-check values.
The feature only *carries* the timeout — it never enforces or measures one.

## D10 — Testing strategy (Principle V real evidence)

**Decision.** Pure unit + property tests over real in-memory `TypedFacts` — the actual values a
downstream caller passes, not mocks. No git, filesystem, or network is reachable, so "real evidence"
here *is* real typed facts exercised through the public surface:
- US1 (`GateBuildTests`): N checks ⇒ N gates; each gate's id/domain/cost/owner/maturity/timeout/
  description match the declared check; twice-identical ids.
- US2 (`RegistryInvariantTests`, FsCheck): over arbitrary valid `CapabilityFacts`, gate ids are
  distinct, gate count = check count, every `RequiresCommand` resolves, assembly never throws.
- US3/US5 (`DeterminismTests`, FsCheck): compute twice ⇒ byte-identical; permute checks/commands ⇒
  unchanged `GateId`-ordered list; fields name only declared ids.
- US4 (`MetadataTests`): `Release`-env check ⇒ `productCheck = true`, `Local`/`Ci` ⇒ `false`; every
  gate carries a non-empty freshness key of declared inputs; default timeout applied when no command;
  command timeout applied when present.
- `SurfaceDriftTests`: guards `surface/FS.GG.Governance.Gates.surface.txt`.
- An FSI transcript in `scripts/prelude.fsx` exercises the packed surface end-to-end (Principle I).

**Rationale.** A pure projection's honest evidence is real typed facts exercised through the public
surface, asserting behavior not internals (Principle V). No synthetic evidence is anticipated — every
case is reachable from real `Valid TypedFacts`. (This is a direct dividend of D4: by refusing the
never-triggered diagnostic layer, the whole suite stays real-evidence with no `Synthetic` disclosure.)

## Resolved Technical Context

| Field | Value |
|---|---|
| Language/Version | F# on .NET `net10.0` (Directory.Build.props) |
| Primary Dependencies | `FS.GG.Governance.Config` project ref only; **no new third-party package**; no Routing dep |
| Storage | None — pure in-memory values |
| Testing | `dotnet test` (Expecto + FsCheck via VSTest) |
| Target Platform | Cross-platform .NET library |
| Project Type | Optional packable F# class library + one test project |
| Performance Goals | Deterministic O(checks) projection + one ordinal sort; byte-identical output |
| Constraints | Pure, total, never throws, no I/O/git/clock; ordinal-deterministic; declared ids only |
| Scale/Scope | One new src project + one test project; modules `Model`, `Gates`; one closed `GatePrerequisite` set; no diagnostics |
