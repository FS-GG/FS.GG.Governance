module FS.GG.Governance.FreshnessKey.Tests.Support

open System
open System.IO
open Expecto
open FsCheck
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model

// Shared REAL-input builders + FsCheck generators for the F029 tests (Principle V — every value below is a
// real, literally-constructible typed `FreshnessInputs`, never a mock; the operations are pure so no
// upstream chain is needed). The covered-artifact set, option-presence, and per-category variation helpers
// are the table the distinction/injectivity/inspection tests drive. No I/O beyond repo-root resolution.

// ── A real base input set every test varies from ──

/// A complete, literal `FreshnessInputs` — every category present and distinct so a single-field change is
/// unambiguous. The worked-example values from contracts/freshness-key-format.md.
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
/// distinction/inspection tests iterate this so EVERY category is covered (SC-003).
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

/// All 10 `InputCategory` cases (for total/injective `categoryToken` coverage).
let allCategoryCases: InputCategory list = allCategories |> List.map fst

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
            // append one duplicate of the first distinct element so duplication is exercised too.
            let! extra = Gen.elements distinct
            return (List.ofArray shuffled) @ [ extra ]
        }

type Generators =
    static member FreshnessInputs() : Arbitrary<FreshnessInputs> = Arb.fromGen genFreshnessInputs

/// FsCheck config registering the real `FreshnessInputs` generator.
let fscheckConfig =
    { FsCheckConfig.defaultConfig with arbitrary = [ typeof<Generators> ] }

/// Build a same-set permutation generator for a given input's covered artifacts (used by determinism
/// order/dup invariance properties).
let samePermutationOf = genResetSamePermutation

// ── Repo root (for the surface baseline path) ──

/// Locate the repo root (the dir holding the solution) by walking up from the test binary.
let rec private findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root (FS.GG.Governance.sln) not found"
    | d ->
        if File.Exists(Path.Combine(d.FullName, "FS.GG.Governance.sln")) then d.FullName
        else findRepoRoot d.Parent

let repoRoot = findRepoRoot (DirectoryInfo(AppContext.BaseDirectory))
