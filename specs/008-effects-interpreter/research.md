# Phase 0 — Research (F08 · 008-effects-interpreter)

Engineering decisions for the effects shell — the Elmish/MVU boundary feature. Each resolves a
design question the spec deferred to "the plan and the curated `.fsi`" (spec Assumptions). No
NEEDS CLARIFICATION remained in the Technical Context; these record *why* the chosen shapes.

---

## D1 — A new `FS.GG.Governance.Host` project, separate from the kernel

- **Decision**: Ship F08 as a **new library project** `src/FS.GG.Governance.Host/` (namespace
  `FS.GG.Governance.Host`) with a single `ProjectReference` on `FS.GG.Governance.Kernel`. The
  kernel gains **no** reference to Host.
- **Rationale**: FR-017 demands "a new effects-shell component separate from the pure kernel,
  depend on the kernel (F01–F07) without making the kernel depend on it." The roadmap (§3) names
  exactly this project: `FS.GG.Governance.Host  effects shell (IO); depends on Kernel`. Keeping
  I/O out of the kernel preserves the kernel's V12 BCL-only hygiene proof and the constitution's
  dependency direction (governance may inspect a project; a project must never require
  governance). The name `Host` follows the roadmap; it is the *host* of the pure kernel's edge.
- **Alternatives considered**:
  - *Add the loop to the kernel assembly (as F02–F07 did).* Rejected: it would put `System.IO`
    and the impure `run` inside the kernel, breaking V12 and Principle IV's "interpretation only
    at the edge" — the effects shell is definitionally separate.
  - *Name it `FS.GG.Governance.Effects`.* Reasonable, but the roadmap already fixed `Host`; using
    it keeps the solution layout matching the published plan.

## D2 — A local MVU/effect algebra, NOT the Elmish package

- **Decision**: Model the boundary with a **local effect algebra** — `Effect` is a plain DU,
  `update: LoopConfig -> Msg -> Model -> Model * Effect list`, and `Interpreter.run` is the edge
  driver. **No `Elmish` (or `Elmish.*`) `PackageReference`.**
- **Rationale**: Constitution Principle IV explicitly permits this: "For libraries, CLIs, and
  small tools — the common shape in this repository — a local MVU/effect algebra is acceptable
  when it preserves the same separation: `update` is pure, I/O is represented as data or
  `Cmd<Msg>`, and interpretation happens only at the edge." The spec Assumptions concur ("A local
  MVU/effect algebra is acceptable … whether the Elmish package is used as the runtime is a
  plan/`.fsi` decision, constrained only by FR-001–FR-004"). A local algebra keeps the **zero new
  dependency** property (FR-017, SC-009) — Elmish would add a package and a `Program`/subscription
  runtime this synchronous, run-to-quiescence loop does not need. `Effect list` is the `Cmd<Msg>`
  the principle names; the interpreter is the "edge that executes effects and turns results back
  into `Msg`."
- **Alternatives considered**:
  - *Use `Elmish.Program` with `Cmd`.* Rejected: a dependency and a runtime model aimed at
    long-lived UI/subscription programs; this loop runs `init → update*` to quiescence and stops.
    The local algebra is simpler and dependency-free.
  - *Use `Async`/`Task` effects with an async `update`.* Rejected for `update` (it MUST be pure
    and synchronous — FR-002); async lives only in the *ports* at the edge if a real host needs
    it (the F08 ports are synchronous `Result`-returning functions — D5).

## D3 — `update` is pure and runs the kernel; the edge does I/O only

- **Decision**: `update` does **all** the decision logic — it asserts sensed facts, **runs the
  pure kernel** (`config.Rules |> List.map (CheckRule.toRule config.Bridge)` then
  `FixedPoint.evaluate config.Identify`), reads the resulting `RuleOutcome`s (via
  `config.Bridge.Project`) to find `NeedsReview` keys, applies the `AcceptancePolicy`, and emits
  the next `Effect list`. `Interpreter.run`/`step` only **execute** effects against ports and
  feed results back. Planning happens *inside* `update` because the kernel is pure (FR-006).
- **Rationale**: FR-002/FR-003 require `update` pure with all I/O reified; FR-006 requires
  planning to be "the already-shipped pure kernel … adding no new decision logic." Since
  `toRule`/`evaluate` are pure and total, running them inside `update` keeps the entire decision
  surface in the exhaustively-testable pure core (SC-001) and leaves the interpreter a thin,
  total executor. The cache-hit short-circuit (FR-008) falls out of the kernel itself: a
  `RecordedReview` fact present in the `Model.Facts` makes `toRule` emit `Decided` instead of
  `NeedsReview`, so no `DispatchReview` is produced (research D6).
- **Alternatives considered**: running the kernel in the interpreter and passing only verdicts
  back as `Msg`. Rejected: it would move decision logic out of `update`, defeating SC-001 (the
  point is that the loop's *entire* logic is a pure function of state and events).

## D4 — Effects, messages, and the loop lifecycle

- **Decision**: Five effects and five message families, driving three `Phase`s
  (`Sensing → Planning → Quiescent`):
  - `Effect = ReadArtifact of ArtifactRef | LoadReview of key | DispatchReview of ReviewDispatch
    | RecordVerdict of RecordedReview | EmitOutput of Output`.
  - `Msg = Sensed of ArtifactRef * Result<string,string> | Loaded of key *
    Result<RecordedReview option,string> | Reviewed of key * Result<JudgeVerdict list,string> |
    Recorded of key * Result<unit,string> | Disclosed of Disclosure`.
  - **Lifecycle**: `init` computes the F07 `Route` and emits one `ReadArtifact` per artifact the
    applicable rules declare (`Check.reads` over `config.Rules`, de-duplicated). After all sensed,
    `update` plans: it runs the kernel, and for each `NeedsReview` key not already resolved emits
    `LoadReview` (the cache lookup). A `Loaded (key, Ok (Some rr))` asserts the `RecordedReview`
    fact (cache HIT — short-circuit, FR-008) and re-plans; a `Loaded (key, Ok None)` emits
    `DispatchReview` (cache MISS). A `Reviewed (key, Ok samples)` runs `accept policy samples`:
    `Freeze v` emits `RecordVerdict` **and** asserts the `RecordedReview` fact (so this run uses
    it and the kernel decides) then re-plans; `StayPending` records nothing and leaves the
    conclusion `Uncertain` (FR-009). At quiescence (`Phase = Quiescent`) `update` emits the F06
    `EmitOutput` effects **once**.
- **Rationale**: this is the literal `sense → plan → act` decomposition the spec demands
  (FR-005/006/007), with `LoadReview` separated from `DispatchReview` so the cache hit is a
  *distinct, observable, suppress-the-dispatch* step (spec Edge Cases: "Cache hit, no dispatch").
  Every result — success or failure — is a `Msg` (FR-004); failures map to `Failure` values
  (D5). `Disclosed` carries the FR-013 bypass record into the loop as an ordinary event that is
  logged, never verdict-changing.
- **Alternatives considered**:
  - *Fold `LoadReview` into `DispatchReview` (dispatch, and let the judge "be" the cache).*
    Rejected: it conflates a free, deterministic cache read with the costly stochastic dispatch
    and makes "zero dispatches on re-run" (SC-003) unobservable.
  - *Emit F06 outputs from `run` after the loop instead of as an `Effect`.* Rejected: FR-015
    wants emission reified like every other I/O; an `EmitOutput` effect keeps the edge uniform
    and the `OutputSink` injectable/testable.

## D5 — Ports are injected, synchronous, `Result`-returning; failures are reified

- **Decision**: The edge ports are injected as a `Ports` record:
  `ArtifactReader = ArtifactRef -> Result<string,string>`,
  `Judge = ReviewTask -> Result<JudgeVerdict,string>` (called once per sample),
  `ReviewStore = { Load: string -> Result<RecordedReview option,string>; Save: RecordedReview ->
  Result<unit,string> }`, and `OutputSink = Output -> unit`. `Interpreter.step` wraps each port
  call so that **either** an `Error` return **or** a thrown exception becomes the matching failure
  `Msg`/`Failure` (`ArtifactUnavailable` / `ReviewDispatchFailed` / `ReviewStoreUnavailable`).
  `step` (and thus `run`) **never throws** (FR-012, SC-006).
- **Rationale**: FR-017 requires injected ports so the loop runs with a fake judge and no real
  network/agent (SC-009); FR-004/FR-012 require every real-world outcome — including failure — to
  re-enter as a `Msg`, and the interpreter to degrade safely. Synchronous `Result` functions are
  the simplest shape that (a) keeps the fake judge trivial, (b) lets a real host wrap async/IO
  behind the function, and (c) makes the failure path explicit data. Wrapping in `try/with` at
  the edge covers ports that throw despite the `Result` contract (a real `File.ReadAllText`
  throws), satisfying "never throws out of the interpreter." The `Failure` cases name **absent/
  bad input** distinctly from a tool defect (Principle VI): a defect surfaces as a test failure,
  never as a `Failure` value.
- **Alternatives considered**:
  - *`Async`/`Task` ports.* Deferred to the host: F08's contract is synchronous so the pure-core
    tests and the run-to-quiescence interpreter stay simple; a real CLI (F12) can run the
    synchronous `run` on a worker or wrap async I/O inside the port functions.
  - *Ports that throw (no `Result`).* Rejected: it would hide the failure taxonomy; explicit
    `Result` makes the FR-012 distinctions first-class while the `try/with` still guards the
    accidental throw.

## D6 — Freeze-then-cache reuses the F04 cache key and the kernel's own short-circuit

- **Decision**: The loop keys frozen evidence with **`CheckRule.cacheKey`** (judge id + check
  hash + artifact hashes + reviewer-prompt hash) — the **same** key `toRule` emits in its
  `NeedsReview.Key`. A `Freeze` produces `RecordedReview { Rule; Key; Verdict }`, which is
  `RecordVerdict`-ed to the store **and** asserted into `Model.Facts` (via `config.Bridge.Embed
  (Reviewed rr)`). The cache-hit short-circuit is the **kernel's own** behaviour: with that fact
  present, `toRule` finds it via `Bridge.Project` and emits `Decided`, so `update` produces **no**
  `DispatchReview` (FR-008).
- **Rationale**: FR-007/FR-008 require recording against the F04 key and short-circuiting on a
  re-run when it matches; decision #1 (F04) already folds judge identity into that key. Reusing
  `cacheKey` and the `Bridge` means F08 adds **no new cache logic** and inherits "any cache-key
  ingredient changing forces a fresh dispatch" for free (SC-003) — the key differs, no
  `RecordedReview` matches, `toRule` emits `NeedsReview`. The artifact-hash half of the key reads
  from the **sensed** facts via `Bridge.ArtifactHash`, which is why sensing precedes planning (D4).
- **Alternatives considered**: a bespoke F08 cache keyed differently. Rejected: it would
  reintroduce the drift decision #1 closed and duplicate `cacheKey`.

## D7 — No JSON-for-`Route`/no new serializer; reuse F06 `Json.*`; no packing

- **Decision**: The F06 edge outputs (`EmitOutput`) are the **existing** `Json.ofExplanation`,
  `Json.ofContract`, and `Route.renderRoute` (text) — `Output = ExplanationJson | ContractJson |
  RouteText`. F08 adds **no** new serializer and **no** `Json.ofRoute`. `FS.GG.Governance.Host`
  does **not** pack in F08.
- **Rationale**: FR-015 says "emit the F06 edge outputs … the persisted/printed artifacts the
  pure F06 layer produced as values and deferred emitting" — so the loop *emits* what F06 already
  *produces*, it does not invent formats. A `Json.ofRoute` was explicitly deferred by F07
  (research D7 there) to F08/F12; F08 still defers it — `Route.renderRoute` text suffices for the
  edge, and a JSON route is an F12 concern when the CLI fixes its output contract (YAGNI).
  Packing: the kernel packs at F06 and the CLI tool packs at F12 (roadmap §7); F08 is an
  intermediate library consumed by F12, so it inherits `IsPackable=false`.
- **Alternatives considered**: adding `Json.ofRoute` now. Rejected: speculative until F12 defines
  the CLI's JSON surface; adding it later is purely additive.

## D8 — Structured logging: none for F08 (ADR), observability via values + the sink

- **Decision**: Resolve `TODO(STRUCTURED_LOGGING)` for F08 with an ADR
  (`docs/decisions/0001-structured-logging.md`): **no structured-logging library** is added.
  Observability is delivered as **values** — the `Model.Failures` and `Model.Disclosures` lists,
  the F06 outputs through the injected `OutputSink` — which the host (F12) renders with whatever
  logging it chooses.
- **Rationale**: The constitution's `TODO(STRUCTURED_LOGGING)` and the roadmap (§5: "Record the
  choice in an ADR before F08, the first feature that does real IO") require a recorded decision,
  not necessarily a dependency. FR-012/Principle VI are satisfied by reifying every operationally
  significant event as a `Failure`/`Disclosure`/`Output` value the host can log — this keeps
  F08's "zero new dependency / light by default" stance (FR-017, SC-009) and defers the concrete
  logging sink to the host that actually owns process lifecycle. The ADR documents that a logging
  library, if ever wanted, is added at the **host/CLI** boundary, never in the kernel or the pure
  `Loop`.
- **Alternatives considered**:
  - *Add `Microsoft.Extensions.Logging` / Serilog now.* Rejected: a dependency the pure loop
    cannot use (it is I/O) and the edge does not yet need — the `OutputSink` plus value-based
    failure reporting covers F08; the CLI can wire a logger behind the sink.

---

### Build order (consequence of D1/D4)
The new project compiles `Loop.fsi`/`Loop.fs` **then** `Interpreter.fsi`/`Interpreter.fs`
(`Interpreter` references `Loop`'s `Effect`/`Msg`/`Model`/`LoopConfig`/`Output`). The project
references only `FS.GG.Governance.Kernel`; **zero new `PackageReference`** (D2/D6/D7). The test
project references Host (Expecto + FsCheck, already pinned).
