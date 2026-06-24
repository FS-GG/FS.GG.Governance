module FS.GG.Governance.ReleaseFactsSensing.Tests.PropertyTests

open Expecto
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseFactsSensing
open FS.GG.Governance.ReleaseFactsSensing.Model
open FS.GG.Governance.ReleaseFactsSensing.Tests.Support

// FsCheck properties (FR-009/SC-006/SC-002): over random expectations × recovered evidence, `deriveFacts`
// always returns exactly the six families, every state is one of the three, an Error-recovered or
// None-expectation family is NEVER fabricated `Met`, and `deriveFacts` never throws.

// The expectation accessor + recovered Result per family, to assert the no-fabrication invariant.
let private familyInputs (exp: ReleaseExpectations) (recovered: RecoveredEvidence) =
    [ VersionBump, Option.isSome exp.VersionBaseline, (match recovered.Version with Ok _ -> true | Error _ -> false)
      PackageMetadata, Option.isSome exp.RequiredMetadataFields, (match recovered.Metadata with Ok _ -> true | Error _ -> false)
      TemplatePins, Option.isSome exp.ExpectedPins, (match recovered.Pins with Ok _ -> true | Error _ -> false)
      PublishPlan, Option.isSome exp.RequiredPublishPosture, (match recovered.PublishPlan with Ok _ -> true | Error _ -> false)
      TrustedPublishing, Option.isSome exp.RequiredTrustedPublishing, (match recovered.TrustedPublishing with Ok _ -> true | Error _ -> false)
      Provenance, Option.isSome exp.RequiredProvenance, (match recovered.Provenance with Ok _ -> true | Error _ -> false) ]

[<Tests>]
let tests =
    testList
        "PropertyTests"
        [ testPropertyWithConfig fsCheckConfig "deriveFacts always yields exactly the six families"
          <| fun (exp: ReleaseExpectations) (recovered: RecoveredEvidence) ->
              let states = (Sensing.deriveFacts exp recovered).Facts.States
              states.Count = 6
              && (states |> Map.toList |> List.map fst |> List.sort) = (Sensing.releaseFamilies |> List.sort)

          testPropertyWithConfig fsCheckConfig "every state is Met/Unmet/Unrecoverable"
          <| fun (exp: ReleaseExpectations) (recovered: RecoveredEvidence) ->
              (Sensing.deriveFacts exp recovered).Facts.States
              |> Map.forall (fun _ s -> s = Met || s = Unmet || s = Unrecoverable)

          testPropertyWithConfig fsCheckConfig "an Error-recovered or None-expectation family is NEVER Met (no fabrication, SC-002)"
          <| fun (exp: ReleaseExpectations) (recovered: RecoveredEvidence) ->
              let states = (Sensing.deriveFacts exp recovered).Facts.States

              familyInputs exp recovered
              |> List.forall (fun (kind, hasExpectation, recoveredOk) ->
                  if (not hasExpectation) || (not recoveredOk) then
                      states.[kind] = Unrecoverable
                  else
                      true)

          testPropertyWithConfig fsCheckConfig "deriveFacts never throws over arbitrary input"
          <| fun (exp: ReleaseExpectations) (recovered: RecoveredEvidence) ->
              let _ = Sensing.deriveFacts exp recovered
              true ]
