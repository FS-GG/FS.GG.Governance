module FS.GG.Governance.ScaffoldManifestJson.Tests.ProjectionTests

open Expecto
open FS.GG.Governance.Scaffold.Model
open FS.GG.Governance.ScaffoldManifestJson
open FS.GG.Governance.ScaffoldManifestJson.Tests.Support

// `ofManifest` field-order / shape coverage (contracts/scaffold-manifest.schema.md). Inspects the
// EMITTED BYTES via JsonDocument; every closed token is exercised through the exhaustive match.

[<Tests>]
let tests =
    testList
        "Projection"
        [ test "scaffolded: fixed field order, null refusal, provider, ascending providerOwned generated" {
              let m = scaffoldedManifest "acme.fsharp-lib" [ "src/App/Program.fs"; "src/App/App.fsproj" ]
              use doc = parse (ScaffoldManifestJson.ofManifest m)

              Expect.equal
                  (topLevelFieldOrder doc)
                  [ "schemaVersion"; "outcome"; "refusal"; "provider"; "generated"; "collisions" ]
                  "fixed top-level field order"

              Expect.equal (strField doc.RootElement "schemaVersion") "fsgg.scaffold-manifest/v1" "schema version"
              Expect.equal (strField doc.RootElement "outcome") "scaffolded" "outcome token"
              Expect.isTrue (isNull doc "refusal") "refusal null on success"

              let provider = doc.RootElement.GetProperty "provider"
              Expect.equal (strField provider "id") "acme.fsharp-lib" "provider id"
              Expect.equal (strField provider "contractVersion") "1.0" "contract version M.m"

              Expect.equal (generatedPaths doc) [ "src/App/App.fsproj"; "src/App/Program.fs" ] "generated ascending by path"

              Expect.isTrue
                  (generatedEntries doc |> List.forall (fun g -> strField g "ownership" = "providerOwned"))
                  "every generated entry providerOwned"

              Expect.isEmpty (collisions doc) "no collisions"
          }

          test "collision refusal: outcome refused, closed refusal object, collisions array" {
              let m = refusedManifest "acme.fsharp-lib" (Collision [ "src/App/Program.fs" ]) [ "src/App/Program.fs" ]
              use doc = parse (ScaffoldManifestJson.ofManifest m)

              Expect.equal (strField doc.RootElement "outcome") "refused" "outcome refused"

              let refusal = doc.RootElement.GetProperty "refusal"
              Expect.equal (fieldOrder refusal) [ "reason"; "paths" ] "collision refusal field order"
              Expect.equal (strField refusal "reason") "collision" "reason token"
              Expect.equal (stringArrayProp refusal "paths") [ "src/App/Program.fs" ] "refusal paths"
              Expect.equal (collisions doc) [ "src/App/Program.fs" ] "top-level collisions"
              Expect.isEmpty (generatedPaths doc) "nothing generated"
          }

          test "contractMismatch refusal: reason + declaredVersion" {
              let m = refusedManifest "future.lib" (ContractMismatch { Major = 2; Minor = 0 }) []
              use doc = parse (ScaffoldManifestJson.ofManifest m)

              let refusal = doc.RootElement.GetProperty "refusal"
              Expect.equal (fieldOrder refusal) [ "reason"; "declaredVersion" ] "field order"
              Expect.equal (strField refusal "reason") "contractMismatch" "reason"
              Expect.equal (strField refusal "declaredVersion") "2.0" "declared version M.m"
          }

          test "providerUnavailable / providerErrored refusals carry detail" {
              let unavailable = refusedManifest "x" (ProviderUnavailable "missing") []
              let errored = refusedManifest "x" (ProviderErrored "boom") []

              use d1 = parse (ScaffoldManifestJson.ofManifest unavailable)
              use d2 = parse (ScaffoldManifestJson.ofManifest errored)

              let r1 = d1.RootElement.GetProperty "refusal"
              Expect.equal (strField r1 "reason") "providerUnavailable" "unavailable reason"
              Expect.equal (strField r1 "detail") "missing" "unavailable detail"

              let r2 = d2.RootElement.GetProperty "refusal"
              Expect.equal (strField r2 "reason") "providerErrored" "errored reason"
              Expect.equal (strField r2 "detail") "boom" "errored detail"
          }

          test "outOfTarget refusal: reason + sorted paths" {
              let m = refusedManifest "x" (OutOfTarget [ "../b.fs"; "../a.fs" ]) []
              use doc = parse (ScaffoldManifestJson.ofManifest m)

              let refusal = doc.RootElement.GetProperty "refusal"
              Expect.equal (strField refusal "reason") "outOfTarget" "reason"
              Expect.equal (stringArrayProp refusal "paths") [ "../a.fs"; "../b.fs" ] "paths ascending"
          }

          test "noProvider: provider null, generated [], collisions [] (totality fixture)" {
              use doc = parse (ScaffoldManifestJson.ofManifest noProviderManifest)

              Expect.equal (strField doc.RootElement "outcome") "noProvider" "outcome token"
              Expect.isTrue (isNull doc "provider") "provider null"
              Expect.isTrue (isNull doc "refusal") "refusal null"
              Expect.isEmpty (generatedPaths doc) "generated empty"
              Expect.isEmpty (collisions doc) "collisions empty"
          } ]
