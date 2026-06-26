module FS.GG.Governance.Scaffold.Tests.ParityTests

open Expecto
open FS.GG.Governance.Scaffold
open FS.GG.Governance.Scaffold.Model
open FS.GG.Governance.Scaffold.Tests.Support

// US2: any conforming provider runs through the SAME seam with no provider-specific branch (FR-004,
// contract C3) — delegation differs ONLY in what the provider emits — and an incompatible contract
// version is refused BEFORE invocation with an actionable diagnostic (contract C2, US2 AS3).

let private outcomeOf (target: string) (p: TemplateProvider) =
    let model = Interpreter.run (Interpreter.realPorts target) (runRequest target [] (Some p))
    model.Manifest

[<Tests>]
let tests =
    testList
        "Parity"
        [ test "two distinct Synthetic providers differ ONLY in emitted files + recorded id; safety identical" {
              let a = fakeProvider "acme.lib" [ "src/A.fs", "// a" ]
              let b = fakeProvider "globex.app" [ "src/B.fs", "// b"; "src/C.fs", "// c" ]

              let ta = freshTempDir ()
              let tb = freshTempDir ()

              try
                  match outcomeOf ta a, outcomeOf tb b with
                  | Some ma, Some mb ->
                      // Same safety + reporting behaviour: both scaffolded, no collisions, all providerOwned.
                      Expect.equal ma.Outcome Scaffolded "provider A scaffolded"
                      Expect.equal mb.Outcome Scaffolded "provider B scaffolded"
                      Expect.isEmpty ma.Collisions "A: no collisions"
                      Expect.isEmpty mb.Collisions "B: no collisions"

                      Expect.isTrue
                          ([ ma; mb ]
                           |> List.forall (fun m -> m.Generated |> List.forall (fun g -> g.Ownership = ProviderOwned)))
                          "both: every generated path providerOwned"

                      // The ONLY differences: the provider id and the emitted file set.
                      Expect.notEqual (fst (Option.get ma.Provider)) (fst (Option.get mb.Provider)) "different provider ids"

                      Expect.equal (ma.Generated |> List.map (fun g -> g.RelativePath)) [ "src/A.fs" ] "A emits its own files"
                      Expect.equal (mb.Generated |> List.map (fun g -> g.RelativePath)) [ "src/B.fs"; "src/C.fs" ] "B emits its own files"
                  | _ -> failtest "expected manifests from both providers"
              finally
                  cleanup ta
                  cleanup tb
          }

          // ── version negotiation (T022) ──

          test "a {Major=2} provider ⇒ init yields Done(Refused(ContractMismatch)) WITHOUT invoking, no writes" {
              let p = providerAtVersion "future.lib" 2 0 [ "src/A.fs", "// a" ]
              let model, effects = Loop.init (requestFor "/tmp/x" []) (Some p)

              Expect.equal model.Phase Loop.Done "terminal before invocation"
              Expect.isEmpty effects "no InvokeProvider effect emitted"

              match model.Manifest with
              | Some { Outcome = Refused(ContractMismatch declared); Generated = [] } ->
                  Expect.equal declared { Major = 2; Minor = 0 } "carries the declared version (actionable diagnostic)"
              | other -> failtestf "expected Refused(ContractMismatch), got %A" other
          }

          test "a {Major=1;Minor=1} provider ⇒ refused (minor beyond supported), no invocation" {
              let p = providerAtVersion "future.lib" 1 1 [ "src/A.fs", "// a" ]
              let model, effects = Loop.init (requestFor "/tmp/x" []) (Some p)

              Expect.isEmpty effects "no invocation"

              match model.Manifest with
              | Some { Outcome = Refused(ContractMismatch declared) } -> Expect.equal declared { Major = 1; Minor = 1 } "declared minor carried"
              | other -> failtestf "expected Refused(ContractMismatch), got %A" other
          }

          test "a {Major=1;Minor=0} provider is accepted ⇒ Invoking with an InvokeProvider effect" {
              let p = providerAtVersion "ok.lib" 1 0 [ "src/A.fs", "// a" ]
              let model, effects = Loop.init (requestFor "/tmp/x" []) (Some p)

              Expect.equal model.Phase Loop.Invoking "accepted: enters Invoking"

              match effects with
              | [ Loop.InvokeProvider _ ] -> ()
              | other -> failtestf "expected InvokeProvider, got %A" other
          } ]
