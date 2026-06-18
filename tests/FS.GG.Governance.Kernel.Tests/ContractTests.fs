module FS.GG.Governance.Kernel.Tests.ContractTests

open Expecto
open FS.GG.Governance.Kernel

// ── The drift-proof published contract (F06 · US2) — V35–V36 ──
//
// EVIDENCE-OBLIGATIONS NOTE (Principle IV / V): F06 is a PURE DERIVATION — Principle IV
// (Elmish/MVU) is N/A. All evidence here is REAL: real CheckRule values authored through
// the public CheckRule.rule/blocking constructors over real reified checks. No synthetic
// fixtures, no mocks/stubs, hence no `// SYNTHETIC:` disclosures.

let private spec doc sec : SpecSource = { Document = doc; Section = sec }
let private chk name : Check<string> = Check.probe name [] [] (fun _ -> Met)

let private okRule =
    function
    | Ok r -> r
    | Error e -> failtestf "rule authoring expected Ok, got Error %A" e

let private advisoryRule id s c =
    CheckRule.rule (RuleId id) Deterministic s c |> okRule

let private blockingRule id s c =
    CheckRule.rule (RuleId id) Deterministic s c |> Result.map CheckRule.blocking |> okRule

[<Tests>]
let tests =
    testList
        "Contract"
        [ test "V35 ofRules: one entry per rule (catalog order), Statement = Check.render, drift-proof" {
              let s1 = spec "constitution.md" "V"
              let s2 = spec "wcag" "1.4.3"
              let cA = chk "has-tests"
              let cB = chk "has-contrast"
              let rA = blockingRule "ra" s1 cA
              let rB = advisoryRule "rb" s2 cB

              let contract = Contract.ofRules [ rA; rB ]
              Expect.equal (List.length contract) 2 "one entry per rule"

              let eA = contract.[0]
              let eB = contract.[1]
              Expect.equal eA.Id (RuleId "ra") "entry 0 carries the rule id, in catalog order"
              Expect.equal eA.Severity Blocking "entry 0 carries the (promoted) severity"
              Expect.equal eA.Spec s1 "entry 0 carries the spec source"
              Expect.equal eA.Statement (Check.render cA) "entry 0 Statement = Check.render (the single source)"
              Expect.equal eB.Statement (Check.render cB) "entry 1 Statement = Check.render"

              // (b) mutating a rule's Check changes ITS entry's Statement — tracks the selector, cannot drift
              let rA' = { rA with Check = chk "totally-different" }
              let mutated = Contract.ofRules [ rA'; rB ]
              Expect.notEqual mutated.[0].Statement eA.Statement "changing the check changes the Statement (drift-proof, SC-005)"
              Expect.equal mutated.[0].Statement (Check.render rA'.Check) "Statement is still render of the new check"

              // (c) reordering the catalog leaves each rule's own entry unchanged (per-rule rendering)
              let reordered = Contract.ofRules [ rB; rA ]
              Expect.equal
                  (reordered |> List.find (fun e -> e.Id = RuleId "ra"))
                  eA
                  "rule ra's own entry is independent of catalog order"
          }

          test "V36 ofRules total over the empty catalog; deterministic; contract JSON round-trips" {
              Expect.equal (Contract.ofRules ([]: CheckRule<string> list)) [] "empty catalog ⇒ empty contract (FR-007, SC-006)"
              Expect.equal (Contract.render []) "" "empty contract ⇒ empty string"

              let rules =
                  [ blockingRule "ra" (spec "doc" "1") (chk "a")
                    advisoryRule "rb" (spec "doc" "2") (chk "b") ]

              Expect.equal (Contract.ofRules rules) (Contract.ofRules rules) "ofRules is deterministic"

              let c = Contract.ofRules rules
              Expect.equal (Json.toContract (Json.ofContract c)) c "contract JSON round-trips (FR-007, SC-003)"
              Expect.equal (Json.ofContract []) "[]" "the empty contract ⇒ \"[]\""
          } ]
