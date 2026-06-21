module FS.GG.Governance.EnforcementFixtures.Tests.TruthTableTests

open Expecto
open FS.GG.Governance.EnforcementFixtures.Tests.Generator
open FS.GG.Governance.EnforcementFixtures.Tests.Support

// Completeness, drift, determinism, and visible-property guards for the golden truth table (SC-001,
// SC-002, SC-003). Every assertion reads the GENERATED text — the same bytes the drift guard commits —
// so a regression in the cores or the rendering trips a test, never passes quietly.

/// Parse a rendered pipe table into its data rows (header + `|---|` rule dropped), each a trimmed
/// cell list.
let private dataRows (table: string) : string list list =
    table.Split('\n')
    |> Array.toList
    |> List.skip 2
    |> List.filter (fun line -> line.StartsWith "|")
    |> List.map (fun line ->
        let inner = line.Trim().Trim('|')

        inner.Split('|')
        |> Array.map (fun c -> c.Trim())
        |> Array.toList)

[<Tests>]
let tests =
    testList
        "F028 truth table"
        [ test "completeness — exactly 240 rows and 240 distinct (base,maturity,mode,profile) keys (SC-001)" {
              let rows = dataRows (renderPrimaryTable ())
              Expect.equal (List.length rows) 240 "primary table must have exactly 2×5×6×4 = 240 data rows"

              let keys =
                  rows |> List.map (fun r -> r[0], r[1], r[2], r[3]) |> Set.ofList

              Expect.equal (Set.count keys) 240 "every (base,maturity,mode,profile) combination must appear exactly once — no missing, no duplicate"
          }

          test "drift guard — regenerated truth-table.md is byte-equal to the committed golden (SC-002/SC-003)" {
              blessOrCompare "truth-table.md" (renderTruthTable ())
          }

          test "determinism — regenerating twice yields identical bytes (SC-002, FR-004)" {
              Expect.equal (renderTruthTable ()) (renderTruthTable ()) "generation must be a pure function of the cores — no clock/host/order influence"
          }

          test "visible properties — observe/warn and base-advisory never escalate; saturated combos present (Edge)" {
              let rows = dataRows (renderPrimaryTable ())

              let effectiveOf r = List.item 4 r

              // FR-007: observe/warn always derive advisory regardless of mode/profile.
              for r in rows do
                  if r[1] = "observe" || r[1] = "warn" then
                      Expect.equal (effectiveOf r) "advisory" (sprintf "%A: observe/warn must derive advisory" r)

              // Edge: a base-advisory finding is never escalated by this core.
              for r in rows do
                  if r[0] = "advisory" then
                      Expect.equal (effectiveOf r) "advisory" (sprintf "%A: base-advisory must stay advisory" r)

              // Edge: saturated/unreachable combinations are PRESENT, never omitted.
              let saturated =
                  rows
                  |> List.exists (fun r -> r[0] = "blocking" && r[1] = "block-on-release" && r[2] = "release" && r[3] = "release")

              Expect.isTrue saturated "the saturated blocking/block-on-release/release/release row must be present"
          } ]
