module FS.GG.Governance.RefreshCommand.Tests.CurrencyTests

open Expecto
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.RefreshJson.RefreshModel
open FS.GG.Governance.RefreshCommand
open FS.GG.Governance.RefreshCommand.Tests.Support

// The pure currency decision inside `update`, reusing F029 `FreshnessKey` with revisions held EQUAL
// (research D1). Driven in `--dry-run` so a stale view surfaces as `WouldRegenerate drifted` (the same
// drifted categories a write run computes) with NO disk and NO regeneration effect.

let private oneViewManifest = parseYml refreshYmlOneView
let private dryRequest = { requestFor "." with DryRun = true }

/// Drive init → ManifestLoaded → Sensed → RecordedRead and return the decided "doc" status.
let private decide (recorded: (ArtifactHash list * GeneratorVersion) option) (sensed: Result<ArtifactHash list * GeneratorVersion, string>) : CurrencyStatus =
    let m0, _ = Loop.init dryRequest
    let m1, _ = Loop.update (Loop.ManifestLoaded(Ok oneViewManifest)) m0
    let m2, _ = Loop.update (Loop.Sensed("doc", sensed)) m1
    let m3, _ = Loop.update (Loop.RecordedRead("doc", recorded)) m2
    (m3.Views |> List.find (fun v -> v.Entry.ViewId = "doc")).Status

[<Tests>]
let tests =
    testList
        "Currency"
        [ test "matching digests + generator ⇒ Current (by digest, SC-002)" {
              let status = decide (Some(digestsOf [ "d1" ], GeneratorVersion "g1")) (Ok(digestsOf [ "d1" ], GeneratorVersion "g1"))
              Expect.equal status Current "recorded matches sensed ⇒ current"
          }

          test "a changed source digest ⇒ stale, drifted = [CoveredArtifactsCat]" {
              let status = decide (Some(digestsOf [ "d1" ], GeneratorVersion "g1")) (Ok(digestsOf [ "d2" ], GeneratorVersion "g1"))
              Expect.equal status (WouldRegenerate [ CoveredArtifactsCat ]) "only the covered-artifacts category drifted"
          }

          test "a changed generator version ⇒ stale, drifted = [GeneratorVersionCat]" {
              let status = decide (Some(digestsOf [ "d1" ], GeneratorVersion "g1")) (Ok(digestsOf [ "d1" ], GeneratorVersion "g2"))
              Expect.equal status (WouldRegenerate [ GeneratorVersionCat ]) "only the generator-version category drifted"
          }

          test "no recorded provenance (first generation) ⇒ stale" {
              let status = decide None (Ok(digestsOf [ "d1" ], GeneratorVersion "g1"))
              match status with
              | WouldRegenerate _ -> ()
              | other -> failtestf "expected first-generation stale, got %A" other
          }

          test "a source whose digest cannot be sensed ⇒ StaleUnresolved, NEVER Current (FR-010)" {
              let status = decide (Some(digestsOf [ "d1" ], GeneratorVersion "g1")) (Error "source not found: src.txt")
              match status with
              | StaleUnresolved reason -> Expect.stringContains reason "src.txt" "reason names the offending source"
              | other -> failtestf "expected StaleUnresolved, got %A" other
          } ]
