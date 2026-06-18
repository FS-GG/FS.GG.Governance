# Phase 1 — Data Model (F08 · 008-effects-interpreter)

The MVU types, the loop lifecycle, the policy/isolation/cache rules, the `renderRoute`/emit
shape, and the invariants for the effects shell. The authoritative shape lives in
[`contracts/Loop.fsi`](./contracts/Loop.fsi) and
[`contracts/Interpreter.fsi`](./contracts/Interpreter.fsi); this document explains them — the
`.fsi` are the source of truth.

## 1. New public types

### Pure side — `Loop` (`Loop.fsi`)

| Type | Kind | Role |
|---|---|---|
| `ArtifactContent` | record `{ Ref; Content }` | Untrusted artifact content; carried only on the DATA channel (decision #3). |
| `JudgeVerdict` | record `{ Verdict; Confidence }` | One stochastic sample (F02 `Verdict` + `[0,1]` confidence). |
| `ReviewTask` | record `{ Key; Instruction; Data }` | Dispatched review with **instruction isolated from data** (FR-010). |
| `ReviewDispatch` | record `{ Task; Samples }` | A cache-miss dispatch + the sample budget (`samplesFor`). |
| `AcceptancePolicy` | DU `SingleSample \| Agreement n \| Confidence t` | The configurable freeze policy (decision #2, FR-009). |
| `Acceptance` | DU `Freeze v \| StayPending` | Output of `accept` — freeze, or stay `Uncertain` (FR-009). |
| `Disclosure` | record `{ Rule; Justification }` | Observable bypass log (FR-013). |
| `Failure` | DU (3 cases) | Reified safe-failure record (FR-012); absent/bad input ≠ defect. |
| `Output` | DU `ExplanationJson \| ContractJson \| RouteText` | F06 edge outputs emitted (FR-015). |
| `Effect` | DU (5 cases) | The I/O the loop requests as data — the `Cmd<Msg>` (FR-003). |
| `Msg<'fact>` | DU (5 cases) | Every effect result + transitions (FR-001/FR-004). |
| `Phase` | DU `Sensing \| Planning \| Quiescent` | The loop stage. |
| `Model<'fact>` | record (7 fields) | The durable loop state (FR-001). |
| `LoopConfig<'change,'fact>` | record (7 fields) | The caller-supplied pure wiring (FR-017). |

### Edge side — `Interpreter` (`Interpreter.fsi`)

| Type | Kind | Role |
|---|---|---|
| `ArtifactReader` | `ArtifactRef -> Result<string,string>` | Injected sensing port (FR-005). |
| `Judge` | `ReviewTask -> Result<JudgeVerdict,string>` | Injected AI-judge port, one sample per call (FR-007/FR-017). |
| `ReviewStore` | record `{ Load; Save }` | Injected store, keyed by F04 cache key (FR-007/FR-008). |
| `OutputSink` | `Output -> unit` | Injected sink for F06 outputs (FR-015). |
| `Ports` | record `{ Read; Judge; Store; Sink }` | The injected edge bundle (FR-017). |

### Reused types (no new declaration)
- F07: `Route`, `Route.route`, `Fence<'change>`, `RunMode` — the route the loop acts on.
- F06: `Json.ofExplanation`/`ofContract`, `Contract.ofRules`, `Freshness.decide`, `Check.render`.
- F04: `CheckRule<'fact>`, `Bridge<'fact>`, `CheckRule.cacheKey`, `CheckRule.toRule`,
  `RuleOutcome` (`NeedsReview`/`Reviewed`/`Decided`), `ReviewRequest`, `RecordedReview`,
  `JudgeId`, `Severity`.
- F03: `Check`, `Check.reads`, `Check.explain`, `ArtifactRef`, `Outcome`.
- F02/F01: `Verdict`, `FactSet<'fact>`, `FactAssertion`, `FactId`, `RuleId`,
  `FixedPoint.evaluate`.

## 2. Functions (pure unless noted)

| Function | Signature | Purity | Role |
|---|---|---|---|
| `Loop.defaultPolicy` | `AcceptancePolicy` | pure value | The documented default (`SingleSample`). |
| `Loop.samplesFor` | `AcceptancePolicy -> int` | pure | Sample budget for a dispatch. |
| `Loop.accept` | `AcceptancePolicy -> JudgeVerdict list -> Acceptance` | pure & total | Freeze vs stay-pending (FR-009). |
| `Loop.init` | `LoopConfig<'change,'fact> -> 'change -> Model<'fact> * Effect list` | pure | Initial model + sense effects (FR-001). |
| `Loop.update` | `LoopConfig<'change,'fact> -> Msg<'fact> -> Model<'fact> -> Model<'fact> * Effect list` | **pure & total** | The MVU transition (FR-002). |
| `Interpreter.step` | `Ports -> Effect -> Msg<'fact> list` | **impure (edge)** | Execute one effect → result Msg(s) (FR-004). |
| `Interpreter.run` | `Ports -> LoopConfig<'change,'fact> -> 'change -> Model<'fact>` | **impure (edge)** | Drive init→update* to quiescence (FR-016). |

## 3. The loop lifecycle (FR-005/006/007, the `sense → plan → act` cycle)

```text
init config change
  │  Route = Route.route config.Fences config.Rules config.Mode change   (FR-011, computed once)
  │  effects = [ ReadArtifact r  for each distinct r ∈ ⋃ Check.reads config.Rules ]   (SENSE, FR-005)
  ▼  Phase = Sensing  (or Planning if nothing to read)
run: while update emits effects, step each via ports, feed result Msgs back
  │
  ├─ Sensed (r, Ok content)  → assert SenseArtifact r content into Facts (dedup by Identify)
  │                            when all reads done → PLAN
  ├─ Sensed (r, Error e)     → Failures += ArtifactUnavailable (r,e)   (FR-012) ; still PLAN when done
  │
  PLAN (pure, inside update):  rules' = config.Rules |> List.map (CheckRule.toRule config.Bridge)
  │     result = FixedPoint.evaluate config.Identify rules' Facts        (FR-006, no new logic)
  │     needs  = result.Facts |> choose (Bridge.Project >> NeedsReview keys) \ (Pending ∪ recorded)
  │     effects = [ LoadReview key  for key ∈ needs ]                    (cache lookup, FR-008)
  │
  ├─ Loaded (key, Ok (Some rr)) → assert RecordedReview fact (HIT, FR-008) ; re-PLAN ; NO dispatch
  ├─ Loaded (key, Ok None)      → emit DispatchReview { Task = isolate(key); Samples = samplesFor policy }
  ├─ Loaded (key, Error e)      → Failures += ReviewStoreUnavailable (key,e)
  │
  ├─ Reviewed (key, Ok samples) → accept policy samples:
  │      Freeze v   → emit RecordVerdict rr ; assert rr fact ; Pending −= key ; re-PLAN   (FR-007/009)
  │      StayPending→ record nothing ; conclusion stays Uncertain ; Pending −= key        (FR-009/SC-004)
  ├─ Reviewed (key, Error e)    → Failures += ReviewDispatchFailed (key,e) ; review stays pending (FR-012)
  │
  ├─ Recorded (key, Ok ())      → no-op (fact already asserted ; IDEMPOTENT, FR-014)
  ├─ Recorded (key, Error e)    → Failures += ReviewStoreUnavailable (key,e)  (in-memory verdict still used)
  │
  ├─ Disclosed d                → Disclosures += d   (host/caller-supplied; logged, NEVER flips a verdict, FR-013)
  │
  ▼  QUIESCENCE (no pending sensing, no Pending, no new NeedsReview)
     Phase = Quiescent ; emit EmitOutput [ ExplanationJson … ; ContractJson … ; RouteText … ] ONCE (FR-015)
```

`isolate(key)` builds the `ReviewTask`: `Instruction` = the rule's `Question`; `Data` = the
`ArtifactContent` of the artifacts the rule reads (from `Facts`). The two are **separate fields**
the loop never concatenates (FR-010, decision #3).

## 4. Precedence & invariant rules

### Acceptance policy (FR-009, SC-004)
- **R-A1 (single)** `accept SingleSample [s] = Freeze s.Verdict`; `accept SingleSample [] =
  StayPending`; `SingleSample` with many takes the first (documented).
- **R-A2 (agreement)** `accept (Agreement n) samples = Freeze v` iff some verdict `v` appears in
  `≥ n` samples; else `StayPending`.
- **R-A3 (confidence)** `accept (Confidence t) samples = Freeze v` iff all (non-empty) samples
  agree on `v` and `mean(confidence) ≥ t`; else `StayPending`.
- **R-A4 (never launder noise)** a below-policy result is **never** frozen and **never** cached;
  the conclusion stays `Uncertain`/pending (FR-009) → the next run re-dispatches.
- **R-A5 (total)** `accept` is total for every policy and sample list, including `[]`.

### Instruction/data isolation (FR-010, SC-005, decision #3)
- **R-I1** `ReviewTask.Instruction` is **only** the rule's `Question`; `ReviewTask.Data` is
  **only** `ArtifactContent`. The loop never merges them.
- **R-I2** For two changes with identical rules/keys, one honest and one with injection text in
  its artifact content, the produced `Instruction` is **byte-for-byte identical** — only `Data`
  differs (SC-005). The task the judge is asked is unaffected by the artifact.

### Freeze-then-cache (FR-007/008, SC-003, decision #1 reuse)
- **R-C1** A frozen verdict is `RecordedReview { Rule; Key; Verdict }` with
  `Key = CheckRule.cacheKey Bridge.Judge (Check.hash rule.Check) artifactHashes rule.Question`
  — the **same** key `toRule` puts in `NeedsReview.Key` (research D6).
- **R-C2 (short-circuit)** with a matching `RecordedReview` fact present, `toRule` emits `Decided`
  → `update` produces **no** `DispatchReview` (cache HIT, FR-008); a re-run dispatches **zero**
  reviews (SC-003).
- **R-C3 (stale ⇒ fresh)** changing any cache-key ingredient (judge id/version, check hash,
  artifact content hash, or prompt) yields a different key, so no `RecordedReview` matches and a
  fresh dispatch is emitted (SC-003) — inherited from F04, no new logic.

### Gate enforcement (FR-011, SC-008)
- **R-G1** `Model.Route` is computed at `init` from the supplied **base** fences/rules/mode/change
  and is stable; blocking gates partition `Blocking` only when `Mode = Gate ∧ stakes = Fenced ∧
  Severity = Blocking` (F07 `route`). Enforcement in F08 is **exactly this computed/exposed
  `Route` value** — the loop emits **no separate halting effect** and does not gate quiescence or
  emit on a non-empty `Blocking`; acting on `Blocking` (refusing to land) is the host/F12's job
  (spec FR-011).
- **R-G2 (recompute from base)** because the route is a pure function of the base inputs, a model
  developed conceptually in `Sandbox`/`Inner` cannot carry a pre-cleared gate — running at `Gate`
  recomputes it afresh (SC-008).

### Safe failure & observability (FR-012, SC-006, Principle VI)
- **R-F1** Every effect failure (`Error` or thrown) is reified as a `Failure` + a failure `Msg`;
  `step`/`run` never throw (SC-006).
- **R-F2** `Failure` cases name **absent/bad input** (`ArtifactUnavailable`,
  `ReviewDispatchFailed`, `ReviewStoreUnavailable`); a **tool defect** would surface as a test
  failure, never a `Failure` value (Principle VI — defect ≠ bad input).
- **R-F3** A failed read makes the affected conclusion `Uncertain`/`Failed` (never a silent pass);
  a failed dispatch leaves the review pending; the loop still reaches a well-formed `Model`.

### Idempotency & order-independence (FR-014, SC-007)
- **R-D1 (idempotent)** re-applying the same result `Msg` records no duplicate verdict and no
  duplicate fact — facts dedup by `FactId` (via `Identify`); `Pending`/recorded membership is
  checked before emitting/recording.
- **R-D2 (order-independent)** the final `Model` is identical across permutations of the
  completion order of independent effects — the kernel's least-fixed-point is order-independent,
  and `Failures`/`Disclosures` are stored in a deterministic order so byte-equality holds (SC-007).

## 5. F06 emit shape (FR-015)

At quiescence `update` emits, once, the `EmitOutput` effects:
- `ExplanationJson (Json.ofExplanation (Check.explain Facts <planned check>))` — the proof tree.
- `ContractJson (Json.ofContract (Contract.ofRules config.Rules))` — the published contract.
- `RouteText (Route.renderRoute Model.Route)` — the rendered route.

`Interpreter.step` hands each to `Ports.Sink`. No new serializer is added; emission reuses F06
(research D7). (A `Json.ofRoute` remains deferred to F12.)

**Freshness emission is deferred (spec FR-015).** `Freshness.decide` is reused as an F06 *value*
where the kernel needs it, but this feature emits **no** freshness report — there is deliberately
no `FreshnessJson` `Output` case. The three emitted outputs are exactly `ExplanationJson` (F06),
`ContractJson` (F06), and `RouteText` (F07). Freshness-report emission lands with the F12 CLI.

## 6. Edge cases (from spec → behaviour)

| Edge case | Behaviour |
|---|---|
| Nothing to do | `init` over a change with no reads/reviews → quiescence, empty effect list, well-formed empty-derivation `Model`. |
| Cache hit, no dispatch | a matching `RecordedReview` suppresses `DispatchReview` entirely (R-C2). |
| Stale cache, fresh dispatch | any cache-key ingredient change → different key → fresh dispatch (R-C3). |
| Below-confidence verdict | `StayPending` — not frozen, not cached; conclusion stays `Uncertain` (R-A4). |
| Injection-laden artifact | content carried only on `Data`; `Instruction` unchanged (R-I1/R-I2). |
| Failed read / dispatch / store | each reified as a `Msg`/`Failure`; never throws (R-F1/R-F3). |
| Gate recomputes from base | enforcement recomputed at `Gate` from base inputs (R-G2). |
| Disclosure never flips a verdict | host/caller-supplied `Disclosed`; logged in `Disclosures`; verdict untouched (FR-013). |
| Re-entrant / duplicate Msg | idempotent — no double record/count (R-D1). |
| Effect ordering | final `Model` independent of completion order (R-D2). |

## 7. Dependencies & ordering
New project `FS.GG.Governance.Host` with a single `ProjectReference` → `FS.GG.Governance.Kernel`
and **zero `PackageReference`** (research D1/D2/D6/D7). Compile order: `Loop.fsi`/`Loop.fs` then
`Interpreter.fsi`/`Interpreter.fs` (`Interpreter` references `Loop`'s `Effect`/`Msg`/`Model`/
`LoopConfig`/`Output` and the kernel). The edge uses BCL `System.IO` (sensing/store in tests) and
the kernel's `System.Text.Json`-backed `Json.*` (F06 emit). A new surface baseline
`surface/FS.GG.Governance.Host.surface.txt` and a Host-side drift/hygiene test (V13/V14).
