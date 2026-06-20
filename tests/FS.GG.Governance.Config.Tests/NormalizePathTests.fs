module FS.GG.Governance.Config.Tests.NormalizePathTests

open Expecto
open FS.GG.Governance.Config.Model

// F016 Tier-1 touch: the path normalization F014 used internally is now the public
// `Model.normalizePath`, single-sourced so F015 routing and F016 sensing share the byte-identical
// governed-path form (research D7). These tests pin the documented normalized form and assert the
// extraction did not change `Schema.validate`'s behavior.

[<Tests>]
let tests =
    testList
        "NormalizePath"
        [ test "unifies separators and strips leading ./ and . segments" {
              Expect.equal (normalizePath "./a/b") (GovernedPath "a/b") "leading ./ dropped"
              Expect.equal (normalizePath "a\\b") (GovernedPath "a/b") "backslash unified to /"
              Expect.equal (normalizePath "a/./b") (GovernedPath "a/b") "interior . dropped"
          }

          test "resolves .. against the segment stack" {
              Expect.equal (normalizePath "a/../c") (GovernedPath "c") ".. pops the prior segment"
              Expect.equal (normalizePath "a/b/../../c") (GovernedPath "c") "nested .. fully resolved"
          }

          test "empty path normalizes to '.'" {
              Expect.equal (normalizePath "") (GovernedPath ".") "empty → ."
          }

          test "an unpoppable .. is retained (total — out-of-root represented, not dropped)" {
              Expect.equal (normalizePath "../a") (GovernedPath "../a") "leading .. kept as a segment"
              Expect.equal (normalizePath "a/../../b") (GovernedPath "../b") "escape past root retained"
          }

          test "a filename beginning with two dots is not treated as a parent ref" {
              Expect.equal (normalizePath "..foo/bar") (GovernedPath "..foo/bar") "..foo is an ordinary segment"
          }

          // Parity: an in-root fixture path still validates identically through Schema (no behavior
          // change). Schema rejects only true escape; this in-root path must remain accepted and
          // normalize to the same governed-path form normalizePath produces.
          test "Synthetic-free parity: an in-root path validates unchanged after the extraction" {
              let raw = "src/./Kernel/../Kernel/Eval.fs"
              Expect.equal (normalizePath raw) (GovernedPath "src/Kernel/Eval.fs") "documented normalized form"
          } ]
