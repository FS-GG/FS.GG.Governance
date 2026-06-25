// The pure skill rule pack for F24 (P2). Visibility lives here (Constitution Principle II);
// SkillChecks.fs carries NO access modifiers.

namespace FS.GG.Governance.SkillChecks

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SkillChecks =

    /// PURE and TOTAL (no I/O). Path unresolved/escapes ⇒ Blocking `skill.path-contract` naming skill+path;
    /// inconsistent task list ⇒ Blocking `skill.task-list`; missing/drifted mirror ⇒ Blocking `skill.mirror`;
    /// `NoMirrorDeclared` / `MirrorInSync` / `PathHolds` / `TaskListConsistent` ⇒ no finding (FR-005,
    /// SC-003). An unreadable manifest/mirror ⇒ `IsInputState` `skill.manifest-unreadable` (FR-012). Each
    /// finding carries the request's declared `EvidenceTag` (FR-009). Sorted by (skill id, locus, code);
    /// identical facts ⇒ byte-identical findings.
    val evaluate:
        request: FS.GG.Governance.SurfaceChecks.Model.SurfaceCheckRequest ->
        facts: Model.SkillFacts ->
            FS.GG.Governance.SurfaceChecks.Model.SurfaceFinding list
