module FS.GG.Governance.FreshnessKey.Tests.DeterminismTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.FreshnessKey.Tests.Support

// Determinism, reflexive match, covered-artifact SET semantics, and the committed golden table
// (SC-001, SC-002). The key is a byte-stable fingerprint: identical inputs ⇒ byte-identical key, every
// time; reordering/duplicating covered artifacts changes nothing.

// The worked example from contracts/freshness-key-format.md, segment-by-segment, joined by '\n'.
let private goldenWorkedExample =
    String.concat
        "\n"
        [ "check=111:build:tests"
          "domain=15:build"
          "cmd=16:dotnet"
          "env=15:local"
          "rule=12:r1"
          "art=2;2:h1;2:h2"
          "cmdv=13:8.0"
          "genv=12:g1"
          "base=13:aaa"
          "head=13:bbb" ]

// A fully-degenerate input: empty strings, no command/version, zero covered artifacts.
let private minimalInputs: FreshnessInputs =
    { Check = CheckId ""
      Domain = DomainId ""
      Command = None
      Environment = Local
      RuleHash = RuleHash ""
      CoveredArtifacts = []
      CommandVersion = None
      GeneratorVersion = GeneratorVersion ""
      Base = Revision ""
      Head = Revision "" }

let private goldenMinimal =
    String.concat
        "\n"
        [ "check=10:"
          "domain=10:"
          "cmd=0"
          "env=15:local"
          "rule=10:"
          "art=0;"
          "cmdv=0"
          "genv=10:"
          "base=10:"
          "head=10:" ]

[<Tests>]
let tests =
    testList
        "Determinism"
        [ test "compute is reflexively byte-equal and matches itself (US1 #1)" {
              let k1 = FreshnessKey.value (FreshnessKey.compute baseInputs)
              let k2 = FreshnessKey.value (FreshnessKey.compute baseInputs)
              Expect.equal k1 k2 "computing the same input twice is byte-identical"
              Expect.isTrue (FreshnessKey.matches baseInputs baseInputs) "matches x x = true"
          }

          testPropertyWithConfig fscheckConfig "compute x = compute x for any input (SC-001)"
          <| fun (x: FreshnessInputs) ->
              FreshnessKey.value (FreshnessKey.compute x) = FreshnessKey.value (FreshnessKey.compute x)

          testPropertyWithConfig fscheckConfig "matches x x = true for any input (SC-002)"
          <| fun (x: FreshnessInputs) -> FreshnessKey.matches x x

          test "covered artifacts are a SET: reordered + duplicated ⇒ identical key (SC-002)" {
              // base has [h2; h1; h1]; this is the same set in a different order with another duplicate.
              let reordered =
                  { baseInputs with CoveredArtifacts = [ ArtifactHash "h1"; ArtifactHash "h2"; ArtifactHash "h2" ] }

              Expect.equal
                  (FreshnessKey.value (FreshnessKey.compute baseInputs))
                  (FreshnessKey.value (FreshnessKey.compute reordered))
                  "order and duplication of covered artifacts must not change the key"

              Expect.isTrue (FreshnessKey.matches baseInputs reordered) "set-equal artifacts ⇒ matches"
          }

          testPropertyWithConfig fscheckConfig "any same-set permutation of covered artifacts ⇒ identical key (SC-002)"
          <| fun (x: FreshnessInputs) ->
              let permGen = samePermutationOf x.CoveredArtifacts
              // sample one permutation deterministically per input via FsCheck's Gen.
              let perm = FsCheck.FSharp.Gen.sampleWithSize 0 1 permGen |> Seq.head
              let permuted = { x with CoveredArtifacts = perm }
              FreshnessKey.value (FreshnessKey.compute x) = FreshnessKey.value (FreshnessKey.compute permuted)

          test "golden: the worked example renders the committed canonical string (SC-001)" {
              Expect.equal
                  (FreshnessKey.value (FreshnessKey.compute baseInputs))
                  goldenWorkedExample
                  "worked-example key must match contracts/freshness-key-format.md byte-for-byte"
          }

          test "golden: the degenerate (empty/None/zero) input renders the committed canonical string" {
              Expect.equal
                  (FreshnessKey.value (FreshnessKey.compute minimalInputs))
                  goldenMinimal
                  "minimal key must match the documented empty/None/zero encoding"
          } ]
