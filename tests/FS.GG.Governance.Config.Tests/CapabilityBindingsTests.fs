module FS.GG.Governance.Config.Tests.CapabilityBindingsTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Config.CapabilityBindings

// Synthetic normalized declarations exercise the pure public API. They are not
// installed tools, execution evidence or a qualified provider descriptor.
let private command : Fsgg.Provider.DeclaredCommand =
    { Executable = "tool with spaces"; Arguments = [ ""; "two words"; "$HOME"; ";"; "last" ] }
let private limits =
    { WorkingDirectory = Some (GovernedPath "src/./cli")
      Timeout = Some (TimeoutLimit 60); Cost = Some Cheap; Environments = [ Local ] }
let private binding =
    { CapabilityId = "build:build"; Required = false; Platforms = [ "linux" ]
      ToolIds = [ "compiler" ]; EvidenceIds = [ "build-log" ]
      Binding = Command command; Limits = Some limits }
let private format = { Id = "build-log"; Version = "1.0.0" }
let private request =
    { ContractVersion = "1.0.0"; SelectedPlatform = "linux"
      RequiredCapabilityIds = [ "build:build" ]; SemanticOnlyObligationIds = []
      KnownPlatforms = [ "linux"; "windows" ]; SupportedEnvironments = [ Local; Ci ]
      SupportedEvidenceFormats = [ format ]
      Tools = [ { Id = "compiler"; ExactVersion = "10.0.0" } ]
      Evidence = [ { Id = "build-log"; Format = format; OutputPath = GovernedPath "artifacts/./build.json" } ]
      Bindings = [ binding ] }
let private rejected code candidate =
    match resolve candidate with
    | Resolved _ -> failtestf "expected refusal %A" code
    | Rejected diagnostics ->
        Expect.isTrue (diagnostics |> List.exists (fun d -> d.Code = code)) (sprintf "located diagnostic %A" code)
        Expect.equal diagnostics (List.sortBy (fun d -> d.CapabilityId, d.Field, d.Code, d.Message) diagnostics) "deterministic diagnostic order"
let private withBinding candidate = { request with Bindings = [ candidate ] }

[<Tests>]
let tests =
    testList "CapabilityBindings" [
        test "canonical inventory is exactly the eight executable meanings" {
            Expect.equal (catalog |> List.map (fun c -> c.Id))
                [ "build:build"; "evidence:evidence"; "lint:lint"; "package:package";
                  "public-surface:public-surface"; "release:release"; "security:security"; "test:test" ] "ADR0092 identities"
            Expect.isTrue (catalog |> List.forall (fun c -> c.RequiresExecutable)) "no command-free neutral alias"
        }
        test "trusted requirement cannot be cleared and literal argv survives normalization" {
            match resolve request with
            | Rejected ds -> failtestf "%A" ds
            | Resolved resolved ->
                let actual = List.head resolved.Bindings
                Expect.isTrue actual.Required "provider false cannot lower trusted requirement"
                Expect.equal actual.Binding (Command command) "empty and metacharacter argv preserve exact order"
                Expect.equal actual.Limits.Value.WorkingDirectory (Some (GovernedPath "src/cli")) "normalized working directory"
                Expect.equal resolved.Evidence.Head.OutputPath (GovernedPath "artifacts/build.json") "normalized evidence path"
        }
        test "trusted gameplay and Fsharp command-free obligations remain distinct" {
            let floors = [ "gameplay:fr-covered"; "gameplay:production-journey"; "fsharp:public-surface" ]
            let floorBindings = floors |> List.map (fun id ->
                { binding with CapabilityId = id; Required = true; ToolIds = []; EvidenceIds = []; Binding = SemanticOnly; Limits = None })
            match resolve { request with SemanticOnlyObligationIds = floors; Bindings = binding :: floorBindings } with
            | Rejected ds -> failtestf "%A" ds
            | Resolved resolved -> Expect.equal resolved.Bindings.Length 4 "original semantics stay command-free"
        }
        test "optional unknown executable declaration is explicitly unsupported" {
            let unknown = { binding with CapabilityId = "future:check" }
            match resolve { request with Bindings = [ binding; unknown ] } with
            | Rejected ds -> failtestf "%A" ds
            | Resolved resolved ->
                Expect.equal resolved.UnsupportedCapabilityIds [ "future:check" ] "not satisfied"
                Expect.equal resolved.Bindings.Length 1 "no unknown executable accepted"
        }
        test "provider required is part of the union" {
            rejected UnknownRequiredSemanticId { request with Bindings = [ binding; { binding with CapabilityId = "future:check"; Required = true } ] }
        }
        test "complete resolution and diagnostics ignore declaration order" {
            let other = { binding with CapabilityId = "test:test"; Platforms = [ "windows"; "linux" ] }
            let candidate = { request with Bindings = [ binding; other ]; Tools = request.Tools @ [ { Id = "runtime"; ExactVersion = "2.0.0" } ] }
            let reversed = { candidate with Bindings = candidate.Bindings |> List.rev |> List.map (fun b -> { b with Platforms = List.rev b.Platforms }); Tools = List.rev candidate.Tools; KnownPlatforms = List.rev candidate.KnownPlatforms; SupportedEnvironments = List.rev candidate.SupportedEnvironments }
            Expect.equal (resolve candidate) (resolve reversed) "complete deterministic result"
            let invalid = { candidate with Bindings = [ { binding with ToolIds = [ "absent" ]; EvidenceIds = [ "missing" ] }; other ]; RequiredCapabilityIds = [ "absent:required"; "build:build" ] }
            Expect.equal (resolve invalid) (resolve { invalid with Bindings = List.rev invalid.Bindings; RequiredCapabilityIds = List.rev invalid.RequiredCapabilityIds }) "deterministic independent failures"
        }
        test "missing and unknown trusted requirements refuse" {
            rejected MissingRequiredBinding { request with Bindings = [] }
            rejected UnknownRequiredSemanticId { request with RequiredCapabilityIds = [ "unknown:check" ] }
        }
        test "neutral semantic-only declarations and trusted overrides refuse" {
            for capability in catalog do
                for required in [ false; true ] do
                    rejected IllegalSemanticOnly
                        { request with RequiredCapabilityIds = []
                                       Bindings = [ { binding with CapabilityId = capability.Id; Required = required; Binding = SemanticOnly; Limits = None } ] }
            rejected IllegalSemanticOnly { request with SemanticOnlyObligationIds = [ "build:build" ] }
            for required in [ false; true ] do
                rejected IllegalSemanticOnly
                    { request with Bindings = [ binding; { binding with CapabilityId = "provider:semantic"; Required = required; Binding = SemanticOnly; Limits = None } ] }
        }
        test "trusted semantic-only obligation cannot acquire a provider command" {
            rejected IllegalSemanticOnly { request with SemanticOnlyObligationIds = [ "gameplay:fr-covered" ]; Bindings = [ binding; { binding with CapabilityId = "gameplay:fr-covered" } ] }
        }
        test "duplicate declarations and references refuse" {
            rejected DuplicateIdentity { request with Bindings = [ binding; binding ] }
            rejected DuplicateIdentity { request with Tools = request.Tools @ request.Tools }
            rejected DuplicateIdentity { request with Evidence = request.Evidence @ request.Evidence }
            rejected DuplicateIdentity (withBinding { binding with ToolIds = [ "compiler"; "compiler" ] })
            rejected DuplicateIdentity (withBinding { binding with EvidenceIds = [ "build-log"; "build-log" ] })
            rejected DuplicateIdentity (withBinding { binding with Platforms = [ "linux"; "linux" ] })
        }
        test "unresolved declarations and unsupported exact formats refuse" {
            rejected UnresolvedTool { request with Tools = [] }
            rejected UnresolvedTool (withBinding { binding with ToolIds = [] })
            rejected UnresolvedEvidence { request with Evidence = [] }
            rejected UnresolvedEvidence (withBinding { binding with EvidenceIds = [] })
            rejected UnresolvedPlatform (withBinding { binding with Platforms = [ "windows" ] })
            rejected UnresolvedPlatform { request with KnownPlatforms = [] }
            rejected UnsupportedEvidenceFormat { request with SupportedEvidenceFormats = [ { format with Version = "2.0.0" } ] }
        }
        test "explicit positive and supported limits are mandatory" {
            rejected InvalidExecutionLimit (withBinding { binding with Limits = None })
            for seconds in [ 0; -1 ] do
                rejected InvalidExecutionLimit (withBinding { binding with Limits = Some { limits with Timeout = Some (TimeoutLimit seconds) } })
            rejected InvalidExecutionLimit (withBinding { binding with Limits = Some { limits with Timeout = None } })
            rejected InvalidExecutionLimit (withBinding { binding with Limits = Some { limits with Cost = None } })
            rejected InvalidExecutionLimit (withBinding { binding with Limits = Some { limits with Environments = [] } })
            rejected InvalidExecutionLimit (withBinding { binding with Limits = Some { limits with Environments = [ Release ] } })
            rejected InvalidPath (withBinding { binding with Limits = Some { limits with WorkingDirectory = None } })
        }
        test "raw absolute and escaping paths refuse before normalization" {
            for path in [ "/tmp/output"; "C:/output"; "C:output"; "../out"; "a/../../out"; "a/../../../a/../out"; @"\\server\share"; "" ] do
                rejected InvalidPath (withBinding { binding with Limits = Some { limits with WorkingDirectory = Some (GovernedPath path) } })
                rejected InvalidPath { request with Evidence = [ { request.Evidence.Head with OutputPath = GovernedPath path } ] }
            rejected InvalidPath { request with Evidence = [ { request.Evidence.Head with OutputPath = GovernedPath "." } ] }
        }
        test "blank executable and declarations refuse while root directory is explicit" {
            rejected MalformedInput (withBinding { binding with Binding = Command { command with Executable = " " } })
            rejected MalformedInput { request with Tools = [ { request.Tools.Head with ExactVersion = "" } ] }
            match resolve (withBinding { binding with Limits = Some { limits with WorkingDirectory = Some (GovernedPath ".") } }) with
            | Rejected ds -> failtestf "%A" ds
            | Resolved _ -> ()
        }
        test "catalog version is exact rather than optimistic major compatibility" {
            for version in [ ""; "1.0.1"; "1.1.0"; "2.0.0" ] do
                rejected UnsupportedContractVersion { request with ContractVersion = version }
        }
    ]
