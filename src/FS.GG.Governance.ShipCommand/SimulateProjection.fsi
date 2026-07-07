// Curated public signature contract for the PURE dry-run projections (112, US1/US3).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II).
// The matching SimulateProjection.fs carries NO `private`/`internal`/`public` modifiers.
//
// Design-first artifact (Principle I): the two projections of a `Simulate.SimulatedResult`, mirroring the
// reused `AuditJson`/`HumanText` split. Both are PURE and DETERMINISTIC (byte-stable for a fixed input).
// The machine-readable form uses the DISTINCT schema id `fsgg.audit.dryrun/v1` and a top-level
// `simulated: true` marker, so a simulated document is recognizable to audit readers yet can NEVER be
// consumed as a real gate result — the real `AuditJson.ofShipDecision` (`fsgg.audit/v2`) output is untouched.

namespace FS.GG.Governance.ShipCommand

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SimulateProjection =

    /// The DISTINCT dry-run schema id — deliberately NOT the real `fsgg.audit/v2`, so a simulated document
    /// cannot be mistaken for, or substituted for, a genuine gate result (spec FR-006 / contract G1).
    val schemaVersion: string

    /// The machine-readable simulated document: `schemaVersion = fsgg.audit.dryrun/v1`, `simulated: true`,
    /// the recognizable `verdict`/`exitCodeBasis`/`blockers`/`warnings`/`passing` fields, plus the
    /// `sufficiency` and `handoffDiagnostics` blocks. PURE and byte-stable (fixed key order).
    val toJson: result: Simulate.SimulatedResult -> string

    /// The human (ANSI-free, plain) projection: a prominent `SIMULATED (dry-run)` banner, the reused
    /// `HumanText` verdict view, the sufficiency breakdown (required-absent first), and any handoff
    /// diagnostics. PURE and deterministic.
    val toText: result: Simulate.SimulatedResult -> string
