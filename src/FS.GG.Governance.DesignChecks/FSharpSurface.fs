namespace FS.GG.Governance.DesignChecks

open System
open System.IO
open System.Xml.Linq
open System.Text.RegularExpressions
open FS.GG.Governance.Config.Model
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

    /// Edge sensor for SDK-style projects.  It reads the declared Compile order rather than globbing source;
    /// this preserves F# compilation semantics and lets the pure evaluator report exact pairing defects.
    let senseProject root project isTestProject requiresSurfaceBaseline surfaceBaselineCurrent =
        try
            let projectPath = Path.Combine(root, project)
            if not (File.Exists projectPath) then Error(sprintf "F# project was not found: %s" project)
            else
                let document = XDocument.Load projectPath
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
                                      Exemption = NoExemption
                                      Declarations = if File.Exists fullSignature then signatureDeclarations fullSignature else []
                                      SignatureMatchesSource = not (File.Exists fullSignature) || signatureIndex = Some(index - 1)
                                      RequiresSurfaceBaseline = requiresSurfaceBaseline
                                      SurfaceBaselineCurrent = surfaceBaselineCurrent })
                    Ok facts
        with ex -> Error(sprintf "unable to sense F# project '%s': %s" project ex.Message)
