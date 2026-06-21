// Freshness-input vocabulary for the freshness-key core (F029). The public surface is fixed by Model.fsi
// (Principle II); no top-level binding here carries an access modifier. These are product-neutral,
// comparable values that `FreshnessKey.compute` fingerprints; they reuse the F014 typed-fact newtypes
// (`CheckId`, `DomainId`, `CommandId`, `EnvironmentClass`) rather than redefining them (FR-009). Base/head
// use a LOCAL `Revision` newtype so the pure core never references the git-sensing Snapshot assembly
// (research D3). `Cost` is deliberately absent (research D5).

namespace FS.GG.Governance.FreshnessKey

open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type RuleHash = RuleHash of string

    type ArtifactHash = ArtifactHash of string

    type CommandVersion = CommandVersion of string

    type GeneratorVersion = GeneratorVersion of string

    type Revision = Revision of string

    type FreshnessInputs =
        { Check: CheckId
          Domain: DomainId
          Command: CommandId option
          Environment: EnvironmentClass
          RuleHash: RuleHash
          CoveredArtifacts: ArtifactHash list
          CommandVersion: CommandVersion option
          GeneratorVersion: GeneratorVersion
          Base: Revision
          Head: Revision }

    type Key = Key of string

    type InputCategory =
        | CheckIdentity
        | DomainIdentity
        | CommandIdentity
        | EnvironmentClassCat
        | RuleHashCat
        | CoveredArtifactsCat
        | CommandVersionCat
        | GeneratorVersionCat
        | BaseRevisionCat
        | HeadRevisionCat

    let categoryToken (category: InputCategory) : string =
        match category with
        | CheckIdentity -> "check"
        | DomainIdentity -> "domain"
        | CommandIdentity -> "command"
        | EnvironmentClassCat -> "environmentClass"
        | RuleHashCat -> "ruleHash"
        | CoveredArtifactsCat -> "coveredArtifacts"
        | CommandVersionCat -> "commandVersion"
        | GeneratorVersionCat -> "generatorVersion"
        | BaseRevisionCat -> "baseRevision"
        | HeadRevisionCat -> "headRevision"
