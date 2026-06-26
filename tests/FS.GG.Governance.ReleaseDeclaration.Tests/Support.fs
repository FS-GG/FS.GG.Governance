module FS.GG.Governance.ReleaseDeclaration.Tests.Support

open System
open System.IO
open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.ReleaseDeclaration

// Shared REAL fixtures for the 065 ReleaseDeclaration leaf tests (Principle V — every value is real
// `release.yml` text or a literally-constructible typed value, never a mock). `Declaration.parse` reads no
// filesystem; the content is handed in as lines.

let surfaceId = SurfaceId "pkg"

// ── The F055 base declaration (rules/expectations/layout) — re-homed verbatim from the release host's
//    test fixtures (kebab-case kind tokens), the floor every additive case extends. ──

let releaseYmlAllBlocking =
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
    + "  expectedPins:\n    base: \"9.0.0\"\n"
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

/// The base declaration PLUS two declared packable projects (one with a baseline, one a first release) and
/// a declared exhaustive matrix — the additive 065 surface.
let releaseYmlWithPackablesAndMatrix =
    releaseYmlAllBlocking
    + "packableProjects:\n"
    + "  - surface: pkg\n"
    + "    packCommand:\n"
    + "      executable: dotnet\n"
    + "      arguments: [pack, src/Pkg/Pkg.fsproj, -c, Release]\n"
    + "      workingDirectory: .\n"
    + "      timeoutSeconds: 300\n"
    + "    baseline: \"1.2.0\"\n"
    + "  - surface: pkg-first\n"
    + "    packCommand:\n"
    + "      executable: dotnet\n"
    + "      arguments: [pack, src/First/First.fsproj]\n"
    + "matrix:\n"
    + "  name: cross-target\n"
    + "  cost: exhaustive\n"
    + "  dimensions: [net8, net9, net10]\n"

let ymlLines (yml: string) : string list =
    yml.Replace("\r\n", "\n").Split('\n') |> List.ofArray

let okDecl (yml: string) : Declaration.ReleaseDeclaration =
    match Declaration.parse (ymlLines yml) with
    | Ok d -> d
    | Error e -> failtestf "expected Ok, got Error: %s" e.Reason

let isErr (yml: string) : bool =
    match Declaration.parse (ymlLines yml) with
    | Error _ -> true
    | Ok _ -> false
// 074: findRepoRoot consolidated into the shared RepositoryHelpers (sln||slnx superset).
let repoRoot = FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot
