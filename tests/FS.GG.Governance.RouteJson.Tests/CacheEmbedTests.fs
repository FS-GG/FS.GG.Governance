module FS.GG.Governance.RouteJson.Tests.CacheEmbedTests

open System.Text.Json
open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.CacheEligibility
open FS.GG.Governance.Route.Model
open FS.GG.Governance.RouteJson
open FS.GG.Governance.RouteJson.Tests.Support

// F045 (US1/US3/US4) — the embedded cache-eligibility verdict on route.json. Every report below is a
// REAL `CacheEligibility.evaluate` roll-up over real `FreshnessInputs` and a real `ReuseStore`
// (`EvidenceReuse.record`) — no mocks of the cores (Principle V). The RouteResult is the genuine
// F015->F017->F018->F019 chain (Support.resultOf). The emitted bytes are inspected by a read-only
// JsonDocument parse. These tests FAIL before `RouteJson.fs` carries the verdict and pass after.

// ── a real RouteResult whose selected gates are build:format, build:tests, docs:lint ──

let private embedFacts =
    facts
        "src"
        [ "src/build/**", "build"; "src/docs/**", "docs" ]
        [ surface GovernedRoot "root" [ "src" ] ]
        [ check "build" "tests" None Medium Local BlockOnShip
          check "build" "format" None Cheap Local Observe
          check "docs" "lint" None Cheap Local Warn ]
        []

// build + docs paths select the three gates; the unclassified in-root path yields one F017 finding (so
// the gate-scoped / findings-carry-no-verdict law has a real finding to assert against).
let private embedResult = resultOf embedFacts [ "src/build/Core.fs"; "src/docs/Guide.md"; "src/loose/x.fs" ]

// ── real freshness inputs + store + report builders (the F041/F030/F029 worked example) ──

let private baseInputs: FreshnessInputs =
    { Check = CheckId "tests"
      Domain = DomainId "build"
      Command = Some(CommandId "dotnet")
      Environment = Local
      RuleHash = RuleHash "r1"
      CoveredArtifacts = [ ArtifactHash "h1" ]
      CommandVersion = Some(CommandVersion "8.0")
      GeneratorVersion = GeneratorVersion "g1"
      Base = Revision "aaa"
      Head = Revision "bbb" }

/// build:tests' inputs (identity Check=tests/Domain=build) and docs:lint's (Check=lint/Domain=docs) — so
/// each candidate's freshness identity reflects its own gate and a recorded base entry matches it.
let private buildInputs = baseInputs
let private docsInputs = { baseInputs with Check = CheckId "lint"; Domain = DomainId "docs" }

let private refA = EvidenceRef "ev-A"
let private refD = EvidenceRef "ev-D"

let private candidate (gate: string) (inputs: FreshnessInputs) : CandidateGate = { Gate = GateId gate; Inputs = inputs }

let private storeOf (entries: (FreshnessInputs * EvidenceRef) list) : ReuseStore =
    entries |> List.fold (fun s (i, e) -> EvidenceReuse.record i e s) EvidenceReuse.empty

/// Records both gates' base inputs, so an exact-match candidate is `Reusable` and a one-field-moved
/// candidate is `MustRecompute (InputsChanged …)`.
let private recordedStore = storeOf [ buildInputs, refA; docsInputs, refD ]

let private reportOf (cands: CandidateGate list) (store: ReuseStore) : CacheEligibilityReport =
    CacheEligibility.evaluate cands store

/// build:tests exact ⇒ Reusable ev-A; docs:lint RuleHash moved ⇒ InputsChanged [ruleHash]; build:format
/// is ABSENT from the report ⇒ notEvaluated.
let private mixedReport =
    reportOf
        [ candidate "build:tests" buildInputs
          candidate "docs:lint" { docsInputs with RuleHash = RuleHash "r2" } ]
        recordedStore

/// build:tests against the EMPTY store ⇒ MustRecompute NoPriorEvidence.
let private noPriorReport = reportOf [ candidate "build:tests" buildInputs ] EvidenceReuse.empty

// ── read helpers over the emitted cache verdict ──

let private gateById (doc: JsonDocument) (gid: string) : JsonElement =
    selectedGates doc |> List.find (fun g -> strField g "id" = gid)

let private cacheOf (gateEl: JsonElement) : JsonElement = gateEl.GetProperty "cacheEligibility"
let private kindOf (verdict: JsonElement) : string = strField verdict "kind"

let private causeKind (verdict: JsonElement) : string = strField (verdict.GetProperty "cause") "kind"

let private causeCategories (verdict: JsonElement) : string list =
    [ for c in (verdict.GetProperty("cause").GetProperty "categories").EnumerateArray() ->
          match c.GetString() with
          | null -> failwith "null category"
          | s -> s ]

/// The report's own verdict for a gate id — to cross-check the projection renders it verbatim.
let private verdictFor (report: CacheEligibilityReport) (gid: string) : CacheEligibilityVerdict option =
    CacheEligibility.entries report
    |> List.tryPick (fun e -> if gateIdValue e.Gate = gid then Some e.Verdict else None)

[<Tests>]
let tests =
    testList
        "CacheEmbed (US1/US3/US4)"
        [
          // ── US1: verdict shapes matched by GateId ──

          test "a reusable gate carries { kind:reusable, evidence:<ref> } verbatim (US1.1, SC-001)" {
              use doc = parse (RouteJson.ofRouteResult embedResult (Some mixedReport))
              let v = cacheOf (gateById doc "build:tests")
              Expect.equal (kindOf v) "reusable" "build:tests is reusable"
              Expect.equal (strField v "evidence") "ev-A" "the opaque evidence reference verbatim"
              Expect.isFalse (hasField v "cause") "reusable carries no cause"
          }

          test "a must-recompute (noPriorEvidence) gate carries its cause, no evidence (US1.2)" {
              use doc = parse (RouteJson.ofRouteResult embedResult (Some noPriorReport))
              let v = cacheOf (gateById doc "build:tests")
              Expect.equal (kindOf v) "mustRecompute" "build:tests must recompute"
              Expect.equal (causeKind v) "noPriorEvidence" "cause is noPriorEvidence"
              Expect.isFalse (hasField v "evidence") "mustRecompute carries no evidence (no-hide, FR-009)"
              Expect.isFalse (hasField (v.GetProperty "cause") "categories") "noPriorEvidence has no categories"
          }

          test "an inputsChanged gate names exactly the report's changed categories in report order (US1.3, SC-005)" {
              use doc = parse (RouteJson.ofRouteResult embedResult (Some mixedReport))
              let v = cacheOf (gateById doc "docs:lint")
              Expect.equal (kindOf v) "mustRecompute" "docs:lint must recompute"
              Expect.equal (causeKind v) "inputsChanged" "cause is inputsChanged"

              // cross-check against the report's own verdict — the categories are rendered verbatim, in
              // report order, none dropped/added (matched by GateId, never re-derived).
              match verdictFor mixedReport "docs:lint" with
              | Some(MustRecompute(InputsChanged cats)) ->
                  Expect.equal (causeCategories v) (cats |> List.map categoryToken) "categories verbatim, in report order"
              | other -> failtestf "expected docs:lint InputsChanged in the report, got %A" other
          }

          test "a selected gate absent from the report renders notEvaluated, never reusable (US1.4, L2)" {
              use doc = parse (RouteJson.ofRouteResult embedResult (Some mixedReport))
              let v = cacheOf (gateById doc "build:format")
              Expect.equal (kindOf v) "notEvaluated" "build:format (absent from report) is notEvaluated"
              Expect.equal (fieldOrder v) [ "kind" ] "notEvaluated carries only kind"
          }

          // ── US3: no-hide, additive, no-fabricate ──

          test "the None case renders cacheEligibilityEvaluated:false and every gate notEvaluated (L2, L9)" {
              use doc = parse (RouteJson.ofRouteResult embedResult None)
              Expect.isFalse (doc.RootElement.GetProperty("cacheEligibilityEvaluated").GetBoolean()) "evaluated flag false under None"

              for g in selectedGates doc do
                  Expect.equal (kindOf (cacheOf g)) "notEvaluated" (sprintf "%s is notEvaluated under None" (strField g "id"))
          }

          test "Some _ renders cacheEligibilityEvaluated:true (L9)" {
              use doc = parse (RouteJson.ofRouteResult embedResult (Some mixedReport))
              Expect.isTrue (doc.RootElement.GetProperty("cacheEligibilityEvaluated").GetBoolean()) "evaluated flag true under Some"
          }

          test "additive: every non-cache field is byte-identical to the None projection (L7, SC-004)" {
              use docSome = parse (RouteJson.ofRouteResult embedResult (Some mixedReport))
              use docNone = parse (RouteJson.ofRouteResult embedResult None)

              // schemaVersion, findings, cost untouched by the embedded verdict content.
              Expect.equal (strField docSome.RootElement "schemaVersion") (strField docNone.RootElement "schemaVersion") "schemaVersion unchanged"
              Expect.equal
                  (docSome.RootElement.GetProperty("findings").GetRawText())
                  (docNone.RootElement.GetProperty("findings").GetRawText())
                  "findings byte-identical"
              Expect.equal
                  (docSome.RootElement.GetProperty("cost").GetRawText())
                  (docNone.RootElement.GetProperty("cost").GetRawText())
                  "cost byte-identical"

              // each gate: identical field set/order, and every field EXCEPT cacheEligibility byte-identical.
              for (gs, gn) in List.zip (selectedGates docSome) (selectedGates docNone) do
                  Expect.equal (fieldOrder gs) (fieldOrder gn) "same gate field order"

                  for name in fieldOrder gs do
                      if name <> "cacheEligibility" then
                          Expect.equal (gs.GetProperty(name).GetRawText()) (gn.GetProperty(name).GetRawText()) (sprintf "gate field %s unchanged" name)
          }

          test "findings carry no cacheEligibility verdict (gate-scoped, L4, FR-004)" {
              use doc = parse (RouteJson.ofRouteResult embedResult (Some mixedReport))
              Expect.isNonEmpty (findings doc) "fixture has at least one finding"

              for f in findings doc do
                  Expect.isFalse (hasField f "cacheEligibility") "a finding carries no cacheEligibility"
          }

          test "an orphan report entry (GateId matching no selected gate) adds nothing (L5, FR-006)" {
              let orphan = reportOf [ candidate "ghost:gate" buildInputs ] recordedStore
              use docOrphan = parse (RouteJson.ofRouteResult embedResult (Some orphan))
              use docNone = parse (RouteJson.ofRouteResult embedResult None)

              // no extra gate is invented, and the orphan id appears nowhere.
              Expect.equal (selectedGateIds docOrphan) (selectedGateIds docNone) "orphan adds no gate"
              Expect.isFalse (selectedGateIds docOrphan |> List.contains "ghost:gate") "orphan gate id absent"
          }

          test "no raw freshness input value / cache-derived severity leaks; evidence is verbatim (L8, SC-007)" {
              let json = RouteJson.ofRouteResult embedResult (Some mixedReport)
              let lower = json.ToLowerInvariant()
              // The category TOKENS (e.g. ruleHash) are the legitimate no-hide cause vocabulary, NOT raw
              // inputs. What must never leak are the raw freshness-input VALUES (RuleHash "r1"/"r2",
              // ArtifactHash "h1", Revision "aaa"/"bbb", GeneratorVersion "g1") and any cache-derived
              // severity/enforcement field — the projection computes no key/hash and dereferences nothing.
              for token in [ "\"r1\""; "\"r2\""; "\"h1\""; "\"aaa\""; "\"bbb\""; "\"g1\""; "\"severity\""; "effectiveseverity"; "enforcement" ] do
                  Expect.isFalse (lower.Contains token) (sprintf "no raw input value / enforcement token %s leaks into the cache verdict" token)
              // the evidence reference is the exact opaque token, never dereferenced/expanded.
              use doc = parse json
              Expect.equal (strField (cacheOf (gateById doc "build:tests")) "evidence") "ev-A" "evidence verbatim, never dereferenced"
          }

          // ── US4: deterministic, versioned, ordered contract ──

          test "schemaVersion is fsgg.route/v2 (US4.3, FR-013)" {
              use doc = parse (RouteJson.ofRouteResult embedResult (Some mixedReport))
              Expect.equal (strField doc.RootElement "schemaVersion") "fsgg.route/v2" "v2 contract"
              Expect.equal RouteJson.schemaVersion "fsgg.route/v2" "constant is v2"
          }

          test "byte-identical for identical inputs (L10, SC-003)" {
              Expect.equal
                  (RouteJson.ofRouteResult embedResult (Some mixedReport))
                  (RouteJson.ofRouteResult embedResult (Some mixedReport))
                  "repeated projection is byte-identical"
          }

          test "cache entries follow the document's existing GateId-ordinal gate order (L10)" {
              use doc = parse (RouteJson.ofRouteResult embedResult (Some mixedReport))
              // the gate order is the RouteResult's existing ordinal order; the verdict rides each gate.
              Expect.equal (selectedGateIds doc) [ "build:format"; "build:tests"; "docs:lint" ] "existing gate order preserved"
          }

          test "a duplicate GateId in the report resolves to the first entry by report order, deterministically (L6, FR-007)" {
              // two build:tests entries with DIFFERENT verdicts (exact-match reusable + empty-store
              // recompute). The projection keeps the FIRST by the report's list position.
              let dup =
                  CacheEligibility.evaluate
                      [ candidate "build:tests" buildInputs
                        candidate "build:tests" { buildInputs with RuleHash = RuleHash "moved" } ]
                      recordedStore

              let firstVerdict =
                  CacheEligibility.entries dup
                  |> List.find (fun e -> gateIdValue e.Gate = "build:tests")
                  |> fun e -> e.Verdict

              use doc = parse (RouteJson.ofRouteResult embedResult (Some dup))
              let v = cacheOf (gateById doc "build:tests")

              let expectedKind =
                  match firstVerdict with
                  | Reusable _ -> "reusable"
                  | MustRecompute _ -> "mustRecompute"

              Expect.equal (kindOf v) expectedKind "first-by-report-order verdict wins"
              // and it is deterministic across repeats.
              Expect.equal (RouteJson.ofRouteResult embedResult (Some dup)) (RouteJson.ofRouteResult embedResult (Some dup)) "duplicate resolution is deterministic"
          }

          // ── US4 totality: every edge returns a document with the section present, never throws ──

          test "totality: None / empty report / empty route / finding-only route each yield a document with the section (L11, SC-006)" {
              let emptyRoute = resultOf embedFacts []
              let findingOnly = resultOf embedFacts [ "src/loose/x.fs" ]

              let cases =
                  [ "None", RouteJson.ofRouteResult embedResult None
                    "empty report", RouteJson.ofRouteResult embedResult (Some(CacheEligibilityReport []))
                    "empty route", RouteJson.ofRouteResult emptyRoute (Some mixedReport)
                    "finding-only", RouteJson.ofRouteResult findingOnly None ]

              for (label, json) in cases do
                  use doc = parse json
                  Expect.equal doc.RootElement.ValueKind JsonValueKind.Object (sprintf "%s yields a JSON object" label)
                  Expect.isTrue (hasField doc.RootElement "cacheEligibilityEvaluated") (sprintf "%s carries the cache section" label)
          }

          test "an evaluated-but-empty report still reports evaluated:true with every gate notEvaluated (L9)" {
              use doc = parse (RouteJson.ofRouteResult embedResult (Some(CacheEligibilityReport [])))
              Expect.isTrue (doc.RootElement.GetProperty("cacheEligibilityEvaluated").GetBoolean()) "empty report is evaluated:true (distinct from None)"

              for g in selectedGates doc do
                  Expect.equal (kindOf (cacheOf g)) "notEvaluated" "every gate notEvaluated under an empty report"
          } ]
