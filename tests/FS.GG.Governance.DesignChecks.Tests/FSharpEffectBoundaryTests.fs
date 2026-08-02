module FS.GG.Governance.DesignChecks.Tests.FSharpEffectBoundaryTests

open System
open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.DesignChecks.FSharpEffectBoundary
module SC = FS.GG.Governance.SurfaceChecks.Model

let private request : SC.SurfaceCheckRequest = { Domain = SC.DesignDomain; Surface = SurfaceId "fsharp-effect-boundary"; Class = DesignSurface; Path = normalizePath "src/App.fsproj"; EvidenceTag = None }
let private baseFact =
    { Project = "src/App.fsproj"; Symbol = "transition"; Source = normalizePath "src/Domain.fs"; IsStatefulWorkflow = true
      IsPureParserOrValidator = false; IsThinOneShotAdapter = false; DirectEffects = []; CallbackHiddenState = false
      ExceptionDrivenContinuation = false; EdgeInterpreter = None; Delivery = None; Exemption = NoExemption }

[<Tests>]
let tests = testList "FSharpEffectBoundary" [
    test "pure reducer is clean without name-shaped requirements" { Expect.isEmpty (evaluate request [ { baseFact with Symbol = "reduceEverything" } ]) "pure declared transition passes" }
    test "direct filesystem and process effects fail with their categories" {
        let codes = evaluate request [ { baseFact with DirectEffects = [ Filesystem; Process ] } ] |> List.filter (fun f -> f.Code = "fsharp.effect-in-transition") |> List.map _.Location.Detail
        Expect.equal codes [ "filesystem"; "process" ] "diagnostics identify classified effects" }
    test "effect edge requires explicit result retry and idempotency semantics" {
        let codes = evaluate request [ { baseFact with DirectEffects = [ Network ]; EdgeInterpreter = Some "interpret"; Delivery = Some { SuccessMessage = None; FailureMessage = None; RetryPolicy = None; Idempotency = None } } ] |> List.map _.Code
        Expect.contains codes "fsharp.effect-result-message-missing" "success/failure messages are required"
        Expect.contains codes "fsharp.effect-retry-missing" "retry visible"
        Expect.contains codes "fsharp.effect-idempotency-missing" "idempotency visible" }
    test "callback hidden state and exception continuations fail" {
        let codes = evaluate request [ { baseFact with CallbackHiddenState = true; ExceptionDrivenContinuation = true } ] |> List.map _.Code
        Expect.equal codes [ "fsharp.callback-hidden-state"; "fsharp.exception-continuation" ] "both hidden continuations are named" }
    test "pure parsers and dated exemptions are narrow non-applicable cases" {
        let parser = { baseFact with IsPureParserOrValidator = true; DirectEffects = [ Filesystem ] }
        let exempt = { baseFact with DirectEffects = [ Filesystem ]; Exemption = ActiveExemption("team", "one-shot host bridge", DateOnly(2099, 1, 1)) }
        Expect.isEmpty (evaluate request [ parser; exempt ]) "legitimate exemptions do not manufacture MVU ceremony" }
    test "expired exemption fails closed" {
        let findings = evaluate request [ { baseFact with Exemption = InvalidExemption "review date expired" } ]
        Expect.equal (findings |> List.exactlyOne).Code "fsharp.effect-exemption-invalid" "invalid policy is input state" }
]
