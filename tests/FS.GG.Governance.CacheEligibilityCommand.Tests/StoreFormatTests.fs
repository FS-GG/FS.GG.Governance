module FS.GG.Governance.CacheEligibilityCommand.Tests.StoreFormatTests

open System
open System.IO
open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.CacheEligibilityCommand
open FS.GG.Governance.CacheEligibilityCommand.Tests.Support

// T029 (FR-006/FR-013, L12, A5) — the read-only `fsgg.evidence-reuse-store/v1` deserializer: a well-formed
// document round-trips to the expected F030 ReuseStore (built independently via the public F029/F030
// constructors), order preserved; an absent path ⇒ Ok None; a malformed/unknown-schema doc ⇒ Error.

let private reader: Interpreter.StoreReader = (Interpreter.realPorts ".").Store

let private sampleStore: ReuseStore =
    let inputs1: FreshnessInputs =
        { Check = CheckId "build:format"
          Domain = DomainId "build"
          Command = Some(CommandId "dotnet-format")
          Environment = EnvironmentClass.LocalOrCi
          RuleHash = RuleHash "r1"
          CoveredArtifacts = [ ArtifactHash "h1"; ArtifactHash "h2" ]
          CommandVersion = Some(CommandVersion "8.0")
          GeneratorVersion = GeneratorVersion "g1"
          Base = Revision "base1"
          Head = Revision "head1" }

    let inputs2: FreshnessInputs =
        { inputs1 with
            Check = CheckId "docs:check"
            Command = None
            CommandVersion = None
            CoveredArtifacts = [] }

    ReuseStore
        [ { Inputs = inputs1; Evidence = EvidenceRef "ev-1" }
          { Inputs = inputs2; Evidence = EvidenceRef "ev-2" } ]

let private withTempFile (content: string) (body: string -> 'a) : 'a =
    let path = Path.Combine(Path.GetTempPath(), "fsgg-store-" + Guid.NewGuid().ToString("N") + ".json")
    File.WriteAllText(path, content)

    try
        body path
    finally
        try
            File.Delete path
        with _ ->
            ()

[<Tests>]
let tests =
    testList
        "StoreFormat"
        [ test "a well-formed store round-trips to the expected ReuseStore (order preserved)" {
              let json = serializeStore sampleStore

              withTempFile json (fun path ->
                  match reader path with
                  | Ok(Some parsed) -> Expect.equal (EvidenceReuse.entries parsed) (EvidenceReuse.entries sampleStore) "round-trips, including absent command + sensed-empty covered set"
                  | other -> failtestf "expected Ok (Some store), got %A" other)
          }

          test "an absent store file ⇒ Ok None (FR-006)" {
              let missing = Path.Combine(Path.GetTempPath(), "fsgg-store-absent-" + Guid.NewGuid().ToString("N") + ".json")
              Expect.equal (reader missing) (Ok None) "absent ⇒ Ok None (caller treats as empty)"
          }

          test "malformed JSON ⇒ Error (present-but-malformed, ToolError)" {
              withTempFile "{ not json" (fun path ->
                  match reader path with
                  | Error _ -> ()
                  | other -> failtestf "expected Error, got %A" other)
          }

          test "unknown schema id ⇒ Error" {
              withTempFile "{\"schemaVersion\":\"fsgg.evidence-reuse-store/v999\",\"recorded\":[]}" (fun path ->
                  match reader path with
                  | Error msg -> Expect.stringContains msg "schema" "names the schema mismatch"
                  | other -> failtestf "expected Error, got %A" other)
          }

          test "no hash/key is computed by the reader (opaque strings taken verbatim)" {
              // The round-trip above proves the opaque newtype strings survive unchanged; an empty store reads
              // back as the empty store.
              withTempFile (serializeStore (ReuseStore [])) (fun path ->
                  match reader path with
                  | Ok(Some s) -> Expect.equal (EvidenceReuse.entries s) [] "empty recorded ⇒ empty store"
                  | other -> failtestf "expected Ok (Some empty), got %A" other)
          } ]
