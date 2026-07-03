module FS.GG.Governance.HumanText.Tests.Support

open System
open System.IO
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Routing.Model
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Route.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.Ship.Ship
open FS.GG.Governance.RouteExplain.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.CacheEligibility
open FS.GG.Governance.CommandRecord
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.CommandKind
open FS.GG.Governance.CommandKind.Model
open FS.GG.Governance.GateRun.Model
open FS.GG.Governance.Provenance.Model
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseRules
open FS.GG.Governance.ReleaseFactsSensing.Model
open FS.GG.Governance.PackEvidence
open FS.GG.Governance.PackEvidence.Model
open FS.GG.Governance.Attestation
open FS.GG.Governance.Attestation.Model
open FS.GG.Governance.ReleaseReport.Model
open FS.GG.Governance.ReleaseReport
// 074: findRepoRoot consolidated into the shared RepositoryHelpers (sln||slnx superset).
let repoRoot = FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot

// ── real F018 gate / F017 finding / F019 route builders (mirroring VerifyJson.Tests.Support) ──

let mkGate (id: GateId) (maturity: Maturity) (cost: Cost) : Gate =
    let (GateId raw) = id
    let domain = DomainId "build"

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

let decisionOf (route: RouteResult) (mode: RunMode) (profile: Profile) : ShipDecision = rollup route mode profile

// route fixtures
let cleanRoute: RouteResult =
    mkRoute [ mkSelectedGate (mkGate (GateId "build:compile") Observe Cheap) ] []

/// A route with a warnings/findings shape — selected gates plus an unknown-governed-path finding
/// (the projection's clear input signal, distinct from a tool defect — FR-012).
let routeWithFindings: RouteResult =
    mkRoute
        [ mkSelectedGate (mkGate (GateId "build:compile") Observe Cheap)
          mkSelectedGate (mkGate (GateId "docs:lint") Observe Medium) ]
        [ mkFinding UnknownGovernedPath (GovernedPath "src/new/Thing.fs") GovernedRootUnknown
          mkFinding UnknownProtectedBoundaryPath (GovernedPath "src/boundary/Api.fs") (ProtectedBoundaryUnknown(SurfaceId "api")) ]

// ── ship / verify fixtures (real F024 rollup) ──

let emptyCleanDecision: ShipDecision = decisionOf emptyRoute Verify Standard

/// A blocked decision: BlockOnShip gate blocks at Verify/Strict, BlockOnRelease relaxes to a warning,
/// Observe passes; a ProtectedBoundaryUnknown finding blocks, a GovernedRootUnknown finding passes.
let blockedDecision: ShipDecision =
    decisionOf
        (mkRoute
            [ mkSelectedGate (mkGate (GateId "build:ship") BlockOnShip Cheap)
              mkSelectedGate (mkGate (GateId "build:rel") BlockOnRelease Cheap)
              mkSelectedGate (mkGate (GateId "docs:lint") Observe Cheap) ]
            [ mkFinding UnknownProtectedBoundaryPath (GovernedPath "src/boundary/Api.fs") (ProtectedBoundaryUnknown(SurfaceId "api"))
              mkFinding UnknownGovernedPath (GovernedPath "src/new/Thing.fs") GovernedRootUnknown ])
        Verify
        Strict

// ── explain fixture (real F023 explain) ──

let highCostRoute: RouteResult =
    mkRoute
        [ mkSelectedGate (mkGate (GateId "build:exhaustive") Observe Exhaustive)
          mkSelectedGate (mkGate (GateId "build:compile") Observe Cheap) ]
        []

let explanation: RouteExplanation =
    let registry: GateRegistry =
        { Gates =
            [ mkGate (GateId "build:exhaustive") Observe Exhaustive
              mkGate (GateId "build:compile") Observe Cheap ] }

    FS.GG.Governance.RouteExplain.RouteExplain.explain highCostRoute registry

// ── evidence fixture (real F041 evaluate) ──

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

let relInputs = { baseInputs with Check = CheckId "rel" }
let candidate (gate: string) (inputs: FreshnessInputs) : CandidateGate = { Gate = GateId gate; Inputs = inputs }

let private recordedStore =
    [ baseInputs, EvidenceRef "ev-A"; relInputs, EvidenceRef "ev-R" ]
    |> List.fold (fun s (i, e) -> EvidenceReuse.record i e s) EvidenceReuse.empty

/// build:ship exact ⇒ Reusable ev-A; build:rel RuleHash moved ⇒ MustRecompute (InputsChanged …).
let evidenceReport: CacheEligibilityReport =
    CacheEligibility.evaluate
        [ candidate "build:ship" baseInputs
          candidate "build:rel" { relInputs with RuleHash = RuleHash "r2" } ]
        recordedStore

let emptyEvidenceReport: CacheEligibilityReport = CacheEligibility.evaluate [] EvidenceReuse.empty

// ── execution outcomes (real GateOutcome) ──

let mixedOutcomes: (GateId * GateOutcome) list =
    [ GateId "build:ship",
      { GateId = GateId "build:ship"
        Disposition = Reused(ExitCode 0, true) }
      GateId "build:rel",
      { GateId = GateId "build:rel"
        Disposition = Executed(ExitCode 1, false) } ]

// ── release fixture (real F26 assemble; mirrors ReleaseReport.Tests.Support) ──

let private blockingRule kind : ReleaseRule =
    { Kind = kind
      Surface = SurfaceId "release"
      BaseSeverity = Blocking
      Maturity = BlockOnRelease }

let private sensedFrom (states: (ReleaseRuleKind * FactState) list) (diagnostics: (ReleaseRuleKind * string) list) : SensedRelease =
    { Facts = { States = Map.ofList states }
      Snapshot =
        { Surface = SurfaceId "release"
          Version = None
          Metadata = None
          Pins = None
          PublishPlan = None
          TrustedPublishing = None
          Provenance = None
          Diagnostics = diagnostics |> List.map (fun (f, r) -> { Family = f; Reason = r }) } }

let private decisionFor (sensed: SensedRelease) : ReleaseDecision =
    let rules = sensed.Facts.States |> Map.toList |> List.map (fst >> blockingRule)
    Release.evaluateRelease rules sensed.Facts

let private record =
    CommandRecord.build
        (Executable "dotnet")
        [ Argument "pack" ]
        (WorkingDirectory "/work")
        { Added = []; Changed = []; Removed = [] }
        (TimeoutLimit 600)
        (ExitCode 0)
        (OutputDigest "o")
        (OutputDigest "e")
        NoCapturedOutput
        (SensedDuration 100L)

let private packRun = { Kind = Pack; Record = record }

let private packEvidence: PackEvidenceSet =
    Pack.evaluatePack
        Map.empty
        [ Packed({ Surface = SurfaceId "A"; ArtifactPath = "a.nupkg"; PackedVersion = "1.1.0"; Digest = ArtifactHash "dA" }, packRun) ]

let private attestationOf () : AttestationSummary =
    let snapshot =
        Audit.auditSnapshot
            (Revision "c") (Revision "b") (Revision "h") (RuleHash "r") (GeneratorVersion "g")
            [ ArtifactHash "dA" ] [ packRun ] Local (BuilderIdentity "ci")

    Attestation.summarize snapshot packEvidence

let private releaseReportOf (states: (ReleaseRuleKind * FactState) list) : ReleaseReport =
    let sensed = sensedFrom states []
    Report.assemble (decisionFor sensed) sensed packEvidence (attestationOf ())

let allFamilies =
    [ VersionBump; PackageMetadata; TemplatePins; PublishPlan; TrustedPublishing; Provenance ]

/// A clean release (every precondition met).
let cleanReleaseReport: ReleaseReport =
    releaseReportOf (allFamilies |> List.map (fun k -> k, Met))

/// A blocked release: VersionBump unmet ⇒ a blocker; the rest met.
let blockedReleaseReport: ReleaseReport =
    releaseReportOf
        ((VersionBump, Unmet) :: (allFamilies |> List.tail |> List.map (fun k -> k, Met)))
