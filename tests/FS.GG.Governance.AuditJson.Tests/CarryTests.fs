module FS.GG.Governance.AuditJson.Tests.CarryTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.AuditJson
open FS.GG.Governance.AuditJson.Tests.Support

// US3 — the no-hide rule is visible on every item: base + effective severity both present, all six
// enforcement fields carried verbatim, and a finding's identity carries both its id token and its
// governed path. All fixtures are real `Ship.rollup` outputs.

let private allItems (d: ShipDecision) = List.concat [ d.Blockers; d.Warnings; d.Passing ]

let private severityTok (s: Severity) = match s with | Advisory -> "advisory" | Blocking -> "blocking"
let private maturityTok (m: Maturity) =
    match m with
    | Observe -> "observe" | Warn -> "warn" | BlockOnPr -> "blockOnPr"
    | BlockOnShip -> "blockOnShip" | BlockOnRelease -> "blockOnRelease"
let private modeTok (m: RunMode) =
    match m with
    | Sandbox -> "sandbox" | Inner -> "inner" | Focused -> "focused"
    | Verify -> "verify" | Gate -> "gate" | RunMode.Release -> "release"
let private profileTok (p: Profile) =
    match p with | Light -> "light" | Standard -> "standard" | Strict -> "strict" | Profile.Release -> "release"

[<Tests>]
let tests =
    testList
        "Carry (US3)"
        [ test "a relaxed base-Blocking warning shows baseSeverity:blocking AND effectiveSeverity:advisory plus mode/profile/maturity/reason (AS1, FR-011, SC-005)" {
              // richDecision carries a BlockOnRelease gate at Gate/Standard — base Blocking relaxed to
              // effective Advisory (the release boundary is above Gate). The non-empty reason is
              // guaranteed by the real F024 rollup, not by the string type alone.
              let d = richDecision
              use doc = parse (AuditJson.ofShipDecision d)

              let relaxed =
                  section doc "warnings"
                  |> List.filter (fun it ->
                      enforcement it "baseSeverity" = "blocking"
                      && enforcement it "effectiveSeverity" = "advisory")

              Expect.isNonEmpty relaxed "warnings include a relaxed base-Blocking item (the no-hide case)"

              for it in relaxed do
                  Expect.equal (enforcement it "baseSeverity") "blocking" "base severity present and blocking"
                  Expect.equal (enforcement it "effectiveSeverity") "advisory" "effective severity present and advisory"
                  Expect.isNonEmpty (enforcement it "mode") "mode present"
                  Expect.isNonEmpty (enforcement it "profile") "profile present"
                  Expect.isNonEmpty (enforcement it "maturity") "maturity present"
                  Expect.isNonEmpty (enforcement it "reason") "reason non-empty (guaranteed by real rollup)"
          }

          test "every blocker/warning/passing item carries all six enforcement fields verbatim from its F023 decision (AS2, FR-006, SC-005)" {
              let d = richDecision
              use doc = parse (AuditJson.ofShipDecision d)

              // Build the expected six-field map keyed by item identity from the SOURCE decision.
              let identityOf (item: EnforcedItem) =
                  match item.Id with
                  | GateItem g -> "gate:" + gateIdValue g
                  | FindingItem(fid, GovernedPath p) -> "finding:" + findingIdToken fid + "@" + p

              let expected =
                  allItems d
                  |> List.map (fun i ->
                      let e = i.Decision
                      identityOf i,
                      [ "baseSeverity", severityTok e.BaseSeverity
                        "maturity", maturityTok e.Maturity
                        "mode", modeTok e.Mode
                        "profile", profileTok e.Profile
                        "effectiveSeverity", severityTok e.EffectiveSeverity
                        "reason", e.Reason ])
                  |> Map.ofList

              let renderedIdentity (it: System.Text.Json.JsonElement) =
                  match itemKind it with
                  | "gate" -> "gate:" + itemId it
                  | "finding" -> "finding:" + itemId it + "@" + itemPath it
                  | k -> failwithf "unexpected kind %s" k

              for it in List.concat [ section doc "blockers"; section doc "warnings"; section doc "passing" ] do
                  let id = renderedIdentity it
                  Expect.equal (enforcementFields it) (Map.find id expected) (sprintf "six enforcement fields verbatim + ordered for %s" id)
          }

          test "a finding item's identity carries both findingIdToken and governed path; a gate item's its gateIdValue, neither re-derived (AS3, FR-004/FR-010)" {
              let d = richDecision
              use doc = parse (AuditJson.ofShipDecision d)

              for it in List.concat [ section doc "blockers"; section doc "warnings"; section doc "passing" ] do
                  match itemKind it with
                  | "gate" ->
                      Expect.isFalse (hasField it "path") "a gate item has no path field"
                      Expect.isNonEmpty (itemId it) "gate id present"
                  | "finding" ->
                      Expect.isTrue (hasField it "path") "a finding item carries its governed path"
                      Expect.isNonEmpty (itemId it) "finding id token present"
                  | k -> failtestf "unexpected kind %s" k
          }

          test "the same finding id on two different governed paths renders as two distinct entries (FR-004)" {
              let d = sameFindingIdTwoPathsDecision
              use doc = parse (AuditJson.ofShipDecision d)

              let findings =
                  List.concat [ section doc "blockers"; section doc "warnings"; section doc "passing" ]
                  |> List.filter (fun it -> itemKind it = "finding")

              let sameId = findings |> List.filter (fun it -> itemId it = "unknownGovernedPath")
              Expect.equal sameId.Length 2 "two entries for the same finding id"
              let paths = sameId |> List.map itemPath |> Set.ofList
              Expect.equal paths.Count 2 "each entry carries its own distinct path — not deduplicated"
          }

          test "a GateId / governed-path containing the id separator renders verbatim — no re-parse (FR-008/FR-010)" {
              // build:ship and src/boundary/Api.fs carry separators; the emitted id/path equal the
              // declared values exactly (no domain re-derivation across `:` or `/`).
              let d = richDecision
              use doc = parse (AuditJson.ofShipDecision d)

              let gateIds =
                  List.concat [ section doc "blockers"; section doc "warnings"; section doc "passing" ]
                  |> List.filter (fun it -> itemKind it = "gate")
                  |> List.map itemId
                  |> Set.ofList

              Expect.isTrue (Set.contains "build:ship" gateIds) "gate id with `:` separator rendered verbatim"

              let findingPaths =
                  List.concat [ section doc "blockers"; section doc "warnings"; section doc "passing" ]
                  |> List.filter (fun it -> itemKind it = "finding")
                  |> List.map itemPath
                  |> Set.ofList

              Expect.isTrue (Set.contains "src/boundary/Api.fs" findingPaths) "governed path with `/` rendered verbatim"
          } ]
