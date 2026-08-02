namespace FS.GG.Governance.DesignChecks

open System
open System.IO
open System.Xml.Linq
open System.Text.RegularExpressions
open System.Security.Cryptography
open System.Text
open System.Text.Json
open System.Diagnostics
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Config.FSharpSurfacePolicy
open FS.GG.Governance.Enforcement.Enforcement

module SC = FS.GG.Governance.SurfaceChecks.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module FSharpSurface =

    type SignatureDeclaration =
        { Name: string
          HasXmlDocumentation: bool }

    type Exemption =
        | NoExemption
        | ActiveExemption of owner: string * rationale: string * reviewBy: string
        | InvalidExemption of reason: string

    type ModuleFacts =
        { Project: string
          Source: GovernedPath
          Signature: GovernedPath option
          SourceCompileIndex: int
          SignatureCompileIndex: int option
          IsTestProject: bool
          IsExplicitlyInternal: bool
          IsEntryPoint: bool
          IsGenerated: bool
          Exemption: Exemption
          Declarations: SignatureDeclaration list
          SignatureMatchesSource: bool
          RequiresSurfaceBaseline: bool
          SurfaceBaselineCurrent: bool }

    type ReceiptFinding =
        { Code: string; File: string; Detail: string; IsInputState: bool
          BaseSeverity: string; EffectiveSeverity: string; Evidence: string option }
    type Receipt =
        { SchemaVersion: int
          Kind: string
          Applicability: string
          Applicable: bool
          ApplicabilityReason: string
          Project: string
          DeclaredGlob: string
          CompiledSources: string list
          MatchedModules: string list
          MatchedModuleCount: int
          Cardinality: string
          Maturity: string
          Findings: ReceiptFinding list
          FreshnessDigest: string option
          ConfigDigest: string option
          PolicyDigest: string option
          SourceDigest: string option
          Malformed: string option }

    let migrationMaturity = Warn

    let mkFinding request source code detail isInput message =
        SC.mkFinding SC.DesignDomain migrationMaturity request source code detail Blocking isInput message

    let exempt moduleFacts =
        moduleFacts.IsTestProject
        || moduleFacts.IsExplicitlyInternal
        || moduleFacts.IsEntryPoint
        || moduleFacts.IsGenerated
        || match moduleFacts.Exemption with
           | ActiveExemption _ -> true
           | _ -> false

    let moduleFindings request moduleFacts =
        if moduleFacts.IsTestProject then
            []
        else
            let missingSignature =
                match moduleFacts.Signature with
                | None when not (exempt moduleFacts) ->
                    [ mkFinding request moduleFacts.Source "fsharp.signature-missing" moduleFacts.Project false
                          "compiled public-by-default module has no curated .fsi; add a minimal signature or make the module explicitly internal" ]
                | _ -> []

            let exemptionFindings =
                match moduleFacts.Exemption with
                | InvalidExemption reason ->
                    [ mkFinding request moduleFacts.Source "fsharp.exemption-invalid" moduleFacts.Project true
                          (sprintf "governed exemption is incomplete or expired: %s" reason) ]
                | _ -> []

            let orderFindings =
                match moduleFacts.Signature, moduleFacts.SignatureCompileIndex with
                | Some signature, Some index when index <> moduleFacts.SourceCompileIndex - 1 ->
                    [ mkFinding request signature "fsharp.signature-compile-order" moduleFacts.Project false
                          "signature must be compiled immediately before its implementation in the project file" ]
                | Some _, None ->
                    [ mkFinding request moduleFacts.Source "fsharp.signature-compile-order" moduleFacts.Project true
                          "signature is not a compiled project item immediately before its implementation" ]
                | _ -> []

            let documentationFindings =
                moduleFacts.Declarations
                |> List.choose (fun declaration ->
                    if declaration.HasXmlDocumentation then None
                    else
                        Some(mkFinding request moduleFacts.Source "fsharp.signature-docs" declaration.Name false
                            (sprintf "public signature declaration '%s' lacks XML documentation; document behavior, invariants, failures, units, or compatibility as applicable" declaration.Name)))

            let mismatchFindings =
                match moduleFacts.Signature with
                | Some _ when not moduleFacts.SignatureMatchesSource ->
                    [ mkFinding request moduleFacts.Source "fsharp.signature-source-mismatch" moduleFacts.Project false
                          "signature does not match the implementation; correct the curated contract rather than exposing implementation helpers" ]
                | _ -> []

            let baselineFindings =
                if moduleFacts.RequiresSurfaceBaseline && not moduleFacts.SurfaceBaselineCurrent then
                    [ mkFinding request moduleFacts.Source "fsharp.surface-baseline-stale" moduleFacts.Project false
                          "package or tool-facing public-surface baseline is stale; refresh and review the intentional contract change" ]
                else []

            [ missingSignature; exemptionFindings; orderFindings; documentationFindings; mismatchFindings; baselineFindings ]
            |> List.concat

    let evaluate request modules =
        modules
        |> List.collect (moduleFindings request)
        |> List.sortBy (fun finding -> finding.Code, finding.Location.File, finding.Location.Detail)

    let private fileName (path: string) = path.Replace('\\', '/')

    let private isGeneratedPath (path: string) =
        let name = path.Replace('\\', '/').Split('/') |> Array.last
        name.EndsWith(".g.fs", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".generated.fs", StringComparison.OrdinalIgnoreCase)
        || path.IndexOf("/obj/", StringComparison.OrdinalIgnoreCase) >= 0

    let private signatureDeclarations (signaturePath: string) =
        let declaration = Regex("^\\s*(?:val|type|module|member|new)\\s+([A-Za-z_][A-Za-z0-9_']*)", RegexOptions.Compiled)
        File.ReadLines signaturePath
        |> Seq.fold (fun (pendingDocs, declarations) line ->
            if line.TrimStart().StartsWith("///", StringComparison.Ordinal) then true, declarations
            else
                let matched = declaration.Match line
                if matched.Success then
                    false, { Name = matched.Groups.[1].Value; HasXmlDocumentation = pendingDocs } :: declarations
                else pendingDocs, declarations) (false, [])
        |> snd
        |> List.rev

    let private sourceDeclarationNames (sourceText: string) =
        Regex.Matches(sourceText, "(?m)^\\s*(?:let|type|module|member)\\s+(?:inline\\s+|rec\\s+|private\\s+|internal\\s+)*(?:``(?<quoted>[^`]+)``|(?<name>[A-Za-z_][A-Za-z0-9_']*))")
        |> Seq.cast<Match>
        |> Seq.map (fun matched ->
            if matched.Groups.["quoted"].Success then matched.Groups.["quoted"].Value
            else matched.Groups.["name"].Value)
        |> Set.ofSeq

    let private compilerMatchesSignature signaturePath sourcePath =
        let sdk =
            let probe = Process.Start(ProcessStartInfo("dotnet", "--list-sdks", RedirectStandardOutput = true, UseShellExecute = false)) |> Option.ofObj |> Option.defaultWith (fun () -> failwith "dotnet SDK probe failed")
            let lines = probe.StandardOutput.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries)
            probe.WaitForExit()
            let line = lines |> Array.last
            let bracket = line.IndexOf('[')
            let version = line.Substring(0, line.IndexOf(' '))
            let root = line.Substring(bracket + 1).TrimEnd(']')
            Path.Combine(root, version, "FSharp", "fsc.dll")
        let output = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dll")
        try
            let info = ProcessStartInfo("dotnet", RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false)
            for argument in [ sdk; "--targetprofile:netcore"; "--target:library"; "--out:" + output; signaturePath; sourcePath ] do
                info.ArgumentList.Add argument
            use compilerProcess = Process.Start info |> Option.ofObj |> Option.defaultWith (fun () -> failwith "F# compiler failed to start")
            compilerProcess.WaitForExit()
            compilerProcess.ExitCode = 0
        finally
            if File.Exists output then File.Delete output

    let private policyFor policy project source fallbackRequires fallbackCurrent =
        match policy with
        | Invalid reason -> InvalidExemption reason, fallbackRequires, fallbackCurrent
        | Missing facts | Loaded facts ->
            let requiresBaseline, baselineCurrent =
                match Map.tryFind project facts.Projects with
                | Some configured -> configured.RequiresBaseline, configured.BaselineCurrent
                | None -> fallbackRequires, fallbackCurrent
            let exemption =
                facts.Exemptions
                |> List.tryFind (fun entry -> String.Equals(entry.Module, source, StringComparison.Ordinal))
                |> Option.map (fun entry ->
                    if entry.ReviewBy >= DateOnly.FromDateTime(DateTime.UtcNow) then
                        ActiveExemption(entry.Owner, entry.Rationale, entry.ReviewBy.ToString("yyyy-MM-dd"))
                    else InvalidExemption("review date has expired"))
                |> Option.defaultValue NoExemption
            exemption, requiresBaseline, baselineCurrent

    /// Edge sensor for SDK-style projects.  It reads the declared Compile order rather than globbing source;
    /// this preserves F# compilation semantics and lets the pure evaluator report exact pairing defects.
    let senseProject root project isTestProject requiresSurfaceBaseline surfaceBaselineCurrent =
        try
            let projectPath = Path.Combine(root, project)
            if not (File.Exists projectPath) then Error(sprintf "F# project was not found: %s" project)
            else
                let document = XDocument.Load projectPath
                let policy = load root
                let projectDir = Path.GetDirectoryName(projectPath) |> Option.ofObj |> Option.defaultValue "."
                let compiled =
                    document.Descendants(XName.Get "Compile")
                    |> Seq.choose (fun node ->
                        match node.Attribute(XName.Get "Include") |> Option.ofObj with
                        | Some attribute -> Some(fileName attribute.Value)
                        | None -> None)
                    |> Seq.toList

                if List.isEmpty compiled then Error(sprintf "F# project declares no explicit Compile items: %s" project)
                else
                    let projectIsExecutable =
                        document.Descendants(XName.Get "OutputType")
                        |> Seq.exists (fun n -> String.Equals(n.Value.Trim(), "Exe", StringComparison.OrdinalIgnoreCase))

                    let facts =
                        compiled
                        |> List.mapi (fun index item -> index, item)
                        |> List.choose (fun (index, source) ->
                            if not (source.EndsWith(".fs", StringComparison.OrdinalIgnoreCase))
                               || source.EndsWith(".fsi", StringComparison.OrdinalIgnoreCase) then None
                            else
                                let signature = source.Substring(0, source.Length - 3) + ".fsi"
                                let signatureIndex = compiled |> List.tryFindIndex ((=) signature)
                                let fullSource = Path.Combine(projectDir, source)
                                let fullSignature = Path.Combine(projectDir, signature)
                                if not (File.Exists fullSource) then
                                    raise (FileNotFoundException(sprintf "compiled source was not found: %s" source))
                                let sourceText = File.ReadAllText fullSource
                                let declarations = if File.Exists fullSignature then signatureDeclarations fullSignature else []
                                let sourceNames = sourceDeclarationNames sourceText
                                let signatureMatchesSource =
                                    if File.Exists fullSignature then
                                        declarations |> List.forall (fun declaration -> Set.contains declaration.Name sourceNames)
                                        && compilerMatchesSignature fullSignature fullSource
                                    else true
                                let exemption, configuredRequiresBaseline, configuredBaselineCurrent =
                                    policyFor policy project source requiresSurfaceBaseline surfaceBaselineCurrent
                                let entry = projectIsExecutable && sourceText.Contains("[<EntryPoint>", StringComparison.Ordinal)
                                Some
                                    { Project = project
                                      Source = normalizePath source
                                      Signature = if File.Exists fullSignature then Some(normalizePath signature) else None
                                      SourceCompileIndex = index
                                      SignatureCompileIndex = signatureIndex
                                      IsTestProject = isTestProject
                                      IsExplicitlyInternal = Regex.IsMatch(sourceText, "^\\s*(?:module|namespace)\\s+internal\\b", RegexOptions.Multiline)
                                      IsEntryPoint = entry
                                      IsGenerated = isGeneratedPath source
                                      Exemption = exemption
                                      Declarations = declarations
                                      SignatureMatchesSource = not (File.Exists fullSignature) || signatureMatchesSource
                                      RequiresSurfaceBaseline = configuredRequiresBaseline
                                      SurfaceBaselineCurrent = configuredBaselineCurrent })
                    Ok facts
        with ex -> Error(sprintf "unable to sense F# project '%s': %s" project ex.Message)

    let private digestFiles root project (facts: ModuleFacts list) =
        let projectDirectory = Path.GetDirectoryName(Path.Combine(root, project)) |> Option.ofObj |> Option.defaultValue root
        let paths =
            project ::
                (facts
                 |> List.collect (fun f ->
                     let (GovernedPath source) = f.Source
                     match f.Signature with
                     | Some(GovernedPath signature) -> [ source; signature ]
                     | None -> [ source ])
                 |> List.distinct
                 |> List.sort)
        let bytes =
            paths
            |> List.collect (fun path ->
                let fullPath = if path = project then Path.Combine(root, path) else Path.Combine(projectDirectory, path)
                let text = File.ReadAllText(fullPath)
                [ path; "\u0000"; text; "\u0000" ])
            |> String.concat ""
            |> Encoding.UTF8.GetBytes
        SHA256.HashData bytes |> Convert.ToHexString |> fun value -> value.ToLowerInvariant()

    let receipt root project isTestProject requiresSurfaceBaseline surfaceBaselineCurrent request =
        let digestOptional relative =
            let path = Path.Combine(root, relative)
            if File.Exists path then Some(SHA256.HashData(File.ReadAllBytes path) |> Convert.ToHexString |> fun value -> value.ToLowerInvariant()) else None
        match senseProject root project isTestProject requiresSurfaceBaseline surfaceBaselineCurrent with
        | Error reason ->
            { SchemaVersion = 1
              Kind = "fsharp-public-surface"
              Applicability = "applicable"
              Applicable = true
              ApplicabilityReason = "project input must be readable"
              Project = project
              DeclaredGlob = "src/**/*.fsi"
              CompiledSources = []
              MatchedModules = []
              MatchedModuleCount = 0
              Cardinality = "zero"
              Maturity = "warn"
              Findings = [ { Code = "fsharp.surface-malformed"; File = project; Detail = reason; IsInputState = true; BaseSeverity = "blocking"; EffectiveSeverity = "blocking"; Evidence = None } ]
              FreshnessDigest = None
              ConfigDigest = digestOptional ".fsgg/capabilities.yml"
              PolicyDigest = digestOptional ".fsgg/fsharp-surface.json"
              SourceDigest = None
              Malformed = Some reason }
        | Ok facts ->
            let findings = evaluate request facts |> List.map (fun finding ->
                let (GovernedPath file) = finding.Location.File
                { Code = finding.Code; File = file; Detail = finding.Location.Detail; IsInputState = finding.IsInputState
                  BaseSeverity = "blocking"; EffectiveSeverity = "advisory"
                  Evidence = finding.EvidenceTag |> Option.map (fun (EvidenceTag value) -> value) })
            let sources =
                facts
                |> List.choose (fun fact -> fact.Signature |> Option.map (fun (GovernedPath path) -> path))
                |> List.sort
            let declaredGlob =
                match load root with
                | Missing policy | Loaded policy -> policy.DeclaredGlob
                | Invalid _ -> defaultFacts.DeclaredGlob
            { SchemaVersion = 1
              Kind = "fsharp-public-surface"
              Applicability = if isTestProject then "not-applicable" else "applicable"
              Applicable = not isTestProject
              ApplicabilityReason = if isTestProject then "test projects are excluded" else "compiled non-test F# project"
              Project = project
              DeclaredGlob = declaredGlob
              CompiledSources = sources
              MatchedModules = sources
              MatchedModuleCount = List.length sources
              Cardinality = match List.length sources with 0 -> "zero" | 1 -> "one" | _ -> "many"
              Maturity = "warn"
              Findings = findings
              FreshnessDigest = Some(digestFiles root project facts)
              ConfigDigest = digestOptional ".fsgg/capabilities.yml"
              PolicyDigest = digestOptional ".fsgg/fsharp-surface.json"
              SourceDigest = Some(digestFiles root project facts)
              Malformed = None }

    let receiptJson receipt =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = false))
        writer.WriteStartObject()
        writer.WriteNumber("schemaVersion", receipt.SchemaVersion)
        writer.WriteString("kind", receipt.Kind)
        writer.WriteString("applicability", receipt.Applicability)
        writer.WriteBoolean("applicable", receipt.Applicable)
        writer.WriteString("applicabilityReason", receipt.ApplicabilityReason)
        writer.WriteString("project", receipt.Project)
        writer.WriteString("declaredGlob", receipt.DeclaredGlob)
        writer.WritePropertyName("compiledSources")
        writer.WriteStartArray()
        receipt.CompiledSources |> List.iter writer.WriteStringValue
        writer.WriteEndArray()
        writer.WritePropertyName("matchedModules")
        writer.WriteStartArray()
        receipt.MatchedModules |> List.iter writer.WriteStringValue
        writer.WriteEndArray()
        writer.WriteNumber("matchedModuleCount", receipt.MatchedModuleCount)
        writer.WriteString("cardinality", receipt.Cardinality)
        writer.WriteString("maturity", receipt.Maturity)
        writer.WritePropertyName("findings")
        writer.WriteStartArray()
        receipt.Findings |> List.iter (fun finding ->
            writer.WriteStartObject(); writer.WriteString("code", finding.Code); writer.WriteString("file", finding.File)
            writer.WriteString("detail", finding.Detail); writer.WriteBoolean("isInputState", finding.IsInputState)
            writer.WriteString("baseSeverity", finding.BaseSeverity); writer.WriteString("effectiveSeverity", finding.EffectiveSeverity)
            match finding.Evidence with Some value -> writer.WriteString("evidence", value) | None -> writer.WriteNull("evidence")
            writer.WriteEndObject())
        writer.WriteEndArray()
        match receipt.FreshnessDigest with
        | Some digest -> writer.WriteString("freshnessDigest", digest)
        | None -> writer.WriteNull("freshnessDigest")
        match receipt.ConfigDigest with Some value -> writer.WriteString("configDigest", value) | None -> writer.WriteNull("configDigest")
        match receipt.PolicyDigest with Some value -> writer.WriteString("policyDigest", value) | None -> writer.WriteNull("policyDigest")
        match receipt.SourceDigest with Some value -> writer.WriteString("sourceDigest", value) | None -> writer.WriteNull("sourceDigest")
        match receipt.Malformed with
        | Some reason -> writer.WriteString("malformed", reason)
        | None -> writer.WriteNull("malformed")
        writer.WriteEndObject()
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray())
