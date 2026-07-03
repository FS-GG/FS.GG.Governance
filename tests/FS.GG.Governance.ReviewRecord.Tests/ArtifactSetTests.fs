module FS.GG.Governance.ReviewRecord.Tests.ArtifactSetTests

open Expecto
open FsCheck
open FsCheck.FSharp
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.ReviewRecord
open FS.GG.Governance.ReviewRecord.Model
open FS.GG.Governance.ReviewRecord.Tests.Support

// US2 (D4, L-I5): the reviewed-artifact digests are compared as a SET — reorder/duplicate ⇒ same identity;
// add/remove a distinct digest ⇒ different identity; zero-artifact ⇒ deterministic `art=0;`.

let private idWith (arts: ArtifactHash list) : string =
    let r =
        buildOf baseRequest (modelId "gpt") (modelVersion "2026-06") (promptHash "ph1") arts (responseDigest "sha:resp") (recordedVerdict "pass") []

    ReviewRecord.identityValue (ReviewRecord.canonicalId r)

[<Tests>]
let tests =
    testList
        "ArtifactSet"
        [ test "reordering reviewed artifacts ⇒ same identity (L-I5)" {
              let a = [ artifactHash "sha:a"; artifactHash "sha:b"; artifactHash "sha:c" ]
              let b = [ artifactHash "sha:c"; artifactHash "sha:a"; artifactHash "sha:b" ]
              Expect.equal (idWith b) (idWith a) "reorder must not change identity"
          }

          test "duplicate reviewed artifacts ⇒ same identity as the deduped set (L-I5)" {
              let deduped = [ artifactHash "sha:a"; artifactHash "sha:b" ]
              let withDupes = [ artifactHash "sha:b"; artifactHash "sha:a"; artifactHash "sha:b"; artifactHash "sha:a" ]
              Expect.equal (idWith withDupes) (idWith deduped) "duplicates collapse to the same identity"
          }

          test "adding or removing a distinct digest ⇒ different identity" {
              let one = [ artifactHash "sha:a" ]
              let two = [ artifactHash "sha:a"; artifactHash "sha:b" ]
              Expect.notEqual (idWith two) (idWith one) "a distinct added digest changes identity"
              Expect.notEqual (idWith []) (idWith one) "removing the only digest changes identity"
          }

          test "zero-artifact record renders art=0; and identifies deterministically" {
              let id = idWith []
              Expect.stringContains id "art=0;" "empty set renders art=0;"
              Expect.equal id (idWith []) "zero-artifact identity is deterministic"
          }

          testPropertyWithConfig fscheckConfig "set-invariance over arbitrary permutations/duplications (L-I5)"
          <| fun (arts: ArtifactHash list) ->
              // A permutation-with-duplicates: reverse then append the originals (same SET).
              let shuffled = (List.rev arts) @ arts
              idWith shuffled = idWith arts ]
