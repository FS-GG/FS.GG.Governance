module FS.GG.Governance.FreshnessResolution.Tests.Support

open System
open System.IO
open Expecto
open FsCheck
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.CacheEligibility
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.FreshnessResolution.Model

// Shared REAL-input builders + FsCheck generators for the F043 tests (Principle V — every value below is a
// real, literally-constructible typed `Gate` (its carried five-field `FreshnessKey`) + real F029 newtypes
// bundled into a real `SensedFacts`; the F041 bridge is proven by feeding `candidate` results into the GENUINE
// `CacheEligibility.evaluate` over a real F030 `ReuseStore` built via `EvidenceReuse.record` — never a mock, no
// clock read, no git, no hash/freshness-key/digest computed). The base gate + per-fact `without*` mutators are
// the F041 `Support.fs` shape reused so a single dropped fact is unambiguous. No I/O beyond repo-root resolution.

// ── Gate identity helper (F018 GateId, "<domain>:<checkId>") ──

/// Build a `GateId` from a domain + check id, the design's `"<domain>:<checkId>"` wire form.
let gid (domain: string) (check: string) : GateId = GateId(domain + ":" + check)

/// Build a complete, real F018 `Gate` from a domain/check id and its carried freshness-key identity (the four
/// identity fields + `Cost`, which the join DROPS). The non-identity gate metadata (`Description`, `Timeout`,
/// `Owner`, `Maturity`, `ProductCheck`) is real but never read by `resolve` — only `Id` and `FreshnessKey`
/// matter to the join. `Cost` is set explicitly so its drop from the resolved `FreshnessInputs` is observable.
let gateWith
    (domain: string)
    (check: string)
    (cost: Cost)
    (env: EnvironmentClass)
    (command: CommandId option)
    : Gate =
    let fk: FreshnessKey =
        { Check = CheckId check
          Domain = DomainId domain
          Cost = cost
          Environment = env
          Command = command }

    { Id = gid domain check
      Domain = DomainId domain
      Description = sprintf "gate %s:%s" domain check
      Prerequisites =
        (match command with
         | Some c -> [ RequiresCommand c ]
         | None -> [])
      Cost = cost
      Timeout = TimeoutLimit 60
      Owner = Owner "team"
      Maturity = Observe
      ProductCheck = false
      FreshnessKey = fk }

// ── Canonical worked-example gates (contracts/freshness-resolution-outcome.md A–E) + commands ──

let dotnetCmd = CommandId "dotnet"
let eslintCmd = CommandId "eslint"

/// Worked example A: a fully-sensed, command-bearing gate. `Cost = Medium` so the drop is observable.
let gBuildTests = gateWith "build" "tests" Medium Ci (Some dotnetCmd)

/// Worked example C: a command-bearing gate whose facts we selectively drop to drive `Unresolved`.
let gLintStyle = gateWith "lint" "style" Cheap Local (Some eslintCmd)

/// Worked example B: a command-LESS gate (consistent command absence — never `MissingCommandVersion`).
let gDocsCheck = gateWith "docs" "check" High Local None

let artA = ArtifactHash "artA"
let artB = ArtifactHash "artB"
let artC = ArtifactHash "artC"

/// A complete, literal `SensedFacts` carrying EVERY repo-wide fact, the covered-artifacts key for each canonical
/// gate (distinct, non-empty for the command-bearing gates; deliberately empty for `gDocsCheck` to exercise the
/// sensed-empty value), and the command version for each declared command — so a SINGLE dropped fact is
/// observable against this baseline.
let fullSensed: SensedFacts =
    { RuleHash = Some(RuleHash "rule-1")
      GeneratorVersion = Some(GeneratorVersion "gen-1")
      Base = Some(Revision "base-1")
      Head = Some(Revision "head-1")
      CoveredArtifacts =
        Map.ofList
            [ gBuildTests.Id, [ artA; artB ]
              gLintStyle.Id, [ artC ]
              gDocsCheck.Id, [] ]
      CommandVersions = Map.ofList [ dotnetCmd, CommandVersion "8.0"; eslintCmd, CommandVersion "9.3" ] }

/// A `SensedFacts` bundle that FULLY senses exactly the given gate: every repo-wide fact present, the gate's
/// covered-artifacts key present (non-empty), and — only when the gate declares a command — that command's
/// version present. Used by the carry / command-absence / bridge / determinism tests over arbitrary gates.
let senseFully (g: Gate) : SensedFacts =
    { RuleHash = Some(RuleHash "rule-1")
      GeneratorVersion = Some(GeneratorVersion "gen-1")
      Base = Some(Revision "base-1")
      Head = Some(Revision "head-1")
      CoveredArtifacts = Map.ofList [ g.Id, [ artA; artB ] ]
      CommandVersions =
        (match g.FreshnessKey.Command with
         | Some c -> Map.ofList [ c, CommandVersion "8.0" ]
         | None -> Map.empty) }

/// An INDEPENDENT oracle for the resolved `FreshnessInputs` of a fully-sensed gate (the carry law): the four
/// identity fields from the gate's carried `FreshnessKey` (NO `Cost`), the six sensed fields from the bundle.
/// `Option.get` is safe only when the bundle fully senses the gate (the carry tests guarantee this).
let expectedResolved (g: Gate) (s: SensedFacts) : FreshnessInputs =
    { Check = g.FreshnessKey.Check
      Domain = g.FreshnessKey.Domain
      Command = g.FreshnessKey.Command
      Environment = g.FreshnessKey.Environment
      RuleHash = Option.get s.RuleHash
      CoveredArtifacts = Map.find g.Id s.CoveredArtifacts
      CommandVersion = g.FreshnessKey.Command |> Option.bind (fun c -> Map.tryFind c s.CommandVersions)
      GeneratorVersion = Option.get s.GeneratorVersion
      Base = Option.get s.Base
      Head = Option.get s.Head }

// ── Single-fact mutators (drop EXACTLY one sensed fact from a bundle) ──

let withoutRuleHash (s: SensedFacts) = { s with RuleHash = None }
let withoutGeneratorVersion (s: SensedFacts) = { s with GeneratorVersion = None }
let withoutBase (s: SensedFacts) = { s with Base = None }
let withoutHead (s: SensedFacts) = { s with Head = None }

let withoutCovered (g: GateId) (s: SensedFacts) =
    { s with CoveredArtifacts = Map.remove g s.CoveredArtifacts }

let withoutCommandVersion (c: CommandId) (s: SensedFacts) =
    { s with CommandVersions = Map.remove c s.CommandVersions }

/// The six gaps for a given command-bearing gate, each paired with its `MissingFact` — the table the
/// no-fabricate / no-hide tests iterate so EVERY required fact drives an `Unresolved` naming exactly it.
/// `MissingCommandVersion`'s mutator drops the gate's declared command version (only meaningful for a
/// command-bearing gate; `id` for a command-less gate, where the case is unreachable, FR-005).
let gapTable (g: Gate) : (MissingFact * (SensedFacts -> SensedFacts)) list =
    [ MissingRuleHash, withoutRuleHash
      MissingCoveredArtifacts, withoutCovered g.Id
      MissingCommandVersion,
      (match g.FreshnessKey.Command with
       | Some c -> withoutCommandVersion c
       | None -> id)
      MissingGeneratorVersion, withoutGeneratorVersion
      MissingBaseRevision, withoutBase
      MissingHeadRevision, withoutHead ]

/// An INDEPENDENT oracle for the missing facts of a gate against a sensed bundle, in FR-002 enum order — used by
/// the no-hide property as a separate spec of what `resolve` must name (it re-derives the rule, it does not call
/// the library). Mirrors the data-model.md join table.
let expectedMissing (g: Gate) (s: SensedFacts) : MissingFact list =
    [ if Option.isNone s.RuleHash then
          MissingRuleHash
      if not (Map.containsKey g.Id s.CoveredArtifacts) then
          MissingCoveredArtifacts
      match g.FreshnessKey.Command with
      | Some c when not (Map.containsKey c s.CommandVersions) -> MissingCommandVersion
      | _ -> ()
      if Option.isNone s.GeneratorVersion then
          MissingGeneratorVersion
      if Option.isNone s.Base then
          MissingBaseRevision
      if Option.isNone s.Head then
          MissingHeadRevision ]

// ── Real F030 store + F041 bridge helpers (no mocks) ──

/// Build a `ReuseStore` by folding `EvidenceReuse.record` over `EvidenceReuse.empty` (the real recording path).
let storeOf (entries: (FreshnessInputs * EvidenceRef) list) : ReuseStore =
    entries
    |> List.fold (fun store (inputs, evidence) -> EvidenceReuse.record inputs evidence store) EvidenceReuse.empty

/// Feed candidates into the GENUINE F041 roll-up — the bridge under test (a resolved candidate must be accepted
/// without adaptation).
let cacheReport (cands: CandidateGate list) (store: ReuseStore) : CacheEligibilityReport =
    CacheEligibility.evaluate cands store

// ── FsCheck generators (real values, no mocks) ──

// A small label pool so generated `GateId`s collide often — exercising the duplicate-`GateId` paths (attribution
// keeps duplicates, the structural tiebreak orders them). Includes a `:`-containing label and ordinal-edge case.
let private labelPool = [ "a"; "b"; "z"; "build"; "lint"; "Z" ]

let private genCommand: Gen<CommandId option> =
    Gen.elements [ None; Some dotnetCmd; Some eslintCmd; Some(CommandId "x:y") ]

let private genCost: Gen<Cost> =
    Gen.elements [ Cheap; Medium; High; Exhaustive ]

let private genEnv: Gen<EnvironmentClass> =
    Gen.elements [ Local; Ci; LocalOrCi; Release ]

let private genGate: Gen<Gate> =
    gen {
        let! d = Gen.elements labelPool
        let! c = Gen.elements labelPool
        let! cost = genCost
        let! env = genEnv
        let! cmd = genCommand
        return gateWith d c cost env cmd
    }

/// Arbitrary gate lists, incl. `[]`, singletons, and lists with duplicate `GateId`s (from the small label pool)
/// — the cross-product the totality / order / attribution properties sweep.
let private genGateList: Gen<Gate list> = Gen.listOf genGate

let private genOpt (g: Gen<'a>) : Gen<'a option> =
    Gen.oneof [ Gen.map Some g; Gen.constant None ]

let private genCoveredArtifacts: Gen<Map<GateId, ArtifactHash list>> =
    gen {
        let! pairs =
            Gen.listOf (
                gen {
                    let! d = Gen.elements labelPool
                    let! c = Gen.elements labelPool
                    // present-empty AND present-nonempty values both occur; an absent key is "not sensed".
                    let! arts = Gen.listOf (Gen.elements [ "h1"; "h2"; "h3" ] |> Gen.map ArtifactHash)
                    return (gid d c, arts)
                }
            )

        return Map.ofList pairs
    }

let private genCommandVersions: Gen<Map<CommandId, CommandVersion>> =
    gen {
        let! pairs =
            Gen.listOf (
                gen {
                    let! c = Gen.elements [ "dotnet"; "eslint"; "x:y" ]
                    let! v = Gen.elements [ "1.0"; "2.0"; "8.0" ]
                    return (CommandId c, CommandVersion v)
                }
            )

        return Map.ofList pairs
    }

let private genSensedFacts: Gen<SensedFacts> =
    gen {
        let! rh = genOpt (Gen.elements [ "r1"; "r2" ] |> Gen.map RuleHash)
        let! gv = genOpt (Gen.elements [ "g1"; "g2" ] |> Gen.map GeneratorVersion)
        let! b = genOpt (Gen.elements [ "b1"; "b2" ] |> Gen.map Revision)
        let! h = genOpt (Gen.elements [ "h1"; "h2" ] |> Gen.map Revision)
        let! cov = genCoveredArtifacts
        let! cv = genCommandVersions

        return
            { RuleHash = rh
              GeneratorVersion = gv
              Base = b
              Head = h
              CoveredArtifacts = cov
              CommandVersions = cv }
    }

type Generators =
    static member Gate() : Arbitrary<Gate> = Arb.fromGen genGate
    static member GateList() : Arbitrary<Gate list> = Arb.fromGen genGateList
    static member SensedFacts() : Arbitrary<SensedFacts> = Arb.fromGen genSensedFacts

/// FsCheck config registering the real generators.
let fscheckConfig =
    { FsCheckConfig.defaultConfig with
        arbitrary = [ typeof<Generators> ] }

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
