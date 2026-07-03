module FS.GG.Governance.Gates.Tests.RegistryInvariantTests

open Expecto
open FsCheck
open FsCheck.FSharp
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Gates
open FS.GG.Governance.Gates.Tests.Support

// US2 (SC-002): over arbitrary VALID facts (distinct check ids; every Check.Command, when Some,
// names a declared command — F014's resolved cross-reference), prove the registry is internally
// consistent BY CONSTRUCTION: all GateIds distinct, gate count = check count, every RequiresCommand
// resolves, and assembly is total. The generators MODEL `Valid TypedFacts`; the suite asserts the
// preserved guarantees — it does not re-introduce a validator (research D4).

/// Generate a `Valid TypedFacts`: a set of declared commands, then checks with catalog-wide unique
/// (domain, checkId) ids whose optional `Command`, when present, references a declared command.
let private genValidFacts : Gen<TypedFacts> =
    gen {
        let token prefix n = sprintf "%s%d" prefix n

        // Declared commands (possibly empty), with distinct ids.
        let! commandCount = Gen.choose (0, 4)
        let commandIds = [ for i in 1..commandCount -> token "cmd" i ]
        let! timeouts = Gen.listOfLength commandCount (Gen.choose (30, 1800))
        let commands = List.map2 (fun id t -> command id t) commandIds timeouts

        // Distinct check ids: pick distinct (domain, checkId) keys by index so ids never collide.
        let! checkCount = Gen.choose (0, 8)

        let! rows =
            Gen.listOfLength
                checkCount
                (gen {
                    let! domN = Gen.choose (1, 3)
                    let! cost = Gen.elements [ Cheap; Medium; High; Exhaustive ]
                    let! env = Gen.elements [ Local; Ci; LocalOrCi; Release ]
                    let! mat = Gen.elements [ Observe; Warn; BlockOnPr; BlockOnShip; BlockOnRelease ]
                    // Reference a declared command, or none. Only declared commands are reachable.
                    let! cmd =
                        if List.isEmpty commandIds then
                            Gen.constant None
                        else
                            Gen.elements (None :: (commandIds |> List.map Some))

                    return (domN, cost, env, mat, cmd)
                })

        let checks =
            rows
            |> List.mapi (fun i (domN, cost, env, mat, cmd) ->
                // The check id is index-unique catalog-wide; the domain is one of a few buckets.
                check (token "dom" domN) (token "chk" i) cmd "owner" cost env mat)

        return factsOf checks commands
    }

let private config = { FsCheckConfig.defaultConfig with maxTest = 300; arbitrary = [] }

let private declaredCommandIds (facts: TypedFacts) =
    facts.Tooling
    |> Option.map (fun t -> t.Commands |> List.map (fun c -> c.Id))
    |> Option.defaultValue []
    |> Set.ofList

[<Tests>]
let tests =
    testList
        "RegistryInvariants"
        [ testPropertyWithConfig config "all GateIds are distinct — injective derivation (SC-002, AS1)"
          <| Prop.forAll (Arb.fromGen genValidFacts) (fun facts ->
              let reg = Gates.buildRegistry facts
              let ids = reg.Gates |> List.map (fun g -> gateIdValue g.Id)
              List.length ids = List.length (List.distinct ids))

          testPropertyWithConfig config "gate count = declared check count (parity)"
          <| Prop.forAll (Arb.fromGen genValidFacts) (fun facts ->
              let reg = Gates.buildRegistry facts
              reg.Gates.Length = facts.Capabilities.Checks.Length)

          testPropertyWithConfig config "every RequiresCommand resolves to a declared command (AS2)"
          <| Prop.forAll (Arb.fromGen genValidFacts) (fun facts ->
              let reg = Gates.buildRegistry facts
              let declared = declaredCommandIds facts

              reg.Gates
              |> List.forall (fun g ->
                  g.Prerequisites
                  |> List.forall (fun (RequiresCommand c) -> Set.contains c declared)))

          testPropertyWithConfig config "Gates.buildRegistry never throws and yields one gate per check (AS3, totality)"
          <| Prop.forAll (Arb.fromGen genValidFacts) (fun facts ->
              // Forcing the whole list proves no lazy throw and no partial result.
              let reg = Gates.buildRegistry facts
              reg.Gates |> List.forall (fun g -> gateIdValue g.Id <> "") |> ignore
              reg.Gates.Length = facts.Capabilities.Checks.Length) ]
