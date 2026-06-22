module FS.GG.Governance.FreshnessSensing.Tests.StoreReaderTests

open Expecto
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.FreshnessSensing
open FS.GG.Governance.FreshnessSensing.Tests.Support

// Real-bytes evidence for the read-only store reader (Principle V): `loadStore` over real on-disk files
// maps absent ⇒ `EvidenceReuse.empty`, present-well-formed ⇒ the round-trip-equal `ReuseStore` built the
// genuine way (`EvidenceReuse.record`), present-malformed ⇒ `Error`.

[<Tests>]
let tests =
    testList
        "StoreReader"
        [ test "loadStore over an ABSENT file ⇒ Ok EvidenceReuse.empty (absent ⇒ empty)" {
              withTempDir (fun t ->
                  match FreshnessSensing.loadStore FreshnessSensing.realStoreReader t.AbsentStorePath with
                  | Ok store -> Expect.equal store EvidenceReuse.empty "absent store ⇒ EvidenceReuse.empty"
                  | Error e -> failtestf "absent store must be Ok empty, got Error: %s" e)
          }

          test "loadStore over a well-formed store ⇒ the round-trip-equal ReuseStore (real deserializer)" {
              withTempDir (fun t ->
                  match FreshnessSensing.loadStore FreshnessSensing.realStoreReader t.WellFormedStorePath with
                  | Ok store -> Expect.equal store expectedStore "deserialized store equals the record-built store (round-trip)"
                  | Error e -> failtestf "well-formed store must parse, got Error: %s" e)
          }

          test "loadStore over a malformed store ⇒ Error (never throws)" {
              withTempDir (fun t ->
                  match FreshnessSensing.loadStore FreshnessSensing.realStoreReader t.MalformedStorePath with
                  | Error _ -> ()
                  | Ok _ -> failtest "a malformed store must surface as Error, never a silently-empty store")
          }

          test "realStoreReader distinguishes absent (Ok None) / present (Ok Some) / malformed (Error)" {
              withTempDir (fun t ->
                  Expect.equal (FreshnessSensing.realStoreReader t.AbsentStorePath) (Ok None) "absent ⇒ Ok None"

                  match FreshnessSensing.realStoreReader t.WellFormedStorePath with
                  | Ok(Some store) -> Expect.equal store expectedStore "present-well-formed ⇒ Ok (Some store)"
                  | other -> failtestf "expected Ok (Some store), got %A" other

                  match FreshnessSensing.realStoreReader t.MalformedStorePath with
                  | Error _ -> ()
                  | other -> failtestf "expected Error for malformed, got %A" other)
          } ]
