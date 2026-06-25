module FS.GG.Governance.Attestation.Tests.IdentityReuseGuardTests

open Expecto
open FS.GG.Governance.CommandKind
open FS.GG.Governance.CommandKind.Model
open FS.GG.Governance.Attestation
open FS.GG.Governance.Attestation.Tests.Support

// FR-007 / FR-008 / D5: the attestation Identity computes NO new fingerprint — it equals Provenance
// .canonicalId (via Audit.snapshotIdentity) verbatim, and the descriptive CommandKind never participates in
// identity (a kind-only change leaves Identity equal).

[<Tests>]
let tests =
    testList
        "identity-reuse-guard"
        [ test "Identity equals Audit.snapshotIdentity verbatim (no new fingerprint)" {
              let snapshot = snapshotOf [ packRun ]
              let summary = Attestation.summarize snapshot twoPacked
              Expect.equal summary.Identity (Audit.snapshotIdentity snapshot) ""
          }

          test "a kind-only change (Build vs Pack on the same record) leaves Identity equal" {
              let record = makeRecord 0 100L
              let asBuild = snapshotOf [ { Kind = Build; Record = record } ]
              let asPack = snapshotOf [ { Kind = Pack; Record = record } ]
              let idBuild = (Attestation.summarize asBuild twoPacked).Identity
              let idPack = (Attestation.summarize asPack twoPacked).Identity
              Expect.equal idBuild idPack "the descriptive kind never participates in identity"
          } ]
