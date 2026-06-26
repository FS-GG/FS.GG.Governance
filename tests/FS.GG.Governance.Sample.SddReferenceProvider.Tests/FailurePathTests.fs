module FS.GG.Governance.Sample.SddReferenceProvider.Tests.FailurePathTests

open System.IO
open Expecto
open FS.GG.Governance.Scaffold
open FS.GG.Governance.Scaffold.Model
open FS.GG.Governance.Sample.SddReferenceProvider
open FS.GG.Governance.Sample.SddReferenceProvider.Tests.Support

// Failure-path + parity examples (US2). These pin the seam's opt-in/safety/empty-emission guarantees the
// worked example relies on, and prove the seam treats the reference, a clone, and a broken clone
// IDENTICALLY — delegation differs only in emitted files, with NO provider-specific branch in the tool
// (FR-010/FR-011/FR-012; SC-004/SC-007). The reference provider is selected through the SAME
// `Interpreter.run` call as US1.

let private runWith (provider: TemplateProvider option) (target: string) =
    Interpreter.run (Interpreter.realPorts target) (runRequest target lifecycleReservedPaths provider)

let private manifestOf (model: Loop.Model) =
    match model.Manifest with
    | Some m -> m
    | None -> failtest "no terminal manifest"

[<Tests>]
let tests =
    testList
        "FailurePaths"
        [ // FR-010 / SC-007: no provider selected ⇒ today's behavior, zero diff. `init` emits ZERO
          // effects and folds a `NoProvider` manifest; nothing is written to the target.
          test "no provider selected → NoProvider, zero effects, nothing written" {
              let target = freshTarget ()

              try
                  let _, effects = Loop.init (runRequest target lifecycleReservedPaths None).Request None
                  Expect.isTrue (List.isEmpty effects) "init emits zero effects when no provider is selected"

                  let model = runWith None target
                  Expect.equal (manifestOf model).Outcome NoProvider "terminal outcome is NoProvider"
                  Expect.equal (filesUnder target) [] "the lifecycle layer is untouched — nothing written"
              finally
                  cleanup target
          }

          // FR-007 / contract R4: a pre-existing file at an emitted path ⇒ a clean collision refusal,
          // nothing overwritten.
          test "collision at an emitted path → Refused(Collision), nothing overwritten" {
              let target = freshTarget ()

              try
                  let seeded = Path.Combine(target, "README.md")
                  File.WriteAllText(seeded, "PRE-EXISTING — must not be overwritten")

                  let model = runWith (Some SddReferenceProvider.provider) target

                  match (manifestOf model).Outcome with
                  | Refused(Collision paths) -> Expect.equal paths [ "README.md" ] "the pre-existing path is reported"
                  | other -> failtestf "expected Refused(Collision), got %A" other

                  Expect.equal (filesUnder target) [ "README.md" ] "only the seeded file is present — nothing else written"
                  Expect.equal (File.ReadAllText seeded) "PRE-EXISTING — must not be overwritten" "seeded file unchanged"
              finally
                  cleanup target
          }

          // Spec Edge Cases "Empty provider output": an in-test provider that emits zero files ⇒ a clean
          // `Scaffolded` with an EMPTY generated set, no error.
          test "empty provider output → Scaffolded with empty generated set" {
              let target = freshTarget ()

              try
                  // SYNTHETIC: an in-test provider modeling a no-op emission; only its Emit varies.
                  let emptyProvider =
                      { SddReferenceProvider.provider with
                          Id = ProviderId "fsgg.sample.empty"
                          Emit = fun _ -> Ok { Files = [] } }

                  let model = runWith (Some emptyProvider) target
                  let manifest = manifestOf model
                  Expect.equal manifest.Outcome Scaffolded "empty emission still completes cleanly"
                  Expect.equal manifest.Generated [] "no generated paths"
                  Expect.equal manifest.Collisions [] "no collisions"
                  Expect.equal (filesUnder target) [] "nothing written"
              finally
                  cleanup target
          }

          // FR-011 / contract R4: a clone declaring an incompatible major ⇒ a clean version-mismatch
          // refusal BEFORE any invocation; no files written. Extends this file (not parallel with the
          // tests above — same module).
          test "contract-mismatch clone → Refused(ContractMismatch), no files written" {
              let target = freshTarget ()

              try
                  // SYNTHETIC: a deliberately-incompatible clone of the reference; only the declared
                  // version differs (Major = 2).
                  let incompatible =
                      { SddReferenceProvider.provider with
                          ContractVersion = { Major = 2; Minor = 0 } }

                  let model = runWith (Some incompatible) target

                  match (manifestOf model).Outcome with
                  | Refused(ContractMismatch declared) ->
                      Expect.equal declared { Major = 2; Minor = 0 } "the declared incompatible version is reported"
                  | other -> failtestf "expected Refused(ContractMismatch), got %A" other

                  Expect.equal (filesUnder target) [] "nothing written on a contract mismatch"
              finally
                  cleanup target
          }

          // FR-006 / SC-004 / contract R5: a third-party CLONE runs through the IDENTICAL seam call as
          // US1 — only the emitted files differ; NO edit to Scaffold/the tool.
          test "cloned provider runs through the identical seam — only emitted files differ" {
              let target = freshTarget ()

              try
                  // A minimal clone: same contract, different id, a single different emitted file.
                  let clone =
                      { Id = ProviderId "fsgg.sample.my-clone"
                        ContractVersion = { Major = 1; Minor = 0 }
                        Emit = fun _ -> Ok { Files = [ { RelativePath = "hello.txt"; Contents = "from a clone\n" } ] } }

                  let model = runWith (Some clone) target
                  let manifest = manifestOf model

                  Expect.equal manifest.Outcome Scaffolded "the clone scaffolds through the same seam"

                  Expect.equal
                      (manifest.Provider |> Option.map fst)
                      (Some(ProviderId "fsgg.sample.my-clone"))
                      "the manifest records the clone's id verbatim"

                  Expect.equal
                      (manifest.Generated |> List.map (fun g -> g.RelativePath))
                      [ "hello.txt" ]
                      "only the clone's emitted file is generated"

                  Expect.equal (filesUnder target) [ "hello.txt" ] "only the clone's file is on disk"
                  Expect.equal (File.ReadAllText(Path.Combine(target, "hello.txt"))) "from a clone\n" "clone content written"
              finally
                  cleanup target
          } ]
