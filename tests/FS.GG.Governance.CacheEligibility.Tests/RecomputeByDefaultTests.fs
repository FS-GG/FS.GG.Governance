module FS.GG.Governance.CacheEligibility.Tests.RecomputeByDefaultTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.CacheEligibility
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.CacheEligibility.Tests.Support

// User Story 1 — recompute by default when evidence is absent or stale (SC-001/SC-003, L-G2/L-G3/L-G4). With
// no prior recorded evidence, or with recorded evidence produced under different freshness inputs, each
// candidate's verdict defaults to `MustRecompute` carrying its named cause, and no candidate yields `Reusable`
// without a defensible F030 match. The safety property the whole row exists to protect.

// Single-field variants of the NON-identity categories, each touching EXACTLY one `diff` category while
// keeping the gate identity (Check/Domain) fixed — so a one-field change against a `baseInputs` entry yields
// `MustRecompute (InputsChanged [thatCategory])`. (Distinct from Support's `variantCommand`, which flips
// Command AND CommandVersion together; here each variant moves a single category — the F030 ExplanationTests
// precedent.)
let private singleCategoryVariants: (InputCategory * (FreshnessInputs -> FreshnessInputs)) list =
    [ CommandIdentity, (fun i -> { i with Command = Some(CommandId "msbuild") })
      EnvironmentClassCat, (fun i -> { i with Environment = Ci })
      RuleHashCat, (fun i -> { i with RuleHash = RuleHash "r2" })
      CoveredArtifactsCat, (fun i -> { i with CoveredArtifacts = [ ArtifactHash "h3" ] })
      CommandVersionCat, (fun i -> { i with CommandVersion = Some(CommandVersion "9.0") })
      GeneratorVersionCat, (fun i -> { i with GeneratorVersion = GeneratorVersion "g2" })
      BaseRevisionCat, (fun i -> { i with Base = Revision "ccc" })
      HeadRevisionCat, (fun i -> { i with Head = Revision "ddd" }) ]

[<Tests>]
let tests =
    testList
        "RecomputeByDefault"
        [ test "empty store ⇒ MustRecompute NoPriorEvidence (US1 #1, SC-001, L-G2/L-G3)" {
              let c = candidate (gid "build" "tests") baseInputs

              Expect.equal
                  (CacheEligibility.evaluateGate c EvidenceReuse.empty)
                  (MustRecompute NoPriorEvidence)
                  "an empty store yields recompute, no prior evidence"
          }

          test "store recording only OTHER gates ⇒ MustRecompute NoPriorEvidence (US1 #1, L-G3)" {
              // Entries exist, but none shares the candidate's gate identity (Check+Domain) ⇒ no prior
              // evidence for THIS work.
              let otherCheck = { baseInputs with Check = CheckId "build:other" }
              let otherDomain = { baseInputs with Domain = DomainId "release" }
              let store = storeOf [ otherCheck, refA; otherDomain, refB ]
              let c = candidate (gid "build" "tests") baseInputs

              Expect.equal
                  (CacheEligibility.evaluateGate c store)
                  (MustRecompute NoPriorEvidence)
                  "a store of only other gates is not prior evidence for this candidate"
          }

          test "same-gate single-field change ⇒ MustRecompute (InputsChanged [thatCategory]) (US1 #2, SC-003, L-G4)" {
              // For each NON-identity category, the stored entry is `baseInputs` and the candidate differs in
              // only that category; both share the gate, so the cause names exactly that category (no-hide).
              for (category, vary) in singleCategoryVariants do
                  let store = storeOf [ baseInputs, refA ]
                  let c = candidate (gid "build" "tests") (vary baseInputs)

                  match CacheEligibility.evaluateGate c store with
                  | MustRecompute(InputsChanged cats) ->
                      Expect.equal cats [ category ] (sprintf "the cause must name exactly %A" category)
                  | other -> failtestf "category %A: expected MustRecompute (InputsChanged [%A]), got %A" category category other
          }

          test "several same-gate changes ⇒ InputsChanged names them ALL in F029's fixed diff order, never truncated (US1 #2, L-G4)" {
              // RuleHash AND Head both moved; the cause names BOTH, RuleHash before Head, never just the first.
              let store = storeOf [ baseInputs, refA ]
              let c = candidate (gid "build" "tests") { baseInputs with RuleHash = RuleHash "r2"; Head = Revision "ccc" }

              match CacheEligibility.evaluateGate c store with
              | MustRecompute(InputsChanged cats) ->
                  Expect.equal cats [ RuleHashCat; HeadRevisionCat ] "both categories named, fixed order, never truncated to the first"
              | other -> failtestf "expected MustRecompute (InputsChanged [RuleHashCat; HeadRevisionCat]), got %A" other
          }

          testPropertyWithConfig fscheckConfig "the named cause is EXACTLY F030's diff for any same-gate change (US1 #2, FR-004, L-G4)"
          <| fun (s: ReuseStore) ->
              // Against any store, an InputsChanged cause carries exactly the categories F030 `decide` would
              // name — the relabel introduces no new or different categories.
              let c = candidate (gid "build" "tests") baseInputs

              match CacheEligibility.evaluateGate c s, EvidenceReuse.decide baseInputs s with
              | MustRecompute(InputsChanged a), Recompute(InputsChanged b) -> a = b
              | MustRecompute NoPriorEvidence, Recompute NoPriorEvidence -> true
              | Reusable a, Reuse b -> a = b
              | _ -> false

          testPropertyWithConfig fscheckConfig "no candidate is Reusable unless F030 deems a defensible match (US1 #3, SC-001, L-G2)"
          <| fun (c: CandidateGate) (s: ReuseStore) ->
              // isReusable holds IFF F030 `decide` returns Reuse — recompute by default, never a falsely-claimed
              // reuse.
              let f030Reuses =
                  match EvidenceReuse.decide c.Inputs s with
                  | Reuse _ -> true
                  | Recompute _ -> false

              CacheEligibility.isReusable (CacheEligibility.evaluateGate c s) = f030Reuses

          testPropertyWithConfig fscheckConfig "recompute-by-default: a Recompute relabels to MustRecompute carrying the same cause (US1, FR-004, L-G2)"
          <| fun (c: CandidateGate) (s: ReuseStore) ->
              // Whenever F030 `decide` is a Recompute, the verdict is MustRecompute carrying the IDENTICAL cause
              // — the relabel is information-preserving, introducing no new policy.
              match EvidenceReuse.decide c.Inputs s with
              | Recompute cause -> CacheEligibility.evaluateGate c s = MustRecompute cause
              | Reuse _ -> true ]
