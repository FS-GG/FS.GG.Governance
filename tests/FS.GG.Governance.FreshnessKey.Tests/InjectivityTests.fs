module FS.GG.Governance.FreshnessKey.Tests.InjectivityTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey
open FS.GG.Governance.FreshnessKey.Model

// Cross-category injectivity (SC-004, FR-006): the same opaque string placed in one category vs another
// must not collide, and a value laden with the encoding's delimiter characters (`:`, `=`, `\n`, `;`)
// cannot masquerade as a neighbouring field — the length-prefix guarantees it.

// A neutral all-empty base (Local env), so a placed marker is the only distinguishing content.
let private neutral: FreshnessInputs =
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

let private key i = FreshnessKey.value (FreshnessKey.compute i)

// Representative string-bearing category pairs: place the SAME marker in the first vs the second.
let private placements: (string * FreshnessInputs * FreshnessInputs) list =
    let mark = "MARK"
    [ "ruleHash vs generatorVersion",
      { neutral with RuleHash = RuleHash mark },
      { neutral with GeneratorVersion = GeneratorVersion mark }
      "base vs head",
      { neutral with Base = Revision mark },
      { neutral with Head = Revision mark }
      "check vs domain",
      { neutral with Check = CheckId mark },
      { neutral with Domain = DomainId mark }
      "ruleHash vs coveredArtifacts",
      { neutral with RuleHash = RuleHash mark },
      { neutral with CoveredArtifacts = [ ArtifactHash mark ] } ]

[<Tests>]
let tests =
    testList
        "Injectivity"
        ([ for (name, a, b) in placements ->
               test (sprintf "the same string in %s yields different keys (SC-004)" name) {
                   Expect.notEqual (key a) (key b) "moving an opaque value between categories must change the key"
               } ]
         @ [ test "a delimiter-laden value cannot masquerade as a neighbouring field (FR-006)" {
                 // Naive non-length-prefixed encoders risk conflating these two distinct inputs: x splits
                 // a value across rule+genv; y crams the forged segment text into rule alone.
                 let x = { neutral with RuleHash = RuleHash "a"; GeneratorVersion = GeneratorVersion "b" }
                 let y = { neutral with RuleHash = RuleHash "a\ngenv=11:b"; GeneratorVersion = GeneratorVersion "" }
                 Expect.notEqual (key x) (key y) "length-prefixing must keep forged segment text from colliding"
             }

             test "None command is distinct from a Some command holding the encoded-absence shape" {
                 // `cmd=0` (None) must not collide with a present command whose value mimics that text.
                 let none = { neutral with Command = None }
                 let some = { neutral with Command = Some(CommandId "") }
                 Expect.notEqual (key none) (key some) "absent command must never equal a present empty command"
             } ])
