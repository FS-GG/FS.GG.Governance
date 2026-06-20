# Phase 0 Research: Route Gate Selection (F019)

All Technical Context unknowns are resolved below. No `NEEDS CLARIFICATION` remains. The two scope
questions the spec explicitly deferred to plan time — the **project home** (a new `Route` library vs.
an addition to an existing one) and the **cost-rollup value shape** (multiset of tiers vs. summed
scalar) — are settled here (D1, D5), grounded in spec assumptions and the F014→F018 precedent.

## D1 — Project home: a new `FS.GG.Governance.Route` library

**Decision.** Land the feature as a new optional, packable library `FS.GG.Governance.Route` (plus its
test project), sibling to Config/Routing/Snapshot/Findings/Gates. It references **three** upstream
projects — `FS.GG.Governance.Gates` (the `GateRegistry`/`Gate`/`GateId`), `FS.GG.Governance.Routing`
(the `RouteReport`/`PathRouting`/`RoutingResult`), and `FS.GG.Governance.Findings` (the
`FindingReport`) — with `FS.GG.Governance.Config` arriving transitively (the `GovernedPath`/`Cost`/
`DomainId` newtypes). **No new third-party `PackageReference`** — its own code is BCL + FSharp.Core
only (the transitive YamlDotNet edge arrives via Config and is unused here).

**Rationale.** Mirrors the established one-library-per-row shape (F014 Config, F015 Routing, F016
Snapshot, F017 Findings, F018 Gates). This is the first row whose natural job is to *join* three
prior rows' typed outputs, so it is the first library to reference Routing + Findings + Gates
together — exactly the consumer F018's research D1 predicted ("gate *selection* by route is a later
row that will reference both Routing and Gates"). The dependency direction stays one-way
(`Route → {Gates, Routing, Findings} → Config`); the kernel/host stay untouched (FR-013).

**On the name `Route` vs. the existing `Routing`.** The spec names the candidate `FS.GG.Governance.
Route`, and that is adopted. The proximity to `Routing` is real but the two are genuinely distinct
nouns: *Routing* answers "which domain owns each path"; *Route* is the resolved route trace ("which
gates this change selects, and why"). The namespaces never collide (each module is qualified), and
the route trace is precisely "the Route" the design's route.json serializes. Renaming Routing is out
of scope.

**Alternatives rejected.**
- *Add a module to `FS.GG.Governance.Gates`.* Rejected: it would force `Gates → Routing` — the very
  edge F018's D1 deliberately avoided to keep the registry a change-independent projection of the
  catalog. Selection is change-dependent; it belongs above both.
- *Add to `FS.GG.Governance.Routing`.* Rejected: Routing answers domain-per-path and depends on
  neither Gates nor Findings; folding selection in would pull both into Routing's surface and blur
  the "per-path routing" boundary every later row consumes.
- *Put it in Host/Cli.* Rejected: the model and selector are product-neutral pure values. The
  whole-route consumers (`fsgg route`/`fsgg ship`, the route/audit JSON emitter) live in Cli and will
  *reference* Route, exactly as they reference Routing/Findings/Gates.

## D2 — Boundary shape: a pure total function, no MVU

**Decision.** A single pure entry point `Route.select : GateRegistry -> RouteReport -> FindingReport
-> RouteResult`, over a `Model` of route types. No `Model`/`Msg`/`Effect`/`update`.

**Rationale.** The feature performs no I/O, senses no git, holds no multi-step state — it is a
deterministic join of already-typed, already-validated inputs (FR-008). Principle IV mandates the MVU
boundary only for stateful or I/O features and explicitly exempts "a single rule evaluation / pure
function." F015 `route`, F017 `findUnknownGovernedPaths`, and F018 `buildRegistry` all took this path
for the same reason; this row follows.

**Argument order.** `registry` then `report` then `findings` — the stable catalog projection first,
the change (routing report) second, the carried findings last. Matches the upstream calling order a
downstream `fsgg route` will use (build registry once, route a change, classify findings, select).

## D3 — Selection algorithm: a domain→gates index, then a per-path lookup

**Decision.** Build a `Map<DomainId, Gate list>` index over `registry.Gates` ONCE (group by
`Gate.Domain`, each bucket in `GateId` order). Then fold the `Routed` routings: for each
`Routed (d, glob, _)`, look up the `d` bucket and, for each gate in it, accumulate the
`SelectingPath {Path; MatchedGlob = glob}` under that gate's `GateId`. Dedup is by `GateId` key in the
accumulator (a `Map<GateId, Gate * SelectingPath list>` or equivalent); a gate reached by several
paths grows its selecting-path list. Finally sort gates by `GateId` ordinal and each gate's selecting
paths by normalized path ordinal.

**Rationale.** O(gates) to index + O(routedPaths × gatesPerDomain) to select — linear in the inputs,
no quadratic scan of the whole registry per path. Selection is by **declared id equality** between
`Gate.Domain` and the `Routed` `DomainId` (FR-010) — the index key IS `Gate.Domain`, so the join can
only match on the declared domain; the `GateId` string is never re-parsed. Grouping the index
buckets in `GateId` order makes the final result order fall out naturally.

**`mutable` disclosure.** If the accumulator fold reads cleaner as a `mutable` dictionary than as an
immutable `Map` fold, that is allowed (Principle III) and disclosed at the use site with a one-line
comment (e.g. `// mutable: single unaliased accumulator over the routed paths`). The default is the
immutable fold; the `mutable` form is taken only if it is demonstrably plainer.

## D4 — The route trace shape: `SelectedGate` embeds the F018 `Gate`

**Decision.** `SelectedGate = { Gate: Gate; SelectingPaths: SelectingPath list }` — the selected
gate embeds the whole F018 `Gate` verbatim and pairs it with its selecting paths. `SelectingPath =
{ Path: GovernedPath; MatchedGlob: GovernedPath }`.

**Rationale.** FR-004 requires the trace to name the `GateId`, the `Domain`, the selecting path(s),
the matching glob each won on, and the declared `Cost`. Embedding `Gate` supplies `Id`, `Domain`,
`Cost` (and the rest of the *Gate identities* metadata) WITHOUT re-deriving or re-declaring any of it
(FR-010, FR-012) — the route trace reuses F018's type, so a field added to `Gate` later flows through
for free. `SelectingPath` supplies the path + the rule (the F015 `matchedGlob`).

**The matching glob comes straight from F015.** `RoutingResult.Routed` carries
`(domain, matchedGlob, reason)` — `matchedGlob` IS the "matching rule" FR-004 wants. It is read
directly off the routing outcome; this feature re-routes nothing (FR-008). The `PrecedenceReason` is
deliberately NOT carried onto the trace in this MVP — FR-004 names path/domain/glob/cost, not the
precedence reason; it is available on the F015 report if a later row wants it.

## D5 — Cost-rollup value shape: a multiset of `Cost` tiers, not a summed scalar

**Decision.** `CostRollup = { Cheap: int; Medium: int; High: int; Exhaustive: int }` — the count of
**distinct** selected gates in each closed `Cost` tier. The identity (empty selection) is all-zero.

**Rationale.** F014's `Cost` is a *closed, ordered* class (`Cheap < Medium < High < Exhaustive`) with
**no declared numeric weights**. A summed scalar would require inventing weights (is `High` = 3?
= 10?) — magnitudes F014 never states. That is exactly the "invent semantics the schema does not
declare" move F018's research D5 refused when it declined to derive prerequisite edges from cost
tiers. A per-tier count: (a) preserves the declared vocabulary exactly; (b) counts each distinct gate
once (FR-006, dedup by `GateId` already done in selection); (c) is trivially deterministic and
byte-stable; (d) has an obvious zero identity. The spec's assumption explicitly floats "a multiset of
cost tiers vs. a summed scalar … settled at plan time, consistent with how F018 carries `Cost`" — the
multiset is the F018-consistent choice. Phase 11 (cost & cache) MAY layer a weighted total on top
once it *declares* weights; this row does not pre-empt that.

**Alternative rejected.** *Summed scalar with hardcoded tier weights.* Rejected: invents undeclared
magnitudes, couples the route to a weighting policy that belongs to Phase 11, and loses the per-tier
detail a "warn that a change pulls in expensive gates" preview actually wants.

## D6 — Findings carry-through: place the F017 report unchanged

**Decision.** `RouteResult.Findings` is the F017 `FindingReport` placed on the result UNCHANGED — no
re-sort, no re-derive, no re-classify, no filter (FR-005). The F017 report is already deterministic
(sorted by normalized path then finding-id token), so carrying it verbatim preserves determinism.

**Rationale.** The design's route.json carries *selected gates* AND *unmatched governed paths* in one
record; this feature is where the two meet. FR-005 is explicit that findings are carried, not
re-derived — F017 already made the classification decision F015 deferred, and re-touching it here
would duplicate (and risk diverging from) that logic. An empty `FindingReport` is carried as an empty
finding list — a success, never a fabricated "all clear" (edge case, US3 scenario 2).

## D7 — What is NOT consumed, and what is NOT produced

**Not consumed.** `report.Diagnostics` (the F015 routing diagnostics — `AmbiguousRoute` etc.) are not
read: selection acts on the *resolved* per-path outcome (`Routings`), exactly as F017 did. A `Routed`
path with an `AmbiguousRoute` diagnostic still selects its resolved domain's gates (edge case).

**Not produced (held by FR-011).** No base/effective severity; no profile/mode/maturity enforcement;
no evidence-freshness computation or cache-reuse decision (a gate's carried `FreshnessKey` is
propagated inside `Gate`, never evaluated); no gate execution/ordering; no ship verdict / blockers /
warnings / exit-code basis; no route/audit JSON, `.fsgg/gates.json`, or CLI command. Those are the
remaining Phase-2 rows and Phase 5/11 that *consume* this route trace.

## D8 — Testing strategy: real upstream values, end-to-end through the public surface

**Decision.** Tests drive `Route.select` over **real** in-memory inputs assembled by the genuine
upstream functions — `Gates.buildRegistry` for the `GateRegistry`, `Routing.route` for the
`RouteReport`, `Findings.findUnknownGovernedPaths` for the `FindingReport` — all from real
`TypedFacts` built via Config. No mocks, no hand-forged registry/report, no private helpers
(Principle V). FsCheck supplies the permutation-invariance and totality properties (D9).

**Rationale.** This row's whole value is the *join* of F015/F017/F018, so the most honest evidence
runs the actual F015→F017→F018→F019 chain over real fixtures — the exact values the future `fsgg
route` will pass. This also transitively re-exercises the upstream rows, catching any join-time
mismatch a mock would hide. **No synthetic evidence is anticipated**: every case (routed/unrouted
paths, shared gates, empty registry, findings present with no gates) is reachable from real upstream
outputs. Any unavoidable literal standing in for an un-derivable case would carry `Synthetic` in the
test name with a use-site disclosure and be listed in the PR.

## D9 — Determinism & permutation invariance

**Decision.** `SelectedGates` sorted by `GateId` ordinal; each `SelectingPaths` sorted by normalized
`Path` ordinal; `Findings` carried in F017 order; `CostRollup` is order-free (counts). Proven by an
FsCheck property: permuting the input candidate paths AND the registry's gate list yields a
byte-identical `RouteResult`, plus a twice-identical run over fixed inputs (SC-005).

**Rationale.** A non-deterministic route cannot back a byte-stable route.json snapshot. Ordinal sort
keys (not input order) are the single documented order (FR-007); the counts in `CostRollup` are
inherently permutation-free.

## D10 — Resolved Technical Context

| Unknown | Resolution |
|---|---|
| Language/Version | F# on .NET, `net10.0` (`Directory.Build.props`). |
| Primary dependencies | No new third-party package. ProjectReferences: Gates, Routing, Findings (Config transitive). |
| Storage | None — pure in-memory values. |
| Testing | `dotnet test` (Expecto + FsCheck via VSTest) over real upstream-assembled inputs (D8). |
| Target platform | Cross-platform .NET library; validated on Linux dev host. No git/filesystem touched. |
| Project type | Optional packable F# class library + one test project (same shape as Config/Routing/Findings/Gates). |
| Performance | Deterministic join: O(gates) index + O(routedPaths × gatesPerDomain) select + one `GateId` sort. |
| Constraints | Pure & total (FR-008/FR-009): no I/O, git, clock; never throws; empty route is a valid success. |
| Scale/Scope | One production project + one test project; public modules `Model` + `Route`; one surface baseline. |
