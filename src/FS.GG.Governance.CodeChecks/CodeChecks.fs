namespace FS.GG.Governance.CodeChecks

open System
open System.Security.Cryptography
open System.Text
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.Symbols
open FSharp.Compiler.Text
open FSharp.Compiler.Tokenization
open FS.GG.Governance.CodeChecks.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CodeChecks =

    let sourceDigest (source: string) =
        source |> Encoding.UTF8.GetBytes |> SHA256.HashData |> Convert.ToHexString |> fun s -> s.ToLowerInvariant()

    let checker = FSharpChecker.Create(keepAssemblyContents = true)

    let normalizePath (path: string) = path.Replace('\\', '/').TrimStart('.', '/')

    let rangeOf (r: FSharp.Compiler.Text.Range) =
        { StartLine = r.StartLine
          StartColumn = r.StartColumn
          EndLine = r.EndLine
          EndColumn = r.EndColumn }

    let safeSymbolName (symbol: FSharpSymbol) =
        try
            if String.IsNullOrWhiteSpace symbol.FullName then symbol.DisplayName else symbol.FullName
        with _ -> symbol.DisplayName

    let entityName (entity: FSharpEntity) = defaultArg entity.TryFullName entity.DisplayName

    let isPurePath prefixes path =
        let p = normalizePath path
        prefixes |> List.exists (fun prefix -> p.StartsWith(normalizePath prefix, StringComparison.Ordinal))

    let disposition request digest path symbol =
        let matches =
            request.Justifications
            |> List.filter (fun j -> normalizePath j.Path = normalizePath path && j.Symbol = symbol)

        match matches with
        | [] -> Missing
        | [ j ] when String.IsNullOrWhiteSpace j.SimplerAlternative || String.IsNullOrWhiteSpace j.Evidence -> Invalid
        | [ j ] when j.Head <> request.Head -> StaleHead
        | [ j ] when not (String.Equals(j.SourceDigest, digest, StringComparison.OrdinalIgnoreCase)) -> StaleSource
        | [ _ ] -> NotApplicable // current: caller suppresses the candidate
        | _ -> Invalid

    let candidate request digest id path symbol range message =
        let d = disposition request digest path symbol
        if d = NotApplicable then None
        else
            Some
                { Id = id
                  Category = ComplexityRequiresJustification
                  Path = normalizePath path
                  Symbol = symbol
                  Range = range
                  Justification = d
                  Message = message }

    let prohibited id path symbol range message =
        { Id = id
          Category = ProhibitedStructure
          Path = normalizePath path
          Symbol = symbol
          Range = range
          Justification = NotApplicable
          Message = message }

    let thresholdFinding request digest threshold measured id path symbol range subject =
        match threshold with
        | Some limit when limit >= 0 && measured > limit ->
            candidate request digest id path symbol range
                (sprintf "%s measures %d, above the configured review trigger %d; simplify it or bind measured justification to this symbol and source." subject measured limit)
        | _ -> None

    let tokenRanges (path: string) (source: string) (tokenNames: Set<string>) =
        let tokenizer = FSharpSourceTokenizer([], Some path, None, None)
        source.Replace("\r\n", "\n").Split('\n')
        |> Array.mapi (fun index line ->
            let lineTokenizer = tokenizer.CreateLineTokenizer line
            let rec scan state acc =
                match lineTokenizer.ScanToken state with
                | None, _ -> List.rev acc
                | Some token, next ->
                    let acc' = if Set.contains token.TokenName tokenNames then (index + 1, token.LeftColumn, token.RightColumn + 1, token.TokenName) :: acc else acc
                    scan next acc'
            scan FSharpTokenizerLexState.Initial [])
        |> Array.toList
        |> List.concat

    let symbolDependencies (uses: FSharpSymbolUse array) =
        uses
        |> Array.filter (fun u -> not u.IsFromDefinition)
        |> Array.map (fun u ->
            let n = safeSymbolName u.Symbol
            let parts = n.Split('.')
            if parts.Length >= 2 then parts.[0] + "." + parts.[1] else n)
        |> Array.filter (String.IsNullOrWhiteSpace >> not)
        |> Array.distinct
        |> Array.sort

    let reflectionNamespaces =
        [ "System.Reflection"; "System.Linq.Expressions"; "Microsoft.FSharp.Quotations" ]

    let hasReflectionOwner (name: string) =
        name = "System.Type"
        || name.StartsWith("System.Type.", StringComparison.Ordinal)
        || (reflectionNamespaces |> List.exists (fun prefix -> name = prefix || name.StartsWith(prefix + ".", StringComparison.Ordinal)))

    let isReflectionSymbol (symbol: FSharpSymbol) =
        match symbol with
        | :? FSharpEntity as entity ->
            // An `open System.Reflection` produces namespace entity uses. Namespace presence is not
            // executable reflection; a referenced reflection type is.
            not entity.IsNamespace && hasReflectionOwner (entityName entity)
        | :? FSharpMemberOrFunctionOrValue as memberOrValue ->
            // Resolved member full names carry their semantic owner (`System.Type.GetMethods`,
            // `System.Reflection.Assembly.Load`, …). Avoid logical-parent access: FCS correctly has
            // none for local values and throws when that optional relation is queried.
            hasReflectionOwner (safeSymbolName memberOrValue)
        | other -> hasReflectionOwner (safeSymbolName other)

    let positionLe line column otherLine otherColumn =
        line < otherLine || (line = otherLine && column <= otherColumn)

    let rangeContains (outer: FSharp.Compiler.Text.Range) (inner: FSharp.Compiler.Text.Range) =
        positionLe outer.StartLine outer.StartColumn inner.StartLine inner.StartColumn
        && positionLe inner.EndLine inner.EndColumn outer.EndLine outer.EndColumn

    let analyzeDocument request (doc: SourceDocument) = async {
        let path = normalizePath doc.Path
        let digest = sourceDigest doc.Source
        let text = SourceText.ofString doc.Source
        let! options, optionDiagnostics =
            checker.GetProjectOptionsFromScript(path + ".fsx", text, assumeDotNetFramework = false, useSdkRefs = true)
        let! parsed, checkedAnswer = checker.ParseAndCheckFileInProject(path + ".fsx", 0, text, options)
        let optionMessages = optionDiagnostics |> Seq.map _.Message |> Seq.toList
        let parseErrors = parsed.Diagnostics |> Array.filter (fun d -> d.Severity = FSharpDiagnosticSeverity.Error)

        match checkedAnswer with
        | FSharpCheckFileAnswer.Aborted ->
            let r = { StartLine = 1; StartColumn = 0; EndLine = 1; EndColumn = 0 }
            return [ prohibited CompilerAnalysisFailed path path r "F# compiler analysis aborted; the gate cannot report a clean result." ], optionMessages @ [ "compiler analysis aborted: " + path ]
        | FSharpCheckFileAnswer.Succeeded check when parseErrors.Length > 0 || (check.Diagnostics |> Array.exists (fun d -> d.Severity = FSharpDiagnosticSeverity.Error)) ->
            let errors =
                Array.append parsed.Diagnostics check.Diagnostics
                |> Array.filter (fun d -> d.Severity = FSharpDiagnosticSeverity.Error)
                |> Array.map (fun d -> sprintf "%s(%d,%d): %s" path d.StartLine d.StartColumn d.Message)
                |> Array.distinct |> Array.sort |> Array.toList
            let first = errors |> List.tryHead |> Option.defaultValue ("compiler analysis failed: " + path)
            let r = { StartLine = 1; StartColumn = 0; EndLine = 1; EndColumn = 0 }
            return [ prohibited CompilerAnalysisFailed path path r first ], optionMessages @ errors
        | FSharpCheckFileAnswer.Succeeded check ->
            let uses = check.GetAllUsesOfAllSymbolsInFile() |> Seq.toArray
            let openRanges = check.OpenDeclarations |> Seq.choose _.Range |> Seq.toArray
            let lineCount = doc.Source.Replace("\r\n", "\n").Split('\n').Length
            let fullRange = { StartLine = 1; StartColumn = 0; EndLine = lineCount; EndColumn = 0 }
            let findings = ResizeArray<ArchitectureFinding>()
            let definitionSymbols = ResizeArray<string * int * bool>() // symbol, start line, is type

            let addCandidate id symbol range message =
                candidate request digest id path symbol range message |> Option.iter findings.Add

            let rec walkEntity (entity: FSharpEntity) =
                let symbol = entityName entity
                let er = rangeOf entity.DeclarationLocation
                if not entity.IsFSharpModule then
                    definitionSymbols.Add(symbol, entity.DeclarationLocation.StartLine, true)
                if entity.IsClass && entity.Accessibility.IsPublic && not entity.IsFSharpRecord && not entity.IsFSharpUnion then
                    addCandidate PublicClassShape symbol er "Public class shape requires evidence that a record, DU, or module cannot express the contract."
                if entity.IsAbstractClass then
                    addCandidate AbstractClassHierarchy symbol er "Abstract class hierarchy requires a bounded interoperability or measured-complexity justification."
                match entity.BaseType with
                | Some baseType when entity.IsClass && baseType.HasTypeDefinition && entityName baseType.TypeDefinition <> "obj" && not ((entityName baseType.TypeDefinition).StartsWith("System.Object", StringComparison.Ordinal)) ->
                    addCandidate InheritanceHierarchy symbol er "Inheritance hierarchy detected from compiler base-type facts; prefer a DU/module or justify the exception."
                | _ -> ()
                entity.MembersFunctionsAndValues
                |> Seq.filter (fun m -> not m.IsCompilerGenerated && not m.IsImplicitConstructor)
                |> Seq.iter (fun m -> definitionSymbols.Add(safeSymbolName m, m.DeclarationLocation.StartLine, false))
                entity.NestedEntities |> Seq.iter walkEntity

            check.PartialAssemblySignature.Entities |> Seq.iter walkEntity

            uses
            |> Array.filter _.IsFromDefinition
            |> Array.choose (fun u ->
                match u.Symbol with
                | :? FSharpMemberOrFunctionOrValue as m when m.IsMutable -> Some(m, u)
                | _ -> None)
            |> Array.distinctBy (fun (m, _) -> safeSymbolName m, m.DeclarationLocation.StartLine, m.DeclarationLocation.StartColumn)
            |> Array.iter (fun (m, u) ->
                let symbol = safeSymbolName m
                if m.IsModuleValueOrMember then
                    addCandidate SharedMutableState symbol (rangeOf u.Range) "Module/global mutable state requires a simpler-local-state or measured/interoperability justification."
                elif isPurePath request.PureDomainPrefixes path then
                    addCandidate ImperativeInPureDomain symbol (rangeOf u.Range) "Local mutation in a declared pure domain requires measured evidence or relocation to an imperative seam.")

            uses
            |> Array.filter (fun u -> not u.IsFromDefinition)
            |> Array.filter (fun u -> openRanges |> Array.exists (fun openRange -> rangeContains openRange u.Range) |> not)
            |> Array.filter (fun u -> isReflectionSymbol u.Symbol)
            |> Array.distinctBy (fun u -> safeSymbolName u.Symbol, u.Range.StartLine, u.Range.StartColumn)
            |> Array.iter (fun u ->
                let symbol = safeSymbolName u.Symbol
                addCandidate ReflectionOrMetaprogramming symbol (rangeOf u.Range) "Reflection, quotations, or expression-tree use requires an interoperability or measured justification.")

            if isPurePath request.PureDomainPrefixes path then
                tokenRanges path doc.Source (set [ "FOR"; "WHILE" ])
                |> List.iter (fun (line, left, right, token) ->
                    addCandidate ImperativeInPureDomain (path + ":" + string line + ":" + token)
                        { StartLine = line; StartColumn = left; EndLine = line; EndColumn = right }
                        "Compiler-tokenized imperative loop in a declared pure domain requires measured evidence or relocation to an imperative seam.")

            thresholdFinding request digest request.Thresholds.ModuleLines lineCount ModuleSizeReview path path fullRange "Module"
            |> Option.iter findings.Add

            let orderedDefs = definitionSymbols |> Seq.distinct |> Seq.sortBy (fun (_, line, isType) -> line, not isType) |> Seq.toArray
            orderedDefs
            |> Array.iteri (fun i (symbol, startLine, isType) ->
                let nextLine = if i + 1 < orderedDefs.Length then let _, line, _ = orderedDefs.[i + 1] in line else lineCount + 1
                let endLine = max startLine (nextLine - 1)
                let measured = endLine - startLine + 1
                let id, threshold, subject =
                    if isType then TypeSizeReview, request.Thresholds.TypeLines, "Type region"
                    else MemberSizeReview, request.Thresholds.MemberLines, "Member region"
                thresholdFinding request digest threshold measured id path symbol
                    { StartLine = startLine; StartColumn = 0; EndLine = endLine; EndColumn = 0 } subject
                |> Option.iter findings.Add)

            let dependencies = symbolDependencies uses
            thresholdFinding request digest request.Thresholds.DependencyFanOut dependencies.Length DependencyFanOutReview path path fullRange "Dependency fan-out"
            |> Option.iter findings.Add

            let declared =
                uses |> Array.filter _.IsFromDefinition |> Array.map (fun u -> safeSymbolName u.Symbol) |> Set.ofArray
            request.ApprovedPrimitives
            |> List.iter (fun primitive ->
                primitive.CandidateSymbols
                |> List.filter (fun symbol -> Set.contains symbol declared)
                |> List.iter (fun symbol ->
                    findings.Add(
                        prohibited DuplicateHomeGrownAbstraction path symbol fullRange
                            (sprintf "Capability '%s' already declares approved primitive(s) [%s]; replace duplicate '%s' or change the capability declaration."
                                primitive.Capability (String.concat ", " primitive.ApprovedSymbols) symbol))))

            let warnings =
                check.Diagnostics
                |> Array.filter (fun d -> d.Severity <> FSharpDiagnosticSeverity.Error)
                |> Array.map (fun d -> sprintf "%s(%d,%d): %s" path d.StartLine d.StartColumn d.Message)
                |> Array.toList
            return findings |> Seq.toList, optionMessages @ warnings
    }

    let sortFindings findings =
        findings
        |> List.distinct
        |> List.sortWith (fun a b ->
            compare
                (a.Path, a.Range.StartLine, a.Range.StartColumn, findingIdToken a.Id, a.Symbol)
                (b.Path, b.Range.StartLine, b.Range.StartColumn, findingIdToken b.Id, b.Symbol))

    let analyze request = async {
        let! results =
            request.Documents
            |> List.filter (fun d -> not d.IsGenerated)
            |> List.sortBy (fun d -> normalizePath d.Path)
            |> List.map (analyzeDocument request)
            |> Async.Sequential
        return
            { Findings = results |> Array.toList |> List.collect fst |> sortFindings
              Diagnostics = results |> Array.toList |> List.collect snd |> List.distinct |> List.sort }
    }
