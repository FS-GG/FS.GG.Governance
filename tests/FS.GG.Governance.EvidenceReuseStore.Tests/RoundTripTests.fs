module FS.GG.Governance.EvidenceReuseStore.Tests.RoundTripTests

open System.IO
open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.EvidenceReuseStore
open FS.GG.Governance.EvidenceReuseStore.Tests.Support

// US1 (SC-001/SC-003, FR-002/FR-004/FR-005): `serialise` then the REAL `readBack` (real `realStoreReader`,
// never a re-implemented parser — research D3) yields a store EQUAL to the input.

[<Tests>]
let tests =
    testList
        "RoundTrip"
        [ testPropertyWithConfig fscheckConfig "lossless round-trip: readBack (serialise store) = Some store" (fun (store: ReuseStore) ->
              // Every entry's full freshness-input set + opaque evidence reference preserved, newest-first, with
              // each CoveredArtifacts list in its stored order (ReuseStore equality is list-order-sensitive).
              readBack (EvidenceReuseStore.serialise store) = Some store)

          test "worked-example non-empty store round-trips losslessly" {
              let store =
                  storeOf
                      [ inputs "fmt", syntheticRef "fmt" // SYNTHETIC: real refs need gate execution
                        inputs "lint", syntheticRef "lint" ]

              Expect.equal (readBack (EvidenceReuseStore.serialise store)) (Some store) "round-trip equal"
          }

          test "empty store serialises to a present empty recorded array, re-readable as empty" {
              let text = EvidenceReuseStore.serialise EvidenceReuse.empty
              Expect.equal text """{"schemaVersion":"fsgg.evidence-reuse-store/v1","recorded":[]}""" "well-formed empty document"
              Expect.equal (readBack text) (Some EvidenceReuse.empty) "empty round-trips as empty"
          }

          test "an ABSENT file loads as the empty store (Ok None)" {
              let missing = Path.Combine(Path.GetTempPath(), "fsgg-no-such-store-" + string (System.Guid.NewGuid()) + ".json")
              Expect.equal (readPath missing) (Ok None) "absent file ⇒ Ok None"
          }

          test "opaque evidence + JSON-escaping edge round-trips verbatim (FR-004)" {
              // EvidenceRef + freshness strings containing quotes, backslash, and control chars — never
              // parsed/re-hashed/interpreted; Utf8JsonWriter escapes them as JSON string values.
              let i =
                  { inputs "esc"
                    with
                        RuleHash = RuleHash "rule\"with\\escapes\tand\ncontrols"
                        Head = Revision "héllo" }

              let store = ReuseStore [ { Inputs = i; Evidence = EvidenceRef "ev://a\"b\\c\td" } ]
              Expect.equal (readBack (EvidenceReuseStore.serialise store)) (Some store) "escaped strings round-trip verbatim"
          }

          test "sensed-empty covered set (CoveredArtifacts = []) round-trips as an empty list" {
              // The F029/F043 sensed-empty-vs-unsensed distinction preserved (Edge Cases).
              let i = { inputs "empty-cov" with CoveredArtifacts = [] }
              let store = ReuseStore [ { Inputs = i; Evidence = syntheticRef "empty-cov" } ]
              let loaded = readBack (EvidenceReuseStore.serialise store)
              Expect.equal loaded (Some store) "empty covered set preserved"

              match loaded with
              | Some(ReuseStore [ e ]) -> Expect.equal e.Inputs.CoveredArtifacts [] "covered artifacts are the empty list"
              | other -> failtestf "unexpected load: %A" other
          }

          test "multi-element coveredArtifacts preserved in stored list order, verbatim (D5)" {
              let i = { inputs "ord" with CoveredArtifacts = [ ArtifactHash "z"; ArtifactHash "a"; ArtifactHash "a"; ArtifactHash "m" ] }
              let store = ReuseStore [ { Inputs = i; Evidence = syntheticRef "ord" } ]

              match readBack (EvidenceReuseStore.serialise store) with
              | Some(ReuseStore [ e ]) ->
                  Expect.equal
                      e.Inputs.CoveredArtifacts
                      [ ArtifactHash "z"; ArtifactHash "a"; ArtifactHash "a"; ArtifactHash "m" ]
                      "covered artifacts kept in verbatim order, never sorted/de-duped"
              | other -> failtestf "unexpected load: %A" other
          } ]
