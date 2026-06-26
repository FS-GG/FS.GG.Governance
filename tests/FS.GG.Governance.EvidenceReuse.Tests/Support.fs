module FS.GG.Governance.EvidenceReuse.Tests.Support

open System
open System.IO
open Expecto
open FsCheck
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse.Model

// Shared REAL-input builders + FsCheck generators for the F030 tests (Principle V — every value below is a
// real, literally-constructible typed `FreshnessInputs` / `EvidenceRef` / `ReuseStore`, never a mock; the
// operations are pure so no upstream chain is needed). The base input + per-category variation table is the
// F029 `Support.fs` shape reused so a single-field change is unambiguous. No I/O beyond repo-root
// resolution.

// ── A real base input set every test varies from (the F029 worked example) ──

/// A complete, literal `FreshnessInputs` — every category present and distinct so a single-field change is
/// unambiguous. The worked-example values from contracts/reuse-decision-semantics.md.
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
// Each takes `baseInputs` and changes EXACTLY the named category to a distinct value (option categories
// flip present↔absent). Paired with its `InputCategory` for table-driven tests.

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
/// reuse-distinction tests iterate this so EVERY category drives a Recompute when changed (SC-001).
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
/// single-field change of one of these against a base entry keeps the entry same-gate ⇒ the recompute cause
/// is `InputsChanged [thatCategory]` (research D5). Drives the explanation tests (SC-003).
let nonIdentityCategories: (InputCategory * (FreshnessInputs -> FreshnessInputs)) list =
    allCategories
    |> List.filter (fun (c, _) -> c <> CheckIdentity && c <> DomainIdentity)

// ── Real EvidenceRef builders (opaque, edge-supplied; an empty string is a literal value) ──

let E1 = EvidenceRef "ev-1"
let E2 = EvidenceRef "ev-2"
let E3 = EvidenceRef "ev-3"
let E4 = EvidenceRef "ev-4"
let emptyRef = EvidenceRef ""

/// Build a `ReuseStore` directly from a literal entry list (the DU constructor is public — hand-built
/// stores need not go through `record`, which lets tests exercise duplicate/disordered stores too).
let storeOf (entries: (FreshnessInputs * EvidenceRef) list) : ReuseStore =
    ReuseStore(entries |> List.map (fun (i, e) -> { Inputs = i; Evidence = e }))

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
    Gen.elements [ ""; "ev-1"; "ev-2"; "ev-3"; "héllo" ] |> Gen.map EvidenceRef

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

/// A permutation+duplication of an `ArtifactHash list` that preserves its SET (for order/dup invariance
/// properties). Returns a list with the same distinct elements in a possibly-different order, possibly with
/// repeats.
let private genResetSamePermutation (arts: ArtifactHash list) : Gen<ArtifactHash list> =
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
    static member FreshnessInputs() : Arbitrary<FreshnessInputs> = Arb.fromGen genFreshnessInputs
    static member EvidenceRef() : Arbitrary<EvidenceRef> = Arb.fromGen genEvidenceRef
    static member ReuseStore() : Arbitrary<ReuseStore> = Arb.fromGen genReuseStore

/// FsCheck config registering the real `FreshnessInputs` / `EvidenceRef` / `ReuseStore` generators.
let fscheckConfig =
    { FsCheckConfig.defaultConfig with arbitrary = [ typeof<Generators> ] }

/// Build a same-set permutation generator for a given input's covered artifacts (used by determinism
/// order/dup invariance properties).
let samePermutationOf = genResetSamePermutation
// 074: findRepoRoot consolidated into the shared RepositoryHelpers (sln||slnx superset).
let repoRoot = FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot
