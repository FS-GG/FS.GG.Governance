// The design-domain fact vocabulary for F24 (P3, render-fenced) — implementation. Visibility lives in
// Model.fsi (Constitution Principle II); no top-level access modifiers here. Pure data only; NO rendering
// reference (FR-007, SC-004).

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
