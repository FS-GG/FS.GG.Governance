module FS.GG.Governance.VerdictReuse.Tests.Support

open System
open System.IO
open Expecto
open FsCheck
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.VerdictReuse
open FS.GG.Governance.VerdictReuse.Model

// Shared REAL-input builders + FsCheck generators for the F036 tests (Principle V — every value below is a
// real, literally-constructible typed `AgentReviewInputs` (incl. real F035 newtypes over real F029
// `RuleHash`/`ArtifactHash`s) paired with a literal `VerdictRef`, never a mock; the operations are pure so no
// upstream chain is needed, no clock read, no model invoked, no process spawned). The single-input variation
// helpers are the table the validity/distinction/diff tests drive. No I/O beyond repo-root resolution.

// ── A real base input set every test varies from ──

/// A complete, literal `AgentReviewInputs` — every input present and distinct so a single-input change is
/// unambiguous. The F035 worked-example values (contracts/lookup-decision-semantics.md base request `R`).
let baseInputs: AgentReviewInputs =
    { Model = ModelId "claude-opus-4"
      ModelVersion = ModelVersion "20260101"
      Config = ModelConfig "temp=0"
      PromptHash = ReviewerPromptHash "p1"
      Question = QuestionText "explains API?"
      Check = RuleHash "c1"
      ReviewedArtifacts = [ ArtifactHash "h2"; ArtifactHash "h1"; ArtifactHash "h1" ] }

// ── Literal verdict references (opaque, edge-minted; incl. an empty-string ref) ──

let refV1 = VerdictRef "verdict:v1"
let refV2 = VerdictRef "verdict:v2"
let refV3 = VerdictRef "verdict:v3"
let refEmpty = VerdictRef ""

// ── One representative single-input variant per comparable input ──
// Each takes `baseInputs` and changes EXACTLY the named input to a distinct value, paired with its
// `ReviewInput`. Reviewed-artifact variant uses a genuinely different SET (not a reorder/dup).

let variantModel (i: AgentReviewInputs) = { i with Model = ModelId "claude-sonnet-4" }
let variantModelVersion (i: AgentReviewInputs) = { i with ModelVersion = ModelVersion "20260202" }
let variantPromptHash (i: AgentReviewInputs) = { i with PromptHash = ReviewerPromptHash "p2" }
let variantConfig (i: AgentReviewInputs) = { i with Config = ModelConfig "temp=1" }
let variantCheck (i: AgentReviewInputs) = { i with Check = RuleHash "c2" }
let variantArtifacts (i: AgentReviewInputs) = { i with ReviewedArtifacts = [ ArtifactHash "h3" ] }
let variantQuestion (i: AgentReviewInputs) = { i with Question = QuestionText "different?" }

/// All 7 comparable inputs, each paired with a single-input variation function. Table-driven
/// validity/distinction tests iterate this so EVERY input is covered (SC-001).
let allInputs: (ReviewInput * (AgentReviewInputs -> AgentReviewInputs)) list =
    [ ModelIdInput, variantModel
      ModelVersionInput, variantModelVersion
      PromptHashInput, variantPromptHash
      ModelConfigInput, variantConfig
      CheckHashInput, variantCheck
      ReviewedArtifactsInput, variantArtifacts
      QuestionTextInput, variantQuestion ]

/// The SIX non-check inputs (a same-check entry differing in exactly one of these ⇒ `InputsChanged
/// [thatInput]`). Drives the located-cause / attribution tests (SC-002, SC-003); `CheckHashInput` is
/// excluded because a check change is a different-work `NoCachedVerdict`, never an `InputsChanged` element.
let nonCheckInputs: (ReviewInput * (AgentReviewInputs -> AgentReviewInputs)) list =
    allInputs |> List.filter (fun (ri, _) -> ri <> CheckHashInput)

/// All 7 `ReviewInput` cases paired with their expected `inputGroup` (the data-model table) — drives the
/// total `inputGroup` coverage test.
let inputGroupTable: (ReviewInput * IdentityGroup) list =
    [ ModelIdInput, JudgeIdentity
      ModelVersionInput, JudgeIdentity
      ModelConfigInput, JudgeIdentity
      PromptHashInput, PromptIdentity
      QuestionTextInput, PromptIdentity
      CheckHashInput, CheckArtifactIdentity
      ReviewedArtifactsInput, CheckArtifactIdentity ]

// ── Store builders ──

/// Build a `VerdictStore` by folding `record` over `(inputs, verdict)` pairs (left-to-right ⇒ last pair is
/// newest/head). NOTE: `record`'s store parameter is LAST, so it cannot be the fold accumulator directly —
/// wrap it (plan T008 note).
let storeOf (pairs: (AgentReviewInputs * VerdictRef) list) : VerdictStore =
    pairs |> List.fold (fun s (i, v) -> VerdictReuse.record i v s) VerdictReuse.empty

/// Direct hand-built store (newest-first as given) — for tests that must bypass `record`'s de-dup, e.g.
/// multiple full-match entries.
let handStore (pairs: (AgentReviewInputs * VerdictRef) list) : VerdictStore =
    VerdictStore(pairs |> List.map (fun (i, v) -> { Inputs = i; Verdict = v }))

// ── FsCheck generators (real values, no mocks) ──

let private shortStringGen: Gen<string> =
    Gen.elements
        [ ""; "a"; "b"; "h1"; "h2"; "h3"; "c1"; "c2"; "p1"; "p2"; "temp=0"; "claude-opus-4"; "20260101"; "héllo" ]

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

let private genVerdictRef: Gen<VerdictRef> =
    Gen.elements [ ""; "verdict:v1"; "verdict:v2"; "verdict:v3"; "x"; "héllo" ] |> Gen.map VerdictRef

let private genCachedVerdict: Gen<CachedVerdict> =
    gen {
        let! i = genAgentReviewInputs
        let! v = genVerdictRef
        return { Inputs = i; Verdict = v }
    }

// A hand-built `VerdictStore` (a list of cached entries, newest-first). Built directly so the generator does
// not depend on the `record` implementation under test.
let private genVerdictStore: Gen<VerdictStore> = Gen.listOf genCachedVerdict |> Gen.map VerdictStore

/// A permutation+duplication of an `ArtifactHash list` that preserves its SET (for order/dup invariance
/// properties). Same distinct elements, possibly-different order, possibly with repeats.
let private genSameSetPermutation (arts: ArtifactHash list) : Gen<ArtifactHash list> =
    let distinct = arts |> List.distinct

    if List.isEmpty distinct then
        Gen.constant []
    else
        gen {
            let! shuffled = Gen.shuffle (List.toArray distinct)
            let! extra = Gen.elements distinct
            return (List.ofArray shuffled) @ [ extra ]
        }

type Generators =
    static member AgentReviewInputs() : Arbitrary<AgentReviewInputs> = Arb.fromGen genAgentReviewInputs
    static member VerdictRef() : Arbitrary<VerdictRef> = Arb.fromGen genVerdictRef
    static member VerdictStore() : Arbitrary<VerdictStore> = Arb.fromGen genVerdictStore

/// FsCheck config registering the real `AgentReviewInputs` / `VerdictRef` / `VerdictStore` generators.
let fscheckConfig =
    { FsCheckConfig.defaultConfig with arbitrary = [ typeof<Generators> ] }

/// Build a same-set permutation generator for a given input's reviewed artifacts (used by the set-semantics
/// order/dup invariance properties).
let samePermutationOf = genSameSetPermutation

// ── Repo root (for the surface baseline path) ──

/// Locate the repo root (the dir holding the solution) by walking up from the test binary.
let rec private findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root (FS.GG.Governance.sln) not found"
    | d ->
        if File.Exists(Path.Combine(d.FullName, "FS.GG.Governance.sln")) then
            d.FullName
        else
            findRepoRoot d.Parent

let repoRoot = findRepoRoot (DirectoryInfo(AppContext.BaseDirectory))
