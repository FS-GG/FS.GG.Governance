module FS.GG.Governance.CommandKind.Tests.IdentityReuseGuardTests

open Expecto
open FS.GG.Governance.CommandRecord
open FS.GG.Governance.Provenance
open FS.GG.Governance.CommandKind.Model
open FS.GG.Governance.CommandKind
open FS.GG.Governance.CommandKind.Tests.Support

// Polish (T049): `runIdentity`/`snapshotIdentity` compute NO new fingerprint — they equal
// `CommandRecord.canonicalId`/`Provenance.canonicalId` VERBATIM — and the `CommandKind` never participates in
// either identity (research D5, FR-008, FR-009).

let private runs = everyKindRun |> List.map snd

[<Tests>]
let tests =
    testList
        "IdentityReuseGuard"
        [ test "runIdentity is exactly CommandRecord.canonicalId (no new fingerprint)" {
              for (_, run) in everyKindRun do
                  Expect.equal
                      (Audit.runIdentity run)
                      (CommandRecord.identityValue (CommandRecord.canonicalId run.Record))
                      "verbatim F032 identity"
          }

          test "snapshotIdentity is exactly Provenance.canonicalId (no new fingerprint)" {
              let snapshot = Audit.auditSnapshot srcCommit baseRev headRev ruleHash genVer digests runs env builder
              Expect.equal
                  (Audit.snapshotIdentity snapshot)
                  (Provenance.identityValue (Provenance.canonicalId snapshot.Provenance))
                  "verbatim F033 identity"
          }

          test "a kind-only change leaves runIdentity equal (kind is descriptive metadata)" {
              let record = makeRecord 42L
              Expect.equal
                  (Audit.runIdentity { Kind = Build; Record = record })
                  (Audit.runIdentity { Kind = VisualCapture; Record = record })
                  "kind never enters the identity"
          } ]
