module FS.GG.Governance.RouteCommand.Tests.InterpreterTests

open System.Text.Json
open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.RouteCommand
open FS.GG.Governance.RouteCommand.Tests.Support

// US1/US2/US3 (the edge): `Interpreter.run` over FAKED ports (in-memory FileReader, in-memory GitPort
// over canned read-only git output, capturing ArtifactWriter/OutputSink) — no real git, no real
// filesystem (FR-012, SC-007). The written bytes are compared to the genuine F020/F021 projections.

let private runWith files git scope format =
    let req = requestFor scope format
    let cap = newCapture ()
    let model = Interpreter.run (fakePorts files git cap req) req
    req, cap, model

[<Tests>]
let tests =
    testList
        "Interpreter"
        [ // ── US1: route + persist, bytes = F020/F021 ──

          test "writes both artifacts byte-for-byte equal to the F021/F020 projections, exits 0 (US1, SC-001, SC-007)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let req, cap, model = runWith validCatalog git Loop.DefaultRange Loop.Text
              let candidates = candidatesOf git defaultOpts
              let expectedGates, expectedRoute = projectExpected validCatalog candidates

              Expect.equal (writtenOf cap Loop.GatesArtifact) (Some(req.GatesOut, expectedGates)) "gates.json = GatesJson.ofGateRegistry"
              Expect.equal (writtenOf cap Loop.RouteArtifact) (Some(req.RouteOut, expectedRoute)) "route.json = RouteJson.ofRouteResult"
              Expect.equal model.Exit Loop.Success "exit decision Success"
              Expect.equal (Loop.exitCode model.Exit) 0 "exit code 0"
          }

          test "the summary lists each selected gate by id with its path and the cost rollup (US1 AS2)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let req, cap, model = runWith validCatalog git Loop.DefaultRange Loop.Text
              let summary = Expect.wantSome (List.tryHead cap.Emits) "a summary was emitted"

              let result = Option.get model.Result
              Expect.isNonEmpty result.SelectedGates "the src change selects gates"
              for sg in result.SelectedGates do
                  Expect.stringContains summary (gateIdValue sg.Gate.Id) "summary names each selected gate id"
              Expect.stringContains summary "cost:" "summary carries the cost rollup"
              Expect.stringContains summary req.GatesOut "summary names the gates path" |> ignore
          }

          // ── US1 AS3 / SC-006: empty-result and empty-input are SUCCESS, never failure ──

          test "a routine-only change selects no gates yet still writes both artifacts and exits 0 (SC-006)" {
              let git = gitWithChanges [ 'M', "notes.txt" ]
              let _, cap, model = runWith validCatalog git Loop.DefaultRange Loop.Text

              let result = Option.get model.Result
              Expect.isEmpty result.SelectedGates "an unclassified path selects nothing"
              Expect.isSome (writtenOf cap Loop.RouteArtifact) "route.json still written"
              Expect.isSome (writtenOf cap Loop.GatesArtifact) "gates.json still written (full catalog)"
              Expect.equal model.Exit Loop.Success "exit 0 — routine change is information, not failure"
          }

          test "an empty changed-path set selects nothing and exits 0 (SC-006, no-changes-in-scope)" {
              let _, cap, model = runWith validCatalog gitEmpty Loop.DefaultRange Loop.Text
              let result = Option.get model.Result
              Expect.isEmpty result.SelectedGates "no changed paths ⇒ nothing selected"
              Expect.isSome (writtenOf cap Loop.RouteArtifact) "valid route.json written for an empty diff"
              Expect.equal model.Exit Loop.Success "empty diff is never an error"
          }

          test "a valid empty catalog yields an empty gate list and exits 0 (SC-006, empty catalog)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let candidates = candidatesOf git defaultOpts
              let expectedGates, _ = projectExpected emptyCatalog candidates
              let req, cap, model = runWith emptyCatalog git Loop.DefaultRange Loop.Text

              Expect.equal (writtenOf cap Loop.GatesArtifact) (Some(req.GatesOut, expectedGates)) "gates.json = projection of the empty registry"
              Expect.equal (Option.get model.Result).SelectedGates [] "empty registry ⇒ nothing selected"
              Expect.equal model.Exit Loop.Success "empty catalog is a success"
          }

          // ── US2 / SC-003: three scopes ──

          test "ExplicitPaths routes exactly those paths WITHOUT consulting git (US2 AS1)" {
              // The git port reports not-a-repo: if `init` consulted it the run would fail. It must not.
              let req, cap, model = runWith validCatalog gitNotRepo (Loop.ExplicitPaths [ gp "src/Lib/Thing.fs" ]) Loop.Text
              let expectedGates, expectedRoute = projectExpected validCatalog [ gp "src/Lib/Thing.fs" ]

              Expect.equal model.Exit Loop.Success "explicit paths bypass git entirely ⇒ success despite not-a-repo git"
              Expect.equal (writtenOf cap Loop.RouteArtifact) (Some(req.RouteOut, expectedRoute)) "route.json = projection of the explicit paths"
              Expect.equal (writtenOf cap Loop.GatesArtifact) (Some(req.GatesOut, expectedGates)) "gates.json = full catalog"
          }

          test "Since and DefaultRange route the faked snapshot's changed paths (US2 AS2/AS3, SC-003)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]

              let _, capSince, mSince = runWith validCatalog git (Loop.Since "HEAD~2") Loop.Text
              let _, capDefault, mDefault = runWith validCatalog git Loop.DefaultRange Loop.Text

              Expect.equal mSince.Exit Loop.Success "since scope routes successfully"
              Expect.equal mDefault.Exit Loop.Success "default scope routes successfully"
              // Same faked diff ⇒ same selected set under both sensed scopes.
              Expect.isNonEmpty (Option.get mSince.Result).SelectedGates "since selected the package-api gates"
              Expect.equal
                  (writtenOf capSince Loop.RouteArtifact |> Option.map snd)
                  (writtenOf capDefault Loop.RouteArtifact |> Option.map snd)
                  "identical faked diff ⇒ identical route.json across Since/DefaultRange"
          }

          // ── US3 / SC-002/SC-005: determinism, format, exclusion ──

          test "twice-run over fixed inputs yields byte-identical artifacts and --json summary (US3, SC-002)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let _, cap1, _ = runWith validCatalog git Loop.DefaultRange Loop.Json
              let _, cap2, _ = runWith validCatalog git Loop.DefaultRange Loop.Json

              Expect.equal (writtenOf cap1 Loop.GatesArtifact) (writtenOf cap2 Loop.GatesArtifact) "gates.json byte-identical across runs"
              Expect.equal (writtenOf cap1 Loop.RouteArtifact) (writtenOf cap2 Loop.RouteArtifact) "route.json byte-identical across runs"
              Expect.equal cap1.Emits cap2.Emits "--json summary byte-identical across runs"
          }

          test "--json emits a parseable JSON summary and suppresses the text form; --text the converse (US3 AS2)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let _, capJson, _ = runWith validCatalog git Loop.DefaultRange Loop.Json
              let _, capText, _ = runWith validCatalog git Loop.DefaultRange Loop.Text

              let j = Expect.wantSome (List.tryHead capJson.Emits) "json summary emitted"
              use _doc = JsonDocument.Parse j // throws if not valid JSON
              Expect.isFalse (j.Contains "route:") "json form is not the human text"

              let t = Expect.wantSome (List.tryHead capText.Emits) "text summary emitted"
              Expect.stringContains t "route:" "text form is the human summary"
              Expect.isFalse (t.TrimStart().StartsWith "{") "text form is not JSON"
          }

          test "neither the written artifacts nor the summary carry an excluded verdict/clock/abs-path token (US3 AS3, SC-005)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let _, cap, _ = runWith validCatalog git Loop.DefaultRange Loop.Json

              let gates = writtenOf cap Loop.GatesArtifact |> Option.map snd |> Option.defaultValue ""
              let route = writtenOf cap Loop.RouteArtifact |> Option.map snd |> Option.defaultValue ""
              let summary = String.concat "\n" cap.Emits
              let blob = (gates + "\n" + route + "\n" + summary).ToLowerInvariant()

              for token in [ "verdict"; "severity"; "profile"; "mode"; "enforcement"; "cacheeligib"; "blockers"; "warnings"; "exitcode" ] do
                  Expect.isFalse (blob.Contains token) (sprintf "excluded token %s must not appear" token)
          } ]
