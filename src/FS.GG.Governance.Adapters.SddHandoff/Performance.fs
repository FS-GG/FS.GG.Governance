namespace FS.GG.Governance.Adapters.SddHandoff

open System
open System.Globalization
open System.Text.RegularExpressions
open FS.GG.Governance.Adapters.SddHandoff.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Performance =

    let normalized (value: string) = value.Trim().ToLowerInvariant()

    let nonblank label value =
        if String.IsNullOrWhiteSpace value then [ $"{label} is required" ] else []

    let validTimestamp value =
        not (String.IsNullOrWhiteSpace value)
        && Regex.IsMatch(
            value,
            @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})$",
            RegexOptions.CultureInvariant
        )
        && (match DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None) with
            | true, _ -> true
            | _ -> false)

    let nearestRank percentile samples =
        let ordered = samples |> List.sort
        ordered.[max 0 (int (Math.Ceiling(percentile * float ordered.Length)) - 1)]

    let intentBindings (entries: string list) =
        entries
        |> List.map (fun entry ->
            let separator = entry.IndexOf('=')

            if separator <= 0 || separator = entry.Length - 1 then
                None
            else
                Some(entry.Substring(0, separator).Trim(), entry.Substring(separator + 1).Trim()))

    let sampleBinding (sample: Fsgg.Schemas.PerformanceEvidenceSampleSet) =
        sample.WorkloadDefinitionDigest,
        sample.HostProfile,
        List.sort sample.PackageVersions,
        sample.MeasurementMode,
        sample.MeasurementScope,
        sample.RequiredCapability,
        List.sort sample.Capabilities,
        sample.WarmupPolicy,
        sample.SamplePolicy,
        sample.CapturedAtUtc,
        sample.CurrencyToken,
        sample.ProbeReadbackContaminated

    let measurementKey (measurement: Fsgg.Schemas.PerformanceEvidenceMeasurement) =
        measurement.WorkloadId,
        measurement.P95Ms,
        measurement.P99Ms,
        measurement.MaxCatchUpFrames

    let recompute (samples: Fsgg.Schemas.PerformanceEvidenceSampleSet list) =
        samples
        |> List.groupBy _.WorkloadId
        |> List.choose (fun (workloadId, sets) ->
            let durations = sets |> List.collect _.DurationSamplesMs
            let catchUps = sets |> List.collect _.CatchUpFrames

            if List.isEmpty durations || List.isEmpty catchUps then
                None
            else
                let measured: Fsgg.Schemas.PerformanceEvidenceMeasurement =
                    { WorkloadId = workloadId
                      P95Ms = nearestRank 0.95 durations
                      P99Ms = nearestRank 0.99 durations
                      MaxCatchUpFrames = List.max catchUps }

                Some measured)
        |> List.sortBy _.WorkloadId

    let evaluate
        (stale: bool)
        (evidence: Fsgg.Schemas.GovernanceHandoffPerformanceEvidence)
        : PerformanceEvaluation =
        let remediation =
            $"Re-capture '{evidence.ArtifactPath}' from the declared workload and capable runner, then regenerate the governance handoff."

        let finish state measurements failures =
            { EvidenceId = evidence.EvidenceId
              ArtifactPath = evidence.ArtifactPath
              State = state
              Measurements = measurements
              Failures = failures |> List.distinct
              Remediation = remediation }

        match evidence.Intent with
        | Some intent when normalized intent.Disposition = "not-applicable" ->
            finish PerformanceNotApplicable [] []
        | Some intent when normalized intent.Disposition = "deferred" ->
            let issue = intent.DeferralIssue |> Option.defaultValue "<missing deferral issue>"
            finish PerformanceEnvironmentLimited [] [ $"performance intent is deferred to {issue}" ]
        | None ->
            finish PerformanceFailed [] [ "typed performance intent is required; self-attested performance evidence is insufficient" ]
        | Some intent when normalized intent.Disposition <> "active" ->
            finish PerformanceFailed [] [ $"performance intent has unsupported disposition '{intent.Disposition}'" ]
        | Some intent ->
            let parsedBindings = intentBindings intent.WorkloadDefinitionDigests
            let validBindings = parsedBindings |> List.choose id
            let bindingGroups = validBindings |> List.groupBy fst
            let expectedBindings = validBindings |> Map.ofList
            let declared = intent.WorkloadIds |> Set.ofList
            let samples = evidence.Artifact.SampleSets
            let measurements = recompute samples

            let structuralFailures =
                [ yield! nonblank "evidenceId" evidence.EvidenceId
                  yield! nonblank "artifactPath" evidence.ArtifactPath
                  yield! nonblank "intent.id" intent.Id

                  if intent.TargetFps <= 0 then
                      "intent.targetFps must be positive"

                  if List.isEmpty intent.WorkloadIds then
                      "intent.workloadIds must name at least one normal-play workload"

                  if declared.Count <> intent.WorkloadIds.Length then
                      "intent.workloadIds must not contain duplicates"

                  if parsedBindings |> List.exists Option.isNone then
                      "intent.workloadDefinitionDigests entries must use '<workloadId>=<sha256:digest>'"

                  for workloadId, entries in bindingGroups do
                      if entries.Length <> 1 then
                          $"intent workload '{workloadId}' must have exactly one digest binding"

                  for workloadId in declared do
                      if not (Map.containsKey workloadId expectedBindings) then
                          $"intent workload '{workloadId}' is missing a digest binding"

                  for workloadId, digest in validBindings do
                      if not (Set.contains workloadId declared) then
                          $"intent digest binds undeclared workload '{workloadId}'"

                      if
                          not (
                              Regex.IsMatch(
                                  digest,
                                  @"^sha256:[a-f0-9]{64}$",
                                  RegexOptions.CultureInvariant
                              )
                          )
                      then
                          $"intent workload '{workloadId}' has malformed digest '{digest}'"

                  yield! nonblank "intent.maximumExpectedScale" intent.MaximumExpectedScale

                  if intent.MaxP95Ms <= 0m || intent.MaxP99Ms <= 0m || intent.MaxCatchUpFrames < 0 then
                      "intent thresholds require positive p95/p99 and non-negative catch-up frames"

                  if List.isEmpty intent.StructuralCostBudgets then
                      "intent.structuralCostBudgets must declare at least one structural limit"

                  yield! nonblank "intent.requiredCapability" intent.RequiredCapability

                  if evidence.Artifact.ContractVersion <> "performance-evidence-v1" then
                      $"artifact contractVersion '{evidence.Artifact.ContractVersion}' is unsupported"

                  if List.isEmpty samples then
                      "artifact.sampleSets must contain independently verifiable raw samples"

                  if stale then
                      "performance evidence is stale according to the handoff diagnostic"

                  let hostProfiles = samples |> List.map _.HostProfile |> List.filter (String.IsNullOrWhiteSpace >> not) |> List.distinct

                  if hostProfiles.Length > 1 then
                      let hosts = hostProfiles |> List.sort |> String.concat ", "
                      $"artifact sample sets use mixed host profiles: {hosts}"

                  for workloadId in declared do
                      let sets = samples |> List.filter (fun sample -> sample.WorkloadId = workloadId)

                      if List.isEmpty sets then
                          $"normal-play workload '{workloadId}' is absent"
                      elif sets |> List.exists (fun sample -> sample.WorkloadClass <> "normal-play") then
                          $"workload '{workloadId}' must be classified as normal-play"
                      elif sets |> List.map sampleBinding |> List.distinct |> List.length > 1 then
                          $"workload '{workloadId}' mixes digest, host, package, mode, scope, capability, policy, capture, currency, or contamination bindings"

                  for sample in samples do
                      let id = if String.IsNullOrWhiteSpace sample.WorkloadId then "<missing workloadId>" else sample.WorkloadId
                      yield! nonblank $"{id}.workloadId" sample.WorkloadId
                      yield! nonblank $"{id}.workloadDefinitionDigest" sample.WorkloadDefinitionDigest
                      yield! nonblank $"{id}.workloadClass" sample.WorkloadClass
                      yield! nonblank $"{id}.measurementScope" sample.MeasurementScope
                      yield! nonblank $"{id}.requiredCapability" sample.RequiredCapability
                      yield! nonblank $"{id}.hostProfile" sample.HostProfile
                      yield! nonblank $"{id}.measurementMode" sample.MeasurementMode
                      yield! nonblank $"{id}.warmupPolicy" sample.WarmupPolicy
                      yield! nonblank $"{id}.samplePolicy" sample.SamplePolicy
                      yield! nonblank $"{id}.currencyToken" sample.CurrencyToken

                      if sample.WorkloadClass = "normal-play" && not (Set.contains sample.WorkloadId declared) then
                          $"{id} is an undeclared normal-play workload"

                      if Set.contains sample.WorkloadId declared then
                          if Map.tryFind sample.WorkloadId expectedBindings <> Some sample.WorkloadDefinitionDigest then
                              $"{id}.workloadDefinitionDigest does not match the intent"

                          if sample.TargetFps <> intent.TargetFps then
                              $"{id}.targetFps {sample.TargetFps} does not match intent {intent.TargetFps}"

                          if sample.MaxP95Ms <> intent.MaxP95Ms || sample.MaxP99Ms <> intent.MaxP99Ms then
                              $"{id} p95/p99 thresholds do not match the intent"

                          if sample.MaxCatchUpFrames <> intent.MaxCatchUpFrames then
                              $"{id}.maxCatchUpFrames does not match the intent"

                      if sample.RequiredCapability <> intent.RequiredCapability then
                          $"{id}.requiredCapability '{sample.RequiredCapability}' does not match intent '{intent.RequiredCapability}'"

                      if not (List.contains intent.RequiredCapability sample.Capabilities) then
                          $"{id}.capabilities does not contain required '{intent.RequiredCapability}'"

                      if List.isEmpty sample.PackageVersions || sample.PackageVersions |> List.exists String.IsNullOrWhiteSpace then
                          $"{id}.packageVersions must contain nonblank package identities"

                      if List.isEmpty sample.Capabilities || sample.Capabilities |> List.exists String.IsNullOrWhiteSpace then
                          $"{id}.capabilities must contain nonblank capability identities"

                      if sample.MeasurementMode <> "headless" && sample.MeasurementMode <> "live-compositor" then
                          $"{id}.measurementMode '{sample.MeasurementMode}' is unsupported"

                      if not (validTimestamp sample.CapturedAtUtc) then
                          $"{id}.capturedAtUtc must be an ISO-8601 timestamp"

                      if List.isEmpty sample.DurationSamplesMs then
                          $"{id}.durationSamplesMs must not be empty"
                      elif sample.DurationSamplesMs |> List.exists (fun value -> value < 0m) then
                          $"{id}.durationSamplesMs cannot contain negative values"

                      if List.isEmpty sample.CatchUpFrames then
                          $"{id}.catchUpFrames must not be empty"
                      elif sample.CatchUpFrames |> List.exists (fun value -> value < 0) then
                          $"{id}.catchUpFrames cannot contain negative values"

                      if sample.DurationSamplesMs.Length <> sample.CatchUpFrames.Length then
                          $"{id}.durationSamplesMs and catchUpFrames must have equal per-sample cardinality"

                      if intent.LiveCompositorRequired && sample.ProbeReadbackContaminated then
                          $"{id} live-compositor evidence is probe/readback contaminated" ]

            let environmentFailures =
                [ if
                      intent.LiveCompositorRequired
                      && samples |> List.exists (fun sample -> sample.MeasurementMode <> "live-compositor")
                  then
                      "live-compositor proof requires the protected capable-runner lane; headless evidence cannot satisfy it" ]

            let thresholdFailures =
                [ for measured in measurements do
                      if Set.contains measured.WorkloadId declared then
                          if measured.P95Ms > intent.MaxP95Ms then
                              $"{measured.WorkloadId} recomputed p95 {measured.P95Ms} ms exceeds {intent.MaxP95Ms} ms"

                          if measured.P99Ms > intent.MaxP99Ms then
                              $"{measured.WorkloadId} recomputed p99 {measured.P99Ms} ms exceeds {intent.MaxP99Ms} ms"

                          if measured.MaxCatchUpFrames > intent.MaxCatchUpFrames then
                              $"{measured.WorkloadId} recomputed catch-up frames {measured.MaxCatchUpFrames} exceeds {intent.MaxCatchUpFrames}" ]

            let producerFailures =
                let produced = evidence.Measurements |> List.sortBy _.WorkloadId

                [ if produced |> List.map _.WorkloadId |> List.distinct |> List.length <> produced.Length then
                      "producer measurements contain duplicate workload ids"

                  if not (List.isEmpty produced) && List.map measurementKey produced <> List.map measurementKey measurements then
                      "producer measurements do not match Governance recomputation from raw samples"

                  match evidence.Artifact.ClaimedBudgetPassed with
                  | Some true when not (List.isEmpty thresholdFailures) ->
                      "claimedBudgetPassed=true disagrees with Governance recomputation from raw samples"
                  | Some false when List.isEmpty structuralFailures && List.isEmpty thresholdFailures && List.isEmpty environmentFailures ->
                      "claimedBudgetPassed=false disagrees with Governance recomputation from raw samples"
                  | _ -> () ]

            let failures = structuralFailures @ thresholdFailures @ producerFailures

            if not (List.isEmpty failures) then
                finish PerformanceFailed measurements failures
            elif not (List.isEmpty environmentFailures) then
                finish PerformanceEnvironmentLimited measurements environmentFailures
            else
                finish PerformancePassed measurements []
