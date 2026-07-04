module FS.GG.Governance.VerifyJson.Tests.Support

open System
open System.IO
open System.Text.Json
open Expecto
open FsCheck
open FsCheck.FSharp
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Route.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.Ship.Ship
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.CacheEligibility
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.GateRun.Model

// Shared REAL-input builders + FsCheck generators for the F056 VerifyJson tests (Principle V — every value
// below is a real, literally-constructible typed value, never a mock). Each fixture is assembled into a real
// F019 `RouteResult` and rolled up by the GENUINE F024 `Ship.rollup` at `RunMode.Verify`, so every
// `ShipDecision` under test is the value the `fsgg verify` host holds. The cache report is a real
// `CacheEligibility.evaluate` over real `FreshnessInputs` + a real `ReuseStore`. The execution outcomes are
// real `GateOutcome` records. JSON read helpers inspect the emitted bytes via a read-only `JsonDocument`.
// (Mirrors the F025 `AuditJson.Tests.Support` real-chain style verbatim.)

// ── Real F018 gate / F017 finding / F019 route builders ──

let mkGate (id: GateId) (maturity: Maturity) : Gate =
    let (GateId raw) = id
    let domain = DomainId "build"
    let cost = Cheap

    { Id = id
      Domain = domain
      Description = sprintf "gate %s" raw
      Prerequisites = []
      Cost = cost
      Timeout = TimeoutLimit 60
      Owner = Owner "team"
      Maturity = maturity
      ProductCheck = false
      FreshnessKey =
        { Check = CheckId raw
          Domain = domain
          Cost = cost
          Environment = Local
          Command = None } }

let mkSelectedGate (gate: Gate) : SelectedGate =
    { Gate = gate
      SelectingPaths =
        [ { Path = GovernedPath "src/a.fs"
            MatchedGlob = GovernedPath "src/**" } ] }

let mkFinding (id: FindingId) (path: GovernedPath) (zone: FindingZone) : UnknownGovernedPathFinding =
    let (GovernedPath p) = path

    { Id = id
      Path = path
      Zone = zone
      Message = sprintf "unclassified path %s" p }

let mkRoute (gates: SelectedGate list) (findings: UnknownGovernedPathFinding list) : RouteResult =
    { SelectedGates = gates
      Findings = { Findings = findings }
      Cost = { Cheap = 0; Medium = 0; High = 0; Exhaustive = 0 } }

let emptyRoute: RouteResult = mkRoute [] []

let allModes: RunMode list =
    [ Sandbox; Inner; Focused; Verify; Gate; RunMode.Release ]

let allProfiles: Profile list =
    [ Light; Standard; Strict; Profile.Release ]

let allMaturities: Maturity list =
    [ Observe; Warn; BlockOnPr; BlockOnShip; BlockOnRelease ]

let allZones: FindingZone list =
    [ GovernedRootUnknown; ProtectedBoundaryUnknown(SurfaceId "s") ]

let findingIdForZone (zone: FindingZone) : FindingId =
    match zone with
    | GovernedRootUnknown -> UnknownGovernedPath
    | ProtectedBoundaryUnknown _ -> UnknownProtectedBoundaryPath

// ── The real rollup convenience: every fixture is a genuine F024 decision rolled at Verify ──

let decisionOf (route: RouteResult) (mode: RunMode) (profile: Profile) : ShipDecision = rollup route mode profile

/// The empty/clean decision rolled at `Verify` — the "nothing to verify" projection input.
let emptyCleanDecision: ShipDecision = decisionOf emptyRoute Verify Standard

/// A rich decision exercising all three sections AND the no-hide warning case, rolled at `Verify`/`Strict`
/// (at `Verify` the BlockOnShip floor 4 tightens to 3 under Strict ⇒ it blocks; BlockOnRelease floor 5 → 4
/// stays above Verify ⇒ relaxed to a warning — the no-hide case):
///   • `build:ship`  BlockOnShip      → blocker  (base Blocking, effective Blocking at Verify/Strict)
///   • `build:rel`   BlockOnRelease   → warning  (base Blocking RELAXED to effective Advisory)
///   • `docs:lint`   Observe          → passing  (base Advisory)
///   • a ProtectedBoundaryUnknown finding → blocker
///   • a GovernedRootUnknown finding      → passing
let richDecision: ShipDecision =
    decisionOf
        (mkRoute
            [ mkSelectedGate (mkGate (GateId "build:ship") BlockOnShip)
              mkSelectedGate (mkGate (GateId "build:rel") BlockOnRelease)
              mkSelectedGate (mkGate (GateId "docs:lint") Observe) ]
            [ mkFinding UnknownProtectedBoundaryPath (GovernedPath "src/boundary/Api.fs") (ProtectedBoundaryUnknown(SurfaceId "api"))
              mkFinding UnknownGovernedPath (GovernedPath "src/new/Thing.fs") GovernedRootUnknown ])
        Verify
        Strict

// ── Real cache-eligibility report builders (real FreshnessInputs + real ReuseStore + real evaluate) ──

let baseInputs: FreshnessInputs =
    { Check = CheckId "ship"
      Domain = DomainId "build"
      Command = Some(CommandId "dotnet")
      Environment = Local
      RuleHash = RuleHash "r1"
      CoveredArtifacts = [ ArtifactHash "h1" ]
      CommandVersion = Some(CommandVersion "8.0")
      GeneratorVersion = GeneratorVersion "g1"
      Base = Revision "aaa"
      Head = Revision "bbb" }

let shipInputs = baseInputs
let relInputs = { baseInputs with Check = CheckId "rel"; Domain = DomainId "build" }
let lintInputs = { baseInputs with Check = CheckId "lint"; Domain = DomainId "docs" }

let candidate (gate: string) (inputs: FreshnessInputs) : CandidateGate = { Gate = GateId gate; Inputs = inputs }

let storeOf (entries: (FreshnessInputs * EvidenceRef) list) : ReuseStore =
    entries |> List.fold (fun s (i, e) -> EvidenceReuse.record i e s) EvidenceReuse.empty

let recordedStore = storeOf [ shipInputs, EvidenceRef "ev-A"; relInputs, EvidenceRef "ev-R"; lintInputs, EvidenceRef "ev-L" ]

let reportOf (cands: CandidateGate list) (store: ReuseStore) : CacheEligibilityReport = CacheEligibility.evaluate cands store

/// build:ship exact ⇒ Reusable ev-A; build:rel RuleHash moved ⇒ MustRecompute (InputsChanged [ruleHash]);
/// docs:lint ABSENT (unresolved/notEvaluated).
let mixedReport =
    reportOf
        [ candidate "build:ship" shipInputs
          candidate "build:rel" { relInputs with RuleHash = RuleHash "r2" } ]
        recordedStore

/// A report where build:ship has no prior evidence ⇒ MustRecompute NoPriorEvidence.
let noPriorReport = reportOf [ candidate "build:ship" shipInputs ] EvidenceReuse.empty

// ── Real execution-outcome builders ──

let outcome (gate: string) (disposition: GateDisposition) : GateId * GateOutcome =
    GateId gate, { GateId = GateId gate; Disposition = disposition }

/// Execution outcomes for richDecision's gates: build:ship reused-pass, build:rel executed-fail,
/// docs:lint not-executed.
let mixedOutcomes: (GateId * GateOutcome) list =
    [ outcome "build:ship" (Reused(ExitCode 0, true))
      outcome "build:rel" (Executed(ExitCode 1, false))
      outcome "docs:lint" NotExecuted ]

// ── FsCheck generators over the finite enumerations (real `RouteResult`s, real `rollup` at Verify) ──

let private elements xs = Gen.elements xs

let private genSelectedGate (i: int) : Gen<SelectedGate> =
    gen {
        let! maturity = elements allMaturities
        return mkSelectedGate (mkGate (GateId(sprintf "build:check%d" i)) maturity)
    }

let private genFinding (i: int) : Gen<UnknownGovernedPathFinding> =
    gen {
        let! zone = elements allZones
        let path = GovernedPath(sprintf "src/p%d.fs" i)
        return mkFinding (findingIdForZone zone) path zone
    }

let genRoute: Gen<RouteResult> =
    gen {
        let! nGates = Gen.choose (0, 4)
        let! nFindings = Gen.choose (0, 4)
        let! gates = Gen.collectToList genSelectedGate [ 1..nGates ]
        let! findings = Gen.collectToList genFinding [ 1..nFindings ]
        return mkRoute gates findings
    }

let genDecision: Gen<ShipDecision> =
    gen {
        let! route = genRoute
        let! profile = elements allProfiles
        return decisionOf route Verify profile
    }

type VerifyArbs =
    static member ShipDecision() = Arb.fromGen genDecision

let fsCheckConfig =
    { FsCheckConfig.defaultConfig with arbitrary = [ typeof<VerifyArbs> ] }

// ── JsonDocument read helpers (read-only inspection of the emitted bytes) ──

let parse (json: string) : JsonDocument = JsonDocument.Parse json

let private reqStr (el: JsonElement) : string =
    match el.GetString() with
    | null -> failwith "expected a JSON string but found null"
    | s -> s

let strField (el: JsonElement) (name: string) : string = reqStr (el.GetProperty name)

let fieldOrder (el: JsonElement) : string list =
    [ for p in el.EnumerateObject() -> p.Name ]

let topLevelFieldOrder (doc: JsonDocument) : string list = fieldOrder doc.RootElement

let section (doc: JsonDocument) (name: string) : JsonElement list =
    [ for it in doc.RootElement.GetProperty(name).EnumerateArray() -> it ]

let currency (doc: JsonDocument) (name: string) : JsonElement list =
    [ for it in (doc.RootElement.GetProperty("currency").GetProperty name).EnumerateArray() -> it ]

let hasField (el: JsonElement) (name: string) : bool =
    match el.TryGetProperty name with
    | true, _ -> true
    | false, _ -> false

let lower (s: string) : string = s.ToLowerInvariant()
// 074: findRepoRoot consolidated into the shared RepositoryHelpers (sln||slnx superset).
let repoRoot = FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot
