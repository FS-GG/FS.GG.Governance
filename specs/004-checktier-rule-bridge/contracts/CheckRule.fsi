// Curated public signature contract for the CheckTier & Rule bridge (F04).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution
// Principle II). The matching CheckRule.fs carries NO `private`/`internal`/`public`
// modifiers on top-level bindings — visibility is presence/absence here.
//
// Design-first artifact: this contract is drafted and exercised in FSI
// (scripts/prelude.fsx) before any CheckRule.fs body exists (Principle I). The shapes
// mirror docs/governance-design/rule-edsl.md ("The bridge back to the kernel") and the
// CheckTier arbitration model in docs/governance-design/kernel.md, and lock decision #1
// (the agent-review cache key includes the judge identity) from the roadmap.
//
// This is the bridge: it pairs an F03 `Check<'fact>` with a declaration of WHO decides
// it (`CheckTier`), HOW BADLY a failure matters (`Severity`), and WHICH requirement it
// enforces (`SpecSource`), then turns that authored `CheckRule<'fact>` into the kernel's
// executable `Rule<'fact>` (F01). It performs NO agent call and NO I/O — it emits a
// `RuleOutcome` as data (a verdict, a review request, or an escalation); the actual
// dispatch/recording is the F08 edge interpreter's job. Reuses F03 `Check.eval`/`hash`/
// `reads`/`render`/`isReified` and F01 `Rule`/`FactSet`/`RuleId`; zero new dependencies.

namespace FS.GG.Governance.Kernel

/// WHO is competent to decide a rule (docs/governance-design/kernel.md §CheckTier).
/// Orthogonal to `Severity`. `Deterministic` requires a fully-reified check (the
/// `rule` constructor refuses it otherwise — FR-006); `AgentReviewed`/`HumanOnly`
/// accept any check, including one with an `Opaque` node.
type CheckTier =
    /// A machine decides: `toRule` evaluates the check every run, reproducibly.
    | Deterministic
    /// An AI agent decides: `toRule` consults the review cache and, on a miss, emits a
    /// review request (the verdict is later frozen as evidence by F08).
    | AgentReviewed
    /// A person decides: `toRule` escalates (emits a blocker) and never decides.
    | HumanOnly

/// HOW BADLY a failure matters — orthogonal to `CheckTier`. Defaults to `Advisory`
/// (the `rule` constructor sets it; `blocking` promotes it). Consumed by routing (F07)
/// to decide whether a failing verdict blocks; `HumanOnly` escalates regardless (FR-010).
type Severity =
    | Advisory
    | Blocking

/// A stable, structural handle to the authoritative requirement a rule enforces, carried
/// for provenance and the single-source contract fold (F06). Renderable and hashable;
/// the algebra never interprets its content (FR-003). Exact field choice is a design
/// detail — `Document` names the source, `Section` the clause within it.
type SpecSource = { Document: string; Section: string }

/// The identity of the stochastic judge, folded into the agent-review cache key so a
/// verdict frozen by one judge is never silently reused after the judge changes
/// (decision #1 / FR-013). The reviewer PROMPT is NOT here — it is the rule's `Question`,
/// so the prompt half of the key is per-rule while the model identity is per-run config.
type JudgeId = { ModelId: string; Version: string }

/// Emitted by `toRule` on an `AgentReviewed` cache MISS: a typed request for the edge
/// interpreter (F08) to dispatch to an agent. The bridge produces it but never acts on
/// it (Principle IV: I/O represented as data). `Key` is the content-hash cache key.
type ReviewRequest =
    { Rule: RuleId
      Question: string option
      Key: string }

/// A frozen agent verdict, recorded by F08 against its cache `Key` (carried as the
/// `Reviewed` case of `RuleOutcome`). `toRule` RECOGNISES one (via `Bridge.Project`) to
/// short-circuit an `AgentReviewed` rule on a cache HIT (FR-009/FR-014); recording it is
/// F08's job, recognising it is the bridge's.
type RecordedReview =
    { Rule: RuleId
      Key: string
      Verdict: Verdict }

/// The governance outcome a bridged rule asserts each run — the domain-neutral payload
/// the kernel carries. An adapter embeds it into its own `'fact` (`Bridge.Embed`) and
/// projects it back (`Bridge.Project`); F07/F08 pattern-match it to route and to
/// dispatch. It is the only governance vocabulary the kernel owns (FR-015). `toRule`
/// EMITS `Decided`/`NeedsReview`/`Escalated`; `Reviewed` is written by the F08 edge and
/// only READ by `toRule` (the cache-hit lookup), never emitted by it.
type RuleOutcome =
    /// A decided verdict: a `Deterministic` evaluation, or a cache-HIT recorded verdict.
    | Decided of rule: RuleId * verdict: Verdict
    /// A cache MISS on an `AgentReviewed` rule: a review must be dispatched (F08).
    | NeedsReview of request: ReviewRequest
    /// A frozen agent verdict recorded by the F08 edge against its cache `Key`. Read by
    /// `toRule`'s cache-hit lookup (matched on `Key`); `toRule` never emits this itself.
    | Reviewed of review: RecordedReview
    /// A `HumanOnly` escalation: the kernel blocks and asks; it never decides.
    | Escalated of rule: RuleId

/// An authored governance rule (docs/governance-design/rule-edsl.md): a reified `Check`
/// paired with its tier, spec source, severity, and optional reviewer prompt. DISTINCT
/// from the kernel's executable `Rule<'fact>` (F01) — `toRule` translates this into that.
/// (Named `CheckRule` to avoid the clash with the already-shipped `Rule<'fact>`; see
/// research D1.) Build it with the `rule`/`blocking`/`asking` constructors, not by hand,
/// so the `Deterministic`-tier reified-ness guardrail (FR-006) cannot be bypassed.
type CheckRule<'fact> =
    { Id: RuleId
      Tier: CheckTier
      Spec: SpecSource
      Severity: Severity
      Check: Check<'fact>
      Question: string option }

/// Why authoring a rule was refused. The only refusal is the reified-ness guardrail:
/// an `Opaque` check cannot masquerade as `Deterministic` (FR-006, SC-001).
type RuleRejection =
    | OpaqueCannotBeDeterministic of RuleId

/// The caller-supplied bridge between the domain-neutral `RuleOutcome` and an adapter's
/// own `'fact` vocabulary, plus the judge identity and the artifact-content lookup. This
/// is what keeps the kernel domain-neutral (FR-015): the adapter owns how a governance
/// outcome embeds in `'fact`, how a recorded verdict is recognised, and how an artifact's
/// content hash is read FROM THE FACTS (no live I/O — an adapter asserts artifact-content
/// facts; the bridge looks them up, so `toRule` stays pure).
type Bridge<'fact> =
    { /// Identity of the judge, folded into every agent-review cache key (decision #1).
      Judge: JudgeId
      /// Content hash of an artifact, read from the current facts. Total: an unknown
      /// artifact yields a fixed sentinel (e.g. ""), never an exception.
      ArtifactHash: FactSet<'fact> -> ArtifactRef -> string
      /// Lift a governance outcome into the adapter's fact value.
      Embed: RuleOutcome -> 'fact
      /// Recover a governance outcome from an adapter fact (None if it is not one) — used
      /// to find a `RecordedReview` whose `Key` matches, for the cache-hit short-circuit.
      Project: 'fact -> RuleOutcome option }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CheckRule =

    // ── Smart constructors: the readable rule-authoring surface (FR-005) ──

    /// Author a rule from its identity, tier, spec source, and check, with `Severity`
    /// defaulting to `Advisory` and no reviewer question. REFUSES the `Deterministic`
    /// tier when the check is not fully reified (`not (Check.isReified check)`), returning
    /// `Error (OpaqueCannotBeDeterministic id)` — an `Opaque` check is forced to
    /// `AgentReviewed`/`HumanOnly` (FR-006, SC-001). All other tiers always succeed.
    val rule:
        id: RuleId -> tier: CheckTier -> spec: SpecSource -> check: Check<'fact> ->
            Result<CheckRule<'fact>, RuleRejection>

    /// Promote a rule's severity to `Blocking` (leaves its tier unchanged) (FR-005).
    val blocking: rule: CheckRule<'fact> -> CheckRule<'fact>

    /// Attach the reviewer prompt and set the `AgentReviewed` tier (FR-005). Because it
    /// targets `AgentReviewed` (which accepts any check), this is the natural constructor
    /// for an agent rule over an `Opaque`/non-reified check, and never trips FR-006.
    val asking: prompt: string -> rule: CheckRule<'fact> -> CheckRule<'fact>

    // ── The cache key: decision #1, a pure fold over its ingredients (FR-011/FR-012) ──

    /// The agent-review content-hash cache key, combining EXACTLY: the judge model id and
    /// version, the check's structural hash (`Check.hash`), the content hashes of the
    /// artifacts the check reads (de-duplicated and ordinal-sorted — order-independent,
    /// the F04 policy F03's `reads` deferred), and the reviewer-prompt hash (of
    /// `question`). Pure and total: identical ingredients → identical key; any ingredient
    /// changing → a different key (SC-002). Kept separate from `toRule` so each ingredient
    /// can be varied in isolation under test. Encoding (a SHA-256 hex digest) is a detail.
    val cacheKey:
        judge: JudgeId -> checkHash: string -> artifactHashes: string list -> question: string option ->
            string

    // ── The bridge to the executable kernel rule (FR-007 … FR-010) ──

    /// Translate an authored `CheckRule<'fact>` into the kernel's executable `Rule<'fact>`
    /// (F01), whose `Description` is the rendered check (`Check.render`) so it cannot drift
    /// from what is enforced (FR-007, SC-006). The produced rule's `Apply`, per tier:
    ///   • `Deterministic` → assert `Decided (id, Check.eval facts check)` — the verdict is
    ///     never coerced (FR-008, SC-005).
    ///   • `AgentReviewed` → compute `cacheKey` over `bridge`'s judge + artifact hashes +
    ///     the rule's check/question; on a HIT (a `RecordedReview` with that key found via
    ///     `bridge.Project`) assert `Decided (id, recorded verdict)` and emit NO request;
    ///     on a MISS assert `NeedsReview { … Key = key }` — exactly one, no agent call
    ///     (FR-009, SC-003/SC-004).
    ///   • `HumanOnly` → assert `Escalated id`, regardless of severity (FR-010, SC-008).
    /// Every emitted fact is `bridge.Embed`-ed into `'fact` and carries a `ProvenanceStep`
    /// naming the rule (its `Note` is the rendered check). The step's `Inputs` are `[]` for
    /// `Deterministic`/`HumanOnly`/`AgentReviewed`-miss — the domain-neutral `Bridge` cannot
    /// resolve a read `ArtifactRef` to a `FactId` (`ArtifactHash` yields a content-hash
    /// string, `Project` only `RuleOutcome`s; recovering ids would breach FR-015) — and on an
    /// `AgentReviewed` cache **hit** are `[ <the matching RecordedReview's FactId> ]`, the
    /// recorded input the rule consumed. (Artifact-read provenance is deferred; a later
    /// feature may add a `Bridge` resolver if F06 needs it.) Total for every rule and fact
    /// set (FR-017, SC-007); performs no I/O and no agent call (FR-015).
    val toRule: bridge: Bridge<'fact> -> rule: CheckRule<'fact> -> Rule<'fact>
