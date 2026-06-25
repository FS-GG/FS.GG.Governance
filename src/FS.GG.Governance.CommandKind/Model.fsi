// Curated public signature contract for the command-run kind taxonomy + audit-snapshot types (F25).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// Model.fs carries NO access modifiers. These types WRAP the F032 `CommandRecord` and F033 `Provenance`
// verbatim (D5) — neither is extended, and the descriptive `CommandKind` does NOT participate in either
// identity.

namespace FS.GG.Governance.CommandKind

open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.Provenance.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// The closed taxonomy of expensive command kinds a governed run performs (FR-008). Exactly seven; the
    /// kind is DESCRIPTIVE metadata sensed at the host edge — it does NOT participate in the F032 identity.
    type CommandKind =
        | Build
        | Test
        | Pack
        | TemplateInstantiation
        | GitDiff
        | PackageInspection
        | VisualCapture

    /// An F032 `CommandRecord` wrapped (NOT extended) with its kind. Identity is the record's identity.
    type KindedCommandRun =
        { Kind: CommandKind
          Record: CommandRecord }

    /// The provenance audit snapshot: the F033 `Provenance` roll-up plus the kind labels for projection.
    type AuditSnapshot =
        { Provenance: Provenance
          Runs: KindedCommandRun list }
