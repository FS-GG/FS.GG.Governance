module FS.GG.Governance.Provenance.Tests.DeterminismTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.CommandRecord
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.Provenance
open FS.GG.Governance.Provenance.Model
open FS.GG.Governance.Provenance.Tests.Support

// US3 — `build` and `canonicalId` are deterministic functions of the supplied facts; the artifact digests
// are compared as a SET (order/dup invariant) while the command-record order is significant (SC-005,
// FR-008, D4).

/// A second, distinct command record so a reordering of CommandRecords is observable.
let private secondRecord =
    CommandRecord.build
        (Executable "ld")
        []
        (WorkingDirectory "/work")
        { Added = []; Changed = []; Removed = [] }
        (TimeoutLimit 30)
        (ExitCode 0)
        (OutputDigest "o2")
        (OutputDigest "e2")
        NoCapturedOutput
        (SensedDuration 7L)

[<Tests>]
let tests =
    testList
        "Determinism"
        [ // (a) build then canonicalId twice ⇒ structurally / byte equal.
          test "build and canonicalId are deterministic for a representative provenance" {
              let p1 = rebuild baseProvenance
              let p2 = rebuild baseProvenance
              Expect.equal p1 p2 "identical facts ⇒ structurally identical provenance"
              Expect.equal (Provenance.canonicalId p1) (Provenance.canonicalId p2) "identical facts ⇒ byte-identical identity"
          }

          testPropertyWithConfig fscheckConfig "build + canonicalId are deterministic over generated facts" <| fun (p: Provenance) ->
              let p1 = rebuild p
              let p2 = rebuild p
              p1 = p2 && Provenance.canonicalId p1 = Provenance.canonicalId p2

          // (b) Artifact-digest set order/dup invariance.
          test "reordering and duplicating artifact digests leaves canonicalId unchanged" {
              let digests = [ ArtifactHash "z"; ArtifactHash "a"; ArtifactHash "m" ]
              let p = { baseProvenance with ArtifactDigests = digests }
              let permuted = { p with ArtifactDigests = permuteAndDuplicateDigests digests }
              Expect.equal
                  (Provenance.canonicalId (rebuild permuted))
                  (Provenance.canonicalId (rebuild p))
                  "artifact digests compared as a set ⇒ order/dup invariant"
          }

          testPropertyWithConfig fscheckConfig "artifact-digest order/dup permutation is identity-invariant" <| fun (p: Provenance) ->
              let permuted = { p with ArtifactDigests = permuteAndDuplicateDigests p.ArtifactDigests }
              Provenance.canonicalId (rebuild permuted) = Provenance.canonicalId (rebuild p)

          // (c) Command-record order significance (the contrast — D4).
          test "reordering command records DOES change canonicalId" {
              let forward = { baseProvenance with CommandRecords = [ baseCommandRecord; secondRecord ] }
              let reversed = { baseProvenance with CommandRecords = [ secondRecord; baseCommandRecord ] }
              Expect.notEqual
                  (Provenance.canonicalId (rebuild reversed))
                  (Provenance.canonicalId (rebuild forward))
                  "command-record order is significant"
          } ]
