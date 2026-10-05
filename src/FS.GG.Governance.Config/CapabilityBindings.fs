namespace FS.GG.Governance.Config

open System
open System.IO
open System.Text
open System.Text.Json

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CapabilityBindings =
    /// A stable neutral meaning; this declaration creates no organization floor.
    type SemanticCapability = { Id: string; Meaning: string; RequiresExecutable: bool }

    /// Ordered argv is passed literally; no command-string parsing is permitted.
    type BindingKind = SemanticOnly | Command of Fsgg.Provider.DeclaredCommand

    /// Explicit normalized limits. Missing input remains representable and refuses.
    type CommandLimits =
        { WorkingDirectory: Model.GovernedPath option
          Timeout: Model.TimeoutLimit option
          Cost: Model.Cost option
          Environments: Model.EnvironmentClass list }

    type CapabilityBinding =
        { CapabilityId: string
          Required: bool
          Platforms: string list
          ToolIds: string list
          EvidenceIds: string list
          Binding: BindingKind
          Limits: CommandLimits option }

    /// Exact version is a declaration, never an observation of the installed tool.
    type ToolBinding = { Id: string; ExactVersion: string }
    type EvidenceFormat = { Id: string; Version: string }
    type EvidenceBinding =
        { Id: string; Format: EvidenceFormat; OutputPath: Model.GovernedPath }

    /// Support and requirement lists belong to trusted caller policy, not provider data.
    type ResolutionRequest =
        { ContractVersion: string
          SelectedPlatform: string
          RequiredCapabilityIds: string list
          SemanticOnlyObligationIds: string list
          KnownPlatforms: string list
          SupportedEnvironments: Model.EnvironmentClass list
          SupportedEvidenceFormats: EvidenceFormat list
          Tools: ToolBinding list
          Evidence: EvidenceBinding list
          Bindings: CapabilityBinding list }

    type DiagnosticCode =
        | MalformedInput | DuplicateIdentity | MissingRequiredBinding
        | UnknownRequiredSemanticId | IllegalSemanticOnly | UnresolvedTool
        | UnresolvedEvidence | UnresolvedPlatform | UnsupportedEvidenceFormat
        | InvalidPath | InvalidExecutionLimit | UnsupportedContractVersion

    type Diagnostic =
        { CapabilityId: string; Field: string; Code: DiagnosticCode; Message: string }

    /// Complete validated declarations only; neither execution nor evidence acceptance.
    type ResolvedSet =
        { Bindings: CapabilityBinding list
          Tools: ToolBinding list
          Evidence: EvidenceBinding list
          UnsupportedCapabilityIds: string list }

    type Resolution = Resolved of ResolvedSet | Rejected of Diagnostic list

    let catalog : SemanticCapability list =
        [ "build:build", "Build selected product components"
          "test:test", "Execute selected tests and functional journeys"
          "lint:lint", "Enforce declared formatting/static analysis"
          "evidence:evidence", "Validate required evidence and provenance"
          "package:package", "Produce and verify the declared distributable"
          "security:security", "Verify declared dependency/code security policy"
          "public-surface:public-surface", "Validate declared API/CLI/module surface"
          "release:release", "Verify release identities, coherence and applicable publication gates" ]
        |> List.map (fun (id, meaning) -> { Id = id; Meaning = meaning; RequiresExecutable = true })
        |> List.sortBy (fun c -> c.Id)

    let blank (value: string) = String.IsNullOrWhiteSpace value

    // Inspect the raw spelling before normalizePath can conceal an escaping traversal.
    let validPath allowRoot (Model.GovernedPath raw) =
        if blank raw then false
        else
            let unified = raw.Replace('\\', '/')
            let drive = unified.Length >= 2 && unified.[1] = ':'
            let absolute = unified.StartsWith("/", StringComparison.Ordinal)
            let control = unified |> Seq.exists Char.IsControl
            let depth, escaped =
                unified.Split('/')
                |> Array.fold (fun (depth, escaped) segment ->
                    match segment with
                    | "" | "." -> depth, escaped
                    | ".." -> max 0 (depth - 1), escaped || depth = 0
                    | _ -> depth + 1, escaped) (0, false)
            not (drive || absolute || control || escaped) && (allowRoot || depth > 0)

    let resolve (request: ResolutionRequest) : Resolution =
        // Local diagnostic accumulation is deterministic after sorting, and avoids a
        // branching result monad obscuring independent input defects. No I/O occurs.
        let diagnostics = ResizeArray<Diagnostic>()
        let add id field code message =
            diagnostics.Add { CapabilityId = id; Field = field; Code = code; Message = message }
        let malformed id field value =
            if blank value then add id field MalformedInput "A nonblank declaration is required."
        let identities capability field (values: string list) =
            for value in values do malformed capability field value
            for value, count in values |> List.countBy (fun x -> x) do
                if count > 1 then
                    add capability field DuplicateIdentity (sprintf "Duplicate identity '%s'." value)
        let refs capability field known code values =
            identities capability field values
            for value in values do
                if not (Set.contains value known) then
                    add capability field code (sprintf "Unresolved reference '%s'." value)

        if request.ContractVersion <> "1.0.0" then
            add "" "contractVersion" UnsupportedContractVersion "Only neutral catalog contract 1.0.0 is supported."
        malformed "" "selectedPlatform" request.SelectedPlatform
        identities "" "knownPlatforms" request.KnownPlatforms
        identities "" "requiredCapabilityIds" request.RequiredCapabilityIds
        identities "" "semanticOnlyObligationIds" request.SemanticOnlyObligationIds
        identities "" "tools" (request.Tools |> List.map (fun t -> t.Id))
        identities "" "evidence" (request.Evidence |> List.map (fun e -> e.Id))
        identities "" "bindings" (request.Bindings |> List.map (fun b -> b.CapabilityId))
        let platforms = Set.ofList request.KnownPlatforms
        let toolIds = request.Tools |> List.map (fun t -> t.Id) |> Set.ofList
        let evidenceIds = request.Evidence |> List.map (fun e -> e.Id) |> Set.ofList
        let semantics = catalog |> List.map (fun c -> c.Id) |> Set.ofList
        let semanticOnly = Set.ofList request.SemanticOnlyObligationIds
        let supportedFormats = Set.ofList request.SupportedEvidenceFormats
        let supportedEnvironments = Set.ofList request.SupportedEnvironments
        if not (Set.contains request.SelectedPlatform platforms) then
            add "" "selectedPlatform" UnresolvedPlatform "The selected platform is not independently declared."
        for environment, count in request.SupportedEnvironments |> List.countBy (fun x -> x) do
            if count > 1 then add "" "supportedEnvironments" DuplicateIdentity (sprintf "Duplicate environment '%A'." environment)
        for format, count in request.SupportedEvidenceFormats |> List.countBy (fun x -> x) do
            malformed "" "supportedEvidenceFormats.id" format.Id
            malformed "" "supportedEvidenceFormats.version" format.Version
            if count > 1 then add "" "supportedEvidenceFormats" DuplicateIdentity (sprintf "Duplicate format '%s/%s'." format.Id format.Version)
        for id in request.SemanticOnlyObligationIds do
            if Set.contains id semantics then
                add id "semanticOnlyObligationIds" IllegalSemanticOnly "A neutral executable obligation cannot become semantic-only."
        for tool in request.Tools do malformed "" ("tools." + tool.Id + ".exactVersion") tool.ExactVersion
        for evidence in request.Evidence do
            let field = "evidence." + evidence.Id
            malformed "" (field + ".format.id") evidence.Format.Id
            malformed "" (field + ".format.version") evidence.Format.Version
            if not (validPath false evidence.OutputPath) then
                add "" (field + ".outputPath") InvalidPath "Evidence requires a governed relative output file path."
        let required =
            request.RequiredCapabilityIds
            @ (request.Bindings |> List.filter (fun b -> b.Required) |> List.map (fun b -> b.CapabilityId))
            |> Set.ofList
        for id in required do
            if not (Set.contains id semantics || Set.contains id semanticOnly) then
                add id "capabilityId" UnknownRequiredSemanticId "The required semantic identity is not recognized."
            if not (request.Bindings |> List.exists (fun b -> b.CapabilityId = id)) then
                add id "binding" MissingRequiredBinding "A required obligation has no declaration."

        for binding in request.Bindings do
            let id = binding.CapabilityId
            refs id "platforms" platforms UnresolvedPlatform binding.Platforms
            if List.isEmpty binding.Platforms || not (List.contains request.SelectedPlatform binding.Platforms) then
                add id "platforms" UnresolvedPlatform "Binding does not support the selected platform."
            refs id "toolIds" toolIds UnresolvedTool binding.ToolIds
            refs id "evidenceIds" evidenceIds UnresolvedEvidence binding.EvidenceIds
            for evidence in request.Evidence |> List.filter (fun e -> List.contains e.Id binding.EvidenceIds) do
                if not (Set.contains evidence.Format supportedFormats) then
                    add id "evidenceIds" UnsupportedEvidenceFormat (sprintf "Unsupported evidence format '%s/%s'." evidence.Format.Id evidence.Format.Version)
            match binding.Binding with
            | SemanticOnly ->
                if Set.contains id semantics || not (Set.contains id semanticOnly) then
                    add id "binding" IllegalSemanticOnly "Only an independently recognized semantic-only obligation can omit execution."
                if Option.isSome binding.Limits then
                    add id "limits" InvalidExecutionLimit "Semantic-only obligations cannot declare executable limits."
            | Command command ->
                if Set.contains id semanticOnly then
                    add id "binding" IllegalSemanticOnly "A trusted semantic-only obligation must remain command-free."
                malformed id "binding.executable" command.Executable
                if command.Arguments |> List.exists (fun argument -> Object.ReferenceEquals(argument, null)) then
                    add id "binding.arguments" MalformedInput "Null arguments are invalid; empty literal arguments are permitted."
                if Set.contains id required && List.isEmpty binding.ToolIds then
                    add id "toolIds" UnresolvedTool "A required executable obligation needs an exact declared tool binding."
                if Set.contains id required && List.isEmpty binding.EvidenceIds then
                    add id "evidenceIds" UnresolvedEvidence "A required executable obligation needs execution evidence."
                match binding.Limits with
                | None -> add id "limits" InvalidExecutionLimit "Explicit executable limits are required."
                | Some limits ->
                    match limits.WorkingDirectory with
                    | None -> add id "limits.workingDirectory" InvalidPath "An explicit governed working directory is required."
                    | Some path when not (validPath true path) -> add id "limits.workingDirectory" InvalidPath "Working directory must stay within the governed root."
                    | Some _ -> ()
                    match limits.Timeout with
                    | Some (Model.TimeoutLimit seconds) when seconds > 0 -> ()
                    | _ -> add id "limits.timeout" InvalidExecutionLimit "A positive explicit timeout is required."
                    if Option.isNone limits.Cost then add id "limits.cost" InvalidExecutionLimit "An explicit cost class is required."
                    if List.isEmpty limits.Environments then add id "limits.environments" InvalidExecutionLimit "An explicit supported environment is required."
                    for environment, count in limits.Environments |> List.countBy (fun x -> x) do
                        if count > 1 then add id "limits.environments" DuplicateIdentity (sprintf "Duplicate environment '%A'." environment)
                        if not (Set.contains environment supportedEnvironments) then
                            add id "limits.environments" InvalidExecutionLimit (sprintf "Unsupported environment '%A'." environment)

        if diagnostics.Count > 0 then
            diagnostics |> Seq.distinct |> Seq.sortBy (fun d -> d.CapabilityId, d.Field, d.Code, d.Message) |> List.ofSeq |> Rejected
        else
            let known (binding: CapabilityBinding) = Set.contains binding.CapabilityId semantics || Set.contains binding.CapabilityId semanticOnly
            let normalize (binding: CapabilityBinding) =
                { binding with
                    Required = Set.contains binding.CapabilityId required
                    Platforms = List.sort binding.Platforms
                    ToolIds = List.sort binding.ToolIds
                    EvidenceIds = List.sort binding.EvidenceIds
                    Limits = binding.Limits |> Option.map (fun limits ->
                        { limits with
                            WorkingDirectory = limits.WorkingDirectory |> Option.map (fun (Model.GovernedPath path) -> Model.normalizePath path)
                            Environments = List.sort limits.Environments }) }
            Resolved
                { Bindings = request.Bindings |> List.filter known |> List.map normalize |> List.sortBy (fun b -> b.CapabilityId)
                  Tools = request.Tools |> List.sortBy (fun t -> t.Id)
                  Evidence = request.Evidence |> List.map (fun e -> { e with OutputPath = let (Model.GovernedPath path) = e.OutputPath in Model.normalizePath path }) |> List.sortBy (fun e -> e.Id)
                  UnsupportedCapabilityIds = request.Bindings |> List.filter (known >> not) |> List.map (fun b -> b.CapabilityId) |> List.sort }

    let catalogJson () =
        // Utf8JsonWriter mutation is confined to an in-memory deterministic projection.
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))
        writer.WriteStartObject()
        writer.WriteString("schema", "fsgg.neutral-capability-catalog/v1")
        writer.WriteString("contractVersion", "1.0.0")
        writer.WriteStartArray("capabilities")
        for capability in catalog do
            writer.WriteStartObject()
            writer.WriteString("id", capability.Id)
            writer.WriteString("meaning", capability.Meaning)
            writer.WriteBoolean("requiresExecutable", capability.RequiresExecutable)
            writer.WriteEndObject()
        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray()) + "\n"
