// Curated public signature contract for the pure MVU core of the effects shell (F08).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution
// Principle II). The matching Loop.fs carries NO `private`/`internal`/`public` modifiers
// on top-level bindings — visibility is presence/absence here.
//
// Design-first artifact: this contract is drafted and exercised in FSI
// (scripts/prelude.fsx) before any Loop.fs body exists (Principle I). The shapes mirror
// docs/governance-design/routing-and-modes.md ("How a route is computed") and the
// open-questions decisions #2 (aggregate/threshold before freezing) and #3 (instruction/
// data isolation), which this feature LOCKS.
//
// This is the PURE side of the Elmish/MVU boundary (Constitution Principle IV, applicable
// for the FIRST time): a `Model` (durable loop state), a `Msg` (every effect result +
// internal transitions), an `Effect` (the I/O the loop REQUESTS as data), `init`, and a
// pure, total `update`. `update` SENSES (asserts sensed facts), PLANS (runs the already-
// shipped pure kernel — F04 `toRule` + F01 `FixedPoint.evaluate`), and ACTS (emits effects)
// — but performs NO I/O and NEVER throws (FR-002). Executing those effects is the edge
// `Interpreter`'s job (Interpreter.fsi). It is a LOCAL effect algebra (no Elmish package —
// research D2): `Effect list` IS the `Cmd<Msg>`. Reuses F07 `Route`/`RunMode`/`Fence`, F06
// `Json`/`Contract`/`Freshness` (emitted at the edge), F04 `CheckRule`/`Bridge`/`cacheKey`/
// `RecordedReview`/`Verdict`, and F01–F03 (facts, rules, checks). Zero new dependency.

namespace FS.GG.Governance.Host

open FS.GG.Governance.Kernel

/// Untrusted artifact content sensed from the world — the artifact ref plus its raw
/// textual content. It is carried ONLY on the DATA channel of a review (`ReviewTask.Data`),
/// never the instruction channel (decision #3, FR-010). The kernel never interprets it as
/// instruction.
type ArtifactContent = { Ref: ArtifactRef; Content: string }

/// One sample answer from the stochastic judge: a `Verdict` (F02) plus a `Confidence` in
/// `[0.0, 1.0]`. The acceptance policy (`accept`) aggregates a list of these before any
/// verdict is frozen (decision #2, FR-009).
type JudgeVerdict = { Verdict: Verdict; Confidence: float }

/// The dispatched review request, with the reviewer INSTRUCTION isolated from the untrusted
/// artifact DATA (decision #3, FR-010, SC-005). `Instruction` is the rule's `Question` — the
/// ONLY instruction channel; `Data` is the untrusted artifact content. The two are SEPARATE
/// fields the loop never merges, so a malicious artifact cannot become instruction. `Key` is
/// the F04 content-hash cache key the verdict will be frozen against.
type ReviewTask =
    { Key: string
      Instruction: string
      Data: ArtifactContent list }

/// A request the pure core emits for one cache-MISS agent review (carried by
/// `DispatchReview`): the isolated `Task` plus how many samples the acceptance policy wants
/// the edge to draw (`samplesFor policy`).
type ReviewDispatch =
    { Task: ReviewTask
      Samples: int }

/// The configurable rule that decides whether a stochastic verdict is trustworthy enough to
/// be FROZEN as durable evidence (decision #2, FR-009). Applied by `accept` BEFORE any
/// freeze; a verdict that fails the policy is never recorded (it stays `Uncertain`/pending).
type AcceptancePolicy =
    /// Accept a single sample as-is — the DOCUMENTED, deterministic DEFAULT (`defaultPolicy`).
    /// No aggregation; the lone sample's verdict is frozen (FR-009 default clause).
    | SingleSample
    /// Require at least `count` samples to agree on the same verdict before freezing it;
    /// otherwise `StayPending` (no hidden promotion of a split vote).
    | Agreement of count: int
    /// Require the mean sample `Confidence` to be at least `threshold` (with the samples in
    /// agreement) before freezing; otherwise `StayPending`.
    | Confidence of threshold: float

/// The result of applying an `AcceptancePolicy` to a sample set (the output of `accept`):
/// either FREEZE a definite verdict, or STAY PENDING — never a silent promotion of a noisy
/// sample to a definite pass/fail (FR-009, SC-004).
type Acceptance =
    | Freeze of Verdict
    | StayPending

/// An observable disclosure of a bypass/override (FR-013): a logged justification recorded
/// as a fact ABOUT the run. It is reported, never acted on — disclosure NEVER silently flips
/// a verdict (honesty about evidence is separate from the freedom to iterate).
type Disclosure = { Rule: RuleId; Justification: string }

/// Why an interpreted effect could not complete — reified as DATA so the loop degrades
/// safely and stays observable (FR-012, Principle VI). Each case names ABSENT/BAD INPUT (a
/// fact about the world), kept distinct from a tool defect (which surfaces as a test failure,
/// never a `Failure` value).
type Failure =
    /// A `ReadArtifact` for a missing/unreadable artifact failed; the affected conclusion
    /// becomes `Uncertain`/`Failed` (never a silent pass) (FR-012, SC-006).
    | ArtifactUnavailable of artifact: ArtifactRef * reason: string
    /// A `DispatchReview` to the judge port failed/timed out; the review stays pending and
    /// the loop continues (FR-012, SC-006).
    | ReviewDispatchFailed of key: string * reason: string
    /// A `LoadReview`/`RecordVerdict` against the review store failed; the in-memory verdict
    /// (if any) is still used this run, but persistence is reported as failed (FR-012).
    | ReviewStoreUnavailable of key: string * reason: string

/// A persisted/printed F06 output the loop EMITS at the edge (FR-015) — values F06 already
/// PRODUCES, whose emission F06 deferred. No new serializer is introduced (research D7).
type Output =
    /// `Json.ofExplanation` of the planned check's proof tree.
    | ExplanationJson of string
    /// `Json.ofContract` of the applicable rules' published contract.
    | ContractJson of string
    /// `Route.renderRoute` of the acted-on route (text).
    | RouteText of string

/// The I/O the pure core REQUESTS but NEVER performs (FR-003) — the `Cmd<Msg>` of this local
/// algebra. Every case is executed ONLY by the edge `Interpreter`, which turns each result
/// back into a `Msg` (FR-004). `update` emits these; it runs none of them.
type Effect =
    /// SENSE: read a governed artifact's content (FR-005).
    | ReadArtifact of ArtifactRef
    /// ACT (cache lookup): load a frozen verdict by its F04 cache key; a HIT short-circuits
    /// the dispatch (FR-008), so the loop never emits a `DispatchReview` it would discard.
    | LoadReview of key: string
    /// ACT: dispatch a cache-MISS review to the injected judge port (FR-007).
    | DispatchReview of ReviewDispatch
    /// ACT: persist an accepted verdict as frozen evidence, keyed by the F04 cache key
    /// (FR-007).
    | RecordVerdict of RecordedReview
    /// Emit an F06 output at the edge (FR-015).
    | EmitOutput of Output

/// An event the loop accepts — every external result (success OR failure) and the internal
/// transitions. The ONLY way the world (or the loop) advances the `Model` (FR-001/FR-004).
/// Generic over the kernel's `'fact`.
type Msg<'fact> =
    /// An artifact read completed: `Ok content` (sensed) or `Error reason` (unavailable).
    | Sensed of artifact: ArtifactRef * result: Result<string, string>
    /// A store lookup completed: `Ok (Some recorded)` is a cache HIT, `Ok None` a MISS,
    /// `Error reason` a store failure.
    | Loaded of key: string * result: Result<RecordedReview option, string>
    /// A dispatched review returned its samples (`Ok samples`) or failed (`Error reason`).
    | Reviewed of key: string * result: Result<JudgeVerdict list, string>
    /// A `RecordVerdict` completed (`Ok ()`) or failed (`Error reason`).
    | Recorded of key: string * result: Result<unit, string>
    /// A bypass/override disclosure entered the loop (FR-013) — logged, never verdict-changing.
    | Disclosed of Disclosure

/// Which stage of the sense→plan→act loop the `Model` is in. `Sensing` while artifacts are
/// being read; `Planning` while the kernel runs and reviews are loaded/dispatched/recorded;
/// `Quiescent` once `update` emits no more effects (the loop is done) (FR-001).
type Phase =
    | Sensing
    | Planning
    | Quiescent

/// The durable state the governance loop owns (FR-001) — the single value the pure
/// transitions read and rewrite. Generic over the kernel's `'fact`; it DROPS `'change` (like
/// `Route`), carrying only the computed route and the kernel-side state.
type Model<'fact> =
    { /// The current loop stage.
      Phase: Phase
      /// The facts sensed and recorded so far (supplied artifact-content facts + frozen
      /// `RecordedReview` facts + everything the kernel derived), de-duplicated by `FactId`
      /// (FR-014).
      Facts: FactSet<'fact>
      /// The F07 `Route` the loop acts on — computed at `init` from the supplied (base)
      /// fences/rules/mode/change and stable thereafter (FR-011).
      Route: Route
      /// The review cache keys awaiting a verdict (dispatched, not yet resolved).
      Pending: Set<string>
      /// The observable bypass/override log (FR-013) — deterministically ordered.
      Disclosures: Disclosure list
      /// The safe-failure record (FR-012) — deterministically ordered; distinguishes
      /// absent/bad input from a tool defect.
      Failures: Failure list
      /// The number of planning rounds that produced at least one new effect before
      /// quiescence (observability; mirrors `EvaluationResult.Rounds`).
      Rounds: int }

/// The PURE wiring the loop is generic over, SUPPLIED by the host/adapter (F09+/F12), NOT
/// defined here — so F08 ships no domain adapter and stays domain-neutral (FR-017). It
/// carries the kernel catalog + bridge, the routing inputs, the acceptance policy, and the
/// pure lift that turns sensed content into a `'fact`. Everything in it is pure data/
/// functions; none of it performs I/O (the I/O is the injected `Ports` at the edge).
type LoopConfig<'change, 'fact> =
    { /// The kernel's sole identity authority (F01) — assigns `FactId`, dedups, names inputs.
      Identify: 'fact -> FactId
      /// The rules that ALREADY apply to this change (caller-filtered — F07 research D5), to
      /// be bridged with `CheckRule.toRule Bridge` and evaluated.
      Rules: CheckRule<'fact> list
      /// The F04 bridge: lifts a `RuleOutcome` into `'fact` (`Embed`), recovers one
      /// (`Project`) for the cache-hit lookup, carries the `JudgeId`, and reads an artifact's
      /// content hash FROM the sensed facts (`ArtifactHash`).
      Bridge: Bridge<'fact>
      /// The declared fences that raise stakes (F07) — used to compute the `Route`.
      Fences: Fence<'change> list
      /// The run mode (F07): blocking gates are enforced ONLY at `Gate`, recomputed from base
      /// (FR-011).
      Mode: RunMode
      /// The configurable freeze policy (decision #2, FR-009).
      Policy: AcceptancePolicy
      /// Lift a sensed artifact's `(ref, content)` into a `'fact` the kernel evaluates and the
      /// bridge's `ArtifactHash` can read — the adapter's artifact-fact shape (FR-005).
      SenseArtifact: ArtifactRef -> string -> 'fact }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Loop =

    // ── The acceptance policy (decision #2, FR-009) ──

    /// The documented, deterministic DEFAULT acceptance policy: `SingleSample` (FR-009).
    /// Stated explicitly so there is no hidden promotion behaviour.
    val defaultPolicy: AcceptancePolicy

    /// How many judge samples a dispatch should draw for a policy: `SingleSample`/`Confidence`
    /// → 1, `Agreement count` → `count` (at least 1). Pure & total; lets `DispatchReview`
    /// carry the sample budget the edge fulfils.
    val samplesFor: policy: AcceptancePolicy -> int

    /// Apply an acceptance policy to a sample set — a PURE, TOTAL fold (FR-009, SC-004):
    ///   • `SingleSample` → `Freeze` the lone/first sample's verdict; `[]` → `StayPending`.
    ///   • `Agreement n` → `Freeze v` iff at least `n` samples share the verdict `v`; else
    ///     `StayPending`.
    ///   • `Confidence t` → `Freeze v` iff the samples agree on `v` AND their mean confidence
    ///     is `>= t`; else `StayPending`.
    /// A result that fails the policy is NEVER frozen (it stays `Uncertain`/pending) — judge
    /// noise is not laundered into durable evidence (FR-009, SC-004).
    val accept: policy: AcceptancePolicy -> samples: JudgeVerdict list -> Acceptance

    // ── The MVU core: init + the pure transition (FR-001, FR-002) ──

    /// Build the initial `Model` and the startup `Effect list` for a change (FR-001). PURE
    /// (FR-002): computes the F07 `Route` (`Route.route config.Fences config.Rules config.Mode
    /// change`) and emits one `ReadArtifact` per DISTINCT artifact the applicable rules declare
    /// they read (the de-duplicated union of `Check.reads` over `config.Rules`) — the SENSE
    /// step (FR-005). Performs NO I/O. A change whose rules read nothing reaches `Planning`
    /// immediately; a change with no rules and no reads is well-formed and heads to quiescence
    /// with an empty derivation (spec Edge Cases "Nothing to do").
    val init: config: LoopConfig<'change, 'fact> -> change: 'change -> Model<'fact> * Effect list

    /// The PURE, TOTAL transition (FR-002): `(config, msg, model) ⇒` next `Model` + requested
    /// `Effect list`. Performs NO I/O (no filesystem, network, process, clock, or agent) and
    /// NEVER throws; for identical inputs it returns byte-for-byte identical outputs.
    ///
    /// It SENSES, PLANS, and ACTS, adding NO decision logic beyond the kernel (FR-006):
    ///   • `Sensed (ref, Ok content)` → assert `SenseArtifact ref content` into `Facts` (deduped
    ///     by `Identify`); when sensing is complete, PLAN.
    ///   • `Sensed (ref, Error e)` → record `ArtifactUnavailable`; the affected conclusion stays
    ///     `Uncertain`/`Failed` (FR-012); continue.
    ///   • PLAN = bridge the rules (`CheckRule.toRule config.Bridge`) and run
    ///     `FixedPoint.evaluate config.Identify`; for each `NeedsReview` key not already pending
    ///     or recorded, emit `LoadReview key` (FR-006/FR-008).
    ///   • `Loaded (key, Ok (Some rr))` → cache HIT: assert the `RecordedReview` fact and re-plan;
    ///     emit NO dispatch (FR-008). `Loaded (key, Ok None)` → cache MISS: emit `DispatchReview`
    ///     with the rule's `Question` as the ISOLATED instruction and the read artifacts' content
    ///     as untrusted `Data` (FR-007/FR-010). `Loaded (key, Error e)` → `ReviewStoreUnavailable`.
    ///   • `Reviewed (key, Ok samples)` → `accept config.Policy samples`: `Freeze v` emits
    ///     `RecordVerdict { Rule; Key=key; Verdict=v }` AND asserts the `RecordedReview` fact
    ///     (so the kernel decides this run), removing `key` from `Pending`; `StayPending` records
    ///     nothing and leaves the conclusion `Uncertain` (FR-009/SC-004). `Reviewed (key, Error
    ///     e)` → `ReviewDispatchFailed`; the review stays pending (FR-012).
    ///   • `Recorded (key, Ok ())` → no-op (the fact is already asserted; IDEMPOTENT, FR-014).
    ///     `Recorded (key, Error e)` → `ReviewStoreUnavailable` (the run still uses the in-memory
    ///     verdict).
    ///   • `Disclosed d` → append `d` to `Disclosures`; NEVER changes a verdict (FR-013).
    ///   • At quiescence (no pending sensing, no pending reviews, no new `NeedsReview`) set
    ///     `Phase = Quiescent` and emit the F06 `EmitOutput` effects ONCE (FR-015).
    ///
    /// IDEMPOTENT and ORDER-INDEPENDENT (FR-014, SC-007): re-applying the same result `Msg`
    /// records no duplicate verdict or fact (facts dedup by `FactId`; `Pending`/recorded
    /// membership is checked), and the final `Model` is identical across permutations of the
    /// completion order of independent effects (the kernel's least-fixed-point is order-
    /// independent and `Failures`/`Disclosures` are deterministically ordered). Total for every
    /// `(config, msg, model)` (FR-002, SC-006).
    val update:
        config: LoopConfig<'change, 'fact> ->
        msg: Msg<'fact> ->
        model: Model<'fact> ->
            Model<'fact> * Effect list
