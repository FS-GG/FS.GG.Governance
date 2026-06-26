module FS.GG.Governance.ReleaseCommand.Tests.LoopTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.Provenance.Model
open FS.GG.Governance.PackEvidence.Model
open FS.GG.Governance.Attestation.Model
open FS.GG.Governance.ValidationMatrix.Model
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseDeclaration
open FS.GG.Governance.ReleaseCommand
open FS.GG.Governance.ReleaseCommand.Tests.Support

// Pure MVU transitions for the 065 three-way join (Constitution IV): `init`/`update` are pure; the F26
// cores (`evaluatePack`/`factContributions`/`evaluateRelease`/`summarize`/`assemble`/projections) run for
// real inside `update`; emitted-effect lists are asserted explicitly. The pack outcomes are fed as data
// (the interpreter edge is exercised elsewhere). No I/O.

let private req format =
    { Loop.Repo = "."
      Loop.Format = format
      Loop.ReleaseOut = "out/release.json"
      Loop.AttestationOut = "out/attestation.json" }

/// Drive init → DeclarationLoaded(Ok decl) → provenance → sensed → packs, returning the post-join model and
/// the effects emitted by the LAST message (the composition fires on the third of the trio).
let private driveJoin decl sensed packs format =
    let m0, _ = Loop.init (req format)
    let m1, _ = Loop.update (Loop.DeclarationLoaded(Ok decl)) m0
    let m2, _ = Loop.update (Loop.ProvenanceSensed(Revision "", EnvironmentClass.Local, BuilderIdentity "fsgg")) m1
    let m3, _ = Loop.update (Loop.Sensed sensed) m2
    Loop.update (Loop.PacksRun packs) m3

let private blockerKinds (m: Loop.Model) =
    m.Decision
    |> Option.map (fun d -> d.Blockers |> List.map (fun e -> e.Finding.Kind))
    |> Option.defaultValue []

[<Tests>]
let tests =
    testList
        "Loop"
        [ test "init emits LoadDeclaration + SenseProvenance" {
              let _, eff0 = Loop.init (req Loop.Text)
              Expect.equal eff0 [ Loop.LoadDeclaration "."; Loop.SenseProvenance ] "init effects"
          }

          test "DeclarationLoaded(Ok) emits SenseRelease + PackProjects (declared pack commands)" {
              let m0, _ = Loop.init (req Loop.Text)
              let m1, eff1 = Loop.update (Loop.DeclarationLoaded(Ok declWithPackables)) m0
              Expect.equal m1.Phase Loop.Loaded' "phase advanced"

              let expectedPacks =
                  declWithPackables.PackableProjects |> List.map (fun p -> p.Surface, p.PackCommand)

              Expect.equal
                  eff1
                  [ Loop.SenseRelease(declWithPackables.Layout, declWithPackables.Expectations)
                    Loop.PackProjects expectedPacks ]
                  "sense + pack-projects effects"
          }

          test "the composition fires only after ALL THREE of Sensed/PacksRun/ProvenanceSensed" {
              let m0, _ = Loop.init (req Loop.Text)
              let m1, _ = Loop.update (Loop.DeclarationLoaded(Ok declWithPackables)) m0
              // Provenance + Sensed alone do NOT fire the composition.
              let m2, _ = Loop.update (Loop.ProvenanceSensed(Revision "", EnvironmentClass.Local, BuilderIdentity "fsgg")) m1
              let m3, eff3 = Loop.update (Loop.Sensed sensedMet) m2
              Expect.isNone m3.Decision "no decision until the pack outcomes land"
              Expect.isEmpty eff3 "no writes until the trio completes"
              // The third message fires it.
              let m4, eff4 = Loop.update (Loop.PacksRun [ synthaticPacked "pkg" "1.3.0" ]) m3
              Expect.isSome m4.Decision "decision computed once all three landed"
              Expect.equal (List.length eff4) 2 "exactly the two release writes"
          }

          test "all packed+bumped ⇒ unblocked; both artifacts written; release.json is v2" {
              let m4, eff4 = driveJoin declWithPackables sensedMet [ synthaticPacked "pkg" "1.3.0" ] Loop.Json
              Expect.equal m4.Exit Loop.Success "bumped ⇒ clean basis"
              Expect.isEmpty (blockerKinds m4) "no blockers"

              match eff4 with
              | [ Loop.WriteArtifact(Loop.ReleaseArtifact, rp, rc); Loop.WriteArtifact(Loop.AttestationArtifact, ap, _) ] ->
                  Expect.equal rp "out/release.json" "release out path"
                  Expect.equal ap "out/attestation.json" "attestation out path"
                  Expect.isTrue (rc.Contains "fsgg.release/v2") "release.json bumped additively to v2"
              | other -> failtestf "expected Release then Attestation writes, got %A" other
          }

          test "a failed pack blocks: the three pack families go Unmet ⇒ blockers (FR-001)" {
              let m4, _ = driveJoin declWithPackables sensedMet [ synthaticPackFailed "pkg" 7 ] Loop.Text
              Expect.equal m4.Exit Loop.Blocked "failed pack ⇒ Blocked"
              Expect.contains (blockerKinds m4) VersionBump "version-bump blocked by the failed pack"
          }

          test "a pack at an unbumped version blocks naming VersionBump (FR-002)" {
              let m4, _ = driveJoin declWithPackables sensedMet [ synthaticPacked "pkg" "1.2.0" ] Loop.Text
              Expect.equal m4.Exit Loop.Blocked "unbumped ⇒ Blocked"
              Expect.contains (blockerKinds m4) VersionBump "version-bump is a blocker"
          }

          test "a pack at a downgraded version blocks (FR-002)" {
              let m4, _ = driveJoin declWithPackables sensedMet [ synthaticPacked "pkg" "1.1.0" ] Loop.Text
              Expect.equal m4.Exit Loop.Blocked "downgrade ⇒ Blocked"
              Expect.contains (blockerKinds m4) VersionBump "version-bump is a blocker"
          }

          test "C2: a zero-exit pack that produced no artifact (PackedNoArtifact) blocks (edge)" {
              let outcome = PackedNoArtifact(SurfaceId "pkg", NoArtifactEmitted, synthaticPackRun 0)
              let m4, _ = driveJoin declWithPackables sensedMet [ outcome ] Loop.Text
              Expect.equal m4.Exit Loop.Blocked "packed-but-no-artifact ⇒ Blocked"
              Expect.contains (blockerKinds m4) VersionBump "the pack families go Unmet"
          }

          test "C3: a first release (no declared baseline) is NOT blocked as a downgrade" {
              // compliantDeclaration declares no packable projects; feed a packed outcome with no baseline.
              let m4, _ = driveJoin compliantDeclaration sensedMet [ synthaticPacked "pkg" "0.1.0" ] Loop.Text
              // sensedMet already satisfies every family; an evidence-less (no-baseline) pack never blocks.
              Expect.equal m4.Exit Loop.Success "first release ⇒ not blocked"
          }

          test "no packable projects ⇒ vacuously satisfied (NoPackableProjects), unblocked" {
              let m4, _ = driveJoin compliantDeclaration sensedMet [] Loop.Text
              Expect.equal m4.Exit Loop.Success "vacuous pack precondition ⇒ unblocked"
              Expect.isTrue (m4.PackEvidence |> Option.map (fun p -> p.NoPackableProjects) |> Option.defaultValue false) "NoPackableProjects set"
          }

          test "C4: a declared matrix is admitted RunNow at the release boundary, never run" {
              let m4, eff4 = driveJoin declWithPackablesAndMatrix sensedMet [ synthaticPacked "pkg" "1.3.0" ] Loop.Text

              match m4.Matrix with
              | Some(RunNow m) -> Expect.equal m.Name "cross" "the declared matrix is admitted"
              | other -> failtestf "expected RunNow, got %A" other
              // No matrix-execution effect exists; only the two writes are emitted (decided, never invoked).
              Expect.equal (List.length eff4) 2 "only the two release writes — the matrix is never invoked"
          }

          test "an undeclared matrix is NotDeclared (never invented)" {
              let m4, _ = driveJoin declWithPackables sensedMet [ synthaticPacked "pkg" "1.3.0" ] Loop.Text
              Expect.equal m4.Matrix (Some NotDeclared) "no matrix declared ⇒ NotDeclared"
          }

          test "the attestation carries the compatible-shape marker and no subject for a failed pack (FR-007)" {
              let m4, _ = driveJoin declWithPackables sensedMet [ synthaticPackFailed "pkg" 7 ] Loop.Text

              match m4.Attestation with
              | Some att ->
                  Expect.equal att.Compliance CompatibleShapeNotFormalCompliance "marker always present"
                  Expect.isEmpty att.Subjects "a failed pack yields no attested subject"
              | None -> failtest "attestation assembled in the join"
          }

          test "both Wrote(Ok) acks: the FIRST schedules the summary, the second is inert" {
              let m4, _ = driveJoin declWithPackables sensedMet [ synthaticPacked "pkg" "1.3.0" ] Loop.Json
              let m5, eff5 = Loop.update (Loop.Wrote(Loop.ReleaseArtifact, Ok())) m4

              match eff5 with
              | [ Loop.EmitSummary _ ] -> Expect.equal m5.Phase Loop.Persisted "persisted after first ack"
              | other -> failtestf "expected EmitSummary after first Wrote(Ok), got %A" other

              let m6, eff6 = Loop.update (Loop.Wrote(Loop.AttestationArtifact, Ok())) m5
              Expect.isEmpty eff6 "the second ack is inert"
              Expect.equal m6.Phase Loop.Persisted "still persisted"
          }

          test "Wrote(Error) ⇒ ToolError, never Blocked" {
              let m4, _ = driveJoin declWithPackables sensedMet [ synthaticPacked "pkg" "1.3.0" ] Loop.Json
              let m5, _ = Loop.update (Loop.Wrote(Loop.ReleaseArtifact, Error "disk full")) m4
              Expect.equal m5.Exit Loop.ToolError "write failure → ToolError"
              Expect.equal m5.Phase Loop.Done "short-circuit to Done"
          }

          test "DeclarationLoaded(Error) ⇒ InputUnavailable, no further effects" {
              let m0, _ = Loop.init (req Loop.Text)
              let errResult: Result<Declaration.ReleaseDeclaration, Declaration.DeclError> = Error { Reason = "absent" }
              let m1, eff1 = Loop.update (Loop.DeclarationLoaded errResult) m0
              Expect.equal m1.Exit Loop.InputUnavailable "absent declaration ⇒ InputUnavailable"
              Expect.isEmpty eff1 "no sense/pack/write emitted"
          }

          test "exitCode maps the five classes to 0/1/2/3/4" {
              Expect.equal (Loop.exitCode Loop.Success) 0 "Success"
              Expect.equal (Loop.exitCode Loop.Blocked) 1 "Blocked"
              Expect.equal (Loop.exitCode Loop.UsageError') 2 "UsageError'"
              Expect.equal (Loop.exitCode Loop.InputUnavailable) 3 "InputUnavailable"
              Expect.equal (Loop.exitCode Loop.ToolError) 4 "ToolError"
          } ]
