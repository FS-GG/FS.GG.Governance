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
          }

          test "a link escaping to a sibling dir prefixed by the repo name dangles, never a fabricated pass (FR-016)" {
              withTempRepo (fun repo ->
                  // Sibling directory whose name STARTS WITH the repo dir name (`<repo>-sibling`), holding a
                  // real file. A bare `StartsWith repoRoot` boundary would resolve `../<sibling>/secret.md`
                  // as "inside" — fabricating a pass against a file OUTSIDE the standalone product. The
                  // trailing-separator guard must dangle it.
                  let sibling = repo + "-sibling"
                  Directory.CreateDirectory sibling |> ignore

                  try
                      File.WriteAllText(Path.Combine(sibling, "secret.md"), "# outside the product\n")
                      let siblingName = Path.GetFileName sibling

                      File.WriteAllText(
                          Path.Combine(repo, "docs", "guide.md"),
                          sprintf "# Guide\nSee [Escape](../%s/secret.md).\n" siblingName
                      )

                      let facts = Interpreter.senseDocs (Interpreter.realPort repo) req

                      let dangling =
                          facts.Links |> List.filter (fun l -> match l.Outcome with | LinkDangling _ -> true | _ -> false)

                      Expect.hasLength dangling 1 "the escaping sibling link dangles, not a fabricated pass"
                      Expect.equal (List.head dangling).Target (sprintf "../%s/secret.md" siblingName) "names the escaping target"
                  finally
                      try
                          Directory.Delete(sibling, true)
                      with _ ->
                          ())
          } ]
