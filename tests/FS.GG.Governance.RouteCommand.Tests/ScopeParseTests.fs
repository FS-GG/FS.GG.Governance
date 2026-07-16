module FS.GG.Governance.RouteCommand.Tests.ScopeParseTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.RouteCommand

// US2 (the scope selector parse side) + SC-003: `Loop.parse` is pure and TOTAL — it turns argv into a
// normalized RunRequest or a UsageError value, never an exception. Drives the PUBLIC `Loop.parse`.

let private parse argv = Loop.parse argv

[<Tests>]
let tests =
    testList
        "ScopeParse"
        [ test "--paths a b ⇒ ExplicitPaths of the normalized paths (bypasses git)" {
              match parse [ "route"; "--paths"; "a"; "b" ] with
              | Ok req -> Expect.equal req.Scope (Loop.ExplicitPaths [ normalizePath "a"; normalizePath "b" ]) "explicit path scope"
              | Error e -> failtestf "expected Ok, got Error %A" e
          }

          test "--since HEAD~3 ⇒ Since scope" {
              match parse [ "route"; "--since"; "HEAD~3" ] with
              | Ok req -> Expect.equal req.Scope (Loop.Since "HEAD~3") "since scope"
              | Error e -> failtestf "expected Ok, got Error %A" e
          }

          test "neither --paths nor --since ⇒ DefaultRange" {
              match parse [ "route" ] with
              | Ok req -> Expect.equal req.Scope Loop.DefaultRange "default base/head scope"
              | Error e -> failtestf "expected Ok, got Error %A" e
          }

          test "--paths a --since X ⇒ PathsAndSinceTogether (mutually exclusive)" {
              Expect.equal (parse [ "route"; "--paths"; "a"; "--since"; "X" ]) (Error Loop.PathsAndSinceTogether) "exclusive"
          }

          test "--paths with no value ⇒ EmptyPaths" {
              Expect.equal (parse [ "route"; "--paths" ]) (Error Loop.EmptyPaths) "empty paths"
          }

          test "--repo / --gates-out / --route-out set the fields; defaults derive from --repo" {
              match parse [ "route"; "--repo"; "/tmp/x" ] with
              | Ok req ->
                  Expect.equal req.Repo "/tmp/x" "repo"
                  Expect.equal req.GatesOut "/tmp/x/.fsgg/gates.json" "gates default under repo"
                  Expect.equal req.RouteOut "/tmp/x/readiness/route.json" "route default under repo"
              | Error e -> failtestf "expected Ok, got Error %A" e

              match parse [ "route"; "--gates-out"; "g.json"; "--route-out"; "r.json" ] with
              | Ok req ->
                  Expect.equal req.GatesOut "g.json" "explicit gates-out"
                  Expect.equal req.RouteOut "r.json" "explicit route-out"
              | Error e -> failtestf "expected Ok, got Error %A" e

              // The bare default (repo = ".") yields the clean relative locations (research D5).
              match parse [ "route" ] with
              | Ok req ->
                  Expect.equal req.GatesOut ".fsgg/gates.json" "default gates-out"
                  Expect.equal req.RouteOut "readiness/route.json" "default route-out"
              | Error e -> failtestf "expected Ok, got Error %A" e
          }

          test "--json ⇒ Json format; absent ⇒ Text" {
              match parse [ "route"; "--json" ], parse [ "route" ] with
              | Ok j, Ok t ->
                  Expect.equal j.Format Loop.Json "--json ⇒ Json"
                  Expect.equal t.Format Loop.Text "absent ⇒ Text"
              | _ -> failtest "expected both Ok"
          }

          test "an unknown flag ⇒ UnknownFlag; a flag missing its value ⇒ MissingValue" {
              Expect.equal (parse [ "route"; "--bogus" ]) (Error(Loop.UnknownFlag "--bogus")) "unknown flag"
              Expect.equal (parse [ "route"; "--repo" ]) (Error(Loop.MissingValue "--repo")) "missing value"
          }

          test "CLI-5: a stray non-`--` positional ⇒ UnexpectedArgument, not UnknownFlag" {
              Expect.equal (parse [ "route"; "junk" ]) (Error(Loop.UnexpectedArgument "junk")) "stray positional rejected as UnexpectedArgument"
          }

          // ── F046 (U1): --store parse + default + missing-value-is-a-value ──

          test "--store sets StorePath; omitted ⇒ default <repo>/readiness/evidence-reuse.json" {
              match parse [ "route"; "--store"; "s.json" ] with
              | Ok req -> Expect.equal req.StorePath "s.json" "explicit --store"
              | Error e -> failtestf "expected Ok, got Error %A" e

              match parse [ "route" ] with
              | Ok req -> Expect.equal req.StorePath "readiness/evidence-reuse.json" "default store (repo = .)"
              | Error e -> failtestf "expected Ok, got Error %A" e

              match parse [ "route"; "--repo"; "/tmp/x" ] with
              | Ok req -> Expect.equal req.StorePath "/tmp/x/readiness/evidence-reuse.json" "default store under --repo"
              | Error e -> failtestf "expected Ok, got Error %A" e
          }

          test "--store with no value ⇒ a UsageError VALUE, never a throw" {
              Expect.equal (parse [ "route"; "--store" ]) (Error(Loop.MissingValue "--store")) "missing --store value is a value"
          } ]

// M-CLI-3 (#49): a `--`-prefixed token following a value-option is NOT its value — the parser must reject it
// as MissingValue instead of silently swallowing the following flag (`--repo --json` used to set repo="--json").
[<Tests>]
let argvValueGuard =
    testList
        "ArgvValueGuard-MCLI3"
        [ test "--repo followed by a flag ⇒ MissingValue, the flag is not swallowed" {
              Expect.equal (parse [ "route"; "--repo"; "--json" ]) (Error(Loop.MissingValue "--repo")) "flag not swallowed as --repo value"
          }

          test "--repo with a real value then --json still parses; JSON mode is set" {
              match parse [ "route"; "--repo"; "acme/x"; "--json" ] with
              | Ok req ->
                  Expect.equal req.Repo "acme/x" "repo value bound"
                  Expect.equal req.Format Loop.Json "trailing --json still selects JSON mode"
              | Error e -> failtestf "expected Ok, got Error %A" e
          } ]
