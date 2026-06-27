// Curated public signature contract for the verdict-bridge composer (F081, FR-008/012).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II).
// The matching Consumer.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings.
//
// Design-first artifact: drafted in FSI before any Consumer.fs body exists (Principle I). PURE and
// TOTAL. `consume` parses + maps + readiness-projects every located document and emits typed
// `Gates.Model.Gate` registry entries + the matching pre-selected `Route.Model.SelectedGate` entries
// the three verdict hosts fold into the `GateRegistry` + `RouteResult.SelectedGates` BEFORE
// `Ship.rollup` (research D3). Handoff gates are pre-selected because their relevance is the declared
// work item, not a sensed changed path (research D3/D7). A bad document yields a blocking integrity
// gate + a diagnostic and NO mapped evidence/readiness gate for that document (no partial enforce —
// FR-011). Empty input ⇒ empty result (the no-op path — SC-003).

namespace FS.GG.Governance.Adapters.SddHandoff

open FS.GG.Governance.Gates.Model              // Gate
open FS.GG.Governance.Route.Model              // SelectedGate
open FS.GG.Governance.Adapters.SddHandoff.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Consumer =

    /// The bridge to the verdict: the handoff gate registry entries (evidence + readiness + integrity),
    /// the same gates pre-selected, and the surfaced diagnostics. The host unions `Gates` into the
    /// `GateRegistry` and `Selected` into `RouteResult.SelectedGates` before roll-up / the route JSON
    /// projection.
    type ConsumeResult =
        { Gates: Gate list
          Selected: SelectedGate list
          Diagnostics: Diagnostic list }

    /// Parse + map + readiness-project all located documents, in stable (`<id>`, then `GateId`) order.
    /// Empty input ⇒ `{ Gates = []; Selected = []; Diagnostics = [] }` (no-op, SC-003). A bad document
    /// ⇒ a blocking integrity gate + diagnostic and NO mapped gate for that document (FR-011). Total;
    /// never throws.
    val consume: reads: Reader.HandoffRead list -> ConsumeResult
