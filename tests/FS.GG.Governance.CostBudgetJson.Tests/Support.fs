module FS.GG.Governance.CostBudgetJson.Tests.Support

open System
open System.IO
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.CostBudget.Model
open FS.GG.Governance.CostBudget.Findings

// Shared builders for the F25 cost-budget.json projection tests. Every value is a real, literally-
// constructible typed value (Principle V); the projection is pure so no upstream chain is needed.

let gid (domain: string) (check: string) : GateId = GateId(domain + ":" + check)

/// A distinctive CacheKey so a test can assert it is NEVER emitted as a blocking signal.
let secretKey = CacheKey "SECRET-CACHE-KEY-DO-NOT-EMIT"

let entry (gate: GateId) (cost: Cost) (review: AgentReviewMark) (decision: CacheDecision) : CacheDecisionEntry =
    { Gate = gate; Cost = cost; Review = review; Decision = decision }

/// A mixed report covering every decision kind, already in GateId-ordinal order.
let mixedReport =
    CacheDecisionReport
        [ entry (gid "a" "reuse") Cheap Deterministic (Reuse(EvidenceRef "ev-1"))
          entry (gid "b" "recompute") Medium (AgentReviewed secretKey) (Recompute(InputsChanged [ RuleHashCat; BaseRevisionCat ]))
          entry (gid "c" "noev") Cheap Deterministic (Recompute NoPriorEvidence)
          entry (gid "d" "skip") Exhaustive Deterministic (OverBudget { Gate = gid "d" "skip"; Cost = Exhaustive; Ceiling = Cheap; Class = Skipped; Cause = InputsChanged [ HeadRevisionCat ] })
          entry (gid "e" "defer") High Deterministic (OverBudget { Gate = gid "e" "defer"; Cost = High; Ceiling = Medium; Class = Deferred; Cause = NoPriorEvidence }) ]

let advisory (gate: GateId) (kind: CostFindingKind) (msg: string) : CostFinding =
    { Gate = gate; Kind = kind; BaseSeverity = Advisory; Message = msg }

/// A mixed findings list covering each finding kind (stale carries categories).
let mixedFindings =
    [ advisory (gid "a" "reuse") SyntheticTaint "gate a:reuse: evidence is synthetic, not from a real run"
      advisory (gid "b" "recompute") (Stale [ RuleHashCat; BaseRevisionCat ]) "gate b:recompute: evidence stale"
      advisory (gid "c" "noev") NoEvidence "gate c:noev: no prior evidence to reuse" ]
// 074: findRepoRoot consolidated into the shared RepositoryHelpers (sln||slnx superset).
let repoRoot = FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot
