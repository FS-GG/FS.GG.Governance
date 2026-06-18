module FS.GG.Governance.Host.Tests.Support

open System
open System.IO
open FS.GG.Governance.Kernel
open FS.GG.Governance.Host

// Shared test scaffolding: a domain-neutral fact vocabulary, a config/bridge, and the edge
// environment (a REAL temp-filesystem fixture + a REAL-fs review store; the judge is the ONLY
// fake — see makeEnv). The judge is synthetic because a real agent cannot be a reproducible
// test oracle (spec Assumptions, Principle V); reads and the store are real I/O.

/// The adapter's 'fact: either a sensed artifact (ref + raw content) or an embedded governance
/// RuleOutcome. Two artifact-content facts collapse by ref; outcomes by their rule/key.
type TFact =
    | Artifact of ref: ArtifactRef * content: string
    | Outcome of RuleOutcome

let apiRef: ArtifactRef = { Kind = "file"; Key = "src/Api.fs" }

let outcomeId =
    function
    | Decided (RuleId r, _) -> "decided:" + r
    | NeedsReview req -> "needs:" + req.Key
    | RuleOutcome.Reviewed rr -> "reviewed:" + rr.Key
    | Escalated (RuleId r) -> "escalated:" + r

let identify =
    function
    | Artifact (ref, _) -> FactId(sprintf "artifact:%s/%s" ref.Kind ref.Key)
    | Outcome o -> FactId("outcome:" + outcomeId o)

let readContent (facts: FactSet<TFact>) (ref: ArtifactRef) : string option =
    facts
    |> List.tryPick (fun fa ->
        match fa.Value with
        | Artifact (r, c) when r = ref -> Some c
        | _ -> None)

// Content-dependent, NON-EMPTY when sensed (so sensing-completion detection works) and "" when
// absent (the documented sentinel). Changing content changes the hash → a different cache key.
let artifactHash (facts: FactSet<TFact>) (ref: ArtifactRef) : string =
    match readContent facts ref with
    | Some c -> "h:" + string (c.GetHashCode())
    | None -> ""

let bridge: Bridge<TFact> =
    { Judge = { ModelId = "test-judge"; Version = "1" }
      ArtifactHash = artifactHash
      Embed = Outcome
      Project =
        (function
        | Outcome o -> Some o
        | Artifact _ -> None) }

/// An AgentReviewed rule over a probe that reads `apiRef`, asking a fixed question.
let apiRule: CheckRule<TFact> =
    let check = Check.probe "reviewApi" [ apiRef ] [] (fun _ -> Met)

    match CheckRule.rule (RuleId "R1") AgentReviewed { Document = "doc"; Section = "api" } check with
    | Ok r -> r |> CheckRule.blocking |> CheckRule.asking "Does the API meet the bar?"
    | Error _ -> failwith "apiRule construction failed"

let change: Set<string> = set [ "src/Api.fs" ]

let makeConfig (policy: AcceptancePolicy) (rules: CheckRule<TFact> list) : LoopConfig<Set<string>, TFact> =
    { Identify = identify
      Rules = rules
      Bridge = bridge
      Fences = [ { Name = "merge-boundary"; Trips = fun (c: Set<string>) -> c |> Set.exists (fun p -> p.StartsWith "src/") } ]
      Mode = Gate
      Policy = policy
      SenseArtifact = fun ref content -> Artifact(ref, content)
      ReadContent = readContent }

let defaultConfig = makeConfig Loop.defaultPolicy [ apiRule ]

// ── A real verdict <-> file codec for the filesystem store ──

let verdictToString =
    function
    | Pass -> "Pass"
    | Fail r -> "Fail\t" + r
    | Uncertain r -> "Uncertain\t" + r

let verdictOfString (s: string) =
    let parts = s.Split('\t')

    match parts.[0] with
    | "Fail" -> Fail(if parts.Length > 1 then parts.[1] else "")
    | "Uncertain" -> Uncertain(if parts.Length > 1 then parts.[1] else "")
    | _ -> Pass

/// A REAL local-filesystem review store under `dir` (Principle V — real evidence).
let fsStore (dir: string) : ReviewStore =
    { Load =
        fun key ->
            let path = Path.Combine(dir, key)

            if File.Exists path then
                let lines = File.ReadAllLines path
                Ok(Some { Rule = RuleId lines.[0]; Key = lines.[1]; Verdict = verdictOfString lines.[2] })
            else
                Ok None
      Save =
        fun rr ->
            let (RuleId r) = rr.Rule
            File.WriteAllLines(Path.Combine(dir, rr.Key), [| r; rr.Key; verdictToString rr.Verdict |])
            Ok() }

/// The edge environment for an interpreter run: a real temp fixture dir holding `Api.fs` with the
/// given content, a real-fs store dir, a counting/configurable FAKE judge, and a capturing sink.
type Env =
    { Ports: Ports
      Dispatches: int ref
      Outputs: ResizeArray<Output>
      FixtureDir: string
      Cleanup: unit -> unit }

let makeEnvWith (apiContent: string) (judge: Judge) : Env =
    let root = Path.Combine(Path.GetTempPath(), "fsgg-host-" + Guid.NewGuid().ToString("N"))
    let fixtureDir = Path.Combine(root, "fixture")
    let storeDir = Path.Combine(root, "store")
    Directory.CreateDirectory fixtureDir |> ignore
    Directory.CreateDirectory storeDir |> ignore
    File.WriteAllText(Path.Combine(fixtureDir, "Api.fs"), apiContent)

    let dispatches = ref 0
    let outputs = ResizeArray<Output>()

    let read: ArtifactReader =
        fun ref ->
            try
                let name = match Path.GetFileName ref.Key with | null -> ref.Key | n -> n
                Ok(File.ReadAllText(Path.Combine(fixtureDir, name)))
            with e ->
                Error e.Message

    let countingJudge: Judge =
        fun task ->
            incr dispatches
            judge task

    let ports =
        { Read = read
          Judge = countingJudge
          Store = fsStore storeDir
          Sink = fun out -> outputs.Add out }

    { Ports = ports
      Dispatches = dispatches
      Outputs = outputs
      FixtureDir = fixtureDir
      Cleanup = fun () -> (try Directory.Delete(root, true) with _ -> ()) }

/// A passing fake judge. SYNTHETIC: a real agent is not a reproducible oracle (F12 supplies the
/// real judge port). Every test that uses this carries the `Synthetic` token in its name.
let passingJudge: Judge =
    fun _task -> Ok { Verdict = Pass; Confidence = 1.0 }

let makeEnv () = makeEnvWith "let x = 1" passingJudge
