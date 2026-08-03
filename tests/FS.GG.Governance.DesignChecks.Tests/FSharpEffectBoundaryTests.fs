module FS.GG.Governance.DesignChecks.Tests.FSharpEffectBoundaryTests

open System
open System.IO
open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.DesignChecks.FSharpEffectBoundary
module SC = FS.GG.Governance.SurfaceChecks.Model

let private request : SC.SurfaceCheckRequest = { Domain = SC.DesignDomain; Surface = SurfaceId "fsharp-effect-boundary"; Class = DesignSurface; Path = normalizePath "src/App.fsproj"; EvidenceTag = None }
let private baseFact =
    { Project = "src/App.fsproj"; Symbol = "transition"; Source = normalizePath "src/Domain.fs"; IsStatefulWorkflow = true
      IsPureParserOrValidator = false; IsThinOneShotAdapter = false; DirectEffects = []; CallbackHiddenState = false
      ExceptionDrivenContinuation = false; EdgeInterpreter = None; Delivery = None; Exemption = NoExemption }
let private effect category symbol line column = { Category = category; Symbol = symbol; Line = line; Column = column }

[<Tests>]
let tests = testList "FSharpEffectBoundary" [
    test "pure reducer is clean without name-shaped requirements" { Expect.isEmpty (evaluate request [ { baseFact with Symbol = "reduceEverything" } ]) "pure declared transition passes" }
    test "direct filesystem and process effects fail with their categories" {
        let codes = evaluate request [ { baseFact with DirectEffects = [ effect Filesystem "File.WriteAllText" 8 12; effect Process "Process.Start" 9 12 ] } ] |> List.filter (fun f -> f.Code = "fsharp.effect-in-transition") |> List.map _.Location.Detail
        Expect.equal codes [ "filesystem File.WriteAllText@8:12"; "process Process.Start@9:12" ] "diagnostics identify the calls and their locations" }
    test "effect edge requires explicit result retry and idempotency semantics" {
        let codes = evaluate request [ { baseFact with DirectEffects = [ effect Network "HttpClient" 3 9 ]; EdgeInterpreter = Some "interpret"; Delivery = Some { SuccessMessage = None; FailureMessage = None; RetryPolicy = None; Idempotency = None } } ] |> List.map _.Code
        Expect.contains codes "fsharp.effect-result-message-missing" "success/failure messages are required"
        Expect.contains codes "fsharp.effect-retry-missing" "retry visible"
        Expect.contains codes "fsharp.effect-idempotency-missing" "idempotency visible" }
    test "callback hidden state and exception continuations fail" {
        let codes = evaluate request [ { baseFact with CallbackHiddenState = true; ExceptionDrivenContinuation = true } ] |> List.map _.Code
        Expect.equal codes [ "fsharp.callback-hidden-state"; "fsharp.exception-continuation" ] "both hidden continuations are named" }
    test "pure parsers and dated exemptions are narrow non-applicable cases" {
        let parser = { baseFact with IsPureParserOrValidator = true; DirectEffects = [ effect Filesystem "File.ReadAllText" 2 4 ] }
        let exempt = { baseFact with DirectEffects = [ effect Filesystem "File.ReadAllText" 2 4 ]; Exemption = ActiveExemption("team", "one-shot host bridge", DateOnly(2099, 1, 1)) }
        Expect.isEmpty (evaluate request [ parser; exempt ]) "legitimate exemptions do not manufacture MVU ceremony" }
    test "expired exemption fails closed" {
        let findings = evaluate request [ { baseFact with Exemption = InvalidExemption "review date expired" } ]
        Expect.equal (findings |> List.exactlyOne).Code "fsharp.effect-exemption-invalid" "invalid policy is input state" }
    test "compiled declared boundary senses only its own transition body" {
        let root = Path.Combine(Path.GetTempPath(), "fsgg-effect-boundary-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory root |> ignore
        try
            File.WriteAllText(Path.Combine(root, "App.fsproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><Compile Include=\"Domain.fs\" /></ItemGroup></Project>")
            File.WriteAllText(Path.Combine(root, "Domain.fs"), "// fsgg:effect-boundary transition\nlet transition model = File.WriteAllText(\"x\", model)\nlet parser text = text.Length")
            let facts = senseProject root "App.fsproj" |> Result.defaultWith failtest
            Expect.equal facts.Length 1 "only declared symbol is applicable"
            let finding = evaluate request facts |> List.find (fun f -> f.Code = "fsharp.effect-in-transition")
            Expect.equal finding.Location.Detail "filesystem File.WriteAllText@2:24" "effect identity and source location are bound to the transition"
        finally Directory.Delete(root, true) }
    test "comments literals and identifier-shaped text are not executable effects" {
        let root = Path.Combine(Path.GetTempPath(), "fsgg-effect-lexical-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory root |> ignore
        try
            File.WriteAllText(Path.Combine(root, "App.fsproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><Compile Include=\"Domain.fs\" /></ItemGroup></Project>")
            File.WriteAllText(Path.Combine(root, "Domain.fs"), """// fsgg:effect-boundary advance
let advance model =
    // pure: no File.WriteAllText here
    (* nor Process.Start() in a block comment *)
    let ordinary = "File.WriteAllText(ignored)"
    let interpolated = $"File.WriteAllText({model})"
    let FileWriteAllText = ordinary
    model""")
            let fact = senseProject root "App.fsproj" |> Result.defaultWith failtest |> List.exactlyOne
            Expect.isEmpty fact.DirectEffects "non-executable text does not produce effect calls"
        finally Directory.Delete(root, true) }
    test "executable calls inside interpolation holes retain call identity" {
        let root = Path.Combine(Path.GetTempPath(), "fsgg-effect-interpolation-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory root |> ignore
        try
            File.WriteAllText(Path.Combine(root, "App.fsproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><Compile Include=\"Domain.fs\" /></ItemGroup></Project>")
            File.WriteAllText(Path.Combine(root, "Domain.fs"), "// fsgg:effect-boundary advance\nlet advance (path: string) (model: string) = $\"{File.WriteAllText(path, model)}\"")
            let calls = (senseProject root "App.fsproj" |> Result.defaultWith failtest |> List.exactlyOne).DirectEffects
            let call = calls |> List.find (fun actual -> actual.Symbol = "File.WriteAllText")
            Expect.equal call { Category = Filesystem; Symbol = "File.WriteAllText"; Line = 2; Column = 49 } "the executable interpolation expression remains visible to call sensing"
            Expect.isNonEmpty calls "an executable interpolation call cannot yield empty DirectEffects"
        finally Directory.Delete(root, true) }
    test "an executable call retains exact identity and one-based location" {
        let root = Path.Combine(Path.GetTempPath(), "fsgg-effect-call-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory root |> ignore
        try
            File.WriteAllText(Path.Combine(root, "App.fsproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><Compile Include=\"Domain.fs\" /></ItemGroup></Project>")
            File.WriteAllText(Path.Combine(root, "Domain.fs"), """// fsgg:effect-boundary advance
let advance model =
    File.WriteAllText("out.txt", model)""")
            let call = (senseProject root "App.fsproj" |> Result.defaultWith failtest |> List.exactlyOne).DirectEffects |> List.exactlyOne
            Expect.equal call { Category = Filesystem; Symbol = "File.WriteAllText"; Line = 3; Column = 5 } "the actual call is diagnostic evidence"
        finally Directory.Delete(root, true) }
    test "symbol-local sensing excludes later edge bodies and preserves visible delivery values" {
        let root = Path.Combine(Path.GetTempPath(), "fsgg-effect-symbol-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory root |> ignore
        try
            File.WriteAllText(Path.Combine(root, "App.fsproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><Compile Include=\"Domain.fs\" /></ItemGroup></Project>")
            File.WriteAllText(Path.Combine(root, "Domain.fs"), "// fsgg:effect-boundary advance edge=interpret success=Saved failure=Failed retry=never idempotency=document-id\nlet advance model = model\nlet interpret text = File.WriteAllText(\"x\", text)")
            let fact = senseProject root "App.fsproj" |> Result.defaultWith failtest |> List.exactlyOne
            Expect.isEmpty fact.DirectEffects "the later interpreter body is outside advance"
            Expect.equal fact.EdgeInterpreter (Some "interpret") "the declared real edge symbol is retained"
            Expect.equal fact.Delivery (Some { SuccessMessage = Some "Saved"; FailureMessage = Some "Failed"; RetryPolicy = Some "never"; Idempotency = Some "document-id" }) "delivery facts come from exact visible values"
        finally Directory.Delete(root, true) }
    test "missing symbols and substring-shaped options fail closed" {
        let sense (source: string) =
            let root = Path.Combine(Path.GetTempPath(), "fsgg-effect-malformed-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory root |> ignore
            try
                File.WriteAllText(Path.Combine(root, "App.fsproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><Compile Include=\"Domain.fs\" /></ItemGroup></Project>")
                File.WriteAllText(Path.Combine(root, "Domain.fs"), source)
                senseProject root "App.fsproj"
            finally Directory.Delete(root, true)
        Expect.isError (sense "// fsgg:effect-boundary missing\nlet actual model = model") "a nonexistent declaration symbol is malformed"
        Expect.isError (sense "// fsgg:effect-boundary transition not-edge\nlet transition model = model") "not-edge is not accepted by substring semantics"
        Expect.isError (sense "// fsgg:effect-boundary transition edge=missing\nlet transition model = model") "an unresolved edge symbol is malformed" }
]
