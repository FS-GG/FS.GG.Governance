module FS.GG.Governance.Gates.Tests.DeterminismTests

open System
open Expecto
open FsCheck
open FsCheck.FSharp
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Gates
open FS.GG.Governance.Gates.Tests.Support

// US3 (SC-003): byte-identical for identical facts; unchanged under input re-ordering; every field
// carries declared ids only — no raw YAML, host-path separators, timestamps, or product vocabulary.
// US5 (SC-006): the exposed gate order is exactly the GateId ordinal sort, reorder-invariant.

let private sampleChecks =
    [ check "build" "tests" (Some "dotnet-test") "team-a" Medium Ci Warn
      check "security" "scan" (Some "trivy") "team-b" High Ci BlockOnPr
      check "docs" "lint" None "team-c" Cheap Local Observe
      check "build" "format" None "team-a" Cheap Local Observe
      check "api" "contract" (Some "schemathesis") "team-d" High Release BlockOnShip ]

let private sampleCommands =
    [ command "dotnet-test" 600
      command "trivy" 120
      command "schemathesis" 900 ]

let private config = { FsCheckConfig.defaultConfig with maxTest = 200; arbitrary = [] }

[<Tests>]
let tests =
    testList
        "Determinism"
        [ test "assembling identical facts twice → structurally equal registry incl. order (SC-003, AS1)" {
              let facts = factsOf sampleChecks sampleCommands
              Expect.equal (Gates.buildRegistry facts) (Gates.buildRegistry facts) "identical facts → identical registry"
          }

          testPropertyWithConfig config "permuting declared checks AND commands yields an identical registry (AS2)"
          <| (fun (cseed: int) (mseed: int) ->
              // Deterministic permutations driven by the seeds (no Math.random in the library or here).
              let permute seed xs =
                  xs |> List.sortBy (fun x -> (hash x) ^^^ seed)

              let baseReg = Gates.buildRegistry (factsOf sampleChecks sampleCommands)
              let permReg = Gates.buildRegistry (factsOf (permute cseed sampleChecks) (permute mseed sampleCommands))
              baseReg = permReg)

          test "every field carries declared ids only — no raw YAML / host paths / timestamps (AS3, FR-004)" {
              let reg = Gates.buildRegistry (factsOf sampleChecks sampleCommands)

              for g in reg.Gates do
                  // The description is the only free-text field; it must name only declared ids.
                  Expect.isFalse (g.Description.Contains "\\") "no host-path backslash"
                  Expect.isFalse (g.Description.Contains "/") "no host-path slash"
                  Expect.isFalse (g.Description.Contains ".yml") "no raw YAML file reference"
                  Expect.isFalse (g.Description.Contains "20") "no year-like timestamp digits"
                  // The gate id is exactly domain:checkId — a single colon separator, declared ids.
                  let idText = gateIdValue g.Id
                  Expect.equal (idText.Split(':').Length) 2 "GateId is exactly domain:checkId"
          }

          // ── US5 (T024): explicit GateId ordinal order, reorder-invariant ──
          test "gates are in GateId ordinal order (SC-006, US5 AS1)" {
              let reg = Gates.buildRegistry (factsOf sampleChecks sampleCommands)
              let ids = reg.Gates |> List.map (fun g -> gateIdValue g.Id)
              let sorted = ids |> List.sortWith (fun a b -> String.CompareOrdinal(a, b))
              Expect.equal ids sorted "registry.Gates is sorted by GateId ordinal"
          }

          test "the GateId order is unchanged when inputs are reversed (US5 AS2)" {
              // The gate dependency graph is trivially acyclic in this MVP (no gate-to-gate edges),
              // so the order is the GateId sort and the topological order is the deferred Phase-10
              // extension point (US5 AS3, out of MVP scope).
              let forward = Gates.buildRegistry (factsOf sampleChecks sampleCommands)
              let reversed = Gates.buildRegistry (factsOf (List.rev sampleChecks) (List.rev sampleCommands))
              Expect.equal
                  (forward.Gates |> List.map (fun g -> gateIdValue g.Id))
                  (reversed.Gates |> List.map (fun g -> gateIdValue g.Id))
                  "GateId order is independent of declaration order"
          } ]
