// The package-domain fact vocabulary for F24 (P1) — implementation. Visibility lives in Model.fsi
// (Constitution Principle II); no top-level access modifiers here. Pure data only.

namespace FS.GG.Governance.PackageChecks

open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type SurfaceTokens = SurfaceTokens of string list

    type FsiBaselineFact =
        | BaselineMatches
        | BaselineDrift of added: string list * removed: string list
        | BaselineAbsent of generated: SurfaceTokens
        | BaselineUnreadable of source: string

    type TranscriptOutcome =
        | TranscriptPasses
        | TranscriptCompileFailed of detail: string
        | TranscriptResultChanged of expected: string * actual: string
        | TranscriptUnlocatable of source: string

    type TranscriptFact =
        { ExampleId: string
          Source: GovernedPath
          Outcome: TranscriptOutcome }

    type PackageFacts =
        { BaselineSource: GovernedPath
          Baseline: FsiBaselineFact
          Transcripts: TranscriptFact list }
