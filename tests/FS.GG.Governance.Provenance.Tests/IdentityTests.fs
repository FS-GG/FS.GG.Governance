module FS.GG.Governance.Provenance.Tests.IdentityTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.CommandRecord
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.Provenance
open FS.GG.Governance.Provenance.Model
open FS.GG.Governance.Provenance.Tests.Support

// US2 — the embedded records' durations are reachable metadata, structurally excluded from the identity;
// `canonicalId` renders only the reproducible facts to a byte-stable `ProvenanceIdentity` (the F029/F032
// tagged/length-prefixed/injective encoding) folding each command record via F032 `CommandRecord.canonicalId`;
// `identityValue` unwraps it (SC-003, SC-004, FR-005/06/07/08/11, D3/D4/D5).

/// The exact canonical block of contracts/provenance-identity-format.md's worked example. Lines 7–17 are the
/// `cmds=1;135:` prefix followed by the full 135-byte F032 worked-example id (which itself contains '\n'),
/// then the 8th (`env`) and 9th (`bld`) provenance segments.
let private workedExampleBlock =
    String.concat
        "\n"
        [ "src=16:c0ffee"
          "base=15:base1"
          "head=15:head2"
          "rule=16:rule-x"
          "gen=15:gen-1"
          "art=2;2:a1;2:a2"
          "cmds=1;135:exe=13:gcc"
          "args=2;2:-c;6:main.c"
          "cwd=15:/work"
          "env+=1;n:2:CI|v:1:1"
          "env~=0;"
          "env-=0;"
          "to=12:30"
          "exit=11:0"
          "out=17:sha-out"
          "err=17:sha-err"
          "cap=0"
          "env=15:local"
          "bld=19:ci-runner" ]

[<Tests>]
let tests =
    testList
        "Identity"
        [ // (a) Sensed split — durations reachable, distinct, and identity-invariant (SC-003/SC-004, US2 #1/#3).
          test "embedded record durations are reachable as sensed metadata, distinct from reproducible facts" {
              Expect.equal baseProvenance.CommandRecords.Head.Duration (SensedDuration 123_456L) "record.Duration reachable via the provenance"
              Expect.equal baseProvenance.CommandRecords.Head.Reproducible.Executable (Executable "gcc") "reproducible facts reachable separately"
          }

          test "two provenances differing only in an embedded record's duration have equal canonicalId" {
              let p1 = { baseProvenance with CommandRecords = [ makeCommandRecord 1L ] }
              let p2 = { baseProvenance with CommandRecords = [ makeCommandRecord 9_999_999L ] }
              // The durations are genuinely different and reachable...
              Expect.notEqual p1.CommandRecords.Head.Duration p2.CommandRecords.Head.Duration "durations differ"
              // ...but the identity is the same (durations excluded — folded via F032 canonicalId).
              Expect.equal (Provenance.canonicalId (rebuild p1)) (Provenance.canonicalId (rebuild p2)) "duration excluded from identity"
          }

          // (b) Per-field sensitivity — flipping any one reproducible fact changes the identity (SC-004, US2 #2).
          test "changing any one reproducible fact changes canonicalId" {
              let baseId = Provenance.canonicalId baseProvenance
              for (label, variant) in allReproducibleVariants do
                  let perturbed = rebuild (variant baseProvenance)
                  Expect.notEqual (Provenance.canonicalId perturbed) baseId (sprintf "flipping '%s' must change the identity" label)
          }

          // (c) Injective across fields — same opaque string in two fields ⇒ different identities (L-I5).
          test "the same revision string in two fields yields different identities" {
              let p1 = rebuild { baseProvenance with SourceCommit = Revision "dup"; Base = Revision "x" }
              let p2 = rebuild { baseProvenance with SourceCommit = Revision "x"; Base = Revision "dup" }
              Expect.notEqual (Provenance.canonicalId p1) (Provenance.canonicalId p2) "src vs base are distinct tagged segments"
          }

          test "the same string as rule hash vs generator version yields different identities" {
              let p1 = rebuild { baseProvenance with RuleHash = RuleHash "shared"; GeneratorVersion = GeneratorVersion "g" }
              let p2 = rebuild { baseProvenance with RuleHash = RuleHash "g"; GeneratorVersion = GeneratorVersion "shared" }
              Expect.notEqual (Provenance.canonicalId p1) (Provenance.canonicalId p2) "rule vs gen are distinct tagged segments"
          }

          // (d) Idempotence + unwrap (SC-005, US2 #4).
          test "canonicalId is byte-identical when computed twice; identityValue unwraps it" {
              let a = Provenance.canonicalId baseProvenance
              let b = Provenance.canonicalId baseProvenance
              Expect.equal a b "idempotent"
              let (ProvenanceIdentity raw) = a
              Expect.equal (Provenance.identityValue a) raw "identityValue returns the canonical string"
          }

          // (e) Worked example renders to the contract's exact block (durations not appearing in it).
          test "worked example renders to the contract's exact canonical block" {
              let actual = Provenance.identityValue (Provenance.canonicalId baseProvenance)
              Expect.equal actual workedExampleBlock "worked-example identity equals contracts/provenance-identity-format.md"
              Expect.isFalse (actual.Contains "123456") "the sensed duration never appears in the identity"
          }

          test "the embedded F032 id inside the cmds segment is exactly 135 bytes" {
              let f32 = CommandRecord.identityValue (CommandRecord.canonicalId baseCommandRecord)
              Expect.equal (System.Text.Encoding.UTF8.GetByteCount f32) 135 "the contract's 135-byte cmds payload"
          }

          // FsCheck — the duration-invariance law over generated provenances.
          testPropertyWithConfig fscheckConfig "duration-only difference never changes the identity" <| fun (p: Provenance) ->
              // Bump every embedded record's duration by a fixed amount; the reproducible facts are untouched.
              let bumped =
                  p.CommandRecords
                  |> List.map (fun r ->
                      let (SensedDuration n) = r.Duration
                      CommandRecord.build
                          r.Reproducible.Executable r.Reproducible.Arguments r.Reproducible.WorkingDirectory
                          r.Reproducible.Environment r.Reproducible.Timeout r.Reproducible.ExitCode
                          r.Reproducible.StdoutDigest r.Reproducible.StderrDigest r.Reproducible.CapturedOutput
                          (SensedDuration(n + 1L)))
              let p2 = { p with CommandRecords = bumped }
              Provenance.canonicalId (rebuild p) = Provenance.canonicalId (rebuild p2) ]
