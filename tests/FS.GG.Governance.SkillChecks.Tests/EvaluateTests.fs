module FS.GG.Governance.SkillChecks.Tests.EvaluateTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.SkillChecks
open FS.GG.Governance.SkillChecks.Model
open FS.GG.Governance.SkillChecks.Tests.Support

module SC = FS.GG.Governance.SurfaceChecks.Model

let private req = requestFor "skill-foo" ".claude/skills/foo/SKILL.md" (Some "skill-evidence")

let private facts pathContract taskList mirror =
    { SkillId = "skill-foo"
      PathContract = pathContract
      TaskList = taskList
      Mirror = mirror
      Unreadable = [] }

[<Tests>]
let tests =
    testList
        "SkillChecks.evaluate"
        [ test "conformant skill (paths hold, list consistent, mirror in sync) ⇒ zero findings" {
              let f = facts [ { Claimed = "scripts/run.sh"; Outcome = PathHolds } ] TaskListConsistent MirrorInSync
              Expect.isEmpty (SkillChecks.evaluate req f) "conformant ⇒ clean"
          }

          test "no mirror declared ⇒ not an error" {
              let f = facts [ { Claimed = "x"; Outcome = PathHolds } ] TaskListConsistent NoMirrorDeclared
              Expect.isEmpty (SkillChecks.evaluate req f) "no-mirror is not an error"
          }

          test "unresolved path ⇒ Blocking skill.path-contract naming skill + path" {
              let f = facts [ { Claimed = "missing.sh"; Outcome = PathUnresolved "missing.sh" } ] TaskListConsistent NoMirrorDeclared
              let finding = List.head (SkillChecks.evaluate req f)
              Expect.equal finding.Code "skill.path-contract" "code"
              Expect.equal finding.BaseSeverity Blocking "Blocking"
              Expect.stringContains finding.Message "skill-foo" "names the skill"
              Expect.stringContains finding.Message "missing.sh" "names the path"
              Expect.equal finding.EvidenceTag (Some(EvidenceTag "skill-evidence")) "carries the tag"
          }

          test "path escapes bounds ⇒ Blocking skill.path-contract" {
              let f = facts [ { Claimed = "../escape"; Outcome = PathEscapesBounds "../escape" } ] TaskListConsistent NoMirrorDeclared
              let finding = List.head (SkillChecks.evaluate req f)
              Expect.equal finding.Code "skill.path-contract" "code"
              Expect.stringContains finding.Message "escapes" "explains the escape"
          }

          test "inconsistent task list ⇒ Blocking skill.task-list" {
              let f = facts [] (TaskListInconsistent "duplicate task 'a'") NoMirrorDeclared
              let finding = List.head (SkillChecks.evaluate req f)
              Expect.equal finding.Code "skill.task-list" "code"
          }

          test "missing mirror ⇒ Blocking skill.mirror" {
              let f = facts [] TaskListConsistent (MirrorMissing "mirror/SKILL.md")
              let finding = List.head (SkillChecks.evaluate req f)
              Expect.equal finding.Code "skill.mirror" "code"
              Expect.stringContains finding.Message "mirror/SKILL.md" "names the mirror"
          }

          test "drifted mirror ⇒ Blocking skill.mirror" {
              let f = facts [] TaskListConsistent (MirrorDrifted("mirror/SKILL.md", "content differs"))
              let finding = List.head (SkillChecks.evaluate req f)
              Expect.equal finding.Code "skill.mirror" "code"
          }

          test "unreadable manifest ⇒ IsInputState skill.manifest-unreadable" {
              let f =
                  { SkillId = "skill-foo"
                    PathContract = []
                    TaskList = TaskListConsistent
                    Mirror = NoMirrorDeclared
                    Unreadable = [ ".claude/skills/foo/SKILL.md: denied" ] }

              let finding = List.head (SkillChecks.evaluate req f)
              Expect.equal finding.Code "skill.manifest-unreadable" "code"
              Expect.isTrue finding.IsInputState "input state"
          } ]
