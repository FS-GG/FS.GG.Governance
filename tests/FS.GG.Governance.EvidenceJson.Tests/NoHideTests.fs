module FS.GG.Governance.EvidenceJson.Tests.NoHideTests

open Expecto
open FS.GG.Governance.Kernel
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.FreshnessResolution.Model
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.EvidenceJson
open FS.GG.Governance.EvidenceJson.Tests.Support

// US2 — across every non-effective cause plus a tainted node, the document is SELF-DESCRIBING: a reader can
// determine why any node is not effective from the document alone (INV-8, SC-006). `Unknown` is the only
// causeless freshness and is never a guessed `fresh` (FR-003).

[<Tests>]
let tests =
    testList
        "NoHide"
        [ test "each non-effective node names why it is not effective, from the document alone" {
              let nodes =
                  [ mkNode "tainted" Real AutoSynthetic NodeFreshness.Fresh "speckit" // effective <> declared
                    mkNode "stale" Real Real (NodeFreshness.Stale(InputsChanged [ RuleHashCat ])) "speckit"
                    mkNode "unresolved" Real Real (NodeFreshness.Unresolved [ MissingHeadRevision ]) "speckit"
                    mkNode "skipped" Skipped Skipped NodeFreshness.Unknown "speckit"
                    mkNode "unknown" Pending Pending NodeFreshness.Unknown "speckit" ]

              let root = parse (wellFormed nodes [] [])

              let byId id =
                  root.GetProperty("nodes").EnumerateArray()
                  |> Seq.find (fun n -> strProp "id" n = id)

              // tainted: effective differs from declared.
              let t = byId "tainted"
              Expect.notEqual (strProp "declared" t) (strProp "effective" t) "taint visible as the delta"

              // stale: a named cause.
              let s = byId "stale"
              Expect.equal (strProp "kind" (s.GetProperty("freshness"))) "stale" "stale named"
              Expect.equal (strProp "kind" (s.GetProperty("freshness").GetProperty("cause"))) "inputsChanged" "cause named"

              // unresolved: a non-empty missing list.
              let u = byId "unresolved"
              Expect.equal (strProp "kind" (u.GetProperty("freshness"))) "unresolved" "unresolved named"
              Expect.isGreaterThan (u.GetProperty("freshness").GetProperty("missing").GetArrayLength()) 0 "missing facts named"

              // skipped: a distinct declared token.
              Expect.equal (strProp "declared" (byId "skipped")) "Skipped" "skipped self-evident"

              // unknown: explicit honest null-equivalent, never a guessed fresh.
              Expect.equal (strProp "kind" ((byId "unknown").GetProperty("freshness"))) "unknown" "unknown, not a guessed fresh"
          } ]
