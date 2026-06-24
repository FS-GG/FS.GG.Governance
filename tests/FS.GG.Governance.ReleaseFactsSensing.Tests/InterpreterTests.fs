module FS.GG.Governance.ReleaseFactsSensing.Tests.InterpreterTests

open System.IO
open Expecto
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseFactsSensing
open FS.GG.Governance.ReleaseFactsSensing.Model
open FS.GG.Governance.ReleaseFactsSensing.Tests.Support

// US3 (acc. 1, FR-004/FR-006/FR-009, SC-002): the edge `realPort`/`gather`/`senseRelease` over a REAL temp
// fixture repository (Principle V — real files, no fake filesystem). An absent/unreadable/unparseable source
// or a thrown read ⇒ `Unrecoverable` (never `Met`, never a crash), with all six families always returned.

[<Tests>]
let tests =
    testList
        "InterpreterTests"
        [ test "all-satisfying real fixture ⇒ all six Met" {
              withTempDir (fun dir ->
                  writeMetFixture dir
                  let sensed = Interpreter.senseRelease (Interpreter.realPort dir layout) expectations

                  Expect.equal sensed.Facts.States.Count 6 "six families"
                  Expect.isTrue (sensed.Facts.States |> Map.forall (fun _ s -> s = Met)) "every family Met from real files")
          }

          test "deleted source + corrupted-unparseable source ⇒ both Unrecoverable, all six still returned" {
              withTempDir (fun dir ->
                  writeMetFixture dir
                  // Remove the provenance source entirely (absent).
                  File.Delete(Path.Combine(dir, layout.ProvenancePath))
                  // Corrupt the pins source to an unparseable line (no `name=version`).
                  writeFile dir layout.PinsPath "this-line-has-no-equals-sign\n"

                  let sensed = Interpreter.senseRelease (Interpreter.realPort dir layout) expectations

                  Expect.equal sensed.Facts.States.Count 6 "all six families still present (FR-009)"
                  Expect.equal sensed.Facts.States.[Provenance] Unrecoverable "absent source ⇒ Unrecoverable, not Met"
                  Expect.equal sensed.Facts.States.[TemplatePins] Unrecoverable "unparseable source ⇒ Unrecoverable, not Met"
                  // The untouched families stay Met — failure is contained.
                  Expect.equal sensed.Facts.States.[VersionBump] Met "untouched version still Met")
          }

          test "all sources missing ⇒ all six Unrecoverable and a successful result (edge case)" {
              withTempDir (fun dir ->
                  // Empty dir — no fixture files written at all.
                  let sensed = Interpreter.senseRelease (Interpreter.realPort dir layout) expectations

                  Expect.equal sensed.Facts.States.Count 6 "six families even when nothing is readable"

                  Expect.isTrue
                      (sensed.Facts.States |> Map.forall (fun _ s -> s = Unrecoverable))
                      "every family Unrecoverable (not a sensing failure, not a crash)")
          }

          test "a port read that throws ⇒ gather reifies Error ⇒ that family Unrecoverable (never a crash)" {
              let throwingPort =
                  { metPort with ReadProvenance = fun () -> failwith "boom: simulated read failure" }

              // gather must catch and reify, so senseRelease never throws.
              let sensed = Interpreter.senseRelease throwingPort expectations

              Expect.equal sensed.Facts.States.Count 6 "six families despite a throwing read"
              Expect.equal sensed.Facts.States.[Provenance] Unrecoverable "thrown read ⇒ Unrecoverable"
              Expect.equal sensed.Facts.States.[VersionBump] Met "other families unaffected"

              // gather alone is also total/safe and surfaces the Error.
              let bundle = Interpreter.gather throwingPort
              Expect.isTrue (match bundle.Provenance with Error _ -> true | Ok _ -> false) "thrown read reified as Error"
          } ]
