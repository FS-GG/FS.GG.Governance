module FS.GG.Governance.SensedMetadata.Tests.MarkingTests

open Expecto
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.SensedMetadata
open FS.GG.Governance.SensedMetadata.Model
open FS.GG.Governance.SensedMetadata.Tests.Support

// US1 — `markDuration` / `markTimestamp` turn an already-measured duration / wall-clock timestamp into a
// typed `SensedMetadatum` carrying its label and value, sensed BY CONSTRUCTION; `kindOf` / `kindToken` report
// the closed kind. Total over the zero-duration, empty-label, and same-label/different-kind edges (SC-001).

[<Tests>]
let tests =
    testList
        "Marking"
        [ // (a) carriage (L-M1) — label and value read back verbatim.
          test "markDuration carries label + DurationValue verbatim" {
              let l = label "elapsed"
              let d = duration 1_830_000_000L
              let m = SensedMetadata.markDuration l d
              Expect.equal m { Label = l; Value = DurationValue d } "markDuration L d = { Label = L; Value = DurationValue d }"
              Expect.equal m.Label l "label read back verbatim"
              Expect.equal m.Value (DurationValue d) "value read back verbatim"
          }

          test "markTimestamp carries label + TimestampValue verbatim" {
              let l = label "at"
              let t = timestamp "2026-06-21T12:00:00Z"
              let m = SensedMetadata.markTimestamp l t
              Expect.equal m { Label = l; Value = TimestampValue t } "markTimestamp L t = { Label = L; Value = TimestampValue t }"
              Expect.equal m.Label l "label read back verbatim"
              Expect.equal m.Value (TimestampValue t) "value read back verbatim"
          }

          // (b) kind (L-K1 / L-K2).
          test "kindOf agrees with the constructor used; kindToken is the injective two-case map" {
              Expect.equal (SensedMetadata.kindOf (markDur "elapsed" 5L)) DurationKind "markDuration ⇒ DurationKind"
              Expect.equal (SensedMetadata.kindOf (markTs "at" "T")) TimestampKind "markTimestamp ⇒ TimestampKind"
              Expect.equal (SensedMetadata.kindToken DurationKind) "duration" "DurationKind ⇒ \"duration\""
              Expect.equal (SensedMetadata.kindToken TimestampKind) "timestamp" "TimestampKind ⇒ \"timestamp\""
              Expect.notEqual (SensedMetadata.kindToken DurationKind) (SensedMetadata.kindToken TimestampKind) "the two tokens are distinct"
          }

          // (c) sensed by construction (L-M2) — the ONLY inhabitants of `.Value` are the two sensed cases;
          // there is no reproducible variant (FR-001). Exhaustive match proves the closed shape.
          test "every marked value's .Value is a SensedValue — no reproducible variant exists" {
              let assertSensed (m: SensedMetadatum) =
                  match m.Value with
                  | TimestampValue _ -> ()
                  | DurationValue _ -> ()
              assertSensed (markDur "x" 0L)
              assertSensed (markTs "x" "")
          }

          // (d) totality edge cases — each produces an ordinary complete metadatum; marking never throws.
          test "zero-length duration, empty label, same-label/different-kind, and large/negative magnitudes are ordinary values" {
              let zero = markDur "elapsed" 0L
              Expect.equal zero.Value (DurationValue(duration 0L)) "zero-length duration is an ordinary value"

              let emptyLabel = markDur "" 5L
              Expect.equal emptyLabel.Label (label "") "empty label is a literal value"

              let sameLabelTs = markTs "t" "2026-06-21T12:00:00Z"
              let sameLabelDur = markDur "t" 5L
              Expect.equal sameLabelTs.Label sameLabelDur.Label "two metadata may share a label"
              Expect.notEqual (SensedMetadata.kindOf sameLabelTs) (SensedMetadata.kindOf sameLabelDur) "…while differing in kind"

              let big = markDur "max" System.Int64.MaxValue
              let neg = markDur "neg" -1L
              Expect.equal big.Value (DurationValue(duration System.Int64.MaxValue)) "large magnitude is an ordinary value"
              Expect.equal neg.Value (DurationValue(duration -1L)) "negative magnitude is an ordinary value"
          }

          // (e) FsCheck totality — marking always returns and round-trips label + value, and kindOf always
          // agrees with the constructor used.
          testPropertyWithConfig fscheckConfig "markDuration round-trips label + duration; kindOf = DurationKind" <| fun (l: SensedLabel) (d: SensedDuration) ->
              let m = SensedMetadata.markDuration l d
              m.Label = l && m.Value = DurationValue d && SensedMetadata.kindOf m = DurationKind

          testPropertyWithConfig fscheckConfig "markTimestamp round-trips label + timestamp; kindOf = TimestampKind" <| fun (l: SensedLabel) (t: SensedTimestamp) ->
              let m = SensedMetadata.markTimestamp l t
              m.Label = l && m.Value = TimestampValue t && SensedMetadata.kindOf m = TimestampKind ]
