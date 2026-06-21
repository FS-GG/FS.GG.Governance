module FS.GG.Governance.EvidenceReuse.Tests.ExplanationTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.EvidenceReuse.Tests.Support

// US2 — a recompute is always explained, never opaque (SC-003). Every `Recompute` carries a located cause:
// `NoPriorEvidence` when no entry shares the candidate's gate identity, or `InputsChanged (diff …)` naming
// exactly the differing non-identity categories of the most-recent same-gate entry (research D5, FR-006).

// Single-field variants of the NON-identity categories, each touching EXACTLY one `diff` category while
// keeping the gate identity (Check/Domain) fixed — so a one-field change against a `baseInputs` entry yields
// `InputsChanged [thatCategory]`. (Distinct from Support's `variantCommand`, which flips Command AND
// CommandVersion together; here each variant moves a single category.)
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
        "Explanation"
        [ test "same-gate single-field change ⇒ InputsChanged [thatCategory] (US2 #1, SC-003)" {
              // For each NON-identity category, the stored entry differs from the candidate in only that
              // category; both share the gate, so the cause names exactly that category.
              for (category, vary) in singleCategoryVariants do
                  // The entry is `baseInputs`; the candidate is the one-field variant ⇒ diff candidate entry
                  // = [thatCategory].
                  let candidate = vary baseInputs
                  let store = storeOf [ baseInputs, E1 ]

                  match EvidenceReuse.decide candidate store with
                  | Recompute(InputsChanged cats) ->
                      Expect.equal cats [ category ] (sprintf "the cause must name exactly %A" category)
                  | other -> failtestf "category %A: expected Recompute (InputsChanged [%A]), got %A" category category other
          }

          test "multi-field same-gate change ⇒ InputsChanged in F029's fixed diff order (US2 #1, SC-003)" {
              // Candidate differs from the entry in RuleHash and Head; the diff order is fixed (RuleHash
              // before Head), never reversed (contracts/reuse-decision-semantics.md worked table).
              let candidate = { baseInputs with RuleHash = RuleHash "r2"; Head = Revision "ccc" }
              let store = storeOf [ baseInputs, E1 ]

              match EvidenceReuse.decide candidate store with
              | Recompute(InputsChanged cats) ->
                  Expect.equal cats [ RuleHashCat; HeadRevisionCat ] "diff order is fixed: RuleHash before Head"
              | other -> failtestf "expected Recompute (InputsChanged [RuleHashCat; HeadRevisionCat]), got %A" other
          }

          test "no same-gate entry (Domain differs) ⇒ Recompute NoPriorEvidence, distinct from InputsChanged (US2 #2)" {
              // The only entry is for a different Domain ⇒ different gate ⇒ no prior evidence for this work.
              let entry = { baseInputs with Domain = DomainId "release" }
              let store = storeOf [ entry, E4 ]

              Expect.equal
                  (EvidenceReuse.decide baseInputs store)
                  (Recompute NoPriorEvidence)
                  "a different-gate entry is not prior evidence for this candidate"
          }

          test "no same-gate entry (Check differs) ⇒ Recompute NoPriorEvidence (Edge: no entry shares gate)" {
              let entry = { baseInputs with Check = CheckId "build:other" }
              let store = storeOf [ entry, E4 ]

              Expect.equal
                  (EvidenceReuse.decide baseInputs store)
                  (Recompute NoPriorEvidence)
                  "a different-check entry is not prior evidence for this candidate"
          }

          testPropertyWithConfig fscheckConfig "every Recompute carries NoPriorEvidence or a located non-empty InputsChanged (SC-003, FR-006)"
          <| fun (c: FreshnessInputs) (s: ReuseStore) ->
              match EvidenceReuse.decide c s with
              | Reuse _ -> true
              | Recompute NoPriorEvidence -> true
              | Recompute(InputsChanged cats) ->
                  // Non-empty AND never naming the gate-identity categories (they are equal by construction).
                  not (List.isEmpty cats)
                  && not (List.contains CheckIdentity cats)
                  && not (List.contains DomainIdentity cats) ]
