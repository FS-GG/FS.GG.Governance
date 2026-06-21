module FS.GG.Governance.CommandRecord.Tests.IdentityTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.CommandRecord
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.CommandRecord.Tests.Support

// US2 — the sensed duration is reachable metadata, structurally excluded from the identity; `canonicalId`
// renders only `record.Reproducible` to a byte-stable `CommandIdentity` (the F029 tagged/length-prefixed/
// injective encoding); `identityValue` unwraps it (SC-003, SC-004, FR-005/06/07/11, D5/D6).

/// The exact canonical block of contracts/command-record-identity-format.md's worked example.
let private workedExampleBlock =
    String.concat
        "\n"
        [ "exe=13:gcc"
          "args=2;2:-c;6:main.c"
          "cwd=15:/work"
          "env+=1;n:2:CI|v:1:1"
          "env~=0;"
          "env-=0;"
          "to=12:30"
          "exit=11:0"
          "out=17:sha-out"
          "err=17:sha-err"
          "cap=0" ]

/// The worked-example record: built exactly as the contract's §"Worked example" describes.
let private workedExampleRecord =
    CommandRecord.build
        (Executable "gcc")
        [ Argument "-c"; Argument "main.c" ]
        (WorkingDirectory "/work")
        { Added = [ { Name = EnvVarName "CI"; Value = EnvVarValue "1" } ]; Changed = []; Removed = [] }
        (TimeoutLimit 30)
        (ExitCode 0)
        (OutputDigest "sha-out")
        (OutputDigest "sha-err")
        NoCapturedOutput
        (SensedDuration 999L)

[<Tests>]
let tests =
    testList
        "Identity"
        [ // (a) Sensed split — duration reachable, distinct, and identity-invariant (SC-003/SC-004, US2 #1/#3).
          test "duration is reachable as sensed metadata, distinct from the reproducible facts" {
              Expect.equal baseRecord.Duration baseDuration "record.Duration is reachable"
              // The reproducible part carries no duration field — it is a distinct value entirely.
              Expect.equal baseRecord.Reproducible.Executable baseExecutable "reproducible facts are reachable separately"
          }

          test "two records differing only in duration have equal canonicalId" {
              let r1 = rebuild baseRecord.Reproducible (SensedDuration 1L)
              let r2 = rebuild baseRecord.Reproducible (SensedDuration 9_999_999L)
              Expect.equal (CommandRecord.canonicalId r1) (CommandRecord.canonicalId r2) "duration excluded from identity"
          }

          // (b) Per-field sensitivity — flipping any one reproducible fact changes the identity (SC-004, US2 #2).
          test "changing any one reproducible fact changes canonicalId" {
              let baseId = CommandRecord.canonicalId baseRecord
              for (label, variant) in allReproducibleVariants do
                  let perturbed = rebuild (variant baseRecord.Reproducible) baseDuration
                  Expect.notEqual (CommandRecord.canonicalId perturbed) baseId (sprintf "flipping '%s' must change the identity" label)
          }

          // (c) Captured-output disambiguation — absence / empty path / non-empty path are 3 distinct ids (FR-011, D5).
          test "NoCapturedOutput, empty path, and non-empty path yield three distinct identities" {
              let idOf cap =
                  CommandRecord.canonicalId (rebuild { baseRecord.Reproducible with CapturedOutput = cap } baseDuration)

              let none = idOf NoCapturedOutput
              let emptyPath = idOf (CapturedAt(CapturedOutputPath ""))
              let realPath = idOf (CapturedAt(CapturedOutputPath "x"))

              Expect.notEqual none emptyPath "absence distinct from empty present path"
              Expect.notEqual none realPath "absence distinct from real path"
              Expect.notEqual emptyPath realPath "empty path distinct from real path"
          }

          // (d) Idempotence + unwrap (SC-005, US2 #4).
          test "canonicalId is byte-identical when computed twice; identityValue unwraps it" {
              let a = CommandRecord.canonicalId baseRecord
              let b = CommandRecord.canonicalId baseRecord
              Expect.equal a b "idempotent"
              let (CommandIdentity raw) = a
              Expect.equal (CommandRecord.identityValue a) raw "identityValue returns the canonical string"
          }

          // (e) Worked example renders to the contract's exact block (the duration not appearing in it).
          test "worked example renders to the contract's exact canonical block" {
              let actual = CommandRecord.identityValue (CommandRecord.canonicalId workedExampleRecord)
              Expect.equal actual workedExampleBlock "worked-example identity equals contracts/command-record-identity-format.md"
              Expect.isFalse (actual.Contains "999") "the sensed duration never appears in the identity"
          }

          // FsCheck — the duration-invariance law over generated facts/durations.
          testPropertyWithConfig fscheckConfig "duration-only difference never changes the identity" <| fun (facts: ReproducibleFacts) (d1: SensedDuration) (d2: SensedDuration) ->
              let id1 = CommandRecord.canonicalId (rebuild facts d1)
              let id2 = CommandRecord.canonicalId (rebuild facts d2)
              id1 = id2 ]
