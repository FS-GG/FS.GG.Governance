module FS.GG.Governance.ReleaseCommand.Tests.SafeFailureTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.Provenance.Model
open FS.GG.Governance.PackEvidence.Model
open FS.GG.Governance.ReleaseDeclaration
open FS.GG.Governance.ReleaseCommand
open FS.GG.Governance.ReleaseCommand.Tests.Support

// 065 US4 (T033/T035/T037) + the US2 re-run determinism (T022) — the standalone + safe-failure guarantees
// and order-independent determinism, over the REAL F26 cores (the pack execution is disclosed synthetic).

/// Drive init → DeclarationLoaded(Ok) → provenance → sensed → packs, returning the post-join model.
let private driveJoin decl sensed packs =
    let req =
        { Loop.Repo = "."
          Loop.Format = Loop.Json
          Loop.ReleaseOut = "out/release.json"
          Loop.AttestationOut = "out/attestation.json" }

    let m0, _ = Loop.init req
    let m1, _ = Loop.update (Loop.DeclarationLoaded(Ok decl)) m0
    let m2, _ = Loop.update (Loop.ProvenanceSensed(Revision "", EnvironmentClass.Local, BuilderIdentity "fsgg")) m1
    let m3, _ = Loop.update (Loop.Sensed sensed) m2
    Loop.update (Loop.PacksRun packs) m3

[<Tests>]
let tests =
    testList
        "SafeFailure"
        [ test "T033/T036: an unreadable pack output ⇒ InputUnavailable (exit 3), no writes, no hollow attestation" {
              let unreadable = PackedNoArtifact(SurfaceId "pkg", ArtifactUnreadable "io error", synthaticPackRun 0)
              let m, eff = driveJoin declWithPackables sensedMet [ unreadable ]

              Expect.equal m.Exit Loop.InputUnavailable "unreadable pack output ⇒ exit 3 (input-unavailable, not tool-defect/blocked)"
              Expect.isEmpty eff "no writes emitted"
              Expect.isNone m.AttestationDoc "no hollow attestation"
              Expect.isNone m.Report "no report assembled on an input-unavailable pack"
              Expect.notEqual m.Exit Loop.ToolError "distinct from a tool defect (exit 4)"
              Expect.isNonEmpty m.Diagnostics "names the offending source"
          }

          test "T035: no packable projects ⇒ vacuously satisfied + reported, no fabricated pack" {
              let m, _ = driveJoin compliantDeclaration sensedMet []
              Expect.equal m.Exit Loop.Success "vacuous pack precondition ⇒ unblocked"

              match m.PackEvidence with
              | Some p ->
                  Expect.isTrue p.NoPackableProjects "NoPackableProjects = true"
                  Expect.isEmpty p.Verdicts "no per-project verdicts fabricated"
                  Expect.isEmpty p.Runs "no pack runs fabricated"
              | None -> failtest "pack evidence assembled in the join"

              // The attestation attests no subject (nothing was packed).
              Expect.isTrue (m.Attestation |> Option.map (fun a -> List.isEmpty a.Subjects) |> Option.defaultValue false) "no attested subject"
          }

          test "T022: identical inputs ⇒ byte-identical release.json v2 + attestation.json across two runs" {
              let m1, _ = driveJoin declWithPackables sensedMet [ synthaticPacked "pkg" "1.3.0" ]
              let m2, _ = driveJoin declWithPackables sensedMet [ synthaticPacked "pkg" "1.3.0" ]
              Expect.equal m1.ReleaseDoc m2.ReleaseDoc "release.json v2 byte-identical on re-run"
              Expect.equal m1.AttestationDoc m2.AttestationDoc "attestation.json byte-identical on re-run"
          }

          test "T037: reordering the packable projects / pack outcomes ⇒ byte-identical evidence/verdict/docs" {
              let a = synthaticPacked "pkg" "1.3.0"
              let b = synthaticPacked "pkg2" "2.1.0"
              let forward, _ = driveJoin declWithTwoPackables sensedMet [ a; b ]
              let reversed, _ = driveJoin declWithTwoPackables sensedMet [ b; a ]

              Expect.equal forward.Decision reversed.Decision "release verdict is order-independent"
              Expect.equal forward.ReleaseDoc reversed.ReleaseDoc "release.json v2 order-independent"
              Expect.equal forward.AttestationDoc reversed.AttestationDoc "attestation.json order-independent"
              Expect.equal forward.Exit reversed.Exit "exit order-independent"
          } ]
