namespace FS.GG.Governance.VerifyCommand

open System
open System.Security.Cryptography
open System.Text
open FS.GG.SDD.Commands
open FS.GG.SDD.Artifacts

module internal ProviderContextLoader =
    module CB = FS.GG.Governance.Config.CapabilityBindings
    module Capture = ProviderContextCapture

    let diagnostic path message : Fsgg.ProviderCatalog.Diagnostic =
        { Code = "verify.providerContextInvalid"; Path = path; Message = message }

    let refused path message = Error [ diagnostic path message ]
    let digest (bytes: byte array) = Convert.ToHexString(SHA256.HashData bytes).ToLowerInvariant()
    let sameDigest a b = String.Equals(a, b, StringComparison.OrdinalIgnoreCase)

    let decode (selection: ProviderContext.Selection) (provenanceBytes: byte array) (policyBytes: byte array) =
        try
            if not (sameDigest (digest provenanceBytes) selection.ProvenanceSha256) then
                refused "$.provenance" "Selected raw provenance digest does not match captured bytes."
            elif not (sameDigest (digest policyBytes) selection.PolicySha256) then
                refused "$.policy" "Selected raw policy digest does not match captured bytes."
            else
                match CatalogScaffoldPolicy.parse ("sha256:" + digest policyBytes) policyBytes with
                | Error errors -> Error errors
                | Ok policy ->
                    let text = UTF8Encoding(false, true).GetString provenanceBytes
                    match ScaffoldProvenanceDocument.parse text with
                    | Error errors -> Error errors
                    | Ok(ScaffoldProvenanceDocument.Legacy _) ->
                        refused "$.provenance.schemaVersion" "Explicit provider mode requires schema-2 catalog provenance."
                    | Ok(ScaffoldProvenanceDocument.Catalog provenance) ->
                        let declaration = provenance.Declaration
                        let admission = declaration.Admission
                        let identityMatches (recorded: CatalogScaffoldProvenance.Identity) (selected: CatalogScaffoldPolicy.Identity) =
                            recorded.Id = selected.Id && recorded.Version = selected.Version && recorded.Digest = selected.Digest
                        let selectedFormats = policy.SupportedEvidenceFormats |> List.map (fun f -> f.Id, f.Version) |> List.sort
                        let recordedFormats = admission.SupportedFormats |> List.map (fun f -> f.Id, f.Version) |> List.sort
                        let selectedMappings = policy.EvidenceMappings |> List.map (fun m -> m.Format, m.Id, m.Version) |> List.sort
                        let recordedMappings = admission.EvidenceMappings |> List.map (fun m -> m.Format, m.Id, m.Version) |> List.sort
                        let policyMatches =
                            identityMatches declaration.Policy policy.Identity
                            && identityMatches declaration.EvidenceMap policy.EvidenceMapIdentity
                            && List.sort admission.RequiredCapabilityIds = List.sort policy.RequiredCapabilityIds
                            && List.sort admission.SemanticOnlyCapabilityIds = List.sort policy.SemanticOnlyCapabilityIds
                            && List.sort admission.SupportedPlatforms = List.sort policy.KnownPlatforms
                            && List.sort admission.SupportedEnvironmentIds = (policy.EnvironmentBindings |> List.map (fun e -> e.Id) |> List.sort)
                            && recordedFormats = selectedFormats && recordedMappings = selectedMappings
                        if admission.Platform <> selection.Platform || provenance.Observation.Platform <> selection.Platform then
                            refused "$.platform" "Selected platform differs from provenance declaration or observation."
                        elif declaration.Budgets.AdmittedPreflightSeconds > policy.MaximumPreflightSeconds
                             || declaration.Budgets.AdmittedScaffoldSeconds > policy.MaximumScaffoldSeconds then
                            refused "$.policy.budgets" "Recorded admission exceeds the independently selected policy ceilings."
                        elif not policyMatches then
                            refused "$.policy" "Provenance admission differs from the independently selected policy."
                        else
                            match ProviderCapabilityAdmission.request policy selection.Platform declaration.Descriptor with
                            | Error errors -> Error errors
                            | Ok request ->
                                // Whole-request refusal precedes catalog selection, cache reads and empty selection.
                                match CB.resolve request with
                                | CB.Rejected errors ->
                                    errors |> List.map (fun d -> diagnostic ("$.bindings." + d.CapabilityId + "." + d.Field) d.Message) |> Error
                                | CB.Resolved _ -> Ok request
        with
        | :? DecoderFallbackException -> refused "$.provenance" "Captured provenance is not strict UTF-8."

    let load (selection: ProviderContext.Selection) (session: Capture.CaptureSession) =
        let observation = Capture.inspect session
        if observation.Model.Phase <> Capture.Ready || not observation.Model.CaptureConsumed
           || observation.Model.FirstFailure.IsSome || not (List.isEmpty observation.Model.SecondaryFailures)
           || observation.Settlement <> Capture.Retained || observation.WorkEndReached
           || observation.CleanupEndReached || observation.CancellationObserved
           || List.isEmpty observation.Roots || (observation.Roots |> List.exists (fun r -> r.CurrentBindingMatches <> Some true)) then
            refused "$.capture" "Original capture is not ready for declaration admission; its caller retains ownership."
        else
            let selected role path expected =
                observation.Documents
                |> List.filter (fun d -> d.Role = role && d.SelectedPath = path && sameDigest d.RawSha256 expected && d.ComparisonPasses > 0)
            match selected "provenance" selection.ProvenancePath selection.ProvenanceSha256,
                  selected "policy" selection.PolicyPath selection.PolicySha256 with
            | [ provenance ], [ policy ] when observation.Documents.Length = 2 ->
                decode selection (provenance.Bytes |> Seq.toArray) (policy.Bytes |> Seq.toArray)
            | _ -> refused "$.capture.documents" "Exactly the selected, compared provenance and policy documents are required."
