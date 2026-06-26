module FS.GG.Governance.Ship.Tests.Support

open System
open System.IO
open Expecto
open FsCheck
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Route.Model
open FS.GG.Governance.Enforcement.Enforcement

// Shared REAL-input builders + FsCheck generators for the F024 tests (Principle V — every value
// below is a real, literally-constructible typed value, never a mock). The rollup's per-item input
// domain is the finite cross-product of gate maturity / finding zone × run mode × profile, so the
// totality/determinism/carry sweeps enumerate it exhaustively and the FsCheck arbitraries draw from
// the same finite enumerations — every generated input is a constructible value.

// ── Real F018 gate / F017 finding / F019 route builders ──

/// Build a complete F018 `Gate` from an id and a maturity. The carried freshness/cost/timeout/owner
/// fields are real but NEVER read by `rollup` — only `Id` and `Maturity` matter to the rollup.
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

/// Wrap a real `Gate` as a `SelectedGate` with a representative `SelectingPaths` list (never read by
/// the rollup — F019 already deduped the gate; the rollup maps 1:1 over selected gates).
let mkSelectedGate (gate: Gate) : SelectedGate =
    { Gate = gate
      SelectingPaths =
        [ { Path = GovernedPath "src/a.fs"
            MatchedGlob = GovernedPath "src/**" } ] }

/// Build a real F017 finding from an id, path, and zone.
let mkFinding (id: FindingId) (path: GovernedPath) (zone: FindingZone) : UnknownGovernedPathFinding =
    let (GovernedPath p) = path

    { Id = id
      Path = path
      Zone = zone
      Message = sprintf "unclassified path %s" p }

/// Assemble a `RouteResult` from selected gates + findings. Cost is an all-zero `CostRollup` since the
/// rollup never evaluates cost (FR-012).
let mkRoute (gates: SelectedGate list) (findings: UnknownGovernedPathFinding list) : RouteResult =
    { SelectedGates = gates
      Findings = { Findings = findings }
      Cost = { Cheap = 0; Medium = 0; High = 0; Exhaustive = 0 } }

/// The empty route — no selected gates, no findings (the totality edge case).
let emptyRoute: RouteResult = mkRoute [] []

// ── The enumerated finite lever / maturity / zone domains ──

/// All six run modes, least -> most protective. `RunMode.Release` qualified (both DUs define `Release`).
let allModes: RunMode list =
    [ Sandbox; Inner; Focused; Verify; Gate; RunMode.Release ]

/// All four profiles, least -> most strict. `Profile.Release` qualified.
let allProfiles: Profile list =
    [ Light; Standard; Strict; Profile.Release ]

/// All five F014 maturities.
let allMaturities: Maturity list =
    [ Observe; Warn; BlockOnPr; BlockOnShip; BlockOnRelease ]

/// Both finding zones — the ordinary governed-root unknown and the escalated protected boundary.
let allZones: FindingZone list =
    [ GovernedRootUnknown; ProtectedBoundaryUnknown(SurfaceId "s") ]

/// The finding id that matches a zone (the F017 pairing: protected boundary ⇒ escalated id).
let findingIdForZone (zone: FindingZone) : FindingId =
    match zone with
    | GovernedRootUnknown -> UnknownGovernedPath
    | ProtectedBoundaryUnknown _ -> UnknownProtectedBoundaryPath

// ── FsCheck generators over the finite enumerations (real `RouteResult`s) ──

let private elements xs = Gen.elements xs

/// A real selected gate with a distinct id (index-keyed) and a maturity drawn from `allMaturities`.
let private genSelectedGate (i: int) : Gen<SelectedGate> =
    gen {
        let! maturity = elements allMaturities
        return mkSelectedGate (mkGate (GateId(sprintf "build:check%d" i)) maturity)
    }

/// A real finding with a distinct path (index-keyed) and a zone drawn from `allZones`.
let private genFinding (i: int) : Gen<UnknownGovernedPathFinding> =
    gen {
        let! zone = elements allZones
        let path = GovernedPath(sprintf "src/p%d.fs" i)
        return mkFinding (findingIdForZone zone) path zone
    }

/// A real `RouteResult` of up to a few gates + findings, all drawn from the finite enumerations.
let genRoute: Gen<RouteResult> =
    gen {
        let! nGates = Gen.choose (0, 4)
        let! nFindings = Gen.choose (0, 4)
        let! gates = Gen.collect genSelectedGate [ 1..nGates ]
        let! findings = Gen.collect genFinding [ 1..nFindings ]
        return mkRoute gates findings
    }

/// A `(RouteResult × RunMode × Profile)` triple drawing from the finite enumerations.
let genRollupInput: Gen<RouteResult * RunMode * Profile> =
    gen {
        let! route = genRoute
        let! mode = elements allModes
        let! profile = elements allProfiles
        return route, mode, profile
    }

type ShipArbs =
    static member RunMode() = Arb.fromGen (elements allModes)
    static member Profile() = Arb.fromGen (elements allProfiles)
    static member Maturity() = Arb.fromGen (elements allMaturities)
    static member RouteResult() = Arb.fromGen genRoute
    static member RollupInput() = Arb.fromGen genRollupInput

/// FsCheck config wiring the ship arbitraries (used by the property tests).
let fsCheckConfig =
    { FsCheckConfig.defaultConfig with arbitrary = [ typeof<ShipArbs> ] }
// 074: findRepoRoot consolidated into the shared RepositoryHelpers (sln||slnx superset).
let repoRoot = FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot
