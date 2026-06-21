module FS.GG.Governance.SensedMetadata.Tests.RenderingTests

open Expecto
open FS.GG.Governance.SensedMetadata
open FS.GG.Governance.SensedMetadata.Model
open FS.GG.Governance.SensedMetadata.Tests.Support

// US2 — `render` produces the byte-stable, unambiguously-flagged rendering of one metadatum (the reserved
// `!sensed!=` marker, the kind token, the label, and the value, each length-prefixed in the F029/F032/F033
// injective discipline) so it is distinguishable from any reproducible field and unspoofable by its data;
// `renderSection` groups a list into one order-preserving `!sensed-section!`; `renderingValue` unwraps.

let private rv (m: SensedMetadatum) = SensedMetadata.renderingValue (SensedMetadata.render m)

// The lowercase-letter reproducible field tags F029/F032/F033 use before `=` — a sensed rendering must never
// look like one of these.
let private reproducibleTags =
    [ "src"; "base"; "head"; "rule"; "gen"; "art"; "cmds"; "env"; "bld"; "exe"; "args"; "cwd"; "to"; "exit"; "out"; "err"; "cap" ]

[<Tests>]
let tests =
    testList
        "Rendering"
        [ // (a) marker present & distinguishable (L-R1).
          test "render starts with !sensed!= — a form no reproducible field tag produces" {
              Expect.stringStarts (rv workedTimestamp) "!sensed!=" "timestamp rendering carries the marker"
              Expect.stringStarts (rv workedDuration) "!sensed!=" "duration rendering carries the marker"
              for tag in reproducibleTags do
                  Expect.isFalse ((rv workedTimestamp).StartsWith(tag + "=")) (sprintf "sensed rendering must not begin with the reproducible tag %s=" tag)
          }

          // (b) content present & verbatim (L-R2 / L-R4).
          test "render contains the kind token, length-prefixed label, and length-prefixed verbatim value" {
              let ts = rv workedTimestamp
              Expect.stringContains ts "timestamp" "kind token present"
              Expect.stringContains ts "2:at" "length-prefixed label present"
              Expect.stringContains ts "20:2026-06-21T12:00:00Z" "length-prefixed verbatim timestamp present"

              let dur = rv workedDuration
              Expect.stringContains dur "duration" "kind token present"
              Expect.stringContains dur "7:elapsed" "length-prefixed label present"
              Expect.stringContains dur "10:1830000000" "duration value rendered as decimal int64 nanoseconds"

              // Verbatim value: zero and negative durations render as their decimal, never rounded/re-scaled.
              Expect.stringContains (rv (markDur "z" 0L)) "1:0" "zero duration renders as decimal 0"
              Expect.stringContains (rv (markDur "n" -1L)) "2:-1" "negative duration renders verbatim"
          }

          // (c) byte-exact worked examples pinned to contracts/sensed-metadata-format.md.
          test "byte-exact single-metadatum worked examples" {
              Expect.equal (rv workedTimestamp) "!sensed!=timestamp;2:at;20:2026-06-21T12:00:00Z" "timestamp worked example"
              Expect.equal (rv workedDuration) "!sensed!=duration;7:elapsed;10:1830000000" "duration worked example"
              Expect.equal (rv (markDur "" 0L)) "!sensed!=duration;0:;1:0" "empty-label zero-duration: distinct 0: form"
              Expect.equal (rv (markTs "!sensed!" "2026-06-21T12:00:00Z")) "!sensed!=timestamp;8:!sensed!;20:2026-06-21T12:00:00Z" "label text !sensed! neutralized by the length prefix"
          }

          // (d) unspoofable / injective (L-R3): render m = render m' IFF same kind, label, value — even when
          // the label/value text contains the marker characters.
          test "no marker-containing label/value can make two different metadata render equal or masquerade" {
              // A label that is literally the marker is read as 8 label bytes, not as a marker.
              let spoofLabel = markTs "!sensed!" "x"
              let realMarkerShape = markTs "at" "2026-06-21T12:00:00Z"
              Expect.notEqual (rv spoofLabel) (rv realMarkerShape) "a marker-shaped label cannot become another metadatum"

              // A value whose text contains ';'/':'/'=' cannot bleed across a field boundary.
              let trickyValue = markTs "k" "a;b:c=d"
              Expect.equal (rv trickyValue) "!sensed!=timestamp;1:k;7:a;b:c=d" "tricky value read by length, not by separators"

              // Empty label renders to the distinct 0: form (never colliding with absence).
              Expect.stringContains (rv (markTs "" "x")) ";0:;" "empty label ⇒ distinct 0: form"
          }

          testPropertyWithConfig fscheckConfig "render is injective: equal rendering iff equal kind+label+value" <| fun (m: SensedMetadatum) (m': SensedMetadatum) ->
              (SensedMetadata.render m = SensedMetadata.render m') = (m = m')

          // (e) section (L-S1): one order-preserving !sensed-section!; empty list ⇒ !sensed-section!=0;.
          test "renderSection is one order-preserving section; empty list ⇒ !sensed-section!=0;" {
              Expect.equal (SensedMetadata.renderingValue (SensedMetadata.renderSection [])) "!sensed-section!=0;" "empty list is an ordinary value"

              // The two-element worked-example block, byte-for-byte (timestamp then duration).
              let block = SensedMetadata.renderingValue (SensedMetadata.renderSection [ workedTimestamp; workedDuration ])
              Expect.equal
                  block
                  "!sensed-section!=2;47:!sensed!=timestamp;2:at;20:2026-06-21T12:00:00Z;41:!sensed!=duration;7:elapsed;10:1830000000"
                  "two-element worked-example section block"

              // Order is significant (not sorted): reversing differs.
              let reversed = SensedMetadata.renderingValue (SensedMetadata.renderSection [ workedDuration; workedTimestamp ])
              Expect.notEqual block reversed "section order is significant — not sorted"

              // Duplicates are real repeats (not deduped): count and body reflect both.
              let dup = SensedMetadata.renderingValue (SensedMetadata.renderSection [ workedTimestamp; workedTimestamp ])
              Expect.stringStarts dup "!sensed-section!=2;" "a repeated metadatum is a real repeat, count = 2"
          }

          // (f) unwrap (L-U1).
          test "renderingValue unwraps a SensedRendering" {
              Expect.equal (SensedMetadata.renderingValue (SensedRendering "abc")) "abc" "renderingValue (SensedRendering s) = s"
          } ]
