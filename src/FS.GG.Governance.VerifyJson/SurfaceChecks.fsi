// Curated public signature for the verify.json surface-checks writer (076 Phase C seam, extracted from
// VerifyJson.fs). This .fsi is the SOLE declaration of the module's public surface (Principle II);
// SurfaceChecks.fs carries NO top-level access modifiers. One public writer the composing entry calls under
// its `findings` guard. Compiled after `Core`, before `VerifyJson.fs`.

namespace FS.GG.Governance.VerifyJson

open System.Text.Json                          // Utf8JsonWriter
open FS.GG.Governance.SurfaceChecks.Model      // SurfaceFinding

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SurfaceChecks =

    /// Write one F24 `surfaceChecks` element — `{ domain, surface, code, ruleId, file, detail, severity,
    /// inputState, evidenceTag?, message }`. `evidenceTag` is emitted ONLY when the surface declared one
    /// (FR-009); `severity` is the BASE severity. Verbatim from `VerifyJson.writeSurfaceFinding`.
    val writeSurfaceFinding: w: Utf8JsonWriter -> f: SurfaceFinding -> unit
