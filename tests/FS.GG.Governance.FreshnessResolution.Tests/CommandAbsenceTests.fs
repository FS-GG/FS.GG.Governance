module FS.GG.Governance.FreshnessResolution.Tests.CommandAbsenceTests

open Expecto
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.FreshnessResolution
open FS.GG.Governance.FreshnessResolution.Model
open FS.GG.Governance.FreshnessResolution.Tests.Support

// User Story 1 edge — CONSISTENT command absence (SC-003, L-command-absent). A gate with `Command = None`, all
// repo-wide facts and covered artifacts sensed, resolves to `Resolved` with `Command = None` AND
// `CommandVersion = None`, and is NEVER `Unresolved` on a command-version basis (no `MissingCommandVersion` for
// a command-less gate) — even when `CommandVersions` is empty or omits every command.

let private onlyOutcome (gates: Gate list) (s: SensedFacts) : ResolutionOutcome =
    match FreshnessResolution.entries (FreshnessResolution.resolve gates s) with
    | [ e ] -> e.Outcome
    | other -> failwithf "expected exactly one entry, got %d" (List.length other)

[<Tests>]
let tests =
    testList
        "CommandAbsence"
        [ test "worked example B: command-less gate, fully sensed ⇒ Resolved with Command=None and CommandVersion=None (SC-003)" {
              match onlyOutcome [ gDocsCheck ] (senseFully gDocsCheck) with
              | Resolved i ->
                  Expect.equal i.Command None "command-less gate keeps Command = None"
                  Expect.equal i.CommandVersion None "command-less gate has CommandVersion = None"
              | Unresolved facts -> failtestf "expected Resolved for a command-less gate, got Unresolved %A" facts
          }

          test "command-less gate resolves even when CommandVersions carries OTHER commands (never MissingCommandVersion)" {
              // fullSensed carries dotnet/eslint versions and covers docs:check (as an empty artifact set).
              match onlyOutcome [ gDocsCheck ] fullSensed with
              | Resolved i ->
                  Expect.equal i.Command None "Command stays None"
                  Expect.equal i.CommandVersion None "CommandVersion stays None despite other commands sensed"
                  Expect.equal i.CoveredArtifacts [] "the sensed-empty covered set resolves to []"
              | Unresolved facts -> failtestf "expected Resolved, got Unresolved %A" facts
          }

          test "command-less gate with EMPTY CommandVersions is never MissingCommandVersion" {
              let emptyCmds = { senseFully gDocsCheck with CommandVersions = Map.empty }

              match onlyOutcome [ gDocsCheck ] emptyCmds with
              | Resolved i -> Expect.equal i.CommandVersion None "no command ⇒ no version, not a gap"
              | Unresolved facts ->
                  Expect.isFalse (List.contains MissingCommandVersion facts) "MissingCommandVersion impossible for a command-less gate"
          }

          testPropertyWithConfig fscheckConfig "command-less gate: fully sensed ⇒ Resolved, CommandVersion=None, never MissingCommandVersion"
          <| fun (g: Gate) ->
              // Force the gate command-less (identity + key), keeping everything else.
              let gLess =
                  { g with FreshnessKey = { g.FreshnessKey with Command = None } }

              match onlyOutcome [ gLess ] (senseFully gLess) with
              | Resolved i -> i.Command = None && i.CommandVersion = None
              | Unresolved _ -> false ]
