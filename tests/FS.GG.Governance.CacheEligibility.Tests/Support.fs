module FS.GG.Governance.CacheEligibility.Tests.Support

open System
open System.IO
open Expecto
open FsCheck
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.CacheEligibility.Model

// Shared REAL-input builders + FsCheck generators for the F041 tests (Principle V — every value below is a
// real, literally-constructible typed `GateId` / `FreshnessInputs` / `EvidenceRef` / `ReuseStore` /
// `CandidateGate`, never a mock; the operations are pure so no upstream chain is needed). The base input +
// per-category variation table is the F029/F030 `Support.fs` shape reused so a single-field change is
// unambiguous. No I/O beyond repo-root resolution.

// ── Gate identity helper (F018 GateId, "<domain>:<checkId>") ──

/// Build a `GateId` from a domain + check id, the design's `"<domain>:<checkId>"` wire form.
let gid (domain: string) (check: string) : GateId = GateId(domain + ":" + check)

// ── A real base input set every test varies from (the F029/F030 worked example) ──

/// A complete, literal `FreshnessInputs` — every category present and distinct so a single-field change is
/// unambiguous. The worked-example values from contracts/cache-eligibility-api.md (gate ("build", "tests")).
let baseInputs: FreshnessInputs =
    { Check = CheckId "build:tests"
      Domain = DomainId "build"
      Command = Some(CommandId "dotnet")
      Environment = Local
      RuleHash = RuleHash "r1"
      CoveredArtifacts = [ ArtifactHash "h2"; ArtifactHash "h1"; ArtifactHash "h1" ]
      CommandVersion = Some(CommandVersion "8.0")
      GeneratorVersion = GeneratorVersion "g1"
      Base = Revision "aaa"
      Head = Revision "bbb" }

// ── One representative single-field variant per comparable category ──
// Each takes `baseInputs` and changes EXACTLY the named category to a distinct value (option categories flip
// present↔absent). Paired with its `InputCategory` for table-driven tests.

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

/// The 10 comparable categories, each paired with a single-field variation function. Table-driven
/// recompute-cause tests iterate this so EVERY category drives a `MustRecompute` when changed.
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
/// recompute cause is `InputsChanged [thatCategory]` (the no-hide explanation tests, SC-003).
let nonIdentityCategories: (InputCategory * (FreshnessInputs -> FreshnessInputs)) list =
    allCategories
    |> List.filter (fun (c, _) -> c <> CheckIdentity && c <> DomainIdentity)

// ── Real EvidenceRef + candidate + store builders (opaque, edge-supplied; empty string is a literal) ──

let refA = EvidenceRef "ev-A"
let refB = EvidenceRef "ev-B"

/// Build a `CandidateGate` from a gate identity and its already-resolved freshness inputs.
let candidate (gate: GateId) (inputs: FreshnessInputs) : CandidateGate = { Gate = gate; Inputs = inputs }

/// Build a `ReuseStore` by folding `EvidenceReuse.record` over `EvidenceReuse.empty` (the real recording
/// path, so the store is exactly what production would hold; de-dup/most-recent-wins is F030's, not ours).
/// Entries are supplied oldest-first and recorded in turn, matching how evidence accrues over time.
let storeOf (entries: (FreshnessInputs * EvidenceRef) list) : ReuseStore =
    entries
    |> List.fold (fun store (inputs, evidence) -> EvidenceReuse.record inputs evidence store) EvidenceReuse.empty

// ── FsCheck generators (real values, no mocks) ──

let private shortStringGen: Gen<string> =
    Gen.elements [ ""; "a"; "b"; "h1"; "h2"; "r1"; "build:tests"; "8.0"; "g1"; "aaa"; "héllo"; "x:y=z" ]

let private genEnvironment: Gen<EnvironmentClass> =
    Gen.elements [ Local; Ci; LocalOrCi; Release ]

let private genFreshnessInputs: Gen<FreshnessInputs> =
    gen {
        let! check = shortStringGen
        let! domain = shortStringGen
        let! hasCommand = Gen.elements [ true; false ]
        let! command = shortStringGen
        let! env = genEnvironment
        let! ruleHash = shortStringGen
        let! arts = Gen.listOf shortStringGen
        let! hasCmdVersion = Gen.elements [ true; false ]
        let! cmdVersion = shortStringGen
        let! genVersion = shortStringGen
        let! baseRev = shortStringGen
        let! headRev = shortStringGen

        return
            { Check = CheckId check
              Domain = DomainId domain
              Command = (if hasCommand then Some(CommandId command) else None)
              Environment = env
              RuleHash = RuleHash ruleHash
              CoveredArtifacts = arts |> List.map ArtifactHash
              CommandVersion = (if hasCmdVersion then Some(CommandVersion cmdVersion) else None)
              GeneratorVersion = GeneratorVersion genVersion
              Base = Revision baseRev
              Head = Revision headRev }
    }

let private genEvidenceRef: Gen<EvidenceRef> =
    Gen.elements [ ""; "ev-A"; "ev-B"; "ev-C"; "héllo" ] |> Gen.map EvidenceRef

/// A small label pool so generated `GateId`s collide often — exercising the duplicate-`GateId` paths
/// (attribution keeps duplicates, the structural tiebreak orders them). Includes empty + multi-byte +
/// ordinal-edge strings.
let private genGateId: Gen<GateId> =
    Gen.elements [ ""; "a:a"; "a:b"; "z:a"; "build:tests"; "Z:a"; "héllo:x" ] |> Gen.map GateId

let private genCandidate: Gen<CandidateGate> =
    gen {
        let! g = genGateId
        let! i = genFreshnessInputs
        return { Gate = g; Inputs = i }
    }

/// Arbitrary candidate lists, incl. `[]`, singletons, and lists with duplicate `GateId`s (from the small
/// label pool) — the cross-product the totality / order / attribution properties sweep.
let private genCandidateList: Gen<CandidateGate list> = Gen.listOf genCandidate

let private genReuseStore: Gen<ReuseStore> =
    gen {
        let! entries =
            Gen.listOf (
                gen {
                    let! i = genFreshnessInputs
                    let! e = genEvidenceRef
                    return { Inputs = i; Evidence = e }
                }
            )

        return ReuseStore entries
    }

type Generators =
    static member FreshnessInputs() : Arbitrary<FreshnessInputs> = Arb.fromGen genFreshnessInputs
    static member EvidenceRef() : Arbitrary<EvidenceRef> = Arb.fromGen genEvidenceRef
    static member GateId() : Arbitrary<GateId> = Arb.fromGen genGateId
    static member CandidateGate() : Arbitrary<CandidateGate> = Arb.fromGen genCandidate
    static member CandidateList() : Arbitrary<CandidateGate list> = Arb.fromGen genCandidateList
    static member ReuseStore() : Arbitrary<ReuseStore> = Arb.fromGen genReuseStore

/// FsCheck config registering the real generators.
let fscheckConfig =
    { FsCheckConfig.defaultConfig with arbitrary = [ typeof<Generators> ] }

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
