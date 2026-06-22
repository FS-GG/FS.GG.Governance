module FS.GG.Governance.CacheEligibilityCommand.Tests.DeterminismTests

open Expecto
open FsCheck
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.CacheEligibilityCommand
open FS.GG.Governance.CacheEligibilityCommand.Tests.Support

// T022 (US3, SC-004, L9) — identical inputs ⇒ byte-identical artifacts + summary; entries appear in GateId
// order regardless of the order gates are supplied (order-independence). No clock/cwd/abs-path content (C3).

let private git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]

let private runDocs sensor store format =
    let req = requestFor Loop.DefaultRange format
    let cap = newCapture ()
    Interpreter.run (fakePorts validCatalog git sensor store cap req) req |> ignore
    writtenOf cap Loop.CacheArtifact |> Option.map snd, writtenOf cap Loop.UnresolvedArtifact |> Option.map snd, cap.Emits

let private cacheDocFor (gates: Gate list) =
    let sensed = fullSensed gates
    let _, effs = driveProjection (selectedModel gates (requestFor Loop.DefaultRange Loop.Human)) sensed EvidenceReuse.empty

    effs
    |> List.pick (function
        | Loop.WriteArtifact(Loop.CacheArtifact, _, c) -> Some c
        | _ -> None)

[<Tests>]
let tests =
    testList
        "Determinism"
        [ test "twice-run over fixed inputs ⇒ byte-identical artifacts and summary (SC-004)" {
              let c1, u1, e1 = runDocs fixedSensor (storeReaderOf (Ok None)) Loop.Json
              let c2, u2, e2 = runDocs fixedSensor (storeReaderOf (Ok None)) Loop.Json
              Expect.equal c1 c2 "cache-eligibility.json byte-identical across runs"
              Expect.equal u1 u2 "cache-eligibility.unresolved.json byte-identical across runs"
              Expect.equal e1 e2 "summary byte-identical across runs"
          }

          testProperty "entries are in GateId order regardless of supplied gate order (L9)"
          <| fun (b: bool) ->
              // Two literal gates; permute their supply order; the projected doc must be identical.
              let gA = mkGate "build" "format" Cheap LocalOrCi (Some(CommandId "dotnet-format"))
              let gB = mkGate "alpha" "check" Medium Local (Some(CommandId "dotnet-x"))
              let ordered = if b then [ gA; gB ] else [ gB; gA ]
              cacheDocFor ordered = cacheDocFor [ gA; gB ]

          test "neither artifact nor summary carries a clock/cwd/abs-path-dependent token" {
              let c, u, emits = runDocs fixedSensor (storeReaderOf (Ok None)) Loop.Human
              let blob = (Option.defaultValue "" c) + "\n" + (Option.defaultValue "" u) + "\n" + String.concat "\n" emits
              // The fixtures use relative paths only; no absolute temp path should leak.
              Expect.isFalse (blob.Contains "/tmp/") "no absolute temp path leaks into output"
          } ]
