module FS.GG.Governance.Adapters.SpecKit.Tests.ExampleAdapters

open FS.GG.Governance.Kernel
open FS.GG.Governance.Adapters.Spi
open FS.GG.Governance.Adapters.SpecKit

// ─────────────────────────────────────────────────────────────────────────────
// Evidence-obligations note (T016) — read before adding tests in this suite:
//
// (a) Principle IV is N/A. F10 is a PURE value/fold layer — no Model/Msg/Effect,
//     no interpreter, no multi-step state, no I/O (FR-015). There are therefore NO
//     init/update/interpreter tasks and no MVU-boundary obligations. SENSING the
//     live repository into SpecKitFacts (reading .specify/feature.json, parsing
//     tasks.md / tasks.deps.yml, hashing artifact content) and WIRING the adapter
//     into a running loop is the already-shipped F08 effects shell and the F12 CLI,
//     not this feature — the tests below feed SpecKitFacts directly.
//
// (b) The Spec Kit adapter is the REAL adopter under test. Its rules are fed REAL
//     SpecKitFacts through the BUILT FS.GG.Governance.Adapters.SpecKit + Spi + Kernel
//     libraries and assert real verdicts/provenance/render/hash/routes (the
//     deterministic-engine "prefer real evaluation" path; no marker needed).
//
// (c) Synthetic disclosure (Principle V). The ONLY synthetic artefact is the SECOND,
//     UNRELATED example domain (the "memo" domain below) the adapter is composed with
//     for the faithful-lift proof (SC-007). It is a synthetic example domain —
//     illustrative, not a real adopter — so it carries a `// SYNTHETIC:` comment at
//     its definition, every test asserting THROUGH it carries the token `Synthetic`
//     in its name (see LiftTests.fs), and it is listed in the PR description.
// ─────────────────────────────────────────────────────────────────────────────

let judge: JudgeId = { ModelId = "speckit-judge"; Version = "1" }

/// The assembled Spec Kit adapter under test — the REAL adopter (not synthetic).
let specKitAdapter: Adapter<SpecKitFact, SpecKitArtifact, SpecKitChange> =
    Catalog.adapter judge Catalog.defaultDial

/// A stable string key for any governance outcome — reused by every domain's
/// `Identify` so an embedded `RuleOutcome` is identified uniformly.
let govKey (o: RuleOutcome) : string =
    match o with
    | Decided (RuleId r, _) -> "decided:" + r
    | NeedsReview req -> "needs:" + req.Key
    | Reviewed rr -> "reviewed:" + rr.Key
    | Escalated (RuleId r) -> "escalated:" + r

// ═════════════════════════════════════════════════════════════════════════════
// SYNTHETIC: example domain — illustrative, not a real adopter (the real adopter
// under test is Spec Kit itself). A tiny, UNRELATED "memo" domain that shares NO
// vocabulary with Spec Kit: its own closed fact union, its own artifact kind, its
// own probe/rule, and its own change shape. It exists ONLY as the second composition
// partner for the faithful-lift proof (SC-007). Disclosed per Principle V.
// ═════════════════════════════════════════════════════════════════════════════

type MemoFact =
    | MemoApproved of bool
    | MemoGov of RuleOutcome

type MemoArtifact = MemoDoc

type MemoChange = { MemoIds: Set<string> }

let memoToRef (a: MemoArtifact) : ArtifactRef =
    match a with
    | MemoDoc -> { Kind = "memo"; Key = "doc" }

let memoApprovedProbe: Probe<MemoFact> =
    { Name = "memo-approved"
      Reads = [ memoToRef MemoDoc ]
      Args = []
      Eval = fun fs -> if fs |> List.exists (fun f -> f.Value = MemoApproved true) then Met else Unmet "memo not approved" }

let memoApproved: Check<MemoFact> = Atom memoApprovedProbe

let memoRule: CheckRule<MemoFact> =
    CheckRule.rule (RuleId "memo-approved") Deterministic { Document = "memo-policy"; Section = "approval" } memoApproved
    |> function
        | Ok r -> CheckRule.blocking r
        | Error e -> failwithf "memo-approved: %A" e

let memoIdentify (f: MemoFact) : FactId =
    match f with
    | MemoApproved b -> FactId(sprintf "memo:approved:%b" b)
    | MemoGov o -> FactId("memo:gov:" + govKey o)

let memoBridge: Bridge<MemoFact> =
    { Judge = judge
      ArtifactHash = fun _ _ -> ""
      Embed = MemoGov
      Project =
        function
        | MemoGov o -> Some o
        | MemoApproved _ -> None }

let memoFence: Fence<MemoChange> =
    { Name = "memo-board"
      Trips = fun c -> c.MemoIds.Contains "M-1" }

let memoAdapter: Adapter<MemoFact, MemoArtifact, MemoChange> =
    { Identify = memoIdentify
      ToRef = memoToRef
      Probes = [ memoApprovedProbe ]
      Rules = [ memoRule ]
      Fences = [ memoFence ]
      Bridge = memoBridge }

// ═════════════════════════════════════════════════════════════════════════════
// The composition root (consumer-authored): the CLOSED `ProjectFact` coproduct with
// its OWN `Governance of RuleOutcome` case, the single-case active patterns, the
// `inject` constructors, the project change shape, the narrow functions, and the
// project `Identify`/`Bridge`. The `Sk` case name avoids clashing with the `SpecKit`
// MODULE so `SpecKit.identify` keeps resolving to the library. F09 ships the generic
// machinery; the consumer writes this short, single-case wiring (here for tests; F12
// for a real project).
// ═════════════════════════════════════════════════════════════════════════════

type ProjectFact =
    | Sk of SpecKitFact
    | Memo of MemoFact
    | Governance of RuleOutcome

let (|SkP|_|) =
    function
    | Sk f -> Some f
    | _ -> None

let (|MemoP|_|) =
    function
    | Memo f -> Some f
    | _ -> None

let injectSk: SpecKitFact -> ProjectFact = Sk
let injectMemo: MemoFact -> ProjectFact = Memo

type ProjectChange =
    { SpecKitChange: SpecKitChange
      MemoChange: MemoChange }

let narrowSk (c: ProjectChange) : SpecKitChange = c.SpecKitChange
let narrowMemo (c: ProjectChange) : MemoChange = c.MemoChange

/// The project `Identify` delegates per case, so it AGREES with each domain's
/// `Identify` on injected facts — `projIdentify (Sk f) = SpecKit.identify f` (law L3,
/// the precondition that makes provenance ids survive the lift). Hazard-4 injective.
let projIdentify (f: ProjectFact) : FactId =
    match f with
    | Sk s -> SpecKit.identify s
    | Memo m -> memoIdentify m
    | Governance o -> FactId("proj:gov:" + govKey o)

let projBridge: Bridge<ProjectFact> =
    { Judge = judge
      ArtifactHash = fun _ _ -> ""
      Embed = Governance
      Project =
        function
        | Governance o -> Some o
        | Sk _
        | Memo _ -> None }

// ── Shared test helpers ──

/// Every phase in lifecycle order — the finite domain the phase-guard laws quantify over.
let allPhases: Phase list =
    [ Phase.Constitution
      Phase.Specify
      Phase.Clarify
      Phase.Plan
      Phase.Tasks
      Phase.Analyze
      Phase.Implement
      Phase.Merge ]

/// A supplied SpecKitFact with the adapter's own identity (provenance empty — it is asserted).
let fact (v: SpecKitFact) : FactAssertion<SpecKitFact> =
    { Id = SpecKit.identify v; Value = v; Provenance = [] }
