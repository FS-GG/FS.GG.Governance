// Curated public signature contract for the reified Check algebra (F03).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution
// Principle II). The matching Check.fs carries NO `private`/`internal`/`public`
// modifiers on top-level bindings — visibility is presence/absence here.
//
// Design-first artifact: this contract is drafted and exercised in FSI
// (scripts/prelude.fsx) before any Check.fs body exists (Principle I). The shapes
// mirror docs/governance-design/rule-edsl.md (the authoritative `Check` model) and the
// Hazard-3 commutative-hash mitigation in docs/governance-design/theory-and-composition.md.
// The algebra and all six interpreters live IN the kernel assembly (roadmap §3) so every
// adapter (F09–F11) reuses them and supplies only its probe set, with zero new deps.
//
// Check is the keystone: a rule's check is ONE value that can be evaluated, rendered,
// hashed, explained, read for inputs, and tested for reified-ness — six folds over one
// source that therefore cannot drift apart. It builds on the F02 `Verdict` (eval/explain
// reuse Verdict.all/any/negate) and the F01 `FactSet<'fact>` (a probe's Eval consumes it).

namespace FS.GG.Governance.Kernel

/// A stable, structural handle to a governed artifact, naming it by a `Kind` and a
/// `Key`. The SINGLE point at which an adapter's own artifact vocabulary meets the
/// otherwise domain-neutral algebra: an adapter maps its closed union (design tokens,
/// essay sections, …) onto this. Renderable and hashable (FR-001).
type ArtifactRef = { Kind: string; Key: string }

/// The three-valued result a probe reports for one atomic check. Maps one-to-one onto
/// the F02 `Verdict`: `Met → Pass`, `Unmet → Fail`, `Unknown → Uncertain` (FR-002).
/// `Unknown` is the "a competent judge has not yet ruled" state (spec prose: "undecided")
/// — it is preserved through composition, never silently coerced to met/unmet.
type Outcome =
    | Met
    | Unmet of reason: string
    | Unknown of reason: string

/// A declared, renderable/hashable argument of a probe — describing the probe's
/// parameters AS DATA so they appear in the rendered contract and the cache key.
/// The argument list is ORDERED: meaning depends on order, so the hash is positional
/// over it (FR-008, SC-002).
type ProbeArg =
    | ArtifactArg of ArtifactRef
    | LiteralArg of string
    | NumberArg of double

/// The only part of the algebra an adapter supplies. It is itself data: `Name`, `Args`,
/// and `Reads` are the DECLARED SHAPE that gets rendered and hashed; `Eval` is only ever
/// RUN, never rendered or hashed (FR-003). `Reads` declares the artifacts the probe reads
/// (drives routing and the artifact half of the cache key); `Args` are its ordered,
/// declared parameters.
type Probe<'fact> =
    { Name: string
      Reads: ArtifactRef list
      Args: ProbeArg list
      Eval: FactSet<'fact> -> Outcome }

/// The closed, reified combinator algebra — a single value that the six interpreters
/// fold. Deliberately APPLICATIVE, never monadic: there is no `bind` or data-dependent
/// sequencing inside a `Check` (FR-012), so its structure is fixed a priori and
/// `render`/`hash`/`reads`/`isReified` can fold it WITHOUT executing any probe. The set
/// of combinators is closed by design (third parties add probes, not cases).
type Check<'fact> =
    /// An atomic probe — the leaf of the algebra.
    | Atom of Probe<'fact>
    /// Conjunction: "all of these must hold". Commutative (the member order does not
    /// affect the verdict or the hash).
    | All of Check<'fact> list
    /// Disjunction: "at least one of these must hold". Commutative.
    | Any of Check<'fact> list
    /// Negation: flips pass/fail, leaves Uncertain unchanged (reuses Verdict.negate).
    | Not of Check<'fact>
    /// Implication "a implies b": desugars to `Any [Not a; b]` for `eval`/`explain`, but
    /// stays POSITIONAL for `render`/`hash` (`a ==> b` ≠ `b ==> a`).
    | Implies of Check<'fact> * Check<'fact>
    /// Escape hatch for the rare irreducible check. Carries a name but NO inspectable
    /// inner structure: `isReified` returns false (so the F04 rule builder refuses
    /// Deterministic tier, forcing AgentReviewed/HumanOnly), it renders/hashes by its
    /// name only, and contributes no declared reads. Opacity is visible, never silent.
    | Opaque of name: string * eval: (FactSet<'fact> -> Outcome)

/// A structured proof tree produced by `explain`: it mirrors the check's SURFACE shape,
/// records each atomic probe's met/unmet/unknown `Outcome`, and carries at every node the
/// rolled-up `Verdict`. The top node's verdict is identical to `eval` over the same check
/// and facts (FR-009, SC-004) — the two folds cannot disagree. Non-generic (it records
/// names/outcomes/verdicts, never `'fact`). Its JSON serialization is deferred to F06.
type Explanation =
    | AtomExplained of name: string * outcome: Outcome * verdict: Verdict
    | AllExplained of parts: Explanation list * verdict: Verdict
    | AnyExplained of parts: Explanation list * verdict: Verdict
    | NotExplained of part: Explanation * verdict: Verdict
    | ImpliesExplained of antecedent: Explanation * consequent: Explanation * verdict: Verdict
    | OpaqueExplained of name: string * outcome: Outcome * verdict: Verdict

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Explanation =

    /// The rolled-up verdict carried at the proof tree's root. Equals `Check.eval` over
    /// the check and facts that produced this explanation (FR-009, SC-004).
    val verdict: explanation: Explanation -> Verdict

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Check =

    // ── Smart constructors / operators: the readable rule-authoring surface (FR-005) ──
    // The custom operators (==>, .&, .|) are thin aliases that add no semantics; they
    // exist so checks read like the sentences they enforce (Principle III justification
    // in plan.md). `open Check` brings the operators into infix scope.

    /// Build an atomic check from a probe's declared shape and its evaluation function:
    /// `Atom { Name = name; Reads = reads; Args = args; Eval = eval }`.
    val probe:
        name: string -> reads: ArtifactRef list -> args: ProbeArg list -> eval: (FactSet<'fact> -> Outcome) ->
            Check<'fact>

    /// "all of these must hold" (= `All`).
    val allOf: checks: Check<'fact> list -> Check<'fact>

    /// "at least one of these must hold" (= `Any`).
    val anyOf: checks: Check<'fact> list -> Check<'fact>

    /// Negation (= `Not`).
    val not': check: Check<'fact> -> Check<'fact>

    /// Implication "a implies b" (= `Implies (a, b)`). Positional.
    val (==>): a: Check<'fact> -> b: Check<'fact> -> Check<'fact>

    /// Binary conjunction "a and b" (= `All [a; b]`).
    val (.&): a: Check<'fact> -> b: Check<'fact> -> Check<'fact>

    /// Binary disjunction "a or b" (= `Any [a; b]`).
    val (.|): a: Check<'fact> -> b: Check<'fact> -> Check<'fact>

    // ── The six interpreters: one algebra, folded six ways (FR-006 … FR-011) ──

    /// (a) Evaluate to a three-valued `Verdict` using Kleene composition, REUSING the F02
    /// combinators (FR-006): `Atom`/`Opaque` map their outcome (met→Pass, unmet→Fail,
    /// unknown→Uncertain); `All`→`Verdict.all`; `Any`→`Verdict.any`; `Not`→`Verdict.negate`;
    /// `Implies (a,b)`→ eval of `Any [Not a; b]`. Undecided sub-results are preserved
    /// unless a dominating result is present (SC-003). Empty `All`→`Pass`, empty
    /// `Any`→`Fail ""` (inherited from F02). Total for every check (FR-013).
    val eval: facts: FactSet<'fact> -> check: Check<'fact> -> Verdict

    /// (b) Render to a deterministic, human- and agent-readable string using ONLY the
    /// check's structure (probe names, declared args, inputs) — WITHOUT executing any
    /// probe `Eval` and without a fact set (FR-007, SC-001). This is the source the F06
    /// published contract folds, so it cannot drift from what is enforced. Preserves
    /// authoring order (only `hash` canonicalizes commutative order).
    val render: check: Check<'fact> -> string

    /// (c) Fold to a stable structural key WITHOUT executing any probe `Eval` (FR-008,
    /// SC-001/SC-002). Commutative nodes (`All`/`Any`) are CANONICALIZED — reordering
    /// their members does not change the hash (closes Hazard 3, so the F04 agent-review
    /// cache does not miss spuriously). Positional structure is PRESERVED — the two sides
    /// of `Implies` and a probe's ordered `Args`/`Reads` are reflected in the key, so
    /// meaningful reordering does change it. `Opaque` contributes its NAME only (its
    /// un-inspectable function is never part of the key). The exact encoding (a SHA-256
    /// hex digest) is an implementation detail; the contract is stability + the
    /// commutative/positional split.
    val hash: check: Check<'fact> -> string

    /// (d) Fold over facts to a structured `Explanation` proof tree mirroring the check's
    /// surface shape and recording each atomic probe's met/unmet/unknown outcome. Its
    /// overall verdict is identical to `eval` for the same check and facts (FR-009,
    /// SC-004) — feeds the kernel provenance (F01) and the F06 JSON explanation.
    val explain: facts: FactSet<'fact> -> check: Check<'fact> -> Explanation

    /// (e) Collect the artifact references the check declares it reads (from its atomic
    /// probes), for routing and the artifact half of the agent-review cache key (FR-010).
    /// An `Opaque` node contributes none (it declares no inspectable structure).
    val reads: check: Check<'fact> -> ArtifactRef list

    /// (f) True iff EVERY node is structural (no `Opaque` anywhere); false if the check
    /// contains at least one `Opaque` node (FR-011, SC-005). The F04 rule builder uses
    /// this to refuse the Deterministic tier for an opaque check.
    val isReified: check: Check<'fact> -> bool
