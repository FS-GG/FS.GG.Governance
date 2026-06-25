module FS.GG.Governance.SkillChecks.Tests.DeterminismTests

open Expecto
open FS.GG.Governance.SkillChecks
open FS.GG.Governance.SkillChecks.Model
open FS.GG.Governance.SkillChecks.Tests.Support

module SC = FS.GG.Governance.SurfaceChecks.Model

let private req = requestFor "skill-foo" ".claude/skills/foo/SKILL.md" None

let private mk pathContract =
    { SkillId = "skill-foo"
      PathContract = pathContract
      TaskList = TaskListConsistent
      Mirror = NoMirrorDeclared
      Unreadable = [] }

[<Tests>]
let tests =
    testList
        "SkillChecks.determinism"
        [ test "repeated evaluate over identical facts ⇒ byte-identical" {
              let f = mk [ { Claimed = "b"; Outcome = PathUnresolved "b" }; { Claimed = "a"; Outcome = PathUnresolved "a" } ]
              Expect.equal (SkillChecks.evaluate req f) (SkillChecks.evaluate req f) "deterministic"
          }

          test "reordering the path contract leaves the sorted findings unchanged" {
              let a = { Claimed = "a"; Outcome = PathUnresolved "a" }
              let b = { Claimed = "b"; Outcome = PathEscapesBounds "b" }
              Expect.equal (SkillChecks.evaluate req (mk [ a; b ])) (SkillChecks.evaluate req (mk [ b; a ])) "order-independent"
          } ]
