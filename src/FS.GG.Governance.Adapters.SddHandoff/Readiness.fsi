// Curated public signature contract for the readiness → gate projection (F081, US3).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II).
// The matching Readiness.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings.
//
// Design-first artifact: drafted in FSI before any Readiness.fs body exists (Principle I). PURE and
// TOTAL. It projects the handoff's `readiness.*` block into a first-class typed `Gates.Model.Gate`
// (FR-009, FR-015 — resolving ADR-0002 queue item #4) so SDD merge-boundary readiness participates in
// selection, severity resolution, and roll-up like any other gate (research D3).

namespace FS.GG.Governance.Adapters.SddHandoff

open FS.GG.Governance.Gates.Model              // Gate
open FS.GG.Governance.Adapters.SddHandoff.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Readiness =

    /// Build the readiness gate from one declared `ReadinessBlock`. The gate's `Maturity` is
    /// `Config.Model.Maturity.BlockOnShip` when the `ShipDisposition` is non-shippable OR
    /// `BlockingDiagnosticIds` is non-empty; advisory `Maturity.Warn` otherwise. `BlockOnShip` resolves
    /// to blocking under both ship AND verify (Strict) via `Enforcement.deriveEffectiveSeverity`
    /// (SC-005). The declared counts / per-view state are carried into the gate description (FR-009).
    /// `source` is the `readiness/<id>/...` path, used to derive the stable `GateId`. Total; never throws.
    val toGate: source: string -> block: ReadinessBlock -> Gate
