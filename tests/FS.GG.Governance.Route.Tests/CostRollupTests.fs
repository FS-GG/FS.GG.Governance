module FS.GG.Governance.Route.Tests.CostRollupTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Route.Model
open FS.GG.Governance.Route.Tests.Support

// US4 (P2): the route carries a `CostRollup` — the per-tier count of the DISTINCT selected gates'
// declared costs (each shared gate counted once); an empty selection yields the all-zero identity;
// the rollup is additive only and never changes which gates are selected; identical inputs yield an
// identical rollup. A multiset of tiers, NOT a summed scalar (FR-006, research D5, SC-004).

/// Two `build` gates (Cheap + Medium) and one `docs` gate (Cheap).
let private fixtureFacts =
    facts
        "src"
        [ "src/build/**", "build"
          "src/docs/**", "docs" ]
        []
        [ check "build" "tests" None Medium
          check "build" "format" None Cheap
          check "docs" "lint" None Cheap ]
        []

[<Tests>]
let tests =
    testList
        "CostRollup"
        [ test "the rollup counts the DISTINCT selected gates per tier (shared gate once) (AS1, SC-004)" {
              // Two paths both reach `build` (so the two build gates are each shared but counted once)
              // plus one `docs` path.
              let r = selectOf fixtureFacts [ "src/build/A.fs"; "src/build/B.fs"; "src/docs/G.md" ]
              // build:format (Cheap) + build:tests (Medium) + docs:lint (Cheap) = 2 Cheap, 1 Medium.
              Expect.equal r.Cost { Cheap = 2; Medium = 1; High = 0; Exhaustive = 0 } "per-tier multiset over distinct gates"
              Expect.equal r.SelectedGates.Length 3 "exactly three distinct gates selected"
          }

          test "no gates selected → the all-zero CostRollup identity, a valid success (AS2, FR-009)" {
              let r = selectOf fixtureFacts [ "src/loose/x.fs" ]
              Expect.isEmpty r.SelectedGates "no gate selected"
              Expect.equal r.Cost { Cheap = 0; Medium = 0; High = 0; Exhaustive = 0 } "all-zero identity"
          }

          test "identical inputs yield an identical rollup on re-run (AS3)" {
              let paths = [ "src/build/A.fs"; "src/docs/G.md" ]
              Expect.equal (selectOf fixtureFacts paths).Cost (selectOf fixtureFacts paths).Cost "deterministic rollup"
          }

          test "the rollup is additive only — it does not change which gates are selected (AS4)" {
              let r = selectOf fixtureFacts [ "src/build/A.fs"; "src/docs/G.md" ]
              // reading the cost is independent of the selection: selection is the same whether or
              // not the cost is inspected.
              let total = r.Cost.Cheap + r.Cost.Medium + r.Cost.High + r.Cost.Exhaustive
              Expect.equal total r.SelectedGates.Length "the multiset counts exactly the distinct selected gates"
          } ]
