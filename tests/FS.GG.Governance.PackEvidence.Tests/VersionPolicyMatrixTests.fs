module FS.GG.Governance.PackEvidence.Tests.VersionPolicyMatrixTests

open Expecto
open FS.GG.Governance.PackEvidence
open FS.GG.Governance.PackEvidence.Model

// SC-001: versionPolicy is a TOTAL comparison against the PACKED version vs the baseline (D1), with
// semantic-version (not lexical) ordering and pre-release / build-metadata edges.

[<Tests>]
let tests =
    testList
        "versionPolicy"
        [ test "packed strictly above baseline ⇒ Bumped" {
              Expect.equal (Pack.versionPolicy (Some "1.2.0") (Some "1.3.0")) (Bumped("1.2.0", "1.3.0")) ""
          }

          test "packed equal to baseline ⇒ Unbumped" {
              Expect.equal (Pack.versionPolicy (Some "1.2.0") (Some "1.2.0")) (Unbumped "1.2.0") ""
          }

          test "packed below baseline ⇒ Downgraded" {
              Expect.equal (Pack.versionPolicy (Some "1.3.0") (Some "1.2.0")) (Downgraded("1.3.0", "1.2.0")) ""
          }

          test "no baseline supplied ⇒ NoBaseline packed" {
              Expect.equal (Pack.versionPolicy None (Some "1.0.0")) (NoBaseline "1.0.0") ""
          }

          test "no packed version ⇒ NotPackable" {
              Expect.equal (Pack.versionPolicy (Some "1.0.0") None) NotPackable ""
              Expect.equal (Pack.versionPolicy None None) NotPackable ""
          }

          test "semantic (not lexical) ordering: 1.10.0 > 1.9.0" {
              Expect.equal (Pack.versionPolicy (Some "1.9.0") (Some "1.10.0")) (Bumped("1.9.0", "1.10.0")) ""
              Expect.equal (Pack.versionPolicy (Some "1.10.0") (Some "1.9.0")) (Downgraded("1.10.0", "1.9.0")) ""
          }

          test "pre-release is lower than its release: 1.0.0-rc.1 < 1.0.0" {
              Expect.equal (Pack.versionPolicy (Some "1.0.0-rc.1") (Some "1.0.0")) (Bumped("1.0.0-rc.1", "1.0.0")) ""
              Expect.equal (Pack.versionPolicy (Some "1.0.0") (Some "1.0.0-rc.1")) (Downgraded("1.0.0", "1.0.0-rc.1")) ""
          }

          test "pre-release identifier ordering: rc.2 > rc.1" {
              Expect.equal
                  (Pack.versionPolicy (Some "1.0.0-rc.1") (Some "1.0.0-rc.2"))
                  (Bumped("1.0.0-rc.1", "1.0.0-rc.2"))
                  ""
          }

          test "build metadata is ignored in precedence (verdict carries the raw packed version)" {
              Expect.equal
                  (Pack.versionPolicy (Some "1.2.0+build.5") (Some "1.2.0+build.9"))
                  (Unbumped "1.2.0+build.9")
                  "equal precedence ⇒ Unbumped, carrying the real packed version string"
          }

          test "missing trailing core segment treated as zero: 1.2 = 1.2.0" {
              Expect.equal (Pack.versionPolicy (Some "1.2") (Some "1.2.0")) (Unbumped "1.2.0") ""
          }

          test "total over arbitrary (even non-numeric) shapes — never throws" {
              Pack.versionPolicy (Some "abc") (Some "x.y.z") |> ignore
              Pack.versionPolicy (Some "") (Some "") |> ignore
              Expect.isTrue true ""
          } ]
