module FS.GG.Governance.ReviewRecord.Tests.DeterminismTests

open System
open System.IO
open Expecto
open FsCheck
open FsCheck.FSharp
open FS.GG.Governance.ReviewRecord
open FS.GG.Governance.ReviewRecord.Model
open FS.GG.Governance.ReviewRecord.Tests.Support

// Cross-cutting (SC-005, FR-005): building a record and deriving its identity is byte-for-byte identical when
// performed in different working directories, at different times, and with unrelated filesystem state changed
// between operations; no model invoked, no clock/filesystem/git/environment/network read, no bytes hashed,
// nothing persisted. Mirrors the PromptIsolation/AgentReviewKey purity-test precedent.

let private idOf (r: ReviewRecord) : string = ReviewRecord.identityValue (ReviewRecord.canonicalId r)

let private sample () : ReviewRecord =
    buildOf
        baseRequest
        (modelId "gpt")
        (modelVersion "2026-06")
        (promptHash "ph1")
        [ artifactHash "sha:a"; artifactHash "sha:b" ]
        (responseDigest "sha:resp")
        (recordedVerdict "pass")
        [ sensedAt "at" "2026-06-22T10:00:00Z" ]

[<Tests>]
let tests =
    testList
        "Determinism"
        [ test "build + canonicalId are byte-identical across cwd change and unrelated filesystem mutation" {
              let originalCwd = Directory.GetCurrentDirectory()
              let tmp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())
              Directory.CreateDirectory tmp |> ignore

              try
                  let r1 = sample ()
                  let id1 = idOf r1

                  // Change the working directory and touch an unrelated file between the two computations.
                  Directory.SetCurrentDirectory tmp
                  File.WriteAllText(Path.Combine(tmp, "noise.txt"), "unrelated state")

                  let r2 = sample ()
                  let id2 = idOf r2

                  Expect.equal r2 r1 "the record is structurally identical regardless of cwd/filesystem state"
                  Expect.equal id2 id1 "the identity is byte-identical regardless of cwd/filesystem state"
              finally
                  Directory.SetCurrentDirectory originalCwd
                  try
                      Directory.Delete(tmp, true)
                  with _ ->
                      ()
          }

          testPropertyWithConfig fscheckConfig "build and canonicalId are pure functions of their inputs"
          <| fun (r: ReviewRecord) ->
              // Re-derive identity twice; a pure function gives the same string each time.
              idOf r = idOf r ]
