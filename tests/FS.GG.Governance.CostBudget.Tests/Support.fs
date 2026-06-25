module FS.GG.Governance.CostBudget.Tests.Support

open System
open System.IO
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.CacheEligibility
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.AgentReviewKey
open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.CostBudget.Model
open FS.GG.Governance.CostBudget.Findings

// Shared REAL-input builders for the F25 CostBudget tests (Principle V — every value below is a real,
// literally-constructible typed value: real F018 `GateId`, real F041 `CacheEligibilityVerdict` produced by
// the real `CacheEligibility.evaluateGate` over a real `ReuseStore`, real F036 `CacheKey` from the real
// `AgentReviewKey.compute`. No mock, no clock, no I/O beyond repo-root resolution.) The base-input + per-
// category-variant table is the F029/F030/F041 `Support.fs` shape reused so a single-field change is
// unambiguous.

// ── Gate identity helper (F018 GateId, "<domain>:<checkId>") ──

let gid (domain: string) (check: string) : GateId = GateId(domain + ":" + check)

// ── A real base input set every test varies from (the F029/F030/F041 worked example) ──

let baseInputs: FreshnessInputs =
    { Check = CheckId "build:tests"
      Domain = DomainId "build"
      Command = Some(CommandId "dotnet")
      Environment = Local
      RuleHash = RuleHash "r1"
      CoveredArtifacts = [ ArtifactHash "h2"; ArtifactHash "h1" ]
      CommandVersion = Some(CommandVersion "8.0")
      GeneratorVersion = GeneratorVersion "g1"
      Base = Revision "aaa"
      Head = Revision "bbb" }

let private variantCheck (i: FreshnessInputs) = { i with Check = CheckId "build:other" }
let private variantDomain (i: FreshnessInputs) = { i with Domain = DomainId "release" }
let private variantCommand (i: FreshnessInputs) = { i with Command = None; CommandVersion = None }
let private variantEnvironment (i: FreshnessInputs) = { i with Environment = Ci }
let private variantRuleHash (i: FreshnessInputs) = { i with RuleHash = RuleHash "r2" }
let private variantCoveredArtifacts (i: FreshnessInputs) = { i with CoveredArtifacts = [ ArtifactHash "h3" ] }
let private variantCommandVersion (i: FreshnessInputs) = { i with CommandVersion = Some(CommandVersion "9.0") }
let private variantGeneratorVersion (i: FreshnessInputs) = { i with GeneratorVersion = GeneratorVersion "g2" }
let private variantBase (i: FreshnessInputs) = { i with Base = Revision "ccc" }
let private variantHead (i: FreshnessInputs) = { i with Head = Revision "ddd" }

/// The 10 comparable categories, each paired with a single-field variation function.
let allCategories: (InputCategory * (FreshnessInputs -> FreshnessInputs)) list =
    [ CheckIdentity, variantCheck
      DomainIdentity, variantDomain
      CommandIdentity, variantCommand
      EnvironmentClassCat, variantEnvironment
      RuleHashCat, variantRuleHash
      CoveredArtifactsCat, variantCoveredArtifacts
      CommandVersionCat, variantCommandVersion
      GeneratorVersionCat, variantGeneratorVersion
      BaseRevisionCat, variantBase
      HeadRevisionCat, variantHead ]

/// The NON-identity categories — those that change WITHOUT touching the gate identity (Check/Domain), so a
/// single-field change of one of these against a recorded base entry keeps the entry same-gate ⇒ the
/// recompute cause is `InputsChanged [thatCategory]`.
let nonIdentityCategories: (InputCategory * (FreshnessInputs -> FreshnessInputs)) list =
    allCategories
    |> List.filter (fun (c, _) -> c <> CheckIdentity && c <> DomainIdentity)

// ── Real EvidenceRef + store builders (the production recording path) ──

let refA = EvidenceRef "ev-A"

let storeOf (entries: (FreshnessInputs * EvidenceRef) list) : ReuseStore =
    entries
    |> List.fold (fun store (inputs, evidence) -> EvidenceReuse.record inputs evidence store) EvidenceReuse.empty

/// A store that already records the base inputs against `refA` — so a candidate with `baseInputs` is a hit
/// and a candidate with any single-field variant is a miss naming that category.
let baseStore: ReuseStore = storeOf [ baseInputs, refA ]

/// The REAL F041 verdict for a candidate gate's inputs against a store (never mocked).
let verdictFor (inputs: FreshnessInputs) (store: ReuseStore) : CacheEligibilityVerdict =
    CacheEligibility.evaluateGate { Gate = gid "build" "tests"; Inputs = inputs } store

// ── CandidateCost builders ──

let cc (gate: GateId) (cost: Cost) (verdict: CacheEligibilityVerdict) : CandidateCost =
    { Gate = gate; Cost = cost; Verdict = verdict; Review = Deterministic }

let ccReviewed (gate: GateId) (cost: Cost) (verdict: CacheEligibilityVerdict) (key: CacheKey) : CandidateCost =
    { Gate = gate; Cost = cost; Verdict = verdict; Review = AgentReviewed key }

/// A literal must-recompute candidate naming a single changed freshness dimension.
let mustRecompute (gate: GateId) (cost: Cost) (cats: InputCategory list) : CandidateCost =
    cc gate cost (MustRecompute(InputsChanged cats))

let noEvidence (gate: GateId) (cost: Cost) : CandidateCost =
    cc gate cost (MustRecompute NoPriorEvidence)

let reusable (gate: GateId) (cost: Cost) : CandidateCost = cc gate cost (Reusable refA)

// ── Real F036 agent-review inputs / key (built from the real `AgentReviewKey.compute`, never mocked) ──

let reviewInputsBase: AgentReviewInputs =
    { Model = ModelId "claude"
      ModelVersion = ModelVersion "1"
      Config = ModelConfig "cfg"
      PromptHash = ReviewerPromptHash "p1"
      Question = QuestionText "is it safe?"
      Check = RuleHash "chk-1"
      ReviewedArtifacts = [ ArtifactHash "a1"; ArtifactHash "a2" ] }

/// The same inputs with exactly the judge model version changed — `matches` no longer holds.
let reviewInputsChanged: AgentReviewInputs =
    { reviewInputsBase with ModelVersion = ModelVersion "2" }

let reviewKey: CacheKey = AgentReviewKey.compute reviewInputsBase
let reviewKeyChanged: CacheKey = AgentReviewKey.compute reviewInputsChanged

// ── Taint lookups ──

let allReal: GateId -> EvidenceTaint = fun _ -> Real

let taintOnly (synthetic: GateId list) : GateId -> EvidenceTaint =
    fun g -> if List.contains g synthetic then Synthetic else Real

// ── The 4 profiles and 6 run modes ──

let profiles = [ Light; Standard; Strict; Profile.Release ]
let modes = [ Sandbox; Inner; Focused; Verify; Gate; RunMode.Release ]
let boundaryModes = [ Verify; Gate; RunMode.Release ]
let innerModes = [ Sandbox; Inner; Focused ]
let costs = [ Cheap; Medium; High; Exhaustive ]

// ── Repo root (for the surface baseline path) ──

let rec private findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root (FS.GG.Governance.sln) not found"
    | d ->
        if File.Exists(Path.Combine(d.FullName, "FS.GG.Governance.sln")) then
            d.FullName
        else
            findRepoRoot d.Parent

let repoRoot = findRepoRoot (DirectoryInfo(AppContext.BaseDirectory))
