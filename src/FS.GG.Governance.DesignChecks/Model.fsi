// The design-domain fact vocabulary for F24 (P3, render-fenced). Visibility lives here (Constitution
// Principle II); Model.fs carries NO access modifiers. The SENSED facts the pure `DesignChecks.evaluate`
// consumes. NO rendering/UI type is referenced anywhere in this library (FR-007, SC-004): every design
// entry is caller-supplied via the catalog the sensor read — never a literal here.

namespace FS.GG.Governance.DesignChecks

open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type ResolveOutcome =
        | Resolves
        | Absent of entry: string

    type TokenFact = { Token: string; Outcome: ResolveOutcome }
    type CaptureFact = { Capture: string; Outcome: ResolveOutcome }
    type ControlFact = { Control: string; Outcome: ResolveOutcome }

    /// Contrast pair measured against its declared threshold (deterministic numeric compare).
    type ContrastFact =
        { Pair: string
          Ratio: decimal
          Threshold: decimal
          Meets: bool }

    type DesignFacts =
        { Tokens: TokenFact list
          Captures: CaptureFact list
          Controls: ControlFact list
          Contrasts: ContrastFact list
          CatalogUnavailable: string list }
