module FS.GG.Governance.ReleaseCommand.Tests.LoopTests

open Expecto
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseCommand
open FS.GG.Governance.ReleaseCommand.Tests.Support

// Pure MVU transitions (Constitution IV): `init`/`update` are pure; `evaluateRelease` is called purely in
// `update`; emitted-effect lists are asserted explicitly. No I/O.

let private req format =
    { Loop.Repo = "."
      Loop.Format = format
      Loop.ReleaseOut = "out/release.json" }

/// Drive init → DeclarationLoaded(Ok) → return the post-declaration model.
let private afterDeclaration format =
    let m0, _ = Loop.init (req format)
    let m1, eff1 = Loop.update (Loop.DeclarationLoaded(Ok compliantDeclaration)) m0
    m1, eff1

[<Tests>]
let tests =
    testList
        "Loop"
        [ test "init emits exactly one LoadDeclaration for the repo" {
              let _, eff0 = Loop.init (req Loop.Text)
              Expect.equal eff0 [ Loop.LoadDeclaration "." ] "init effect"
          }

          test "DeclarationLoaded(Ok) emits SenseRelease(layout, expectations)" {
              let m1, eff1 = afterDeclaration Loop.Text
              Expect.equal m1.Phase Loop.Loaded' "phase advanced"

              Expect.equal
                  eff1
                  [ Loop.SenseRelease(compliantDeclaration.Layout, compliantDeclaration.Expectations) ]
                  "sense effect carries the declared layout + expectations"
          }

          test "Sensed (compliant) computes a clean decision, Exit=Success, and emits a text summary" {
              let m1, _ = afterDeclaration Loop.Text
              let m2, eff2 = Loop.update (Loop.Sensed sensedMet) m1

              Expect.isSome m2.Decision "decision computed purely in update"
              Expect.equal m2.Exit Loop.Success "clean basis → Success"

              match eff2 with
              | [ Loop.EmitSummary _ ] -> ()
              | other -> failtestf "expected a single EmitSummary, got %A" other
          }

          test "Sensed (un-bumped) blocks: Exit=Blocked and version-bump is a blocker" {
              let m1, _ = afterDeclaration Loop.Text
              let m2, _ = Loop.update (Loop.Sensed sensedUnbumped) m1

              Expect.equal m2.Exit Loop.Blocked "blocked basis → Blocked"

              let blockerKinds =
                  m2.Decision
                  |> Option.map (fun d -> d.Blockers |> List.map (fun e -> e.Finding.Kind))
                  |> Option.defaultValue []

              Expect.contains blockerKinds VersionBump "version-bump is a blocker"
          }

          test "Sensed under --format json projects the doc and emits WriteArtifact" {
              let m1, _ = afterDeclaration Loop.Json
              let m2, eff2 = Loop.update (Loop.Sensed sensedMet) m1

              Expect.isSome m2.ReleaseDoc "release.json projected before the write"

              match eff2 with
              | [ Loop.WriteArtifact(path, content) ] ->
                  Expect.equal path "out/release.json" "writes to the requested out path"
                  Expect.isTrue (content.Contains "\"schemaVersion\":\"fsgg.release/v2\"") "content is the projection (F26 additive bump to v2)"
              | other -> failtestf "expected a single WriteArtifact, got %A" other
          }

          test "Wrote(Ok) emits the summary; Wrote(Error) is a ToolError, never Blocked" {
              let m1, _ = afterDeclaration Loop.Json
              let m2, _ = Loop.update (Loop.Sensed sensedMet) m1

              let m3ok, eff3ok = Loop.update (Loop.Wrote(Ok())) m2

              match eff3ok with
              | [ Loop.EmitSummary _ ] -> Expect.equal m3ok.Phase Loop.Persisted "persisted"
              | other -> failtestf "expected EmitSummary after Wrote(Ok), got %A" other

              let m3err, _ = Loop.update (Loop.Wrote(Error "disk full")) m2
              Expect.equal m3err.Exit Loop.ToolError "write failure → ToolError"
              Expect.equal m3err.Phase Loop.Done "short-circuit to Done"
          }

          test "Emitted reaches Done; further messages are inert" {
              let m1, _ = afterDeclaration Loop.Text
              let m2, _ = Loop.update (Loop.Sensed sensedMet) m1
              let m3, _ = Loop.update Loop.Emitted m2
              Expect.equal m3.Phase Loop.Done "Done"
              let m4, eff4 = Loop.update (Loop.Sensed sensedUnbumped) m3
              Expect.equal m4.Phase Loop.Done "still Done"
              Expect.isEmpty eff4 "no further effects once Done"
          }

          test "exitCode maps the five classes to 0/1/2/3/4" {
              Expect.equal (Loop.exitCode Loop.Success) 0 "Success"
              Expect.equal (Loop.exitCode Loop.Blocked) 1 "Blocked"
              Expect.equal (Loop.exitCode Loop.UsageError') 2 "UsageError'"
              Expect.equal (Loop.exitCode Loop.InputUnavailable) 3 "InputUnavailable"
              Expect.equal (Loop.exitCode Loop.ToolError) 4 "ToolError"
          } ]
