module FS.GG.Governance.JsonText.Tests.JsonTextTests

open System.Text.Json
open Expecto
open FS.GG.Governance.JsonText

// Semantic tests for the 073 canonical deterministic-emit leaf, exercising the PUBLIC surface
// `JsonText.writeToString` over REAL emit callbacks (Principle V — real values, no mocks). The helper
// is the byte-identical body the *Json projections used to hand-copy; these tests pin the contract the
// whole feature rests on: compact (non-indented), deterministic, verbatim-rounding emit.

[<Tests>]
let tests =
    testList
        "JsonText.writeToString"
        [ test "emits compact, non-indented JSON for an object callback" {
              let actual =
                  JsonText.writeToString (fun w ->
                      w.WriteStartObject()
                      w.WriteString("a", "1")
                      w.WriteNumber("b", 2)
                      w.WriteEndObject())

              Expect.equal actual """{"a":"1","b":2}""" "must emit compact JSON with no indentation"
          }

          test "is deterministic — identical callbacks yield byte-identical strings" {
              let emit (w: Utf8JsonWriter) =
                  w.WriteStartArray()
                  w.WriteStringValue "x"
                  w.WriteStringValue "y"
                  w.WriteEndArray()

              Expect.equal (JsonText.writeToString emit) (JsonText.writeToString emit) "repeated emit must match"
              Expect.equal (JsonText.writeToString emit) """["x","y"]""" "compact array form"
          }

          test "rounds a bare string value verbatim" {
              let actual = JsonText.writeToString (fun w -> w.WriteStringValue "real")
              Expect.equal actual "\"real\"" "bare string value, compact"
          } ]
