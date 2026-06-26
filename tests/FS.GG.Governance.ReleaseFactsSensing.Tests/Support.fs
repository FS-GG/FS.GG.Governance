module FS.GG.Governance.ReleaseFactsSensing.Tests.Support

open System
open System.IO
open Expecto
open FsCheck
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseFactsSensing
open FS.GG.Governance.ReleaseFactsSensing.Model

// Shared REAL-input builders + FsCheck generators for the F054 tests (Principle V — every value below is a
// real, literally-constructible typed value, never a mock). The pure-core tests consume hand-built
// `RecoveredEvidence` (its own real declared input, no disk); the edge tests read a REAL temp fixture
// repository through `Interpreter.realPort` (the F016 Snapshot / FreshnessSensing `withTempDir` precedent).
// No network, no registry, no publishing provider (SC-004).

// ── The governed identity + the six closed families (mirrors the source) ──

let surfaceId = SurfaceId "pkg"

/// The six closed release families, in declaration order — for the family-set assertions (SC-006).
let allFamilies: ReleaseRuleKind list =
    [ VersionBump; PackageMetadata; TemplatePins; PublishPlan; TrustedPublishing; Provenance ]

// ── Caller expectations (product-neutral, all criteria present) ──

/// A product-neutral expectation set with every family's criterion declared.
let expectations: ReleaseExpectations =
    { Surface = surfaceId
      VersionBaseline = Some "1.2.0"
      RequiredMetadataFields = Some [ "authors"; "license" ]
      ExpectedPins = Some(Map [ "base", "9.0.0" ])
      RequiredPublishPosture = Some [ "plan-present" ]
      RequiredTrustedPublishing = Some [ "oidc" ]
      RequiredProvenance = Some [ "attestation" ] }

// ── Hand-built recovered evidence (the pure-core input, no disk) ──

/// An all-satisfying recovered bundle matching `expectations` (version bumped past, all fields/pins/tokens).
let recoveredMet: RecoveredEvidence =
    { Version = Ok { Declared = "1.3.0" }
      Metadata = Ok { PresentFields = [ "authors"; "license" ] }
      Pins = Ok { Resolved = Map [ "base", "9.0.0" ] }
      PublishPlan = Ok { Observed = [ "plan-present" ] }
      TrustedPublishing = Ok { Observed = [ "oidc" ] }
      Provenance = Ok { Observed = [ "attestation" ] } }

/// An all-violating recovered bundle (each family recovered, none satisfies `expectations`).
let recoveredAllUnmet: RecoveredEvidence =
    { Version = Ok { Declared = "1.2.0" } // equals baseline ⇒ not bumped past
      Metadata = Ok { PresentFields = [ "authors" ] } // missing "license"
      Pins = Ok { Resolved = Map [ "base", "8.0.0" ] } // drifted from 9.0.0
      PublishPlan = Ok { Observed = [] } // missing "plan-present"
      TrustedPublishing = Ok { Observed = [] } // missing "oidc"
      Provenance = Ok { Observed = [] } } // missing "attestation"

// ── Fake ports backed by a recovered bundle (real records, no mock framework) ──

/// A fake `RepositoryPort` returning the given bundle verbatim.
let portOf (r: RecoveredEvidence) : Interpreter.RepositoryPort =
    { ReadVersion = fun () -> r.Version
      ReadMetadata = fun () -> r.Metadata
      ReadPins = fun () -> r.Pins
      ReadPublishPlan = fun () -> r.PublishPlan
      ReadTrustedPublishing = fun () -> r.TrustedPublishing
      ReadProvenance = fun () -> r.Provenance }

/// The all-satisfying fake port.
let metPort: Interpreter.RepositoryPort = portOf recoveredMet

// ── The F053 hand-off rule set ──

/// Build one blocking-at-release F053 rule per family (the hand-off rule set — `Release.evaluate` input).
let rulesForFamilies (families: ReleaseRuleKind list) : ReleaseRule list =
    families
    |> List.map (fun k ->
        { Kind = k
          Surface = surfaceId
          BaseSeverity = Blocking
          Maturity = BlockOnRelease })

// ── Real temp-fixture repository (the edge tests' Principle-V input) ──

/// The neutral source layout the fixtures + `realPort` share.
let layout: SourceLayout =
    { VersionPath = "version.txt"
      MetadataPath = "metadata.txt"
      PinsPath = "pins.txt"
      PublishPlanPath = "publish-plan.txt"
      TrustedPublishingPath = "trusted-publishing.txt"
      ProvenancePath = "provenance.txt" }

/// Create a disposable temp dir, run `body` against it, then delete it.
let withTempDir (body: string -> 'a) : 'a =
    let dir = Path.Combine(Path.GetTempPath(), "fsgg-relsense-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory dir |> ignore

    try
        body dir
    finally
        try
            Directory.Delete(dir, true)
        with _ ->
            ()

/// Write a file (creating parent dirs) inside a fixture.
let writeFile (dir: string) (relPath: string) (content: string) : unit =
    let full = Path.Combine(dir, relPath)

    match Path.GetDirectoryName full with
    | null -> ()
    | parent -> Directory.CreateDirectory parent |> ignore

    File.WriteAllText(full, content)

/// Write the six neutral fixture files that all SATISFY `expectations` (the all-met fixture).
let writeMetFixture (dir: string) : unit =
    writeFile dir layout.VersionPath "1.3.0\n"
    writeFile dir layout.MetadataPath "authors\nlicense\n"
    writeFile dir layout.PinsPath "base=9.0.0\n"
    writeFile dir layout.PublishPlanPath "plan-present\n"
    writeFile dir layout.TrustedPublishingPath "oidc\n"
    writeFile dir layout.ProvenancePath "attestation\n"

// ── FsCheck generators over arbitrary expectations × recovered evidence (FR-009/SC-006 property) ──

let private genTokens: Gen<string list> =
    Gen.elements [ "a"; "b"; "c"; "authors"; "license"; "oidc"; "attestation"; "plan-present" ]
    |> Gen.listOf

let private genVersion: Gen<string> =
    gen {
        let! a = Gen.choose (0, 4)
        let! b = Gen.choose (0, 4)
        let! c = Gen.choose (0, 4)
        return sprintf "%d.%d.%d" a b c
    }

let private genPins: Gen<Map<string, string>> =
    genTokens |> Gen.map (fun ts -> ts |> List.map (fun t -> t, "1.0.0") |> Map.ofList)

let private genOpt (g: Gen<'a>) : Gen<'a option> =
    Gen.oneof [ Gen.constant None; g |> Gen.map Some ]

let private genResult (genOk: Gen<'a>) : Gen<Result<'a, string>> =
    Gen.oneof [ genOk |> Gen.map Ok; Gen.constant (Error "unrecoverable: arbitrary") ]

let genExpectations: Gen<ReleaseExpectations> =
    gen {
        let! vb = genOpt genVersion
        let! mf = genOpt genTokens
        let! pins = genOpt genPins
        let! pp = genOpt genTokens
        let! tp = genOpt genTokens
        let! pr = genOpt genTokens

        return
            { Surface = surfaceId
              VersionBaseline = vb
              RequiredMetadataFields = mf
              ExpectedPins = pins
              RequiredPublishPosture = pp
              RequiredTrustedPublishing = tp
              RequiredProvenance = pr }
    }

let genRecovered: Gen<RecoveredEvidence> =
    gen {
        let! v = genResult (genVersion |> Gen.map (fun d -> { Declared = d }))
        let! m = genResult (genTokens |> Gen.map (fun fs -> { PresentFields = fs }))
        let! p = genResult (genPins |> Gen.map (fun r -> { Resolved = r }))
        let! pl = genResult (genTokens |> Gen.map (fun ts -> { Observed = ts }))
        let! tp = genResult (genTokens |> Gen.map (fun ts -> { Observed = ts }))
        let! pr = genResult (genTokens |> Gen.map (fun ts -> { Observed = ts }))

        return
            { Version = v
              Metadata = m
              Pins = p
              PublishPlan = pl
              TrustedPublishing = tp
              Provenance = pr }
    }

let genExpRec: Gen<ReleaseExpectations * RecoveredEvidence> =
    gen {
        let! e = genExpectations
        let! r = genRecovered
        return e, r
    }

type SensingArbs =
    static member ReleaseExpectations() = Arb.fromGen genExpectations
    static member RecoveredEvidence() = Arb.fromGen genRecovered
    static member ExpRec() = Arb.fromGen genExpRec

/// FsCheck config wiring the sensing arbitraries (used by the property tests).
let fsCheckConfig =
    { FsCheckConfig.defaultConfig with
        arbitrary = [ typeof<SensingArbs> ] }
// 074: findRepoRoot consolidated into the shared RepositoryHelpers (sln||slnx superset).
let repoRoot = FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot
