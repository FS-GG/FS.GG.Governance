module FS.GG.Governance.Adapters.DesignSystem.Tests.CatalogTests

open Expecto
open FS.GG.Governance.Kernel
open FS.GG.Governance.Adapters.Spi
open FS.GG.Governance.Adapters.DesignSystem
open FS.GG.Governance.Adapters.DesignSystem.Tests.FixtureFacts
open FS.GG.Governance.Adapters.DesignSystem.Tests.ProjectFact

// US2 (the tier split — deterministic block / agent route / human escalate; advisory fence,
// SC-002), US4 (render & explain — advertised = enforced, SC-004), and the commutative-hash /
// cache-key stability law (SC-005). Everything runs through the BUILT adapter + Spi + Kernel.

let private isPass =
    function
    | Pass -> true
    | _ -> false

let private isFail =
    function
    | Fail _ -> true
    | _ -> false

let private isUncertain =
    function
    | Uncertain _ -> true
    | _ -> false

let private blockingIds (change: DesignChange) : Set<RuleId> =
    let adapter = designAdapter
    let route = Route.route adapter.Fences adapter.Rules Gate change
    route.Blocking |> List.map (fun e -> e.Id) |> Set.ofList

/// The governance outcomes a rule emits when bridged and applied to `facts`.
let private outcomesOf (r: CheckRule<DesignSystemFact>) (facts: FactSet<DesignSystemFact>) : RuleOutcome list =
    let bridge = designAdapter.Bridge
    (CheckRule.toRule bridge r).Apply facts
    |> List.choose (fun a -> bridge.Project a.Value)

[<Tests>]
let tests =
    testList
        "Catalog"
        [ // ── US2 (SC-002) — deterministic block ──
          test "V2 the deterministic token/contrast/surface/evidence rules give DEFINITE verdicts and are the Blocking set" {
              let metFacts probe subject =
                  [ fact (SurfaceObservation(probe, subject, true)) ]

              let unmetFacts probe subject =
                  [ fact (SurfaceObservation(probe, subject, false)) ]

              // token-drift / contrast-policy / token-surface-gate: definite Pass / Fail; Unknown when absent.
              Expect.isTrue (isPass (Check.eval (metFacts "surface-matches" GeneratedTokenSurface) Catalog.tokenDrift.Check)) "token-drift Pass on a matching surface"
              Expect.isTrue (isFail (Check.eval (unmetFacts "surface-matches" GeneratedTokenSurface) Catalog.tokenDrift.Check)) "token-drift Fail on drift"
              Expect.isTrue (isUncertain (Check.eval [] Catalog.tokenDrift.Check)) "token-drift Uncertain when the fixture is absent (never a silent Pass)"

              Expect.isTrue (isPass (Check.eval (metFacts "contrast-meets" GeneratedTokenSurface) Catalog.contrastPolicy.Check)) "contrast-policy Pass when the ratio is met"
              Expect.isTrue (isUncertain (Check.eval [] Catalog.contrastPolicy.Check)) "contrast-policy Uncertain when the contrast fixture is missing"

              Expect.isTrue (isPass (Check.eval (metFacts "token-surface-gate" GeneratedTokenSurface) Catalog.tokenSurfaceGate.Check)) "token-surface-gate Pass when blessed"

              // evidence-measured: a deterministic verdict resting on synthetic evidence ⇒ Fail.
              let tainted =
                  [ fact (MeasurementState("contrast-px", Synthetic))
                    fact (MeasurementState("contrast-verdict", Real))
                    fact (VerdictRestsOn("contrast-verdict", "contrast-px")) ]

              Expect.isTrue (isFail (Check.eval tainted Catalog.evidenceMeasured.Check)) "evidence-measured Fail when a verdict rests on synthetic evidence"
              Expect.isTrue (isPass (Check.eval conformingFacts Catalog.evidenceMeasured.Check)) "evidence-measured Pass over the conforming (all-Real) fixtures"

              // They are exactly the deterministic Blocking severities.
              for r in [ Catalog.tokenDrift; Catalog.contrastPolicy; Catalog.tokenSurfaceGate; Catalog.evidenceMeasured ] do
                  let (RuleId id) = r.Id
                  Expect.equal r.Severity Blocking (sprintf "%s is Blocking" id)
                  Expect.equal r.Tier Deterministic (sprintf "%s is Deterministic" id)
          }

          // ── US2 (SC-002) — Opaque judgement routes, never resolves ──
          test "V2 each AgentReviewed judgement rule is Opaque, never resolves, and routes to a review carrying its Question" {
              let judgementRules =
                  [ Catalog.renderedMatchesIntent
                    Catalog.fourValues
                    Catalog.pagePatternCorrect
                    Catalog.colourInformational
                    Catalog.motionRestraint
                    Catalog.elevationLayering ]

              for r in judgementRules do
                  let (RuleId id) = r.Id
                  Expect.equal r.Tier AgentReviewed (sprintf "%s is AgentReviewed" id)
                  Expect.isFalse (Check.isReified r.Check) (sprintf "%s is Opaque ⇒ never Deterministic (FR-008)" id)
                  Expect.isTrue (isUncertain (Check.eval conformingFacts r.Check)) (sprintf "%s never resolves to Pass/Fail — Uncertain for any facts" id)
                  Expect.isTrue (Option.isSome r.Question) (sprintf "%s carries a reviewer Question" id)

                  // toRule routes it to a review whose prompt IS the rule's Question (cache miss).
                  let routed =
                      outcomesOf r []
                      |> List.exists (function
                          | NeedsReview req -> req.Question = r.Question
                          | _ -> false)

                  Expect.isTrue routed (sprintf "%s routes to a review carrying its Question" id)
          }

          // ── US2 (SC-002) — HumanOnly escalates + advisory-by-default + the single surface fence ──
          test "V2 adopt-new-policy is HumanOnly — escalates to a person and never resolves deterministically" {
              Expect.equal Catalog.adoptNewPolicy.Tier HumanOnly "adopt-new-policy is HumanOnly"
              Expect.isTrue (isUncertain (Check.eval conformingFacts Catalog.adoptNewPolicy.Check)) "adopt-new-policy never resolves by engine"

              let escalated =
                  outcomesOf Catalog.adoptNewPolicy []
                  |> List.exists (function
                      | Escalated id -> id = RuleId "adopt-new-policy"
                      | _ -> false)

              Expect.isTrue escalated "toRule escalates adopt-new-policy to a person"
          }

          test "V2 advisory by default; only a token-surface change trips the single fence and makes the Blocking set bite" {
              let plainChange = { Surfaces = Set.ofList [ RenderedCapture ] }
              let surfaceChange = { Surfaces = Set.ofList [ GeneratedTokenSurface ] }

              // The default posture is advisory — a non-token-surface change blocks nothing.
              Expect.isEmpty (blockingIds plainChange) "a change not touching the token surface blocks nothing (advisory default)"

              // A token-surface change trips the fence; the Blocking set is the deterministic
              // blocking rules plus the HumanOnly adopt-new-policy (HumanOnly escalates regardless).
              Expect.equal
                  (blockingIds surfaceChange)
                  (Set.ofList
                      [ RuleId "token-drift"
                        RuleId "contrast-policy"
                        RuleId "token-surface-gate"
                        RuleId "evidence-measured"
                        RuleId "adopt-new-policy" ])
                  "the token-surface fence makes exactly the fixed Blocking set bite (there is no dial)"
          }

          // ── US4 (SC-004) — render & explain: advertised = enforced ──
          test "V4 every rule renders to a non-empty sentence, explains itself, and its published Statement equals Check.render" {
              let contract = Contract.ofRules Catalog.catalog

              Expect.equal contract.Length Catalog.catalog.Length "one contract entry per rule"

              for (r, entry) in List.zip Catalog.catalog contract do
                  let (RuleId id) = r.Id
                  let rendered = Check.render r.Check
                  Expect.isTrue (rendered.Length > 0) (sprintf "%s renders to a non-empty sentence" id)
                  Expect.equal entry.Statement rendered (sprintf "%s: published Statement = Check.render (advertised = enforced)" id)

                  Expect.equal
                      (Explanation.verdict (Check.explain conformingFacts r.Check))
                      (Check.eval conformingFacts r.Check)
                      (sprintf "%s: explain top verdict = eval" id)
          }

          // ── SC-005 — commutative hash + cache-key stability ──
          test "V5 a deterministic rule's hash is invariant under commutative re-ordering while positional nodes stay positional" {
              let a = DesignSystem.surfaceObserved "spacing-scale" GeneratedTokenSurface
              let b = DesignSystem.surfaceObserved "control-height" GeneratedTokenSurface

              Expect.equal
                  (Check.hash (Check.allOf [ a; b ]))
                  (Check.hash (Check.allOf [ b; a ]))
                  "allOf is commutative in the hash — the cache key does not move under re-ordering"

              Expect.equal
                  (Check.hash (Check.anyOf [ a; b ]))
                  (Check.hash (Check.anyOf [ b; a ]))
                  "anyOf is commutative in the hash"

              // Positional structure (a probe's ordered Args/Reads) stays positional.
              Expect.notEqual
                  (Check.hash (DesignSystem.surfaceMatches GeneratedTokenSurface TokenDocument))
                  (Check.hash (DesignSystem.surfaceMatches TokenDocument GeneratedTokenSurface))
                  "surfaceMatches g c ≠ surfaceMatches c g (positional args)"
          }

          test "V5 two structurally-equal judgement rules produce the same agent-review cache key" {
              let r = Catalog.colourInformational
              // A structurally-equal twin — built the SAME way (M-ADPT-2: the reviewed artifact is now part of
              // the check, so the twin must declare it too): same reviewed artifact + Opaque name + prompt.
              let twin =
                  DesignSystem.reviewing [ RenderedCapture ] (Opaque("colour-informational", fun _ -> Unknown "requires judgement"))

              // The real key derivation folds the reviewed-artifact hashes in; equal reads ⇒ equal key.
              let artHash (_: ArtifactRef) = "content-hash"
              let key1 = CheckRule.cacheKey judge (Check.hash r.Check) (Check.reads r.Check |> List.map artHash) r.Question

              let key2 =
                  CheckRule.cacheKey judge (Check.hash twin) (Check.reads twin |> List.map artHash) (Some "Does colour carry information here, or is it decoration?")

              Expect.equal key1 key2 "identical reviewed artifacts + Opaque name + judge + prompt ⇒ identical cache key (no spurious re-review)"
          }

          test "V5b changing a reviewed artifact's content hash moves the agent-review key (M-ADPT-2)" {
              let r = Catalog.renderedMatchesIntent
              let renderedRef = DesignSystem.toRef RenderedCapture
              let baseHash (_: ArtifactRef) = "h0"
              let changed (ref: ArtifactRef) = if ref = renderedRef then "h-CHANGED" else "h0"
              let k0 = CheckRule.cacheKey judge (Check.hash r.Check) (Check.reads r.Check |> List.map baseHash) r.Question
              let k1 = CheckRule.cacheKey judge (Check.hash r.Check) (Check.reads r.Check |> List.map changed) r.Question
              Expect.notEqual k1 k0 "a changed rendered-capture.json must re-open the review"
              Expect.contains (Check.reads r.Check) renderedRef "the rule declares rendered-capture as reviewed"
          } ]
