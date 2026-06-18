// Curated public signature contract for routing, stakes & run modes (F07).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution
// Principle II). The matching Route.fs carries NO `private`/`internal`/`public`
// modifiers on top-level bindings — visibility is presence/absence here.
//
// Design-first artifact: this contract is drafted and exercised in FSI
// (scripts/prelude.fsx) before any Route.fs body exists (Principle I). The shapes
// mirror docs/governance-design/routing-and-modes.md ("Light by default", "Run modes",
// "How a route is computed") and CLOSE hazard 5 / reinforce decision #4: stakes
// combination is deterministic by PRECEDENCE (forbid trumps permit), NEVER positional —
// the design-doc `stakesOf` sketch used `List.tryFind` (first-match = positional); this
// surface deliberately strengthens it to order-independence (spec FR-005, SC-003).
//
// This is the light routing layer: it sits ABOVE the F04 `CheckRule` bridge and decides,
// for an abstract change, WHICH requirements apply, WHETHER they advise or block, and WHY.
// It performs NO I/O and runs NO probe or agent review — it operates over DECLARED fences
// and an ABSTRACT change, producing a `Route` as a pure value. Executing that route
// (sensing facts, running probes, dispatching reviews, logging disclosures) is the F08
// effects interpreter's job (FR-016). Reuses F04 `CheckRule`/`Severity`, F06
// `ContractEntry`/`Contract.ofRules` (so a gate's statement IS `Check.render` — drift-proof,
// FR-012), F01 `RuleId`, and through them F03 `Check`/F02 `Verdict`. Zero new dependencies.

namespace FS.GG.Governance.Kernel

/// The risk classification of a change — the dimension that decides HOW MUCH proof a
/// change must earn. `Routine` is the light default (no gates); `Fenced` is high-stakes,
/// carrying the name/reason of the boundary(ies) that were tripped (FR-002). Orthogonal to
/// `Severity` (a property of a rule) and to `RunMode` (the lifecycle position).
type Stakes =
    /// The light default: no declared fence matched, so the change is routed with no
    /// blocking gates regardless of run mode (FR-006).
    | Routine
    /// High-stakes: at least one declared fence tripped. Carries the tripped fence
    /// name(s) — de-duplicated, ordinal-sorted, and joined with the reserved `"; "`
    /// separator, so the value is a function of the SET of tripped fences and is therefore
    /// identical under any ordering of the fence list (FR-005, SC-002/SC-003).
    | Fenced of name: string

/// A named, DECLARED classifier that decides whether a given change trips a high-stakes
/// boundary (e.g. "touches the merge boundary", "edits security-sensitive surface"). Stakes
/// are raised by declared fences, never hard-coded (FR-003). The change is an abstract
/// `'change` supplied by the caller/adapter — the kernel routes over it WITHOUT assuming any
/// repository, file, or artifact shape, so `ChangeSet` (spec) is realised AS this type
/// parameter, the most domain-neutral encoding and the house style of the kernel's other
/// generic surfaces (`Check<'fact>`, `EvidenceGraph<'id>`) (FR-001, FR-017). `Trips` is a
/// pure predicate; it is only ever RUN, never rendered or hashed.
type Fence<'change> =
    { Name: string
      Trips: 'change -> bool }

/// The lifecycle position at which a change is evaluated — the dial that decides WHEN a
/// stake is enforced (advisory vs blocking), orthogonal to WHETHER the change is high-stakes
/// (FR-007). A blocking-severity requirement on a `Fenced` change surfaces as advisory in
/// `Sandbox`/`Inner` and as a blocking gate only in `Gate` (FR-008); the `Stakes`
/// classification itself is identical across all three (FR-009).
type RunMode =
    /// Free experimentation: governance is advisory only (never a merge basis).
    | Sandbox
    /// The local development loop: fast, advisory only — nothing blocks.
    | Inner
    /// The merge boundary: blocking-severity requirements on a fenced change are enforced.
    | Gate

/// The routing decision for a (fences, applicable rules, run mode, change) input — the
/// change's `Stakes`, the applicable requirements partitioned into `Advisory` and `Blocking`
/// gates, and a mandatory non-empty `Reason` (FR-010, FR-011). Non-generic: it drops both
/// `'change` and `'fact`, carrying only domain-neutral `ContractEntry` requirements (reused
/// from F06 — each entry's `Statement` IS `Check.render` of the rule's check, so a gate
/// names the rule, severity, spec source, and the EXACT rendered check with no drift,
/// FR-012). `Blocking` is the short, filterable subset bounded by the applicable rules, not
/// the catalog (the caller passes only the rules that apply to the change) (FR-013).
type Route =
    { Stakes: Stakes
      Advisory: ContractEntry list
      Blocking: ContractEntry list
      Reason: string }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Route =

    /// Classify a change against a set of declared fences by DETERMINISTIC PRECEDENCE —
    /// forbid trumps permit (FR-004, FR-005). `Fenced` if ANY fence trips (a single match is
    /// sufficient), carrying the tripped fence names de-duplicated, ordinal-sorted, and
    /// `"; "`-joined; `Routine` if none trips (and for the empty fence set). Because the
    /// carried name is a function of the SET of tripped fences, the result is IDENTICAL under
    /// any permutation of `fences` — combination is never positional (closes hazard 5 /
    /// decision #4, SC-002/SC-003). Pure and total: runs each fence's `Trips` predicate and
    /// nothing else — no I/O, no probe, no agent (FR-016). The classification is independent
    /// of `RunMode` (FR-009).
    val stakesOf: fences: Fence<'change> list -> change: 'change -> Stakes

    /// Route a change: compute its `Stakes` (`stakesOf fences change`), fold the APPLICABLE
    /// rules into drift-proof `ContractEntry` requirements (via F06 `Contract.ofRules`, so
    /// each `Statement` IS `Check.render`), and partition them. A requirement is a BLOCKING
    /// gate iff its `Severity` is `Blocking` AND the change is `Fenced` AND `mode` is `Gate`;
    /// every other requirement is `Advisory` (FR-006, FR-008). The route always carries a
    /// non-empty `Reason` naming the stakes, the run mode, and the outcome (FR-011). Entries
    /// stay in catalog order; the partition depends on the stakes/mode, not on fence order,
    /// so the whole route is identical under any permutation of `fences` and byte-for-byte
    /// reproducible across repeated evaluation (FR-015, SC-008). `rules` are the rules that
    /// already apply to this change — selecting them (e.g. by declared `Check.reads`
    /// intersecting the change) is the adapter's domain-specific job, kept out of the
    /// domain-neutral kernel (FR-017); this keeps `Blocking` short (FR-013, SC-007). Pure and
    /// TOTAL for every input, including the empty fence set and the empty rule set (FR-015,
    /// SC-009); runs no probe and dispatches no review (FR-016, SC-010).
    val route:
        fences: Fence<'change> list ->
        rules: CheckRule<'fact> list ->
        mode: RunMode ->
        change: 'change ->
            Route

    /// Render a route as a deterministic, human- and agent-readable explanation naming the
    /// stakes, the reason, and each applicable requirement (blocking gates then advisory),
    /// each line naming its rule id, severity, spec source, and rendered statement (FR-012,
    /// FR-014). Deterministic (stable line/field order) and total — a `Routine` route with no
    /// requirements still renders a non-empty block (the stakes + reason), so there is no
    /// route without an explanation (FR-011, SC-005). EXECUTION-FREE: it folds the route
    /// value only and runs no probe and dispatches no review (FR-014, SC-010).
    val renderRoute: route: Route -> string
