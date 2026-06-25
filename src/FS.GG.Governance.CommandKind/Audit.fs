namespace FS.GG.Governance.CommandKind

open FS.GG.Governance.Config.Model
open FS.GG.Governance.CommandRecord
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.Provenance
open FS.GG.Governance.Provenance.Model
open FS.GG.Governance.CommandKind.Model

// The F25 command-kind audit core (US4). PURE and TOTAL: no I/O, no clock, no git, never throws. Identity is
// F032/F033 reused VERBATIM (D5): `runIdentity` is exactly the F032 `CommandRecord` identity and
// `snapshotIdentity` is exactly the F033 `Provenance` identity — no new fingerprint, and the descriptive
// `CommandKind` never participates, so a kind-only or duration-only change leaves both identities unchanged.
// No access modifiers — the surface is Audit.fsi (Principle II).

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Audit =

    let kindToken (kind: CommandKind) : string =
        match kind with
        | Build -> "build"
        | Test -> "test"
        | Pack -> "pack"
        | TemplateInstantiation -> "templateInstantiation"
        | GitDiff -> "gitDiff"
        | PackageInspection -> "packageInspection"
        | VisualCapture -> "visualCapture"

    let runIdentity (run: KindedCommandRun) : string =
        CommandRecord.identityValue (CommandRecord.canonicalId run.Record)

    let auditSnapshot
        (sourceCommit: Revision)
        (baseRevision: Revision)
        (headRevision: Revision)
        (ruleHash: RuleHash)
        (generatorVersion: GeneratorVersion)
        (artifactDigests: ArtifactHash list)
        (runs: KindedCommandRun list)
        (environment: EnvironmentClass)
        (builder: BuilderIdentity)
        : AuditSnapshot =
        let provenance =
            Provenance.build
                sourceCommit
                baseRevision
                headRevision
                ruleHash
                generatorVersion
                artifactDigests
                (runs |> List.map (fun r -> r.Record))
                environment
                builder

        { Provenance = provenance; Runs = runs }

    let snapshotIdentity (snapshot: AuditSnapshot) : string =
        Provenance.identityValue (Provenance.canonicalId snapshot.Provenance)
