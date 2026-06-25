// The EDGE of the skill check (F24, P2) — the ONLY filesystem seam for this domain (FR-007). Visibility
// lives here (Constitution Principle II); Interpreter.fs carries NO access modifiers. The real port reads
// the skill manifest + mirror via BCL `System.IO` and resolves claimed paths; it never throws out of itself
// — an unreadable manifest/mirror becomes an input fact in `Unreadable` (FR-012).

namespace FS.GG.Governance.SkillChecks

open FS.GG.Governance.Config.Model
open FS.GG.Governance.SkillChecks.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =

    /// Injected port: read the manifest, resolve a claimed path (existence; bounds checked in the sensor),
    /// read a declared mirror (`None` ⇒ declared-absent). The ONLY filesystem seam.
    type SkillPort =
        { ReadManifest: GovernedPath -> Result<string, string>
          ResolvePath: string -> Result<bool, string>
          ReadMirror: string -> Result<string option, string> }

    val realPort: repo: string -> SkillPort

    /// TOTAL and SAFE: reads the manifest at `request.Path`, parses its neutral `path:` / `task:` / `mirror:`
    /// declarations, resolves each claimed path (resolves? within bounds?), assesses task-list consistency,
    /// and reads the mirror; every exception is caught into `Unreadable` (FR-012). DETERMINISTIC.
    val senseSkill:
        port: SkillPort ->
        request: FS.GG.Governance.SurfaceChecks.Model.SurfaceCheckRequest ->
            Model.SkillFacts
