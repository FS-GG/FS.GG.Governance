module FS.GG.Governance.ReleaseCommand.Tests.MergeableTests

open System.IO
open System.Text.Json
open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.ReleaseCommand
open FS.GG.Governance.ReleaseCommand.Tests.Support

// 066 US2 (closes 065 T023): the release boundary is genuinely DISTINCT from the ship/merge boundary. For
// ONE product, the REAL `fsgg ship` host exits 0 (mergeable) while the REAL `fsgg release` host exits 1
// (not releasable) on a distinct basis, and the publish-plan / trusted-publishing / template-pin
// publication preconditions surface as named entries in `release.json` v2 in the correct satisfied/unmet
// states. Both producers are the real hosts (no faked ship verdict); no product code changes.

// ── A combined fixture repo: a `.fsgg` ship catalog (empty checks ⇒ ship is vacuously clean over an
//    explicit governed path, no gate execution, no git) PLUS the release `.fsgg/release.yml` + sources. ──

let private projectYml =
    "schemaVersion: 1\n"
    + "id: my-product\n"
    + "governedRoot: .\n"
    + "domains:\n  - package-api\n"
    + "packageSurfaces:\n  - src\n"
    + "policyRef: .fsgg/policy.yml\n"
    + "capabilitiesRef: .fsgg/capabilities.yml\n"

let private capabilitiesYml =
    "schemaVersion: 2\n"
    + "domains:\n  - package-api\n"
    + "pathMap:\n  - glob: \"src/**\"\n    capability: package-api\n"
    + "checks: []\n"

let private policyYml =
    "schemaVersion: 1\n"
    + "defaultProfile: standard\n"
    + "profiles:\n  - light\n  - standard\n  - strict\n"
    + "branchPolicy:\n  pattern: \"main\"\n  requirePr: true\n"
    + "reviewBudget:\n  maxReviews: 3\n"

let private toolingYml =
    "schemaVersion: 1\n"
    + "commands: []\n"
    + "environmentClasses:\n  - local\n  - ci\n"

/// Write the six release sources, all satisfying the expectations EXCEPT the publish plan, which is PRESENT
/// but does not declare the required `plan-present` posture ⇒ the publish-plan publication precondition is
/// UNMET with a non-empty `missing` (a present-but-insufficient precondition, mergeable-but-not-releasable).
let private writeUnmetPublishPlan (dir: string) : unit =
    writeFile dir layout.VersionPath "1.3.0\n"
    writeFile dir layout.MetadataPath "authors\nlicense\n"
    writeFile dir layout.PinsPath "base=9.0.0\n"
    writeFile dir layout.PublishPlanPath "draft-not-ready\n" // present but NOT `plan-present` ⇒ missing it
    writeFile dir layout.TrustedPublishingPath "oidc\n"
    writeFile dir layout.ProvenancePath "attestation\n"

/// Materialize the combined fixture and run `body repo`, deleting it afterward.
let private withMergeableRepo (writeSources: string -> unit) (body: string -> 'a) : 'a =
    withTempDir (fun dir ->
        writeFile dir (Path.Combine(".fsgg", "project.yml")) projectYml
        writeFile dir (Path.Combine(".fsgg", "capabilities.yml")) capabilitiesYml
        writeFile dir (Path.Combine(".fsgg", "policy.yml")) policyYml
        writeFile dir (Path.Combine(".fsgg", "tooling.yml")) toolingYml
        writeFile dir (Path.Combine(".fsgg", "release.yml")) releaseYmlAllBlocking
        writeFile dir (Path.Combine("src", "Lib", "Thing.fs")) "module Thing\nlet v = 1\n"
        writeSources dir
        body dir)

/// Run the REAL `fsgg ship` host over an explicit governed path (no git, no gate execution), returning its
/// process exit code.
let private shipExit (repo: string) : int =
    match FS.GG.Governance.ShipCommand.Loop.parse [ "ship"; "--repo"; repo; "--paths"; "src/Lib/Thing.fs" ] with
    | Error e -> failtestf "ship parse failed: %A" e
    | Ok req ->
        let ports =
            { FS.GG.Governance.ShipCommand.Interpreter.realPorts repo with
                Out = ignore }

        let model = FS.GG.Governance.ShipCommand.Interpreter.run ports req
        FS.GG.Governance.ShipCommand.Loop.exitCode model.Exit

/// Run the REAL `fsgg release` host over the repo (no packable projects ⇒ no `dotnet pack`), returning its
/// terminal model and the written `release.json` bytes.
let private runRelease (repo: string) : Loop.Model * string =
    let releaseOut = Path.Combine(repo, "readiness", "release.json")

    let req =
        { Loop.Repo = repo
          Loop.Format = Loop.Json
          Loop.ReleaseOut = releaseOut
          Loop.AttestationOut = Path.Combine(repo, "readiness", "attestation.json") }

    let ports = { Interpreter.realPorts repo with Out = ignore }
    let model = Interpreter.run ports req
    model, File.ReadAllText releaseOut

// ── JSON inspection helpers over the produced `release.json` v2 ──

let private missingCount (releaseJson: string) (family: string) : int =
    use doc = JsonDocument.Parse releaseJson
    let evidence = doc.RootElement.GetProperty "evidence"
    let fam = evidence.GetProperty family
    let arr = if family = "pins" then fam.GetProperty "drifted" else fam.GetProperty "missing"
    arr.GetArrayLength()

let private hasEvidenceFamily (releaseJson: string) (family: string) : bool =
    use doc = JsonDocument.Parse releaseJson
    let mutable ev = Unchecked.defaultof<JsonElement>

    doc.RootElement.TryGetProperty("evidence", &ev)
    && (let mutable f = Unchecked.defaultof<JsonElement> in ev.TryGetProperty(family, &f))

/// The reason carried by the named rule (the unmet precondition's named reason, not a bare verdict).
let private ruleReason (releaseJson: string) (kindToken: string) : string option =
    use doc = JsonDocument.Parse releaseJson
    let rules = doc.RootElement.GetProperty "rules"

    rules.EnumerateArray()
    |> Seq.tryPick (fun r ->
        let mutable k = Unchecked.defaultof<JsonElement>
        let mutable reason = Unchecked.defaultof<JsonElement>

        if
            r.TryGetProperty("kind", &k)
            && k.GetString() = kindToken
            && r.TryGetProperty("reason", &reason)
        then
            Some(
                match reason.GetString() with
                | null -> ""
                | s -> s
            )
        else
            None)

[<Tests>]
let tests =
    testList
        "Mergeable"
        [ test "boundary distinction: a mergeable product ships (exit 0) but is not releasable (exit 1, distinct basis)" {
              // FR-003, SC-002, AS-1.
              withMergeableRepo writeUnmetPublishPlan (fun repo ->
                  let ship = shipExit repo
                  Expect.equal ship 0 "the real `fsgg ship` host merges clean (exit 0)"

                  let model, releaseJson = runRelease repo
                  Expect.equal model.Exit Loop.Blocked "the real `fsgg release` host blocks"
                  Expect.equal (Loop.exitCode model.Exit) 1 "release exits 1 — distinct from the clean ship"

                  // The concrete release exit-code basis recorded in release.json v2 (the unmet-precondition
                  // basis), not merely 'the two exit codes differ'.
                  Expect.stringContains releaseJson "\"exitCodeBasis\":\"blocked\"" "release records the blocked basis"
                  Expect.stringContains releaseJson "\"verdict\":\"fail\"" "release records a fail verdict")
          }

          test "named preconditions — unmet: publishPlan/trustedPublishing/pins appear; the failing one is unmet with a named reason" {
              // FR-004, AS-2.
              withMergeableRepo writeUnmetPublishPlan (fun repo ->
                  let _, releaseJson = runRelease repo

                  for family in [ "publishPlan"; "trustedPublishing"; "pins" ] do
                      Expect.isTrue
                          (hasEvidenceFamily releaseJson family)
                          (sprintf "%s appears as a named precondition entry" family)

                  Expect.isGreaterThan
                      (missingCount releaseJson "publishPlan")
                      0
                      "the publish-plan precondition is UNMET (missing is non-empty)"

                  // The satisfied siblings stay satisfied (only publish-plan is unmet here).
                  Expect.equal (missingCount releaseJson "trustedPublishing") 0 "trusted-publishing stays satisfied"
                  Expect.equal (missingCount releaseJson "pins") 0 "template-pins stay satisfied"

                  match ruleReason releaseJson "publishPlan" with
                  | Some reason -> Expect.isNotEmpty reason "the unmet publish-plan carries a named reason, not a bare verdict"
                  | None -> failtest "expected a publishPlan rule with a reason in release.json")
          }

          test "named preconditions — satisfied: a fully-releasable product ships AND releases clean, all three satisfied" {
              // FR-004, SC-002, AS-3.
              withMergeableRepo writeMetSources (fun repo ->
                  Expect.equal (shipExit repo) 0 "the fully-releasable product ships clean"

                  let model, releaseJson = runRelease repo
                  Expect.equal model.Exit Loop.Success "the fully-releasable product releases clean (exit 0)"
                  Expect.equal (Loop.exitCode model.Exit) 0 "release exits 0"

                  for family in [ "publishPlan"; "trustedPublishing"; "pins" ] do
                      Expect.equal
                          (missingCount releaseJson family)
                          0
                          (sprintf "%s is in a satisfied state (nothing missing/drifted)" family))
          } ]
