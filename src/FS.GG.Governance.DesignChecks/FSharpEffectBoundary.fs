namespace FS.GG.Governance.DesignChecks

open System
open System.IO
open System.Globalization
open System.Text.RegularExpressions
open System.Xml.Linq
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement

module SC = FS.GG.Governance.SurfaceChecks.Model
module CP = FS.GG.Governance.SurfaceChecks.Profile

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module FSharpEffectBoundary =
    type EffectCategory = Filesystem | Process | Environment | ClockOrRandomness | Network | UiOrHost | Persistence | MutableGlobalState
    type EffectCall = { Category: EffectCategory; Symbol: string; Line: int; Column: int }
    type Delivery = { SuccessMessage: string option; FailureMessage: string option; RetryPolicy: string option; Idempotency: string option }
    type Exemption = NoExemption | ActiveExemption of owner: string * rationale: string * reviewBy: DateOnly | InvalidExemption of reason: string
    type BoundaryFacts =
        { Project: string; Symbol: string; Source: GovernedPath; IsStatefulWorkflow: bool; IsPureParserOrValidator: bool
          IsThinOneShotAdapter: bool; DirectEffects: EffectCall list; CallbackHiddenState: bool
          ExceptionDrivenContinuation: bool; EdgeInterpreter: string option; Delivery: Delivery option; Exemption: Exemption }

    let private names = function
        | Filesystem -> "filesystem" | Process -> "process" | Environment -> "environment" | ClockOrRandomness -> "clock-randomness"
        | Network -> "network" | UiOrHost -> "ui-host" | Persistence -> "persistence" | MutableGlobalState -> "mutable-global-state"

    let private executableText (text: string) =
        let visible = text.ToCharArray()
        let mask index = if visible.[index] <> '\n' then visible.[index] <- ' '
        let mutable index = 0
        let startsWith (value: string) =
            index + value.Length <= text.Length
            && String.CompareOrdinal(text, index, value, 0, value.Length) = 0
        let stringStart () =
            if startsWith "$@\"\"\"" || startsWith "@$\"\"\"" then Some(2, 3, false, true)
            elif startsWith "$\"\"\"" then Some(1, 3, false, true)
            elif startsWith "$@\"" || startsWith "@$\"" then Some(2, 1, true, true)
            elif startsWith "$\"" then Some(1, 1, false, true)
            elif startsWith "@\"" then Some(1, 1, true, false)
            elif startsWith "\"\"\"" then Some(0, 3, false, false)
            elif text.[index] = '"' then Some(0, 1, false, false)
            else None
        let rec scanCode stopAtInterpolationClose =
            let mutable stopped = false
            let mutable braceDepth = 0
            while index < text.Length && not stopped do
                if stopAtInterpolationClose && text.[index] = '}' then
                    if braceDepth = 0 then
                        mask index
                        index <- index + 1
                        stopped <- true
                    else
                        braceDepth <- braceDepth - 1
                        index <- index + 1
                elif stopAtInterpolationClose && text.[index] = '{' then
                    braceDepth <- braceDepth + 1
                    index <- index + 1
                elif index + 1 < text.Length && text.[index] = '/' && text.[index + 1] = '/' then
                    while index < text.Length && text.[index] <> '\n' do mask index; index <- index + 1
                elif index + 1 < text.Length && text.[index] = '(' && text.[index + 1] = '*' then
                    let mutable depth = 0
                    while index < text.Length && (depth > 0 || (index + 1 < text.Length && text.[index] = '(' && text.[index + 1] = '*')) do
                        if index + 1 < text.Length && text.[index] = '(' && text.[index + 1] = '*' then
                            mask index; mask (index + 1); depth <- depth + 1; index <- index + 2
                        elif index + 1 < text.Length && text.[index] = '*' && text.[index + 1] = ')' then
                            mask index; mask (index + 1); depth <- depth - 1; index <- index + 2
                        else mask index; index <- index + 1
                else
                    match stringStart () with
                    | Some(prefixLength, quoteLength, verbatim, interpolated) ->
                        scanString prefixLength quoteLength verbatim interpolated
                    | None when text.[index] = '\''
                                && ((index + 2 < text.Length && text.[index + 2] = '\'')
                                    || (index + 3 < text.Length && text.[index + 1] = '\\' && text.[index + 3] = '\'')) ->
                        mask index; index <- index + 1
                        let mutable closed = false
                        while index < text.Length && not closed && text.[index] <> '\n' do
                            if text.[index] = '\\' && index + 1 < text.Length then mask index; mask (index + 1); index <- index + 2
                            elif text.[index] = '\'' then mask index; index <- index + 1; closed <- true
                            else mask index; index <- index + 1
                    | None -> index <- index + 1
        and scanString prefixLength quoteLength verbatim interpolated =
            let opening = prefixLength + quoteLength
            for offset in 0 .. opening - 1 do mask (index + offset)
            index <- index + opening
            let mutable closed = false
            while index < text.Length && not closed do
                if quoteLength = 3 && startsWith "\"\"\"" then
                    mask index; mask (index + 1); mask (index + 2); index <- index + 3; closed <- true
                elif quoteLength = 1 && text.[index] = '"' then
                    if verbatim && index + 1 < text.Length && text.[index + 1] = '"' then
                        mask index; mask (index + 1); index <- index + 2
                    else mask index; index <- index + 1; closed <- true
                elif not verbatim && text.[index] = '\\' && index + 1 < text.Length then
                    mask index; mask (index + 1); index <- index + 2
                elif interpolated && text.[index] = '{' then
                    if index + 1 < text.Length && text.[index + 1] = '{' then
                        mask index; mask (index + 1); index <- index + 2
                    else
                        mask index
                        index <- index + 1
                        scanCode true
                elif interpolated && index + 1 < text.Length && text.[index] = '}' && text.[index + 1] = '}' then
                    mask index; mask (index + 1); index <- index + 2
                else mask index; index <- index + 1
        scanCode false
        String visible

    let private effectPatterns =
        let token = "(?<![A-Za-z0-9_'.])"
        [ Filesystem, token + "(?<symbol>(?:System\\.IO\\.)?(?:File|Directory)\\.[A-Za-z_][A-Za-z0-9_']*)\\s*\\("
          Filesystem, token + "(?<symbol>(?:System\\.IO\\.)?FileStream)\\s*\\("
          Process, token + "(?<symbol>(?:System\\.Diagnostics\\.)?Process\\.Start)\\s*\\("
          Environment, token + "(?<symbol>(?:System\\.)?Environment\\.(?:GetEnvironmentVariable|SetEnvironmentVariable|Exit))\\s*\\("
          ClockOrRandomness, token + "(?<symbol>(?:System\\.)?DateTime(?:Offset)?\\.(?:Now|UtcNow))\\b"
          ClockOrRandomness, token + "(?<symbol>(?:System\\.)?Random)\\s*\\("
          Network, token + "(?<symbol>(?:System\\.Net\\.Http\\.)?HttpClient)\\s*\\("
          Network, token + "(?<symbol>(?:System\\.Net\\.)?WebRequest\\.Create)\\s*\\("
          Network, token + "(?<symbol>(?:System\\.Net\\.Sockets\\.)?Socket)\\s*\\("
          UiOrHost, token + "(?<symbol>(?:System\\.)?Console\\.[A-Za-z_][A-Za-z0-9_']*)\\s*\\("
          UiOrHost, token + "(?<symbol>(?:Application|Window)\\.[A-Za-z_][A-Za-z0-9_']*)\\s*\\("
          Persistence, token + "(?<symbol>[A-Za-z_][A-Za-z0-9_']*\\.(?:SaveChanges|SaveChangesAsync))\\s*\\("
          Persistence, token + "(?<symbol>(?:System\\.Text\\.Json\\.)?JsonSerializer\\.Serialize)\\s*\\("
          MutableGlobalState, token + "(?<symbol>let\\s+mutable)\\b" ]

    let private classify startLine (text: string) =
        let visible = executableText text
        effectPatterns
        |> List.collect (fun (category, pattern) ->
            Regex.Matches(visible, pattern)
            |> Seq.cast<Match>
            |> Seq.map (fun matched ->
                let before = visible.Substring(0, matched.Index)
                let relativeLine = before |> Seq.filter ((=) '\n') |> Seq.length
                let lastNewline = before.LastIndexOf('\n')
                { Category = category
                  Symbol = matched.Groups.["symbol"].Value
                  Line = startLine + relativeLine + 1
                  Column = matched.Index - lastNewline })
            |> Seq.toList)
        |> List.sortBy (fun call -> call.Line, call.Column, names call.Category, call.Symbol)

    type private Binding =
        { Symbol: string
          StartLine: int
          EndLine: int
          Body: string }

    type private Declaration =
        { Symbol: string
          Line: int
          Options: Map<string, string> }

    let private markerPrefix = "// fsgg:effect-boundary"
    let private symbolPattern = "[A-Za-z_][A-Za-z0-9_']*"
    let private declarationPattern =
        "^//\\s*fsgg:effect-boundary\\s+(?<symbol>" + symbolPattern + ")(?<options>(?:\\s+[a-z][a-z-]*=(?:\"[^\"\\r\\n]+\"|[^\\s\"]+))*)\\s*$"
    let private optionPattern = "(?<key>[a-z][a-z-]*)=(?:\"(?<quoted>[^\"\\r\\n]+)\"|(?<plain>[^\\s\"]+))"
    let private bindingPattern =
        "^(?<indent>[ \\t]*)let\\s+(?:(?:inline|rec|private|internal|mutable)\\s+)*(?<symbol>" + symbolPattern + ")(?=\\s|\\(|=|:)"
    let private declarationBoundaryPattern = "^(?:let|and|type|module|namespace|exception|open)\\b|^\\[<"
    let private allowedOptions =
        Set.ofList
            [ "kind"; "edge"; "success"; "failure"; "retry"; "idempotency"
              "exemption-owner"; "exemption-rationale"; "exemption-review-by" ]

    let private indentation (line: string) =
        line |> Seq.takeWhile (fun c -> c = ' ' || c = '\t') |> Seq.sumBy (fun c -> if c = '\t' then 4 else 1)

    let private isBoundary (line: string) =
        Regex.IsMatch(line.TrimStart(), declarationBoundaryPattern)

    let private bindings (lines: string array) =
        lines
        |> Array.mapi (fun index line -> index, Regex.Match(line, bindingPattern))
        |> Array.choose (fun (index, binding) -> if binding.Success then Some(index, binding) else None)
        |> Array.map (fun (index, binding) ->
            let indent = indentation lines.[index]
            let finish =
                [ index + 1 .. lines.Length - 1 ]
                |> List.tryFind (fun candidate ->
                    not (String.IsNullOrWhiteSpace lines.[candidate])
                    && indentation lines.[candidate] <= indent
                    && isBoundary lines.[candidate])
                |> Option.defaultValue lines.Length
            { Symbol = binding.Groups.["symbol"].Value
              StartLine = index
              EndLine = finish
              Body = lines.[index .. finish - 1] |> String.concat "\n" })
        |> Array.toList

    let private parseDeclaration source lineNumber (line: string) =
        let trimmed = line.Trim()
        let declaration = Regex.Match(trimmed, declarationPattern)
        if not declaration.Success then
            Error(sprintf "%s:%d has a malformed effect-boundary declaration" source lineNumber)
        else
            let pairs =
                Regex.Matches(declaration.Groups.["options"].Value, optionPattern)
                |> Seq.cast<Match>
                |> Seq.map (fun option ->
                    let value = if option.Groups.["quoted"].Success then option.Groups.["quoted"].Value else option.Groups.["plain"].Value
                    option.Groups.["key"].Value, value)
                |> Seq.toList
            match pairs |> List.groupBy fst |> List.tryFind (fun (_, values) -> values.Length > 1) with
            | Some(key, _) -> Error(sprintf "%s:%d repeats effect-boundary option '%s'" source lineNumber key)
            | None ->
                match pairs |> List.tryFind (fun (key, _) -> not (Set.contains key allowedOptions)) with
                | Some(key, _) -> Error(sprintf "%s:%d has unknown effect-boundary option '%s'" source lineNumber key)
                | None ->
                    Ok
                        { Symbol = declaration.Groups.["symbol"].Value
                          Line = lineNumber
                          Options = pairs |> Map.ofList }

    let private exemption source declaration =
        let option key = Map.tryFind key declaration.Options
        let values =
            [ "exemption-owner", option "exemption-owner"
              "exemption-rationale", option "exemption-rationale"
              "exemption-review-by", option "exemption-review-by" ]
        let present = values |> List.choose (fun (key, value) -> value |> Option.map (fun actual -> key, actual))
        match present with
        | [] -> Ok NoExemption
        | [ (_, owner); (_, rationale); (_, reviewText) ] ->
            match DateOnly.TryParseExact(reviewText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None) with
            | false, _ -> Error(sprintf "%s:%d has invalid exemption-review-by '%s'" source declaration.Line reviewText)
            | true, reviewBy when reviewBy <= DateOnly.FromDateTime(DateTime.UtcNow) -> Ok(InvalidExemption "review date expired")
            | true, reviewBy -> Ok(ActiveExemption(owner, rationale, reviewBy))
        | _ ->
            let missing = values |> List.choose (fun (key, value) -> if value.IsNone then Some key else None)
            Error(sprintf "%s:%d has incomplete exemption options; missing %s" source declaration.Line (String.concat ", " missing))

    let private boundaryFact project source (knownBindings: Binding list) declaration binding =
        let option key = Map.tryFind key declaration.Options
        let kind = option "kind" |> Option.defaultValue "transition"
        let applicability =
            match kind with
            | "transition" -> Ok(false, false)
            | "parser" | "validator" -> Ok(true, false)
            | "thin-adapter" -> Ok(false, true)
            | invalid -> Error(sprintf "%s:%d has invalid effect-boundary kind '%s'" source declaration.Line invalid)
        match applicability, exemption source declaration with
        | Error reason, _ | _, Error reason -> Error reason
        | Ok(parserOrValidator, thinAdapter), Ok exemptionFact ->
            let visibleBody = executableText binding.Body
            match option "edge" with
            | Some edge when knownBindings |> List.exists (fun candidate -> candidate.Symbol = edge) |> not ->
                Error(sprintf "%s:%d names unresolved edge interpreter '%s'" source declaration.Line edge)
            | edge ->
                let deliveryKeys = [ "success"; "failure"; "retry"; "idempotency" ]
                let delivery =
                    if deliveryKeys |> List.exists (fun key -> Map.containsKey key declaration.Options) then
                        Some
                            { SuccessMessage = option "success"
                              FailureMessage = option "failure"
                              RetryPolicy = option "retry"
                              Idempotency = option "idempotency" }
                    else None
                Ok
                    { Project = project
                      Symbol = declaration.Symbol
                      Source = normalizePath source
                      IsStatefulWorkflow = true
                      IsPureParserOrValidator = parserOrValidator
                      IsThinOneShotAdapter = thinAdapter
                      DirectEffects = classify binding.StartLine binding.Body
                      CallbackHiddenState = visibleBody.Contains("Async.Start", StringComparison.Ordinal) || visibleBody.Contains("ContinueWith", StringComparison.Ordinal)
                      ExceptionDrivenContinuation = Regex.IsMatch(visibleBody, "\\btry\\b") && Regex.IsMatch(visibleBody, "\\bwith\\b")
                      EdgeInterpreter = edge
                      Delivery = delivery
                      Exemption = exemptionFact }

    let private senseSource project source (text: string) =
        let lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
        let knownBindings = bindings lines
        let markerLines =
            lines
            |> Array.mapi (fun index line -> index, line)
            |> Array.filter (fun (_, line) ->
                let trimmed = line.Trim()
                trimmed.StartsWith(markerPrefix, StringComparison.Ordinal)
                && (trimmed.Length = markerPrefix.Length || Char.IsWhiteSpace trimmed.[markerPrefix.Length]))
            |> Array.toList
        let parseMarker (index, line) =
            match parseDeclaration source (index + 1) line with
            | Error reason -> Error reason
            | Ok declaration ->
                let bindingLine =
                    [ index + 1 .. lines.Length - 1 ]
                    |> List.tryFind (fun candidate -> not (String.IsNullOrWhiteSpace lines.[candidate]))
                match bindingLine with
                | None -> Error(sprintf "%s:%d declaration '%s' has no following symbol" source declaration.Line declaration.Symbol)
                | Some candidate ->
                    match knownBindings |> List.tryFind (fun binding -> binding.StartLine = candidate && binding.Symbol = declaration.Symbol) with
                    | None -> Error(sprintf "%s:%d declaration '%s' does not bind the immediately following same-named let" source declaration.Line declaration.Symbol)
                    | Some binding -> boundaryFact project source knownBindings declaration binding
        ((Ok [], markerLines)
         ||> List.fold (fun state marker ->
             match state, parseMarker marker with
             | Error reason, _ | _, Error reason -> Error reason
             | Ok facts, Ok fact -> Ok(fact :: facts)))
        |> Result.map List.rev

    let senseProject root project =
        try
            let path = Path.Combine(root, project)
            if not (File.Exists path) then Error(sprintf "F# project was not found: %s" project) else
            let dir = Path.GetDirectoryName(path) |> Option.ofObj |> Option.defaultValue root
            let sources =
                XDocument.Load(path).Descendants(XName.Get "Compile")
                |> Seq.choose (fun node -> node.Attribute(XName.Get "Include") |> Option.ofObj |> Option.map (fun a -> a.Value))
                |> Seq.filter (fun source -> source.EndsWith(".fs", StringComparison.OrdinalIgnoreCase) && not (source.EndsWith(".fsi", StringComparison.OrdinalIgnoreCase)))
                |> Seq.toList
            ((Ok [], sources)
             ||> List.fold (fun state source ->
                 match state with
                 | Error reason -> Error reason
                 | Ok facts ->
                     let full = Path.Combine(dir, source)
                     if not (File.Exists full) then Error(sprintf "compiled F# source was not found: %s" source)
                     else
                         match senseSource project source (File.ReadAllText full) with
                         | Error reason -> Error reason
                         | Ok sensed -> Ok(facts @ sensed)))
        with ex -> Error(sprintf "unable to sense F# effect boundaries for '%s': %s" project ex.Message)

    // #385: this pack's maturity is READ from the composed F# constitution profile
    // (`SurfaceChecks.Profile`, docs/decisions/0012), not restated here. It still evaluates to
    // `BlockOnPr` — #369's shipped posture is preserved — but the profile now records WHY this pack
    // blocks while #366's warns (applicability: this one fires only on an opted-in transition), so
    // the difference is a decision a reader can find rather than an accident of two emit sites.
    let private packMaturity = CP.declaredMaturity CP.EffectBoundary
    let private emit request fact code detail input message = SC.mkFinding SC.DesignDomain packMaturity request fact.Source code detail Blocking input message
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
            let direct = fact.DirectEffects |> List.map (fun call -> emit request fact "fsharp.effect-in-transition" (sprintf "%s %s@%d:%d" (names call.Category) call.Symbol call.Line call.Column) false (sprintf "declared pure transition '%s' directly calls %s at %d:%d (%s); emit an effect for its edge interpreter" fact.Symbol call.Symbol call.Line call.Column (names call.Category)))
            let hidden = [ if fact.CallbackHiddenState then yield emit request fact "fsharp.callback-hidden-state" fact.Symbol false "stateful workflow hides continuation state in a callback; model messages and requested effects explicitly"
                           if fact.ExceptionDrivenContinuation then yield emit request fact "fsharp.exception-continuation" fact.Symbol false "stateful workflow uses exception-driven continuation; return explicit failure messages into the transition" ]
            let deliveryContract =
                if not (List.isEmpty fact.DirectEffects) || fact.EdgeInterpreter.IsSome || fact.Delivery.IsSome then delivery request fact
                else []
            direct @ hidden @ deliveryContract
    let evaluate request boundaries = boundaries |> List.collect (findings request) |> List.sortBy (fun f -> f.Code, f.Location.File, f.Location.Detail)
