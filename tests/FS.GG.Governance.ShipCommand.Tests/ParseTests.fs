module FS.GG.Governance.ShipCommand.Tests.ParseTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.ShipCommand

// US2 (the parse side) + SC-003/SC-004: `Loop.parse` is pure and TOTAL — it turns argv into a
// normalized RunRequest or a UsageError value, never an exception. Lever recognition happens INSIDE
// parse (research D5), so an unrecognized --mode/--profile is a usage error decided before any port is
// built. Drives the PUBLIC `Loop.parse`.

let private parse argv = Loop.parse argv

[<Tests>]
let tests =
    testList
        "Parse"
        [ test "--paths a b ⇒ ExplicitPaths of the normalized paths (tolerates leading ship verb)" {
              match parse [ "ship"; "--paths"; "a"; "b" ] with
              | Ok req -> Expect.equal req.Scope (Loop.ExplicitPaths [ normalizePath "a"; normalizePath "b" ]) "explicit path scope"
              | Error e -> failtestf "expected Ok, got Error %A" e
          }

          test "--since HEAD~3 ⇒ Since scope" {
              match parse [ "ship"; "--since"; "HEAD~3" ] with
              | Ok req -> Expect.equal req.Scope (Loop.Since "HEAD~3") "since scope"
              | Error e -> failtestf "expected Ok, got Error %A" e
          }

          test "neither --paths nor --since ⇒ DefaultRange" {
              match parse [ "ship" ] with
              | Ok req -> Expect.equal req.Scope Loop.DefaultRange "default base/head scope"
              | Error e -> failtestf "expected Ok, got Error %A" e
          }

          test "--paths a --since X ⇒ PathsAndSinceTogether (mutually exclusive)" {
              Expect.equal (parse [ "ship"; "--paths"; "a"; "--since"; "X" ]) (Error Loop.PathsAndSinceTogether) "exclusive"
          }

          test "--paths with no value ⇒ EmptyPaths" {
              Expect.equal (parse [ "ship"; "--paths" ]) (Error Loop.EmptyPaths) "empty paths"
          }

          test "--mode/--profile recognized via F023; recorded on the request (US2)" {
              match parse [ "ship"; "--mode"; "release"; "--profile"; "strict" ] with
              | Ok req ->
                  Expect.equal req.Mode RunMode.Release "mode release recognized"
                  Expect.equal req.Profile Profile.Strict "profile strict recognized"
              | Error e -> failtestf "expected Ok, got Error %A" e
          }

          test "omitted --mode/--profile default to Gate/Standard (US2 AS4)" {
              match parse [ "ship" ] with
              | Ok req ->
                  Expect.equal req.Mode Gate "default mode gate"
                  Expect.equal req.Profile Standard "default profile standard"
              | Error e -> failtestf "expected Ok, got Error %A" e
          }

          test "unrecognized --mode ⇒ UnrecognizedMode; unrecognized --profile ⇒ UnrecognizedProfile (US2 AS3)" {
              Expect.equal (parse [ "ship"; "--mode"; "bogus" ]) (Error(Loop.UnrecognizedMode "bogus")) "unrecognized mode"
              Expect.equal (parse [ "ship"; "--profile"; "nope" ]) (Error(Loop.UnrecognizedProfile "nope")) "unrecognized profile"
          }

          test "--repo / --audit-out set the fields; AuditOut default derives from --repo (research D7)" {
              match parse [ "ship"; "--repo"; "/tmp/x" ] with
              | Ok req ->
                  Expect.equal req.Repo "/tmp/x" "repo"
                  Expect.equal req.AuditOut "/tmp/x/readiness/audit.json" "audit default under repo"
              | Error e -> failtestf "expected Ok, got Error %A" e

              match parse [ "ship"; "--audit-out"; "out/a.json" ] with
              | Ok req -> Expect.equal req.AuditOut "out/a.json" "explicit audit-out"
              | Error e -> failtestf "expected Ok, got Error %A" e

              // The bare default (repo = ".") yields the clean relative location (research D7).
              match parse [ "ship" ] with
              | Ok req -> Expect.equal req.AuditOut "readiness/audit.json" "default audit-out"
              | Error e -> failtestf "expected Ok, got Error %A" e
          }

          test "--json ⇒ Json format; absent ⇒ Text" {
              match parse [ "ship"; "--json" ], parse [ "ship" ] with
              | Ok j, Ok t ->
                  Expect.equal j.Format Loop.Json "--json ⇒ Json"
                  Expect.equal t.Format Loop.Text "absent ⇒ Text"
              | _ -> failtest "expected both Ok"
          }

          test "an unknown flag ⇒ UnknownFlag; a flag missing its value ⇒ MissingValue" {
              Expect.equal (parse [ "ship"; "--bogus" ]) (Error(Loop.UnknownFlag "--bogus")) "unknown flag"
              Expect.equal (parse [ "ship"; "--repo" ]) (Error(Loop.MissingValue "--repo")) "missing value"
              Expect.equal (parse [ "ship"; "--mode" ]) (Error(Loop.MissingValue "--mode")) "missing mode value"
          } ]
