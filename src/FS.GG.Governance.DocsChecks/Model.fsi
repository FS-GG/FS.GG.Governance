// The docs-domain fact vocabulary for F24 (P2). Visibility lives here (Constitution Principle II);
// Model.fs carries NO access modifiers. The SENSED facts the pure `DocsChecks.evaluate` consumes.

namespace FS.GG.Governance.DocsChecks

open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// A link target's currency (internal path, anchor, or external-style reference).
    type LinkOutcome =
        | LinkResolves
        | LinkDangling of target: string

    type LinkFact =
        { Source: GovernedPath
          LinkText: string
          Target: string
          Outcome: LinkOutcome }

    /// A referenced symbol/anchor's currency.
    type ReferenceOutcome =
        | ReferenceResolves
        | ReferenceStale of symbol: string

    type ReferenceFact =
        { Source: GovernedPath
          Reference: string
          Outcome: ReferenceOutcome }

    /// A docs example whose "match the current product surface" verdict is judgement-heavy (advisory
    /// boundary, C3). `ExampleStale` ⇒ an Advisory finding (never blocks, FR-011/US5). Compile/evaluate
    /// staleness is deterministic and handled by the package transcript machinery, NOT here.
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
