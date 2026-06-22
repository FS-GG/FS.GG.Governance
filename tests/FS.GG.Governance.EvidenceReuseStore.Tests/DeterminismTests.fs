module FS.GG.Governance.EvidenceReuseStore.Tests.DeterminismTests

open System
open System.IO
open System.Text.Json
open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.EvidenceReuseStore
open FS.GG.Governance.EvidenceReuseStore.Tests.Support

// US1 (SC-002, FR-003): the same `ReuseStore` value yields byte-identical output on every run, with a fixed
// field/entry order and optional fields OMITTED on `None`.

/// The field names of an object element in emitted order.
let private fieldOrder (el: JsonElement) : string list =
    [ for p in el.EnumerateObject() -> p.Name ]

let private firstEntry (text: string) : JsonElement =
    use doc = JsonDocument.Parse text
    // Clone so the element stays valid after the document is disposed.
    ((doc.RootElement.GetProperty "recorded").EnumerateArray() |> Seq.head).Clone()

[<Tests>]
let tests =
    testList
        "Determinism"
        [ testPropertyWithConfig fscheckConfig "byte-for-byte determinism: serialise store = serialise store" (fun (store: ReuseStore) ->
              EvidenceReuseStore.serialise store = EvidenceReuseStore.serialise store)

          test "purity: identical text across working directory + filesystem state changes (no I/O)" {
              let store = storeOf [ inputs "fmt", syntheticRef "fmt"; inputs "lint", syntheticRef "lint" ]
              let before = EvidenceReuseStore.serialise store

              // Change the working directory and unrelated filesystem state between calls.
              let original = Directory.GetCurrentDirectory()

              try
                  Directory.SetCurrentDirectory(Path.GetTempPath())
                  let scratch = Path.GetTempFileName()
                  File.WriteAllText(scratch, "unrelated")
                  let after = EvidenceReuseStore.serialise store
                  File.Delete scratch
                  Expect.equal after before "serialise reads no clock/filesystem/cwd — identical bytes"
              finally
                  Directory.SetCurrentDirectory original
          }

          test "stable top-level key order: schemaVersion then recorded" {
              let text = EvidenceReuseStore.serialise (storeOf [ inputs "fmt", syntheticRef "fmt" ])
              use doc = JsonDocument.Parse text
              Expect.equal (fieldOrder doc.RootElement) [ "schemaVersion"; "recorded" ] "top-level order fixed"
          }

          test "stable per-entry field order with both optionals present (D7)" {
              // A fully-present entry: command + commandVersion are Some.
              let text = EvidenceReuseStore.serialise (storeOf [ inputs "full", syntheticRef "full" ])

              Expect.equal
                  (fieldOrder (firstEntry text))
                  [ "check"; "domain"; "command"; "environment"; "ruleHash"; "coveredArtifacts"; "commandVersion"; "generatorVersion"; "base"; "head"; "evidence" ]
                  "entry field order is the fixed documented order"
          }

          test "optional fields OMITTED on None (D4) — not rendered as null" {
              let i = { inputs "cmdless" with Command = None; CommandVersion = None }
              let text = EvidenceReuseStore.serialise (ReuseStore [ { Inputs = i; Evidence = syntheticRef "cmdless" } ])
              let names = fieldOrder (firstEntry text)
              Expect.isFalse (List.contains "command" names) "command omitted when None"
              Expect.isFalse (List.contains "commandVersion" names) "commandVersion omitted when None"
              Expect.isFalse (text.Contains "null") "no null literal emitted"

              // The omit choice is byte-stable across re-serialisation.
              Expect.equal (EvidenceReuseStore.serialise (ReuseStore [ { Inputs = i; Evidence = syntheticRef "cmdless" } ])) text "byte-stable across omit"
          }

          test "Some optionals render as JSON strings" {
              let text = EvidenceReuseStore.serialise (storeOf [ inputs "full", syntheticRef "full" ])
              let entry = firstEntry text
              Expect.equal (entry.GetProperty("command").GetString()) "dotnet" "command string"
              Expect.equal (entry.GetProperty("commandVersion").GetString()) "8.0" "commandVersion string"
          }

          test "entries emitted in the store's verbatim newest-first order, coveredArtifacts verbatim (D5/D7)" {
              // record cons's newest at head: lint recorded last ⇒ lint first.
              let store = storeOf [ inputs "fmt", syntheticRef "fmt"; inputs "lint", syntheticRef "lint" ]
              let text = EvidenceReuseStore.serialise store
              use doc = JsonDocument.Parse text

              let checks =
                  [ for e in (doc.RootElement.GetProperty "recorded").EnumerateArray() -> e.GetProperty("check").GetString() ]

              Expect.equal checks [ "lint"; "fmt" ] "entries in verbatim newest-first order"

              let arts =
                  [ for a in ((doc.RootElement.GetProperty "recorded").EnumerateArray() |> Seq.head).GetProperty("coveredArtifacts").EnumerateArray() -> a.GetString() ]

              Expect.equal arts [ "h2"; "h1"; "h1" ] "coveredArtifacts verbatim, never re-sorted/de-duped"
          } ]
