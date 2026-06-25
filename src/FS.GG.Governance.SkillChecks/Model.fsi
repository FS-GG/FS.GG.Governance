// The skill-domain fact vocabulary for F24 (P2). Visibility lives here (Constitution Principle II);
// Model.fs carries NO access modifiers. The SENSED facts the pure `SkillChecks.evaluate` consumes.

namespace FS.GG.Governance.SkillChecks

open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// One claimed path of a skill's path contract.
    type PathContractOutcome =
        | PathHolds
        | PathUnresolved of claimed: string
        | PathEscapesBounds of claimed: string

    type PathContractFact = { Claimed: string; Outcome: PathContractOutcome }

    /// Task-skill-list internal consistency.
    type TaskListOutcome =
        | TaskListConsistent
        | TaskListInconsistent of detail: string

    /// Optional declared mirror state. `NoMirrorDeclared` is NOT an error (FR-005, SC-003).
    type MirrorOutcome =
        | NoMirrorDeclared
        | MirrorInSync
        | MirrorMissing of mirror: string
        | MirrorDrifted of mirror: string * detail: string

    type SkillFacts =
        { SkillId: string
          PathContract: PathContractFact list
          TaskList: TaskListOutcome
          Mirror: MirrorOutcome
          Unreadable: string list }
