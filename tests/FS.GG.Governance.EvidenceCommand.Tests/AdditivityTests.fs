module FS.GG.Governance.EvidenceCommand.Tests.AdditivityTests

open System.IO
open System.Text.Json
open Expecto
open FS.GG.Governance.EvidenceJson
open FS.GG.Governance.EvidenceCommand.Tests.Support

// SC-005 / FR-009 — emitting evidence.json is PURELY ADDITIVE: it bumps no existing schema version and leaves
// every existing route.json / verify.json / cache-eligibility.json golden 0-byte changed. This feature adds
// only new projects (it edits no existing projection), so the guarantee is structural; this guard catches any
// accidental leakage of the new schema into a sibling golden and confirms the new schema does not collide.

/// Every committed golden JSON under a `golden`/`goldens` directory in `tests/` (excluding build output).
let private committedGoldens () =
    let testsRoot = Path.Combine(repoRoot, "tests")

    Directory.GetDirectories(testsRoot, "*", SearchOption.AllDirectories)
    |> Array.filter (fun d ->
        let name = Path.GetFileName d
        (name = "golden" || name = "goldens"))
    |> Array.collect (fun d -> Directory.GetFiles(d, "*.json", SearchOption.AllDirectories))
    |> Array.filter (fun f -> not (f.Contains("/obj/") || f.Contains("/bin/")))
    |> Array.sort

[<Tests>]
let tests =
    testList
        "Additivity"
        [ test "the new evidence schema is a fresh v1 that collides with no existing sibling schema" {
              Expect.equal EvidenceJson.schemaVersion "fsgg.evidence/v1" "new schema version"

              for existing in [ "fsgg.cache-eligibility/v1"; "fsgg.route/v2"; "fsgg.verify/v1" ] do
                  Expect.notEqual EvidenceJson.schemaVersion existing (sprintf "no collision with %s" existing)
          }

          test "no existing committed golden contains the new evidence schema (no leakage, SC-005)" {
              let goldens = committedGoldens ()
              Expect.isGreaterThan goldens.Length 0 "found committed sibling goldens to guard"

              for file in goldens do
                  let text = File.ReadAllText file
                  // Still valid JSON (not corrupted by this feature) …
                  JsonDocument.Parse(text) |> ignore
                  // … and never carrying the new evidence schema token.
                  Expect.isFalse
                      (text.Contains "fsgg.evidence/v1")
                      (sprintf "sibling golden %s must be untouched by 069" (Path.GetFileName file))
          } ]
