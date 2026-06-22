module FS.GG.Governance.FreshnessSensing.Tests.SensorTests

open Expecto
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.FreshnessResolution.Model
open FS.GG.Governance.FreshnessSensing
open FS.GG.Governance.FreshnessSensing.Tests.Support

// Real-bytes evidence for the freshness sensor (Principle V): `realSensor` over a temp dir hashes genuine
// `.fsgg/*.yml` + `src/**` bytes; `senseFreshness` assembles a `SensedFacts` whose present/absent Map keys
// match the sensed/unsensed inputs and is total under a throwing accessor.

[<Tests>]
let tests =
    testList
        "Sensor"
        [ test "realSensor over a real catalog/src senses stable, deterministic SHA-256 facts (re-run identical)" {
              withTempDir (fun t ->
                  let g = gateWith "build" "tests" (Some dotnetCmd)
                  let s1 = FreshnessSensing.realSensor t.Dir
                  let s2 = FreshnessSensing.realSensor t.Dir

                  // A present catalog ⇒ Some rule hash, identical across two independent senses (determinism).
                  let rh1 = s1.SenseRuleHash()
                  let rh2 = s2.SenseRuleHash()
                  Expect.isSome rh1 "a present .fsgg catalog ⇒ Some ruleHash"
                  Expect.equal rh1 rh2 "ruleHash is deterministic across two senses of the same dir"

                  Expect.isSome (s1.SenseGeneratorVersion()) "generator version is sensed"

                  // A present src/** surface ⇒ Some (non-empty), deterministic.
                  let cov1 = s1.SenseCoveredArtifacts g
                  let cov2 = s2.SenseCoveredArtifacts g
                  Expect.equal cov1 cov2 "covered artifacts are deterministic"

                  match cov1 with
                  | Some hs -> Expect.equal (List.length hs) 2 "two src/** files ⇒ two covered hashes"
                  | None -> failtest "a present src/** surface ⇒ Some covered hashes, not None"

                  Expect.isSome (s1.SenseCommandVersion dotnetCmd) "a present catalog ⇒ Some command version")
          }

          test "an absent catalog/src ⇒ accessors return None (unsensed), never a fabricated empty hash" {
              withBareDir (fun dir ->
                  let s = FreshnessSensing.realSensor dir
                  Expect.equal (s.SenseRuleHash()) None "no .fsgg catalog ⇒ ruleHash is None (unsensed, no-hide)"
                  Expect.equal (s.SenseCommandVersion dotnetCmd) None "no catalog ⇒ command version is None (unsensed)"

                  // A missing src/** surface is SENSED-EMPTY (`Some []`), a legitimate resolved value — NOT None.
                  let g = gateWith "build" "tests" (Some dotnetCmd)
                  Expect.equal (s.SenseCoveredArtifacts g) (Some []) "a missing src/** ⇒ Some [] (sensed-empty), not None")
          }

          test "senseFreshness assembles a SensedFacts whose present keys match the sensed inputs, base/head passed through" {
              withTempDir (fun t ->
                  let g = gateWith "build" "tests" (Some dotnetCmd)
                  let sensor = FreshnessSensing.realSensor t.Dir
                  let baseHead = Some(Revision "base-x"), Some(Revision "head-x")

                  match FreshnessSensing.senseFreshness sensor [ g ] baseHead with
                  | Error e -> failtestf "senseFreshness unexpectedly failed: %s" e
                  | Ok(facts: SensedFacts) ->
                      Expect.isSome facts.RuleHash "ruleHash sensed"
                      Expect.isSome facts.GeneratorVersion "generator sensed"
                      Expect.equal facts.Base (Some(Revision "base-x")) "base passed through verbatim"
                      Expect.equal facts.Head (Some(Revision "head-x")) "head passed through verbatim"
                      Expect.isTrue (Map.containsKey g.Id facts.CoveredArtifacts) "the gate's covered key is present (sensed)"
                      Expect.isTrue (Map.containsKey dotnetCmd facts.CommandVersions) "the declared command's version key is present (sensed)")
          }

          test "senseFreshness is total under a throwing accessor ⇒ Error (never throws)" {
              let throwingSensor: FreshnessSensing.FreshnessSensor =
                  { SenseRuleHash = fun () -> failwith "boom"
                    SenseGeneratorVersion = fun () -> None
                    SenseCoveredArtifacts = fun _ -> None
                    SenseCommandVersion = fun _ -> None }

              let g = gateWith "build" "tests" (Some dotnetCmd)

              match FreshnessSensing.senseFreshness throwingSensor [ g ] (None, None) with
              | Error _ -> ()
              | Ok _ -> failtest "a throwing accessor must surface as Error, never a fabricated SensedFacts"
          }

          test "an unsensed command version ⇒ the command key is ABSENT (never fabricated)" {
              // A sensor that senses everything EXCEPT the command version: the assembled facts must NOT carry
              // the command key (absent = not sensed), so the gate resolves unresolved on command version.
              let partial: FreshnessSensing.FreshnessSensor =
                  { SenseRuleHash = fun () -> Some(RuleHash "r")
                    SenseGeneratorVersion = fun () -> Some(GeneratorVersion "g")
                    SenseCoveredArtifacts = fun _ -> Some []
                    SenseCommandVersion = fun _ -> None }

              let g = gateWith "build" "tests" (Some dotnetCmd)

              match FreshnessSensing.senseFreshness partial [ g ] (None, None) with
              | Ok facts -> Expect.isFalse (Map.containsKey dotnetCmd facts.CommandVersions) "an unsensed command version is an ABSENT key, never fabricated"
              | Error e -> failtestf "unexpected Error: %s" e
          } ]
