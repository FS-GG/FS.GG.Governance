// Curated public signature for the production-journey readiness → gate projection
// (Governance#324). Visibility lives here; Journey.fs carries no top-level access modifiers.

namespace FS.GG.Governance.Adapters.SddHandoff

open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Adapters.SddHandoff.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Journey =

    /// Project SDD's validated journey readiness into a first-class gameplay gate. Zero unmet
    /// obligations is advisory; any non-zero value is `BlockOnShip`. The description preserves the
    /// typed provenance disposition, producer diagnostics, and affected obligation/scenario ids.
    val toGate: source: string -> readiness: JourneyReadiness -> Gate
