module FS.GG.Governance.PackageChecks.Tests.SensorTests

open System
open System.IO
open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.PackageChecks
open FS.GG.Governance.PackageChecks.Model
open FS.GG.Governance.PackageChecks.Tests.Support

module SC = FS.GG.Governance.SurfaceChecks.Model

// Real on-disk fixtures: a temp repo with a committed `.fsi` surface, its `.baseline`, and FSI transcripts.
// Transcripts run through the REAL F051 GateExecution port (Principle V — a real `dotnet fsi` process).
let private withTempRepo (body: string -> 'a) : 'a =
    let dir = Path.Combine(Path.GetTempPath(), "fsgg-pkg-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory(Path.Combine(dir, "src")) |> ignore

    try
        body dir
    finally
        try
            Directory.Delete(dir, true)
        with _ ->
            ()

let private fsiText =
    "module Foo\nval a: int\nval b: string\n"

let private writeSurface (repo: string) = File.WriteAllText(Path.Combine(repo, "src", "Foo.fsi"), fsiText)

let private realPort (repo: string) =
    Interpreter.realPort repo FS.GG.Governance.GateExecution.Interpreter.realPort

let private req = requestFor "pkg" "src/Foo.fsi" (Some "pkg-evidence")

[<Tests>]
let tests =
    testList
        "PackageChecks.sensor"
        [ test "committed unchanged surface ⇒ BaselineMatches" {
              withTempRepo (fun repo ->
                  writeSurface repo
                  // Baseline holds exactly the regenerated tokens.
                  File.WriteAllText(Path.Combine(repo, "src", "Foo.fsi.baseline"), "module Foo\nval a: int\nval b: string\n")
                  let facts = Interpreter.sensePackage (realPort repo) req
                  Expect.equal facts.Baseline BaselineMatches "unchanged surface ⇒ no drift")
          }

          test "changed public surface ⇒ BaselineDrift naming the added member" {
              withTempRepo (fun repo ->
                  writeSurface repo
                  // Committed baseline is missing `val b: string` ⇒ regeneration adds it.
                  File.WriteAllText(Path.Combine(repo, "src", "Foo.fsi.baseline"), "module Foo\nval a: int\n")
                  let facts = Interpreter.sensePackage (realPort repo) req

                  match facts.Baseline with
                  | BaselineDrift(added, removed) ->
                      Expect.contains added "val b: string" "drift names the added member"
                      Expect.isEmpty removed "nothing removed"
                  | other -> failtestf "expected BaselineDrift, got %A" other)
          }

          test "absent baseline ⇒ baseline written + BaselineAbsent (never a silent pass)" {
              withTempRepo (fun repo ->
                  writeSurface repo
                  let baselineFile = Path.Combine(repo, "src", "Foo.fsi.baseline")
                  Expect.isFalse (File.Exists baselineFile) "no baseline before sensing"
                  let facts = Interpreter.sensePackage (realPort repo) req

                  match facts.Baseline with
                  | BaselineAbsent _ -> ()
                  | other -> failtestf "expected BaselineAbsent, got %A" other

                  Expect.isTrue (File.Exists baselineFile) "baseline generated and written on first run")
          }

          test "passing transcript ⇒ TranscriptPasses (real dotnet fsi)" {
              withTempRepo (fun repo ->
                  writeSurface repo
                  File.WriteAllText(Path.Combine(repo, "src", "Foo.fsi.baseline"), fsiText)
                  let tdir = Path.Combine(repo, "src", "transcripts")
                  Directory.CreateDirectory tdir |> ignore
                  File.WriteAllText(Path.Combine(tdir, "pass.fsx"), "printfn \"ok\"\n")
                  let facts = Interpreter.sensePackage (realPort repo) req
                  Expect.hasLength facts.Transcripts 1 "one transcript sensed"
                  Expect.equal (List.head facts.Transcripts).Outcome TranscriptPasses "valid transcript passes")
          }

          test "broken transcript ⇒ TranscriptCompileFailed (real dotnet fsi)" {
              withTempRepo (fun repo ->
                  writeSurface repo
                  File.WriteAllText(Path.Combine(repo, "src", "Foo.fsi.baseline"), fsiText)
                  let tdir = Path.Combine(repo, "src", "transcripts")
                  Directory.CreateDirectory tdir |> ignore
                  // A genuine F# syntax error ⇒ non-zero exit.
                  File.WriteAllText(Path.Combine(tdir, "broken.fsx"), "let x =\n")
                  let facts = Interpreter.sensePackage (realPort repo) req
                  let outcome = (List.head facts.Transcripts).Outcome

                  match outcome with
                  | TranscriptCompileFailed _ -> ()
                  | other -> failtestf "expected TranscriptCompileFailed, got %A" other)
          }

          test "unlocatable transcript path ⇒ TranscriptUnlocatable (FR-012 exception mapping)" {
              withTempRepo (fun repo ->
                  writeSurface repo
                  File.WriteAllText(Path.Combine(repo, "src", "Foo.fsi.baseline"), fsiText)
                  let baseReal = realPort repo
                  // A port whose discovery yields a path that does not exist exercises the Error⇒Unlocatable
                  // mapping (FR-012). Only `ListTranscripts` is overridden; every other read is the real port.
                  let port =
                      { baseReal with
                          ListTranscripts = fun _ -> Ok [ normalizePath "src/transcripts/ghost.fsx" ] }

                  let facts = Interpreter.sensePackage port req
                  let outcome = (List.head facts.Transcripts).Outcome

                  match outcome with
                  | TranscriptUnlocatable _ -> ()
                  | other -> failtestf "expected TranscriptUnlocatable, got %A" other)
          } ]
