module FS.GG.Governance.ReleaseCommand.Tests.ParseTests

open Expecto
open FS.GG.Governance.ReleaseCommand

// `Loop.parse` is PURE and TOTAL: argv in, `Result<RunRequest, UsageError>` out, NO I/O on rejection
// (cli.md invocation table). It consumes the FLAGS ONLY — no leading `release` subcommand token is
// expected or stripped (cli.md §subcommand mapping).

let private ok argv =
    match Loop.parse argv with
    | Ok r -> r
    | Error e -> failtestf "expected Ok, got Error: %s" e.Message

let private isError argv =
    match Loop.parse argv with
    | Error _ -> true
    | Ok _ -> false

[<Tests>]
let tests =
    testList
        "Parse"
        [ test "valid --repo parses with text default and repo-relative release.json out" {
              let r = ok [ "--repo"; "/x" ]
              Expect.equal r.Repo "/x" "repo"
              Expect.equal r.Format Loop.Text "default format is text"
              Expect.equal r.ReleaseOut "/x/release.json" "default out is <repo>/release.json"
          }

          test "a '.' repo yields the clean relative release.json default" {
              let r = ok [ "--repo"; "." ]
              Expect.equal r.ReleaseOut "release.json" "clean relative default"
          }

          test "--format both maps to TextAndJson; json maps to Json" {
              Expect.equal (ok [ "--repo"; "/x"; "--format"; "both" ]).Format Loop.TextAndJson "both"
              Expect.equal (ok [ "--repo"; "/x"; "--format"; "json" ]).Format Loop.Json "json"
              Expect.equal (ok [ "--repo"; "/x"; "--format"; "text" ]).Format Loop.Text "text"
          }

          test "--out overrides the artifact destination" {
              Expect.equal (ok [ "--repo"; "/x"; "--out"; "/o.json" ]).ReleaseOut "/o.json" "explicit out"
          }

          test "missing --repo is a usage error" { Expect.isTrue (isError [ "--format"; "text" ]) "no --repo" }

          test "an unknown flag is a usage error" { Expect.isTrue (isError [ "--repo"; "/x"; "--bogus" ]) "unknown flag" }

          test "a malformed --format value is a usage error" {
              Expect.isTrue (isError [ "--repo"; "/x"; "--format"; "xml" ]) "bad format"
          }

          test "--repo with no value is a usage error" { Expect.isTrue (isError [ "--repo" ]) "missing value" }

          test "a leading bare 'release' token is an unknown argument (flags-only contract)" {
              // No central fsgg dispatcher: parse does not expect/strip a leading `release` token.
              Expect.isTrue (isError [ "release"; "--repo"; "/x" ]) "leading release rejected"
              // ...while the flags-only argv parses Ok.
              Expect.equal (ok [ "--repo"; "/x" ]).Repo "/x" "flags-only Ok"
          } ]
