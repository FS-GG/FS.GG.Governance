// Curated public signature contract for the edge interpreter of the effects shell (F08).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution
// Principle II). The matching Interpreter.fs carries NO `private`/`internal`/`public`
// modifiers on top-level bindings — visibility is presence/absence here.
//
// Design-first artifact: this contract is drafted and exercised in FSI
// (scripts/prelude.fsx) before any Interpreter.fs body exists (Principle I).
//
// This is the EDGE side of the Elmish/MVU boundary (Constitution Principle IV): the ONLY
// impure code in the feature. It executes the `Effect` values the pure `Loop.update`
// requests against INJECTED ports, reifies EVERY result — including every failure — as a
// `Msg`, and feeds it back into `update`, driving the loop from `init` to QUIESCENCE
// (FR-004, FR-016). The judge and store are injected, so the whole loop runs against a REAL
// filesystem fixture with a FAKE judge and NO real network or agent (FR-017, SC-009). The
// interpreter NEVER throws out of itself: a port that returns `Error` or throws becomes the
// matching failure `Msg` (FR-012, SC-006). Reuses the pure surface in Loop.fsi and the
// kernel (F01–F07). Zero new dependency (BCL `System.IO`/`System.Text.Json` only).

namespace FS.GG.Governance.Host

open FS.GG.Governance.Kernel

/// The injected SENSING port: read a governed artifact's content. Returns `Ok content` or
/// `Error reason`; the interpreter ALSO guards against a thrown exception (a real
/// `File.ReadAllText` throws on a missing file), converting either to a `Sensed` failure
/// `Msg` → `ArtifactUnavailable` (FR-004/FR-012). Tests back it with a REAL temp-directory
/// filesystem tree (Principle V).
type ArtifactReader = ArtifactRef -> Result<string, string>

/// The injected AI-JUDGE port (FR-007/FR-017): given an ISOLATED `ReviewTask` (the rule's
/// instruction SEPARATE from the untrusted artifact data — decision #3), return ONE sample
/// verdict (`Ok`) or a failure (`Error`). The interpreter calls it once per requested sample
/// (`ReviewDispatch.Samples`) and gathers the results into a single `Reviewed` `Msg`.
/// Injected so the loop runs with a FAKE judge and NO real agent/network (SC-009); a real
/// agent cannot be a reproducible test oracle (spec Assumptions, Principle V).
type Judge = ReviewTask -> Result<JudgeVerdict, string>

/// The injected REVIEW STORE (FR-007): `Load` a frozen verdict by its F04 content-hash cache
/// key (a HIT short-circuits dispatch — FR-008), and `Save` a newly frozen one. Either may
/// return `Error` or throw; the interpreter reifies both as the corresponding failure `Msg`
/// → `ReviewStoreUnavailable` (FR-012). Tests back it with a REAL local-filesystem store
/// under a temp directory (Principle V).
type ReviewStore =
    { Load: string -> Result<RecordedReview option, string>
      Save: RecordedReview -> Result<unit, string> }

/// The injected SINK for the F06 edge outputs (FR-015): the interpreter hands it each
/// `Output` value (the JSON explanation, the JSON contract, the rendered route) to persist
/// or print. Side-effecting at the edge only; tests capture the emitted values.
type OutputSink = Output -> unit

/// The bundle of injected edge ports — everything impure the loop touches (FR-017). Wholly
/// faked/realised in tests (a temp-dir `Read`, a fake `Judge`, a real-fs `Store`, a
/// capturing `Sink`) so no real network or agent is ever reached (SC-009).
type Ports =
    { Read: ArtifactReader
      Judge: Judge
      Store: ReviewStore
      Sink: OutputSink }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =

    /// Execute a SINGLE `Effect` against the ports and return the result `Msg`(s) it produces
    /// (FR-004) — `ReadArtifact`→`Sensed`, `LoadReview`→`Loaded`, `DispatchReview`→`Reviewed`
    /// (drawing `Samples` from the judge), `RecordVerdict`→`Recorded`, `EmitOutput`→the sink
    /// (no `Msg`, returns `[]`). EVERY failure — an `Error` from a port OR a thrown exception —
    /// is CAUGHT and reified as the matching failure `Msg`; this function NEVER throws (FR-012,
    /// SC-006). It performs the I/O the pure `update` only requested; it makes no decisions.
    val step: ports: Ports -> effect: Effect -> Msg<'fact> list

    /// Drive the loop from `Loop.init` to QUIESCENCE (FR-004, FR-016): execute every requested
    /// `Effect` via `step`, feed each result `Msg` back into the pure `Loop.update`, and repeat
    /// until `update` emits no further effects. The returned `Model` carries the final fact set
    /// (equal to what the pure kernel yields over the same sensed facts — SC-002), the acted-on
    /// `Route`, the disclosures, and any failures.
    ///
    /// The final `Model` is INDEPENDENT of the order in which independent effects complete and
    /// of duplicate result delivery (FR-014, SC-007). A recorded verdict matching the cache key
    /// makes the re-run dispatch ZERO reviews (FR-008, SC-003). Blocking gates are enforced
    /// only at `config.Mode = Gate`, recomputed from the base fences/rules (FR-011, SC-008).
    /// TOTAL: returns a well-formed `Model` even when every effect fails (no driven input makes
    /// it throw or reach a malformed `Model` — FR-012, SC-006); reaches NO real network/agent
    /// (the ports are injected — FR-017, SC-009).
    val run:
        ports: Ports ->
        config: LoopConfig<'change, 'fact> ->
        change: 'change ->
            Model<'fact>
