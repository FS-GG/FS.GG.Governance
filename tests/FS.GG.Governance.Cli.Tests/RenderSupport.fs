module FS.GG.Governance.Cli.Tests.RenderSupport

open System.IO
open Spectre.Console
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Route.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.Ship.Ship
open FS.GG.Governance.HumanText

// Shared fixtures for the F27 HumanRender edge tests. The ReportView under test is the REAL
// projection of a REAL ShipDecision rolled by the genuine F024 `Ship.rollup` (Principle V — no
// mock). Console builders back an IAnsiConsole with a StringWriter at a fixed width so the rich/
// width tests are deterministic without a real terminal.

let private mkGate (id: string) (maturity: Maturity) : Gate =
    let domain = DomainId "build"

    { Id = GateId id
      Domain = domain
      Description = sprintf "gate %s" id
      Prerequisites = []
      Cost = Cheap
      Timeout = TimeoutLimit 60
      Owner = Owner "team"
      Maturity = maturity
      ProductCheck = false
      FreshnessKey =
        { Check = CheckId id
          Domain = domain
          Cost = Cheap
          Environment = Local
          Command = None } }

let private mkSelected (id: string) (maturity: Maturity) : SelectedGate =
    { Gate = mkGate id maturity
      SelectingPaths = [ { Path = GovernedPath "src/a.fs"; MatchedGlob = GovernedPath "src/**" } ] }

let private route: RouteResult =
    { SelectedGates = [ mkSelected "build:ship" BlockOnShip; mkSelected "docs:lint" Observe ]
      Findings = { Findings = [] }
      Cost = { Cheap = 0; Medium = 0; High = 0; Exhaustive = 0 } }

/// A real blocked ShipDecision (BlockOnShip blocks at Verify/Strict).
let blockedDecision: ShipDecision = rollup route Verify Strict

/// The real ReportView the plain/JSON/rich/TUI surfaces all project from this decision.
let blockedView: ReportView.ReportView =
    ReportView.viewOfShipDecision blockedDecision None []

/// The plain projection of the same decision (the degrade-path string).
let blockedPlain: string = HumanText.ofShipDecision blockedDecision None []

/// An IAnsiConsole writing ANSI-free text to a StringWriter at a fixed width.
let plainConsole (width: int) : IAnsiConsole * StringWriter =
    let sw = new StringWriter()
    let settings = AnsiConsoleSettings()
    settings.Ansi <- AnsiSupport.No
    settings.ColorSystem <- ColorSystemSupport.NoColors
    settings.Out <- AnsiConsoleOutput(sw)
    let console = AnsiConsole.Create settings
    console.Profile.Width <- width
    console, sw

/// An IAnsiConsole that DOES emit ANSI color, for the rich-color assertion.
let colorConsole (width: int) : IAnsiConsole * StringWriter =
    let sw = new StringWriter()
    let settings = AnsiConsoleSettings()
    settings.Ansi <- AnsiSupport.Yes
    settings.ColorSystem <- ColorSystemSupport.Standard
    settings.Out <- AnsiConsoleOutput(sw)
    let console = AnsiConsole.Create settings
    console.Profile.Width <- width
    console, sw

let esc = string (char 0x1B)
