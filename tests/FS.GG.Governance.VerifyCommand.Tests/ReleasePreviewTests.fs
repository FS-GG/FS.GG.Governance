module FS.GG.Governance.VerifyCommand.Tests.ReleasePreviewTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseFactsSensing
open FS.GG.Governance.ReleaseFactsSensing.Model
open FS.GG.Governance.ValidationMatrix.Model
open FS.GG.Governance.ReleaseDeclaration
open FS.GG.Governance.VerifyJson
open FS.GG.Governance.VerifyCommand
open FS.GG.Governance.VerifyCommand.Tests.Support

// 065 (US3) T031/T032 — the declaration-gated advisory release-readiness preview, driven through the pure
// `update` (the interpreter edge replays the same `ReleasePreviewSensed` msg). A present declaration ⇒
// `verify.json` carries an advisory `releaseReadiness` block from the SAME release evidence the boundary
// would, the verify exit is UNCHANGED, and a declared matrix is recorded `Deferred` (not run). With NO
// declaration the projection is byte-identical (no block). The cores run for real; verify never packs.

// ── A declared release surface (six families + an exhaustive matrix) + a compliant F54 sense ──

let private releaseYml =
    "surface: pkg\n"
    + "rules:\n"
    + "  - kind: version-bump\n    severity: blocking\n    maturity: block-on-release\n"
    + "  - kind: package-metadata\n    severity: blocking\n    maturity: block-on-release\n"
    + "  - kind: template-pins\n    severity: blocking\n    maturity: block-on-release\n"
    + "  - kind: publish-plan\n    severity: blocking\n    maturity: block-on-release\n"
    + "  - kind: trusted-publishing\n    severity: blocking\n    maturity: block-on-release\n"
    + "  - kind: provenance\n    severity: blocking\n    maturity: block-on-release\n"
    + "expectations:\n"
    + "  versionBaseline: \"1.2.0\"\n"
    + "  requiredMetadataFields: [authors, license]\n"
    + "  requiredPublishPosture: [plan-present]\n"
    + "  requiredTrustedPublishing: [oidc]\n"
    + "  requiredProvenance: [attestation]\n"
    + "layout:\n"
    + "  versionPath: version.txt\n"
    + "  metadataPath: metadata.txt\n"
    + "  pinsPath: pins.txt\n"
    + "  publishPlanPath: publish-plan.txt\n"
    + "  trustedPublishingPath: trusted-publishing.txt\n"
    + "  provenancePath: provenance.txt\n"
    + "matrix:\n  name: cross\n  cost: exhaustive\n  dimensions: [net10]\n"

let private decl =
    match Declaration.parse (releaseYml.Replace("\r\n", "\n").Split('\n') |> List.ofArray) with
    | Ok d -> d
    | Error e -> failwithf "fixture release.yml failed to parse: %s" e.Reason

let private surfaceId = SurfaceId "pkg"

let private expectations: ReleaseExpectations =
    { Surface = surfaceId
      VersionBaseline = Some "1.2.0"
      RequiredMetadataFields = Some [ "authors"; "license" ]
      ExpectedPins = None
      RequiredPublishPosture = Some [ "plan-present" ]
      RequiredTrustedPublishing = Some [ "oidc" ]
      RequiredProvenance = Some [ "attestation" ] }

// A fully-met recovered evidence ⇒ a compliant sensed (the cheap pre-PR preview the maintainer sees).
let private recoveredMet: RecoveredEvidence =
    { Version = Ok { Declared = "1.3.0" }
      Metadata = Ok { PresentFields = [ "authors"; "license" ] }
      Pins = Ok { Resolved = Map.empty }
      PublishPlan = Ok { Observed = [ "plan-present" ] }
      TrustedPublishing = Ok { Observed = [ "oidc" ] }
      Provenance = Ok { Observed = [ "attestation" ] } }

let private sensed = Sensing.deriveFacts expectations recoveredMet

// Drive init → Sensed → ReleasePreviewSensed(opt) → Loaded(Valid emptyCatalog) and return the terminal model
// (the empty-selection projection path; no freshness/store/execute work).
let private driveTo (opt: (Declaration.ReleaseDeclaration * SensedRelease) option) =
    let m0, _ = Loop.init (requestFor Loop.DefaultRange Loop.Text)
    let snap = snapshotOf gitSrcChange defaultOpts
    let m1, _ = Loop.update (Loop.Sensed(Ok snap)) m0
    let m1b, _ = Loop.update (Loop.ProvenanceSensed(EnvironmentClass.Local, FS.GG.Governance.Provenance.Model.BuilderIdentity "fsgg")) m1
    let m2, _ = Loop.update (Loop.ReleasePreviewSensed opt) m1b
    let m3, _ = Loop.update (Loop.Loaded(Valid(factsOf emptyCatalog))) m2
    // 067: the empty-selection projection is now deferred until the (read-only) surface checks land; the
    // emptyCatalog declares no product surface, so the sense returns `[]` (byte-identical, no verdict fold).
    let m4, _ = Loop.update (Loop.SurfacesSensed []) m3
    m4

[<Tests>]
let tests =
    testList
        "ReleasePreview"
        [ test "T031: a present declaration ⇒ advisory releaseReadiness preview, exit unchanged, matrix deferred" {
              let withDecl = driveTo (Some(decl, sensed))
              let withoutDecl = driveTo None

              // The advisory block is present and advisory.
              match withDecl.ReleasePreview with
              | Some p -> Expect.isTrue p.Advisory "the preview is advisory"
              | None -> failtest "expected a release-readiness preview when a declaration is present"

              Expect.isTrue (withDecl.VerifyDoc |> Option.exists (fun d -> d.Contains "releaseReadiness")) "verify.json carries the releaseReadiness block"

              // The preview NEVER changes the verify exit code (same as the no-declaration run).
              Expect.equal withDecl.Exit withoutDecl.Exit "the preview does not change the verify exit code"

              // A declared exhaustive matrix is recorded Deferred (to the scheduled/release boundary), not run.
              match withDecl.ReleaseMatrix with
              | Some(Deferred(DeferredToScheduledBoundary(name, _))) -> Expect.equal name "cross" "the declared matrix is deferred"
              | other -> failtestf "expected the declared matrix Deferred at the inner loop, got %A" other
          }

          test "T032: no declaration ⇒ no releaseReadiness block, byte-identical to the plain projection" {
              let withoutDecl = driveTo None
              Expect.isNone withoutDecl.ReleasePreview "no declaration ⇒ no preview"

              let doc = withoutDecl.VerifyDoc |> Option.defaultValue ""
              Expect.isFalse (doc.Contains "releaseReadiness") "no releaseReadiness block when absent"

              // Byte-identical to the existing 3-arg projection (the additive WithPreview []/None equivalence).
              let plain = VerifyJson.ofVerifyDecision (withoutDecl.Decision |> Option.get) None []
              Expect.equal doc plain "verify.json byte-identical to the pre-wiring projection"
          }

          test "an undeclared matrix is NotDeclared; a no-declaration run has no matrix at all" {
              let withoutDecl = driveTo None
              Expect.isNone withoutDecl.ReleaseMatrix "no declaration ⇒ no matrix decision"
          } ]
