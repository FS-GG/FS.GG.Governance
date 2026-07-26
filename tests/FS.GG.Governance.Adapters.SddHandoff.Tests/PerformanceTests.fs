module FS.GG.Governance.Adapters.SddHandoff.Tests.PerformanceTests

open Expecto
open FS.GG.Governance.Adapters.SddHandoff
open FS.GG.Governance.Adapters.SddHandoff.Model

// SYNTHETIC: these compact contract records isolate Governance's pure recomputation rules; the
// real producer JSON path is covered by Reader/Consumer on-disk fixtures.
let private workloadDigest = "sha256:" + String.replicate 64 "a"

let private intent disposition live : Fsgg.Schemas.PerformanceIntentDeclaration =
    { Id = "PI-001"
      Disposition = disposition
      TargetFps = 60
      WorkloadIds = [ "normal" ]
      WorkloadDefinitionDigests = [ $"normal={workloadDigest}" ]
      MaximumExpectedScale = "10k sprites"
      MaxP95Ms = 16.67m
      MaxP99Ms = 25m
      MaxCatchUpFrames = 0
      StructuralCostBudgets = [ "draw-calls<=500" ]
      RequiredCapability = if live then "live-compositor" else "headless"
      LiveCompositorRequired = live
      DeferralIssue = None
      EvidenceRefs = [ "EV-PERF" ]
      Rationale = None }

let private sample
    mode
    host
    (durations: decimal list)
    (catchUps: int list)
    : Fsgg.Schemas.PerformanceEvidenceSampleSet =
    { WorkloadId = "normal"
      WorkloadDefinitionDigest = workloadDigest
      WorkloadClass = "normal-play"
      TargetFps = 60
      MaxP95Ms = 16.67m
      MaxP99Ms = 25m
      MaxCatchUpFrames = 0
      MeasurementScope = "interactive"
      RequiredCapability = mode
      HostProfile = host
      PackageVersions = [ "FS.GG.Game@1.2.3" ]
      MeasurementMode = mode
      Capabilities = [ mode ]
      WarmupPolicy = "120-frames"
      SamplePolicy = $"nearest-rank/{durations.Length}"
      CapturedAtUtc = "2026-07-26T00:00:00Z"
      CurrencyToken = "commit:abc123"
      ProbeReadbackContaminated = false
      DurationSamplesMs = durations
      CatchUpFrames = catchUps }

let private evidence intentValue samples claimed measurements : Fsgg.Schemas.GovernanceHandoffPerformanceEvidence =
    let artifact: Fsgg.Schemas.PerformanceEvidenceArtifact =
        { ContractVersion = "performance-evidence-v1"
          ClaimedBudgetPassed = claimed
          SampleSets = samples }

    { EvidenceId = "EV-PERF"
      ArtifactPath = "readiness/performance.json"
      Intent = intentValue
      Artifact = artifact
      Measurements = measurements }

let private evaluate value = Performance.evaluate false value

[<Tests>]
let tests =
    testList
        "Performance Synthetic"
        [ test "Synthetic M5/M6-like raw samples pass after independent nearest-rank recomputation" {
              let durations = [ for i in 1 .. 100 -> decimal i / 10m ]
              let value = evidence (Some(intent "active" false)) [ sample "headless" "linux-ci" durations (List.replicate 100 0) ] (Some true) []
              let result = evaluate value
              Expect.equal result.State PerformancePassed "within-budget raw evidence passes"
              Expect.equal result.Measurements.Head.P95Ms 9.5m "p95 is nearest rank"
              Expect.equal result.Measurements.Head.P99Ms 9.9m "p99 is nearest rank"
          }

          test "Synthetic M0-like claimed pass is rejected when raw samples exceed the budget" {
              let value =
                  evidence
                      (Some(intent "active" false))
                      [ sample "headless" "linux-ci" [ 10m; 30m ] [ 0; 0 ] ]
                      (Some true)
                      []
              let result = evaluate value
              Expect.equal result.State PerformanceFailed "raw samples override self-attested success"
              Expect.exists result.Failures (fun f -> f.Contains "recomputed p95 30") "exact recomputed p95 failure"
              Expect.exists result.Failures (fun f -> f.Contains "claimedBudgetPassed=true") "overclaim is named"
          }

          test "Synthetic producer measurements are cross-checks and mismatch is rejected" {
              let producer: Fsgg.Schemas.PerformanceEvidenceMeasurement =
                  { WorkloadId = "normal"
                    P95Ms = 1m
                    P99Ms = 1m
                    MaxCatchUpFrames = 0 }
              let value =
                  evidence
                      (Some(intent "active" false))
                      [ sample "headless" "linux-ci" [ 10m; 11m ] [ 0; 0 ] ]
                      None
                      [ producer ]
              let result = evaluate value
              Expect.equal result.State PerformanceFailed "mismatched producer projection fails"
              Expect.exists result.Failures (fun f -> f.Contains "producer measurement") "mismatch is explicit"
          }

          test "Synthetic headless samples cannot satisfy live compositor intent" {
              let value =
                  evidence
                      (Some(intent "active" true))
                      [ { sample "headless" "linux-ci" [ 10m ] [ 0 ] with
                            RequiredCapability = "live-compositor"
                            Capabilities = [ "live-compositor" ] } ]
                      None
                      []
              let result = evaluate value
              Expect.equal result.State PerformanceEnvironmentLimited "capable runner remains required"
              Expect.exists result.Failures (fun f -> f.Contains "live-compositor") "limitation names the missing lane"
          }

          test "Synthetic readback-contaminated live proof is rejected" {
              let contaminated =
                  { sample "live-compositor" "windows-gpu" [ 10m ] [ 0 ] with
                      ProbeReadbackContaminated = true }
              let result = evaluate (evidence (Some(intent "active" true)) [ contaminated ] None [])
              Expect.equal result.State PerformanceFailed "contamination cannot prove live performance"
              Expect.exists result.Failures (fun f -> f.Contains "readback contaminated") "contamination is explicit"
          }

          test "Synthetic cross-host mixing is rejected" {
              let samples =
                  [ sample "headless" "linux-a" [ 10m ] [ 0 ]
                    sample "headless" "linux-b" [ 11m ] [ 0 ] ]
              let result = evaluate (evidence (Some(intent "active" false)) samples None [])
              Expect.equal result.State PerformanceFailed "mixed hosts fail closed"
              Expect.exists result.Failures (fun f -> f.Contains "mixed host profiles") "host mismatch is explicit"
          }

          test "Synthetic stale evidence is rejected with recapture remediation" {
              let value =
                  evidence (Some(intent "active" false)) [ sample "headless" "linux-ci" [ 10m ] [ 0 ] ] None []
              let result = Performance.evaluate true value
              Expect.equal result.State PerformanceFailed "stale evidence fails"
              Expect.exists result.Failures (fun f -> f.Contains "stale") "staleness is explicit"
              Expect.stringContains result.Remediation "readiness/performance.json" "artifact pointer is carried"
          }

          test "Synthetic not-applicable intent produces no applicable gate result" {
              let value =
                  evidence
                      (Some { intent "not-applicable" false with Rationale = Some "no render loop" })
                      []
                      None
                      []
              let result = evaluate value
              Expect.equal result.State PerformanceNotApplicable "non-interactive profile is not burdened"
          }

          test "Synthetic self-attested artifact without typed intent is rejected" {
              let value = evidence None [ sample "headless" "linux-ci" [ 10m ] [ 0 ] ] (Some true) []
              let result = evaluate value
              Expect.equal result.State PerformanceFailed "intent is the policy authority"
              Expect.exists result.Failures (fun f -> f.Contains "typed performance intent") "missing authority is named"
          }

          test "Synthetic definition digests require exactly 64 lowercase hexadecimal digits" {
              let malformed =
                  [ "sha256:x"
                    "sha256:" + String.replicate 63 "a"
                    "sha256:" + String.replicate 64 "A"
                    "sha256:" + String.replicate 63 "a" + "g" ]

              for digest in malformed do
                  let declaration =
                      { intent "active" false with
                          WorkloadDefinitionDigests = [ $"normal={digest}" ] }

                  let value = evidence (Some declaration) [ sample "headless" "linux-ci" [ 10m ] [ 0 ] ] None []
                  let result = evaluate value
                  Expect.equal result.State PerformanceFailed $"malformed digest {digest} fails closed"
                  Expect.exists result.Failures (fun f -> f.Contains "malformed digest") "digest failure is explicit"
          }

          test "Synthetic duplicate declared workload ids are rejected" {
              let declaration =
                  { intent "active" false with
                      WorkloadIds = [ "normal"; "normal" ] }

              let value = evidence (Some declaration) [ sample "headless" "linux-ci" [ 10m ] [ 0 ] ] None []
              let result = evaluate value
              Expect.equal result.State PerformanceFailed "duplicate declaration fails closed"
              Expect.exists result.Failures (fun f -> f.Contains "must not contain duplicates") "duplicate is explicit"
          }

          test "Synthetic duration and catch-up arrays require paired sample cardinality" {
              let value =
                  evidence
                      (Some(intent "active" false))
                      [ sample "headless" "linux-ci" [ 10m; 11m ] [ 0 ] ]
                      None
                      []

              let result = evaluate value
              Expect.equal result.State PerformanceFailed "unpaired raw samples fail closed"
              Expect.exists result.Failures (fun f -> f.Contains "equal per-sample cardinality") "pairing failure is explicit"
          } ]
