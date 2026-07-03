module FS.GG.Governance.CacheEligibilityJson.Tests.Support

open System
open System.IO
open System.Text.Json
open Expecto
open FsCheck
open FsCheck.FSharp
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.CacheEligibility

// Shared REAL-input builders + FsCheck generators for the F042 tests (Principle V — every report below is a
// real, upstream-assembled `CacheEligibilityReport` from the genuine F041 `CacheEligibility.evaluate` over real
// `GateId` / `FreshnessInputs` / `ReuseStore` (via `EvidenceReuse.record`), never a mock, never a hand-built
// JSON oracle). The base input + per-category variation table is the F029/F030/F041 `Support.fs` shape reused
// so a single-field change is unambiguous. The emitted document is inspected by a read-only
// `System.Text.Json.JsonDocument` parse — the kernel `Json` / F020-F025 projection-test pattern. No I/O beyond
// repo-root resolution.

// ── Gate identity helper (F018 GateId, "<domain>:<checkId>") ──

/// Build a `GateId` from a domain + check id, the design's `"<domain>:<checkId>"` wire form.
let gid (domain: string) (check: string) : GateId = GateId(domain + ":" + check)

// ── A real base input set every test varies from (the F029/F030/F041 worked example) ──

/// A complete, literal `FreshnessInputs` — every category present and distinct so a single-field change is
/// unambiguous (gate ("build", "tests")).
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

// ── One representative single-field variant per comparable category (the F041 table) ──
// Each takes `baseInputs` and changes EXACTLY the named category to a distinct value (option categories flip
// present↔absent). Paired with its `InputCategory` for table-driven no-hide tests.

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

/// The 10 comparable categories, each paired with a single-field variation function. The non-identity ones
/// (everything but Check/Domain) keep a candidate same-gate against a recorded base entry, so a change drives
/// `InputsChanged [thatCategory]` — the no-hide cause carry under test.
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

/// The non-identity categories — those that change WITHOUT touching the gate identity (Check/Domain). A
/// single-field change of one of these against a recorded base entry keeps the entry same-gate ⇒ the recompute
/// cause is `InputsChanged [thatCategory]`.
let nonIdentityCategories: (InputCategory * (FreshnessInputs -> FreshnessInputs)) list =
    allCategories
    |> List.filter (fun (c, _) -> c <> CheckIdentity && c <> DomainIdentity)

// ── Real EvidenceRef + candidate + store + report builders (opaque, edge-supplied) ──

let refA = EvidenceRef "ev-A"
let refB = EvidenceRef "ev-B"

/// Build a `CandidateGate` from a gate identity and its already-resolved freshness inputs.
let candidate (gate: GateId) (inputs: FreshnessInputs) : CandidateGate = { Gate = gate; Inputs = inputs }

/// Build a `ReuseStore` by folding the REAL `EvidenceReuse.record` over `EvidenceReuse.empty` (so the store is
/// exactly what production would hold; de-dup / most-recent-wins is F030's, not ours). Oldest-first.
let storeOf (entries: (FreshnessInputs * EvidenceRef) list) : ReuseStore =
    entries
    |> List.fold (fun store (inputs, evidence) -> EvidenceReuse.record inputs evidence store) EvidenceReuse.empty

/// The genuine F041 roll-up — every report under test flows through the real `evaluate`.
let report (candidates: CandidateGate list) (store: ReuseStore) : CacheEligibilityReport =
    CacheEligibility.evaluate candidates store

// ── Named worked-example reports (the document/api contract examples, from real `evaluate`) ──

/// Empty report — the totality success edge (FR-009).
let emptyReport: CacheEligibilityReport = report [] EvidenceReuse.empty

/// A store recording `baseInputs` under `ev-A` — the exact-match reusable fixture.
let exactStore: ReuseStore = storeOf [ baseInputs, refA ]

/// Single exact-match candidate (docs:lint) against `exactStore` ⇒ `Reusable ev-A`.
let reusableReport: CacheEligibilityReport = report [ candidate (gid "docs" "lint") baseInputs ] exactStore

/// Single no-prior candidate (security:scan) against the empty store ⇒ `MustRecompute NoPriorEvidence`.
let noPriorReport: CacheEligibilityReport =
    report [ candidate (gid "security" "scan") baseInputs ] EvidenceReuse.empty

/// Single candidate (build:tests) whose RuleHash + Head moved against `exactStore` ⇒
/// `MustRecompute (InputsChanged [ruleHash; headRevision])` — the multi-category no-hide fixture.
let inputsChangedReport: CacheEligibilityReport =
    let moved = baseInputs |> variantRuleHash |> variantHead
    report [ candidate (gid "build" "tests") moved ] exactStore

/// Candidates supplied z:a, a:b, a:a (any order) ⇒ report ordered a:a, a:b, z:a — the order-independence fixture.
let orderingReport (order: (string * string) list) : CacheEligibilityReport =
    report (order |> List.map (fun (d, c) -> candidate (gid d c) baseInputs)) EvidenceReuse.empty

/// Two candidates sharing a GateId (build:tests, different inputs) against the empty store ⇒ TWO
/// `MustRecompute NoPriorEvidence` entries — the duplicate-GateId fixture.
let duplicateReport: CacheEligibilityReport =
    report
        [ candidate (gid "build" "tests") baseInputs
          candidate (gid "build" "tests") (variantRuleHash baseInputs) ]
        EvidenceReuse.empty

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

/// A small label pool so generated `GateId`s collide often — exercising the duplicate-`GateId` paths. Includes
/// empty + multi-byte + ordinal-edge + `:`-containing strings.
let private genGateId: Gen<GateId> =
    Gen.elements [ ""; "a:a"; "a:b"; "z:a"; "build:tests"; "Z:a"; "héllo:x" ] |> Gen.map GateId

let private genCandidate: Gen<CandidateGate> =
    gen {
        let! g = genGateId
        let! i = genFreshnessInputs
        return { Gate = g; Inputs = i }
    }

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

/// Arbitrary well-typed reports spanning empty / all-reusable / all-must-recompute / mixed / duplicate-`GateId`
/// — assembled by driving the REAL `evaluate` over a generated candidate list and store (Principle V).
let private genReport: Gen<CacheEligibilityReport> =
    gen {
        let! candidates = genCandidateList
        let! store = genReuseStore
        return CacheEligibility.evaluate candidates store
    }

type Generators =
    static member FreshnessInputs() : Arbitrary<FreshnessInputs> = Arb.fromGen genFreshnessInputs
    static member EvidenceRef() : Arbitrary<EvidenceRef> = Arb.fromGen genEvidenceRef
    static member GateId() : Arbitrary<GateId> = Arb.fromGen genGateId
    static member CandidateGate() : Arbitrary<CandidateGate> = Arb.fromGen genCandidate
    static member CandidateList() : Arbitrary<CandidateGate list> = Arb.fromGen genCandidateList
    static member ReuseStore() : Arbitrary<ReuseStore> = Arb.fromGen genReuseStore
    static member CacheEligibilityReport() : Arbitrary<CacheEligibilityReport> = Arb.fromGen genReport

/// FsCheck config registering the real generators.
let fscheckConfig =
    { FsCheckConfig.defaultConfig with arbitrary = [ typeof<Generators> ] }

// ── JsonDocument read helpers (read-only inspection of the emitted bytes) ──

/// Parse the emitted document text into a JsonDocument (the caller disposes via `use`).
let parse (json: string) : JsonDocument = JsonDocument.Parse json

let private reqStr (el: JsonElement) : string =
    match el.GetString() with
    | null -> failwith "expected a JSON string but found null"
    | s -> s

/// Fail-fast read of a named string property on an object element.
let strField (el: JsonElement) (name: string) : string = reqStr (el.GetProperty name)

/// The field names of an object element in their emitted order.
let fieldOrder (el: JsonElement) : string list =
    [ for p in el.EnumerateObject() -> p.Name ]

/// The top-level field names in their emitted order.
let topLevelFieldOrder (doc: JsonDocument) : string list = fieldOrder doc.RootElement

/// The document's `schemaVersion` field.
let docSchemaVersion (doc: JsonDocument) : string = strField doc.RootElement "schemaVersion"

/// The entry objects of the `entries` array, in emitted order.
let entriesOf (doc: JsonDocument) : JsonElement list =
    [ for it in doc.RootElement.GetProperty("entries").EnumerateArray() -> it ]

/// Whether an object element has a property of the given name.
let hasField (el: JsonElement) (name: string) : bool =
    match el.TryGetProperty name with
    | true, _ -> true
    | false, _ -> false

/// An entry's declared `gate`.
let entryGate (entry: JsonElement) : string = strField entry "gate"

/// An entry's `verdict` object.
let entryVerdict (entry: JsonElement) : JsonElement = entry.GetProperty "verdict"

/// A verdict's `kind` discriminator.
let verdictKind (verdict: JsonElement) : string = strField verdict "kind"

/// A reusable verdict's opaque `evidence` reference.
let verdictEvidence (verdict: JsonElement) : string = strField verdict "evidence"

/// A mustRecompute verdict's `cause` object.
let verdictCause (verdict: JsonElement) : JsonElement = verdict.GetProperty "cause"

/// A cause's `kind` discriminator.
let causeKind (cause: JsonElement) : string = strField cause "kind"

/// An inputsChanged cause's `categories` array, in emitted order.
let causeCategories (cause: JsonElement) : string list =
    [ for c in cause.GetProperty("categories").EnumerateArray() -> reqStr c ]

/// Every string value anywhere in the document (recursively) — for the positive-allowlist sweep.
let rec allStringValues (el: JsonElement) : string list =
    match el.ValueKind with
    | JsonValueKind.String -> [ reqStr el ]
    | JsonValueKind.Object -> [ for p in el.EnumerateObject() do yield! allStringValues p.Value ]
    | JsonValueKind.Array -> [ for v in el.EnumerateArray() do yield! allStringValues v ]
    | _ -> []

/// Every property name anywhere in the document (recursively) — for the closed-key-set sweep.
let rec allPropertyNames (el: JsonElement) : string list =
    match el.ValueKind with
    | JsonValueKind.Object ->
        [ for p in el.EnumerateObject() do
              yield p.Name
              yield! allPropertyNames p.Value ]
    | JsonValueKind.Array -> [ for v in el.EnumerateArray() do yield! allPropertyNames v ]
    | _ -> []

/// The whole emitted document text, lowercased — for the deny-token exclusion sweep.
let lower (s: string) : string = s.ToLowerInvariant()
// 074: findRepoRoot consolidated into the shared RepositoryHelpers (sln||slnx superset).
let repoRoot = FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot
