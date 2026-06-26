module FS.GG.Governance.RuleIdentity.Tests.RuleIdentityTests

open Expecto
open FsCheck
open FS.GG.Governance.RuleIdentity

// Unit tests for the 068 leaf (T006): each constructor produces its source-prefixed token; the five
// prefixes are DISJOINT; `ruleIdToken` is total and deterministic (same input ⇒ byte-identical token);
// `unattributed` never yields an empty id (data-model §1). All inputs are real literal strings — the
// constructors are pure, so no upstream chain and no mocks (Principle V).

let private token = RuleIdentity.ruleIdToken

[<Tests>]
let tests =
    testList
        "RuleIdentity"
        [ test "gate prefixes with gate:" {
              Expect.equal (token (RuleIdentity.gate "d:c")) "gate:d:c" "gate token"
          }

          test "boundary prefixes with boundary:" {
              Expect.equal
                  (token (RuleIdentity.boundary "unknownGovernedPath"))
                  "boundary:unknownGovernedPath"
                  "boundary token"
          }

          test "surface joins domain and code under surface:" {
              Expect.equal (token (RuleIdentity.surface "d" "code")) "surface:d:code" "surface token"
          }

          test "release prefixes with release:" {
              Expect.equal (token (RuleIdentity.release "k")) "release:k" "release token"
          }

          test "unattributed prefixes with unattributed:" {
              Expect.equal (token (RuleIdentity.unattributed "r")) "unattributed:r" "unattributed token"
          }

          test "the five source prefixes are disjoint" {
              // Same raw payload placed through each constructor yields five distinct tokens — a consumer
              // discriminates the source class by the leading segment alone (FR-008, SC-006).
              let raw = "x"

              let tokens =
                  [ token (RuleIdentity.gate raw)
                    token (RuleIdentity.boundary raw)
                    token (RuleIdentity.surface raw raw)
                    token (RuleIdentity.release raw)
                    token (RuleIdentity.unattributed raw) ]

              let distinct = tokens |> List.distinct
              Expect.equal (List.length distinct) (List.length tokens) "all five tokens are distinct"

              for t in tokens do
                  Expect.isTrue
                      (t.StartsWith "gate:"
                       || t.StartsWith "boundary:"
                       || t.StartsWith "surface:"
                       || t.StartsWith "release:"
                       || t.StartsWith "unattributed:")
                      (sprintf "token %s carries a known source prefix" t)
          }

          test "unattributed never yields an empty id even for an empty reason" {
              // The disclosed marker is honest and non-empty by construction (FR-010): the `unattributed:`
              // prefix is always present, so the token is never the empty string.
              Expect.equal (token (RuleIdentity.unattributed "")) "unattributed:" "non-empty marker"
              Expect.isFalse ((token (RuleIdentity.unattributed "")) = "") "id is never empty"
          }

          testPropertyWithConfig
              { FsCheckConfig.defaultConfig with maxTest = 200 }
              "ruleIdToken is deterministic — same input yields byte-identical token"
          <| fun (s: string) ->
              let a = token (RuleIdentity.gate s)
              let b = token (RuleIdentity.gate s)
              a = b

          testPropertyWithConfig
              { FsCheckConfig.defaultConfig with maxTest = 200 }
              "ruleIdToken inverts each constructor's prefixing (totality)"
          <| fun (s: string) ->
              token (RuleIdentity.gate s) = "gate:" + s
              && token (RuleIdentity.boundary s) = "boundary:" + s
              && token (RuleIdentity.release s) = "release:" + s
              && token (RuleIdentity.unattributed s) = "unattributed:" + s ]
