namespace FS.GG.Governance.HumanText

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Route.Model
open FS.GG.Governance.RouteExplain.Model
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.ReleaseReport.Model
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.GateRun.Model
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.EvidenceReuse

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ReportView =

    type ReportNode =
        | Leaf of label: string * detail: string option
        | Group of title: string * children: ReportNode list

    type ReportView =
        { Title: string
          ExitStatus: string
          Sections: ReportNode list }

    // ── token helpers (closed-enum → stable, deterministic text; hidden from the .fsi) ──

    let private pathValue (GovernedPath p) = p
    let private domainValue (DomainId d) = d

    let private costToken (cost: Cost) =
        match cost with
        | Cheap -> "cheap"
        | Medium -> "medium"
        | High -> "high"
        | Exhaustive -> "exhaustive"

    let private verdictToken (verdict: Verdict) =
        match verdict with
        | Pass -> "PASS"
        | Fail -> "FAIL"

    let private exitToken (basis: ExitCodeBasis) =
        match basis with
        | Clean -> "clean"
        | Blocked -> "blocked"

    let private severityToken (s: Severity) =
        match s with
        | Advisory -> "advisory"
        | Blocking -> "blocking"

    let private ruleKindToken (kind: ReleaseRuleKind) =
        match kind with
        | VersionBump -> "version-bump"
        | PackageMetadata -> "package-metadata"
        | TemplatePins -> "template-pins"
        | PublishPlan -> "publish-plan"
        | TrustedPublishing -> "trusted-publishing"
        | Provenance -> "provenance"

    let private factStateToken (state: FactState) =
        match state with
        | Met -> "met"
        | Unmet -> "unmet"
        | Unrecoverable -> "unrecoverable"

    let private dispositionToken (d: GateDisposition) =
        match d with
        | Executed -> "executed"
        | Reused -> "reused"
        | NotExecuted -> "not-executed"

    let private causeToken (cause: RecomputeCause) =
        match cause with
        | NoPriorEvidence -> "no prior evidence"
        | InputsChanged cats -> sprintf "inputs changed (%d)" (List.length cats)

    let private enforcedItemLabel (item: EnforcedItem) =
        match item.Id with
        | GateItem id -> gateIdValue id
        | FindingItem(fid, path) -> sprintf "%s @ %s" (findingIdToken fid) (pathValue path)

    let private enforcedItemDetail (item: EnforcedItem) =
        Some(sprintf "%s — %s" (severityToken item.Decision.EffectiveSeverity) item.Decision.Reason)

    // ── a Group built from a list, with a deterministic "(none)" leaf when empty ──
    let private groupOf (title: string) (children: ReportNode list) =
        match children with
        | [] -> Group(title, [ Leaf("(none)", None) ])
        | cs -> Group(title, cs)

    let private countLeaf (label: string) (n: int) = Leaf(sprintf "%s: %d" label n, None)

    // ── selected-gate / finding / cache leaves shared across views ──

    let private selectingPathsDetail (g: SelectedGate) =
        g.SelectingPaths
        |> List.map (fun sp -> pathValue sp.Path)
        |> String.concat ", "

    let private selectedGateLeaf (g: SelectedGate) =
        Leaf(
            gateIdValue g.Gate.Id,
            Some(sprintf "domain=%s cost=%s; paths: %s" (domainValue g.Gate.Domain) (costToken g.Gate.Cost) (selectingPathsDetail g))
        )

    let private findingLeaf (f: UnknownGovernedPathFinding) =
        Leaf(sprintf "%s @ %s" (findingIdToken f.Id) (pathValue f.Path), Some f.Message)

    let private cacheEntryLeaf (entry: CacheEligibilityEntry) =
        let detail =
            match entry.Verdict with
            | Reusable ref -> sprintf "reusable: %s" (EvidenceReuse.referenceValue ref)
            | MustRecompute cause -> sprintf "recompute: %s" (causeToken cause)

        Leaf(gateIdValue entry.Gate, Some detail)

    let private outcomeLeaf ((id, outcome): GateId * GateOutcome) =
        let passed =
            match outcome.Passed with
            | Some true -> "passed"
            | Some false -> "failed"
            | None -> "no-result"

        Leaf(gateIdValue id, Some(sprintf "%s — %s" (dispositionToken outcome.Disposition) passed))

    let private cacheSection (cache: CacheEligibilityReport option) =
        match cache with
        | None -> []
        | Some(CacheEligibilityReport entries) ->
            [ groupOf "Cache eligibility" (entries |> List.map cacheEntryLeaf) ]

    let private outcomesSection (outcomes: (GateId * GateOutcome) list) =
        match outcomes with
        | [] -> []
        | os -> [ groupOf "Gate execution" (os |> List.map outcomeLeaf) ]

    // ── §3 projections (one per report object; pure, total, deterministic) ──

    let viewOfRouteResult
        (result: RouteResult)
        (cache: CacheEligibilityReport option)
        (outcomes: (GateId * GateOutcome) list)
        : ReportView =
        let cost = result.Cost

        let costLeaf =
            Leaf(
                sprintf
                    "cheap=%d medium=%d high=%d exhaustive=%d"
                    cost.Cheap
                    cost.Medium
                    cost.High
                    cost.Exhaustive,
                None
            )

        { Title = sprintf "route: %d selected gate(s)" (List.length result.SelectedGates)
          ExitStatus = "success"
          Sections =
            [ groupOf "Selected gates" (result.SelectedGates |> List.map selectedGateLeaf)
              Group("Cost", [ costLeaf ])
              groupOf "Findings" (result.Findings.Findings |> List.map findingLeaf) ]
            @ cacheSection cache
            @ outcomesSection outcomes }

    let viewOfRouteExplanation (explanation: RouteExplanation) : ReportView =
        let findingLeaf (hcf: HighCostFinding) =
            let detail =
                match hcf.Alternative with
                | CheaperLocalAlternative g -> sprintf "cheaper local alternative: %s" (gateIdValue g.Id)
                | NoCheaperLocalAlternative -> "no cheaper local alternative"

            Leaf(gateIdValue hcf.Selected.Gate.Id, Some detail)

        { Title = sprintf "explain: %d high-cost finding(s)" (List.length explanation.Findings)
          ExitStatus = "advisory"
          Sections = [ groupOf "High-cost gates" (explanation.Findings |> List.map findingLeaf) ] }

    let private shipView (decision: ShipDecision) : ReportView =
        { Title = sprintf "verdict: %s" (verdictToken decision.Verdict)
          ExitStatus = exitToken decision.ExitCodeBasis
          Sections =
            [ groupOf "Blockers" (decision.Blockers |> List.map (fun i -> Leaf(enforcedItemLabel i, enforcedItemDetail i)))
              groupOf "Warnings" (decision.Warnings |> List.map (fun i -> Leaf(enforcedItemLabel i, enforcedItemDetail i)))
              countLeaf "Passing" (List.length decision.Passing) ] }

    let viewOfShipDecision
        (decision: ShipDecision)
        (cache: CacheEligibilityReport option)
        (outcomes: (GateId * GateOutcome) list)
        : ReportView =
        let baseView = shipView decision

        { baseView with
            Sections = baseView.Sections @ cacheSection cache @ outcomesSection outcomes }

    let viewOfVerifyDecision
        (decision: ShipDecision)
        (cache: CacheEligibilityReport option)
        (outcomes: (GateId * GateOutcome) list)
        : ReportView =
        // verify renders the SAME ShipDecision as ship (VerifyJson.ofVerifyDecision parity).
        viewOfShipDecision decision cache outcomes

    let viewOfReleaseReport (report: ReleaseReport) : ReportView =
        let decision = report.Decision

        let preconditionLeaf (p: PreconditionEvidence) =
            Leaf(ruleKindToken p.Kind, Some(sprintf "%s — %s" (factStateToken p.State) p.Reason))

        let releaseFindingLeaf (f: EnforcedReleaseFinding) =
            Leaf(
                ruleKindToken f.Finding.Kind,
                Some(sprintf "%s — %s" (severityToken f.Decision.EffectiveSeverity) f.Finding.Reason)
            )

        { Title = sprintf "release verdict: %s" (verdictToken decision.Verdict)
          ExitStatus = exitToken report.ReleaseExitCodeBasis
          Sections =
            [ groupOf "Preconditions" (report.Preconditions |> List.map preconditionLeaf)
              groupOf "Blockers" (decision.Blockers |> List.map releaseFindingLeaf)
              groupOf "Warnings" (decision.Warnings |> List.map releaseFindingLeaf)
              countLeaf "Passing" (List.length decision.Passing) ] }

    let viewOfCacheEligibilityReport (report: CacheEligibilityReport) : ReportView =
        let (CacheEligibilityReport entries) = report

        let reusable =
            entries
            |> List.filter (fun e ->
                match e.Verdict with
                | Reusable _ -> true
                | MustRecompute _ -> false)
            |> List.length

        let recompute = List.length entries - reusable

        { Title = sprintf "evidence: %d gate(s)" (List.length entries)
          ExitStatus = sprintf "%d reusable, %d must-recompute" reusable recompute
          Sections = [ groupOf "Cache eligibility" (entries |> List.map cacheEntryLeaf) ] }
