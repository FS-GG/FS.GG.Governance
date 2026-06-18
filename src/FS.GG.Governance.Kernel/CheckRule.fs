namespace FS.GG.Governance.Kernel

// The CheckTier arbitration model & the Rule bridge to the kernel (F04).
//
// The matching CheckRule.fsi is the SOLE visibility declaration — no top-level binding
// here carries private/internal/public (Principle II); helpers absent from the .fsi
// (digest, …) are hidden by signature, exactly as F03's Check.fs hides its helpers.
// Pure values and total folds only: the bridge performs NO agent call and NO I/O
// (FR-015). `toRule` emits a `RuleOutcome` as data (a verdict, a review request, or an
// escalation); the actual dispatch/recording is the F08 edge interpreter's job. Reuses
// the in-assembly F03 `Check` interpreters and F01 `Rule`/`FactSet`; zero new deps
// (SHA-256 is `System.Security.Cryptography`, the same hash F03's `Check.hash` uses).

open System
open System.Security.Cryptography
open System.Text

type CheckTier =
    | Deterministic
    | AgentReviewed
    | HumanOnly

type Severity =
    | Advisory
    | Blocking

type SpecSource = { Document: string; Section: string }

type JudgeId = { ModelId: string; Version: string }

type ReviewRequest =
    { Rule: RuleId
      Question: string option
      Key: string }

type RecordedReview =
    { Rule: RuleId
      Key: string
      Verdict: Verdict }

type RuleOutcome =
    | Decided of rule: RuleId * verdict: Verdict
    | NeedsReview of request: ReviewRequest
    | Reviewed of review: RecordedReview
    | Escalated of rule: RuleId

type CheckRule<'fact> =
    { Id: RuleId
      Tier: CheckTier
      Spec: SpecSource
      Severity: Severity
      Check: Check<'fact>
      Question: string option }

type RuleRejection =
    | OpaqueCannotBeDeterministic of RuleId

type Bridge<'fact> =
    { Judge: JudgeId
      ArtifactHash: FactSet<'fact> -> ArtifactRef -> string
      Embed: RuleOutcome -> 'fact
      Project: 'fact -> RuleOutcome option }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CheckRule =

    // ── Smart constructors: the readable rule-authoring surface (FR-005) ──

    let rule
        (id: RuleId)
        (tier: CheckTier)
        (spec: SpecSource)
        (check: Check<'fact>)
        : Result<CheckRule<'fact>, RuleRejection> =
        // FR-006 guardrail: an Opaque (non-reified) check can never masquerade as
        // Deterministic — refuse here so the bad tier is UNCONSTRUCTABLE, not caught
        // later at bridge time. Every other tier (and Deterministic over a reified
        // check) succeeds; Severity defaults to Advisory, Question to None.
        match tier with
        | Deterministic when not (Check.isReified check) -> Error(OpaqueCannotBeDeterministic id)
        | _ ->
            Ok
                { Id = id
                  Tier = tier
                  Spec = spec
                  Severity = Advisory
                  Check = check
                  Question = None }

    let blocking (rule: CheckRule<'fact>) : CheckRule<'fact> = { rule with Severity = Blocking }

    let asking (prompt: string) (rule: CheckRule<'fact>) : CheckRule<'fact> =
        // Targets AgentReviewed (which accepts any check), so this is the natural
        // constructor for an agent rule over an Opaque check and never trips FR-006.
        { rule with
            Tier = AgentReviewed
            Question = Some prompt }

    // ── The cache key: decision #1, a pure fold over its ingredients (FR-011/FR-012) ──

    // SHA-256 hex digest of a string — fixed-width (64 hex chars, no '|'), so embedding
    // digests in a '|'-separated pre-image is prefix-free. The SAME hash discipline
    // F03's Check.hash uses (System.* only — zero new deps, FR-016/SC-009).
    let digest (s: string) : string =
        use sha = SHA256.Create()
        sha.ComputeHash(Encoding.UTF8.GetBytes s)
        |> Array.map (fun b -> b.ToString("x2"))
        |> String.concat ""

    let cacheKey
        (judge: JudgeId)
        (checkHash: string)
        (artifactHashes: string list)
        (question: string option)
        : string =
        // The artifact half is the SET of read-artifact hashes: de-duplicated and
        // ordinal-sorted so probe order or a duplicate read does not change the key
        // (the F04 policy F03's `reads` deferred — order-independent, culture-invariant).
        let artifactDigests =
            artifactHashes
            |> List.distinct
            |> List.sortWith (fun a b -> String.CompareOrdinal(a, b))
            |> List.map digest
        // The reviewer-prompt half: the hash of the question, or a fixed sentinel for
        // None (a 4-char marker that can never collide with a 64-hex digest).
        let questionDigest =
            match question with
            | Some q -> digest q
            | None -> "none"
        // Fixed order (decision #1): model id, version, check hash, the artifact-hash
        // set, the reviewer-prompt hash. Counts pin section boundaries (prefix-free).
        digest (
            String.concat
                "|"
                ([ "checkrule"
                   digest judge.ModelId
                   digest judge.Version
                   digest checkHash
                   string artifactDigests.Length ]
                 @ artifactDigests
                 @ [ questionDigest ]))

    // ── The bridge to the executable kernel rule (FR-007 … FR-010) ──

    let toRule (bridge: Bridge<'fact>) (rule: CheckRule<'fact>) : Rule<'fact> =
        // Description IS the rendered check, so the published contract cannot drift from
        // what is enforced (FR-007, SC-006). render runs no probe Eval.
        let note = Check.render rule.Check

        // Lift a governance outcome into the adapter's 'fact and attach a ProvenanceStep
        // naming the rule. The fact Id is a placeholder — the kernel's `identify`
        // re-keys every produced fact at evaluation time (F01), so the bridge does not
        // own fact identity. `inputs` are the FactIds the rule consumed this run.
        let embed (inputs: FactId list) (outcome: RuleOutcome) : FactAssertion<'fact> =
            { Id = FactId ""
              Value = bridge.Embed outcome
              Provenance = [ { Rule = rule.Id; Inputs = inputs; Note = note } ] }

        { Id = rule.Id
          Description = note
          Apply =
            fun facts ->
                match rule.Tier with
                // A machine decides: assert the three-valued verdict VERBATIM — never
                // coerced (an Uncertain stays Uncertain) (FR-008, SC-005).
                | Deterministic -> [ embed [] (Decided(rule.Id, Check.eval facts rule.Check)) ]
                // A person decides: escalate (a blocker), regardless of severity, and
                // never assert a decided verdict (FR-010, SC-008).
                | HumanOnly -> [ embed [] (Escalated rule.Id) ]
                // An AI agent decides: key the review (decision #1), then short-circuit
                // on a recorded verdict (cache HIT) or emit exactly one request (MISS).
                | AgentReviewed ->
                    let key =
                        cacheKey
                            bridge.Judge
                            (Check.hash rule.Check)
                            (Check.reads rule.Check |> List.map (bridge.ArtifactHash facts))
                            rule.Question
                    // Find a recorded verdict for THIS key, capturing its fact id so the
                    // consumed input is recorded in provenance. Keyed by `key` (not by
                    // RuleId), so a stale verdict under an old judge no longer matches —
                    // the re-review-on-judge-change policy falls out for free (FR-013).
                    let hit =
                        facts
                        |> List.tryPick (fun f ->
                            match bridge.Project f.Value with
                            | Some(Reviewed r) when r.Key = key -> Some(f.Id, r.Verdict)
                            | _ -> None)
                    match hit with
                    | Some(fid, v) -> [ embed [ fid ] (Decided(rule.Id, v)) ] // cache hit: no request, no agent call
                    | None -> [ embed [] (NeedsReview { Rule = rule.Id; Question = rule.Question; Key = key }) ] }
