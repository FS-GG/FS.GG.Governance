// Curated public signature for the surface-check verdict fold (076 Phase C seam, extracted from Loop.fs).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II); the matching
// SurfaceFold.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings. PURE over
// `Profile` + `SurfaceFinding list` + `ShipDecision` — already host-`Model`-free, so it lifts verbatim from
// `Loop.surfaceBlocks`/`Loop.foldSurfaceVerdict`. The fold reuses the EXISTING `deriveEffectiveSeverity`
// truth table (no new rule, no new severity — FR-007); with `findings = []` it is the identity, so verify.json
// stays byte-identical (FR-004). Compiled BEFORE `Loop.fs`.

namespace FS.GG.Governance.VerifyCommand

open FS.GG.Governance.Enforcement.Enforcement   // Profile
open FS.GG.Governance.SurfaceChecks.Model        // SurfaceFinding
open FS.GG.Governance.Ship.Model                 // ShipDecision

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SurfaceFold =

    /// True when the profile makes any surface finding effective-Blocking at `RunMode.Verify` (verbatim from
    /// `Loop.surfaceBlocks`). The effective severity is derived by the EXISTING `deriveEffectiveSeverity` over
    /// the input `enforcementInputOf` builds from the finding — reuse only, no new rule/severity (FR-007).
    val surfaceBlocks: profile: Profile -> findings: SurfaceFinding list -> bool

    /// Fold the surface findings into an ALREADY-rolled (and, on the executed path, ALREADY-relocated)
    /// decision: a blocking surface finding flips Verdict/ExitCodeBasis to blocked; an advisory one leaves the
    /// verdict untouched. It NEVER injects a surface item into Blockers/Warnings/Passing. With `findings = []`
    /// it is the identity ⇒ byte-identical verify.json (FR-004). Verbatim from `Loop.foldSurfaceVerdict`.
    val foldSurfaceVerdict: profile: Profile -> findings: SurfaceFinding list -> decision: ShipDecision -> ShipDecision
