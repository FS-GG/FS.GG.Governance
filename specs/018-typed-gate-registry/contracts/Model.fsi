// Curated public signature contract for the gate-domain types of the typed gate registry (F018).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II).
// The matching Model.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings —
// visibility is presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any Model.fs body
// exists (Principle I). These are the product-neutral, YAML-free values the `Gates.buildRegistry`
// projection returns: one typed `Gate` per declared capability check, carrying the stable identity
// and metadata the design's *Gate identities* table fixes. They REUSE the F014 typed-fact newtypes
// (`DomainId`, `Owner`, `Cost`, `Maturity`, `TimeoutLimit`, `CommandId`, `EnvironmentClass`,
// `CheckId`) rather than redefining them — this feature consumes the already-validated facts, it
// re-parses no YAML and re-validates no catalog (FR-004, FR-013). Every emitted collection is in
// deterministic ordinal order (FR-011, SC-003/SC-006). No field carries raw YAML, host paths, or
// product vocabulary beyond declared ids (FR-004).

namespace FS.GG.Governance.Gates

open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    // ── Stable gate identity (FR-002, FR-003, FR-005) ──

    /// The stable machine id of a gate — the domain-qualified check id `"<domain>:<checkId>"` — used
    /// by route, evidence, and audit JSON to refer to the gate across runs, machines, and tools. A
    /// deterministic, INJECTIVE function of the declared facts: distinct checks yield distinct ids, so
    /// no two gates collide and none is dropped or merged (research D3). Never positional,
    /// time-derived, or random.
    type GateId = GateId of string

    // ── Prerequisites (FR-006, research D5) ──

    /// Something required before a gate runs. In this MVP the ONLY source is the check's declared
    /// command — a genuine fact prerequisite ("this gate cannot run until command `c` is available")
    /// that F014 has already proven resolvable, so it never dangles. Gate-to-gate prerequisites are
    /// NOT declarable in F014's MVP schema; they are the documented Phase-10 extension point and no
    /// gate-to-gate edge is produced here. Closed so tests assert exactly the cases that exist.
    type GatePrerequisite = RequiresCommand of command: CommandId

    // ── Freshness key (FR-009, research D8) ──

    /// The declared inputs a LATER freshness/cache step (kernel `Freshness`, Phase 11) will use to
    /// decide whether prior evidence can be reused. CARRIED by every gate, never evaluated here: this
    /// feature computes no freshness verdict, compares no instants, caches nothing, and reads no clock
    /// (FR-009, SC-004/SC-007). The MVP key is the always-available declared identity; Phase 11 extends
    /// it with rule/artifact hashes, command version, and base/head. Ids only — no raw YAML, no clock.
    type FreshnessKey =
        { Check: CheckId
          Domain: DomainId
          Cost: Cost
          Environment: EnvironmentClass
          Command: CommandId option }

    // ── The gate (key entity "Gate", FR-001/FR-002) ──

    /// The typed identity of ONE unit of governance, projected from one declared capability `Check`.
    /// Carries the full *Gate identities* field set:
    ///   • `Id`           — the stable `GateId` (domain-qualified check id).
    ///   • `Domain`       — the owning capability domain (from `Check.Domain`).
    ///   • `Description`   — a human-readable purpose composed from the declared ids (no raw YAML).
    ///   • `Prerequisites`— declared `RequiresCommand` references (empty when the check has no command).
    ///   • `Cost`         — the declared `Check.Cost`.
    ///   • `Timeout`      — the referenced command's declared timeout, else the documented default
    ///                      (`Gates.defaultTimeout`); never zero or unbounded (FR-010).
    ///   • `Owner`        — the declared `Check.Owner` (failure owner).
    ///   • `Maturity`     — the declared `Check.Maturity`, VERBATIM; not translated to enforcement
    ///                      (that is Phase 5).
    ///   • `ProductCheck` — MVP heuristic: `true` iff `Check.Environment = Release` (research D6);
    ///                      richer product-domain tagging is Phase 10.
    ///   • `FreshnessKey` — the carried declared-input key (above).
    /// A pure, deterministic value — declared ids only (FR-004).
    type Gate =
        { Id: GateId
          Domain: DomainId
          Description: string
          Prerequisites: GatePrerequisite list
          Cost: Cost
          Timeout: TimeoutLimit
          Owner: Owner
          Maturity: Maturity
          ProductCheck: bool
          FreshnessKey: FreshnessKey }

    // ── The aggregate result (FR-001, FR-007, FR-014) ──

    /// The deterministic gate registry: one `Gate` per declared check, sorted by `GateId` ordinal so
    /// identical facts yield a byte-identical list and re-ordering the inputs never changes it
    /// (FR-011, FR-012, SC-003/SC-006). Assembly is TOTAL over `Valid TypedFacts`: there is no
    /// diagnostic channel and no failure mode — F014 already proved the facts consistent, and this
    /// registry PRESERVES that consistency by construction (research D4). An EMPTY list is a valid,
    /// successful outcome (no declared checks), never an error (FR-014).
    type GateRegistry = { Gates: Gate list }

    // ── Stable rendering of a gate id (for messages, tests, and any later JSON) ──

    /// The stable wire string of a `GateId` (e.g. `GateId "build:tests"` → `"build:tests"`).
    /// Deterministic and total.
    val gateIdValue: id: GateId -> string
