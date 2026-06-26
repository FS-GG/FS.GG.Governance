module FS.GG.Governance.GateRun.Tests.Support

// Real, literally-constructible builders (Principle V — NO mocks). A ToolingFacts/CommandSpec builder so a
// CommandId resolves to a declared command line/timeout/environment; a Gate builder with and without a
// RequiresCommand prerequisite; a deterministic fake ExecutionPort (a literal ExecutionOutcome regardless of
// command) plus a helper that assembles a REAL CommandRecord via `senseExecution fakePort cmd` and a REAL
// `EvidenceCapture.referenceOf` of it, so the `priorExitOf` round-trip reads a genuine canonical-identity
// string (never a hand-written literal). No network, no governed repository (SC-007).

open System
open System.IO
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.GateExecution
open FS.GG.Governance.GateExecution.Model
open FS.GG.Governance.EvidenceCapture
// 074: findRepoRoot consolidated into the shared RepositoryHelpers (sln||slnx superset).
let repoRoot = FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot

// ── tooling / command-spec builders ──

let commandSpec (id: string) (commandLine: string) : CommandSpec =
    { Id = CommandId id
      Command = commandLine
      Timeout = TimeoutLimit 600
      Environment = LocalOrCi }

let toolingOf (commands: CommandSpec list) : ToolingFacts =
    { SchemaVersion = SchemaVersion 1
      Commands = commands
      EnvironmentClasses = [ Local; Ci ]
      ExternalTools = [] }

// ── gate builders ──

let private freshnessKeyOf (check: string) (command: CommandId option) : FreshnessKey =
    { Check = CheckId check
      Domain = DomainId "package-api"
      Cost = Cheap
      Environment = LocalOrCi
      Command = command }

/// A gate whose `RequiresCommand` prerequisite references `commandId` (⇒ `commandFor` resolves to Some).
let gateWithCommand (check: string) (commandId: string) : Gate =
    { Id = GateId("package-api:" + check)
      Domain = DomainId "package-api"
      Description = check
      Prerequisites = [ RequiresCommand(CommandId commandId) ]
      Cost = Cheap
      Timeout = TimeoutLimit 600
      Owner = Owner "platform"
      Maturity = BlockOnShip
      ProductCheck = false
      FreshnessKey = freshnessKeyOf check (Some(CommandId commandId)) }

/// A gate with NO `RequiresCommand` prerequisite (⇒ `commandFor` returns None ⇒ NotExecuted).
let gateWithoutCommand (check: string) : Gate =
    { Id = GateId("package-api:" + check)
      Domain = DomainId "package-api"
      Description = check
      Prerequisites = []
      Cost = Cheap
      Timeout = TimeoutLimit 600
      Owner = Owner "platform"
      Maturity = BlockOnShip
      ProductCheck = false
      FreshnessKey = freshnessKeyOf check None }

// ── deterministic fake ExecutionPort (literal outcome regardless of command) ──

/// A fake ExecutionPort that yields a literal outcome with the chosen exit code (real `byte[]`, not Synthetic
/// stand-ins). Deterministic ⇒ the assembled record's canonical identity is byte-stable.
let fakePortExiting (code: int) : ExecutionPort =
    fun _command ->
        { Stdout = System.Text.Encoding.UTF8.GetBytes "out"
          Stderr = System.Text.Encoding.UTF8.GetBytes "err"
          ExitCode = ExitCode code
          Duration = SensedDuration 42L }

/// Assemble a REAL CommandRecord by running a command through the fake port, then derive its REAL evidence
/// reference (the genuine F049 canonical-identity string the `priorExitOf` round-trip reads).
let realReferenceFor (code: int) (command: GateCommand) : EvidenceRef =
    let record = Interpreter.senseExecution (fakePortExiting code) command
    EvidenceCapture.referenceOf record

/// A simple GateCommand fixture (no process is spawned — the fake port ignores it).
let sampleCommand: GateCommand =
    { Executable = Executable "dotnet"
      Arguments = [ Argument "test" ]
      WorkingDirectory = WorkingDirectory "/repo"
      Environment = { Added = []; Changed = []; Removed = [] }
      Timeout = TimeoutLimit 600
      CapturedOutput = NoCapturedOutput }

/// A non-canonical EvidenceRef fixture (⇒ `priorExitOf` returns None ⇒ recompute).
let nonCanonicalRef: EvidenceRef = EvidenceRef "not-canonical"
