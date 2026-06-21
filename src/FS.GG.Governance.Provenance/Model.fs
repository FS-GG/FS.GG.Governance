// Provenance fact types for the provenance core (F033). The public surface is fixed by Model.fsi
// (Principle II); no top-level binding here carries an access modifier. These are product-neutral,
// YAML-free values that `Provenance.build` constructs and `canonicalId` projects over; they reuse the F029
// `Revision`/`RuleHash`/`GeneratorVersion`/`ArtifactHash`, the F032 `CommandRecord`, and the F014
// `EnvironmentClass` verbatim rather than redefining them (FR-010). The only new shapes are `BuilderIdentity`
// and `ProvenanceIdentity`.

namespace FS.GG.Governance.Provenance

open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.CommandRecord.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type BuilderIdentity = BuilderIdentity of string

    type ProvenanceIdentity = ProvenanceIdentity of string

    type Provenance =
        { SourceCommit: Revision
          Base: Revision
          Head: Revision
          RuleHash: RuleHash
          GeneratorVersion: GeneratorVersion
          ArtifactDigests: ArtifactHash list
          CommandRecords: CommandRecord list
          Environment: EnvironmentClass
          Builder: BuilderIdentity }
