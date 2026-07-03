module FS.GG.Governance.SkillChecks.Tests.SensorTests

open System
open System.IO
open Expecto
open FS.GG.Governance.SkillChecks
open FS.GG.Governance.SkillChecks.Model
open FS.GG.Governance.SkillChecks.Tests.Support

module SC = FS.GG.Governance.SurfaceChecks.Model

let private skillRel = ".claude/skills/foo"

let private withTempRepo (body: string -> 'a) : 'a =
    let dir = Path.Combine(Path.GetTempPath(), "fsgg-skill-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory(Path.Combine(dir, skillRel)) |> ignore

    try
        body dir
    finally
        try
            Directory.Delete(dir, true)
        with _ ->
            ()

let private writeManifest (repo: string) (content: string) =
    File.WriteAllText(Path.Combine(repo, skillRel, "SKILL.md"), content)

let private req = requestFor "skill-foo" (skillRel + "/SKILL.md") (Some "skill-evidence")

[<Tests>]
let tests =
    testList
        "SkillChecks.sensor"
        [ test "conformant skill ⇒ paths hold, list consistent, mirror in sync" {
              withTempRepo (fun repo ->
                  let content = "path: scripts/run.sh\ntask: build\ntask: test\nmirror: mirror.md\n"
                  writeManifest repo content
                  Directory.CreateDirectory(Path.Combine(repo, skillRel, "scripts")) |> ignore
                  File.WriteAllText(Path.Combine(repo, skillRel, "scripts", "run.sh"), "#!/bin/sh\n")
                  File.WriteAllText(Path.Combine(repo, skillRel, "mirror.md"), content)

                  let facts = Interpreter.senseSkill (Interpreter.realPort repo) req
                  Expect.equal facts.PathContract [ { Claimed = "scripts/run.sh"; Outcome = PathHolds } ] "path holds"
                  Expect.equal facts.TaskList TaskListConsistent "list consistent"
                  Expect.equal facts.Mirror MirrorInSync "mirror in sync"
                  Expect.isEmpty facts.Unreadable "readable")
          }

          test "broken skill ⇒ unresolved + escaping paths, inconsistent list, missing mirror" {
              withTempRepo (fun repo ->
                  writeManifest repo "path: ghost.sh\npath: ../escape\ntask: dup\ntask: dup\nmirror: missing.md\n"
                  let facts = Interpreter.senseSkill (Interpreter.realPort repo) req

                  let outcomes = facts.PathContract |> List.map (fun p -> p.Outcome)
                  Expect.contains outcomes (PathUnresolved "ghost.sh") "ghost.sh unresolved"
                  Expect.contains outcomes (PathEscapesBounds "../escape") "../escape escapes bounds"

                  match facts.TaskList with
                  | TaskListInconsistent _ -> ()
                  | other -> failtestf "expected inconsistent task list, got %A" other

                  Expect.equal facts.Mirror (MirrorMissing "missing.md") "mirror missing")
          }

          test "drifted mirror ⇒ MirrorDrifted" {
              withTempRepo (fun repo ->
                  writeManifest repo "path: scripts/run.sh\nmirror: mirror.md\n"
                  Directory.CreateDirectory(Path.Combine(repo, skillRel, "scripts")) |> ignore
                  File.WriteAllText(Path.Combine(repo, skillRel, "scripts", "run.sh"), "x")
                  File.WriteAllText(Path.Combine(repo, skillRel, "mirror.md"), "DIFFERENT CONTENT\n")
                  let facts = Interpreter.senseSkill (Interpreter.realPort repo) req

                  match facts.Mirror with
                  | MirrorDrifted("mirror.md", _) -> ()
                  | other -> failtestf "expected MirrorDrifted, got %A" other)
          }

          test "unreadable manifest ⇒ recorded in Unreadable (FR-012)" {
              withTempRepo (fun repo ->
                  // No SKILL.md written.
                  let facts = Interpreter.senseSkill (Interpreter.realPort repo) req
                  Expect.isNonEmpty facts.Unreadable "missing manifest recorded")
          }

          test "a `..` INSIDE a filename is not a bounds escape (#56/B12)" {
              withTempRepo (fun repo ->
                  // `notes..md` contains ".." as a substring but not as a path SEGMENT — it must resolve
                  // normally, unlike `../escape`. The old `Contains ".."` check over-rejected it.
                  writeManifest repo "path: notes..md\npath: ../escape\n"
                  File.WriteAllText(Path.Combine(repo, skillRel, "notes..md"), "x")
                  let facts = Interpreter.senseSkill (Interpreter.realPort repo) req
                  let outcomes = facts.PathContract |> List.map (fun p -> p.Outcome)
                  Expect.contains outcomes (PathHolds) "notes..md holds (not an escape)"
                  Expect.contains outcomes (PathEscapesBounds "../escape") "../escape still escapes")
          }

          test "a declared mirror that cannot be read ⇒ MirrorUnreadable, not NoMirrorDeclared (#56/B12)" {
              // A mirror IS declared but the port errors reading it. The disposition must say so — mislabelling
              // it `NoMirrorDeclared` (the old behaviour) would claim no mirror was declared at all.
              let port: Interpreter.SkillPort =
                  { ReadManifest = fun _ -> Ok "mirror: m.md\n"
                    ResolvePath = fun _ -> Ok true
                    ReadMirror = fun _ -> Error "mirror read exploded" }

              let facts = Interpreter.senseSkill port req

              match facts.Mirror with
              | MirrorUnreadable("m.md", detail) -> Expect.stringContains detail "exploded" "carries the read error"
              | other -> failtestf "expected MirrorUnreadable, got %A" other

              Expect.isNonEmpty facts.Unreadable "the read error is also recorded in Unreadable"
          } ]
