// Curated public signature contract for the F12 concrete project composition root.
//
// F09 deliberately ships generic composition machinery but not a concrete project
// coproduct. The CLI is the first real consumer root, so it owns this closed union and the
// functions that lift the shipped F10 Spec Kit adapter and F11 design-system adapter into
// one run.

namespace FS.GG.Governance.Cli

open FS.GG.Governance.Kernel
open FS.GG.Governance.Host
open FS.GG.Governance.Adapters.Spi
open FS.GG.Governance.Adapters.SpecKit
open FS.GG.Governance.Adapters.DesignSystem

/// Domains selected for the composed run. The default is all shipped domains.
type Domain =
    | SpecKitDomain
    | DesignSystemDomain

/// Closed project fact union for the CLI composition root.
type ProjectFact =
    | SpecKitProjectFact of SpecKitFact
    | DesignSystemProjectFact of DesignSystemFact
    | GovernanceFact of RuleOutcome
    | ArtifactContentFact of artifact: ArtifactRef * hash: string * content: string
    | EvidenceStateFact of node: string * state: EvidenceState
    | EvidenceDependencyFact of dependent: string * dependency: string
    | FreshnessFact of node: string * recorded: int64 * covered: int64 list

/// Project-level change shape. Each adapter sees its own narrowed change through F09 lift.
type ProjectChange =
    { SpecKit: SpecKitChange option
      DesignSystem: DesignChange option
      Scope: string list }

/// Snapshot sensed from a repository root before Host runs.
type ProjectSnapshot =
    { Root: string
      Supplied: FactSet<ProjectFact>
      Change: ProjectChange
      Artifacts: ArtifactRef list
      /// F081 wiring: the raw SDD→Governance handoff documents located under `Root`
      /// (`readiness/<id>/governance-handoff.json`), in stable `<id>` order; `[]` when none.
      /// The `route` command folds these through `Adapters.SddHandoff.Consumer` into its gate
      /// verdict so a produced handoff drives the exit code (blocks at `--mode gate`).
      Handoffs: FS.GG.Governance.Adapters.SddHandoff.Reader.HandoffRead list
      /// 090: the product's declared `.fsgg/policy.yml defaultProfile`, read at the Config-load
      /// edge (`Config.Loader.loadAndValidate`); `None` when no policy is declared, the policy is
      /// invalid, or `defaultProfile` is absent. The `route` exit resolves this through
      /// `Enforcement.recognizeProfile` (absent / unrecognized → `Strict`, the one-way fail-safe)
      /// so the handoff gate honors the active profile like every other gate.
      DefaultProfile: FS.GG.Governance.Config.Model.ProfileId option }

/// Options for building the composed catalog and Host configuration.
type ProjectOptions =
    { Domains: Set<Domain>
      Judge: JudgeId
      SpecKitDial: ConstitutionDial }

/// One evidence node in the CLI evidence report.
type EvidenceNodeReport =
    { Id: string
      Declared: EvidenceState option
      Effective: EvidenceState option
      Freshness: Freshness option
      Source: string }

/// Project-level evidence report before review-budget accounting is attached by Cli.
type ProjectEvidenceReport =
    { Nodes: EvidenceNodeReport list
      Dependencies: (string * string) list
      Disclosures: Disclosure list
      Failures: Failure list }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Project =

    /// Default judge identity used when no explicit `--judge-model`/`--judge-version` is supplied.
    /// The identity is folded into F04 agent-review cache keys. Relocated here from the `Cli` module
    /// (100/M-ARCH-2) so both the CLI and the EvidenceCommand tool consume it without an exe→exe edge.
    val defaultJudge: JudgeId

    /// Prism used by F09 lift: recover a Spec Kit fact from the project coproduct.
    val (|SpecKitProject|_|): fact: ProjectFact -> SpecKitFact option

    /// Prism used by F09 lift: recover a design-system fact from the project coproduct.
    val (|DesignSystemProject|_|): fact: ProjectFact -> DesignSystemFact option

    /// Project fact identity authority for F01 fixed-point evaluation.
    val identify: fact: ProjectFact -> FactId

    /// Project bridge used by F04/F09 when composed rules emit/read governance outcomes.
    val bridge: judge: JudgeId -> Bridge<ProjectFact>

    /// Compose the selected shipped adapters and any CLI-owned cross-domain rules.
    val compose: options: ProjectOptions -> Composed<ProjectFact, ProjectChange>

    /// Lift artifact content into project facts. Used by Host.Loop `SenseArtifact`.
    val senseArtifact: artifact: ArtifactRef -> content: string -> ProjectFact

    /// Read raw artifact content back from facts. Used to build review data channels.
    val readContent: facts: FactSet<ProjectFact> -> artifact: ArtifactRef -> string option

    /// Build the Host loop configuration for a run over a sensed project snapshot.
    val toLoopConfig:
        options: ProjectOptions ->
        mode: RunMode ->
        snapshot: ProjectSnapshot ->
            LoopConfig<ProjectChange, ProjectFact>

    /// Fold project facts and Host model failures into the `evidence` command report.
    val evidenceReport: host: FS.GG.Governance.Host.Model<ProjectFact> -> ProjectEvidenceReport
