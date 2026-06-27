// The surface-check verdict fold (076 Phase C seam). Visibility lives in SurfaceFold.fsi (Principle II) —
// this file carries NO top-level access modifiers. PURE and host-`Model`-free: it folds the already-classified
// `SurfaceFinding list` into the verdict via the EXISTING `deriveEffectiveSeverity` (no truth-table change —
// FR-007). Bindings lifted verbatim from `Loop.fs` (surfaceBlocks / foldSurfaceVerdict); `Loop` now calls
// `SurfaceFold.foldSurfaceVerdict` at its two projection sites, changing no emitted byte.

namespace FS.GG.Governance.VerifyCommand

open FS.GG.Governance.Enforcement.Enforcement // RunMode (Verify), Profile, Severity (Blocking), deriveEffectiveSeverity
open FS.GG.Governance.Ship.Model               // ShipDecision, Verdict (Fail), ExitCodeBasis

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SurfaceFold =

    // True iff any surface finding is effective-Blocking at `RunMode.Verify` under the active profile. The
    // effective severity is derived by the EXISTING `deriveEffectiveSeverity` over the input `enforcementInputOf`
    // builds from the finding — reuse only, no new rule, no new severity (FR-007, FR-008). A base-Advisory
    // finding never escalates; a base-Blocking finding blocks once the verify floor is reached.
    let surfaceBlocks (profile: Profile) (findings: FS.GG.Governance.SurfaceChecks.Model.SurfaceFinding list) : bool =
        findings
        |> List.exists (fun f ->
            (deriveEffectiveSeverity (FS.GG.Governance.SurfaceChecks.Model.enforcementInputOf f Verify profile))
                .EffectiveSeverity = Blocking)

    // Fold the surface findings into an ALREADY-rolled (and, on the executed path, ALREADY-relocated) decision:
    // a blocking surface finding fails the run; an advisory one leaves the verdict/exit untouched. Surface
    // findings stay DISTINCT from gate/finding items in the projection (`surfaceChecks` vs `execution`) — this
    // only flips the verdict/exit basis, it never injects a surface item into Blockers/Warnings/Passing. MUST
    // run AFTER `applyExecution` (which recomputes from gate blockers only and would otherwise erase the
    // surface-driven block). With `findings = []` it is the identity ⇒ byte-identical verify.json (FR-004).
    let foldSurfaceVerdict
        (profile: Profile)
        (findings: FS.GG.Governance.SurfaceChecks.Model.SurfaceFinding list)
        (decision: ShipDecision)
        : ShipDecision =
        if surfaceBlocks profile findings then
            { decision with
                Verdict = Fail
                ExitCodeBasis = ExitCodeBasis.Blocked }
        else
            decision
