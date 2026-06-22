module FS.GG.Governance.CacheEligibilityCommand.Tests.SensedEmptyTests

open System.Text.Json
open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.FreshnessResolution
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.CacheEligibilityCommand
open FS.GG.Governance.CacheEligibilityCommand.Tests.Support

// T018 (US2, SC-005) — sensed-empty ≠ unsensed (L4); a command-less gate resolves with absent command +
// absent command version and is evaluated normally, never unresolved on that basis (L5).

let private req = requestFor Loop.DefaultRange Loop.Human

let private resolvedGates (gates: Gate list) (sensed) : string list =
    FreshnessResolution.resolve gates sensed
    |> FreshnessResolution.entries
    |> List.filter (fun e -> FreshnessResolution.isResolved e.Outcome)
    |> List.map (fun e ->
        let (GateId g) = e.Gate
        g)

let private unresolvedInDoc (json: string) : (string * string list) list =
    use doc = JsonDocument.Parse json

    [ for e in doc.RootElement.GetProperty("unresolved").EnumerateArray() ->
          jsonProp e "gate", [ for f in e.GetProperty("missingFacts").EnumerateArray() -> jsonStr f ] ]

[<Tests>]
let tests =
    testList
        "SensedEmpty"
        [ test "a sensed-EMPTY covered set (Some []) RESOLVES; an unsensed one does not (L4, SC-005)" {
              let g = mkGate "build" "format" Cheap LocalOrCi (Some(CommandId "dotnet-format"))

              let emptyCovered =
                  { fullSensed [ g ] with CoveredArtifacts = Map.ofList [ g.Id, [] ] }

              Expect.equal (resolvedGates [ g ] emptyCovered) [ "build:format" ] "sensed-empty covered set resolves"

              let unsensedCovered = { fullSensed [ g ] with CoveredArtifacts = Map.empty }
              Expect.equal (resolvedGates [ g ] unsensedCovered) [] "unsensed covered set does NOT resolve"
          }

          test "a command-less gate resolves with absent command, never unresolved on that basis (L5)" {
              let g = mkGate "docs" "check" High Local None // no command declared
              let sensed = fullSensed [ g ] // CommandVersions has no key for this gate
              Expect.equal (resolvedGates [ g ] sensed) [ "docs:check" ] "command-less gate resolves"

              let _, effs = driveProjection (selectedModel [ g ] req) sensed EvidenceReuse.empty

              let sidecar =
                  effs
                  |> List.pick (function
                      | Loop.WriteArtifact(Loop.UnresolvedArtifact, _, c) -> Some c
                      | _ -> None)

              Expect.equal (unresolvedInDoc sidecar) [] "a command-less gate is never reported unresolved"

              let cacheDoc =
                  effs
                  |> List.pick (function
                      | Loop.WriteArtifact(Loop.CacheArtifact, _, c) -> Some c
                      | _ -> None)

              Expect.isTrue (cacheDoc.Contains "docs:check") "the command-less gate is evaluated (appears resolved)"
          } ]
