// The package-domain fact vocabulary for F24 (P1). Visibility lives here (Constitution Principle II);
// Model.fs carries NO access modifiers. These are the SENSED facts the pure `PackageChecks.evaluate`
// consumes — produced by the `Interpreter` sensor, never sensed in the pure pack (FR-007).

namespace FS.GG.Governance.PackageChecks

open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// A normalized public-surface token set for one module's `.fsi` (D5: token diff, not text diff).
    /// Sorted, normalized public declarations.
    type SurfaceTokens = SurfaceTokens of string list

    /// Result of comparing a regenerated `.fsi` surface against the committed baseline.
    type FsiBaselineFact =
        | BaselineMatches
        | BaselineDrift of added: string list * removed: string list
        | BaselineAbsent of generated: SurfaceTokens
        | BaselineUnreadable of source: string

    /// Result of running one published FSI transcript (a public example / package contract).
    type TranscriptOutcome =
        | TranscriptPasses
        | TranscriptCompileFailed of detail: string
        | TranscriptResultChanged of expected: string * actual: string
        | TranscriptUnlocatable of source: string

    type TranscriptFact =
        { ExampleId: string
          Source: GovernedPath
          Outcome: TranscriptOutcome }

    /// Everything the package sensor produced for one surface. An empty `Transcripts` list ⇒ no transcripts
    /// declared (not an error).
    type PackageFacts =
        { BaselineSource: GovernedPath
          Baseline: FsiBaselineFact
          Transcripts: TranscriptFact list }
