// Curated public signature contract for the Evidence model & synthetic taint (F05).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution
// Principle II). The matching Evidence.fs carries NO `private`/`internal`/`public`
// modifiers on top-level bindings — visibility is presence/absence here.
//
// Design-first artifact: this contract is drafted and exercised in FSI
// (scripts/prelude.fsx) before any Evidence.fs body exists (Principle I). The shapes
// mirror docs/governance-design/kernel.md ("The evidence model") — the six-case
// `EvidenceState`, the transitive `effective(t)` taint rule, and decision #4 (the
// dependency structure is a DAG; cycles are rejected). It REINFORCES that standing
// precondition so the taint closure is a well-defined, terminating least-fixed-point.
//
// This is the orthogonal evidence dimension of the kernel: separate from a verdict
// (F02 — *whether* a conclusion holds), this tracks *how trustworthy the evidence for
// it is*. It performs NO I/O and reads NO real artifacts — it operates over DECLARED
// evidence states supplied to it. Discovering a node's true declared state from the
// filesystem/git, the disclosure logging around a bypass, and the freshness/explanation
// rendering are the edge interpreter's job (F08) and F06, NOT this feature. Node
// identity is generic (`'id`, carrying no domain vocabulary), so the same model serves
// software, research, and writing alike, and the F10 dogfood adapter's `TaskDependsOn`
// graph runs through THIS model rather than a bespoke engine. Zero new dependencies
// (FSharp.Core `Map`/`Set` only).

namespace FS.GG.Governance.Kernel

/// The quality of the evidence behind a node — the dimension the kernel tracks
/// orthogonally to a verdict. EXACTLY six cases (FR-001). Five of them are AUTHORED
/// (declared by a consumer); `AutoSynthetic` is COMPUTED-ONLY — produced solely by the
/// `effective` taint closure, never a valid declared input (FR-002). The bracketed
/// markers are the docs/governance-design/kernel.md mnemonics.
type EvidenceState =
    /// `[ ]` Not started.
    | Pending
    /// `[X]` Done, backed by real evidence.
    | Real
    /// `[S]` Done, but only on synthetic / placeholder evidence — the ROOT CAUSE of a
    /// taint. Declared at its source; reported verbatim (never re-labelled), so a taint's
    /// origin always stays distinguishable from its inheritance (FR-008).
    | Synthetic
    /// `[F]` Failed.
    | Failed
    /// `[-]` Skipped (with a written rationale held by the consumer, not the kernel).
    | Skipped
    /// `[S*]` COMPUTED, never declared: a `Real` node that rests — directly or
    /// transitively — on a `Synthetic`/`AutoSynthetic` node. Produced only by `effective`
    /// (FR-002); the `build` constructor REFUSES a node declared `AutoSynthetic`.
    | AutoSynthetic

/// Why constructing an `EvidenceGraph` was refused — the validity guarantees `build`
/// upholds so that `effective` is a well-defined, terminating, total least-fixed-point.
/// `build` returns the FIRST violation it finds, in the precedence documented on `build`.
type GraphError<'id> =
    /// The dependency declaration contains a cycle (a self-dependency or a loop through
    /// two or more nodes). `cycle` witnesses one such loop. The structure MUST be a DAG
    /// (FR-004, decision #4) — a cyclic graph is never produced and never evaluated.
    | Cycle of cycle: 'id list
    /// A dependency edge names a node that was not declared in `nodes`. Rejected so every
    /// node `effective` folds over has a declared state (totality, FR-011).
    | UnknownNode of node: 'id
    /// A node was declared `AutoSynthetic`, which is computed-only and never a valid
    /// authored input (FR-002, SC-006).
    | AutoSyntheticDeclared of node: 'id

/// The acyclic dependency graph of evidence nodes — who rests on whom. ABSTRACT by
/// design: it has no public constructor, so the ONLY way to obtain one is through `build`,
/// which validates acyclicity (and the other `GraphError` conditions). This makes the DAG
/// invariant unforgeable — a cyclic or `AutoSynthetic`-declaring graph is unrepresentable,
/// so `effective` is guaranteed to terminate and be total (FR-004, FR-011). Node identity
/// is generic and domain-neutral (FR-012); `comparison` lets the closure key its result
/// and detect cycles with the standard `Map`/`Set`.
[<Sealed>]
type EvidenceGraph<'id when 'id: comparison>

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Evidence =

    /// Construct an evidence graph from declared nodes and directed dependency edges.
    /// Each `(id, state)` pair declares a node's identity and its DECLARED `EvidenceState`;
    /// each edge `(a, b)` means "node `a` depends on (rests on) node `b`" (FR-003). Returns
    /// `Ok graph` for a valid DAG, or `Error` for the FIRST violation in this precedence:
    ///   1. `AutoSyntheticDeclared id` — any node declared `AutoSynthetic` (FR-002);
    ///   2. `UnknownNode id` — any edge endpoint not present in `nodes`;
    ///   3. `Cycle path` — any self-dependency or multi-node loop (FR-004, decision #4).
    /// A node id repeated in `nodes` keeps its LAST declaration (the nodes form a map).
    /// Total: every input either yields `Ok` or one of the three `Error`s (FR-011).
    val build:
        nodes: ('id * EvidenceState) list ->
        dependencies: ('id * 'id) list ->
            Result<EvidenceGraph<'id>, GraphError<'id>>
            when 'id: comparison

    /// The graph's declared nodes and their DECLARED states (the inputs to `build`,
    /// de-duplicated by id). Lets a consumer inspect the abstract graph; ordering is
    /// by id (deterministic).
    val nodes: graph: EvidenceGraph<'id> -> ('id * EvidenceState) list when 'id: comparison

    /// The graph's directed dependency edges `(dependent, dependency)`, de-duplicated and
    /// ordered deterministically. Lets a consumer inspect the abstract graph.
    val dependencies: graph: EvidenceGraph<'id> -> ('id * 'id) list when 'id: comparison

    /// Compute every node's EFFECTIVE evidence state — the transitive synthetic-taint
    /// closure (FR-005, docs/governance-design/kernel.md `effective(t)`):
    ///   • a node declared `Synthetic` is `Synthetic` (the root cause — never `AutoSynthetic`);
    ///   • a node declared `Real` with any dependency whose EFFECTIVE state is `Synthetic`
    ///     or `AutoSynthetic` (directly or transitively) is `AutoSynthetic` (FR-006);
    ///   • every other node keeps its declared state — `Pending`/`Failed`/`Skipped` are
    ///     NEVER upgraded, even when they rest on synthetic evidence (FR-007).
    /// The taint flows the full transitive depth and is idempotent over diamonds (a node
    /// reachable to a synthetic root by many paths is tainted once). Because the graph is a
    /// DAG (cycles rejected by `build`), the closure is a DETERMINISTIC least-fixed-point —
    /// a pure function of the current declared states and edges, independent of node/edge
    /// order, carrying no hidden history, so re-declaring a `Synthetic` root as `Real` and
    /// recomputing CLEARS the taint everywhere it had flowed with no other action (FR-009,
    /// FR-010). TOTAL over every valid graph, including the empty graph (which yields the
    /// empty map) (FR-011). Performs no I/O (FR-013).
    val effective: graph: EvidenceGraph<'id> -> Map<'id, EvidenceState> when 'id: comparison
