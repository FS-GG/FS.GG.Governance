// Contract sketch for the NEW artifact-reading edge module (US2a, FR-003/FR-006).
//
// ArtifactReading turns a repository root into supplied facts + a ProjectChange and the
// resolved artifact list. It owns spec-kit/design path resolution, file/directory reads,
// the regex task/dependency parsers, and fact extraction. It is the impure edge: it
// touches the filesystem but degrades to the SAME `missing <path>` / InputUnavailable
// reasons as before (spec edge cases). Compiles after CliRender, before Program.
//
// The .fsi exposes only what the executable edge calls (research D1/D5); every path
// helper, regex parser, JSON reader, and fact builder stays HIDDEN.
//
// Byte-identity contract (US2 Acceptance 1, SC-001): for a given tree, the supplied
// facts, the ProjectChange, and the resolved artifact list are identical to the
// pre-extraction snapshot; directory artifacts still concatenate sorted, `### <rel>`-
// prefixed; missing/unreadable paths still degrade identically.

namespace FS.GG.Governance.Cli

open FS.GG.Governance.Kernel
open FS.GG.Governance.Host

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ArtifactReading =

    /// Build the ProjectOptions for a request (domains + judge + default spec-kit dial).
    /// Pure; shared by loadSnapshot and the host effect interpreter. (Relocated from
    /// Program.optionsFor.)
    val optionsFor: request: RunRequest -> ProjectOptions

    /// Read one artifact (file, or a directory concatenated in sorted `### <rel>` form)
    /// resolved from the spec-kit/design path rules. Returns `Error("missing " + path)` or
    /// the OS error message on failure. Called by the host `ReadArtifact` effect.
    /// (Relocated from Program.readArtifact.)
    val readArtifact: root: string -> artifact: ArtifactRef -> Result<string, string>

    /// Load the full ProjectSnapshot for a request: resolve the root, extract spec-kit and
    /// design facts, compute the ProjectChange and the artifact list. The CLI `LoadSnapshot`
    /// port. (Relocated from Program.loadSnapshot.)
    val loadSnapshot: request: RunRequest -> Result<ProjectSnapshot, string>
