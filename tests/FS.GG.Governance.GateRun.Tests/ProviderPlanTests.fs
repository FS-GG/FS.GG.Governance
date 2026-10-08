module FS.GG.Governance.GateRun.Tests.ProviderPlanTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.GateExecution.Model
open FS.GG.Governance.GateRun

// SYNTHETIC: normalized declared inputs only. Actual Config resolves these declarations;
// tools/profile enforcement and real process observations remain GOV423-C2 phase2/SDD928-C2.3.

module CB = FS.GG.Governance.Config.CapabilityBindings
let command : Fsgg.Provider.DeclaredCommand =
    { Executable = "tool with spaces"; Arguments = [ ""; "two words"; "$HOME"; ";"; "last" ] }
let limits : CB.CommandLimits =
    { WorkingDirectory = Some (GovernedPath "src/./cli")
      Timeout = Some (TimeoutLimit 60); Cost = Some Cheap; Environments = [ Local ] }
let binding : CB.CapabilityBinding =
    { CapabilityId = "build:build"; Required = false; Platforms = [ "linux" ]
      ToolIds = [ "compiler" ]; EvidenceIds = [ "build-log" ]; Binding = CB.Command command; Limits = Some limits }
let format : CB.EvidenceFormat = { Id = "build-log"; Version = "1.0.0" }
let request : CB.ResolutionRequest =
    { ContractVersion = "1.0.0"; SelectedPlatform = "linux"; RequiredCapabilityIds = [ "build:build" ]
      SemanticOnlyObligationIds = []; KnownPlatforms = [ "linux" ]; SupportedEnvironments = [ Local; Ci ]
      SupportedEvidenceFormats = [ format ]; Tools = [ { Id = "compiler"; ExactVersion = "10.0.0" } ]
      Evidence = [ { Id = "build-log"; Format = format; OutputPath = GovernedPath "artifacts/build.json" } ]; Bindings = [ binding ] }
let gate : Gate =
    { Id = GateId "build:build"; Domain = DomainId "build"; Description = "Synthetic effective build gate"
      Prerequisites = [ RequiresCommand (CommandId "legacy-build") ]; Cost = Medium; Timeout = TimeoutLimit 40
      Owner = Owner "synthetic-owner"; Maturity = BlockOnShip; ProductCheck = false
      FreshnessKey = { Check = CheckId "build"; Domain = DomainId "build"; Cost = Medium; Environment = LocalOrCi; Command = Some (CommandId "legacy-build") } }
let context : Plan.ProviderCommandContext =
    { RepoRoot = "/synthetic-governed"; Environment = Local; RemainingTimeout = TimeoutLimit 25
      EnvironmentDelta = { Added = [ { Name = EnvVarName "LITERAL"; Value = EnvVarValue "a $HOME; b" } ]; Changed = []; Removed = [] } }

let resolvedBinding =
    match CB.resolve request with
    | CB.Rejected errors -> failwithf "%A" errors
    | CB.Resolved resolved -> resolved.Bindings.Head

let refuse code profile effective candidate =
    match Plan.commandForBinding profile effective candidate with
    | Ok _ -> failtestf "Expected %A" code
    | Error errors -> Expect.isTrue (errors |> List.exists (fun d -> d.Code = code)) "Located refusal"

[<Tests>]
let tests = testList "ProviderCommand" [
    test "Synthetic exact literal command bypasses string lexer" {
        match Plan.commandForBinding context gate resolvedBinding with
        | Error errors -> failtestf "%A" errors
        | Ok actual ->
            Expect.equal actual.Executable (Executable command.Executable) "Executable stays literal"
            Expect.equal actual.Arguments (command.Arguments |> List.map Argument) "Order/empty/spaces/metacharacters survive"
            Expect.equal actual.Environment context.EnvironmentDelta "Admitted delta carried verbatim"
            Expect.equal actual.WorkingDirectory (WorkingDirectory "/synthetic-governed/src/cli") "Normalized governed cwd"
            Expect.equal actual.Timeout (TimeoutLimit 25) "Host timeout cap"
    }
    test "Synthetic direct projection rejects another gate identity" {
        refuse CB.MalformedInput context { gate with Id = GateId "test:test" } resolvedBinding
    }
    test "Synthetic direct projection refuses traversal and unnormalized or null path" {
        for path in [ "src/../../outside"; "src\\cli"; "src/./cli"; Unchecked.defaultof<string> ] do
            let candidate = { resolvedBinding with Limits = Some { limits with WorkingDirectory = Some (GovernedPath path) } }
            refuse CB.InvalidPath context gate candidate
    }
    test "Synthetic missing executable limits refuse" {
        refuse CB.InvalidExecutionLimit context gate { resolvedBinding with Limits = None }
    }
    test "Synthetic semantic-only declaration cannot acquire command" {
        refuse CB.IllegalSemanticOnly context gate { resolvedBinding with Binding = CB.SemanticOnly; Limits = None }
    }
    test "Synthetic observed environment must be concrete" {
        refuse CB.InvalidExecutionLimit { context with Environment = LocalOrCi } gate resolvedBinding
    }
    test "Synthetic absolute governed root required" {
        refuse CB.InvalidPath { context with RepoRoot = "relative-root" } gate resolvedBinding
    }
    test "Synthetic effective gate environment floor remains authoritative" {
        let effective = { gate with FreshnessKey = { gate.FreshnessKey with Environment = Release } }
        refuse CB.InvalidExecutionLimit context effective resolvedBinding
    }
]
