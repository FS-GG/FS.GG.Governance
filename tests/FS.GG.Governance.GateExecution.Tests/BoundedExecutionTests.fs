module FS.GG.Governance.GateExecution.Tests.BoundedExecutionTests

open System
open Expecto
open System.IO
open System.Text
open System.Threading
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.GateExecution
open FS.GG.Governance.GateExecution.Tests.Support
open FS.GG.Governance.GateExecution.Model

// SYNTHETIC: explicit transition and clock messages establish only pure workflow semantics.
// Real direct-child/session/capture controls require independently selected native qualification.
[<Tests>]
let tests =
    testList "BoundedDirectChild" [
        test "Synthetic initial state requests no I/O" {
            let initial = initDirect ()
            Expect.equal initial.Phase Prepared "preparation starts no process"
            Expect.isFalse initial.LaunchConsumed "launch remains unused"
        }
        test "Synthetic one-use launch cannot replay after settlement" {
            let starting, effects = initDirect () |> updateDirect RunRequested
            Expect.equal effects [LaunchDirectChild] "one explicit launch request"
            let running, _ = updateDirect LaunchObserved starting
            let settled, _ = updateDirect DirectResourcesSettled running
            let repeated, replay = updateDirect RunRequested settled
            Expect.isEmpty replay "no second launch"
            Expect.isTrue repeated.LaunchConsumed "consumption remains sticky"
        }
        test "Synthetic cancellation before start requests no launch" {
            let cancelled, effects = initDirect () |> updateDirect (FailureObserved CancellationRequested)
            let repeated, replay = updateDirect RunRequested cancelled
            Expect.isEmpty effects "no process existed to retire"
            Expect.isEmpty replay "cancelled preparation cannot launch"
            Expect.equal repeated.FirstFailure (Some CancellationRequested) "original cancellation retained"
        }
        test "Synthetic first cause survives cleanup deadline and secondary failure" {
            let starting, _ = initDirect () |> updateDirect RunRequested
            let running, _ = updateDirect LaunchObserved starting
            let retiring, effects = updateDirect (FailureObserved OutputLimitReached) running
            let late, repeated = updateDirect (FailureObserved CleanupDeadlineReached) retiring
            Expect.equal effects [RetireDirectResources] "retirement requested once"
            Expect.isEmpty repeated "no renewed retirement effect"
            Expect.equal late.FirstFailure (Some OutputLimitReached) "first cause preserved"
            Expect.equal late.SecondaryFailures [CleanupDeadlineReached] "secondary fact separate"
        }
        test "Synthetic secondary failure storage remains bounded" {
            let original, _ = initDirect () |> updateDirect (FailureObserved CancellationRequested)
            let mutable current = original
            for i in 1..30 do
                current <- updateDirect (FailureObserved (InvalidRequest (string i))) current |> fst
            Expect.isLessThanOrEqual current.SecondaryFailures.Length 8 "fixed secondary capacity"
            Expect.equal current.FirstFailure (Some CancellationRequested) "bounded storage preserves first cause"
        }
        test "Synthetic distinct secondary causes retain cleanup diagnosis" {
            let original, _ = initDirect () |> updateDirect (FailureObserved OutputLimitReached)
            let mutable current = original
            for _ in 1..30 do
                current <- updateDirect (FailureObserved StopIdentityUnknown) current |> fst
            let final, _ = updateDirect (FailureObserved CleanupDeadlineReached) current
            Expect.equal final.FirstFailure (Some OutputLimitReached) "sticky first cause"
            Expect.equal final.SecondaryFailures [StopIdentityUnknown; CleanupDeadlineReached] "distinct secondary causes"
        }
        test "Synthetic late direct settlement preserves bounded deadline failure" {
            let starting, _ = initDirect () |> updateDirect RunRequested
            let running, _ = updateDirect LaunchObserved starting
            let late, _ = updateDirect (FailureObserved CleanupDeadlineReached) running
            let settled, _ = updateDirect DirectResourcesSettled late
            Expect.equal settled.Phase Settled "later direct settlement is a real fact"
            Expect.equal settled.FirstFailure (Some CleanupDeadlineReached) "late settlement never becomes bounded success"
        }
        test "Synthetic release prepared ownership prevents later launch" {
            let settled, _ = initDirect () |> updateDirect DirectResourcesSettled
            let repeated, effects = updateDirect RunRequested settled
            Expect.isEmpty effects "released prepared session cannot launch"
            Expect.isTrue repeated.LaunchConsumed "release consumes the launch"
        }
        test "Synthetic explicit weaker path and descendant acceptance" {
            let policy = {Root="/synthetic";CapturedEnvironment=Map.empty
                          Descendants=AcceptUncontainedUnobservedDescendants;Paths=AcceptObservedPathsWithoutAtomicBinding}
            Expect.equal (validateCurrentHostPolicy policy) None "both choices explicit"
            Expect.equal (validateCurrentHostPolicy {policy with Paths=RequireStableAtomicPathBinding}) (Some UnsupportedGuarantee) "atomic path unavailable"
            Expect.equal (validateCurrentHostPolicy {policy with Descendants=RequireContainedWorkload}) (Some UnsupportedGuarantee) "workload containment unavailable"
        }
        test "Synthetic clock domains and original deadline order refuse" {
            let domain = Guid.Parse "11111111-1111-1111-1111-111111111111"
            let instant ticks : MonotonicInstant = { Domain = domain; Ticks = ticks }
            let budget = {WorkEnd = instant 10L; CleanupEnd = instant 20L}
            Expect.equal (validateBudget (instant 9L) budget) None "remaining original work"
            Expect.equal (validateBudget (instant 10L) budget) (Some WorkDeadlineReached) "exact work end"
            Expect.equal (validateBudget (instant 20L) budget) (Some CleanupDeadlineReached) "exact total end"
            Expect.equal (validateBudget {Domain=Guid.Empty;Ticks=1L} budget) (Some WrongClockDomain) "no foreign clock"
            Expect.isSome (validateBudget (instant 1L) {budget with CleanupEnd=instant 9L}) "no regressing cleanup end"
        }
    ]

// These remaining controls have independent schedules and exact native-owner recipes. A normal
// full suite must not run either merely because the direct ten-case suite was opted in.
[<Tests>]
let heldPipeTests =
    let register =
        if Environment.GetEnvironmentVariable("FSGG_BOUNDED_HELD_PIPE") = "1" then testList else ptestList
    register "BoundedHeldPipe" [
        test "actual holder keeps pipe open after direct exit without upgrading descendants" {
            let control, operation = boundedFixtureControl ()
            withTempDir (fun dir ->
                let ready = Path.Combine(control, "holder-ready.json")
                let release = Path.Combine(control, "holder-release")
                let adopted = Path.Combine(control, "holder-adopted.json")
                let code =
                    "import json,os,pathlib,sys,time\n"
                    + "ready,release,operation=sys.argv[1:]\n"
                    + "parent=os.getpid(); holder=os.fork()\n"
                    + "if holder==0:\n"
                    + " staging=pathlib.Path(ready+'.pending'); staging.write_text(json.dumps({'holder':os.getpid(),'direct':parent,'operation':operation})); staging.rename(ready)\n"
                    + " expiry=time.monotonic()+10\n"
                    + " while time.monotonic()<expiry and not pathlib.Path(release).exists(): time.sleep(.01)\n"
                    + " os._exit(0 if pathlib.Path(release).exists() else 73)\n"
                    + "os._exit(0)\n"
                let original = boundedRequest dir code 1000 3000
                let request =
                    { original with
                        Identity = { original.Identity with Operation = operation }
                        Command =
                            { original.Command with
                                Arguments = [Argument "-c"; Argument code; Argument ready; Argument release; Argument operation] } }
                withBoundedFixture request (fun session ->
                    try
                        let result = Interpreter.run session
                        publishBoundedRunSnapshot control operation request.Identity.Launch "held-run-snapshot" result
                        let readiness = System.Diagnostics.Stopwatch.StartNew()
                        while not (File.Exists adopted) && readiness.Elapsed.TotalSeconds < 2.0 do Thread.Sleep 5
                        Expect.isTrue (File.Exists ready && File.Exists adopted) "actual guardian observed live adopted holder"
                        Expect.equal result.DirectExit (Some (ExitCode 0)) "direct child exited independently of held streams"
                        Expect.notEqual result.Stdout.State EndOfFile "held stdout did not establish EOF"
                        Expect.notEqual result.Stderr.State EndOfFile "held stderr did not establish EOF"
                        Expect.equal result.Descendants UncontainedUnobserved "outer adoption never upgrades product guarantee"
                        Expect.isSome result.FirstFailure "original bounded failure remains recorded"
                        Expect.isLessThanOrEqual result.Stdout.ObservedBytes 4096L "stdout capture stays bounded"
                        publishBoundedFixtureControl control operation request.Identity.Launch "held-pipe-observed" "direct exit observed; EOF absent; descendant scope unchanged"
                    finally
                        File.WriteAllText(release, operation)) )
        }
    ]

[<Tests>]
let unknownStartTests =
    let register =
        if Environment.GetEnvironmentVariable("FSGG_BOUNDED_UNKNOWN_START") = "1" then testList else ptestList
    register "BoundedUnknownStartExternal" [
        test "ambiguous Start retains actual caller until independently owned external retirement" {
            let control, operation = boundedFixtureControl ()
            withTempDir (fun dir ->
                let original = boundedRequest dir "" 1000 3000
                let request =
                    { original with
                        Identity = { original.Identity with Operation = operation }
                        Command = { original.Command with Executable = Executable(Path.Combine(dir, "missing-selected-executable")) } }
                let _, session = acquireBoundedFixture request
                try
                    let result = Interpreter.run session
                    publishBoundedRunSnapshot control operation request.Identity.Launch "unknown-run-snapshot" result
                    Expect.equal result.Launch LaunchOutcomeUnknown "Start failure does not prove no effect"
                    Expect.equal result.Settlement Retained "actual unknown session remains owned"
                    Expect.isNone result.DirectExit "no direct exit was fabricated"
                    Expect.isTrue (Interpreter.release session |> Result.isError) "unknown ownership cannot release"
                    publishBoundedFixtureControl control operation request.Identity.Launch "unknown-start-asserted" "actual session retained; expected external retirement, not test pass"
                with error ->
                    try publishBoundedFixtureControl control operation request.Identity.Launch "unknown-start-failed" (error.GetType().Name)
                    with _ -> () // Missing marker fails external qualification; actual owner still stays alive.
                // Both assertion branches preserve the actual session and CLR owner. This test
                // never returns a passing unit result; its selected guardian records real retirement.
                while true do
                    GC.KeepAlive session
                    Thread.Sleep 20)
        }
    ]


// REAL disposable process controls. The selected native recipe must own the actual runner and
// fixtures independently; direct/EOF facts here never establish descendant or workload cleanup.
[<Tests>]
let realTests =
    // Scheduling only: this switch does not supply fixture ownership or execution admission.
    // Ordinary full-suite runs discover pending cases without launching their process bodies.
    let nativeScheduled =
        System.Environment.GetEnvironmentVariable("FSGG_BOUNDED_NATIVE_FIXTURES") = "1"
    let register = if nativeScheduled then testList else ptestList
    testSequenced <| register "BoundedDirectChildReal" [
        test "literal argv captured environment and absolute cwd are applied" {
            withTempDir (fun dir ->
                let code = "import os,sys;sys.stdout.write(repr(sys.argv[1:])+'\\n'+os.getcwd()+'\\n'+repr(sorted(os.environ.items())))"
                let original = boundedRequest dir code 5000 7000
                let delta = {Added=[{Name=EnvVarName "ADDED";Value=EnvVarValue "one"}]
                             Changed=[{Name=EnvVarName "CHANGE";Old=EnvVarValue "before";New=EnvVarValue "after"}]
                             Removed=[{Name=EnvVarName "REMOVE";Old=EnvVarValue "gone"}]}
                let request = {original with
                                Command={original.Command with Arguments=original.Command.Arguments @ [Argument "";Argument "two words";Argument "$(not-a-shell);*"];Environment=delta}
                                Policy={original.Policy with CapturedEnvironment=Map.ofList ["CHANGE","before";"REMOVE","gone"]}}
                withBoundedFixture request (fun session ->
                    Expect.equal session.Identity request.Identity "custody is held before run"
                    Expect.equal (Interpreter.observe session).Launch NotStarted "preparation launches nothing"
                    let actual = Interpreter.run session
                    Expect.equal actual.DirectExit (Some (ExitCode 0)) "actual normal direct exit"
                    Expect.equal actual.FirstFailure None "no causal failure"
                    Expect.equal actual.Stdout.State EndOfFile "actual EOF"
                    Expect.equal actual.Stderr.State EndOfFile "both streams settle"
                    Expect.equal actual.Settlement Released "direct owner released"
                    Expect.equal actual.Descendants UncontainedUnobserved "no descendant upgrade"
                    let text = Encoding.UTF8.GetString(actual.Stdout.Prefix |> Seq.toArray)
                    Expect.stringContains text "['', 'two words', '$(not-a-shell);*']" "literal ordered arguments"
                    Expect.stringContains text dir "selected cwd"
                    Expect.stringContains text "('ADDED', 'one')" "added value"
                    Expect.stringContains text "('CHANGE', 'after')" "changed baseline"
                    Expect.isFalse (text.Contains "REMOVE") "removed baseline"
                    Expect.isFalse (text.Contains "PATH") "no ambient PATH inheritance"))
        }
        test "real child exit124 remains independent from deadline failure" {
            withTempDir (fun dir ->
                withBoundedFixture (boundedRequest dir "raise SystemExit(124)" 5000 7000) (fun session ->
                    let result = Interpreter.run session
                    Expect.equal result.DirectExit (Some (ExitCode 124)) "actual exit code"
                    Expect.equal result.FirstFailure None "124 is not a timeout"
                    Expect.equal result.Settlement Released "direct settlement"))
        }
        test "concurrent binary stdout stderr are captured without decoding" {
            withTempDir (fun dir ->
                let code = "import os,threading;a=threading.Thread(target=lambda:os.write(1,bytes(range(256))));b=threading.Thread(target=lambda:os.write(2,bytes(reversed(range(256)))));a.start();b.start();a.join();b.join()"
                withBoundedFixture (boundedRequest dir code 5000 7000) (fun session ->
                    let result = Interpreter.run session
                    Expect.equal (result.Stdout.Prefix |> Seq.toArray) [|for i in 0..255 -> byte i|] "raw stdout bytes"
                    Expect.equal (result.Stderr.Prefix |> Seq.toArray) [|for i in 255.. -1..0 -> byte i|] "independent raw stderr"
                    Expect.equal result.Settlement Released "tasks and handles settled"))
        }
        test "overflow caps prefixes and preserves first cause through late settlement" {
            withTempDir (fun dir ->
                let original = boundedRequest dir "import os,time;os.write(1,b'x'*1024);time.sleep(0.8)" 5000 700
                let request={original with Limits={StdoutBytes=32L;StderrBytes=32L;AggregateBytes=48L};Budget={original.Budget with WorkEnd={original.Budget.WorkEnd with Ticks=original.Budget.CleanupEnd.Ticks-1L}}}
                let owner, session = acquireBoundedFixture request
                try
                    let result = Interpreter.run session
                    Expect.equal result.FirstFailure (Some OutputLimitReached) "overflow first cause"
                    Expect.isTrue result.OutputLimitReached "overflow witness"
                    Expect.isLessThanOrEqual result.Stdout.Prefix.Length 32 "stream cap before retention"
                    Expect.isLessThanOrEqual (result.Stdout.Prefix.Length+result.Stderr.Prefix.Length) 48 "aggregate cap"
                    let frozen=result.Stdout.Prefix |> Seq.toArray
                    let later=awaitBoundedFixture owner session
                    Expect.equal later.FirstFailure result.FirstFailure "later fact does not replace cause"
                    Expect.equal (result.Stdout.Prefix |> Seq.toArray) frozen "published snapshot cannot change"
                    Expect.equal later.Settlement Released "self-finishing direct owner later settles"
                finally awaitBoundedFixture owner session |> ignore)
        }
        test "deadline returns retained custody then observes self finishing child without signaling" {
            withTempDir (fun dir ->
                let request=boundedRequest dir "import time;time.sleep(0.8)" 100 250
                let owner, session=acquireBoundedFixture request
                try
                    let elapsed=System.Diagnostics.Stopwatch.StartNew()
                    let result=Interpreter.run session
                    Expect.isLessThan elapsed.Elapsed.TotalSeconds 1.0 "original observation deadline"
                    Expect.equal result.FirstFailure (Some WorkDeadlineReached) "timeout cause separate"
                    Expect.equal result.Settlement Retained "real session remains held"
                    Expect.contains result.SecondaryFailures StopIdentityUnknown "no qualified signal"
                    Expect.contains result.SecondaryFailures CleanupDeadlineReached "distinct cleanup diagnosis"
                    Expect.equal (Interpreter.release session) (Error CleanupDeadlineReached) "pending owner cannot be released"
                    let later=awaitBoundedFixture owner session
                    Expect.equal later.DirectExit (Some (ExitCode 0)) "later actual direct exit"
                    Expect.equal later.Settlement Released "already-owned retirement can settle late"
                    Expect.equal later.FirstFailure result.FirstFailure "no retroactive bounded success"
                finally awaitBoundedFixture owner session |> ignore)
        }
        test "cancellation before start launches nothing and retires prepared resources" {
            withTempDir (fun dir ->
                use cancellation=new CancellationTokenSource()
                cancellation.Cancel()
                let original=boundedRequest dir "raise SystemExit(99)" 5000 7000
                let request={original with Cancellation=cancellation.Token}
                let owner, session=acquireBoundedFixture request
                try
                    let result=Interpreter.run session
                    Expect.equal result.Launch NotStarted "no Start"
                    Expect.equal result.FirstFailure (Some CancellationRequested) "original cancellation"
                    Expect.equal (awaitBoundedFixture owner session).Settlement Released "prepared handles retired"
                finally awaitBoundedFixture owner session |> ignore)
        }
        test "cancellation during work retains actual owner until self finish" {
            withTempDir (fun dir ->
                use cancellation=new CancellationTokenSource()
                let original=boundedRequest dir "import time;time.sleep(0.8)" 5000 6000
                let request={original with Cancellation=cancellation.Token}
                withBoundedFixture request (fun session ->
                    cancellation.CancelAfter 100
                    let result=Interpreter.run session
                    Expect.equal result.FirstFailure (Some CancellationRequested) "cancellation is causal"
                    Expect.isTrue result.CancellationRequested "requested independently"
                    Expect.equal result.DirectExit (Some (ExitCode 0)) "no unsafe signal needed"
                    Expect.equal result.Settlement Released "self finishing direct resources"))
        }
        test "a settled session cannot execute a second launch" {
            withTempDir (fun dir ->
                let count=Path.Combine(dir,"launch-count")
                let code="import sys;open(sys.argv[1],'a').write('x')"
                let original=boundedRequest dir code 5000 7000
                let request={original with Command={original.Command with Arguments=original.Command.Arguments @ [Argument count]}}
                withBoundedFixture request (fun session ->
                    let first=Interpreter.run session
                    Expect.equal first.Settlement Released "first settles"
                    let repeated=Interpreter.run session
                    Expect.equal repeated.FirstFailure (Some AlreadyRun) "explicit replay refusal"
                    Expect.equal (File.ReadAllText count) "x" "one real launch only"))
        }
        test "changed cwd observation refuses before Start and settles preparation" {
            withTempDir (fun dir ->
                let owner, session=acquireBoundedFixture (boundedRequest dir "raise SystemExit(99)" 5000 7000)
                try
                    Directory.SetLastWriteTimeUtc(dir, DateTime.UtcNow.AddMinutes(1.0))
                    let result=Interpreter.run session
                    Expect.equal result.Launch NotStarted "changed metadata refused before Start"
                    Expect.equal result.FirstFailure (Some (InvalidRequest "directory-observation-changed")) "observed change"
                    Expect.equal (awaitBoundedFixture owner session).Settlement Released "no permanent prepared owner"
                finally awaitBoundedFixture owner session |> ignore)
        }
        test "stronger guarantees and relative executable refuse preparation" {
            withTempDir (fun dir ->
                let request=boundedRequest dir "raise SystemExit(99)" 5000 7000
                let refused candidate expected =
                    match Interpreter.prepare candidate with
                    | Error causes -> Expect.contains causes expected "prelaunch refusal"
                    | Ok session ->
                        Interpreter.release session |> ignore
                        failtest "unsupported request prepared"
                refused {request with Policy={request.Policy with Descendants=RequireContainedWorkload}} UnsupportedGuarantee
                refused {request with Policy={request.Policy with Paths=RequireStableAtomicPathBinding}} UnsupportedGuarantee
                refused {request with Command={request.Command with Executable=Executable "python3"}} (InvalidRequest "absolute-executable-required")
                refused {request with Policy={request.Policy with Root=dir+"-other"}} (InvalidRequest "cwd-outside-selected-root"))
        }
    ]
