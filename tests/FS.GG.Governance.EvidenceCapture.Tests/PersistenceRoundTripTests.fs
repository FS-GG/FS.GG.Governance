module FS.GG.Governance.EvidenceCapture.Tests.PersistenceRoundTripTests

open System.IO
open Expecto
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.EvidenceReuseStore
open FS.GG.Governance.FreshnessSensing
open FS.GG.Governance.EvidenceCapture
open FS.GG.Governance.EvidenceCapture.Tests.Support

// FR-010 / SC-007: a `capture`-grown store survives the ALREADY-MERGED persistence path with the captured world
// and the EXACT derived reference preserved verbatim — through the REAL F047 `EvidenceReuseStore.serialise` and
// the REAL F046 `FreshnessSensing.realStoreReader`, no new persistence code. EvidenceReuseStore + FreshnessSensing
// are TEST-ONLY references here; the production capture library has no such dependency. The temp file is the ONLY
// I/O in the test — the capture core itself touches nothing.

/// Write the serialised text to a temp file and load it back through the REAL F046 reader (its only public load
/// path reads a FILE PATH: `StoreReader = string -> Result<ReuseStore option, string>`). Mirrors F047 `readBack`.
let private readBack (text: string) : ReuseStore option =
    let path = Path.GetTempFileName()

    try
        File.WriteAllText(path, text)

        match FreshnessSensing.realStoreReader path with
        | Ok loaded -> loaded
        | Error reason -> failwithf "realStoreReader rejected serialised output: %s" reason
    finally
        File.Delete path

[<Tests>]
let tests =
    testList
        "PersistenceRoundTrip"
        [ test "a capture-grown store round-trips losslessly through F047 serialise → F046 reader" {
              let world = inputs "build:tests"
              let grown = EvidenceCapture.capture world baseRecord EvidenceReuse.empty

              Expect.equal
                  (readBack (EvidenceReuseStore.serialise grown))
                  (Some grown)
                  "the captured world + the exact derived reference survive serialise → read byte-for-byte"
          }

          test "the captured derived reference is preserved verbatim across persistence" {
              let world = inputs "build:tests"
              let grown = EvidenceCapture.capture world baseRecord EvidenceReuse.empty

              match readBack (EvidenceReuseStore.serialise grown) with
              | Some(ReuseStore [ e ]) ->
                  Expect.equal e.Inputs world "the freshness world is preserved verbatim"
                  Expect.equal e.Evidence (EvidenceCapture.referenceOf baseRecord) "the derived reference is rendered verbatim, never re-parsed or re-hashed"
              | other -> failtestf "unexpected re-read store: %A" other
          }

          testPropertyWithConfig fscheckConfig "round-trip preserves an arbitrary captured (record, world) pair" (fun (world: FreshnessInputs) (r: CommandRecord) ->
              let grown = EvidenceCapture.capture world r EvidenceReuse.empty
              readBack (EvidenceReuseStore.serialise grown) = Some grown) ]
