module FS.GG.Governance.VerifyCommand.Tests.ParseTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.VerifyCommand

// T012 (US1) — `Loop.parse`: pure, total argv normalization. Usage problems are `UsageError` values (exit 2),
// never exceptions; recognition (profile) happens IN parse so a typo writes no artifact. No `--mode` flag
// (FR-017). A leading `verify` verb is tolerated.

let private ok argv =
    match Loop.parse argv with
    | Ok r -> r
    | Error e -> failtestf "expected Ok, got Error %A" e

let private err argv =
    match Loop.parse argv with
    | Ok r -> failtestf "expected Error, got Ok %A" r
    | Error e -> e

[<Tests>]
let tests =
    testList
        "Parse (US1)"
        [ test "bare argv yields the documented defaults" {
              let r = ok []
              Expect.equal r.Repo "." "repo default ."
              Expect.equal r.Profile Standard "profile default Standard"
              Expect.equal r.Format Loop.Text "format default Text"
              Expect.equal r.Scope Loop.DefaultRange "scope default DefaultRange"
              Expect.equal r.VerifyOut "readiness/verify.json" "verify-out default"
              Expect.equal r.StorePath "readiness/evidence-reuse.json" "store default"
              Expect.isFalse r.PersistStore "persist default off"
          }

          test "a leading bare `verify` verb is tolerated and dropped" {
              Expect.equal (ok [ "verify" ]) (ok []) "leading verify == bare"
              Expect.equal (ok [ "verify"; "--json" ]).Format Loop.Json "verify --json parses"
          }

          test "--json selects Json; --persist-store opts in; --profile strict recognized" {
              let r = ok [ "--json"; "--persist-store"; "--profile"; "strict" ]
              Expect.equal r.Format Loop.Json "json"
              Expect.isTrue r.PersistStore "persist on"
              Expect.equal r.Profile Strict "strict profile"
          }

          test "--repo prefixes the default artifact paths" {
              let r = ok [ "--repo"; "/work/app" ]
              Expect.equal r.VerifyOut "/work/app/readiness/verify.json" "verify-out under repo"
              Expect.equal r.StorePath "/work/app/readiness/evidence-reuse.json" "store under repo"
          }

          test "--paths normalizes and sets ExplicitPaths; --since sets Since" {
              match (ok [ "--paths"; "src/a.fs"; "src/b.fs" ]).Scope with
              | Loop.ExplicitPaths ps -> Expect.equal (List.length ps) 2 "two paths"
              | other -> failtestf "expected ExplicitPaths, got %A" other

              match (ok [ "--since"; "HEAD~1" ]).Scope with
              | Loop.Since rev -> Expect.equal rev "HEAD~1" "since rev"
              | other -> failtestf "expected Since, got %A" other
          }

          test "--verify-out / --store overrides are honored" {
              let r = ok [ "--verify-out"; "out/v.json"; "--store"; "out/s.json" ]
              Expect.equal r.VerifyOut "out/v.json" "verify-out override"
              Expect.equal r.StorePath "out/s.json" "store override"
          }

          test "an unknown flag, a missing value, paths+since, empty paths, and a bad profile are usage errors" {
              Expect.equal (err [ "--nope" ]) (Loop.UnknownFlag "--nope") "unknown flag"
              Expect.equal (err [ "--repo" ]) (Loop.MissingValue "--repo") "missing value"
              Expect.equal (err [ "--paths"; "a"; "--since"; "x" ]) Loop.PathsAndSinceTogether "paths+since"
              Expect.equal (err [ "--paths" ]) Loop.EmptyPaths "empty paths"

              match err [ "--profile"; "bogus" ] with
              | Loop.UnrecognizedProfile p -> Expect.equal p "bogus" "bad profile carries the token"
              | other -> failtestf "expected UnrecognizedProfile, got %A" other
          }

          test "there is NO --mode flag: --mode is an UnknownFlag (FR-017, verify cannot escalate to Gate)" {
              Expect.equal (err [ "--mode"; "gate" ]) (Loop.UnknownFlag "--mode") "--mode is unknown"
          }

          test "CLI-5: a non-verify leading positional is an UnexpectedArgument, an unknown --flag stays UnknownFlag" {
              Expect.equal (err [ "ship" ]) (Loop.UnexpectedArgument "ship") "stray positional rejected as UnexpectedArgument"
              Expect.equal (err [ "--nope" ]) (Loop.UnknownFlag "--nope") "unknown --flag stays UnknownFlag" } ]
