module FS.GG.Governance.EnforcementFixtures.Tests.Generator

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Routing.Model
open FS.GG.Governance.Routing.Routing
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Findings.Findings
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Route.Model
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.Ship.Ship
open FS.GG.Governance.AuditJson.AuditJson
open FS.GG.Governance.EnforcementFixtures.Tests.Support

// The PURE renderer over the merged cores: it enumerates the closed dial cross-product, calls the
// genuine F023/F024/F025/F015/F017 surfaces, and renders byte-stable fixture text. No clock, host path,
// environment, or enumeration-order influence (FR-004) — every value is a deterministic function of the
// cores. The fixed dial-iteration order and token spellings below are the pinned bytes the drift guard
// enforces (contracts/truth-table-format.md).

// ── Dial → token renderers (contracts/truth-table-format.md, exact spellings) ──

let severityToken (s: Severity) : string =
    match s with
    | Advisory -> "advisory"
    | Blocking -> "blocking"

let maturityToken (m: Maturity) : string =
    match m with
    | Observe -> "observe"
    | Warn -> "warn"
    | BlockOnPr -> "block-on-pr"
    | BlockOnShip -> "block-on-ship"
    | BlockOnRelease -> "block-on-release"

let modeToken (m: RunMode) : string =
    match m with
    | Sandbox -> "sandbox"
    | Inner -> "inner"
    | Focused -> "focused"
    | Verify -> "verify"
    | Gate -> "gate"
    | RunMode.Release -> "release"

let profileToken (p: Profile) : string =
    match p with
    | Light -> "light"
    | Standard -> "standard"
    | Strict -> "strict"
    | Profile.Release -> "release"

/// `Routed(d,…)` → `routed:<domain>`; `UnmatchedInRoot` → `unmatched-in-root`; `OutOfScope` →
/// `out-of-scope`.
let routingResultToken (r: RoutingResult) : string =
    match r with
    | Routed(DomainId d, _, _) -> "routed:" + d
    | UnmatchedInRoot -> "unmatched-in-root"
    | OutOfScope -> "out-of-scope"

/// A finding id via the core's own `findingIdToken`; `(none)` when no finding.
let findingToken (f: FindingId option) : string =
    match f with
    | Some id -> findingIdToken id
    | None -> "(none)"

// ── Markdown table rendering (no trailing whitespace, `\n` newlines) ──

/// Escape the one Markdown-significant character in reason text so the pipe table stays well-formed;
/// this is the ONLY transformation applied to a reason cell, and it is byte-stable.
let markdownCell (s: string) : string = s.Replace("|", "\\|")

/// Render a well-formed GitHub pipe table: a header row, a `|---|` rule, then one row per `rows` entry.
/// Cells are emitted verbatim (callers pre-escape via `markdownCell`); `\n` line separators, no trailing
/// whitespace on any line, no trailing newline (the document composer adds spacing).
let renderTable (headers: string list) (rows: string list list) : string =
    let line (cells: string list) = "| " + String.concat " | " cells + " |"
    let rule = "|" + String.replicate (List.length headers) "---|"
    line headers :: rule :: List.map line rows |> String.concat "\n"

// ── The primary cross-product table (T007) ──

/// Render the 240-row primary table: base → maturity → mode → profile in fixed nested order (innermost
/// = profile varies fastest), each row pinning the four dial tokens, the derived effective-severity
/// token, and the reason text VERBATIM from `deriveEffectiveSeverity` (FR-002).
let renderPrimaryTable () : string =
    let rows =
        [ for b in allBaseSeverities do
              for m in allMaturities do
                  for mode in allModes do
                      for p in allProfiles do
                          let d =
                              deriveEffectiveSeverity
                                  { BaseSeverity = b
                                    Maturity = m
                                    Mode = mode
                                    Profile = p }

                          [ severityToken b
                            maturityToken m
                            modeToken mode
                            profileToken p
                            severityToken d.EffectiveSeverity
                            markdownCell d.Reason ] ]

    renderTable [ "base"; "maturity"; "mode"; "profile"; "effective"; "reason" ] rows

// ── The route-class table (T008) ──

/// Run the genuine `Routing.route` + `Findings.findUnknownGovernedPaths` for one candidate path against
/// the real `routeClassFacts`, returning the rendered `| class | example path | route outcome | finding
/// | note |` row.
let private routeClassRow (cls: string) (rawPath: string) (note: string) : string list =
    let path = normalizePath rawPath
    let report = route routeClassFacts [ path ]

    let outcome =
        match report.Routings with
        | [ pr ] -> pr.Result
        | _ -> failwith "route-class generation expected exactly one routing"

    let finding =
        match (findUnknownGovernedPaths routeClassFacts report).Findings with
        | f :: _ -> Some f.Id
        | [] -> None

    let (GovernedPath p) = path
    [ cls; p; routingResultToken outcome; findingToken finding; note ]

/// Render the route-class section: routine (out-of-scope, selects nothing), fenced (routed into a
/// domain), unknown-governed-path (ordinary finding), and the protected-surface unknown variant
/// (escalated finding) — all from the genuine cores over real facts (FR-003).
let renderRouteClassTable () : string =
    let rows =
        [ routeClassRow "routine" "docs/readme.md" "selects nothing; never default-denies, even under the strictest dials"
          routeClassRow "fenced" "src/build/Main.fs" "routes into the domain's gates"
          routeClassRow "unknown-governed-path" "src/new/Thing.fs" "explicit finding; never a silent default-deny"
          routeClassRow "protected-surface-unknown" "src/boundary/Api.fs" "escalated finding on a declared protected boundary" ]

    renderTable [ "class"; "example path"; "route outcome"; "finding"; "note" ] rows

// ── The whole document (T009) ──

/// Compose the full `truth-table.md`: title, the do-not-edit/regenerate note, the `##` primary table,
/// and the `##` route-class table, in the fixed order from the contract. UTF-8, `\n` newlines, exactly
/// one trailing newline.
let renderTruthTable () : string =
    [ "# Golden Enforcement Truth Table"
      ""
      "<!-- GENERATED — do not edit by hand; regenerate with `BLESS_FIXTURES=1 dotnet test tests/FS.GG.Governance.EnforcementFixtures.Tests`. -->"
      ""
      "Every value below comes verbatim from the merged enforcement cores (F023 `deriveEffectiveSeverity`,"
      "F015 `Routing.route`, F017 `findUnknownGovernedPaths`). This file is a coverage/evidence artifact —"
      "it computes no new semantics. A byte-equality drift guard regenerates it from the live cores."
      ""
      "## Primary cross-product (base severity × maturity × run mode × profile)"
      ""
      renderPrimaryTable ()
      ""
      "## Route classes (routine vs fenced vs unknown governed path)"
      ""
      renderRouteClassTable () ]
    |> String.concat "\n"
    |> fun body -> body + "\n"

// ── US2: the blocking-altering audit.json snapshot scenarios (T017/T018) ──

/// One named scenario seeding one `audit.json` snapshot: the single dial under test, the real route /
/// mode / profile fed to the genuine `Ship.rollup`, and the section the dialed item must land in.
type Scenario =
    { Name: string
      DialUnderTest: string
      Route: RouteResult
      Mode: RunMode
      Profile: Profile
      ExpectedSection: string }

/// The verbatim merged projection for a scenario — `ofShipDecision (rollup route mode profile)`. No new
/// schema, no post-processing (FR-008).
let snapshotFor (s: Scenario) : string =
    ofShipDecision (rollup s.Route s.Mode s.Profile) None

// Convenience real items for the scenarios (all driven through the genuine rollup).
let private gateRoute (id: string) (maturity: Maturity) : RouteResult =
    mkRoute [ mkSelectedGate (mkGate (GateId id) maturity) ] []

let private protectedFindingRoute: RouteResult =
    mkRoute
        []
        [ mkFinding UnknownProtectedBoundaryPath (GovernedPath "src/boundary/Api.fs") (ProtectedBoundaryUnknown(SurfaceId "api")) ]

/// The fixed named scenario set (contracts/audit-snapshot-set.md). Each isolates ONE dial as the lever
/// that flips blocking; `ExpectedSection` is the section the genuine `rollup` actually places the item
/// in (confirmed against the blessed truth table; the partition test T020 is the safety net).
///
/// CONTRACT DEVIATION (maintainer-confirmed 2026-06-21): the contract's expected partition for the two
/// `maturity-withholds-*` scenarios is `warnings`, but that is UNREALIZABLE through the mandated
/// `ofShipDecision (rollup …)` path — `Ship.rollup` derives base severity FROM maturity, so an
/// `Observe`/`Warn` gate is base-`Advisory` and lands in `passing` (base == effective), never in
/// `warnings` (which requires a base-`Blocking` item relaxed to `Advisory`). FR-008 forbids a hand-built
/// `ShipDecision` to force the warnings case. These two scenarios therefore honestly assert `passing`
/// (the maturity dial's blocking-withholding still demonstrated); the no-hide *warnings* evidence comes
/// from `profile-relaxes-blocker` and `mode-below-floor`, which are genuine base-`Blocking`→`Advisory`
/// relaxations.
let scenarios: Scenario list =
    [ { Name = "maturity-withholds-observe"
        DialUnderTest = "maturity"
        Route = gateRoute "build:observe" Observe
        Mode = RunMode.Release
        Profile = Profile.Release
        ExpectedSection = "passing" }
      { Name = "maturity-withholds-warn"
        DialUnderTest = "maturity"
        Route = gateRoute "build:warn" Warn
        Mode = RunMode.Release
        Profile = Profile.Release
        ExpectedSection = "passing" }
      { Name = "base-advisory-stays-advisory"
        DialUnderTest = "base severity"
        Route = gateRoute "build:advisory" Warn
        Mode = RunMode.Release
        Profile = Profile.Release
        ExpectedSection = "passing" }
      { Name = "profile-relaxes-blocker"
        DialUnderTest = "profile"
        Route = gateRoute "build:rel" BlockOnRelease
        Mode = Gate
        Profile = Light
        ExpectedSection = "warnings" }
      { Name = "profile-tightens-to-block"
        DialUnderTest = "profile"
        Route = gateRoute "build:rel" BlockOnRelease
        Mode = Gate
        Profile = Profile.Release
        ExpectedSection = "blockers" }
      { Name = "mode-below-floor"
        DialUnderTest = "run mode"
        Route = gateRoute "build:ship" BlockOnShip
        Mode = Inner
        Profile = Standard
        ExpectedSection = "warnings" }
      { Name = "mode-reaches-floor"
        DialUnderTest = "run mode"
        Route = gateRoute "build:ship" BlockOnShip
        Mode = Gate
        Profile = Standard
        ExpectedSection = "blockers" } ]
