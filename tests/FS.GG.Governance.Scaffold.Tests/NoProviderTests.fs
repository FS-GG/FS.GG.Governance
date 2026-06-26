module FS.GG.Governance.Scaffold.Tests.NoProviderTests

open System.IO
open Expecto
open FS.GG.Governance.Scaffold
open FS.GG.Governance.Scaffold.Model
open FS.GG.Governance.Scaffold.Tests.Support

// US3: with no provider selected the seam is a literal no-op — zero effects, terminal NoProvider, and
// the host's pre-existing files stay byte-identical (FR-002, research D3). The seam NEVER authors the
// lifecycle skeleton; the manifest VALUE exists (for projection totality) but the host writes none here.

[<Tests>]
let tests =
    testList
        "NoProvider"
        [ test "init with None ⇒ zero effects and terminal Done(NoProvider)" {
              let model, effects = Loop.init (requestFor "/tmp/x" []) None

              Expect.isEmpty effects "zero effects on the no-provider path"
              Expect.equal model.Phase Loop.Done "terminal immediately"

              match model.Manifest with
              | Some m ->
                  Expect.equal m.Outcome NoProvider "outcome NoProvider"
                  Expect.equal m.Provider None "no provider attributed"
                  Expect.isEmpty m.Generated "nothing generated"
                  Expect.isEmpty m.Collisions "no collisions"
              | None -> failtest "expected a (no-provider) manifest value"
          }

          test "run over a real temp dir with no provider writes NO files and leaves host files byte-identical" {
              let target = freshTempDir ()

              try
                  // Seed a host-owned lifecycle file; the seam must not touch it.
                  Directory.CreateDirectory(Path.Combine(target, ".fsgg")) |> ignore
                  let hostFile = Path.Combine(target, ".fsgg/project.yml")
                  File.WriteAllText(hostFile, "id: host-owned")
                  let before = filesUnder target

                  let model = Interpreter.run (Interpreter.realPorts target) (runRequest target [] None)

                  match model.Manifest with
                  | Some { Outcome = NoProvider } -> ()
                  | other -> failtestf "expected NoProvider, got %A" other

                  Expect.equal (filesUnder target) before "no files added"
                  Expect.equal (File.ReadAllText hostFile) "id: host-owned" "host file byte-identical"
              finally
                  cleanup target
          } ]
