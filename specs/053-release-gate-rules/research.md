# Phase 0 Research: Pure Release-Gate Readiness Rules Core (F053)

This row introduces no NEEDS CLARIFICATION from the Technical Context — language, frameworks, target, and the
"no new dependency / no schema bump" constraints are fixed by the repo and the spec. The research below
resolves the **design decisions** the pure core forces: how to reuse the frozen enforcement/verdict machinery
without editing or duplicating it, how to model the facts so the fail-safe and edge cases fall out, and where
the library sits in the graph.

Each decision is stated as **Decision / Rationale / Alternatives considered**.

---

## D1 — How the rollup reuses F024 without calling `Ship.rollup`

**Decision.** The rollup reuses F023 `deriveEffectiveSeverity` **verbatim** for every per-finding severity
decision, reuses the F024 `Ship.Model.Verdict` and `Ship.Model.ExitCodeBasis` **result types verbatim**, and
**re-applies the F024 partition rule** (Blockers = effective `Blocking`; Warnings = base `Blocking` relaxed to
effective `Advisory`; Passing = base `Advisory`; `Verdict = Fail` iff Blockers non-empty; `ExitCodeBasis =
Blocked` iff `Fail`) over the release findings. It does **not** call `Ship.rollup`.

**Rationale.** `Ship.rollup : RouteResult -> RunMode -> Profile -> ShipDecision` takes a `RouteResult` whose
content is F018 gates (`SelectedGate`, identified by `GateId`) and F017 unknown-path findings
(`FindingReport`, identified by `FindingId * GovernedPath`). A release rule is **neither**: it has no
`GateId`, no `FreshnessKey`, no domain/cost, and it is not an unknown-governed-path finding. `ShipDecision`'s
partition lists are `EnforcedItem`s keyed by `EnforcedItemId = GateItem of GateId | FindingItem of FindingId *
GovernedPath` — there is no constructor that represents a release rule. To literally call `Ship.rollup` we
would have to **fabricate** `GateId`s (or `FindingId`/`Path` pairs) for release rules, which would *redefine*
the release primitives FR-009 tells us to reuse, couple release rules to F018/F017 identity, and smuggle
release rules through a sort/mapping (`Ship.fs`'s unexported gate→`EnforcementInput` rule and composite sort
key) that was written for gates, not release rules. That is the dishonest duplication FR-009 forbids.

The honest reuse is: call the *actual frozen decision* — `deriveEffectiveSeverity` — unchanged for each
finding (FR-003), reuse the *actual result vocabulary* — `Verdict`/`ExitCodeBasis` — unchanged (FR-004), and
re-state the three-way classification predicate, which is itself a total, one-line function of each finding's
(base severity, effective severity) pair. F024 applies that exact predicate to gates and findings; this row
applies the **same** predicate to the release-finding domain. Reusing a rule across a new item domain is
composition, not a fork: nothing in `Enforcement` or `Ship` is edited, relaxed, or copy-pasted; the severity
algebra and the result types are imported and called as-is. FR-004's wording — "reusing the F024 partition
rule unchanged" — is satisfied by re-applying the rule and reusing its result types; FR-009's "MUST NOT …
duplicate … the F024 Ship verdict rollup" is satisfied because `Ship.rollup`'s gate/finding-specific input
mapping is genuinely inapplicable and is **not** reproduced.

**Alternatives considered.**
- *Synthesize a `RouteResult` of fake gates and call `Ship.rollup`.* Rejected: fabricates `GateId`s,
  redefines release primitives (FR-009), and misroutes release rules through gate-only logic. Dishonest.
- *Add a public partition helper to `Ship` and call it from here.* Rejected: editing the frozen F024 core is
  forbidden (FR-009) and unnecessary — the partition predicate is a trivial total function of the
  already-public `EnforcementDecision`.
- *Define release-specific `Verdict`/`ExitCodeBasis` types.* Rejected: FR-004/FR-009 say reuse the existing
  result vocabulary; redefining it would diverge the release verdict from the ship verdict for no benefit.

---

## D2 — Run mode and profile are fixed to `Release`

**Decision.** The rollup derives effective severity under `RunMode.Release` and `Profile.Release`, fixed.
There is no run-mode/profile parameter on `rollup`/`evaluateRelease` in this row.

**Rationale.** FR-003 is explicit: effective severity is derived "under the `Release` run mode and `Release`
profile — no new severity scheme, mode, or profile is introduced." This *is* the release gate; it runs at the
most protective boundary by definition. The lever a release author uses to relax a rule is the rule's
**declared maturity / base severity** (FR-010, D6), *not* a looser profile — relaxing the profile would be a
different, project-wide dial that FR-010 deliberately does not expose here. Fixing both keeps the core's
surface minimal and matches the spec's single-mode framing (`Ship.rollup` takes one mode + one profile for
the whole change; the release gate's "whole change" mode/profile is `Release`/`Release`).

**Alternatives considered.**
- *Parameterize `rollup` by mode/profile like `Ship.rollup`.* Rejected for this row: FR-003 nails both to
  `Release`, and exposing a profile knob would contradict FR-010's "relaxation is via declared maturity only."
  A future row that genuinely needs a configurable release profile can add the parameter additively.

---

## D3 — Modeling `ReleaseFacts` so the fail-safe and edge cases fall out

**Decision.** A fact is a closed tri-state `FactState = Met | Unmet | Unrecoverable`. `ReleaseFacts` is a thin
record wrapping a `Map<ReleaseRuleKind, FactState>` (`{ States: Map<ReleaseRuleKind, FactState> }`). A rule
reads the fact for **its kind**; a kind absent from the map resolves to `Unrecoverable` (via the exposed
`factFor` lookup). Evaluation maps `Met -> Satisfied`, and both `Unmet -> Violated` and `Unrecoverable ->
Violated` (with distinct reasons).

**Rationale.** This makes every spec edge case a direct consequence of the model, with no special-casing:
- **Absent / unrecoverable fact ⇒ violated** (FR-005, SC-005): a missing map key resolves to `Unrecoverable`
  ⇒ `Violated`. `Unmet` and `Unrecoverable` are kept distinct so the reason text can say "expected … was not
  met" vs "no recoverable evidence for …" — distinguishing "no evidence" from "satisfied," never conflating
  them.
- **Extra / unrecognized facts ignored** (edge case): evaluation iterates **rules**, not facts, so a fact for
  a kind no rule declares is never read and never invents a finding.
- **Duplicate rules of the same kind** (edge case): each rule independently looks up its kind's `FactState`
  and emits its own finding — same truth, independent reporting (FR-001).
- **Additive extension** (Assumptions): a new rule kind is a new map key and a new `ReleaseRuleKind` case; no
  existing field changes shape, and an unknown kind already fails safe to `Unrecoverable` ⇒ `Violated`.

The tri-state is the right altitude for a *pure* core: the richer per-kind fact shapes (the actual version
string, the metadata field list, the resolved pin set) are what the **sensing** row will inspect to *produce*
a `FactState`; the core only needs "is the governing fact met, unmet, or unrecoverable?"

**Alternatives considered.**
- *A flat record with one `FactState` field per kind.* Rejected: adding a kind would be a breaking record
  change, and an omitted field could not express "absent" (every field is always present). The `Map` makes
  absence first-class and extension additive.
- *`option<bool>` per fact (`None` = absent).* Rejected: collapses `Unmet` and `Unrecoverable` into the same
  `false`/`None` ambiguity the fail-safe wants to keep distinct in the reason text (SC-005).
- *Facts keyed by `(kind, SurfaceId)` for per-identity facts.* Rejected for this row as over-design: the
  roadmap's six families are project-wide, and per-identity facts are an additive future refinement. The rule
  still carries its `SurfaceId` for the finding/reason; only the *fact lookup* is per-kind here.

---

## D4 — One finding per rule, deterministic order, no-hide

**Decision.** `evaluate` emits exactly one `ReleaseFinding` per declared rule and returns the list sorted by a
stable composite key: `releaseRuleKindOrdinal` (the closed rule-kind order) then the rule's `SurfaceId`
ordinal string. `rollup` preserves that order within each partition list (a stable partition).

**Rationale.** FR-001 (one finding per rule), FR-006 (drop nothing — satisfied and relaxed rules stay
visible), and FR-007 (byte-identical output) together require a total, order-independent projection: identical
input ⇒ identical output regardless of the input list's arrival order. A composite ordinal key over a closed
kind order + the declared id string is the same determinism device F024/F019/F017 already use ("sorted by a
stable composite per-item key"). The output rule-kind multiset therefore equals the declared rule-kind
multiset (SC-001, no drops, no fabrications), and re-evaluation is byte-identical (SC-003).

**Alternatives considered.**
- *Preserve input order.* Rejected: FR-007/SC-003 demand order-independence; input order is not a stable
  contract.
- *Deduplicate same-kind rules.* Rejected: the edge case requires duplicates to each yield their own finding
  (per declared rule, not per kind).

---

## D5 — Library placement, name, and references

**Decision.** A new pure library `FS.GG.Governance.ReleaseRules` with modules `Model` (types) and `Release`
(functions), `ProjectReference`ing exactly `FS.GG.Governance.Config` (F014), `FS.GG.Governance.Enforcement`
(F023), and `FS.GG.Governance.Ship` (F024). Compile order `Model.fsi → Model.fs → Release.fsi → Release.fs`.
`IsPackable=true` with `PackageId=FS.GG.Governance.ReleaseRules`, matching the other optional packable cores.

**Rationale.** The repo idiom is one small focused library per pure core (≈40 of them), and the constitution
says "heavier capabilities layer on top, not into the core." The three references are exactly the primitives
the core reuses: F014 for the maturity/surface/environment vocabulary, F023 for the severity algebra, F024 for
the `Verdict`/`ExitCodeBasis` result types. It deliberately omits `Route`/`Gates`/`Findings` — a release rule
is not an F018 gate or an F017 finding (D1), so those would be dead weight and would tempt the rejected
"synthesize a `RouteResult`" design. The two-module split mirrors every other core (`Ship` = `Model` +
`Ship`, `Enforcement` = single module, `Findings` = `Model` + `Findings`). No new third-party dependency is
introduced (FR-008); FSharp.Core + the three ProjectReferences only.

**Alternatives considered.**
- *Fold the core into `Ship` or `Enforcement`.* Rejected: editing a frozen core is forbidden (FR-009) and
  would widen those cores' surfaces; the release vocabulary is a distinct concern.
- *Reference `Route` for the `RouteResult`.* Rejected: not consumed (D1); adds an unused dependency.

---

## D6 — A rule declares both base severity and maturity (the F023 `EnforcementInput` shape)

**Decision.** `ReleaseRule` carries an explicit `BaseSeverity: Severity` **and** `Maturity: Maturity` (plus
`Kind` and `Surface: SurfaceId`). The rollup builds `EnforcementInput { BaseSeverity = rule.BaseSeverity;
Maturity = rule.Maturity; Mode = Release; Profile = Release }` and passes it straight to
`deriveEffectiveSeverity` — no re-derivation of any unexported mapping.

**Rationale.** F023 `EnforcementInput` takes `BaseSeverity` and `Maturity` as **independent** inputs, so
declaring both on the rule is the most faithful, least-duplicative reuse: the rule *is* essentially the
`(BaseSeverity, Maturity)` pair plus identity, fed verbatim into the frozen derivation. F024's gate path
derives a gate's base severity *from* its maturity via a helper that lives **unexported** in `Ship.fs`;
re-deriving that here would duplicate frozen logic (FR-009) and is unnecessary when the rule can declare base
severity directly. This also makes FR-010 exact: relaxing a rule to advisory is changing its declared
`Maturity` (e.g. `BlockOnRelease → Warn`) and/or `BaseSeverity`, which flows through `deriveEffectiveSeverity`
to a `Advisory` effective severity ⇒ the violation becomes a Warning — its satisfied/violated **truth** and
its **visibility** are untouched (SC-006). `BlockOnRelease` is the natural blocking maturity for a release
rule (the F014 primitive FR-009 says to reuse).

**Alternatives considered.**
- *Derive base severity from maturity (the F024 gate convention).* Rejected: that mapping is unexported frozen
  logic; reproducing it duplicates a frozen core (FR-009). Declaring base severity directly matches the F023
  input shape with zero re-derivation.
- *Carry only maturity and assume base `Blocking`.* Rejected: loses the F023-modeled distinction between a
  base-advisory and a base-blocking rule and would force a hidden assumption.

---

## D7 — Reasons are self-explaining and product-neutral

**Decision.** Each finding's `Reason` is a deterministic, product-neutral string naming the rule kind (via
`releaseRuleKindToken`), the governed `SurfaceId`, and the outcome basis ("met", "not met", "no recoverable
evidence"). No host paths, no timestamps, no rendering vocabulary; the only product nouns are the declared
`SurfaceId` and the closed kind token.

**Rationale.** FR-001 requires a self-explaining reason; the constitution's genericity rule and the existing
cores' "no raw YAML, host paths, timestamps, or product vocabulary beyond declared ids" discipline require the
reason to be product-neutral and deterministic so output stays byte-identical (FR-007) and the operating rule
(no assumed rendering identity) holds. The kind token reuses the same stable-token idiom as
`findingIdToken`/`diagnosticIdToken`.

**Alternatives considered.**
- *Free-form messages with file paths.* Rejected: breaks determinism and the genericity rule.

---

## Summary of decisions

| # | Decision |
|---|----------|
| D1 | Reuse F023 `deriveEffectiveSeverity` + F024 `Verdict`/`ExitCodeBasis` types verbatim and **re-apply** the F024 partition rule; do **not** call `Ship.rollup` (release rules aren't `RouteResult` gates/findings). |
| D2 | Fix `RunMode.Release` + `Profile.Release`; relaxation is via declared maturity only (FR-010). |
| D3 | `FactState = Met \| Unmet \| Unrecoverable`; `ReleaseFacts` wraps `Map<ReleaseRuleKind, FactState>`; absent key ⇒ `Unrecoverable` ⇒ `Violated` (fail-safe); facts iterate by rule. |
| D4 | One finding per rule; stable sort by (kind ordinal, surface id); stable partition; multiset-preserving. |
| D5 | New pure lib `FS.GG.Governance.ReleaseRules` (`Model` + `Release`); refs Config + Enforcement + Ship only. |
| D6 | `ReleaseRule` declares both `BaseSeverity` and `Maturity`, fed verbatim into `EnforcementInput`. |
| D7 | Deterministic, product-neutral reasons naming the kind token + governed `SurfaceId` + outcome basis. |
