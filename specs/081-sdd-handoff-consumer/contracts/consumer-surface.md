# Contract: consumer library public surface (`FS.GG.Governance.Adapters.SddHandoff`)

**Tier 1** — every module below is `.fsi`-curated (Constitution II) and covered by
`surface/FS.GG.Governance.Adapters.SddHandoff.surface.txt`. Drafted in FSI before any `.fs`
body (Constitution I). Signatures below are the *intended* shapes (`/speckit-tasks` refines
exact names); they reuse existing Governance types and define only the new vocabulary.

## `Model` — typed handoff shape + version pin

```fsharp
namespace FS.GG.Governance.Adapters.SddHandoff

module Model =
    /// The tokens a PRODUCED handoff may declare (no autoSynthetic — FR-005).
    type DeclaredState =
        | Pending | Real | Synthetic | Failed | Skipped | Deferred | AcceptedDeferral

    type DeclaredNode = { Id: string; State: DeclaredState; Stale: bool; Rationale: string option }
    type EvidenceBlock = { Nodes: DeclaredNode list; Dependencies: (string * string) list }
    type ReadinessBlock =
        { ShipDisposition: string; VerificationReadiness: string
          BlockingDiagnosticIds: string list; Counts: (string * int) list
          PerViewState: (string * string) list }
    type GovernedReference = { WorkItem: string; Paths: GovernedPath list }
    type Handoff =
        { ContractVersion: string; SchemaVersion: int
          Evidence: EvidenceBlock; Readiness: ReadinessBlock option
          GovernedReferences: GovernedReference list }

    type DiagnosticCause = VersionMismatch | Malformed | AutoSyntheticDeclared | StaleEvidence
    type Diagnostic = { Cause: DiagnosticCause; Source: string; Message: string }

    /// The pinned contract major the consumer recognizes.
    val supportedContractMajor: int      // = 1
```

## `Reader` — pure parse + version-check (US2; FR-002/011, research D2/D5)

```fsharp
module Reader =
    /// One located document: its path and raw JSON text (the impure read is the host's port).
    type HandoffRead = { Source: string; Json: string }

    /// Pure: parse + validate one document. Unknown major / malformed / missing-required ⇒ Error
    /// with a distinct, descriptive Diagnostic (never throws — Constitution VI).
    val parse: read: HandoffRead -> Result<Handoff, Diagnostic>
```

## `Mapping` — ADR-0002 evidence map → `Evidence` (US1; FR-003/004/005/006/007, research D4)

```fsharp
module Mapping =
    /// Map declared nodes to (id, EvidenceState) + carry stale/autoSynthetic diagnostics.
    /// Returns the mapped graph inputs and any per-node diagnostics; autoSynthetic ⇒ rejected.
    val mapEvidence:
        source: string -> block: EvidenceBlock ->
            Result<(string * EvidenceState) list * (string * string) list, Diagnostic> * Diagnostic list

    /// Build + taint-close. A Failed/AutoSynthetic effective state ⇒ evidence is blocking-capable.
    val effectiveStates:
        nodes: (string * EvidenceState) list -> deps: (string * string) list ->
            Result<Map<string, EvidenceState>, Diagnostic>
```

## `Readiness` — readiness.* → typed gate (US3; FR-009, research D3)

```fsharp
module Readiness =
    /// Build the readiness gate: block-on-* maturity when non-shippable disposition OR
    /// non-empty blockingDiagnosticIds; advisory `warn` otherwise.
    val toGate: source: string -> block: ReadinessBlock -> Gate
```

## `Consumer` — compose to the verdict bridge (FR-008/012, research D3/D7)

```fsharp
module Consumer =
    type ConsumeResult =
        { Gates: Gate list                 // registry entries (evidence + readiness + integrity)
          Selected: SelectedGate list      // pre-selected (relevance = work item, not changed path)
          Diagnostics: Diagnostic list }

    /// Parse + map + readiness over all located documents, in stable (<id>, GateId) order.
    /// Empty input ⇒ empty result (no-op). A bad document ⇒ a blocking integrity gate + diagnostic,
    /// and NO mapped evidence/readiness gate for that document (no partial enforce — FR-011).
    val consume: reads: HandoffRead list -> ConsumeResult
```

## Reused types (imported, never redefined)

`Kernel.Evidence.{EvidenceState, build, effective}` · `Gates.Model.{Gate, GateId, Maturity,
Cost, DomainId}` · `Route.Model.{SelectedGate, SelectingPath, RouteResult}` ·
`Config.Model.GovernedPath`.
