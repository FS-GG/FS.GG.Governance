module FS.GG.Governance.CacheEligibilityCommand.Tests.UnresolvedTests

open System.Text.Json
open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.CacheEligibilityCommand
open FS.GG.Governance.CacheEligibilityCommand.Tests.Support

// T017 (US2, SC-003) — a gate missing a sensed fact appears in the no-hide sidecar naming EXACTLY the missing
// facts, is ABSENT from cache-eligibility.json, and the sidecar is always written (empty when all resolve).

let private gateA = mkGate "build" "format" Cheap LocalOrCi (Some(CommandId "dotnet-format"))
let private req = requestFor Loop.DefaultRange Loop.Human

/// Parse the sidecar JSON to (gate, missingFacts list) pairs.
let private sidecarEntries (json: string) : (string * string list) list =
    use doc = JsonDocument.Parse json
    let root = doc.RootElement
    Expect.equal (jsonProp root "schemaVersion") Loop.unresolvedSchemaVersion "sidecar schema id"

    [ for e in root.GetProperty("unresolved").EnumerateArray() ->
          let gate = jsonProp e "gate"
          let facts = [ for f in e.GetProperty("missingFacts").EnumerateArray() -> jsonStr f ]
          gate, facts ]

let private sidecarOf (model: Loop.Model) (sensed) (store) =
    let _, effs = driveProjection model sensed store

    effs
    |> List.pick (function
        | Loop.WriteArtifact(Loop.UnresolvedArtifact, _, c) -> Some c
        | _ -> None)

[<Tests>]
let tests =
    testList
        "Unresolved"
        [ test "a gate whose covered artifacts are unsensed → sidecar names exactly [coveredArtifacts] (L3/SC-003)" {
              let gates = [ gateA ]
              // fully sensed EXCEPT covered artifacts (key absent ⇒ unsensed).
              let sensed = { fullSensed gates with CoveredArtifacts = Map.empty }
              let sidecar = sidecarOf (selectedModel gates req) sensed EvidenceReuse.empty

              match sidecarEntries sidecar with
              | [ (gate, facts) ] ->
                  Expect.equal gate "build:format" "the unresolved gate is named"
                  Expect.equal facts [ "coveredArtifacts" ] "exactly and only the missing fact is named (no-hide)"
              | other -> failtestf "expected one unresolved entry, got %A" other
          }

          test "the unresolved gate is ABSENT from cache-eligibility.json (never reusable)" {
              let gates = [ gateA ]
              let sensed = { fullSensed gates with CoveredArtifacts = Map.empty }
              let _, effs = driveProjection (selectedModel gates req) sensed EvidenceReuse.empty

              let cacheDoc =
                  effs
                  |> List.pick (function
                      | Loop.WriteArtifact(Loop.CacheArtifact, _, c) -> Some c
                      | _ -> None)

              Expect.isFalse (cacheDoc.Contains "build:format") "an unresolved gate never appears in cache-eligibility.json"
          }

          test "multiple missing facts are named in enum order" {
              let gates = [ gateA ]
              // unsensed rule hash AND covered artifacts AND base/head (only generator + command sensed).
              let sensed =
                  { fullSensed gates with
                      RuleHash = None
                      CoveredArtifacts = Map.empty
                      Base = None
                      Head = None }

              match sidecarEntries (sidecarOf (selectedModel gates req) sensed EvidenceReuse.empty) with
              | [ (_, facts) ] -> Expect.equal facts [ "ruleHash"; "coveredArtifacts"; "baseRevision"; "headRevision" ] "named in MissingFact enum order"
              | other -> failtestf "expected one entry, got %A" other
          }

          test "the sidecar is ALWAYS written, empty when every gate resolves (A2)" {
              let gates = [ gateA ]
              let sidecar = sidecarOf (selectedModel gates req) (fullSensed gates) EvidenceReuse.empty
              Expect.equal (sidecarEntries sidecar) [] "all resolve ⇒ empty unresolved array, still written"
          } ]
