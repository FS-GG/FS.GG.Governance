module FS.GG.Governance.CacheEligibilityCommand.Tests.InterpreterTests

open System.Text.Json
open Expecto
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.FreshnessResolution
open FS.GG.Governance.CacheEligibility
open FS.GG.Governance.CacheEligibilityCommand
open FS.GG.Governance.CacheEligibilityCommand.Tests.Support

// T012/T019 (the edge) — `Interpreter.run` over FAKED ports (in-memory FileReader + GitPort, the fake
// FreshnessSensor, an in-memory StoreReader, capturing Write/Out). No real git/hash/filesystem (FR-012,
// SC-007). The written bytes are compared to the genuine F041/F042/F043 cores.

let private git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
let private candidates = candidatesOf git defaultOpts
let private gates = selectedGatesOf validCatalog candidates
let private baseHead = baseHeadOfSnapshot (snapshotOf git defaultOpts)
let private sensed = assembleSensed fixedSensor gates baseHead

let private runWith sensor store =
    let req = requestFor Loop.DefaultRange Loop.Human
    let cap = newCapture ()
    let model = Interpreter.run (fakePorts validCatalog git sensor store cap req) req
    req, cap, model

let private gateIds: string list = gates |> List.map (fun g -> gateIdValue g.Id)

[<Tests>]
let tests =
    testList
        "Interpreter"
        [ test "the change selects gates (sanity for the fixtures)" {
              Expect.isNonEmpty gates "a src change selects the package-api gates"
          }

          test "absent store ⇒ empty ⇒ every gate mustRecompute NoPriorEvidence; exit 0 (L7, US1 AS2)" {
              let _, cap, model = runWith fixedSensor (storeReaderOf (Ok None))

              let written = writtenOf cap Loop.CacheArtifact |> Option.map snd
              Expect.equal written (Some(expectedCacheDoc gates sensed EvidenceReuse.empty)) "cache doc = ofReport over the empty store"
              Expect.equal model.Exit Loop.Success "exit 0"
          }

          test "written cache-eligibility.json = genuine ofReport; sidecar present (US1, L6)" {
              let store = storeMakingReusable [ List.head gates ] sensed (fun _ -> "ev-1")
              let _, cap, model = runWith fixedSensor (storeReaderOf (Ok(Some store)))

              Expect.equal (writtenOf cap Loop.CacheArtifact |> Option.map snd) (Some(expectedCacheDoc gates sensed store)) "cache doc = genuine ofReport"
              Expect.isSome (writtenOf cap Loop.UnresolvedArtifact) "sidecar always written"
              Expect.equal model.Exit Loop.Success "exit 0"

              // The prepared gate is reusable over the genuine evaluate path.
              let report = FreshnessResolution.resolve gates sensed
              let cands = FreshnessResolution.entries report |> List.choose FreshnessResolution.candidate
              let verdicts = CacheEligibility.evaluate cands store |> CacheEligibility.entries
              let head = verdicts |> List.find (fun e -> e.Gate = (List.head gates).Id)
              Expect.equal (CacheEligibility.reusableEvidence head.Verdict) (Some(EvidenceRef "ev-1")) "prepared gate reusable"
          }

          test "T019: every selected gate appears in EXACTLY ONE document; unsensed ⇒ sidecar (A4)" {
              // sensorNoCovered ⇒ every gate unresolved on coveredArtifacts.
              let _, cap, model = runWith sensorNoCovered (storeReaderOf (Ok None))

              let cacheDoc = writtenOf cap Loop.CacheArtifact |> Option.map snd |> Option.get
              let sidecar = writtenOf cap Loop.UnresolvedArtifact |> Option.map snd |> Option.get

              use sdoc = JsonDocument.Parse sidecar
              let sidecarGates = [ for e in sdoc.RootElement.GetProperty("unresolved").EnumerateArray() -> jsonProp e "gate" ]

              Expect.equal (List.sort sidecarGates) (List.sort gateIds) "all selected gates are in the sidecar (unresolved)"

              for g in gateIds do
                  Expect.isFalse (cacheDoc.Contains g) (sprintf "%s is unresolved ⇒ absent from cache-eligibility.json" g)

              Expect.equal model.Exit Loop.Success "unresolved is information ⇒ exit 0"
          } ]
