module FS.GG.Governance.SurfaceChecks.Tests.StandaloneTests

open System
open System.IO
open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.SurfaceChecks.Tests.Support

module SC = FS.GG.Governance.SurfaceChecks.Model
module Docs = FS.GG.Governance.DocsChecks.Model
module DocsInterp = FS.GG.Governance.DocsChecks.Interpreter
module Skill = FS.GG.Governance.SkillChecks.Model
module SkillInterp = FS.GG.Governance.SkillChecks.Interpreter

// T056 — standalone, no-monorepo guard (FR-016): a sensor reading under a product root never resolves a
// source via a `..` escape; such a source is a clear input diagnostic, never a fabricated pass.

let private withStandaloneProduct (body: string -> string -> 'a) : 'a =
    // parent/ (monorepo)  ;  parent/product/ (the standalone product root)
    let parent = Path.Combine(Path.GetTempPath(), "fsgg-standalone-" + Guid.NewGuid().ToString("N"))
    let product = Path.Combine(parent, "product")
    Directory.CreateDirectory(Path.Combine(product, "docs")) |> ignore
    Directory.CreateDirectory(Path.Combine(product, ".claude", "skills", "foo")) |> ignore

    try
        body parent product
    finally
        try
            Directory.Delete(parent, true)
        with _ ->
            ()

[<Tests>]
let tests =
    testList
        "SurfaceChecks.standalone"
        [ test "a docs link escaping the product root via `..` is dangling, never a fabricated pass" {
              withStandaloneProduct (fun parent product ->
                  // A real file exists OUTSIDE the product root (in the monorepo parent).
                  File.WriteAllText(Path.Combine(parent, "outside.md"), "# Secret\n")
                  File.WriteAllText(Path.Combine(product, "docs", "guide.md"), "See [Esc](../outside.md).\n")

                  let req =
                      { (requestForDocs "docs" "docs/guide.md") with EvidenceTag = None }

                  let facts = DocsInterp.senseDocs (DocsInterp.realPort product) req

                  match (List.head facts.Links).Outcome with
                  | Docs.LinkDangling _ -> ()
                  | other -> failtestf "a `..` escape must not resolve, got %A" other)
          }

          test "a skill path escaping the product root is flagged PathEscapesBounds (not silently read)" {
              withStandaloneProduct (fun _ product ->
                  File.WriteAllText(
                      Path.Combine(product, ".claude", "skills", "foo", "SKILL.md"),
                      "path: ../../../outside\n"
                  )

                  let req = requestForSkill "skill-foo" ".claude/skills/foo/SKILL.md"
                  let facts = SkillInterp.senseSkill (SkillInterp.realPort product) req
                  let outcomes = facts.PathContract |> List.map (fun p -> p.Outcome)
                  Expect.contains outcomes (Skill.PathEscapesBounds "../../../outside") "the escape is flagged, never read")
          } ]
