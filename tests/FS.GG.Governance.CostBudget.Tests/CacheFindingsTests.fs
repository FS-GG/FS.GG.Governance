module FS.GG.Governance.CostBudget.Tests.CacheFindingsTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.CostBudget.Model
open FS.GG.Governance.CostBudget
open FS.GG.Governance.CostBudget.Findings
open FS.GG.Governance.CostBudget.Tests.Support

// US3 (T027): `cacheFindings` over a report + a (GateId -> EvidenceTaint) lookup — a changed freshness
// dimension (whether recomputed or deferred) ⇒ Stale naming each dimension via categoryToken; NoPriorEvidence
// ⇒ NoEvidence; a Synthetic-taint gate ⇒ a distinct SyntheticTaint finding even when its decision is Reuse; a
// clean Reuse + Real ⇒ no finding; every finding is base-Advisory with a message naming the gate + cause and
// no raw path/clock/env (FR-007, SC-004).

let private gStale = gid "a" "stale"
let private gDeferredStale = gid "b" "deferred"
let private gNoEv = gid "c" "noev"
let private gReuseSynth = gid "d" "synth"
let private gReuseClean = gid "e" "clean"

let private report =
    // Inner ⇒ Cheap ceiling: the cheap recompute + noEvidence proceed; the High must-recompute is deferred.
    Budget.decide
        (Budget.budgetFor Strict Verify |> fun b -> { b with Ceiling = Cheap }) // force a Cheap ceiling
        Verify
        [ mustRecompute gStale Cheap [ RuleHashCat ]
          mustRecompute gDeferredStale High [ BaseRevisionCat ]
          noEvidence gNoEv Cheap
          reusable gReuseSynth Cheap
          reusable gReuseClean Cheap ]

let private findings = Findings.cacheFindings report (taintOnly [ gReuseSynth ])

let private kindOf (gate: GateId) =
    findings |> List.filter (fun f -> f.Gate = gate) |> List.map (fun f -> f.Kind)

[<Tests>]
let tests =
    testList
        "CacheFindings"
        [ test "a recomputed changed dimension ⇒ Stale naming each changed F029 category" {
              Expect.equal (kindOf gStale) [ Stale [ RuleHashCat ] ] "ruleHash stale"
              let msg = findings |> List.find (fun f -> f.Gate = gStale) |> fun f -> f.Message
              Expect.stringContains msg "ruleHash" "message names the changed dimension via categoryToken"
              Expect.stringContains msg "a:stale" "message names the gate"
          }

          test "a DEFERRED gate whose underlying cause changed inputs ⇒ Stale (cache-invalidated, even though not run)" {
              Expect.equal (kindOf gDeferredStale) [ Stale [ BaseRevisionCat ] ] "deferred-but-stale"
          }

          test "a NoPriorEvidence recompute ⇒ NoEvidence" { Expect.equal (kindOf gNoEv) [ NoEvidence ] "no-evidence finding" }

          test "a Synthetic-taint gate ⇒ a distinct SyntheticTaint finding EVEN when the decision is Reuse" {
              Expect.equal (kindOf gReuseSynth) [ SyntheticTaint ] "synthetic reused is never silently real"
          }

          test "a clean Reuse + Real taint ⇒ NO finding" { Expect.equal (kindOf gReuseClean) [] "clean reuse is silent" }

          test "every finding is base-Advisory" {
              Expect.isTrue (findings |> List.forall (fun f -> f.BaseSeverity = Advisory)) "all advisory"
              Expect.equal (List.length findings) 4 "exactly four findings (the clean reuse is silent)"
          } ]
