module FS.GG.Governance.EvidenceCommand.Tests.ParseTests

open Expecto
open FS.GG.Governance.EvidenceCommand

// US1 — `parse` is a pure, total argv normalizer: it tolerates the leading `evidence` verb, accepts
// `--repo`/`--out`/`--format`/`--plain`, and rejects bad input as `UsageError` values (never exceptions).

[<Tests>]
let tests =
    testList
        "Parse"
        [ test "the leading `evidence` verb is tolerated and dropped" {
              match Loop.parse [ "evidence"; "--repo"; "r" ] with
              | Ok req -> Expect.equal req.Repo "r" "repo parsed after the verb"
              | Error e -> failtestf "expected Ok, got %A" e
          }

          test "defaults: repo '.', out under readiness, format Human" {
              match Loop.parse [] with
              | Ok req ->
                  Expect.equal req.Repo "." "default repo"
                  Expect.stringContains req.Out "evidence.json" "default out names evidence.json"
                  Expect.stringContains req.Out "readiness" "default out under readiness"
                  Expect.equal req.Format Loop.Human "default format Human"
              | Error e -> failtestf "expected Ok, got %A" e
          }

          test "--repo / --out / --format json are accepted" {
              match Loop.parse [ "--repo"; "x"; "--out"; "o.json"; "--format"; "json" ] with
              | Ok req ->
                  Expect.equal req.Repo "x" "repo"
                  Expect.equal req.Out "o.json" "out"
                  Expect.equal req.Format Loop.Json "format json"
                  Expect.isFalse req.ExplicitPlain "no plain flag"
              | Error e -> failtestf "expected Ok, got %A" e
          }

          test "--plain is additive and does not override --format json (M-CLI-7)" {
              match Loop.parse [ "--format"; "json"; "--plain" ] with
              | Ok req ->
                  Expect.equal req.Format Loop.Json "plain composes with --format json (Json still wins)"
                  Expect.isTrue req.ExplicitPlain "plain flag set"
              | Error e -> failtestf "expected Ok, got %A" e
          }

          test "an unknown flag is a UsageError, not an exception" {
              match Loop.parse [ "--bogus" ] with
              | Error(Loop.UnknownFlag flag) -> Expect.equal flag "--bogus" "names the unknown flag"
              | other -> failtestf "expected UnknownFlag, got %A" other
          }

          test "a bad --format value is a BadFormat UsageError" {
              match Loop.parse [ "--format"; "yaml" ] with
              | Error(Loop.BadFormat value) -> Expect.equal value "yaml" "names the bad value"
              | other -> failtestf "expected BadFormat, got %A" other
          }

          test "a flag missing its value is a MissingValue UsageError" {
              match Loop.parse [ "--repo" ] with
              | Error(Loop.MissingValue flag) -> Expect.equal flag "--repo" "names the flag missing a value"
              | other -> failtestf "expected MissingValue, got %A" other
          } ]

// M-CLI-3 (#49): a `--`-prefixed token following a value-option is NOT its value — reject as MissingValue
// rather than swallowing the following flag (covers the combined-arm parser variant).
[<Tests>]
let argvValueGuard =
    testList
        "ArgvValueGuard-MCLI3"
        [ test "--repo followed by a flag ⇒ MissingValue, not swallowed" {
              match Loop.parse [ "--repo"; "--format" ] with
              | Error(Loop.MissingValue flag) -> Expect.equal flag "--repo" "names --repo"
              | other -> failtestf "expected MissingValue, got %A" other
          }

          test "--format followed by a flag ⇒ MissingValue, not swallowed" {
              match Loop.parse [ "--format"; "--plain" ] with
              | Error(Loop.MissingValue flag) -> Expect.equal flag "--format" "names --format"
              | other -> failtestf "expected MissingValue, got %A" other
          }

          test "a valid --repo value then --plain still parses" {
              match Loop.parse [ "--repo"; "r"; "--plain" ] with
              | Ok req ->
                  Expect.equal req.Repo "r" "repo value bound"
                  Expect.isTrue req.ExplicitPlain "trailing --plain still set"
              | Error e -> failtestf "expected Ok, got %A" e
          } ]
