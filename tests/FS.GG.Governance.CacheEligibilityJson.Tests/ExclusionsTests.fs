module FS.GG.Governance.CacheEligibilityJson.Tests.ExclusionsTests

open Expecto
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.CacheEligibility
open FS.GG.Governance.CacheEligibilityJson
open FS.GG.Governance.CacheEligibilityJson.Tests.Support

// SC-007 / FR-012 (L-R11): the document carries ONLY the declared vocabularies — the schema version, the
// declared `gate` id strings, the closed `verdict`/`cause`/`categories` tokens, and the opaque `evidence`
// reference. No timestamp, host path, raw freshness input, freshness key/hash, environment value, exit code,
// severity, ship verdict, exit-code basis, or provenance reference appears.

/// The complete set of property names any document may carry.
let private allowedKeys =
    set [ "schemaVersion"; "entries"; "gate"; "verdict"; "kind"; "evidence"; "cause"; "categories" ]

/// The full F029 category-token vocabulary — the only strings a `categories` element may be.
let private categoryVocabulary =
    allCategories |> List.map (fun (c, _) -> categoryToken c) |> Set.ofList

/// Deny tokens that must never appear as a key anywhere in the document (field-name exclusion).
let private forbiddenKeys =
    [ "timestamp"; "time"; "date"; "path"; "hash"; "freshnessKey"; "key"; "environment"; "env"
      "exitCode"; "exit"; "severity"; "ship"; "exitCodeBasis"; "basis"; "provenance"; "attestation"
      "command"; "ruleHash"; "head"; "base"; "inputs"; "check"; "domain" ]
    // NB: some of these ('ruleHash','command','check','domain','head','base') are legitimate *category token
    // values* but must never be KEYS — this list is asserted against property NAMES only.

[<Tests>]
let tests =
    testList
        "Exclusions"
        [ testPropertyWithConfig fscheckConfig "only the declared keys appear anywhere (L-R11)" (fun (r: CacheEligibilityReport) ->
              use doc = parse (CacheEligibilityJson.ofReport r)
              allPropertyNames doc.RootElement |> List.forall allowedKeys.Contains)

          test "no forbidden field NAME appears (timestamp/path/hash/env/exitCode/severity/ship/provenance…)" {
              // A rich mixed report exercising every shape.
              let moved = baseInputs |> (fun i -> { i with RuleHash = RuleHash "r2" })
              let r =
                  report
                      [ candidate (gid "docs" "lint") baseInputs
                        candidate (gid "security" "scan") baseInputs
                        candidate (gid "build" "tests") moved ]
                      (storeOf [ baseInputs, refA ])
              use doc = parse (CacheEligibilityJson.ofReport r)
              let names = allPropertyNames doc.RootElement |> Set.ofList
              for bad in forbiddenKeys do
                  Expect.isFalse (names.Contains bad) (sprintf "forbidden key %s must not be a document field" bad)
          }

          testPropertyWithConfig fscheckConfig "verdict.kind ∈ {reusable, mustRecompute}; cause.kind ∈ {noPriorEvidence, inputsChanged}; categories ⊆ F029 vocabulary (FR-011)" (fun (r: CacheEligibilityReport) ->
              use doc = parse (CacheEligibilityJson.ofReport r)
              entriesOf doc
              |> List.forall (fun e ->
                  let v = entryVerdict e
                  match verdictKind v with
                  | "reusable" -> true
                  | "mustRecompute" ->
                      let c = verdictCause v
                      match causeKind c with
                      | "noPriorEvidence" -> true
                      | "inputsChanged" -> causeCategories c |> List.forall categoryVocabulary.Contains
                      | _ -> false
                  | _ -> false))

          testPropertyWithConfig fscheckConfig "no string value is anything but a gate id / closed token / category token / opaque evidence ref" (fun (r: CacheEligibilityReport) ->
              use doc = parse (CacheEligibilityJson.ofReport r)
              // Build the allowed value set: schemaVersion, the closed verdict/cause kinds, the report's own
              // gate ids + evidence refs + category tokens. Any leaked timestamp/path/hash would not be in it.
              let es = CacheEligibility.entries r
              let gateVals = es |> List.map (fun e -> gateIdValue e.Gate)
              let evidenceVals =
                  es
                  |> List.choose (fun e ->
                      match e.Verdict with
                      | FS.GG.Governance.CacheEligibility.Model.Reusable ref -> Some(EvidenceReuse.referenceValue ref)
                      | _ -> None)
              let allowedValues =
                  Set.ofList (
                      [ "fsgg.cache-eligibility/v1"; "reusable"; "mustRecompute"; "noPriorEvidence"; "inputsChanged" ]
                      @ gateVals @ evidenceVals @ Set.toList categoryVocabulary)
              allStringValues doc.RootElement |> List.forall allowedValues.Contains) ]
