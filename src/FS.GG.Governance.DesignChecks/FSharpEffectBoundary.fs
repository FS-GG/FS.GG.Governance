namespace FS.GG.Governance.DesignChecks

open System
open System.IO
open System.Text.RegularExpressions
open System.Xml.Linq
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement

module SC = FS.GG.Governance.SurfaceChecks.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module FSharpEffectBoundary =
    type EffectCategory = Filesystem | Process | Environment | ClockOrRandomness | Network | UiOrHost | Persistence | MutableGlobalState
    type Delivery = { SuccessMessage: string option; FailureMessage: string option; RetryPolicy: string option; Idempotency: string option }
    type Exemption = NoExemption | ActiveExemption of owner: string * rationale: string * reviewBy: DateOnly | InvalidExemption of reason: string
    type BoundaryFacts =
        { Project: string; Symbol: string; Source: GovernedPath; IsStatefulWorkflow: bool; IsPureParserOrValidator: bool
          IsThinOneShotAdapter: bool; DirectEffects: EffectCategory list; CallbackHiddenState: bool
          ExceptionDrivenContinuation: bool; EdgeInterpreter: string option; Delivery: Delivery option; Exemption: Exemption }

    let private names = function
        | Filesystem -> "filesystem" | Process -> "process" | Environment -> "environment" | ClockOrRandomness -> "clock-randomness"
        | Network -> "network" | UiOrHost -> "ui-host" | Persistence -> "persistence" | MutableGlobalState -> "mutable-global-state"

    let private classify (text: string) =
        [ Filesystem, [ "File."; "Directory."; "FileStream" ]; Process, [ "Process.Start"; "Diagnostics.Process" ]
          Environment, [ "Environment."; "GetEnvironmentVariable" ]; ClockOrRandomness, [ "DateTime.Now"; "DateTime.UtcNow"; "Random(" ]
          Network, [ "HttpClient"; "WebRequest"; "Socket" ]; UiOrHost, [ "Console."; "Application."; "Window" ]
          Persistence, [ "SaveChanges"; "Serialize"; "WriteAll" ]; MutableGlobalState, [ "static let mutable"; "let mutable" ] ]
        |> List.choose (fun (category, markers) -> if markers |> List.exists (fun m -> text.Contains(m, StringComparison.Ordinal)) then Some category else None)

    let senseProject root project =
        try
            let path = Path.Combine(root, project)
            if not (File.Exists path) then Error(sprintf "F# project was not found: %s" project) else
            let dir = Path.GetDirectoryName(path) |> Option.ofObj |> Option.defaultValue root
            XDocument.Load(path).Descendants(XName.Get "Compile")
            |> Seq.choose (fun node -> node.Attribute(XName.Get "Include") |> Option.ofObj |> Option.map (fun a -> a.Value))
            |> Seq.collect (fun source ->
                if not (source.EndsWith(".fs", StringComparison.OrdinalIgnoreCase)) || source.EndsWith(".fsi", StringComparison.OrdinalIgnoreCase) then [] else
                let full = Path.Combine(dir, source)
                if not (File.Exists full) then raise (FileNotFoundException(source))
                let text = File.ReadAllText(full)
                let declarations = Regex.Matches(text, "(?m)^\\s*//\\s*fsgg:effect-boundary\\s+(?<symbol>[A-Za-z_][A-Za-z0-9_']*)(?<options>.*)$") |> Seq.cast<Match> |> Seq.toList
                declarations
                |> List.map (fun declaration ->
                    let start = declaration.Index + declaration.Length
                    let next = declarations |> List.tryFind (fun other -> other.Index > declaration.Index) |> Option.map (fun other -> other.Index) |> Option.defaultValue text.Length
                    let body = text.Substring(start, next - start)
                    let options = declaration.Groups.["options"].Value
                    let has (option: string) = options.Contains(option, StringComparison.OrdinalIgnoreCase)
                    let delivery =
                        if has "edge" then Some { SuccessMessage = Some "success"; FailureMessage = Some "failure"; RetryPolicy = Some "declared"; Idempotency = Some "declared" }
                        else None
                    { Project = project; Symbol = declaration.Groups.["symbol"].Value; Source = normalizePath source; IsStatefulWorkflow = true
                      IsPureParserOrValidator = has "parser" || has "validator"; IsThinOneShotAdapter = has "thin-adapter"
                      DirectEffects = classify body; CallbackHiddenState = body.Contains("Async.Start", StringComparison.Ordinal) || body.Contains("ContinueWith", StringComparison.Ordinal)
                      ExceptionDrivenContinuation = body.Contains("try", StringComparison.Ordinal) && body.Contains("with", StringComparison.Ordinal)
                      EdgeInterpreter = (if has "edge" then Some "declared-edge" else None); Delivery = delivery; Exemption = NoExemption }))
            |> Seq.toList |> Ok
        with ex -> Error(sprintf "unable to sense F# effect boundaries for '%s': %s" project ex.Message)

    let private emit request fact code detail input message = SC.mkFinding SC.DesignDomain BlockOnPr request fact.Source code detail Blocking input message
    let private excluded fact = fact.IsPureParserOrValidator || fact.IsThinOneShotAdapter || match fact.Exemption with ActiveExemption _ -> true | _ -> false
    let private delivery request fact =
        match fact.EdgeInterpreter, fact.Delivery with
        | None, _ -> [ emit request fact "fsharp.effect-edge-missing" fact.Symbol false "stateful workflow declares effects but no edge interpreter" ]
        | Some _, None -> [ emit request fact "fsharp.effect-delivery-missing" fact.Symbol false "edge interpreter must declare success/failure messages plus retry and idempotency semantics" ]
        | Some _, Some d ->
            [ if d.SuccessMessage.IsNone || d.FailureMessage.IsNone then yield emit request fact "fsharp.effect-result-message-missing" fact.Symbol false "edge interpreter must return explicit success and failure messages into the transition"
              if d.RetryPolicy.IsNone then yield emit request fact "fsharp.effect-retry-missing" fact.Symbol false "repeatable effect lacks visible retry policy"
              if d.Idempotency.IsNone then yield emit request fact "fsharp.effect-idempotency-missing" fact.Symbol false "repeatable effect lacks visible idempotency semantics" ]
    let private findings request fact =
        match fact.Exemption with
        | InvalidExemption reason -> [ emit request fact "fsharp.effect-exemption-invalid" fact.Symbol true (sprintf "effect-boundary exemption is invalid: %s" reason) ]
        | _ when not fact.IsStatefulWorkflow || excluded fact -> []
        | _ ->
            let direct = fact.DirectEffects |> List.map (fun category -> emit request fact "fsharp.effect-in-transition" (names category) false (sprintf "declared pure transition '%s' directly performs %s; emit an effect for its edge interpreter" fact.Symbol (names category)))
            let hidden = [ if fact.CallbackHiddenState then yield emit request fact "fsharp.callback-hidden-state" fact.Symbol false "stateful workflow hides continuation state in a callback; model messages and requested effects explicitly"
                           if fact.ExceptionDrivenContinuation then yield emit request fact "fsharp.exception-continuation" fact.Symbol false "stateful workflow uses exception-driven continuation; return explicit failure messages into the transition" ]
            direct @ hidden @ (if List.isEmpty fact.DirectEffects then [] else delivery request fact)
    let evaluate request boundaries = boundaries |> List.collect (findings request) |> List.sortBy (fun f -> f.Code, f.Location.File, f.Location.Detail)
