// The pure skill rule pack for F24 (P2). Visibility lives in SkillChecks.fsi (Constitution Principle II);
// this file carries NO top-level access modifiers. PURE and TOTAL (FR-007): no I/O, no clock, never throws;
// byte-identical findings for identical facts (FR-010, SC-005).

namespace FS.GG.Governance.SkillChecks

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.SkillChecks.Model

module SC = FS.GG.Governance.SurfaceChecks.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SkillChecks =

    // Fixed pre-PR enforcement maturity (see PackageChecks.checkMaturity for the rationale).
    let checkMaturity = BlockOnPr

    // 111/A6: delegates to the shared `SC.mkFinding`, binding this pack's domain + maturity + path-source.
    let mkFinding
        (request: SC.SurfaceCheckRequest)
        (code: string)
        (detail: string)
        (severity: Severity)
        (isInput: bool)
        (message: string)
        : SC.SurfaceFinding =
        SC.mkFinding SC.SkillDomain checkMaturity request request.Path code detail severity isInput message

    let pathFindings (request: SC.SurfaceCheckRequest) (facts: SkillFacts) : SC.SurfaceFinding list =
        facts.PathContract
        |> List.choose (fun p ->
            match p.Outcome with
            | PathHolds -> None
            | PathUnresolved claimed ->
                let message = sprintf "skill '%s' claims path '%s' that does not resolve" facts.SkillId claimed
                Some(mkFinding request "skill.path-contract" claimed Blocking false message)
            | PathEscapesBounds claimed ->
                let message =
                    sprintf "skill '%s' claims path '%s' that escapes its declared bounds" facts.SkillId claimed

                Some(mkFinding request "skill.path-contract" claimed Blocking false message))

    let taskListFindings (request: SC.SurfaceCheckRequest) (facts: SkillFacts) : SC.SurfaceFinding list =
        match facts.TaskList with
        | TaskListConsistent -> []
        | TaskListInconsistent detail ->
            let message = sprintf "skill '%s' has an inconsistent task list: %s" facts.SkillId detail
            [ mkFinding request "skill.task-list" "task-list" Blocking false message ]

    let mirrorFindings (request: SC.SurfaceCheckRequest) (facts: SkillFacts) : SC.SurfaceFinding list =
        match facts.Mirror with
        | NoMirrorDeclared
        | MirrorInSync -> []
        | MirrorMissing mirror ->
            let message = sprintf "skill '%s' declares mirror '%s' that is missing" facts.SkillId mirror
            [ mkFinding request "skill.mirror" mirror Blocking false message ]
        | MirrorDrifted(mirror, detail) ->
            let message = sprintf "skill '%s' mirror '%s' has drifted: %s" facts.SkillId mirror detail
            [ mkFinding request "skill.mirror" mirror Blocking false message ]
        | MirrorUnreadable(mirror, detail) ->
            let message = sprintf "skill '%s' declares mirror '%s' that could not be read: %s" facts.SkillId mirror detail
            [ mkFinding request "skill.mirror" mirror Blocking true message ]

    let unreadableFindings (request: SC.SurfaceCheckRequest) (facts: SkillFacts) : SC.SurfaceFinding list =
        facts.Unreadable
        |> List.map (fun src ->
            let message = sprintf "skill '%s' source could not be read: %s" facts.SkillId src
            mkFinding request "skill.manifest-unreadable" src Blocking true message)

    let evaluate (request: SC.SurfaceCheckRequest) (facts: SkillFacts) : SC.SurfaceFinding list =
        [ pathFindings request facts
          taskListFindings request facts
          mirrorFindings request facts
          unreadableFindings request facts ]
        |> List.concat
        |> List.sortBy (fun (f: SC.SurfaceFinding) -> facts.SkillId, f.Location.Detail, f.Code)
