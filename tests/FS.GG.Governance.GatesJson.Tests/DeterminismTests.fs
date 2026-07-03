module FS.GG.Governance.GatesJson.Tests.DeterminismTests

open Expecto
open FsCheck
open FsCheck.FSharp
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.GatesJson
open FS.GG.Governance.GatesJson.Tests.Support

// US2 — a stable, versioned schema for CI and agents: identical inputs → byte-identical document;
// value-equal registries from differently-ordered declared checks → identical document; a declared
// schemaVersion + a fixed field order; and none of the excluded enforcement/verdict/selection/
// raw-YAML/host-path/timestamp/environment tokens. Properties run over REAL upstream-assembled inputs.

/// A real multi-domain check world; the property generators draw their checks from this pool.
let private checkPool =
    [ check "build" "tests" (Some "dotnet-test") Medium Local BlockOnShip
      check "build" "format" None Cheap Local Observe
      check "docs" "lint" None Cheap LocalOrCi Warn
      check "api" "surface" None High Ci BlockOnPr
      check "release" "audit" None Exhaustive Release BlockOnRelease ]

let private commandPool = [ command "dotnet-test" 600 ]

/// A generator of check sub-lists (any sublist of the pool, in any order) → real registries via the
/// real `Gates.buildRegistry` (research D7): inputs stay real upstream-assembled values.
let private genRegistry : Gen<GateRegistry> =
    gen {
        let! picks = Gen.subListOf checkPool
        return registryFor picks commandPool
    }

let private config = { FsCheckConfig.defaultConfig with maxTest = 200; arbitrary = [] }

/// A real, prerequisite-bearing, multi-gate registry for the sweeps over populated sections.
let private populated = registryFor checkPool commandPool

[<Tests>]
let tests =
    testList
        "Determinism (US2)"
        [ testPropertyWithConfig config "twice-identical: ofGateRegistry is byte-identical for identical input (AS1, SC-002)"
          <| Prop.forAll (Arb.fromGen genRegistry) (fun r ->
              GatesJson.ofGateRegistry r = GatesJson.ofGateRegistry r)

          test "fixed-fixture twice-identical equality (SC-002)" {
              Expect.equal (GatesJson.ofGateRegistry populated) (GatesJson.ofGateRegistry populated) "same registry → same bytes"
          }

          test "permutation-invariant: registries from differently-ordered declared checks project identically (AS2, SC-003)" {
              // shuffle the declared check list; buildRegistry's GateId-ordinal sort fixes the gate
              // order, so the two value-equal registries must project to identical bytes.
              let a = registryFor checkPool commandPool
              let b = registryFor (List.rev checkPool) commandPool
              Expect.equal a b "the two registries are value-equal (GateId-ordinal sort)"
              Expect.equal (GatesJson.ofGateRegistry a) (GatesJson.ofGateRegistry b) "value-equal registries project to identical bytes"
          }

          testPropertyWithConfig config "permutation-invariant over generated check orderings (AS2, SC-003)"
          <| Prop.forAll (Arb.fromGen (Gen.subListOf checkPool)) (fun picks ->
              let a = GatesJson.ofGateRegistry (registryFor picks commandPool)
              let b = GatesJson.ofGateRegistry (registryFor (List.rev picks) commandPool)
              a = b)

          test "the document carries the declared schemaVersion and the fixed field order (AS3, FR-013)" {
              use doc = parse (GatesJson.ofGateRegistry populated)
              Expect.equal (strField doc.RootElement "schemaVersion") GatesJson.schemaVersion "schemaVersion field equals the constant"
              Expect.equal (topLevelFieldOrder doc) [ "schemaVersion"; "gates" ] "fixed top-level field order"

              for g in gates doc do
                  Expect.equal
                      (fieldOrder g)
                      [ "id"; "domain"; "description"; "cost"; "timeout"; "owner"; "maturity"; "productCheck"; "prerequisites"; "freshnessKey" ]
                      "fixed gate field order"
                  let fk = g.GetProperty "freshnessKey"
                  Expect.equal (fieldOrder fk) [ "check"; "domain"; "cost"; "environment"; "command" ] "fixed freshnessKey field order"
          }

          test "exclusion sweep: the emitted text contains no enforcement/verdict/selection/raw-YAML/clock token (AS4, SC-007, FR-011/FR-012)" {
              let json = GatesJson.ofGateRegistry populated
              let lower = json.ToLowerInvariant()

              let denied =
                  [ "severity"; "profile"; "\"mode\""; "enforcement"; "cacheeligib"; "selectingpaths"
                    "findings"; "verdict"; "blockers"; "warnings"; "exitcode"; "expectedartifacts"; "timestamp" ]

              for token in denied do
                  Expect.isFalse (lower.Contains token) (sprintf "excluded token %A must not appear" token)

              // no ISO-8601-ish wall-clock value (a 'T' between digits, e.g. 2026-06-20T..)
              Expect.isFalse (System.Text.RegularExpressions.Regex.IsMatch(json, @"\d{4}-\d{2}-\d{2}T\d{2}:")) "no wall-clock timestamp"
          }

          test "positive allowlist: every emitted string value is a declared id / vocabulary / carried metadatum (FR-012)" {
              use doc = parse (GatesJson.ofGateRegistry populated)

              // the universe of declared/carried string values the registry can legitimately carry
              let costTokens = Set.ofList [ "cheap"; "medium"; "high"; "exhaustive" ]
              let maturityTokens = Set.ofList [ "observe"; "warn"; "blockOnPr"; "blockOnShip"; "blockOnRelease" ]
              let envTokens = Set.ofList [ "local"; "ci"; "localOrCi"; "release" ]

              let declared =
                  populated.Gates
                  |> List.collect (fun g ->
                      let (DomainId d) = g.Domain
                      let (Owner o) = g.Owner
                      let (CheckId fkCheck) = g.FreshnessKey.Check
                      let (DomainId fkDomain) = g.FreshnessKey.Domain
                      let cmds = g.Prerequisites |> List.map (fun (RequiresCommand(CommandId c)) -> c)
                      let fkCmd = match g.FreshnessKey.Command with Some(CommandId c) -> [ c ] | None -> []
                      [ gateIdValue g.Id; d; g.Description; o; fkCheck; fkDomain ] @ cmds @ fkCmd)
                  |> Set.ofList

              let allowed (s: string) =
                  declared.Contains s
                  || costTokens.Contains s
                  || maturityTokens.Contains s
                  || envTokens.Contains s
                  || s = GatesJson.schemaVersion

              // collect every string value anywhere in the document
              let rec strings (el: System.Text.Json.JsonElement) =
                  match el.ValueKind with
                  | System.Text.Json.JsonValueKind.String -> [ el.GetString() ]
                  | System.Text.Json.JsonValueKind.Object -> [ for p in el.EnumerateObject() do yield! strings p.Value ]
                  | System.Text.Json.JsonValueKind.Array -> [ for e in el.EnumerateArray() do yield! strings e ]
                  | _ -> []

              for s in strings doc.RootElement do
                  match s with
                  | null -> ()
                  | v -> Expect.isTrue (allowed v) (sprintf "emitted string %A is a declared id / vocabulary / carried metadatum" v)
          } ]
