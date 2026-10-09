namespace FS.GG.Governance.Adapters.SddHandoff

open System
open System.Text.Json
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Adapters.SddHandoff.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Reader =

    type HandoffRead = { Source: string; Json: string }

    exception ParseFailure of DiagnosticCause * string

    let fail cause message = raise (ParseFailure(cause, message))

    let property (path: string) (name: string) (value: JsonElement) =
        match value.TryGetProperty name with
        | true, found -> found
        | _ -> fail Malformed $"{path} is missing required field '{name}'"

    let optionalProperty (name: string) (value: JsonElement) =
        match value.TryGetProperty name with
        | true, found -> Some found
        | _ -> None

    let objectValue (path: string) (value: JsonElement) =
        if value.ValueKind <> JsonValueKind.Object then
            fail Malformed $"{path} must be an object"

        value

    let arrayValue (path: string) (value: JsonElement) =
        if value.ValueKind <> JsonValueKind.Array then
            fail Malformed $"{path} must be an array"

        value.EnumerateArray() |> Seq.toList

    let stringValue (path: string) (value: JsonElement) =
        if value.ValueKind <> JsonValueKind.String then
            fail Malformed $"{path} must be a string"

        value.GetString() |> Option.ofObj |> Option.defaultValue ""

    let boolValue (path: string) (value: JsonElement) =
        match value.ValueKind with
        | JsonValueKind.True -> true
        | JsonValueKind.False -> false
        | _ -> fail Malformed $"{path} must be a boolean"

    let intValue (path: string) (value: JsonElement) =
        match value.TryGetInt32() with
        | true, parsed -> parsed
        | _ -> fail Malformed $"{path} must be a 32-bit integer"

    let decimalValue (path: string) (value: JsonElement) =
        match value.TryGetDecimal() with
        | true, parsed -> parsed
        | _ -> fail Malformed $"{path} must be a decimal number"

    let optionalString (path: string) (value: JsonElement option) =
        match value with
        | None -> None
        | Some element when element.ValueKind = JsonValueKind.Null -> None
        | Some element -> Some(stringValue path element)

    let strings (path: string) (value: JsonElement) =
        arrayValue path value
        |> List.mapi (fun index element -> stringValue $"{path}[{index}]" element)

    let decimals (path: string) (value: JsonElement) =
        arrayValue path value
        |> List.mapi (fun index element -> decimalValue $"{path}[{index}]" element)

    let ints (path: string) (value: JsonElement) =
        arrayValue path value
        |> List.mapi (fun index element -> intValue $"{path}[{index}]" element)

    let parseDeclaredState (id: string) (token: string) : DeclaredState =
        match token with
        | "pending" -> Pending
        | "real" -> Real
        | "synthetic" -> Synthetic
        | "failed" -> DeclaredState.Failed
        | "skipped" -> Skipped
        | "deferred" -> Deferred
        | "accepted-deferral" -> AcceptedDeferral
        | "autoSynthetic" ->
            fail AutoSyntheticDeclared $"evidence node '{id}' declares computed-only state 'autoSynthetic'"
        | _ -> fail Malformed $"evidence node '{id}' declares unknown state token '{token}'"

    let majorOf (version: string) =
        match version.Split('.') |> Array.tryHead with
        | Some value ->
            match Int32.TryParse value with
            | true, parsed -> Some parsed
            | _ -> None
        | None -> None

    let generatorRequiresJourney (generatorVersion: string option) =
        generatorVersion
        |> Option.bind (fun generator ->
            let prefix = "FS.GG.SDD.Artifacts/"

            if generator.StartsWith(prefix, StringComparison.Ordinal) then
                match Version.TryParse(generator.Substring prefix.Length) with
                | true, version -> Some(version >= Version(0, 30, 0))
                | _ -> fail Malformed $"generatorVersion '{generator}' is not a supported SDD version identity"
            else
                None)
        |> Option.defaultValue false

    let invalidJourneyReceiptId = "evidence.productionJourneyReceiptInvalid"
    let staleJourneyReceiptId = "evidence.productionJourneyReceiptStale"

    let canonicalJourneyDiagnostics (diagnosticIds: string list) =
        diagnosticIds
        |> List.filter (fun id -> id = invalidJourneyReceiptId || id = staleJourneyReceiptId)
        |> List.distinct

    let journeyDisposition (unmet: int) (journeyDiagnosticIds: string list) =
        if unmet = 0 then
            JourneySatisfied
        else
            let has id =
                journeyDiagnosticIds |> List.contains id

            match has invalidJourneyReceiptId, has staleJourneyReceiptId with
            | false, true -> JourneyReceiptStale
            | true, false -> JourneyReceiptInvalid
            | _ -> JourneyProvenanceUnsupported

    let parseIntent (path: string) (value: JsonElement) : Fsgg.Schemas.PerformanceIntentDeclaration option =
        if value.ValueKind = JsonValueKind.Null then
            None
        else
            let item = objectValue path value

            let intent: Fsgg.Schemas.PerformanceIntentDeclaration =
                {
                    Id = property path "id" item |> stringValue $"{path}.id"
                    Disposition = property path "disposition" item |> stringValue $"{path}.disposition"
                    TargetFps = property path "targetFps" item |> intValue $"{path}.targetFps"
                    WorkloadIds = property path "workloadIds" item |> strings $"{path}.workloadIds"
                    WorkloadDefinitionDigests =
                        property path "workloadDefinitionDigests" item
                        |> strings $"{path}.workloadDefinitionDigests"
                    MaximumExpectedScale =
                        property path "maximumExpectedScale" item
                        |> stringValue $"{path}.maximumExpectedScale"
                    MaxP95Ms = property path "maxP95Ms" item |> decimalValue $"{path}.maxP95Ms"
                    MaxP99Ms = property path "maxP99Ms" item |> decimalValue $"{path}.maxP99Ms"
                    MaxCatchUpFrames = property path "maxCatchUpFrames" item |> intValue $"{path}.maxCatchUpFrames"
                    StructuralCostBudgets =
                        property path "structuralCostBudgets" item
                        |> strings $"{path}.structuralCostBudgets"
                    RequiredCapability =
                        property path "requiredCapability" item
                        |> stringValue $"{path}.requiredCapability"
                    LiveCompositorRequired =
                        property path "liveCompositorRequired" item
                        |> boolValue $"{path}.liveCompositorRequired"
                    DeferralIssue = optionalProperty "deferralIssue" item |> optionalString $"{path}.deferralIssue"
                    EvidenceRefs = property path "evidenceRefs" item |> strings $"{path}.evidenceRefs"
                    Rationale = optionalProperty "rationale" item |> optionalString $"{path}.rationale"
                }

            Some intent

    let parseSample (path: string) (value: JsonElement) : Fsgg.Schemas.PerformanceEvidenceSampleSet =
        let item = objectValue path value

        {
            WorkloadId = property path "workloadId" item |> stringValue $"{path}.workloadId"
            WorkloadDefinitionDigest =
                property path "workloadDefinitionDigest" item
                |> stringValue $"{path}.workloadDefinitionDigest"
            WorkloadClass = property path "workloadClass" item |> stringValue $"{path}.workloadClass"
            TargetFps = property path "targetFps" item |> intValue $"{path}.targetFps"
            MaxP95Ms = property path "maxP95Ms" item |> decimalValue $"{path}.maxP95Ms"
            MaxP99Ms = property path "maxP99Ms" item |> decimalValue $"{path}.maxP99Ms"
            MaxCatchUpFrames = property path "maxCatchUpFrames" item |> intValue $"{path}.maxCatchUpFrames"
            MeasurementScope = property path "measurementScope" item |> stringValue $"{path}.measurementScope"
            RequiredCapability =
                property path "requiredCapability" item
                |> stringValue $"{path}.requiredCapability"
            HostProfile = property path "hostProfile" item |> stringValue $"{path}.hostProfile"
            PackageVersions = property path "packageVersions" item |> strings $"{path}.packageVersions"
            MeasurementMode = property path "measurementMode" item |> stringValue $"{path}.measurementMode"
            Capabilities = property path "capabilities" item |> strings $"{path}.capabilities"
            WarmupPolicy = property path "warmupPolicy" item |> stringValue $"{path}.warmupPolicy"
            SamplePolicy = property path "samplePolicy" item |> stringValue $"{path}.samplePolicy"
            CapturedAtUtc = property path "capturedAtUtc" item |> stringValue $"{path}.capturedAtUtc"
            CurrencyToken = property path "currencyToken" item |> stringValue $"{path}.currencyToken"
            ProbeReadbackContaminated =
                property path "probeReadbackContaminated" item
                |> boolValue $"{path}.probeReadbackContaminated"
            DurationSamplesMs = property path "durationSamplesMs" item |> decimals $"{path}.durationSamplesMs"
            CatchUpFrames = property path "catchUpFrames" item |> ints $"{path}.catchUpFrames"
        }

    let parseMeasurement (path: string) (value: JsonElement) : Fsgg.Schemas.PerformanceEvidenceMeasurement =
        let item = objectValue path value

        {
            WorkloadId = property path "workloadId" item |> stringValue $"{path}.workloadId"
            P95Ms = property path "p95Ms" item |> decimalValue $"{path}.p95Ms"
            P99Ms = property path "p99Ms" item |> decimalValue $"{path}.p99Ms"
            MaxCatchUpFrames = property path "maxCatchUpFrames" item |> intValue $"{path}.maxCatchUpFrames"
        }

    let parsePerformanceEvidence index (value: JsonElement) : Fsgg.Schemas.GovernanceHandoffPerformanceEvidence =
        let path = $"performanceEvidence[{index}]"
        let item = objectValue path value
        let artifactPath = $"{path}.artifact"
        let artifact = property path "artifact" item |> objectValue artifactPath

        let claimed =
            match optionalProperty "claimedBudgetPassed" artifact with
            | None -> None
            | Some element when element.ValueKind = JsonValueKind.Null -> None
            | Some element -> Some(boolValue $"{artifactPath}.claimedBudgetPassed" element)

        let artifactValue: Fsgg.Schemas.PerformanceEvidenceArtifact =
            {
                ContractVersion =
                    property artifactPath "contractVersion" artifact
                    |> stringValue $"{artifactPath}.contractVersion"
                ClaimedBudgetPassed = claimed
                SampleSets =
                    property artifactPath "sampleSets" artifact
                    |> arrayValue $"{artifactPath}.sampleSets"
                    |> List.mapi (fun sampleIndex sample ->
                        parseSample $"{artifactPath}.sampleSets[{sampleIndex}]" sample)
            }

        {
            EvidenceId = property path "evidenceId" item |> stringValue $"{path}.evidenceId"
            ArtifactPath = property path "artifactPath" item |> stringValue $"{path}.artifactPath"
            Intent = property path "intent" item |> parseIntent $"{path}.intent"
            Artifact = artifactValue
            Measurements =
                property path "measurements" item
                |> arrayValue $"{path}.measurements"
                |> List.mapi (fun measurementIndex measurement ->
                    parseMeasurement $"{path}.measurements[{measurementIndex}]" measurement)
        }

    let parse (read: HandoffRead) : Result<Handoff, Diagnostic> =
        let diagnostic cause message =
            Error
                {
                    Cause = cause
                    Source = read.Source
                    Message = message
                }

        try
            use document = JsonDocument.Parse read.Json
            let root = objectValue "handoff" document.RootElement

            let contractVersion =
                property "handoff" "contractVersion" root |> stringValue "contractVersion"

            match majorOf contractVersion with
            | None -> fail Malformed $"handoff contractVersion is not recognizable semver: '{contractVersion}'"
            | Some major when major <> supportedContractMajor ->
                fail
                    VersionMismatch
                    $"handoff contractVersion major {major} is unsupported; this consumer pins major {supportedContractMajor}"
            | Some _ -> ()

            let generatorVersion =
                optionalProperty "generatorVersion" root |> optionalString "generatorVersion"

            let evidence = property "handoff" "evidence" root |> objectValue "evidence"

            let nodes =
                property "evidence" "nodes" evidence
                |> arrayValue "evidence.nodes"
                |> List.mapi (fun index value ->
                    let path = $"evidence.nodes[{index}]"
                    let node = objectValue path value
                    let id = property path "id" node |> stringValue $"{path}.id"
                    let token = property path "state" node |> stringValue $"{path}.state"

                    {
                        Id = id
                        State = parseDeclaredState id token
                        Stale =
                            optionalProperty "stale" node
                            |> Option.map (boolValue $"{path}.stale")
                            |> Option.defaultValue false
                        Rationale = optionalProperty "rationale" node |> optionalString $"{path}.rationale"
                    })

            let dependencies =
                optionalProperty "dependencies" evidence
                |> Option.bind (fun value ->
                    if value.ValueKind = JsonValueKind.Null then
                        None
                    else
                        Some value)
                |> Option.map (arrayValue "evidence.dependencies")
                |> Option.defaultValue []
                |> List.mapi (fun index value ->
                    let path = $"evidence.dependencies[{index}]"
                    let edge = objectValue path value

                    property path "dependent" edge |> stringValue $"{path}.dependent",
                    property path "dependency" edge |> stringValue $"{path}.dependency")

            let readiness =
                optionalProperty "readiness" root
                |> Option.map (fun value ->
                    let item = objectValue "readiness" value
                    let counts = property "readiness" "counts" item |> objectValue "readiness.counts"

                    let countValues =
                        counts.EnumerateObject()
                        |> Seq.map (fun entry -> entry.Name, intValue $"readiness.counts.{entry.Name}" entry.Value)
                        |> Seq.toList

                    let perView =
                        property "readiness" "perViewState" item
                        |> fun perViewState ->
                            if perViewState.ValueKind = JsonValueKind.Object then
                                perViewState.EnumerateObject()
                                |> Seq.map (fun entry ->
                                    entry.Name, stringValue $"readiness.perViewState.{entry.Name}" entry.Value)
                                |> Seq.toList
                            else
                                arrayValue "readiness.perViewState" perViewState
                                |> List.mapi (fun index value ->
                                    let path = $"readiness.perViewState[{index}]"
                                    let view = objectValue path value

                                    property path "view" view |> stringValue $"{path}.view",
                                    property path "state" view |> stringValue $"{path}.state")

                    {
                        ShipDisposition =
                            property "readiness" "shipDisposition" item
                            |> stringValue "readiness.shipDisposition"
                        VerificationReadiness =
                            property "readiness" "verificationReadiness" item
                            |> stringValue "readiness.verificationReadiness"
                        BlockingDiagnosticIds =
                            property "readiness" "blockingDiagnosticIds" item
                            |> strings "readiness.blockingDiagnosticIds"
                        Counts = countValues
                        PerViewState = perView
                    })

            let governedReferences =
                optionalProperty "governedReferences" root
                |> Option.map (arrayValue "governedReferences")
                |> Option.defaultValue []
                |> List.mapi (fun index value ->
                    let path = $"governedReferences[{index}]"
                    let item = objectValue path value

                    match optionalProperty "path" item with
                    | Some rawPath ->
                        [
                            {
                                Path = rawPath |> stringValue $"{path}.path" |> normalizePath
                                Owner = property path "owner" item |> stringValue $"{path}.owner"
                                Relationship = property path "relationship" item |> stringValue $"{path}.relationship"
                                Kind = optionalProperty "kind" item |> optionalString $"{path}.kind"
                                Operation = optionalProperty "operation" item |> optionalString $"{path}.operation"
                            }
                        ]
                    | None ->
                        let workItem =
                            optionalProperty "workItem" item
                            |> Option.map (stringValue $"{path}.workItem")
                            |> Option.defaultValue ""

                        property path "paths" item
                        |> strings $"{path}.paths"
                        |> List.map (fun rawPath ->
                            {
                                Path = normalizePath rawPath
                                Owner = workItem
                                Relationship = "legacy"
                                Kind = None
                                Operation = None
                            }))
                |> List.collect id

            let performanceEvidence =
                optionalProperty "performanceEvidence" root
                |> Option.map (arrayValue "performanceEvidence")
                |> Option.defaultValue []
                |> List.mapi parsePerformanceEvidence

            let diagnostics =
                optionalProperty "diagnostics" root
                |> Option.map (arrayValue "diagnostics")
                |> Option.defaultValue []
                |> List.mapi (fun index value ->
                    let path = $"diagnostics[{index}]"
                    let item = objectValue path value

                    {
                        Id = property path "id" item |> stringValue $"{path}.id"
                        Message = property path "message" item |> stringValue $"{path}.message"
                        Correction = property path "correction" item |> stringValue $"{path}.correction"
                        RelatedIds = property path "relatedIds" item |> strings $"{path}.relatedIds"
                    })

            let journeyReadiness =
                match readiness with
                | None when generatorRequiresJourney generatorVersion ->
                    fail Malformed "readiness is required for SDD 0.30+ production-journey facts"
                | None -> None
                | Some block ->
                    match block.Counts |> List.tryFind (fun (name, _) -> name = "journeyObligationsUnmet") with
                    | None when generatorRequiresJourney generatorVersion ->
                        fail
                            Malformed
                            "readiness.counts is missing required field 'journeyObligationsUnmet' for SDD 0.30+"
                    | None -> None
                    | Some(_, unmet) ->
                        if unmet < 0 then
                            fail Malformed "readiness.counts.journeyObligationsUnmet must be non-negative"

                        let shipDisposition = block.ShipDisposition.Trim().ToLowerInvariant()

                        if unmet > 0 && (shipDisposition = "shipready" || shipDisposition = "shippable") then
                            fail
                                Malformed
                                "readiness contradicts itself: ship-ready disposition has unmet production journeys"

                        let journeyDiagnosticIds = canonicalJourneyDiagnostics block.BlockingDiagnosticIds

                        if unmet = 0 && not (List.isEmpty journeyDiagnosticIds) then
                            fail
                                Malformed
                                "readiness contradicts itself: zero unmet production journeys has a journey receipt failure diagnostic"

                        let relatedIds =
                            diagnostics
                            |> List.filter (fun diagnostic -> journeyDiagnosticIds |> List.contains diagnostic.Id)
                            |> List.collect (fun diagnostic -> diagnostic.RelatedIds)
                            |> List.distinct
                            |> List.sort

                        Some
                            {
                                ObligationsUnmet = unmet
                                BlockingDiagnosticIds = journeyDiagnosticIds
                                RelatedIds = relatedIds
                                Disposition = journeyDisposition unmet journeyDiagnosticIds
                            }

            Ok
                {
                    ContractVersion = contractVersion
                    SchemaVersion =
                        optionalProperty "schemaVersion" root
                        |> Option.map (intValue "schemaVersion")
                        |> Option.defaultValue 1
                    GeneratorVersion = generatorVersion
                    Evidence =
                        {
                            Nodes = nodes
                            Dependencies = dependencies
                        }
                    Readiness = readiness
                    JourneyReadiness = journeyReadiness
                    GovernedReferences = governedReferences
                    PerformanceEvidence = performanceEvidence
                    Diagnostics = diagnostics
                }
        with
        | ParseFailure(cause, message) -> diagnostic cause message
        | :? JsonException as ex -> diagnostic Malformed $"handoff JSON could not be parsed: {ex.Message}"
        | ex -> diagnostic Malformed $"handoff JSON could not be parsed: {ex.Message}"

    let sourceDiagnostic cause field message : SourceDiagnostic =
        { Cause = cause; Field = field; Message = message }

    let validSourcePath (path: string) =
        not (String.IsNullOrWhiteSpace path)
        && path = path.Trim()
        && not (path.Contains('\\') || path.Contains(':') || path |> Seq.exists Char.IsControl)
        && (path.Split('/') |> Array.forall (fun part -> part <> "" && part <> "." && part <> ".."))

    let sourceShape field (source: HandoffSource) =
        let (GovernedPath path) = source.Path
        [ if not (validSourcePath path) then
              sourceDiagnostic MalformedSource (field + ".path") "source path must be a canonical governed relative path"
          if String.IsNullOrWhiteSpace source.Digest then
              sourceDiagnostic MalformedSource (field + ".digest") "source digest must not be empty"
          elif not (source.Digest.StartsWith("sha256:", StringComparison.Ordinal)) then
              sourceDiagnostic UnsupportedSourceDigest (field + ".digest") "only sha256:<64 hex digits> source digests are supported"
          elif source.Digest.Length <> 71 || not (source.Digest.Substring(7) |> Seq.forall Uri.IsHexDigit) then
              sourceDiagnostic MalformedSource (field + ".digest") "source SHA-256 digest must contain exactly 64 hex digits" ]

    let sourceSetDiagnostics field sources =
        let malformed = sources |> List.mapi (fun i source -> sourceShape (sprintf "%s[%d]" field i) source) |> List.concat
        let duplicates =
            sources
            |> List.countBy (fun source -> source.Path)
            |> List.choose (fun (path, count) ->
                if count > 1 then Some(sourceDiagnostic DuplicateSource field (sprintf "duplicate source path %A" path)) else None)
        malformed @ duplicates

    let parseWithSources read =
        match parse read with
        | Error diagnostic -> Error(HandoffRejected diagnostic)
        | Ok handoff ->
            try
                use document = JsonDocument.Parse read.Json
                let root = document.RootElement
                let sourcesFields = root.EnumerateObject() |> Seq.filter (fun p -> p.Name = "sources") |> Seq.toList
                match sourcesFields with
                | [] -> Error(SourcesRejected [ sourceDiagnostic MissingSource "sources" "sources[] is required for provider evidence" ])
                | _ :: _ :: _ -> Error(SourcesRejected [ sourceDiagnostic DuplicateSource "sources" "sources field is duplicated" ])
                | [field] when field.Value.ValueKind <> JsonValueKind.Array ->
                    Error(SourcesRejected [ sourceDiagnostic MalformedSource "sources" "sources must be an array" ])
                | [field] ->
                    let items = field.Value.EnumerateArray() |> Seq.toList
                    if List.isEmpty items then
                        Error(SourcesRejected [ sourceDiagnostic MissingSource "sources" "sources[] must not be empty" ])
                    else
                        let parsed =
                            items |> List.mapi (fun i item ->
                                let field = sprintf "sources[%d]" i
                                try
                                    let item = objectValue field item
                                    let duplicated = item.EnumerateObject() |> Seq.countBy (fun p -> p.Name) |> Seq.exists (fun (_, count) -> count > 1)
                                    if duplicated then
                                        Error [ sourceDiagnostic DuplicateSource field "source object contains duplicate fields" ]
                                    else
                                        let source =
                                            { Path = GovernedPath(property field "path" item |> stringValue (field + ".path"))
                                              Digest = property field "digest" item |> stringValue (field + ".digest") }
                                        match sourceShape field source with
                                        | [] -> Ok { source with Digest = source.Digest.ToLowerInvariant() }
                                        | diagnostics -> Error diagnostics
                                with ParseFailure(_, message) -> Error [ sourceDiagnostic MalformedSource field message ])
                        let sources = parsed |> List.choose (function Ok source -> Some source | _ -> None)
                        let diagnostics =
                            (parsed |> List.collect (function Error errors -> errors | _ -> []))
                            @ sourceSetDiagnostics "sources" sources
                        if List.isEmpty diagnostics then Ok { Handoff = handoff; Sources = sources }
                        else Error(SourcesRejected diagnostics)
            with
            | ex -> Error(SourcesRejected [ sourceDiagnostic MalformedSource "sources" ("source projection failed: " + ex.Message) ])

    let validateSources captured requiredPaths handoff =
        let declarations = sourceSetDiagnostics "sources" handoff.Sources
        let captures = sourceSetDiagnostics "captured" captured
        let requiredShape =
            requiredPaths |> List.mapi (fun i (GovernedPath path) ->
                if validSourcePath path then []
                else [ sourceDiagnostic MalformedSource (sprintf "requiredPaths[%d]" i) "required input path must be canonical and governed relative" ]) |> List.concat
        let requiredDuplicates =
            requiredPaths |> List.countBy id |> List.choose (fun (path, count) ->
                if count > 1 then Some(sourceDiagnostic DuplicateSource "requiredPaths" (sprintf "duplicate required input path %A" path)) else None)
        let empty =
            if List.isEmpty handoff.Sources then [ sourceDiagnostic MissingSource "sources" "sources[] must not be empty" ] else []
        let diagnostics = declarations @ captures @ requiredShape @ requiredDuplicates @ empty
        if not (List.isEmpty diagnostics) then Error diagnostics
        else
            let matches =
                handoff.Sources |> List.mapi (fun i source ->
                    let field = sprintf "sources[%d]" i
                    match captured |> List.tryFind (fun captured -> captured.Path = source.Path) with
                    | None -> [ sourceDiagnostic UncapturedSource field "cited source was not captured before dispatch" ]
                    | Some observed when not (String.Equals(observed.Digest, source.Digest, StringComparison.OrdinalIgnoreCase)) ->
                        [ sourceDiagnostic SourceDigestMismatch field "cited source digest does not match captured input bytes" ]
                    | _ -> []) |> List.concat
            let missing =
                requiredPaths |> List.choose (fun path ->
                    if handoff.Sources |> List.exists (fun source -> source.Path = path) then None
                    else Some(sourceDiagnostic MissingRequiredSource "sources" (sprintf "required input source %A is not cited" path)))
            match matches @ missing with
            | [] -> Ok ()
            | diagnostics -> Error diagnostics
