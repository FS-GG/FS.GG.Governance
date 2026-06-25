namespace FS.GG.Governance.CommandKind

open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.Provenance.Model

// The F25 command-run kind taxonomy + audit-snapshot vocabulary (US4). Closed DU + plain records; no access
// modifiers (Principle II — the surface is Model.fsi). The F032 `CommandRecord` and F033 `Provenance` are
// reused verbatim (D5); the `CommandKind` is descriptive metadata carried beside the record, never folded
// into its identity.

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type CommandKind =
        | Build
        | Test
        | Pack
        | TemplateInstantiation
        | GitDiff
        | PackageInspection
        | VisualCapture

    type KindedCommandRun =
        { Kind: CommandKind
          Record: CommandRecord }

    type AuditSnapshot =
        { Provenance: Provenance
          Runs: KindedCommandRun list }
