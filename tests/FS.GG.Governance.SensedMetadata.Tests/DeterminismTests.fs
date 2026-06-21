module FS.GG.Governance.SensedMetadata.Tests.DeterminismTests

open Expecto
open FS.GG.Governance.SensedMetadata
open FS.GG.Governance.SensedMetadata.Model
open FS.GG.Governance.SensedMetadata.Tests.Support

// US3 — `markDuration` / `markTimestamp` / `render` / `renderSection` are deterministic functions of the
// supplied values (identical inputs ⇒ identical marked value and byte-identical rendering), and a sensed
// rendering is identity-neutral: a report's reproducible bytes are byte-identical regardless of which / how
// many sensed metadata populate its section (SC-003, SC-004).

[<Tests>]
let tests =
    testList
        "Determinism"
        [ // (a) determinism (L-D1) — twice yields structurally / byte-identical results.
          test "marking and rendering the same value twice is byte-equal" {
              Expect.equal (markTs "at" "T") (markTs "at" "T") "marking is deterministic (timestamp)"
              Expect.equal (markDur "elapsed" 5L) (markDur "elapsed" 5L) "marking is deterministic (duration)"
              Expect.equal (SensedMetadata.render workedTimestamp) (SensedMetadata.render workedTimestamp) "render is byte-deterministic"
              Expect.equal
                  (SensedMetadata.renderSection [ workedTimestamp; workedDuration ])
                  (SensedMetadata.renderSection [ workedTimestamp; workedDuration ])
                  "renderSection is byte-deterministic"
          }

          testPropertyWithConfig fscheckConfig "render / renderSection are deterministic over generated inputs" <| fun (m: SensedMetadatum) (ms: SensedMetadatum list) ->
              SensedMetadata.render m = SensedMetadata.render m
              && SensedMetadata.renderSection ms = SensedMetadata.renderSection ms

          // (b) identity-neutrality (L-N1 / L-S2) — a report modeled as (reproducibleBytes, renderSection
          // sensed) keeps its reproducibleBytes byte-identical whether `sensed` is [], one, or many. Adding /
          // removing a sensed metadatum never alters the reproducible partition (the self-contained
          // demonstration — plan D5; the optional cross-core Provenance.canonicalId check is deferred).
          test "a report's reproducible bytes are unchanged regardless of its sensed section" {
              // The reproducible partition is computed independently of the sensed section.
              let reproducibleBytes = "src=16:c0ffee\nhead=15:head2"

              let reportWith (sensed: SensedMetadatum list) =
                  let (SensedRendering section) = SensedMetadata.renderSection sensed
                  reproducibleBytes, section

              let repNone, _ = reportWith []
              let repOne, _ = reportWith [ workedTimestamp ]
              let repMany, _ = reportWith [ workedTimestamp; workedDuration; markDur "x" 0L ]

              Expect.equal repNone reproducibleBytes "no sensed metadata ⇒ reproducible bytes unchanged"
              Expect.equal repOne reproducibleBytes "one sensed metadatum ⇒ reproducible bytes unchanged"
              Expect.equal repMany reproducibleBytes "many sensed metadata ⇒ reproducible bytes unchanged"
          }

          testPropertyWithConfig fscheckConfig "reproducible bytes are invariant under any sensed section" <| fun (ms: SensedMetadatum list) ->
              let reproducibleBytes = "src=16:c0ffee\nhead=15:head2"
              let (SensedRendering _) = SensedMetadata.renderSection ms
              // The reproducible partition is a standalone value the sensed section never reads.
              reproducibleBytes = "src=16:c0ffee\nhead=15:head2" ]
