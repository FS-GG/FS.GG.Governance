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

          test "typed symbol read failures are preserved, never reclassified as stale symbols" {
              let cases =
                  [ Interpreter.PermissionDenied, "permission-denied", "access denied"
                    Interpreter.InvalidEncoding, "invalid-encoding", "bad utf-8"
                    Interpreter.TransientIo, "transient-io", "sharing violation" ]

              for kind, token, detail in cases do
                  let error: Interpreter.DocsReadError =
                      { Path = "src/Unreadable.fsi"
                        Kind = kind
                        Detail = detail }

                  let port: Interpreter.DocsPort =
                      { ReadSource = fun _ -> Ok "# Guide\n[[ExpectedSymbol]]\n"
                        ResolveTarget = fun _ -> true
                        ResolveSymbol = fun _ -> Error [ error ] }

                  let facts = Interpreter.senseDocs port req

                  Expect.isEmpty facts.References (sprintf "%s does not fabricate ReferenceStale" token)
                  Expect.hasLength facts.Unreadable 1 (sprintf "%s is preserved as an input diagnostic" token)
                  Expect.stringContains facts.Unreadable.Head "src/Unreadable.fsi" "diagnostic names the unreadable input"
                  Expect.stringContains facts.Unreadable.Head token "diagnostic preserves the typed failure kind"
                  Expect.stringContains facts.Unreadable.Head detail "diagnostic preserves the underlying detail"
          }

          test "real symbol scan reports invalid UTF-8 instead of a stale symbol" {
              withTempRepo (fun repo ->
                  File.WriteAllText(Path.Combine(repo, "docs", "guide.md"), "# Guide\n[[ExpectedSymbol]]\n")
                  File.WriteAllBytes(Path.Combine(repo, "src", "Corrupt.fsi"), [| 0xC3uy; 0x28uy |])

                  let facts = Interpreter.senseDocs (Interpreter.realPort repo) req

                  Expect.isEmpty facts.References "an incomplete real scan produces no symbol verdict"
                  Expect.hasLength facts.Unreadable 1 "the corrupt .fsi is disclosed"
                  Expect.stringContains facts.Unreadable.Head "src/Corrupt.fsi" "diagnostic names the corrupt input"
                  Expect.stringContains facts.Unreadable.Head "invalid-encoding" "diagnostic classifies malformed UTF-8")
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
