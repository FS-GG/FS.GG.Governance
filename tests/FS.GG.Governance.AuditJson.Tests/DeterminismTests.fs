module FS.GG.Governance.AuditJson.Tests.DeterminismTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Route.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.AuditJson
open FS.GG.Governance.AuditJson.Tests.Support

// US2 — the document is a stable, versioned schema: byte-identical for identical input,
// permutation-invariant, version-stamped with a fixed field order at every level, and free of every
// excluded token. All inputs are real `Ship.rollup` outputs (research D7); the FsCheck generator
// drives the genuine rollup, so no synthetic evidence is used.

[<Tests>]
let tests =
    testList
        "Determinism (US2)"
        [ testPropertyWithConfig fsCheckConfig "ofShipDecision d = ofShipDecision d byte-for-byte (AS1, SC-002)" (fun d ->
              AuditJson.ofShipDecision d None = AuditJson.ofShipDecision d None)

          test "value-equal decisions assembled from differently-ordered route inputs project identically (AS2, SC-003)" {
              // Two routes with the SAME gates + findings supplied in DIFFERENT order. The real
              // rollup's composite sort fixes each section's order, so the projections must be identical.
              let gA = mkSelectedGate (mkGate (GateId "build:ship") BlockOnShip)
              let gB = mkSelectedGate (mkGate (GateId "build:rel") BlockOnRelease)
              let gC = mkSelectedGate (mkGate (GateId "docs:lint") Observe)
              let fA = mkFinding UnknownGovernedPath (GovernedPath "src/a.fs") GovernedRootUnknown
              let fB = mkFinding UnknownProtectedBoundaryPath (GovernedPath "src/b.fs") (ProtectedBoundaryUnknown(SurfaceId "s"))

              let route1 = mkRoute [ gA; gB; gC ] [ fA; fB ]
              let route2 = mkRoute [ gC; gA; gB ] [ fB; fA ]

              let json1 = AuditJson.ofShipDecision (decisionOf route1 Gate Standard) None
              let json2 = AuditJson.ofShipDecision (decisionOf route2 Gate Standard) None

              Expect.equal json1 json2 "permutation-invariant: input order does not change the document"
          }

          test "schemaVersion field equals AuditJson.schemaVersion and every object's field order is fixed (AS3, FR-013)" {
              use doc = parse (AuditJson.ofShipDecision richDecision None)

              Expect.equal (strField doc.RootElement "schemaVersion") AuditJson.schemaVersion "schemaVersion stamped"
              Expect.equal AuditJson.schemaVersion "fsgg.audit/v2" "declared contract version"

              // F045: the top-level cache-eligibility section flag is the always-present last field.
              Expect.equal
                  (topLevelFieldOrder doc)
                  [ "schemaVersion"; "verdict"; "exitCodeBasis"; "blockers"; "warnings"; "passing"; "cacheEligibilityEvaluated" ]
                  "top-level field order"

              let enforcementOrder = [ "baseSeverity"; "maturity"; "mode"; "profile"; "effectiveSeverity"; "reason" ]

              // F045: every GATE item gains a trailing `cacheEligibility` verdict; FINDING items carry none.
              for it in List.concat [ section doc "blockers"; section doc "warnings"; section doc "passing" ] do
                  match itemKind it with
                  | "gate" -> Expect.equal (fieldOrder it) [ "kind"; "id"; "enforcement"; "cacheEligibility" ] "gate item field order"
                  | "finding" -> Expect.equal (fieldOrder it) [ "kind"; "id"; "path"; "enforcement" ] "finding item field order"
                  | k -> failtestf "unexpected kind %s" k

                  Expect.equal (fieldOrder (it.GetProperty "enforcement")) enforcementOrder "enforcement field order"
          }

          test "exclusion sweep: only the contracted fields exist, and only the declared vocabulary is emitted (AS4, SC-007, FR-011/FR-012)" {
              let json = AuditJson.ofShipDecision richDecision None
              use doc = parse json

              // (a) field-name check — the ONLY fields anywhere in the document are the contracted ones,
              // so no excluded concern (a numeric `exitCode`, `provenance`/`attestation`/`digest`,
              // `freshness`, gate registry metadata `cost`/`timeout`/`owner`/`prerequisites`/
              // `freshnessKey`, `selectingPaths`/`matchedGlob`, a route trace, a timestamp, or an
              // environment value) can be present — none of them is a contracted field. (F045 adds the
              // cache-eligibility section fields to the contracted set: `cacheEligibilityEvaluated`,
              // the per-gate `cacheEligibility` verdict object, and its `evidence`/`cause`/`categories`.)
              let rec fieldNames (el: System.Text.Json.JsonElement) : string list =
                  match el.ValueKind with
                  | System.Text.Json.JsonValueKind.Object ->
                      [ for p in el.EnumerateObject() do
                            yield p.Name
                            yield! fieldNames p.Value ]
                  | System.Text.Json.JsonValueKind.Array ->
                      [ for v in el.EnumerateArray() do yield! fieldNames v ]
                  | _ -> []

              let contracted =
                  set [ "schemaVersion"; "verdict"; "exitCodeBasis"; "blockers"; "warnings"; "passing"
                        "kind"; "id"; "path"; "enforcement"
                        "baseSeverity"; "maturity"; "mode"; "profile"; "effectiveSeverity"; "reason"
                        // F045 cache-eligibility embed
                        "cacheEligibilityEvaluated"; "cacheEligibility"; "evidence"; "cause"; "categories" ]

              let foreign = fieldNames doc.RootElement |> List.filter (fun n -> not (Set.contains n contracted))
              Expect.isEmpty foreign (sprintf "no field outside the contracted set may appear; found: %A" foreign)

              // (b) positive allowlist — every emitted string is a member of the declared vocabulary,
              // a declared id/path, or the carried reason. (No host/absolute path can appear: the
              // ShipDecision carries none.)
              use doc = parse json

              let verdicts = set [ "pass"; "fail" ]
              let bases = set [ "clean"; "blocked" ]
              let severities = set [ "advisory"; "blocking" ]
              let maturities = set [ "observe"; "warn"; "blockOnPr"; "blockOnShip"; "blockOnRelease" ]
              let modes = set [ "sandbox"; "inner"; "focused"; "verify"; "gate"; "release" ]
              let profiles = set [ "light"; "standard"; "strict"; "release" ]
              let kinds = set [ "gate"; "finding" ]
              // F045: the cache-eligibility verdict vocabulary. richDecision is projected with `None`, so
              // only `notEvaluated` actually appears; the full closed set is allowed for forward stability.
              let cacheKinds = set [ "reusable"; "mustRecompute"; "notEvaluated"; "noPriorEvidence"; "inputsChanged" ]
              let ids = set [ "build:ship"; "build:rel"; "docs:lint"; "unknownGovernedPath"; "unknownProtectedBoundaryPath" ]
              let paths = set [ "src/boundary/Api.fs"; "src/new/Thing.fs" ]

              let reasons =
                  List.concat [ richDecision.Blockers; richDecision.Warnings; richDecision.Passing ]
                  |> List.map (fun i -> i.Decision.Reason)
                  |> Set.ofList

              let allowed =
                  Set.unionMany
                      [ set [ AuditJson.schemaVersion ]; verdicts; bases; severities; maturities
                        modes; profiles; kinds; cacheKinds; ids; paths; reasons ]

              for s in allStringValues doc.RootElement do
                  Expect.isTrue (Set.contains s allowed) (sprintf "emitted string %A is in the declared vocabulary" s)
          } ]
