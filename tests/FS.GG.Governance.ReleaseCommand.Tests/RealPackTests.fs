module FS.GG.Governance.ReleaseCommand.Tests.RealPackTests

open System
open System.IO
open System.Text.RegularExpressions
open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.CommandKind.Model
open FS.GG.Governance.PackEvidence.Model
open FS.GG.Governance.ReleaseCommand
open FS.GG.Governance.ReleaseCommand.Tests.Support

// 066 US1 (closes 065 T018): the real-`dotnet pack` pack-boundary evidence. The wired 065 release host is
// driven through its existing `Interpreter.run` entry over a REAL temporary multi-project tree, with the
// faked `065` edge swapped for the real one — `GateExecution.Interpreter.realPort` (a real
// `System.Diagnostics.Process` per declared `dotnet pack`) and the per-surface real `PackRead`
// (`realPackReadInto`, which reads the produced `.nupkg` bytes off disk). No host or core code changes; the
// pure transition is already proven by `LoopTests`. The deliberately broken / no-artifact projects are the
// only `Synthetic`-named elements (the pack execution itself is real); every other pack is a genuine build.
// The whole module is SDK-gated: absent a working `dotnet pack`, it surfaces ONE disclosed skip (FR-008).

let private req repo =
    { Loop.Repo = repo
      Loop.Format = Loop.Json
      Loop.ReleaseOut = Path.Combine(repo, "readiness", "release.json")
      Loop.AttestationOut = Path.Combine(repo, "readiness", "attestation.json") }

/// Drive the wired release host over the real-pack tree with the REAL edge ports, returning the terminal
/// model and the request (so a test can read the written artifact paths back).
let private runRelease (repo: string) : Loop.Model * Loop.RunRequest =
    let request = req repo
    Interpreter.run (realPackPorts repo) request, request

let private verdicts (m: Loop.Model) : PackVerdict list =
    m.PackEvidence |> Option.map (fun p -> p.Verdicts) |> Option.defaultValue []

let private packRuns (m: Loop.Model) : KindedCommandRun list =
    m.PackEvidence |> Option.map (fun p -> p.Runs) |> Option.defaultValue []

let private blockerReasons (m: Loop.Model) : string list =
    m.Decision
    |> Option.map (fun d -> d.Blockers |> List.map (fun e -> e.Finding.Reason))
    |> Option.defaultValue []

/// The sensed pack `durationNanos` is the sole wall-clock-sensitive field; strip it before any byte-identity
/// comparison (SC-003, FR-006) so the comparison is over the normalized contract only.
let private stripDuration (json: string) : string =
    Regex.Replace(json, "\"durationNanos\":-?[0-9]+", "\"durationNanos\":0")

let private realPackTests =
    [ test "bumped: every project packs at a bumped version ⇒ Success, one Pack run each, both artifacts written" {
          // SC-001 row 1, FR-001/FR-002, AS-1.
          let specs = [ buildable "fsggalpha" "1.3.0" "1.2.0"; buildable "fsggbeta" "2.1.0" "2.0.0" ]

          withRealPackRepo specs (fun repo ->
              let m, r = runRelease repo
              Expect.equal m.Exit Loop.Success "all bumped ⇒ clean release"
              Expect.isEmpty (blockerReasons m) "no blockers"
              Expect.equal (List.length (packRuns m)) 2 "exactly one Pack run per declared project"
              Expect.isTrue (packRuns m |> List.forall (fun run -> run.Kind = Pack)) "every recorded run is a Pack"

              Expect.isTrue
                  (verdicts m
                   |> List.forall (fun v ->
                       match v.Outcome with
                       | Packed _ -> true
                       | _ -> false))
                  "both projects produced a real artifact"

              Expect.isTrue (File.Exists r.ReleaseOut) "release.json written"
              Expect.isTrue (File.Exists r.AttestationOut) "attestation.json written"
              let releaseDoc = File.ReadAllText r.ReleaseOut
              Expect.stringContains releaseDoc "fsgg.release/v2" "release.json is v2")
      }

      test "failed-pack (Synthetic broken project) ⇒ Blocked naming the project, the failed run recorded with its sentinel" {
          // SC-001 row 2, FR-002, AS-2. SYNTHETIC: `fsggbroken` carries an intentional compile error so the
          // real `dotnet pack` exits non-zero; the pack execution is real (Constitution V).
          let specs =
              [ buildable "fsgggood" "1.3.0" "1.2.0"
                { Surface = "fsggbroken"
                  Version = "1.3.0"
                  Baseline = Some "1.2.0"
                  Kind = BuildFails } ]

          withRealPackRepo specs (fun repo ->
              let m, r = runRelease repo
              Expect.equal m.Exit Loop.Blocked "a failed pack blocks the release"

              let failed =
                  verdicts m
                  |> List.tryPick (fun v ->
                      match v.Outcome with
                      | PackFailed(SurfaceId s, sentinel, run) -> Some(s, sentinel, run)
                      | _ -> None)

              match failed with
              | Some(surface, sentinel, run) ->
                  Expect.equal surface "fsggbroken" "the failing project is named"
                  Expect.notEqual sentinel 0 "the recorded sentinel is the real non-zero exit"
                  let (ExitCode code) = run.Record.Reproducible.ExitCode
                  Expect.equal code sentinel "the failed Pack run is recorded with its sentinel (never dropped)"
              | None -> failtest "expected a PackFailed verdict for the broken project"

              // No fabricated pass: the release is honestly blocked, and the written release.json records the
              // fail verdict naming the failing project (the failed pack is reported, never papered over).
              Expect.isFalse (m.Exit = Loop.Success) "the failed pack is not papered over"
              Expect.isTrue (File.Exists r.ReleaseOut) "the blocked release.json is still written"
              let releaseDoc = File.ReadAllText r.ReleaseOut
              Expect.stringContains releaseDoc "\"verdict\":\"fail\"" "the release.json records a fail verdict"
              Expect.stringContains releaseDoc "fsggbroken" "the release.json names the failing project")
      }

      test "unbumped/downgraded: a project packs at <= its baseline ⇒ Blocked naming the project and the offending version" {
          // SC-001 row 3, FR-001, AS-3.
          let specs =
              [ buildable "fsggbumped" "1.3.0" "1.2.0"
                buildable "fsggstale" "2.0.0" "2.0.0" ] // packed == baseline ⇒ Unbumped

          withRealPackRepo specs (fun repo ->
              let m, _ = runRelease repo
              Expect.equal m.Exit Loop.Blocked "an unbumped project blocks the release"

              let stale =
                  verdicts m |> List.tryFind (fun v -> v.Surface = SurfaceId "fsggstale")

              match stale with
              | Some v ->
                  Expect.equal v.Version (Unbumped "2.0.0") "the version verdict is Unbumped at the packed version"
                  Expect.stringContains v.Reason "fsggstale" "the reason names the project"
                  Expect.stringContains v.Reason "2.0.0" "the reason names the offending version"
              | None -> failtest "expected a verdict for the stale project")
      }

      test "downgraded: a project packs below its baseline ⇒ Blocked, reason names project and versions" {
          // SC-001 row 3 (downgrade variant), FR-001.
          let specs =
              [ buildable "fsggdown" "1.1.0" "1.2.0" ] // packed < baseline ⇒ Downgraded

          withRealPackRepo specs (fun repo ->
              let m, _ = runRelease repo
              Expect.equal m.Exit Loop.Blocked "a downgrade blocks the release"

              match verdicts m |> List.tryFind (fun v -> v.Surface = SurfaceId "fsggdown") with
              | Some v ->
                  Expect.equal v.Version (Downgraded("1.2.0", "1.1.0")) "Downgraded baseline -> packed"
                  Expect.stringContains v.Reason "fsggdown" "the reason names the project"
                  Expect.stringContains v.Reason "1.1.0" "the reason names the offending packed version"
              | None -> failtest "expected a verdict for the downgraded project")
      }

      test "no-baseline: a packable project with no released baseline ⇒ first release (NoBaseline), not a downgrade" {
          // SC-001 row 4, FR-001, AS-4.
          let specs =
              [ { Surface = "fsggfirst"
                  Version = "0.1.0"
                  Baseline = None
                  Kind = Buildable } ]

          withRealPackRepo specs (fun repo ->
              let m, _ = runRelease repo
              Expect.equal m.Exit Loop.Success "a first release is not blocked as a downgrade"

              match verdicts m |> List.tryFind (fun v -> v.Surface = SurfaceId "fsggfirst") with
              | Some v -> Expect.equal v.Version (NoBaseline "0.1.0") "treated as a first release"
              | None -> failtest "expected a verdict for the first-release project")
      }

      test "zero-exit-no-artifact (Synthetic IsPackable=false) ⇒ Blocked 'no artifact emitted', recorded, distinct from a failed pack" {
          // spec edge case L123-124, Constitution VI, FR-002. SYNTHETIC: `fsggnoart` sets IsPackable=false so
          // the real `dotnet pack` exits 0 yet emits no `.nupkg`; the pack execution is real (Constitution V).
          let specs =
              [ buildable "fsgggood2" "1.3.0" "1.2.0"
                { Surface = "fsggnoart"
                  Version = "1.3.0"
                  Baseline = Some "1.2.0"
                  Kind = NoArtifact } ]

          withRealPackRepo specs (fun repo ->
              let m, _ = runRelease repo
              Expect.equal m.Exit Loop.Blocked "packed-but-no-artifact blocks the release"

              match verdicts m |> List.tryFind (fun v -> v.Surface = SurfaceId "fsggnoart") with
              | Some v ->
                  match v.Outcome with
                  | PackedNoArtifact(_, NoArtifactEmitted, run) ->
                      let (ExitCode code) = run.Record.Reproducible.ExitCode
                      Expect.equal code 0 "held DISTINCT from a failed pack: the pack exited zero"
                      Expect.stringContains v.Reason "no artifact emitted" "the reason explains the missing artifact"
                  | other -> failtestf "expected PackedNoArtifact NoArtifactEmitted, got %A" other
              | None -> failtest "expected a verdict for the no-artifact project")
      }

      test "determinism: re-running the bumped case over unchanged inputs ⇒ byte-identical release.json + attestation.json" {
          // SC-003, FR-006. Compare the normalized documents (durationNanos excluded) and assert no machine
          // path / username appears in either asserted output. Depends on the bumped case.
          let specs = [ buildable "fsggdet1" "1.3.0" "1.2.0"; buildable "fsggdet2" "2.1.0" "2.0.0" ]

          withRealPackRepo specs (fun repo ->
              let m1, r = runRelease repo
              Expect.equal m1.Exit Loop.Success "first run is a clean release"
              let release1 = File.ReadAllText r.ReleaseOut
              let attestation1 = File.ReadAllText r.AttestationOut

              let m2, _ = runRelease repo
              Expect.equal m2.Exit Loop.Success "re-run is a clean release"
              let release2 = File.ReadAllText r.ReleaseOut
              let attestation2 = File.ReadAllText r.AttestationOut

              Expect.equal (stripDuration release2) (stripDuration release1) "release.json byte-identical on re-run"

              Expect.equal
                  (stripDuration attestation2)
                  (stripDuration attestation1)
                  "attestation.json byte-identical on re-run (durationNanos excluded)"

              // FR-006: no machine path, username, or temp-root string in any asserted output.
              for doc in [ stripDuration release1; stripDuration attestation1 ] do
                  Expect.isFalse (doc.Contains repo) "no repo/machine path leaks into the asserted output"
                  Expect.isFalse (doc.Contains(Path.GetTempPath().TrimEnd('/', '\\'))) "no temp-root path leaks"
                  Expect.isFalse (doc.Contains(Environment.UserName)) "no username leaks")
      } ]

[<Tests>]
let tests =
    testSequenced
    <| testList
        "RealPack"
        (match dotnetSdkSkipReason () with
         | Some reason ->
             // FR-008: a disclosed skip with a diagnostic naming the missing SDK — never a silent green.
             [ test "RealPack pack-boundary requires a working dotnet SDK" { skiptest reason } ]
         | None -> realPackTests)
