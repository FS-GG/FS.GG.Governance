module FS.GG.Governance.FreshnessKey.Tests.TotalityTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.FreshnessKey.Tests.Support

// Totality + option/edge semantics (FR-011): every degenerate input is an ordinary, total outcome — no
// exception from compute/matches/diff. Covers zero covered artifacts, absent command/version, base = head,
// and empty-string values; and the option distinctions (None ≠ Some, None/None match).

let private key i = FreshnessKey.value (FreshnessKey.compute i)

[<Tests>]
let tests =
    testList
        "Totality"
        [ test "zero covered artifacts is a total, stable, matchable value" {
              let a = { baseInputs with CoveredArtifacts = [] }
              let b = { baseInputs with CoveredArtifacts = [] }
              Expect.equal (key a) (key b) "two zero-artifact sets with equal inputs match"
              Expect.isTrue (FreshnessKey.matches a b) "zero-artifact matches"
          }

          test "absent command/version is total; None ≠ Some; None/None match (FR-011)" {
              let noneBoth = { baseInputs with Command = None; CommandVersion = None }
              let noneBoth2 = { baseInputs with Command = None; CommandVersion = None }
              Expect.isTrue (FreshnessKey.matches noneBoth noneBoth2) "None/None matches"

              // None command version ≠ some present command version.
              let someVer = { noneBoth with CommandVersion = Some(CommandVersion "1.0") }
              Expect.isFalse (FreshnessKey.matches noneBoth someVer) "None version ≠ Some version"
              Expect.notEqual (key noneBoth) (key someVer) "None version key ≠ Some version key"

              // None command ≠ some present command.
              let someCmd = { noneBoth with Command = Some(CommandId "dotnet") }
              Expect.isFalse (FreshnessKey.matches noneBoth someCmd) "None command ≠ Some command"
          }

          test "base = head is a total, stable value distinct from base ≠ head" {
              let equal = { baseInputs with Base = Revision "same"; Head = Revision "same" }
              let differ = { baseInputs with Base = Revision "same"; Head = Revision "other" }
              Expect.equal (key equal) (key equal) "base=head computes without exception, stably"
              Expect.notEqual (key equal) (key differ) "base=head differs from base≠head"
          }

          test "empty-string values are literal: two empties match, empty ≠ non-empty (FR-011)" {
              let emptyRule = { baseInputs with RuleHash = RuleHash "" }
              let emptyRule2 = { baseInputs with RuleHash = RuleHash "" }
              let nonEmpty = { baseInputs with RuleHash = RuleHash "x" }
              Expect.isTrue (FreshnessKey.matches emptyRule emptyRule2) "two empty rule hashes match"
              Expect.isFalse (FreshnessKey.matches emptyRule nonEmpty) "empty ≠ non-empty rule hash"
          }

          test "compute/matches/diff never throw on a fully-degenerate input" {
              let degenerate: FreshnessInputs =
                  { Check = CheckId ""
                    Domain = DomainId ""
                    Command = None
                    Environment = Release
                    RuleHash = RuleHash ""
                    CoveredArtifacts = []
                    CommandVersion = None
                    GeneratorVersion = GeneratorVersion ""
                    Base = Revision ""
                    Head = Revision "" }

              Expect.isTrue
                  (let _ = FreshnessKey.compute degenerate
                   let _ = FreshnessKey.matches degenerate degenerate
                   let _ = FreshnessKey.diff degenerate degenerate
                   true)
                  "no exception on the degenerate value"
          } ]
