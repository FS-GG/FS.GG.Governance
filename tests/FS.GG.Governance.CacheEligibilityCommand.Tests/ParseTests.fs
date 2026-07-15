module FS.GG.Governance.CacheEligibilityCommand.Tests.ParseTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.CacheEligibilityCommand
open FS.GG.Governance.CacheEligibilityCommand.Tests.Support

// T010 — `parse`: flags → RunRequest, defaults (U2/D8), and every UsageError as a value (never a throw).

let private parseOk argv =
    match Loop.parse argv with
    | Ok r -> r
    | Error e -> failwithf "expected Ok, got %A" e

[<Tests>]
let tests =
    testList
        "Parse"
        [ test "defaults: repo '.', readiness/ paths, DefaultRange scope, Human format (U2)" {
              let r = parseOk []
              Expect.equal r.Repo "." "default repo is ."
              Expect.equal r.Scope Loop.DefaultRange "default scope is DefaultRange"
              Expect.equal r.Format Loop.Human "default format is Human"
              Expect.equal r.CacheOut "readiness/cache-eligibility.json" "default --out"
              Expect.equal r.StorePath "readiness/evidence-reuse.json" "default --store"
              Expect.equal r.UnresolvedOut "readiness/cache-eligibility.unresolved.json" "sidecar derived from --out"
          }

          test "the leading `cache-eligibility` verb is tolerated and dropped" {
              let r = parseOk [ "cache-eligibility"; "--format"; "json" ]
              Expect.equal r.Format Loop.Json "verb dropped, --format parsed"
          }

          test "--repo prefixes the default artifact paths (clean for non-dot repo)" {
              let r = parseOk [ "--repo"; "/tmp/x" ]
              Expect.equal r.CacheOut "/tmp/x/readiness/cache-eligibility.json" "out under repo"
              Expect.equal r.StorePath "/tmp/x/readiness/evidence-reuse.json" "store under repo"
              Expect.equal r.UnresolvedOut "/tmp/x/readiness/cache-eligibility.unresolved.json" "sidecar under repo"
          }

          test "--out drives the sidecar stem, and --store/--format are honored" {
              let r = parseOk [ "--out"; "out/ce.json"; "--store"; "s/store.json"; "--format"; "json" ]
              Expect.equal r.CacheOut "out/ce.json" "explicit --out"
              Expect.equal r.UnresolvedOut "out/ce.unresolved.json" "sidecar shares the --out stem"
              Expect.equal r.StorePath "s/store.json" "explicit --store"
              Expect.equal r.Format Loop.Json "--format json"
          }

          test "--format text is an additive synonym for human (CLI-3)" {
              Expect.equal (parseOk [ "--format"; "text" ]).Format Loop.Human "text maps to the canonical Human format"
              Expect.equal (parseOk [ "--format"; "human" ]).Format Loop.Human "human remains the canonical token"
          }

          test "--paths yields ExplicitPaths (normalized); --since yields Since" {
              match (parseOk [ "--paths"; "src/A.fs"; "src/B.fs" ]).Scope with
              | Loop.ExplicitPaths ps -> Expect.equal ps [ normalizePath "src/A.fs"; normalizePath "src/B.fs" ] "explicit paths normalized"
              | other -> failtestf "expected ExplicitPaths, got %A" other

              match (parseOk [ "--since"; "HEAD~2" ]).Scope with
              | Loop.Since rev -> Expect.equal rev "HEAD~2" "since rev"
              | other -> failtestf "expected Since, got %A" other
          }

          test "every usage error is a value, never a throw" {
              Expect.equal (Loop.parse [ "--bogus" ]) (Error(Loop.UnknownFlag "--bogus")) "unknown flag"
              Expect.equal (Loop.parse [ "--repo" ]) (Error(Loop.MissingValue "--repo")) "missing value"
              Expect.equal (Loop.parse [ "--paths"; "a"; "--since"; "x" ]) (Error Loop.PathsAndSinceTogether) "paths+since"
              Expect.equal (Loop.parse [ "--paths" ]) (Error Loop.EmptyPaths) "empty paths"
              Expect.equal (Loop.parse [ "--format"; "yaml" ]) (Error(Loop.BadFormat "yaml")) "bad format"
          }

          test "exitCode mapping is total: 0/2/3/4" {
              Expect.equal (Loop.exitCode Loop.Success) 0 "success 0"
              Expect.equal (Loop.exitCode Loop.UsageError') 2 "usage 2"
              Expect.equal (Loop.exitCode Loop.InputUnavailable) 3 "input-unavailable 3"
              Expect.equal (Loop.exitCode Loop.ToolError) 4 "tool-error 4"
          } ]
