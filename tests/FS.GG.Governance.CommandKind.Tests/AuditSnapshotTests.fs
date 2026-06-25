module FS.GG.Governance.CommandKind.Tests.AuditSnapshotTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.Provenance.Model
open FS.GG.Governance.CommandKind.Model
open FS.GG.Governance.CommandKind
open FS.GG.Governance.CommandKind.Tests.Support

// US4 (T033): `auditSnapshot` is byte-identical for identical inputs; stable under a no-op re-derive; changes
// when a reproducible input changes; UNCHANGED by a duration-only change; a failed/timed-out run is kept in
// `Runs` with its F051 sentinel exit code (FR-009, SC-006).

let private runs = everyKindRun |> List.map snd

let private snapshotWith (rs: KindedCommandRun list) =
    Audit.auditSnapshot srcCommit baseRev headRev ruleHash genVer digests rs env builder

[<Tests>]
let tests =
    testList
        "AuditSnapshot"
        [ test "byte-identical for identical inputs / no-op re-derive is stable" {
              Expect.equal (Audit.snapshotIdentity (snapshotWith runs)) (Audit.snapshotIdentity (snapshotWith runs)) "stable"
          }

          test "changing a reproducible input changes the identity" {
              let baseId = Audit.snapshotIdentity (snapshotWith runs)

              let changedCommit =
                  Audit.auditSnapshot (Revision "different") baseRev headRev ruleHash genVer digests runs env builder

              let changedRule =
                  Audit.auditSnapshot srcCommit baseRev headRev (RuleHash "other") genVer digests runs env builder

              let changedDigest =
                  Audit.auditSnapshot srcCommit baseRev headRev ruleHash genVer [ ArtifactHash "z9" ] runs env builder

              let changedBuilder =
                  Audit.auditSnapshot srcCommit baseRev headRev ruleHash genVer digests runs env (BuilderIdentity "someone-else")

              Expect.notEqual (Audit.snapshotIdentity changedCommit) baseId "commit changes identity"
              Expect.notEqual (Audit.snapshotIdentity changedRule) baseId "rule hash changes identity"
              Expect.notEqual (Audit.snapshotIdentity changedDigest) baseId "an artifact digest changes identity"
              Expect.notEqual (Audit.snapshotIdentity changedBuilder) baseId "builder changes identity"
          }

          test "adding/removing a command run changes the identity" {
              let baseId = Audit.snapshotIdentity (snapshotWith runs)
              let fewer = Audit.snapshotIdentity (snapshotWith (List.tail runs))
              Expect.notEqual fewer baseId "a different set of command runs changes the identity"
          }

          test "a duration-only change does NOT change the identity" {
              let fast = [ { Kind = Build; Record = makeRecord 1L } ]
              let slow = [ { Kind = Build; Record = makeRecord 10_000_000L } ]
              Expect.equal (Audit.snapshotIdentity (snapshotWith fast)) (Audit.snapshotIdentity (snapshotWith slow)) "duration excluded"
          }

          test "a failed/timed-out run is KEPT in Runs with its F051 sentinel exit code (never dropped)" {
              let sentinel = sentinelRun Test
              let snapshot = snapshotWith [ sentinel ]
              Expect.equal (List.length snapshot.Runs) 1 "the failed run is present"
              let (ExitCode code) = sentinel.Record.Reproducible.ExitCode
              let (ExitCode startFail) = FS.GG.Governance.GateExecution.Interpreter.startFailureExitCode
              Expect.equal code startFail "recorded with the start-failure sentinel exit code"
          } ]
