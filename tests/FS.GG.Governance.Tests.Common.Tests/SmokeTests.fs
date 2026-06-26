module FS.GG.Governance.Tests.Common.Tests.SmokeTests

open System.IO
open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.Tests.Common.RepositoryHelpers
open FS.GG.Governance.Tests.Common.CatalogFixtures

// Real-evidence smoke coverage for the shared helpers, exercised directly through the library's PUBLIC
// surface (Principle I/V) — independent of the migrated suites, so the library has standalone coverage.

[<Tests>]
let tests =
    testList
        "Tests.Common.Smoke"
        [ test "RepositoryHelpers.repoRoot locates the real repository root (FS.GG.Governance.sln present)" {
              Expect.isTrue
                  (File.Exists(Path.Combine(repoRoot, "FS.GG.Governance.sln")))
                  "repoRoot must contain the real solution file"
          }

          test "RepositoryHelpers.findRepoRoot fails fast when no marker is found" {
              // Drive the real walk from a temp directory with no solution marker above it on the same path.
              let isolated = DirectoryInfo(Path.GetTempPath())
              // The temp path is outside the repo, so the walk eventually hits a null parent and fails fast.
              Expect.throws (fun () -> findRepoRoot isolated |> ignore) "no marker ⇒ fail fast"
          }

          test "CatalogFixtures.validCatalog validates through the real Loader/Schema edge" {
              // factsOf fails fast on an invalid catalog; a valid one returns real TypedFacts without throwing.
              let facts = factsOf validCatalog
              Expect.isNotNull (box facts) "the valid catalog yields real TypedFacts"
          }

          test "CatalogFixtures.invalidCatalog is rejected by the real Schema (fails fast)" {
              Expect.throws (fun () -> factsOf invalidCatalog |> ignore) "an unsupported schema version ⇒ Invalid"
          } ]
