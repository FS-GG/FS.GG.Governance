module FS.GG.Governance.EnforcementFixtures.Tests.Support

open System
open System.IO
open System.Text
open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Routing.Model
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Route.Model
open FS.GG.Governance.Ship.Model
// 074: findRepoRoot consolidated into the shared RepositoryHelpers (sln||slnx superset).
let repoRoot = FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot

/// The committed golden directory: `<repo>/fixtures/enforcement` (reconciliation D2).
let fixturesDir = Path.Combine(repoRoot, "fixtures", "enforcement")

// ── The byte-compare-or-bless guard (mirrors the repo's `BLESS_SURFACE=1` idiom) ──

let private utf8NoBom = UTF8Encoding(false)

/// True when the run was asked to (re)write the committed goldens rather than check them.
let blessing = Environment.GetEnvironmentVariable "BLESS_FIXTURES" = "1"

/// Generate-or-check one committed fixture under `fixtures/enforcement/<relPath>`.
///
/// With `BLESS_FIXTURES=1` it writes `generated` verbatim (UTF-8, no BOM) and the test trivially
/// passes — this is how the goldens are (re)blessed. Otherwise it reads the committed bytes, normalizes
/// `\r\n`→`\n` (so a CRLF checkout still compares equal), asserts no UTF-8 BOM, and `Expect.equal`s the
/// committed text against the freshly generated text. On drift the failure names the file and the exact
/// re-bless command (FR-006, SC-003).
let blessOrCompare (relPath: string) (generated: string) : unit =
    let full = Path.Combine(fixturesDir, relPath)

    if blessing then
        match Path.GetDirectoryName full with
        | null -> ()
        | dir -> Directory.CreateDirectory dir |> ignore

        File.WriteAllText(full, generated, utf8NoBom)
    else
        Expect.isTrue
            (File.Exists full)
            (sprintf
                "committed fixture %s is missing — regenerate it with `BLESS_FIXTURES=1 dotnet test tests/FS.GG.Governance.EnforcementFixtures.Tests`"
                relPath)

        let raw = File.ReadAllBytes full

        let hasBom =
            raw.Length >= 3 && raw[0] = 0xEFuy && raw[1] = 0xBBuy && raw[2] = 0xBFuy

        Expect.isFalse hasBom (sprintf "committed fixture %s must be UTF-8 without a BOM" relPath)

        let committed = Encoding.UTF8.GetString(raw).Replace("\r\n", "\n")

        Expect.equal
            committed
            generated
            (sprintf
                "DRIFT: committed fixture %s no longer matches the live cores. If this change is INTENTIONAL, re-bless with `BLESS_FIXTURES=1 dotnet test tests/FS.GG.Governance.EnforcementFixtures.Tests` and review the diff; otherwise it is an accidental regression."
                relPath)

// ── The closed dial enumerations, fixed least → most order (research D4) ──

/// Both base severities, least → most. (`Enforcement.Severity`.)
let allBaseSeverities: Severity list = [ Advisory; Blocking ]

/// All five F014 maturities, least → most protective.
let allMaturities: Maturity list =
    [ Observe; Warn; BlockOnPr; BlockOnShip; BlockOnRelease ]

/// All six run modes, least → most protective. `RunMode.Release` qualified (both DUs define `Release`).
let allModes: RunMode list =
    [ Sandbox; Inner; Focused; Verify; Gate; RunMode.Release ]

/// All four profiles, least → most strict. `Profile.Release` qualified.
let allProfiles: Profile list =
    [ Light; Standard; Strict; Profile.Release ]

// ── Real `TypedFacts` for the route-class section (F015/F017 over real facts — no mocks) ──

/// Build a `Surface` from `(class, id, paths)` with inert defaults for the fields F017 never reads.
let private surface (cls: SurfaceClass) (id: string) (paths: string list) : Surface =
    { Id = SurfaceId id
      Class = cls
      Paths = paths |> List.map GovernedPath
      Owner = Owner "fixture"
      Maturity = Observe
      EvidenceTag = None
      TemplateProfile = None
      Baseline = None }

/// The minimal real facts for the route-class scenarios: a governed root `src`, one path-map glob
/// (`src/build/** → build`) so a fenced path genuinely `Routed`s, and one declared `ProtectedSurface`
/// (`src/boundary`) so an unknown there genuinely escalates. Domains = exactly those the path map binds.
let routeClassFacts: TypedFacts =
    let entries = [ { Glob = GovernedPath "src/build/**"; Capability = DomainId "build" } ]
    let domains = entries |> List.map (fun e -> e.Capability) |> List.distinct

    { Project =
        { SchemaVersion = SchemaVersion 1
          Id = ProjectId "fixture"
          Domains = domains
          GovernedRoot = GovernedPath "src"
          PackageSurfaces = []
          PolicyRef = None
          CapabilitiesRef = None }
      Policy = None
      Capabilities =
        { SchemaVersion = SchemaVersion 1
          Domains = domains
          PathMap = entries
          Surfaces = [ surface ProtectedSurface "api" [ "src/boundary" ] ]
          Checks = [] }
      Tooling = None }

// ── Real F018 gate / F017 finding / F019 route builders (F025 `Support.fs` precedent, research D7) ──

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
/// rollup never evaluates cost.
let mkRoute (gates: SelectedGate list) (findings: UnknownGovernedPathFinding list) : RouteResult =
    { SelectedGates = gates
      Findings = { Findings = findings }
      Cost = { Cheap = 0; Medium = 0; High = 0; Exhaustive = 0 } }
