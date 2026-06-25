// The EDGE of the package/API check (F24, P1) — the ONLY filesystem/process seam for this domain (FR-007).
// Visibility lives here (Constitution Principle II); Interpreter.fs carries NO access modifiers. The real
// port reads LOCAL files via BCL `System.IO` and runs published transcripts by shelling FSI through the
// injected F051/F052 `ExecutionPort` — NO `FSharp.Compiler.Service`, NO network. It NEVER throws out of
// itself: a missing/unreadable source or a thrown exception becomes a `*Unreadable`/`*Unlocatable` input
// fact (FR-012).

namespace FS.GG.Governance.PackageChecks

open FS.GG.Governance.Config.Model
open FS.GG.Governance.PackageChecks.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =

    /// Injected I/O port (Constitution IV). The ONLY filesystem/process seam for this domain (FR-007).
    /// `ListTranscripts` enumerates the published transcripts declared for a surface (the data-model's
    /// `RunTranscript` needs a list to drive — discovery is part of the same seam); `RunTranscript` shells
    /// FSI via the injected `ExecutionPort`.
    type PackagePort =
        { RegenerateSurface: GovernedPath -> Result<SurfaceTokens, string>
          ReadBaseline: GovernedPath -> Result<SurfaceTokens option, string>
          WriteBaseline: GovernedPath -> SurfaceTokens -> Result<unit, string>
          ListTranscripts: GovernedPath -> Result<GovernedPath list, string>
          RunTranscript: GovernedPath -> Result<TranscriptOutcome, string> }

    /// Build the REAL port for a repo working directory, reusing the F051/F052 `ExecutionPort` for FSI runs.
    /// The committed baseline lives at `<surface-path>.baseline`; transcripts are the `*.fsx` files under a
    /// sibling `transcripts/` directory, each optionally paired with a `<name>.fsx.expected` stdout file.
    /// This is the ONLY place the feature touches the filesystem/process; it references NO registry/network.
    val realPort: repo: string -> exec: FS.GG.Governance.GateExecution.Model.ExecutionPort -> PackagePort

    /// TOTAL and SAFE: catches every exception, maps to `*Unreadable`/`*Unlocatable` input facts (FR-012).
    /// On an absent baseline, regenerates + writes it and yields `BaselineAbsent` (FR-002). DETERMINISTIC:
    /// identical repository state ⇒ a structurally identical `PackageFacts` (normalized token diff — D5).
    val sensePackage:
        port: PackagePort ->
        request: FS.GG.Governance.SurfaceChecks.Model.SurfaceCheckRequest ->
            Model.PackageFacts
