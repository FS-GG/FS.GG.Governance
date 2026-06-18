module FS.GG.Governance.Host.Tests.InterpreterTests

open System.IO
open Expecto
open FS.GG.Governance.Kernel
open FS.GG.Governance.Host
open FS.GG.Governance.Host.Tests.Support

// The EDGE side of the boundary: `run` drives the loop against a REAL temp-filesystem fixture and
// a REAL-fs review store; the judge is the ONLY fake. Every test here drives that fake judge, so
// every test name carries the `Synthetic` token (Principle V); the use-site comment lives in
// Support.makeEnvWith / Support.passingJudge.

let private outputKinds (outputs: Output seq) =
    outputs
    |> Seq.map (function
        | ExplanationJson _ -> "explanation"
        | ContractJson _ -> "contract"
        | RouteText _ -> "route")
    |> Seq.toList
    |> List.sort

[<Tests>]
let tests =
    testList
        "Interpreter"
        [ test "V53 Synthetic real-fs sense-plan-act yields the kernel's fact set" {
              let env = makeEnv ()

              try
                  let model = Interpreter.run env.Ports defaultConfig change

                  Expect.equal model.Phase Quiescent "reaches quiescence"
                  Expect.equal !env.Dispatches 1 "exactly one cache-MISS dispatch"

                  // No new decision logic: the loop's facts equal the kernel over the same supplied
                  // facts (the sensed artifact + the frozen RecordedReview) — SC-002.
                  let supplied =
                      model.Facts
                      |> List.filter (fun fa ->
                          match fa.Value with
                          | Artifact _ -> true
                          | Outcome (RuleOutcome.Reviewed _) -> true
                          | _ -> false)

                  let bridged = [ apiRule ] |> List.map (CheckRule.toRule bridge)
                  let kernelFacts = (FixedPoint.evaluate identify bridged supplied).Facts
                  Expect.equal (Set.ofList model.Facts) (Set.ofList kernelFacts) "facts equal the kernel's output"

                  // The artifact was actually read and a verdict was frozen as a Decided outcome.
                  Expect.isTrue
                      (model.Facts |> List.exists (fun fa -> match fa.Value with | Outcome (Decided _) -> true | _ -> false))
                      "a decided outcome is present"
              finally
                  env.Cleanup()
          }

          test "V60 Synthetic emits the three edge outputs and gates only from a Gate base" {
              let env = makeEnv ()

              try
                  let model = Interpreter.run env.Ports defaultConfig change
                  Expect.equal (outputKinds env.Outputs) [ "contract"; "explanation"; "route" ] "three F06/F07 outputs once"

                  // Gate base ⇒ the blocking rule partitions into Blocking (Fenced change, Gate mode).
                  Expect.isNonEmpty model.Route.Blocking "blocking gate enforced at Gate"

                  // Same base developed at Inner ⇒ recomputed from base ⇒ no blocking gate (advisory).
                  let envInner = makeEnv ()

                  try
                      let inner = Interpreter.run envInner.Ports { defaultConfig with Mode = Inner } change
                      Expect.isEmpty inner.Route.Blocking "no blocking gate at Inner (recomputed from base)"
                  finally
                      envInner.Cleanup()
              finally
                  env.Cleanup()
          }

          test "V54 Synthetic a verdict round-trips and is frozen against the F04 key" {
              let env = makeEnv ()

              try
                  let model = Interpreter.run env.Ports defaultConfig change
                  Expect.equal !env.Dispatches 1 "one dispatch"

                  let recorded =
                      model.Facts
                      |> List.choose (fun fa -> match fa.Value with | Outcome (RuleOutcome.Reviewed rr) -> Some rr | _ -> None)

                  Expect.equal (List.length recorded) 1 "exactly one recorded review"
                  Expect.equal recorded.[0].Verdict Pass "frozen verdict is the judge's Pass"
              finally
                  env.Cleanup()
          }

          test "V55 Synthetic a re-run over an unchanged change hits the cache (zero new dispatch)" {
              let env = makeEnv () // SAME env ⇒ SAME real-fs store across both runs

              try
                  Interpreter.run env.Ports defaultConfig change |> ignore
                  Expect.equal !env.Dispatches 1 "first run dispatches once"

                  let second = Interpreter.run env.Ports defaultConfig change
                  Expect.equal !env.Dispatches 1 "second run dispatches ZERO new reviews (cache hit)"
                  Expect.equal second.Phase Quiescent "still reaches quiescence"
              finally
                  env.Cleanup()
          }

          test "V56 Synthetic changing a cache-key ingredient forces a fresh dispatch" {
              let env = makeEnv ()

              try
                  Interpreter.run env.Ports defaultConfig change |> ignore
                  Expect.equal !env.Dispatches 1 "first run dispatches once"

                  // Mutate the artifact content ⇒ different content hash ⇒ different cache key.
                  File.WriteAllText(Path.Combine(env.FixtureDir, "Api.fs"), "let x = 999")
                  Interpreter.run env.Ports defaultConfig change |> ignore
                  Expect.equal !env.Dispatches 2 "stale ⇒ exactly one fresh dispatch"
              finally
                  env.Cleanup()
          }

          test "V58 Synthetic every failure surfaces as a handled Msg with no throw" {
              // (a) a failing judge ⇒ ReviewDispatchFailed, review stays pending, no record, no throw.
              let failingJudge: Judge = fun _ -> Error "judge timeout"
              let envFail = makeEnvWith "let x = 1" failingJudge

              try
                  let model = Interpreter.run envFail.Ports defaultConfig change
                  Expect.isTrue
                      (model.Failures |> List.exists (function | ReviewDispatchFailed _ -> true | _ -> false))
                      "dispatch failure reified"
                  Expect.isFalse
                      (model.Facts |> List.exists (fun fa -> match fa.Value with | Outcome (RuleOutcome.Reviewed _) -> true | _ -> false))
                      "nothing recorded on failure"
              finally
                  envFail.Cleanup()

              // (b) a THROWING judge is caught and reified — the interpreter never throws (SC-006).
              let throwingJudge: Judge = fun _ -> failwith "boom"
              let envThrow = makeEnvWith "let x = 1" throwingJudge

              try
                  let model = Interpreter.run envThrow.Ports defaultConfig change
                  Expect.isTrue
                      (model.Failures |> List.exists (function | ReviewDispatchFailed _ -> true | _ -> false))
                      "thrown judge reified as failure"
              finally
                  envThrow.Cleanup()

              // (c) a missing artifact ⇒ ArtifactUnavailable (never a silent pass, never a crash).
              let envMissing = makeEnv ()
              File.Delete(Path.Combine(envMissing.FixtureDir, "Api.fs"))

              try
                  let model = Interpreter.run envMissing.Ports defaultConfig change
                  Expect.isTrue
                      (model.Failures |> List.exists (function | ArtifactUnavailable _ -> true | _ -> false))
                      "missing artifact reified"
              finally
                  envMissing.Cleanup()
          }

          testProperty "V59 Synthetic no sequence of failures makes run throw or malform the model"
          <| fun (failJudge: bool) (failRead: bool) ->
              let judge: Judge = if failJudge then (fun _ -> Error "x") else passingJudge
              let env = makeEnvWith "let x = 1" judge

              try
                  if failRead then
                      File.Delete(Path.Combine(env.FixtureDir, "Api.fs"))

                  let model = Interpreter.run env.Ports defaultConfig change
                  // Well-formed: a valid phase and the route always carries a non-empty reason.
                  (match model.Phase with
                   | Sensing
                   | Planning
                   | Quiescent -> true)
                  && model.Route.Reason <> ""
              finally
                  env.Cleanup() ]
