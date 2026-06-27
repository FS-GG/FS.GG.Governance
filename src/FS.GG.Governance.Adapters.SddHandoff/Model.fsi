// Curated public signature contract for the typed SDD→Governance handoff shape + version pin (F081).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II).
// The matching Model.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings —
// visibility is presence/absence here.
//
// Design-first artifact: drafted in FSI before any Model.fs body exists (Principle I). These are the
// in-memory projection of one read-only `readiness/<id>/governance-handoff.json` document — the shape
// Governance reads against its OWN target types. Field names come from ADR 0002 + the handoff tutorial
// (docs/tutorials/sdd-governance-handoff.md); the authoritative JSON key spellings are SDD-owned and
// cross-checked at implementation (research D8). Governance imports NO SDD code and changes NO field of
// the SDD-owned document (FR-013, SC-006). It REUSES `Config.Model.GovernedPath` for path provenance
// (never redefined).

namespace FS.GG.Governance.Adapters.SddHandoff

open FS.GG.Governance.Config.Model            // GovernedPath

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// The tokens a PRODUCED handoff may declare for an evidence node. A CLOSED union — `autoSynthetic`
    /// is deliberately NOT a member: it is computed-only in the kernel `Evidence` taint closure and
    /// declaring it is rejected on read (FR-005, research D4). `Deferred`/`AcceptedDeferral` map to the
    /// kernel `Skipped` (recorded-rationale `[-]`), not `Pending` (FR-004).
    type DeclaredState =
        | Pending
        | Real
        | Synthetic
        | Failed
        | Skipped
        | Deferred
        | AcceptedDeferral

    /// One declared evidence node (`evidence.nodes[]`). `Stale` is the Governance-owned freshness flag
    /// (FR-006); `Rationale` is carried for `deferred → skipped` and diagnostics.
    type DeclaredNode =
        { Id: string
          State: DeclaredState
          Stale: bool
          Rationale: string option }

    /// The declared evidence block: nodes + `"a rests on b"` dependency edges fed verbatim to
    /// `Evidence.build` (may be empty — consumed independently of `readiness`).
    type EvidenceBlock =
        { Nodes: DeclaredNode list
          Dependencies: (string * string) list }

    /// The declared SDD merge-boundary readiness block. A non-shippable `ShipDisposition` OR a non-empty
    /// `BlockingDiagnosticIds` makes the derived readiness gate blocking-capable (FR-009). `Counts`/
    /// `PerViewState` are carried into the gate description.
    type ReadinessBlock =
        { ShipDisposition: string
          VerificationReadiness: string
          BlockingDiagnosticIds: string list
          Counts: (string * int) list
          PerViewState: (string * string) list }

    /// Optional routing enrichment only (FR-010): a declared work item and the governed paths it touches.
    /// Used solely to populate `SelectingPath` provenance on handoff gates when present; correctness never
    /// depends on it.
    type GovernedReference =
        { WorkItem: string
          Paths: GovernedPath list }

    /// The in-memory projection of one `readiness/<id>/governance-handoff.json`. The consumer pins
    /// `ContractVersion` MAJOR `1`; an unknown major ⇒ a version-mismatch diagnostic (FR-002).
    type Handoff =
        { ContractVersion: string
          SchemaVersion: int
          Evidence: EvidenceBlock
          Readiness: ReadinessBlock option
          GovernedReferences: GovernedReference list }

    /// Why a handoff (or one of its nodes) was refused or flagged. Distinct per cause so the surfaced
    /// message is distinct and descriptive (SC-004). These are handoff-domain diagnostics — NOT F017
    /// `FindingId`s (research D5).
    type DiagnosticCause =
        | VersionMismatch
        | Malformed
        | AutoSyntheticDeclared
        | StaleEvidence

    /// A surfaced, descriptive diagnostic: the cause, the `readiness/<id>/...` source path, and a
    /// descriptive message distinct per cause (SC-004).
    type Diagnostic =
        { Cause: DiagnosticCause
          Source: string
          Message: string }

    /// The pinned contract MAJOR the consumer recognizes (= 1). A handoff whose `ContractVersion` major
    /// differs yields a `VersionMismatch` diagnostic and no mapped result (FR-002).
    val supportedContractMajor: int
