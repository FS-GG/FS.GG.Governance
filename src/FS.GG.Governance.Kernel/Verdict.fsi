// Curated public signature contract for the three-valued verdict algebra (F02).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution
// Principle II). The matching Verdict.fs carries NO `private`/`internal`/`public`
// modifiers on top-level bindings — visibility is presence/absence here.
//
// Design-first artifact: this contract is drafted and exercised in FSI
// (scripts/prelude.fsx) before any Verdict.fs body exists (Principle I). The shapes
// mirror docs/governance-design/kernel.md ("Verdicts") and the Hazard-2 reason
// mitigation in docs/governance-design/theory-and-composition.md (the authoritative
// model). The algebra lives IN the kernel assembly (roadmap §3) so the F03 `Check`
// interpreters reuse it with zero new dependencies.

namespace FS.GG.Governance.Kernel

/// A three-valued (Kleene "strong") judgement. The central distinction of the whole
/// system: `Uncertain` ("a competent judge has not yet ruled") is NOT `Fail` —
/// routing later turns the former into a review request and the latter into a block.
///
///   pass=true · fail=false · uncertain=unknown
///
/// `Pass` carries no reason; `Fail`/`Uncertain` carry an opaque, caller-supplied
/// free-text reason. The algebra combines reasons deterministically but never
/// interprets their meaning (FR-010).
///
/// RESERVED SEPARATOR — `"; "` is reserved inside a `Fail`/`Uncertain` reason: aggregation
/// (`all`/`any`, via `combineReasons`) splits reasons on it, so a leaf reason containing
/// `"; "` is fragmented and reordered on roll-up. Authors of leaf verdicts (probe reasons at
/// `Check.Outcome`) must not embed `"; "` in a single reason. See `combineReasons`.
type Verdict =
    | Pass
    | Fail of reason: string
    | Uncertain of reason: string

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Verdict =

    /// Conjunction — "all of these must hold". Kleene semantics (FR-002):
    ///   any `Fail`  ⇒ `Fail`  · else any `Uncertain` ⇒ `Uncertain` · else `Pass`.
    /// The empty list is the identity `Pass` (FR-009). Commutative and associative
    /// in the outcome (FR-005); the combined reason is order-independent (FR-006).
    ///
    /// Combined reason (the dominating kind only): the contributing reasons are split
    /// on the reserved `"; "` separator into components, de-duplicated, ordinal-sorted,
    /// and re-joined with `"; "`. This makes the reason a function of the *set* of
    /// reason components — identical under reordering, re-nesting, and duplication
    /// (FR-006, US2). `Pass` contributes no reason.
    val all: verdicts: Verdict list -> Verdict

    /// Combine a list of `"; "`-delimited reason strings into one order-, nesting-, and
    /// duplication-independent reason: split each on the reserved `"; "` separator (dropping empties),
    /// de-duplicate, ordinal-sort (culture-invariant, byte-stable), and re-join with `"; "`. Exported
    /// (111/A6) so `Route.stakesOf` reuses this exact convention over tripped-fence names instead of
    /// re-spelling the pipeline.
    val combineReasons: reasons: string list -> string

    /// Disjunction — "at least one of these must hold". Kleene semantics (FR-003):
    ///   any `Pass`  ⇒ `Pass`  · else any `Uncertain` ⇒ `Uncertain` · else `Fail`.
    /// The empty list is the identity `Fail ""` (FR-009: nothing satisfied it; the
    /// empty reason is absorbed by the same component normalisation as `all`).
    /// Commutative and associative in the outcome (FR-005); reason order-independent.
    val any: verdicts: Verdict list -> Verdict

    /// Polarity flip (FR-004): swaps the pass/fail *tag* (`Pass` ⇄ `Fail`) and
    /// leaves `Uncertain` unchanged (an unresolved judgement has no definite
    /// polarity to flip). Reasons are NOT carried across the flip: `negate Pass =
    /// Fail ""` (the empty reason) and `negate (Fail _) = Pass` (the fail reason is
    /// dropped). `negate` is therefore an involution on the **tags** — and a full
    /// involution (`negate (negate v) = v`) exactly for `Uncertain` and for any
    /// `""`-reasoned `Pass`/`Fail`; for a non-empty-reasoned `Fail r` only the tag
    /// recovers (`negate (negate (Fail r)) = Fail ""`). Backs `Not`/`Implies` at F03,
    /// which always negates an *evaluated* sub-verdict, so the tag-level guarantee is
    /// exactly what it needs.
    val negate: verdict: Verdict -> Verdict
