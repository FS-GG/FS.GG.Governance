// The verify.json surface-checks writer (076 Phase C seam). Visibility lives in SurfaceChecks.fsi
// (Principle II) — no top-level access modifiers here. PURE: walks a caller-owned `Utf8JsonWriter`. Lifted
// verbatim from `VerifyJson.writeSurfaceFinding`; the composing `VerifyJson` entry calls
// `SurfaceChecks.writeSurfaceFinding` under the same `if not (List.isEmpty findings)` guard.

namespace FS.GG.Governance.VerifyJson

open System.Text.Json
open FS.GG.Governance.Config.Model         // SurfaceId, EvidenceTag, GovernedPath
open FS.GG.Governance.RuleIdentity         // 068: the additive per-finding `ruleId` source-prefixed token
open FS.GG.Governance.JsonTokens           // 073: severityToken

module SC = FS.GG.Governance.SurfaceChecks.Model // F24: the additive surfaceChecks section

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SurfaceChecks =

    // ── F24: the additive `surfaceChecks` element — `{ domain, surface, code, file, detail, severity,
    //    inputState, evidenceTag?, message }`. `evidenceTag` is emitted ONLY when the surface declared one
    //    (FR-009); omitted otherwise. `severity` is the BASE severity (advisory entries appear but never
    //    change the exit code — FR-011). Deterministic: no abs-path/clock/username (FR-010). ──

    let writeSurfaceFinding (w: Utf8JsonWriter) (f: SC.SurfaceFinding) =
        w.WriteStartObject()
        w.WriteString("domain", SC.checkDomainToken f.Domain)
        let (SurfaceId sid) = f.Surface
        w.WriteString("surface", sid)
        w.WriteString("code", f.Code)
        // 068: the surface finding's `ruleId` (`surface:<domain>:<code>`), emitted after the leading
        // domain/surface/code identity triple and before the detail fields. Derived from the same domain +
        // code already written above; reads no severity / message, so it is profile/mode/message-invariant.
        w.WriteString("ruleId", RuleIdentity.ruleIdToken (RuleIdentity.surface (SC.checkDomainToken f.Domain) f.Code))
        let (GovernedPath file) = f.Location.File
        w.WriteString("file", file)
        w.WriteString("detail", f.Location.Detail)
        w.WriteString("severity", JsonTokens.severityToken f.BaseSeverity)
        w.WriteBoolean("inputState", f.IsInputState)

        match f.EvidenceTag with
        | Some(EvidenceTag t) -> w.WriteString("evidenceTag", t)
        | None -> ()

        w.WriteString("message", f.Message)
        w.WriteEndObject()
