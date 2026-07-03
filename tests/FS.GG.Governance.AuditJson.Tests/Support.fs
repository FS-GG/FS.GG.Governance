module FS.GG.Governance.AuditJson.Tests.Support

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

// Shared REAL-input builders + FsCheck generators for the F025 tests (Principle V — every value below
// is a real, literally-constructible typed value, never a mock). Each fixture is assembled into a real
// F019 `RouteResult` and rolled up by the GENUINE F024 `Ship.rollup` at a real `RunMode`/`Profile`, so
// every `ShipDecision` under test is the value a downstream `fsgg ship`/CI/agent caller holds — not a
// hand-built record. The JSON read helpers inspect the EMITTED BYTES via a read-only `JsonDocument`
// parse, exactly as the kernel's `Json` tests and F020/F021's projection tests do. No I/O, no clock.
// (Builders + generators mirror the F024 `Ship.Tests.Support` real-chain style verbatim.)

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

// ── The real rollup convenience: every fixture is a genuine F024 decision ──

/// Roll a route up into a real `ShipDecision` via the GENUINE F024 `Ship.rollup` (research D7) — never
/// a hand-built value. Every decision under test flows through the real partition + enforcement chain.
let decisionOf (route: RouteResult) (mode: RunMode) (profile: Profile) : ShipDecision =
    rollup route mode profile

// ── Named discriminating-case decisions (all real `rollup` outputs) ──

/// The empty/clean decision: no items; `Pass`; `Clean` (the totality success edge, FR-009).
let emptyCleanDecision: ShipDecision = decisionOf emptyRoute Gate Standard

/// A decision carrying at least one blocker — a base-`Blocking` `BlockOnShip` gate at `Gate`/`Standard`
/// blocks (effective `Blocking`).
let blockersDecision: ShipDecision =
    decisionOf (mkRoute [ mkSelectedGate (mkGate (GateId "build:tests") BlockOnShip) ] []) Gate Standard

/// A rich decision exercising all three sections AND the no-hide warning case, rolled up at
/// `Gate`/`Standard`:
///   • `build:ship`  BlockOnShip      → blocker  (base Blocking, effective Blocking at Gate)
///   • `build:rel`   BlockOnRelease   → warning  (base Blocking RELAXED to effective Advisory — the
///                                                release boundary is above Gate; the no-hide case)
///   • `docs:lint`   Observe          → passing  (base Advisory)
///   • a ProtectedBoundaryUnknown finding → blocker  (base Blocking, block-on-ship-equiv)
///   • a GovernedRootUnknown finding      → passing  (base Advisory, warn)
let richDecision: ShipDecision =
    decisionOf
        (mkRoute
            [ mkSelectedGate (mkGate (GateId "build:ship") BlockOnShip)
              mkSelectedGate (mkGate (GateId "build:rel") BlockOnRelease)
              mkSelectedGate (mkGate (GateId "docs:lint") Observe) ]
            [ mkFinding UnknownProtectedBoundaryPath (GovernedPath "src/boundary/Api.fs") (ProtectedBoundaryUnknown(SurfaceId "api"))
              mkFinding UnknownGovernedPath (GovernedPath "src/new/Thing.fs") GovernedRootUnknown ])
        Gate
        Standard

/// A decision whose `passing` carries the SAME finding id (`UnknownGovernedPath`) on TWO different
/// governed paths — both base-Advisory `GovernedRootUnknown` findings (FR-004: distinct entries).
let sameFindingIdTwoPathsDecision: ShipDecision =
    decisionOf
        (mkRoute
            []
            [ mkFinding UnknownGovernedPath (GovernedPath "src/one.fs") GovernedRootUnknown
              mkFinding UnknownGovernedPath (GovernedPath "src/two.fs") GovernedRootUnknown ])
        Gate
        Standard

// ── FsCheck generators over the finite enumerations (real `RouteResult`s, real `rollup`) ──

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

/// A real `RouteResult` of up to a few gates + findings, all drawn from the finite enumerations.
let genRoute: Gen<RouteResult> =
    gen {
        let! nGates = Gen.choose (0, 4)
        let! nFindings = Gen.choose (0, 4)
        let! gates = Gen.collectToList genSelectedGate [ 1..nGates ]
        let! findings = Gen.collectToList genFinding [ 1..nFindings ]
        return mkRoute gates findings
    }

/// A real `ShipDecision` generated by driving the GENUINE `Ship.rollup` over a generated
/// `RouteResult × RunMode × Profile` (research D7) — the input stays a real upstream-assembled value.
let genDecision: Gen<ShipDecision> =
    gen {
        let! route = genRoute
        let! mode = elements allModes
        let! profile = elements allProfiles
        return decisionOf route mode profile
    }

type AuditArbs =
    static member ShipDecision() = Arb.fromGen genDecision

/// FsCheck config wiring the audit arbitraries (used by the property tests).
let fsCheckConfig =
    { FsCheckConfig.defaultConfig with arbitrary = [ typeof<AuditArbs> ] }

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

/// The item objects of a named section (`blockers`/`warnings`/`passing`), in emitted order.
let section (doc: JsonDocument) (name: string) : JsonElement list =
    [ for it in doc.RootElement.GetProperty(name).EnumerateArray() -> it ]

/// Whether an object element has a property of the given name.
let hasField (el: JsonElement) (name: string) : bool =
    match el.TryGetProperty name with
    | true, _ -> true
    | false, _ -> false

/// An item's `kind` discriminator.
let itemKind (item: JsonElement) : string = strField item "kind"

/// An item's declared `id`.
let itemId (item: JsonElement) : string = strField item "id"

/// An item's governed `path` (finding items only; gate items have no `path` field).
let itemPath (item: JsonElement) : string = strField item "path"

/// The six enforcement fields of an item as a `(field, value)` list in emitted order.
let enforcementFields (item: JsonElement) : (string * string) list =
    let e = item.GetProperty "enforcement"
    [ for p in e.EnumerateObject() -> p.Name, reqStr p.Value ]

/// One enforcement field value by name.
let enforcement (item: JsonElement) (name: string) : string =
    strField (item.GetProperty "enforcement") name

/// Every string value anywhere in the document (recursively) — for the positive-allowlist sweep.
let rec allStringValues (el: JsonElement) : string list =
    match el.ValueKind with
    | JsonValueKind.String -> [ reqStr el ]
    | JsonValueKind.Object -> [ for p in el.EnumerateObject() do yield! allStringValues p.Value ]
    | JsonValueKind.Array -> [ for v in el.EnumerateArray() do yield! allStringValues v ]
    | _ -> []

/// The whole emitted document text, lowercased — for the deny-token exclusion sweep.
let lower (s: string) : string = s.ToLowerInvariant()
// 074: findRepoRoot consolidated into the shared RepositoryHelpers (sln||slnx superset).
let repoRoot = FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot
