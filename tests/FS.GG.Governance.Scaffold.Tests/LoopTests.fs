module FS.GG.Governance.Scaffold.Tests.LoopTests

open Expecto
open FS.GG.Governance.Scaffold
open FS.GG.Governance.Scaffold.Model
open FS.GG.Governance.Scaffold.Tests.Support

// PURE transition + emitted-effect tests for the seam's MVU core (Principle IV). Every assertion drives
// `Loop.init`/`Loop.update` directly with literal Model/Msg values — NO I/O happens in `update`. Covers
// the happy path (T011) and every pre-write refusal (T012). Version negotiation lives in ParityTests.

let private emissionOf (files: (string * string) list) : ProviderEmission =
    { Files =
        files
        |> List.map (fun (p, c) -> { RelativePath = p; Contents = c }) }

let private target = "/tmp/fixture-target"

/// init for a compatible provider, then return the model at Invoking.
let private invoking (provider: TemplateProvider) (reserved: string list) =
    Loop.init (requestFor target reserved) (Some provider)

[<Tests>]
let tests =
    testList
        "Loop"
        [ test "compatible provider: init emits exactly InvokeProvider and enters Invoking" {
              let p = fakeProvider "fixture.lib" [ "src/App/Program.fs", "// p" ]
              let model, effects = invoking p []

              Expect.equal model.Phase Loop.Invoking "phase is Invoking"
              Expect.equal model.Manifest None "no manifest folded yet"

              match effects with
              | [ Loop.InvokeProvider(prov, _req) ] -> Expect.equal prov.Id (ProviderId "fixture.lib") "invokes the selected provider"
              | other -> failtestf "expected a single InvokeProvider effect, got %A" other
          }

          test "in-bounds emission ⇒ ProbeCollisions over resolved ∪ reserved, no write yet" {
              let p = fakeProvider "fixture.lib" [ "src/App/Program.fs", "// p"; "src/App/App.fsproj", "<p/>" ]
              let model, _ = invoking p [ ".fsgg/governance.yml" ]

              let emission = emissionOf [ "src/App/Program.fs", "// p"; "src/App/App.fsproj", "<p/>" ]
              let model2, effects2 = Loop.update (Loop.ProviderEmitted(Ok emission)) model

              Expect.equal model2.Phase Loop.Probing "phase advances to Probing"

              match effects2 with
              | [ Loop.ProbeCollisions paths ] ->
                  Expect.containsAll
                      paths
                      [ "src/App/Program.fs"; "src/App/App.fsproj"; ".fsgg/governance.yml" ]
                      "probes the emitted paths plus the reserved paths"
              | other -> failtestf "expected a single ProbeCollisions effect, got %A" other
          }

          test "empty collision set ⇒ WriteAll the emitted files" {
              let p = fakeProvider "fixture.lib" [ "src/App/Program.fs", "// p" ]
              let model, _ = invoking p []
              let emission = emissionOf [ "src/App/Program.fs", "// p" ]
              let model2, _ = Loop.update (Loop.ProviderEmitted(Ok emission)) model
              let model3, effects3 = Loop.update (Loop.CollisionsProbed(Ok [])) model2

              Expect.equal model3.Phase Loop.Writing "phase advances to Writing"

              match effects3 with
              | [ Loop.WriteAll files ] ->
                  Expect.equal files [ "src/App/Program.fs", "// p" ] "writes exactly the emitted (path, contents)"
              | other -> failtestf "expected a single WriteAll effect, got %A" other
          }

          test "write Ok ⇒ Done(Scaffolded) folding a provider-attributed, ascending, providerOwned manifest" {
              let p = fakeProvider "fixture.lib" [ "src/App/Program.fs", "// p"; "src/App/App.fsproj", "<p/>" ]
              let model, _ = invoking p []
              let emission = emissionOf [ "src/App/Program.fs", "// p"; "src/App/App.fsproj", "<p/>" ]
              let model2, _ = Loop.update (Loop.ProviderEmitted(Ok emission)) model
              let model3, _ = Loop.update (Loop.CollisionsProbed(Ok [])) model2
              let model4, effects4 = Loop.update (Loop.FilesWritten(Ok())) model3

              Expect.equal model4.Phase Loop.Done "terminal"
              Expect.isEmpty effects4 "no further effects"

              match model4.Manifest with
              | Some m ->
                  Expect.equal m.Outcome Scaffolded "outcome scaffolded"
                  Expect.equal m.Provider (Some(ProviderId "fixture.lib", { Major = 1; Minor = 0 })) "provider attributed"

                  Expect.equal
                      (m.Generated |> List.map (fun g -> g.RelativePath))
                      [ "src/App/App.fsproj"; "src/App/Program.fs" ]
                      "generated paths ascending"

                  Expect.isTrue
                      (m.Generated |> List.forall (fun g -> g.Ownership = ProviderOwned))
                      "every generated path is providerOwned"

                  Expect.isEmpty m.Collisions "no collisions on success"
              | None -> failtest "expected a folded manifest"
          }

          // ── failure-mode block (T012): pre-write refusals, each terminal, each writing nothing ──

          test "out-of-target emission ⇒ Refused(OutOfTarget) after Emit, before any probe" {
              let p = fakeProvider "fixture.lib" [ "../escape.fs", "x" ]
              let model, _ = invoking p []
              let emission = emissionOf [ "../escape.fs", "x"; "/etc/rooted.fs", "y"; "src/ok.fs", "z" ]
              let model2, effects2 = Loop.update (Loop.ProviderEmitted(Ok emission)) model

              Expect.equal model2.Phase Loop.Done "terminal"
              Expect.isEmpty effects2 "no probe/write effect emitted"

              match model2.Manifest with
              | Some { Outcome = Refused(OutOfTarget paths) } ->
                  Expect.containsAll paths [ "../escape.fs"; "/etc/rooted.fs" ] "names the escaping paths"
                  Expect.isFalse (List.contains "src/ok.fs" paths) "the in-bounds path is not flagged"
              | other -> failtestf "expected Refused(OutOfTarget), got %A" other
          }

          test "probed pre-existing/reserved path ⇒ Refused(Collision) with NO WriteAll" {
              let p = fakeProvider "fixture.lib" [ "src/App/Program.fs", "// p" ]
              let model, _ = invoking p []
              let emission = emissionOf [ "src/App/Program.fs", "// p" ]
              let model2, _ = Loop.update (Loop.ProviderEmitted(Ok emission)) model
              let model3, effects3 = Loop.update (Loop.CollisionsProbed(Ok [ "src/App/Program.fs" ])) model2

              Expect.equal model3.Phase Loop.Done "terminal"
              Expect.isEmpty effects3 "all-or-nothing: no WriteAll emitted"

              match model3.Manifest with
              | Some m ->
                  Expect.equal m.Outcome (Refused(Collision [ "src/App/Program.fs" ])) "collision refusal"
                  Expect.equal m.Collisions [ "src/App/Program.fs" ] "collisions recorded"
                  Expect.isEmpty m.Generated "nothing generated"
                  Expect.equal m.Provider (Some(ProviderId "fixture.lib", { Major = 1; Minor = 0 })) "provider attributed"
              | None -> failtest "expected a manifest"
          }

          test "EmitFailed ⇒ Refused(ProviderErrored)" {
              let p = fakeProvider "fixture.lib" []
              let model, _ = invoking p []
              let model2, effects2 = Loop.update (Loop.ProviderEmitted(Error(EmitFailed "boom"))) model

              Expect.equal model2.Phase Loop.Done "terminal"
              Expect.isEmpty effects2 "no effects"

              match model2.Manifest with
              | Some { Outcome = Refused(ProviderErrored d); Generated = []; Provider = Some _ } ->
                  Expect.equal d "boom" "carries the provider's detail"
              | other -> failtestf "expected Refused(ProviderErrored), got %A" other
          }

          test "Unresolvable ⇒ Refused(ProviderUnavailable)" {
              let p = fakeProvider "fixture.lib" []
              let model, _ = invoking p []
              let model2, _ = Loop.update (Loop.ProviderEmitted(Error(Unresolvable "missing"))) model

              match model2.Manifest with
              | Some { Outcome = Refused(ProviderUnavailable d); Generated = [] } -> Expect.equal d "missing" "carries the detail"
              | other -> failtestf "expected Refused(ProviderUnavailable), got %A" other
          }

          test "write Error ⇒ recoverable Refused(ProviderErrored), no partial recorded" {
              let p = fakeProvider "fixture.lib" [ "src/App/Program.fs", "// p" ]
              let model, _ = invoking p []
              let emission = emissionOf [ "src/App/Program.fs", "// p" ]
              let model2, _ = Loop.update (Loop.ProviderEmitted(Ok emission)) model
              let model3, _ = Loop.update (Loop.CollisionsProbed(Ok [])) model2
              let model4, effects4 = Loop.update (Loop.FilesWritten(Error "disk full")) model3

              Expect.equal model4.Phase Loop.Done "terminal"
              Expect.isEmpty effects4 "no further effects"

              match model4.Manifest with
              | Some { Outcome = Refused(ProviderErrored d); Generated = [] } -> Expect.equal d "disk full" "carries the write fault"
              | other -> failtestf "expected Refused(ProviderErrored), got %A" other
          } ]
