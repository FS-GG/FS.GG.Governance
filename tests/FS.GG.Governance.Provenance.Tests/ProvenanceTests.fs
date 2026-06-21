module FS.GG.Governance.Provenance.Tests.ProvenanceTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.CommandRecord
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.Provenance
open FS.GG.Governance.Provenance.Model
open FS.GG.Governance.Provenance.Tests.Support

// US1 — `build` assembles the nine supplied facts into one complete `Provenance` from which each fact reads
// back verbatim (the artifact digests as supplied; the command records whole and in order, each retaining
// all ten of its facts incl. its sensed duration), total over no-records / no-artifacts / equal-base-head /
// failed-or-timed-out-record builds (SC-001, SC-002).

[<Tests>]
let tests =
    testList
        "Provenance"
        [ // (a) Verbatim carriage of all nine facts (SC-001, SC-002, US1 #1/#2).
          test "build carries all nine facts back verbatim" {
              let p = baseProvenance
              Expect.equal p.SourceCommit baseSourceCommit "source commit"
              Expect.equal p.Base baseBase "base revision"
              Expect.equal p.Head baseHead "head revision"
              Expect.equal p.RuleHash baseRuleHash "rule hash"
              Expect.equal p.GeneratorVersion baseGeneratorVersion "generator version"
              Expect.equal p.ArtifactDigests baseArtifactDigests "artifact digests (same elements, same order — verbatim)"
              Expect.equal p.CommandRecords baseCommandRecords "command records (same elements, same order, each whole)"
              Expect.equal p.Environment baseEnvironment "environment class"
              Expect.equal p.Builder baseBuilder "builder identity"
          }

          test "each embedded command record is carried whole, retaining all ten of its facts incl. duration" {
              let p = baseProvenance
              let r = p.CommandRecords.Head
              Expect.equal r.Reproducible.Executable (Executable "gcc") "executable retained"
              Expect.equal r.Reproducible.Arguments [ Argument "-c"; Argument "main.c" ] "arguments retained in order"
              Expect.equal r.Reproducible.WorkingDirectory (WorkingDirectory "/work") "working directory retained"
              Expect.equal r.Reproducible.ExitCode (ExitCode 0) "exit code retained"
              Expect.equal r.Duration (SensedDuration 123_456L) "sensed duration retained on the embedded record"
          }

          // (b) Artifact-digest carriage is verbatim — dedup is the identity's job, L-B4 (US1 #3).
          test "every supplied artifact digest is present on the value (no dedup at build time)" {
              let p =
                  Provenance.build
                      baseSourceCommit baseBase baseHead baseRuleHash baseGeneratorVersion
                      [ ArtifactHash "a1"; ArtifactHash "a1"; ArtifactHash "a2" ]
                      baseCommandRecords baseEnvironment baseBuilder
              Expect.equal p.ArtifactDigests [ ArtifactHash "a1"; ArtifactHash "a1"; ArtifactHash "a2" ] "duplicate carried verbatim — canonicalization is canonicalId's job"
          }

          // (c) Totality edge cases — each yields an ordinary complete provenance; build never throws.
          test "no command records is an ordinary complete provenance" {
              let p =
                  Provenance.build
                      baseSourceCommit baseBase baseHead baseRuleHash baseGeneratorVersion
                      baseArtifactDigests [] baseEnvironment baseBuilder
              Expect.equal p.CommandRecords [] "empty command-record list is ordinary, not an error"
          }

          test "no covered artifacts is an ordinary complete provenance" {
              let p =
                  Provenance.build
                      baseSourceCommit baseBase baseHead baseRuleHash baseGeneratorVersion
                      [] baseCommandRecords baseEnvironment baseBuilder
              Expect.equal p.ArtifactDigests [] "empty artifact-digest list is ordinary, not an error"
          }

          test "equal base and head is an ordinary complete provenance" {
              let p =
                  Provenance.build
                      baseSourceCommit (Revision "same") (Revision "same") baseRuleHash baseGeneratorVersion
                      baseArtifactDigests baseCommandRecords baseEnvironment baseBuilder
              Expect.equal p.Base (Revision "same") "base = head is ordinary"
              Expect.equal p.Head (Revision "same") "base = head is ordinary"
          }

          test "a failed / timed-out embedded command record is an ordinary complete provenance" {
              let failed =
                  CommandRecord.build
                      (Executable "gcc") [ Argument "-c" ] (WorkingDirectory "/work")
                      { Added = []; Changed = []; Removed = [] } (TimeoutLimit 30)
                      (ExitCode 137) (OutputDigest "o") (OutputDigest "e") NoCapturedOutput (SensedDuration 5L)
              let p =
                  Provenance.build
                      baseSourceCommit baseBase baseHead baseRuleHash baseGeneratorVersion
                      baseArtifactDigests [ failed ] baseEnvironment baseBuilder
              Expect.equal p.CommandRecords.Head.Reproducible.ExitCode (ExitCode 137) "failed run carried whole, not rejected"
          }

          // (d) FsCheck totality: over generated provenances, build round-trips every fact.
          testPropertyWithConfig fscheckConfig "build is total and round-trips every fact" <| fun (p: Provenance) ->
              let r = rebuild p
              r.SourceCommit = p.SourceCommit
              && r.Base = p.Base
              && r.Head = p.Head
              && r.RuleHash = p.RuleHash
              && r.GeneratorVersion = p.GeneratorVersion
              && r.ArtifactDigests = p.ArtifactDigests
              && r.CommandRecords = p.CommandRecords
              && r.Environment = p.Environment
              && r.Builder = p.Builder ]
