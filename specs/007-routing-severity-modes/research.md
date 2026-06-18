# Phase 0 — Research (F07 · 007-routing-severity-modes)

Engineering decisions for the light routing layer. Each resolves a design question raised by
the spec's "implementation/design details fixed in the plan and the `.fsi`" (spec Assumptions).
No NEEDS CLARIFICATION remained in the Technical Context; these record *why* the chosen shapes.

---

## D1 — `ChangeSet` is the generic type parameter `'change`, not a declared type

- **Decision**: Realise the spec's abstract `ChangeSet` as a **generic type parameter
  `'change`** on `Fence<'change>` / `stakesOf` / `route`, rather than declaring a concrete or
  abstract `ChangeSet` type in the kernel. The adapter supplies whatever value models "what
  changed" (a git diff, a set of Spec Kit phases, design tokens); the kernel routes over it
  opaquely.
- **Rationale**: FR-001 / FR-017 demand the change carry **no domain vocabulary** and that
  routing operate over it "without assuming any repository, file, or artifact shape". A generic
  `'change` is the *most* domain-neutral encoding and is the kernel's established house style
  (`Check<'fact>`, `Bridge<'fact>`, `EvidenceGraph<'id>`). It lets each adapter use its own
  strongly-typed change shape with fences that inspect it directly — no boxing/casting.
- **Alternatives considered**:
  - *A concrete `ChangeSet = { Labels: Set<string> }` (or a path/diff record).* Rejected: it
    bakes in a representation ("labels"/"paths") that is itself a mild domain assumption and
    forces every adapter through one shape — the opposite of FR-017.
  - *An opaque boxed carrier (`ChangeSet of obj`).* Rejected: fences would cast back out,
    which is un-idiomatic and unsafe, and defeats the type system.
- **Spec licence**: the spec's Assumptions explicitly state "the exact representation of the
  abstract `ChangeSet` … [is an] implementation/design detail fixed in the plan and the `.fsi`".
  The data-model and `Route.fsi` document the mapping `ChangeSet ≜ 'change`.

## D2 — `stakesOf` is order-independent (forbid trumps permit), NOT first-match positional

- **Decision**: `stakesOf` returns `Fenced` if **any** fence trips, carrying the tripped fence
  names **de-duplicated, ordinal-sorted, and `"; "`-joined**; `Routine` if none trips. The
  carried value is a function of the *set* of tripped fences, so it is **identical under any
  permutation** of the fence list.
- **Rationale**: FR-005 / SC-003 require combination to be deterministic by precedence and
  **never positional** — this closes hazard 5 / reinforces decision #4. The design doc's sketch
  (`docs/governance-design/routing-and-modes.md`) used `List.tryFind … |> function Some f ->
  Fenced f.Name`, i.e. **first-match — positional**: reordering the fences could change which
  name is carried. The spec deliberately strengthens this, so the implementation must diverge
  from the doc sketch. A set-derived name is the minimal change that makes the result
  order-independent while still naming the tripped boundary.
- **Alternatives considered**:
  - *Carry the first/last matching fence's name (`tryFind`).* Rejected: positional — violates
    FR-005 / SC-003 directly (the hazard the feature exists to close).
  - *Carry only the ordinal-minimum tripped name.* Order-independent and valid, but **loses**
    the other tripped boundaries; a change that trips both "merge-boundary" and "security-
    surface" should name both. Rejected in favour of the full sorted set.
  - *A new combination scheme.* Rejected: F02 `Verdict.all`/`any` already establish the
    repo's reason-combination convention (split on `"; "`, dedup, ordinal-sort, re-join).
    Reusing it keeps the kernel consistent (Principle III) and is provably set-valued.

## D3 — A `Route` requirement IS a F06 `ContractEntry` (reuse, don't re-declare)

- **Decision**: Represent each applicable requirement in a `Route` as a F06 `ContractEntry`
  (`{ Id; Severity; Spec; Statement }`), produced by `Contract.ofRules`. `Route.Advisory` and
  `Route.Blocking` are `ContractEntry list`.
- **Rationale**: FR-012 requires a blocking gate to name **the rule, the fence, and the
  rendered check**, "reusing F03's check rendering so the gated requirement is shown as the
  exact check, with no drift". `Contract.ofRules` already folds a `CheckRule<'fact>` into an
  entry whose `Statement` *is* `Check.render rule.Check` — drift-proof by construction (F06
  R-C2). Reusing it gives FR-012 for free, keeps `Route` non-generic and domain-neutral (it
  drops `'fact`, exactly as `ContractEntry` does), and avoids a parallel "gate" type that could
  drift from the contract. The fence is named separately in `Route.Stakes` / the reason.
- **Alternatives considered**: a bespoke `RouteGate` record duplicating id/severity/statement.
  Rejected: redundant with `ContractEntry`, and a second rendered-statement source reintroduces
  the very drift F06 eliminated.

## D4 — The advisory/blocking partition: `Blocking ∧ Fenced ∧ Gate`

- **Decision**: In `route`, a requirement is a **blocking gate iff** `entry.Severity =
  Blocking` **and** `stakes = Fenced _` **and** `mode = Gate`; **every** other requirement
  (advisory severity, or routine change, or non-`Gate` mode) is `Advisory`.
- **Rationale**: This is the minimal composition satisfying every acceptance scenario:
  light-by-default (`Routine` ⇒ no blocking gates in any mode — FR-006, US1/US2); fenced-gate
  enforcement (FR-008, US2/US3); run mode as the enforcement dial with stakes constant across
  modes (FR-009, US3). It is a pure predicate over three independent axes — no positional or
  order-sensitive logic.
- **HumanOnly / escalation**: the F04 `Severity` doc notes "`HumanOnly` escalates regardless".
  In F07 that is **not** modelled as a routing partition — escalation is an *effect*
  (`RuleOutcome.Escalated`, emitted by `toRule` and acted on by F08). F07 partitions only by
  the declared `Severity`; whether/where an escalation blocks is the F08 edge's decision. This
  keeps F07 pure and matches the spec (escalation/disclosure are "routing inputs/outputs … their
  enforcement and logging are the F08 edge's job").
- **Alternatives considered**: folding `CheckTier`/`HumanOnly` into the partition here.
  Rejected: it would pull escalation policy into the pure layer and exceed FR-008's scope.

## D5 — The caller supplies the *applicable* rules; `route` does not select them

- **Decision**: `route` receives `rules: CheckRule<'fact> list` already filtered to those that
  apply to the change; it partitions them but does not itself decide applicability.
- **Rationale**: deciding *which* rules govern *which* change (e.g. "rules whose `Check.reads`
  intersect the changed artifacts" — design doc step 3) is **domain-specific** and needs the
  adapter's notion of change; doing it in the kernel would breach domain-neutrality (FR-017).
  Receiving pre-applicable rules keeps the kernel neutral and makes the blocking subset
  **short** — bounded by the applicable rules, not the catalog (FR-013, US5, SC-007). The
  `Route.fsi` documents this contract explicitly.
- **Alternatives considered**: passing the whole catalog plus a `'change -> CheckRule -> bool`
  selector. Rejected as unnecessary surface — the adapter can filter before calling, and the
  selector would still be adapter-supplied domain logic.

## D6 — Zero new dependency; reuse the in-assembly kernel only

- **Decision**: `Route` adds **no** `PackageReference`. It uses F06 `Contract`/`ContractEntry`,
  F04 `CheckRule`/`Severity`, F01 `RuleId`, and the standard library (`List.choose`/`partition`,
  `List.sort`/`List.distinct` or `Set`, `String.concat`).
- **Rationale**: FR-017 / SC-011 require zero heavy dependencies; the V12 reflective test
  asserts the kernel references only the BCL + FSharp.Core. Everything routing needs already
  lives in the assembly. No JSON is emitted here (D7), so even `System.Text.Json` is untouched.
- **Alternatives considered**: none needed.

## D7 — No `Json.ofRoute`; `renderRoute` is text-only

- **Decision**: F07 ships **only** the text `renderRoute`. A JSON serialization of `Route` is
  **deferred** to F08/F12.
- **Rationale**: the spec requires `renderRoute` (FR-014) but no JSON form; FR-016 places
  persisting/printing at the F08 edge. This mirrors F06, which produced its JSON but left
  *emitting* it to F08/F12. Keeping F07's surface to the three folds (`stakesOf`, `route`,
  `renderRoute`) keeps it minimal and avoids speculative API.
- **Alternatives considered**: adding `Json.ofRoute`/`toRoute` now. Rejected: YAGNI until F08/
  F12 define how routes are emitted; adding it later is purely additive.

---

### Build order (consequence of D3/D6)
`Route.*` compiles **after** `Contract.*` (it references `ContractEntry`/`Contract.ofRules`,
and through them F04 `CheckRule`/`Severity` and F03 `Check.render`). `Route` and `Json` are
independent, so `Route.*` may sit before or after `Json.*`; appending it after `Json.*` keeps
the `.fsproj` edit append-only. Zero new dependency (D6).
