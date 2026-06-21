module FS.GG.Governance.Enforcement.Tests.RecognitionTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Enforcement.Tests.Support

// US2 — total string recognition (FR-011, SC-005): every canonical mode/profile token recognizes to
// its typed value; any other string yields a total `Unrecognized` carrying the offending value
// verbatim (exact-token match — no trim, no case-fold, no default); the recognized sets are exactly
// the six modes and four profiles; the Profile<->ProfileId mapping is a total bijection.

[<Tests>]
let tests =
    testList
        "Recognition"
        [ test "each canonical run-mode token recognizes to its RunMode (SC-005)" {
              for (tok, expected) in canonicalModeTokens do
                  Expect.equal (recognizeMode tok) (Recognized expected) (sprintf "recognizeMode %A" tok)
          }

          test "each canonical profile token recognizes to its Profile (SC-005)" {
              for (tok, expected) in canonicalProfileTokens do
                  Expect.equal (recognizeProfile tok) (Recognized expected) (sprintf "recognizeProfile %A" tok)
          }

          test "every representative invalid string => Unrecognized carrying the exact input (FR-011)" {
              for raw in invalidTokens do
                  Expect.equal (recognizeMode raw) (Unrecognized raw) (sprintf "recognizeMode %A is Unrecognized verbatim" raw)
                  Expect.equal (recognizeProfile raw) (Unrecognized raw) (sprintf "recognizeProfile %A is Unrecognized verbatim" raw)
          }

          test "recognition is exact-token: no trim, no case-fold, no default" {
              // case-variant, whitespace-pad, and a near-miss all fail rather than silently mapping.
              Expect.equal (recognizeMode "Gate") (Unrecognized "Gate") "no case-fold"
              Expect.equal (recognizeMode "  inner ") (Unrecognized "  inner ") "no trim"
              Expect.equal (recognizeProfile "lite") (Unrecognized "lite") "no near-miss default"
              Expect.equal (recognizeMode "ship") (Unrecognized "ship") "ship is not a run mode"
          }

          test "the recognized sets are exactly six modes and four profiles (SC-005)" {
              let recognizedModes =
                  canonicalModeTokens
                  |> List.choose (fun (tok, _) -> match recognizeMode tok with Recognized m -> Some m | _ -> None)
                  |> List.distinct

              let recognizedProfiles =
                  canonicalProfileTokens
                  |> List.choose (fun (tok, _) -> match recognizeProfile tok with Recognized p -> Some p | _ -> None)
                  |> List.distinct

              Expect.equal recognizedModes.Length 6 "exactly six modes recognized"
              Expect.equal recognizedProfiles.Length 4 "exactly four profiles recognized"
          }

          test "profileOfProfileId recognizes the four canonical ids and rejects others (FR-011)" {
              Expect.equal (profileOfProfileId (ProfileId "light")) (Recognized Light) "light"
              Expect.equal (profileOfProfileId (ProfileId "standard")) (Recognized Standard) "standard"
              Expect.equal (profileOfProfileId (ProfileId "strict")) (Recognized Strict) "strict"
              Expect.equal (profileOfProfileId (ProfileId "release")) (Recognized Profile.Release) "release"
              Expect.equal (profileOfProfileId (ProfileId "experimental")) (Unrecognized "experimental") "non-canonical id carried"
          }

          test "Profile<->ProfileId is a total bijection over the four canonical profiles (FR-003)" {
              for p in allProfiles do
                  Expect.equal (profileOfProfileId (profileToProfileId p)) (Recognized p) (sprintf "round-trip %A" p)

              let tokens =
                  allProfiles |> List.map (fun p -> let (ProfileId s) = profileToProfileId p in s)

              Expect.equal (List.sort tokens) [ "light"; "release"; "standard"; "strict" ] "exactly the four canonical tokens"
          } ]
