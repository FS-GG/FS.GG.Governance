// Curated public signature contract for the gate-registry assembler entry point (F018).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II).
// The matching Gates.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings —
// visibility is presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any Gates.fs body
// exists (Principle I). `buildRegistry` is PURE and TOTAL (FR-013, FR-007): no I/O, no git, no clock,
// never throws, and byte-for-byte identical for identical input (FR-011, SC-003). It consumes the
// already-typed, already-validated F014 facts; it re-parses no `.fsgg` YAML, re-validates no catalog,
// routes no globs, and senses no git (FR-013, FR-016). It is the Phase-2 *Gate identities* row: it
// establishes the stable gate identities the later `route`/`ship`, route/audit JSON, and `gates.json`
// rows consume — but it selects, runs, and enforces nothing (FR-015).

namespace FS.GG.Governance.Gates

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Gates =

    /// The documented default gate timeout used when a check references no declared command (and so
    /// declares no explicit timeout). Five minutes. A gate ALWAYS carries a bounded timeout — never
    /// zero or unbounded (FR-010, SC-005). The feature only CARRIES this value; it never enforces or
    /// measures a timeout.
    val defaultTimeout: TimeoutLimit

    /// Assemble the typed gate registry from the declared facts.
    ///
    /// Input: the F014 `TypedFacts`. Only `Capabilities.Checks` (the gates' source) and, when
    /// `Tooling = Some`, `Tooling.Commands` (for each referenced command's declared timeout) are read;
    /// everything else is ignored. `TypedFacts.Tooling` is optional — when `None` (no `tooling.yml`)
    /// the command-timeout index is empty and every gate takes `defaultTimeout`. The facts are assumed `Valid` — F014's `Schema.validate` has already proven unique
    /// check ids and resolved cross-references (`Check.Command` resolves even when `tooling.yml` is
    /// absent), so this function RE-VALIDATES NOTHING; it PRESERVES those guarantees by construction
    /// (research D4) and never emits a diagnostic.
    ///
    /// Projection — one `Gate` per declared `Check`:
    ///   • `Id`            = `GateId "<domain>:<checkId>"` (injective over distinct checks, FR-003/FR-005);
    ///   • `Domain`/`Cost`/`Owner`/`Maturity` = the declared `Check` fields, verbatim;
    ///   • `Description`    = a human-readable purpose composed from the declared ids (no raw YAML);
    ///   • `Prerequisites`  = `[RequiresCommand c]` when `Check.Command = Some c`, else `[]`
    ///                        (the declared fact prerequisite; gate-to-gate prereqs are Phase 10, D5);
    ///   • `Timeout`        = the referenced command's `CommandSpec.Timeout` when found, else
    ///                        `defaultTimeout` (including when `Tooling = None`) (D9);
    ///   • `ProductCheck`   = `Check.Environment = Release` (MVP heuristic, D6);
    ///   • `FreshnessKey`   = the declared identity inputs `{Check; Domain; Cost; Environment; Command}`
    ///                        — carried, NOT evaluated (D8, FR-009).
    ///
    /// Determinism (FR-011, FR-012, SC-003/SC-006): `Gates` is sorted by `GateId` ordinal; re-ordering
    /// the input checks or commands does not change the result. The gate dependency graph is trivially
    /// acyclic in this MVP (no gate-to-gate edges), so the order is the `GateId` sort and no topological
    /// pass is needed (D5/D7).
    ///
    /// Totality (FR-007, FR-014): succeeds over any `Valid TypedFacts` — no error, no partial result.
    /// An empty `Capabilities.Checks` yields an empty `GateRegistry`, a valid success. Pure: no I/O,
    /// git, clock, severity, enforcement, freshness, selection, execution, JSON, or CLI (FR-013, FR-015).
    val buildRegistry: facts: TypedFacts -> GateRegistry
