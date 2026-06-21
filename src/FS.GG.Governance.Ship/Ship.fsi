// CONTRACT DRAFT (Phase 1) for specs/024-ship-verdict-rollup.
// The shipped Ship.fsi (in src/FS.GG.Governance.Ship/) is authored from this draft and is the SOLE
// declaration of the module's public surface (Constitution Principle II). The matching Ship.fs carries
// NO `private`/`internal`/`public` modifiers — the gate/finding -> EnforcementInput mappings and the
// sort-key helper live ONLY in the .fs and are absent here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any Ship.fs body
// exists (Principle I). `rollup` is PURE and TOTAL (FR-008): no I/O, no git, no clock, no policy.yml
// parsing, never throws, byte-for-byte identical for identical input (FR-009, SC-004). It REUSES F023
// `deriveEffectiveSeverity` for every per-item decision (FR-003) and the F019 `RouteResult` verbatim;
// it re-derives, re-sorts, and re-classifies nothing those cores fixed. It renders NO audit.json (or
// other) document, runs NO command, sets NO process exit code (only a typed `ExitCodeBasis`), and
// evaluates NO cache/freshness (FR-012) — those are the later projection row, the `fsgg ship` host row,
// and Phase 11.

namespace FS.GG.Governance.Ship

open FS.GG.Governance.Route.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Ship.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Ship =

    /// Roll a routed change up into one whole-change ship decision.
    ///
    /// Inputs (all already typed by upstream rows — none recomputed here):
    ///   • `route`   — the F019 `RouteResult`: the distinct selected gates (already union-deduped by
    ///                 `GateId`), the carried F017 `FindingReport`, and the cost rollup. Consumed
    ///                 verbatim; `route.Cost` is carried but NOT evaluated here.
    ///   • `mode`    — the F023 run `RunMode` chosen for this rollup (one mode for the whole change).
    ///   • `profile` — the F023 `Profile` chosen for this rollup (one profile for the whole change).
    ///
    /// Per-item enforcement (FR-003, FR-013):
    ///   • Each selected gate becomes an `EnforcementInput` — base `Blocking` iff its `Maturity` is a
    ///     `block-on-*`, else base `Advisory`; maturity passed verbatim (research D3).
    ///   • Each finding becomes an `EnforcementInput` — `ProtectedBoundaryUnknown` -> base `Blocking`
    ///     with maturity-equivalent `block-on-ship`; `GovernedRootUnknown` -> base `Advisory` with
    ///     `warn` (research D4).
    ///   • `deriveEffectiveSeverity` decides each item's effective severity and reason — reused, not
    ///     re-implemented. Every distinct gate and every finding is evaluated exactly once; nothing is
    ///     dropped (FR-010, SC-006).
    ///
    /// Rollup (FR-002, FR-004, FR-007):
    ///   • `Blockers` = effective-`Blocking` items; `Warnings` = base-`Blocking`-relaxed-to-`Advisory`
    ///     items; `Passing` = base-`Advisory` items. The three partition all N+M items.
    ///   • `Verdict` = `Fail` iff `Blockers` is non-empty, else `Pass`.
    ///   • `ExitCodeBasis` = `Blocked` iff `Verdict = Fail`, else `Clean`.
    ///   • Every item carries its full F023 decision (base severity echoed unchanged — FR-005, FR-006).
    ///
    /// Determinism (FR-009, SC-004): each list is sorted by a stable composite per-item key (gates
    /// before findings, gates by `GateId`, findings by `(Path, finding-id token)`); re-ordering the
    /// input gates/findings does not change the result. No clock, environment, host path, or
    /// input-arrival influence.
    ///
    /// Totality (FR-008, SC-005): defined for EVERY combination of selected-gate maturities, finding
    /// zones, run mode, and profile — including the empty `RouteResult` (yields `Pass`, empty lists,
    /// `Clean`). Never throws. Pure: no I/O, git, clock, JSON, command, exit code, or cache/freshness
    /// (FR-008, FR-012).
    val rollup: route: RouteResult -> mode: RunMode -> profile: Profile -> ShipDecision
