// The docs-domain fact vocabulary for F24 (P2) — implementation. Visibility lives in Model.fsi
// (Constitution Principle II); no top-level access modifiers here. Pure data only.

namespace FS.GG.Governance.DocsChecks

open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type LinkOutcome =
        | LinkResolves
        | LinkDangling of target: string

    type LinkFact =
        { Source: GovernedPath
          LinkText: string
          Target: string
          Outcome: LinkOutcome }

    type ReferenceOutcome =
        | ReferenceResolves
        | ReferenceStale of symbol: string

    type ReferenceFact =
        { Source: GovernedPath
          Reference: string
          Outcome: ReferenceOutcome }

    type ExampleOutcome =
        | ExampleCurrent
        | ExampleStale of detail: string

    type ExampleFact =
        { Source: GovernedPath
          Example: string
          Outcome: ExampleOutcome }

    type DocsFacts =
        { Sources: GovernedPath list
          Links: LinkFact list
          References: ReferenceFact list
          Examples: ExampleFact list
          Unreadable: string list }
