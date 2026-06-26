module FS.GG.Governance.Scaffold.Tests.InterpreterTests

open System.IO
open Expecto
open FS.GG.Governance.Scaffold
open FS.GG.Governance.Scaffold.Model
open FS.GG.Governance.Scaffold.Tests.Support

// REAL temp-dir edge tests for the seam interpreter (contract C4, Principle V). Every filesystem effect
// runs against a genuine temp directory; the interpreter is proven TOTAL (never throws) and SAFE (no
// overwrite, no partial tree) — the safety that makes the scaffold trustworthy (SC-005).

let private withTemp (f: string -> unit) =
    let dir = freshTempDir ()

    try
        f dir
    finally
        cleanup dir

[<Tests>]
let tests =
    testList
        "Interpreter"
        [ test "Synthetic provider: a successful run writes every emitted file under target ⇒ Scaffolded" {
              withTemp (fun target ->
                  // SYNTHETIC: the provider content is the out-of-scope fake; the WRITE is real.
                  let p = fakeProvider "fixture.lib" [ "src/App/Program.fs", "// hello"; "README.md", "# hi" ]
                  let model = Interpreter.run (Interpreter.realPorts target) (runRequest target [] (Some p))

                  Expect.equal model.Phase Loop.Done "terminal"

                  match model.Manifest with
                  | Some m -> Expect.equal m.Outcome Scaffolded "outcome scaffolded"
                  | None -> failtest "expected a manifest"

                  Expect.equal (filesUnder target) [ "README.md"; "src/App/Program.fs" ] "every emitted file exists under target"
                  Expect.equal (File.ReadAllText(Path.Combine(target, "README.md"))) "# hi" "contents written verbatim")
          }

          test "Synthetic provider: a pre-existing file at an emitted path refuses with Collision, overwriting nothing" {
              withTemp (fun target ->
                  // Seed an operator file at one of the emitted paths.
                  let seeded = Path.Combine(target, "README.md")
                  File.WriteAllText(seeded, "OPERATOR CONTENT")

                  let p = fakeProvider "fixture.lib" [ "src/App/Program.fs", "// hello"; "README.md", "# hi" ]
                  let model = Interpreter.run (Interpreter.realPorts target) (runRequest target [] (Some p))

                  match model.Manifest with
                  | Some { Outcome = Refused(Collision paths) } -> Expect.contains paths "README.md" "names the colliding path"
                  | other -> failtestf "expected Refused(Collision), got %A" other

                  Expect.equal (File.ReadAllText seeded) "OPERATOR CONTENT" "operator file untouched"
                  Expect.equal (filesUnder target) [ "README.md" ] "no other file written (all-or-nothing)")
          }

          test "Synthetic provider: an out-of-target emission rejects with OutOfTarget, touching nothing" {
              withTemp (fun target ->
                  let p = fakeProvider "fixture.lib" [ "../escape.fs", "x"; "ok.fs", "y" ]
                  let model = Interpreter.run (Interpreter.realPorts target) (runRequest target [] (Some p))

                  match model.Manifest with
                  | Some { Outcome = Refused(OutOfTarget paths) } -> Expect.contains paths "../escape.fs" "names the escaping path"
                  | other -> failtestf "expected Refused(OutOfTarget), got %A" other

                  Expect.isEmpty (filesUnder target) "nothing written"
                  let escaped = Path.GetFullPath(Path.Combine(target, "..", "escape.fs"))
                  Expect.isFalse (File.Exists escaped) "no sibling escape file created")
          }

          test "Synthetic provider: a Write port returning Error leaves zero new files and is reified to a Msg (never throws)" {
              withTemp (fun target ->
                  let p = fakeProvider "fixture.lib" [ "src/App/Program.fs", "// hello" ]
                  let real = Interpreter.realPorts target
                  let faultyWrite = { real with Write = fun _ -> Error "injected write fault" }

                  let model = Interpreter.run faultyWrite (runRequest target [] (Some p))

                  match model.Manifest with
                  | Some { Outcome = Refused(ProviderErrored d) } -> Expect.stringContains d "injected" "carries the fault"
                  | other -> failtestf "expected Refused(ProviderErrored), got %A" other

                  Expect.isEmpty (filesUnder target) "no partial tree")
          }

          test "Synthetic provider: a Write port that THROWS is caught and reified — no partial tree, never throws" {
              withTemp (fun target ->
                  let p = fakeProvider "fixture.lib" [ "src/App/Program.fs", "// hello" ]
                  let real = Interpreter.realPorts target
                  let throwingWrite = { real with Write = fun _ -> failwith "kaboom" }

                  let model = Interpreter.run throwingWrite (runRequest target [] (Some p))

                  match model.Manifest with
                  | Some { Outcome = Refused(ProviderErrored d) } -> Expect.stringContains d "kaboom" "thrown exception reified"
                  | other -> failtestf "expected Refused(ProviderErrored), got %A" other

                  Expect.isEmpty (filesUnder target) "no partial tree")
          }

          test "Synthetic provider: re-running over an already-scaffolded target reports Collision and writes nothing new" {
              withTemp (fun target ->
                  let p = fakeProvider "fixture.lib" [ "src/App/Program.fs", "// hello"; "README.md", "# hi" ]
                  let ports = Interpreter.realPorts target

                  let first = Interpreter.run ports (runRequest target [] (Some p))

                  match first.Manifest with
                  | Some { Outcome = Scaffolded } -> ()
                  | other -> failtestf "expected first run Scaffolded, got %A" other

                  let before = filesUnder target
                  let second = Interpreter.run ports (runRequest target [] (Some p))

                  match second.Manifest with
                  | Some { Outcome = Refused(Collision paths) } -> Expect.isNonEmpty paths "re-run reports the prior files as collisions"
                  | other -> failtestf "expected re-run Refused(Collision), got %A" other

                  Expect.equal (filesUnder target) before "no new files on re-run")
          }

          test "Synthetic provider: a reserved (host-owned) path is treated as a collision" {
              withTemp (fun target ->
                  // Create the reserved lifecycle path the host owns; the provider does not emit it, but it
                  // is passed as reserved, so the seam refuses if the provider's set intersects it. Here the
                  // provider emits a fresh path AND we reserve a path that exists ⇒ collision on the reserved.
                  Directory.CreateDirectory(Path.Combine(target, ".fsgg")) |> ignore
                  File.WriteAllText(Path.Combine(target, ".fsgg/project.yml"), "id: x")

                  let p = fakeProvider "fixture.lib" [ "src/App/Program.fs", "// hello" ]
                  let model = Interpreter.run (Interpreter.realPorts target) (runRequest target [ ".fsgg/project.yml" ] (Some p))

                  match model.Manifest with
                  | Some { Outcome = Refused(Collision paths) } -> Expect.contains paths ".fsgg/project.yml" "reserved existing path is a collision"
                  | other -> failtestf "expected Refused(Collision) on the reserved path, got %A" other

                  Expect.isFalse (File.Exists(Path.Combine(target, "src/App/Program.fs"))) "nothing written")
          } ]
