module FS.GG.Governance.CommandKind.Tests.RunIdentityTests

open Expecto
open FS.GG.Governance.CommandRecord
open FS.GG.Governance.CommandKind.Model
open FS.GG.Governance.CommandKind
open FS.GG.Governance.CommandKind.Tests.Support

// US4 (T032): for a run of EACH of the seven kinds, `runIdentity` = the F032 identity and the run carries its
// kind; `kindToken` is the exhaustive table; two runs differing ONLY in `SensedDuration` share a
// `runIdentity` (duration excluded — FR-008, SC-005).

[<Tests>]
let tests =
    testList
        "RunIdentity"
        [ test "runIdentity equals the F032 CommandRecord identity for a run of every kind" {
              for (kind, run) in everyKindRun do
                  let expected = CommandRecord.identityValue (CommandRecord.canonicalId run.Record)
                  Expect.equal (Audit.runIdentity run) expected (sprintf "%A runIdentity is the F032 identity" kind)
                  Expect.equal run.Kind kind "the run carries its kind"
          }

          test "kindToken is the exhaustive build|test|pack|templateInstantiation|gitDiff|packageInspection|visualCapture table" {
              Expect.equal (Audit.kindToken Build) "build" "build"
              Expect.equal (Audit.kindToken Test) "test" "test"
              Expect.equal (Audit.kindToken Pack) "pack" "pack"
              Expect.equal (Audit.kindToken TemplateInstantiation) "templateInstantiation" "templateInstantiation"
              Expect.equal (Audit.kindToken GitDiff) "gitDiff" "gitDiff"
              Expect.equal (Audit.kindToken PackageInspection) "packageInspection" "packageInspection"
              Expect.equal (Audit.kindToken VisualCapture) "visualCapture" "visualCapture"
          }

          test "two runs differing ONLY in SensedDuration share a runIdentity (duration excluded)" {
              let a = { Kind = Build; Record = makeRecord 111L }
              let b = { Kind = Build; Record = makeRecord 999_999L }
              Expect.equal (Audit.runIdentity a) (Audit.runIdentity b) "duration never affects the identity"
          }

          test "the kind does NOT participate in the identity (same record, different kind ⇒ same identity)" {
              let record = makeRecord 5L
              let asBuild = { Kind = Build; Record = record }
              let asTest = { Kind = Test; Record = record }
              Expect.equal (Audit.runIdentity asBuild) (Audit.runIdentity asTest) "kind is descriptive, not identity"
          } ]
