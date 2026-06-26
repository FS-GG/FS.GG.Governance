module FS.GG.Governance.AgentReviewKey.Tests.Support

open System
open System.IO
open Expecto
open FsCheck
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.AgentReviewKey.Model

// Shared REAL-input builders + FsCheck generators for the F035 tests (Principle V — every value below is a
// real, literally-constructible typed `AgentReviewInputs`, including real F029 `RuleHash`/`ArtifactHash`s,
// never a mock; the operations are pure so no upstream chain is needed). The single-input variation helpers
// are the table the distinction/diff tests drive. No I/O beyond repo-root resolution.

// ── A real base input set every test varies from ──

/// A complete, literal `AgentReviewInputs` — every input present and distinct so a single-input change is
/// unambiguous. The worked-example values from contracts/agent-review-key-format.md.
let baseInputs: AgentReviewInputs =
    { Model = ModelId "claude-opus-4"
      ModelVersion = ModelVersion "20260101"
      Config = ModelConfig "temp=0"
      PromptHash = ReviewerPromptHash "p1"
      Question = QuestionText "explains API?"
      Check = RuleHash "c1"
      ReviewedArtifacts = [ ArtifactHash "h2"; ArtifactHash "h1"; ArtifactHash "h1" ] }

/// The exact canonical key for `baseInputs` (contracts/agent-review-key-format.md worked example). The
/// artifact set is deduped to {h1,h2} and ordinally sorted; there is NO trailing newline.
let exampleKey =
    String.concat
        "\n"
        [ "mid=13:claude-opus-4"
          "mver=8:20260101"
          "prompt=2:p1"
          "cfg=6:temp=0"
          "chk=2:c1"
          "art=2;2:h1;2:h2"
          "q=13:explains API?" ]

// ── One representative single-input variant per comparable input ──
// Each takes `baseInputs` and changes EXACTLY the named input to a distinct value. Paired with its
// `ReviewInput` for table-driven tests.

let private variantModel (i: AgentReviewInputs) = { i with Model = ModelId "claude-sonnet-4" }
let private variantModelVersion (i: AgentReviewInputs) = { i with ModelVersion = ModelVersion "20260202" }
let private variantPromptHash (i: AgentReviewInputs) = { i with PromptHash = ReviewerPromptHash "p2" }
let private variantConfig (i: AgentReviewInputs) = { i with Config = ModelConfig "temp=1" }
let private variantCheck (i: AgentReviewInputs) = { i with Check = RuleHash "c2" }
let private variantArtifacts (i: AgentReviewInputs) = { i with ReviewedArtifacts = [ ArtifactHash "h3" ] }
let private variantQuestion (i: AgentReviewInputs) = { i with Question = QuestionText "different?" }

/// The 7 comparable inputs, each paired with a single-input variation function. Table-driven
/// distinction/diff tests iterate this so EVERY input is covered (SC-001, SC-003).
let allInputs: (ReviewInput * (AgentReviewInputs -> AgentReviewInputs)) list =
    [ ModelIdInput, variantModel
      ModelVersionInput, variantModelVersion
      PromptHashInput, variantPromptHash
      ModelConfigInput, variantConfig
      CheckHashInput, variantCheck
      ReviewedArtifactsInput, variantArtifacts
      QuestionTextInput, variantQuestion ]

/// All 7 `ReviewInput` cases (for total/injective `inputToken` coverage).
let allInputCases: ReviewInput list = allInputs |> List.map fst

// ── FsCheck generators (real values, no mocks) ──

let private shortStringGen: Gen<string> =
    Gen.elements
        [ ""; "a"; "b"; "h1"; "h2"; "h3"; "c1"; "p1"; "temp=0"; "claude-opus-4"; "20260101"; "héllo"; "x:y=z" ]

let private genAgentReviewInputs: Gen<AgentReviewInputs> =
    gen {
        let! model = shortStringGen
        let! modelVersion = shortStringGen
        let! config = shortStringGen
        let! promptHash = shortStringGen
        let! question = shortStringGen
        let! check = shortStringGen
        let! arts = Gen.listOf shortStringGen

        return
            { Model = ModelId model
              ModelVersion = ModelVersion modelVersion
              Config = ModelConfig config
              PromptHash = ReviewerPromptHash promptHash
              Question = QuestionText question
              Check = RuleHash check
              ReviewedArtifacts = arts |> List.map ArtifactHash }
    }

/// A permutation+duplication of an `ArtifactHash list` that preserves its SET (for order/dup invariance
/// properties). Returns a list with the same distinct elements in a possibly-different order, possibly with
/// repeats.
let private genSameSetPermutation (arts: ArtifactHash list) : Gen<ArtifactHash list> =
    let distinct = arts |> List.distinct

    if List.isEmpty distinct then
        Gen.constant []
    else
        gen {
            let! shuffled = Gen.shuffle (List.toArray distinct)
            // append one duplicate of an existing distinct element so duplication is exercised too.
            let! extra = Gen.elements distinct
            return (List.ofArray shuffled) @ [ extra ]
        }

type Generators =
    static member AgentReviewInputs() : Arbitrary<AgentReviewInputs> = Arb.fromGen genAgentReviewInputs

/// FsCheck config registering the real `AgentReviewInputs` generator.
let fscheckConfig =
    { FsCheckConfig.defaultConfig with arbitrary = [ typeof<Generators> ] }

/// Build a same-set permutation generator for a given input's reviewed artifacts (used by the
/// set-semantics order/dup invariance properties).
let samePermutationOf = genSameSetPermutation
// 074: findRepoRoot consolidated into the shared RepositoryHelpers (sln||slnx superset).
let repoRoot = FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot
