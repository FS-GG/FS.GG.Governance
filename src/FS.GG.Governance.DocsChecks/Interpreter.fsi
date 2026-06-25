// The EDGE of the docs/examples check (F24, P2) — the ONLY filesystem seam for this domain (FR-007).
// Visibility lives here (Constitution Principle II); Interpreter.fs carries NO access modifiers. The real
// port reads docs sources via BCL `System.IO` and resolves internal link/anchor/symbol targets; it never
// throws out of itself — an unreadable source becomes a `docs.source-unreadable` input fact (FR-012).

namespace FS.GG.Governance.DocsChecks

open FS.GG.Governance.Config.Model
open FS.GG.Governance.DocsChecks.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =

    /// Injected port: read a docs source, resolve an internal path/anchor target, resolve a symbol/anchor.
    /// The ONLY filesystem seam. `ResolveTarget`/`ResolveSymbol` interpret their argument relative to the
    /// repo root (the real port closes over `repo`).
    type DocsPort =
        { ReadSource: GovernedPath -> Result<string, string>
          ResolveTarget: string -> bool
          ResolveSymbol: string -> bool }

    val realPort: repo: string -> DocsPort

    /// TOTAL and SAFE: reads the declared source at `request.Path`, extracts its markdown links
    /// (`[text](target)`) and wiki references (`[[symbol]]`), resolves each, and records an unreadable source
    /// in `Unreadable` (every exception caught, FR-012). DETERMINISTIC: identical sources ⇒ identical facts.
    /// Automated example-freshness judgement is out of scope (inherently judgement-heavy — supplied by a
    /// reviewer as an `ExampleFact`), so `Examples` is empty here.
    val senseDocs:
        port: DocsPort ->
        request: FS.GG.Governance.SurfaceChecks.Model.SurfaceCheckRequest ->
            Model.DocsFacts
