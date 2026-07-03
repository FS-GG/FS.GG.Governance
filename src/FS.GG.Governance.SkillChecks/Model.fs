// The skill-domain fact vocabulary for F24 (P2) — implementation. Visibility lives in Model.fsi
// (Constitution Principle II); no top-level access modifiers here. Pure data only.

namespace FS.GG.Governance.SkillChecks

open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type PathContractOutcome =
        | PathHolds
        | PathUnresolved of claimed: string
        | PathEscapesBounds of claimed: string

    type PathContractFact = { Claimed: string; Outcome: PathContractOutcome }

    type TaskListOutcome =
        | TaskListConsistent
        | TaskListInconsistent of detail: string

    type MirrorOutcome =
        | NoMirrorDeclared
        | MirrorInSync
        | MirrorMissing of mirror: string
        | MirrorDrifted of mirror: string * detail: string
        | MirrorUnreadable of mirror: string * detail: string

    type SkillFacts =
        { SkillId: string
          PathContract: PathContractFact list
          TaskList: TaskListOutcome
          Mirror: MirrorOutcome
          Unreadable: string list }
