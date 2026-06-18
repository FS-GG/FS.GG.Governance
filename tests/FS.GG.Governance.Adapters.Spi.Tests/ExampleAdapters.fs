module FS.GG.Governance.Adapters.Spi.Tests.ExampleAdapters

open FS.GG.Governance.Kernel
open FS.GG.Governance.Adapters.Spi
open Check // the ==> operator and the smart constructors

// ─────────────────────────────────────────────────────────────────────────────
// Evidence-obligations note (T010) — read before adding tests in this suite:
//
// (a) Principle IV is N/A. F09 is a PURE value/fold layer — no Model/Msg/Effect,
//     no interpreter, no multi-step state, no I/O. There are therefore NO
//     init/update/interpreter tasks and no MVU-boundary obligations. Wiring a
//     *composed* catalog into a running loop is the already-shipped F08 effects
//     shell and the F12 CLI, not this feature.
//
// (b) Synthetic disclosure (Principle V). F09 ships NO concrete production adapter
//     (F10 delivers Spec Kit, F11 the design system). Generality is proven by the
//     TWO UNRELATED, neutral example domains authored below — a "document" domain
//     and an unrelated "task" domain. They are SYNTHETIC example domains:
//     illustrative, not real adopters. Each carries a `// SYNTHETIC:` comment at
//     its definition, every test asserting THROUGH them carries the token
//     `Synthetic` in its name, and they are listed in the PR description. Note that
//     the example adapters ARE the system-under-test for a generic SPI (not
//     substitutes for a real dependency), so the synthetic framing is a deliberate,
//     conservative application of Principle V — the real adopters are F10/F11.
//
// (c) Real evaluation needs no marker. Everything else — the kernel facts/rules fed
//     through the BUILT FS.GG.Governance.Adapters.Spi + FS.GG.Governance.Kernel
//     libraries, and every assertion over their real verdicts/provenance/render/
//     hash/route — is REAL evaluation (the deterministic-engine "prefer real" path).
// ─────────────────────────────────────────────────────────────────────────────

let judge: JudgeId = { ModelId = "example-judge"; Version = "1" }

/// A stable string key for any governance outcome — reused by every domain's
/// `Identify` so an embedded `RuleOutcome` is identified uniformly (D8).
let govKey (o: RuleOutcome) : string =
    match o with
    | Decided (RuleId r, _) -> "decided:" + r
    | NeedsReview req -> "needs:" + req.Key
    | Reviewed rr -> "reviewed:" + rr.Key
    | Escalated (RuleId r) -> "escalated:" + r

// ═════════════════════════════════════════════════════════════════════════════
// SYNTHETIC: example domain A — a tiny "document" domain. Illustrative, not a real
// adopter (real adapters are F10/F11). Its own closed vocabulary: title presence and
// an embedded governance outcome; its own artifact kind and its own change shape.
// ═════════════════════════════════════════════════════════════════════════════

type DocFact =
    | HasTitle of bool
    | DocGov of RuleOutcome

type DocArtifact = DocBody

/// This domain's change: the set of touched document paths (its OWN change shape).
type DocChange = { DocPaths: Set<string> }

let docToRef (a: DocArtifact) : ArtifactRef =
    match a with
    | DocBody -> { Kind = "doc"; Key = "body" }

let titledProbe: Probe<DocFact> =
    { Name = "has-title"
      Reads = [ docToRef DocBody ]
      Args = []
      Eval = fun fs -> if fs |> List.exists (fun f -> f.Value = HasTitle true) then Met else Unmet "no title" }

let titled: Check<DocFact> = Atom titledProbe

/// A Deterministic, reified rule: the document must have a title.
let docTitledRule: CheckRule<DocFact> =
    CheckRule.rule (RuleId "doc-titled") Deterministic { Document = "doc-policy"; Section = "title" } titled
    |> function
        | Ok r -> CheckRule.blocking r
        | Error e -> failwithf "doc-titled: %A" e

/// An AgentReviewed rule over an OPAQUE check — for the lifted-opacity proof (V65):
/// it can never be Deterministic and always routes to review.
let docReviewRule: CheckRule<DocFact> =
    let manualReview = Opaque("manual-review", fun _ -> Unknown "subjective — a judge must rule")

    CheckRule.rule (RuleId "doc-reviewed") AgentReviewed { Document = "doc-policy"; Section = "review" } manualReview
    |> function
        | Ok r -> CheckRule.asking "Is the document well written?" r
        | Error e -> failwithf "doc-reviewed: %A" e

let docIdentify (f: DocFact) : FactId =
    match f with
    | HasTitle b -> FactId(sprintf "doc:title:%b" b)
    | DocGov o -> FactId("doc:gov:" + govKey o)

let docBridge: Bridge<DocFact> =
    { Judge = judge
      ArtifactHash = fun _ _ -> ""
      Embed = DocGov
      Project =
        function
        | DocGov o -> Some o
        | HasTitle _ -> None }

let docFence: Fence<DocChange> =
    { Name = "doc-body"
      Trips = fun c -> c.DocPaths.Contains "doc.md" }

let docAdapter: Adapter<DocFact, DocArtifact, DocChange> =
    { Identify = docIdentify
      ToRef = docToRef
      Probes = [ titledProbe ]
      Rules = [ docTitledRule; docReviewRule ]
      Fences = [ docFence ]
      Bridge = docBridge }

// ═════════════════════════════════════════════════════════════════════════════
// SYNTHETIC: example domain B — an UNRELATED "task" domain. Illustrative, not a real
// adopter. It shares NO vocabulary or layout with the document domain (T032/FR-010):
// a distinct `'fact` union, a distinct artifact kind, a distinct probe, a distinct
// change shape, and a distinct fence. Composing it needs only its own one-line
// `inject`/active-pattern at the root — nothing about it is reshaped to resemble doc.
// ═════════════════════════════════════════════════════════════════════════════

type TaskFact =
    | TaskClosed of bool
    | TaskGov of RuleOutcome

type TaskArtifact = TaskCard

/// This domain's change: the set of touched task ids (its OWN change shape — note it
/// is a DIFFERENT type from `DocChange`, so the two compose only via `Lift.fence`).
type TaskChange = { TaskIds: Set<string> }

let taskToRef (a: TaskArtifact) : ArtifactRef =
    match a with
    | TaskCard -> { Kind = "task"; Key = "card" }

let closedProbe: Probe<TaskFact> =
    { Name = "task-closed"
      Reads = [ taskToRef TaskCard ]
      Args = []
      Eval = fun fs -> if fs |> List.exists (fun f -> f.Value = TaskClosed true) then Met else Unmet "task open" }

let closed: Check<TaskFact> = Atom closedProbe

/// A Deterministic, reified rule: the task must be closed.
let taskClosedRule: CheckRule<TaskFact> =
    CheckRule.rule (RuleId "task-closed") Deterministic { Document = "task-policy"; Section = "closure" } closed
    |> function
        | Ok r -> r
        | Error e -> failwithf "task-closed: %A" e

let taskIdentify (f: TaskFact) : FactId =
    match f with
    | TaskClosed b -> FactId(sprintf "task:closed:%b" b)
    | TaskGov o -> FactId("task:gov:" + govKey o)

let taskBridge: Bridge<TaskFact> =
    { Judge = judge
      ArtifactHash = fun _ _ -> ""
      Embed = TaskGov
      Project =
        function
        | TaskGov o -> Some o
        | TaskClosed _ -> None }

let taskFence: Fence<TaskChange> =
    { Name = "task-board"
      Trips = fun c -> c.TaskIds.Contains "T-1" }

let taskAdapter: Adapter<TaskFact, TaskArtifact, TaskChange> =
    { Identify = taskIdentify
      ToRef = taskToRef
      Probes = [ closedProbe ]
      Rules = [ taskClosedRule ]
      Fences = [ taskFence ]
      Bridge = taskBridge }

// ═════════════════════════════════════════════════════════════════════════════
// The composition root (consumer-authored, D8): the CLOSED `ProjectFact` coproduct
// with its OWN `Gov of RuleOutcome` case, the single-case active patterns, the
// `inject` constructors, the project change shape, the narrow functions, and the
// project `Identify`/`Bridge`. F09 ships the generic machinery; the consumer writes
// this short, single-case wiring (here for the tests; F12 for a real project).
// ═════════════════════════════════════════════════════════════════════════════

type ProjectFact =
    | Doc of DocFact
    | Task of TaskFact
    | Gov of RuleOutcome

let (|DocP|_|) =
    function
    | Doc f -> Some f
    | _ -> None

let (|TaskP|_|) =
    function
    | Task f -> Some f
    | _ -> None

/// The project's change shape — carries each domain's change independently.
type ProjectChange =
    { Docs: Set<string>
      Tasks: Set<string> }

let narrowDoc (c: ProjectChange) : DocChange = { DocPaths = c.Docs }
let narrowTask (c: ProjectChange) : TaskChange = { TaskIds = c.Tasks }

/// The project `Identify` delegates per case, so it AGREES with each domain's
/// `Identify` on injected facts — `projIdentify (Doc f) = docIdentify f` (law L3, the
/// precondition that makes provenance ids survive the lift). Hazard-4 injective.
let projIdentify (f: ProjectFact) : FactId =
    match f with
    | Doc d -> docIdentify d
    | Task t -> taskIdentify t
    | Gov o -> FactId("proj:gov:" + govKey o)

let projBridge: Bridge<ProjectFact> =
    { Judge = judge
      ArtifactHash = fun _ _ -> ""
      Embed = Gov
      Project =
        function
        | Gov o -> Some o
        | Doc _
        | Task _ -> None }

// ── The single, named cross-domain coupling: an `Implies` over the coproduct ──
// "if the document is titled, the task must be governed." Authored ONCE at the root
// over the coproduct's facts (FR-007/FR-012). Its ANTECEDENT reads Doc facts: when the
// document domain is removed, `titled` reads project facts that are never present and
// reports `Unmet` ("not a titled doc"), so `Implies = Any [Not antecedent; consequent]`
// is vacuously satisfied — the rule goes INERT, never an error (law R2/R3, FR-009).
// The consequent reads a SUPPLIED task fact (proper stratification — theory Hazard 1: a
// cross-domain rule must not negate/condition on a fact another adapter DERIVES in the
// same fixed point, or the result depends on round order). "The task is closed" is a
// supplied fact, so the cross-domain rule decides ONE stable verdict per run.
let taskGoverned: Check<ProjectFact> =
    Check.probe "task-closed" [] [] (fun (fs: FactSet<ProjectFact>) ->
        if
            fs
            |> List.exists (fun f ->
                match f.Value with
                | Task (TaskClosed true) -> true
                | _ -> false)
        then
            Met
        else
            Unmet "task not closed")

/// Reusable cross-domain rule (Deterministic, reified, Blocking) over the coproduct.
let crossDomainRule: CheckRule<ProjectFact> =
    let antecedent = Lift.check (|DocP|_|) titled
    let crossCheck = antecedent ==> taskGoverned

    CheckRule.rule (RuleId "doc-implies-task-gov") Deterministic { Document = "root"; Section = "x-domain" } crossCheck
    |> function
        | Ok r -> CheckRule.blocking r
        | Error e -> failwithf "doc-implies-task-gov: %A" e

let crossDomainRules: CheckRule<ProjectFact> list = [ crossDomainRule ]
