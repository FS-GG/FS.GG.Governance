module FS.GG.Governance.EvidenceReuseStore.Tests.Support

open System
open System.IO
open Expecto
open FsCheck
open FsCheck.FSharp
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.FreshnessSensing

// Shared REAL-input builders + FsCheck generators for the F047 tests (Principle V — every store below is a
// real, upstream-assembled `ReuseStore` folded from the genuine F030 `EvidenceReuse.record` over real F029
// `FreshnessInputs`, never a mock, never a hand-built JSON oracle). The round-trip drives the REAL
// `FreshnessSensing.realStoreReader` against a temp file — never a re-implemented parser (research D3). The
// evidence references are opaque, disclosed `Synthetic` literals — a real `EvidenceRef` needs gate execution
// (the deferred row, Assumptions). No I/O beyond repo-root resolution and the round-trip temp file.

// ── A real base input set every test varies from (the F029/F030 worked example) ──

/// A complete, literal `FreshnessInputs` for `check` — every category present and distinct so loss is
/// observable, with a multi-element verbatim `CoveredArtifacts` list (distinct order from any sort).
let inputs (check: string) : FreshnessInputs =
    { Check = CheckId check
      Domain = DomainId "build"
      Command = Some(CommandId "dotnet")
      Environment = Local
      RuleHash = RuleHash "r1"
      CoveredArtifacts = [ ArtifactHash "h2"; ArtifactHash "h1"; ArtifactHash "h1" ]
      CommandVersion = Some(CommandVersion "8.0")
      GeneratorVersion = GeneratorVersion "g1"
      Base = Revision "aaa"
      Head = Revision "bbb" }

// ── Real EvidenceRef + store builders (opaque, edge-supplied, disclosed Synthetic) ──

/// An opaque, DISCLOSED-SYNTHETIC evidence reference. SYNTHETIC: a real `EvidenceRef` is the output of gate
/// execution (the deferred row, Assumptions); these fixtures carry literal pointers so the store shape is real.
let syntheticRef (label: string) : EvidenceRef = EvidenceRef("synthetic://" + label) // SYNTHETIC: real refs need gate execution

/// Build a `ReuseStore` by folding the REAL `EvidenceReuse.record` over `EvidenceReuse.empty` (so the store is
/// exactly the value production would hold; de-dup / most-recent-wins is F030's, not ours). Oldest-first input
/// ⇒ newest-first store.
let storeOf (entries: (FreshnessInputs * EvidenceRef) list) : ReuseStore =
    entries
    |> List.fold (fun store (i, evidence) -> EvidenceReuse.record i evidence store) EvidenceReuse.empty

// ── The round-trip load-back leg drives the REAL reader (research D3) ──

/// Write the serialised text to a temp file and load it back through the REAL
/// `FreshnessSensing.realStoreReader` (the only public load path reads a path). `Ok (Some s) -> Some s`;
/// `Ok None -> None` (absent file). An `Error` surfaces by raising (a malformed document is a test failure).
let readBack (text: string) : ReuseStore option =
    let path = Path.GetTempFileName()

    try
        File.WriteAllText(path, text)

        match FreshnessSensing.realStoreReader path with
        | Ok loaded -> loaded
        | Error reason -> failwithf "realStoreReader rejected serialised output: %s" reason
    finally
        File.Delete path

/// Load a (non-existent) path through the REAL reader — the absent-file ⇒ `Ok None` edge.
let readPath (path: string) : Result<ReuseStore option, string> = FreshnessSensing.realStoreReader path

// ── FsCheck generators (real values, no mocks) ──

// A small pool including empty, multi-byte, and JSON-escaping-significant strings (quotes, backslash, control
// chars) — so the opaque-reference / escaping edge (FR-004) is exercised by generated stores.
let private shortStringGen: Gen<string> =
    Gen.elements
        [ ""
          "a"
          "h1"
          "h2"
          "r1"
          "g1"
          "8.0"
          "build:tests"
          "héllo"
          "x:y=z"
          "with\"quote"
          "back\\slash"
          "tab\tchar"
          "new\nline" ]

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
        // covered-artifact lists incl. [] and multi-element verbatim order (D5)
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
    // Includes JSON-escaping-significant + empty references — opaque, verbatim (FR-004).
    Gen.elements [ ""; "ev-A"; "ev-B"; "héllo"; "with\"quote"; "back\\slash"; "ctlx" ]
    |> Gen.map EvidenceRef

let private genRecordedEvidence: Gen<RecordedEvidence> =
    gen {
        let! i = genFreshnessInputs
        let! e = genEvidenceRef
        return { Inputs = i; Evidence = e }
    }

/// Arbitrary well-typed `ReuseStore` values — varying every category, covered-artifact lists (incl. [] and
/// multi-element verbatim order), `Some`/`None` optionals, and store length (empty / singleton / large). Built
/// as a raw `ReuseStore` (NOT via `record`) so deliberately-superseded duplicate-world entries can appear for
/// the pruning tests — the round-trip preserves the verbatim list regardless.
let private genReuseStore: Gen<ReuseStore> =
    Gen.listOf genRecordedEvidence |> Gen.map ReuseStore

/// A store built so it deliberately contains a strictly-superseded (duplicate-world) older entry: the same
/// freshness world recorded under two different references, newest-first, NOT via `record` (which would dedup).
let supersededStore: ReuseStore =
    let world = inputs "build:tests"

    ReuseStore
        [ { Inputs = world; Evidence = syntheticRef "newest" }
          { Inputs = { world with Check = CheckId "build:other" }; Evidence = syntheticRef "distinct" }
          { Inputs = world; Evidence = syntheticRef "superseded" } ]

/// Arbitrary candidate `FreshnessInputs` (for the reuse-decision safety property).
type Generators =
    static member FreshnessInputs() : Arbitrary<FreshnessInputs> = Arb.fromGen genFreshnessInputs
    static member EvidenceRef() : Arbitrary<EvidenceRef> = Arb.fromGen genEvidenceRef
    static member RecordedEvidence() : Arbitrary<RecordedEvidence> = Arb.fromGen genRecordedEvidence
    static member ReuseStore() : Arbitrary<ReuseStore> = Arb.fromGen genReuseStore

/// FsCheck config registering the real generators.
let fscheckConfig =
    { FsCheckConfig.defaultConfig with arbitrary = [ typeof<Generators> ] }
// 074: findRepoRoot consolidated into the shared RepositoryHelpers (sln||slnx superset).
let repoRoot = FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot
