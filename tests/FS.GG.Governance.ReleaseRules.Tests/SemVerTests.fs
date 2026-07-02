module FS.GG.Governance.ReleaseRules.Tests.SemVerTests

open Expecto
open FS.GG.Governance.ReleaseRules

// M-ADPT-1: the single shared semantic-version comparator. These lock in the semantics that the pack verdict
// (PackEvidence) and the F054 release-facts sensing (ReleaseFactsSensing) NOW share — in particular the
// pre-release / build-metadata cases where the old dotted-numeric comparator in Sensing disagreed with Pack
// and produced a contradictory VersionBump verdict for the same release family.

let private lt a b =
    Expect.isLessThan (sign (SemVer.compareVersions a b)) 0 (sprintf "%s should sort BELOW %s" a b)

let private gt a b =
    Expect.isGreaterThan (sign (SemVer.compareVersions a b)) 0 (sprintf "%s should sort ABOVE %s" a b)

let private eq a b =
    Expect.equal (SemVer.compareVersions a b) 0 (sprintf "%s should compare EQUAL to %s" a b)

[<Tests>]
let tests =
    testList
        "SemVerTests"
        [ test "numeric core segments compare numerically, not lexically (1.10.0 > 1.9.0)" {
              gt "1.10.0" "1.9.0"
              lt "1.9.0" "1.10.0"
          }

          test "missing trailing segments count as 0 (1.2 = 1.2.0)" {
              eq "1.2" "1.2.0"
              eq "1.0" "1.0.0"
          }

          // The load-bearing divergence: the OLD Sensing comparator split on '.', coerced `0-alpha` to 0, and
          // ranked `2.0.0-alpha.1` ABOVE `2.0.0`; the correct (Pack) semantics rank a pre-release BELOW its
          // release. Both producers now agree.
          test "a pre-release ranks below its release (2.0.0-alpha.1 < 2.0.0)" {
              lt "2.0.0-alpha.1" "2.0.0"
              gt "2.0.0" "2.0.0-alpha.1"
          }

          test "pre-release identifiers compare per semver precedence" {
              lt "1.0.0-alpha" "1.0.0-beta"
              lt "1.0.0-alpha.1" "1.0.0-alpha.2"
              // a shorter pre-release set precedes a longer one that shares its prefix
              lt "1.0.0-alpha" "1.0.0-alpha.1"
              // numeric identifiers rank below alphanumeric ones
              lt "1.0.0-1" "1.0.0-alpha"
          }

          // The OLD Sensing comparator kept `+build` in a segment (`0+build` → 0) and could rank a
          // build-metadata version differently; build metadata is not part of precedence.
          test "build metadata is ignored (1.0.0+build.5 = 1.0.0)" {
              eq "1.0.0+build.5" "1.0.0"
              eq "1.2.3+abc" "1.2.3+xyz"
          }

          test "identical versions compare equal; the comparator is antisymmetric" {
              eq "1.2.3" "1.2.3"

              for a, b in [ "1.0.0", "2.0.0"; "1.0.0-rc.1", "1.0.0"; "1.9.0", "1.10.0" ] do
                  Expect.equal
                      (sign (SemVer.compareVersions a b))
                      (-(sign (SemVer.compareVersions b a)))
                      (sprintf "compare %s %s should be the negation of compare %s %s" a b b a)
          }

          test "the comparator is total — non-numeric / empty segments never throw" {
              SemVer.compareVersions "" "1.0.0" |> ignore
              SemVer.compareVersions "not-a-version" "1.0.0" |> ignore
              SemVer.compareVersions "1.x.0" "1.0.0" |> ignore
          } ]
