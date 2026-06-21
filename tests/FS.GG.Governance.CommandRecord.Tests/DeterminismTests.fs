module FS.GG.Governance.CommandRecord.Tests.DeterminismTests

open Expecto
open FS.GG.Governance.CommandRecord
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.CommandRecord.Tests.Support

// US3 — `build` and `canonicalId` are deterministic; env-delta entries are compared as a SET (order/dup
// invariant) while argument order is significant (SC-005, FR-006/FR-007, D6).

[<Tests>]
let tests =
    testList
        "Determinism"
        [ // (a) build then canonicalId twice ⇒ structurally / byte equal.
          test "build and canonicalId are deterministic for a representative record" {
              let r1 = rebuild baseRecord.Reproducible baseDuration
              let r2 = rebuild baseRecord.Reproducible baseDuration
              Expect.equal r1 r2 "identical facts ⇒ structurally identical record"
              Expect.equal (CommandRecord.canonicalId r1) (CommandRecord.canonicalId r2) "identical facts ⇒ byte-identical identity"
          }

          testPropertyWithConfig fscheckConfig "build + canonicalId are deterministic over generated facts" <| fun (facts: ReproducibleFacts) (duration: SensedDuration) ->
              let r1 = rebuild facts duration
              let r2 = rebuild facts duration
              r1 = r2 && CommandRecord.canonicalId r1 = CommandRecord.canonicalId r2

          // (b) Env-delta order/dup invariance.
          test "reordering and duplicating env-delta entries leaves canonicalId unchanged" {
              let env =
                  { Added = [ { Name = EnvVarName "A"; Value = EnvVarValue "1" }; { Name = EnvVarName "B"; Value = EnvVarValue "2" } ]
                    Changed = [ { Name = EnvVarName "C"; Old = EnvVarValue "o1"; New = EnvVarValue "n1" }; { Name = EnvVarName "D"; Old = EnvVarValue "o2"; New = EnvVarValue "n2" } ]
                    Removed = [ { Name = EnvVarName "E"; Old = EnvVarValue "x" }; { Name = EnvVarName "F"; Old = EnvVarValue "y" } ] }

              let facts = { baseRecord.Reproducible with Environment = env }
              let permuted = { facts with Environment = permuteAndDuplicateEnv env }

              Expect.equal
                  (CommandRecord.canonicalId (rebuild permuted baseDuration))
                  (CommandRecord.canonicalId (rebuild facts baseDuration))
                  "env-delta class compared as a set ⇒ order/dup invariant"
          }

          testPropertyWithConfig fscheckConfig "env-delta order/dup permutation is identity-invariant" <| fun (facts: ReproducibleFacts) ->
              let permuted = { facts with Environment = permuteAndDuplicateEnv facts.Environment }
              CommandRecord.canonicalId (rebuild permuted baseDuration)
              = CommandRecord.canonicalId (rebuild facts baseDuration)

          // (c) Argument-order significance (the contrast).
          test "reordering arguments DOES change canonicalId" {
              let forward = { baseRecord.Reproducible with Arguments = [ Argument "-a"; Argument "-b" ] }
              let reversed = { baseRecord.Reproducible with Arguments = [ Argument "-b"; Argument "-a" ] }
              Expect.notEqual
                  (CommandRecord.canonicalId (rebuild reversed baseDuration))
                  (CommandRecord.canonicalId (rebuild forward baseDuration))
                  "argument order is significant"
          } ]
