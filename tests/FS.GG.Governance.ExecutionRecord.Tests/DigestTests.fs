module FS.GG.Governance.ExecutionRecord.Tests.DigestTests

open Expecto
open FsCheck
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.ExecutionRecord
open FS.GG.Governance.ExecutionRecord.Tests.Support

// US2 (P1): `digestOf` is content-addressed, total, and byte-stable — the first and only place in the codebase
// that hashes output bytes. All assertions are over REAL byte buffers (Support.fs); no I/O (SC-008).

let private digestOf (bytes: byte[]) = ExecutionRecord.digestOf bytes
let private raw (OutputDigest s) = s

[<Tests>]
let tests =
    testList
        "Digest"
        [ // (1) content agreement — SC-002 / FR-002 / US2 acceptance 1
          test "equal content yields the byte-identical digest (worked example)" {
              Expect.equal (digestOf bytesA) (digestOf bytesEqual) "equal bytes => equal digest"
          }

          testPropertyWithConfig fscheckConfig "equal content yields equal digest (property over arbitrary bytes)"
          <| fun (bytes: byte[]) ->
              // round-trip through a fresh copy: content alone, never array identity (FR-009)
              digestOf bytes = digestOf (Array.copy bytes)

          // (2) content sensitivity — SC-003 / FR-002 / US2 acceptance 2
          test "a single byte changed yields a different digest" {
              Expect.notEqual (digestOf bytesA) (digestOf bytesChanged) "changed byte => different digest"
          }
          test "a single byte added yields a different digest" {
              Expect.notEqual (digestOf bytesA) (digestOf bytesAdded) "added byte => different digest"
          }
          test "a single byte removed yields a different digest" {
              Expect.notEqual (digestOf bytesA) (digestOf bytesRemoved) "removed byte => different digest"
          }
          test "reordered bytes yield a different digest" {
              Expect.notEqual (digestOf bytesOrdered) (digestOf bytesReordered) "reorder => different digest"
          }

          testPropertyWithConfig fscheckConfig "non-equal buffers yield non-equal digests"
          <| fun (a: byte[]) (b: byte[]) -> (a <> b) ==> lazy (digestOf a <> digestOf b)

          // (3) empty-input totality + distinctness — FR-003 / FR-008 / Edge "Empty captured output"
          test "the empty digest is defined, equals the fixed empty-SHA-256 hash, and never throws" {
              Expect.equal (raw (digestOf bytesEmpty)) emptySha256Hex "empty digest is the fixed empty-SHA-256 hex"
          }
          test "the empty digest is distinct from every non-empty digest" {
              for label, b in
                  [ "A", bytesA; "binary", bytesBinary; "large", bytesLarge; "ordered", bytesOrdered ] do
                  Expect.notEqual (digestOf bytesEmpty) (digestOf b) (sprintf "empty distinct from %s" label)
          }

          // (4) binary + large totality — FR-008 / Edge "Binary"/"Large"
          test "a binary (non-UTF-8) buffer digests to a fixed-form lowercase-hex digest, never throwing" {
              let s = raw (digestOf bytesBinary)
              Expect.equal s.Length 64 "SHA-256 hex is 64 chars"
              Expect.isTrue (s |> Seq.forall (fun c -> System.Char.IsDigit c || (c >= 'a' && c <= 'f'))) "lowercase hex"
          }
          test "a ~1 MB buffer digests to a fixed-form digest, never throwing (no truncation)" {
              let s = raw (digestOf bytesLarge)
              Expect.equal s.Length 64 "SHA-256 hex is 64 chars regardless of input size"
          }

          // (5) determinism / byte-stability — FR-009 / SC-005
          testPropertyWithConfig fscheckConfig "digest is deterministic and independent of array identity"
          <| fun (bytes: byte[]) ->
              digestOf bytes = digestOf bytes && digestOf bytes = digestOf (Array.copy bytes)

          // (6) identical streams — Edge "Identical stdout and stderr bytes"
          test "equal stdout and stderr bytes yield equal digests (content alone)" {
              let shared = System.Text.Encoding.UTF8.GetBytes "same-on-both-streams"
              Expect.equal (digestOf shared) (digestOf (Array.copy shared)) "equal content on both streams => equal digests"
          } ]
