module FS.GG.Governance.GateRun.Tests.PlanTests

open Expecto
open FsCheck
open FS.GG.Governance.Config.Model
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.GateExecution.Model
open FS.GG.Governance.GateRun
open FS.GG.Governance.GateRun.Model
open FS.GG.Governance.GateRun.Tests.Support

// ── (1) lexCommandLine — a literal POSIX-style argv split, no shell features (data-model §lex, D1) ──

[<Tests>]
let lexTests =
    testList
        "Plan.lexCommandLine"
        [ test "splits on whitespace; first token is the executable, rest the ordered args" {
              Expect.equal
                  (Plan.lexCommandLine "dotnet test --no-build")
                  (Some(Executable "dotnet", [ Argument "test"; Argument "--no-build" ]))
                  "argv split"
          }

          test "single quotes group a token verbatim" {
              Expect.equal
                  (Plan.lexCommandLine "echo 'hello world'")
                  (Some(Executable "echo", [ Argument "hello world" ]))
                  "single-quoted group"
          }

          test "double quotes group; a backslash escapes inside them" {
              Expect.equal
                  (Plan.lexCommandLine "echo \"a b\" c")
                  (Some(Executable "echo", [ Argument "a b"; Argument "c" ]))
                  "double-quoted group"

              Expect.equal
                  (Plan.lexCommandLine "echo \"a\\\"b\"")
                  (Some(Executable "echo", [ Argument "a\"b" ]))
                  "escaped quote inside double quotes"
          }

          test "a bare backslash escapes the next character" {
              Expect.equal
                  (Plan.lexCommandLine "a\\ b")
                  (Some(Executable "a b", []))
                  "escaped space joins the token"
          }

          test "argument order is preserved (identity-significant)" {
              Expect.equal
                  (Plan.lexCommandLine "tool -a -b")
                  (Some(Executable "tool", [ Argument "-a"; Argument "-b" ]))
                  "-a then -b"

              Expect.notEqual
                  (Plan.lexCommandLine "tool -a -b")
                  (Plan.lexCommandLine "tool -b -a")
                  "order matters"
          }

          test "empty / all-whitespace line ⇒ None" {
              Expect.equal (Plan.lexCommandLine "") None "empty"
              Expect.equal (Plan.lexCommandLine "   " ) None "spaces"
              Expect.equal (Plan.lexCommandLine "\t \n") None "mixed whitespace"
          }

          test "no shell features: glob/pipe/var/redirect are literal token characters" {
              Expect.equal
                  (Plan.lexCommandLine "sh *.fs | grep $VAR > out")
                  (Some(
                      Executable "sh",
                      [ Argument "*.fs"; Argument "|"; Argument "grep"; Argument "$VAR"; Argument ">"; Argument "out" ]
                  ))
                  "every metacharacter is a literal token"
          }

          testProperty "round-trip: whitespace-joined plain tokens lex back to those tokens"
          <| fun (tokens: NonEmptyString list) ->
              // Generate plain tokens with no whitespace/quote/backslash, non-empty, then join with single
              // spaces — the lex must recover exactly those tokens in order.
              let clean =
                  tokens
                  |> List.map (fun (NonEmptyString s) ->
                      s |> String.filter (fun c -> c <> ' ' && c <> '\t' && c <> '\n' && c <> '\r' && c <> '\'' && c <> '"' && c <> '\\'))
                  |> List.filter (fun s -> s <> "")

              match clean with
              | [] -> true // nothing to assert (no usable tokens)
              | exe :: args ->
                  let line = String.concat " " clean
                  Plan.lexCommandLine line = Some(Executable exe, args |> List.map Argument) ]

// ── (2) commandFor — declared spec → GateCommand; None on no-command / unresolved / empty-lex ──

[<Tests>]
let commandForTests =
    testList
        "Plan.commandFor"
        [ test "a gate with a resolvable RequiresCommand ⇒ Some with declared inputs verbatim" {
              let tooling = toolingOf [ commandSpec "dotnet-test" "dotnet test --no-build" ]
              let gate = gateWithCommand "tests" "dotnet-test"

              match Plan.commandFor "/repo" tooling gate with
              | None -> failtest "expected Some command"
              | Some cmd ->
                  Expect.equal cmd.Executable (Executable "dotnet") "executable from lex"
                  Expect.equal cmd.Arguments [ Argument "test"; Argument "--no-build" ] "ordered args from lex"
                  Expect.equal cmd.WorkingDirectory (WorkingDirectory "/repo") "cwd = repoRoot"
                  Expect.equal cmd.Environment { Added = []; Changed = []; Removed = [] } "EMPTY env delta (no ambient leak)"
                  Expect.equal cmd.Timeout (TimeoutLimit 600) "declared timeout verbatim"
                  Expect.equal cmd.CapturedOutput NoCapturedOutput "no captured-output target"
          }

          test "a gate with NO RequiresCommand prerequisite ⇒ None" {
              let tooling = toolingOf [ commandSpec "dotnet-test" "dotnet test" ]
              Expect.equal (Plan.commandFor "/repo" tooling (gateWithoutCommand "tests")) None "no command ⇒ None"
          }

          test "a RequiresCommand that resolves to no CommandSpec ⇒ None" {
              let tooling = toolingOf [ commandSpec "other" "dotnet test" ]
              Expect.equal (Plan.commandFor "/repo" tooling (gateWithCommand "tests" "dotnet-test")) None "unresolved ⇒ None"
          }

          test "a command line that lexes to nothing ⇒ None" {
              let tooling = toolingOf [ commandSpec "blank" "   " ]
              Expect.equal (Plan.commandFor "/repo" tooling (gateWithCommand "tests" "blank")) None "empty lex ⇒ None"
          } ]

// ── (3) priorExitOf — round-trip against a REAL referenceOf of a senseExecution record ──

[<Tests>]
let priorExitTests =
    testList
        "Plan.priorExitOf"
        [ test "round-trips the exit code of a real referenceOf for several distinct codes" {
              for code in [ 0; 1; 42; 124; 127 ] do
                  let reference = realReferenceFor code sampleCommand
                  Expect.equal (Plan.priorExitOf reference) (Some(ExitCode code)) (sprintf "recovered exit %d" code)
          }

          test "a non-canonical reference ⇒ None (⇒ recompute, never reuse)" {
              Expect.equal (Plan.priorExitOf nonCanonicalRef) None "not-canonical ⇒ None"
              Expect.equal (Plan.priorExitOf (EvidenceRef "")) None "empty ⇒ None"
              Expect.equal (Plan.priorExitOf (EvidenceRef "synthetic://whatever")) None "foreign shape ⇒ None"
          } ]

// ── (4) passed — exit 0 is pass; any non-zero (incl. F051 sentinels) is fail ──

[<Tests>]
let passedTests =
    testList
        "Plan.passed"
        [ test "exit 0 ⇒ true; non-zero incl. sentinels ⇒ false" {
              Expect.isTrue (Plan.passed (ExitCode 0)) "0 is pass"
              Expect.isFalse (Plan.passed (ExitCode 1)) "1 is fail"
              Expect.isFalse (Plan.passed (ExitCode 124)) "timeout sentinel is fail"
              Expect.isFalse (Plan.passed (ExitCode 127)) "start-failure sentinel is fail"
              Expect.isFalse (Plan.passed (ExitCode -1)) "negative is fail"
          } ]
