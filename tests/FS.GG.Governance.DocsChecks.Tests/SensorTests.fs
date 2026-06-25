module FS.GG.Governance.DocsChecks.Tests.SensorTests

open System
open System.IO
open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.DocsChecks
open FS.GG.Governance.DocsChecks.Model
open FS.GG.Governance.DocsChecks.Tests.Support

module SC = FS.GG.Governance.SurfaceChecks.Model

let private withTempRepo (body: string -> 'a) : 'a =
    let dir = Path.Combine(Path.GetTempPath(), "fsgg-docs-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory(Path.Combine(dir, "docs")) |> ignore
    Directory.CreateDirectory(Path.Combine(dir, "src")) |> ignore

    try
        body dir
    finally
        try
            Directory.Delete(dir, true)
        with _ ->
            ()

let private req = requestFor "docs" "docs/guide.md" (Some "docs-evidence")

[<Tests>]
let tests =
    testList
        "DocsChecks.sensor"
        [ test "senseDocs over real fixtures resolves live/dead links and present/stale references" {
              withTempRepo (fun repo ->
                  File.WriteAllText(Path.Combine(repo, "docs", "other.md"), "# Other\n")
                  File.WriteAllText(Path.Combine(repo, "src", "Api.fsi"), "module Api\nval ValidSymbol: int\n")

                  File.WriteAllText(
                      Path.Combine(repo, "docs", "guide.md"),
                      "# Guide\nSee [Other](docs/other.md) and [Missing](docs/missing.md).\nRefer to [[ValidSymbol]] and [[GoneSymbol]].\n"
                  )

                  let facts = Interpreter.senseDocs (Interpreter.realPort repo) req

                  let dangling =
                      facts.Links |> List.filter (fun l -> match l.Outcome with | LinkDangling _ -> true | _ -> false)

                  let resolving =
                      facts.Links |> List.filter (fun l -> l.Outcome = LinkResolves)

                  Expect.hasLength resolving 1 "the live link resolves"
                  Expect.hasLength dangling 1 "the dead link dangles"
                  Expect.equal (List.head dangling).Target "docs/missing.md" "names the missing target"

                  let stale =
                      facts.References
                      |> List.filter (fun r -> match r.Outcome with | ReferenceStale _ -> true | _ -> false)

                  let present =
                      facts.References |> List.filter (fun r -> r.Outcome = ReferenceResolves)

                  Expect.hasLength present 1 "ValidSymbol resolves"
                  Expect.hasLength stale 1 "GoneSymbol is stale"
                  Expect.isEmpty facts.Unreadable "readable source")
          }

          test "unreadable source ⇒ recorded in Unreadable, never a fabricated pass (FR-012)" {
              withTempRepo (fun repo ->
                  // No guide.md written ⇒ source not found.
                  let facts = Interpreter.senseDocs (Interpreter.realPort repo) req
                  Expect.isNonEmpty facts.Unreadable "missing source recorded"
                  Expect.isEmpty facts.Links "no fabricated links")
          } ]
