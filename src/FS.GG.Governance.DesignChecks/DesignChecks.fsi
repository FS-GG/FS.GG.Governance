// The pure design/rendering rule pack for F24 (P3, render-fenced). Visibility lives here (Constitution
// Principle II); DesignChecks.fs carries NO access modifiers and NO rendering/UI/registry reference (FR-007,
// SC-004).

namespace FS.GG.Governance.DesignChecks

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DesignChecks =

    /// PURE and TOTAL, NO I/O, NO rendering reference. Absent token/capture/control ⇒ Blocking
    /// `design.token` / `design.capture` / `design.control` naming the entry; a sub-threshold contrast pair
    /// (`Meets = false`) ⇒ Blocking `design.contrast` reporting ratio vs threshold (FR-006, SC-004).
    /// All-resolve ⇒ zero findings. An absent/unreadable catalog ⇒ `IsInputState` `design.catalog-unavailable`
    /// naming the catalog (FR-012). Each finding carries the request's declared `EvidenceTag` (FR-009).
    /// Sorted by (kind, entry id, code); identical facts ⇒ byte-identical findings.
    val evaluate:
        request: FS.GG.Governance.SurfaceChecks.Model.SurfaceCheckRequest ->
        facts: Model.DesignFacts ->
            FS.GG.Governance.SurfaceChecks.Model.SurfaceFinding list
