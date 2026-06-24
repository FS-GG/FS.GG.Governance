module FS.GG.Governance.ExecutionRecord.Tests.CloseLoopTests

open Expecto
open FS.GG.Governance.CommandRecord
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.EvidenceCapture
open FS.GG.Governance.ExecutionRecord.Tests.Support

// US1 (close-the-loop) + US2 (record-level identity): an outcome assembled by `recordOf` derives a reproducible
// F049 reference and a reusable F030 store entry, with the sensed duration never leaking into the identity. The
// chain runs over the REAL F049/F030 operations (no re-implementation); no I/O (SC-008).

[<Tests>]
let tests =
    testList
        "CloseLoop"
        [ // (1) canonicalId + F049 referenceOf reproducible (SC-001, FR-007, US1 acceptance 1)
          test "canonicalId and referenceOf of an assembled record are defined and reproducible" {
              Expect.equal
                  (CommandRecord.canonicalId baseOutcome)
                  (CommandRecord.canonicalId baseOutcome)
                  "canonicalId is byte-stable"
              Expect.equal
                  (EvidenceCapture.referenceOf baseOutcome)
                  (EvidenceCapture.referenceOf baseOutcome)
                  "referenceOf is byte-stable"
          }

          // (2) capture makes the world reusable (SC-001, FR-007, US1 acceptance 3)
          test "capture into the empty store makes the world reusable for the derived reference" {
              let world = inputs "build:main"
              let grown = EvidenceCapture.capture world baseOutcome EvidenceReuse.empty
              Expect.equal
                  (EvidenceReuse.decide world grown)
                  (Reuse(EvidenceCapture.referenceOf baseOutcome))
                  "captured world reusable with the derived reference"
          }
          test "an unrelated world is still Recompute after capture (recompute-safety)" {
              let world = inputs "build:main"
              let grown = EvidenceCapture.capture world baseOutcome EvidenceReuse.empty
              Expect.equal
                  (EvidenceReuse.decide differentInputs grown)
                  (Recompute NoPriorEvidence)
                  "capture added no spurious match for a different world"
          }

          // (3) single-reproducible-fact perturbation changes identity + reference (SC-003, FR-007, US2 acceptance 4)
          test "perturbing any one reproducible fact changes both canonicalId and referenceOf" {
              let baseId = CommandRecord.canonicalId baseOutcome
              let baseRef = EvidenceCapture.referenceOf baseOutcome
              for label, variant in reproducibleVariants do
                  Expect.notEqual (CommandRecord.canonicalId variant) baseId (sprintf "%s changes canonicalId" label)
                  Expect.notEqual (EvidenceCapture.referenceOf variant) baseRef (sprintf "%s changes referenceOf" label)
          }
          test "a one-byte change to either output stream flips the reference (changed output never served as fresh)" {
              let baseRef = EvidenceCapture.referenceOf baseOutcome
              let outChanged = Build.outcome (stdout = System.Text.Encoding.UTF8.GetBytes "out-bytez")
              let errChanged = Build.outcome (stderr = System.Text.Encoding.UTF8.GetBytes "err-bytez")
              Expect.notEqual (EvidenceCapture.referenceOf outChanged) baseRef "one stdout byte flips the reference"
              Expect.notEqual (EvidenceCapture.referenceOf errChanged) baseRef "one stderr byte flips the reference"
          }

          testPropertyWithConfig fscheckConfig "distinct reproducible facts give distinct references (injectivity over arbitrary outcomes)"
          <| fun (a: CommandRecord) (b: CommandRecord) ->
              let factsDiffer = a.Reproducible <> b.Reproducible
              let refsDiffer = EvidenceCapture.referenceOf a <> EvidenceCapture.referenceOf b
              factsDiffer = refsDiffer

          // (4) duration-invariance of identity + reference (SC-004, FR-006, US2 acceptance 3)
          test "two outcomes differing only in duration share canonicalId and referenceOf" {
              Expect.equal
                  (CommandRecord.canonicalId baseOutcome)
                  (CommandRecord.canonicalId slowerOutcome)
                  "duration-only difference => equal canonicalId"
              Expect.equal
                  (EvidenceCapture.referenceOf baseOutcome)
                  (EvidenceCapture.referenceOf slowerOutcome)
                  "duration-only difference => equal referenceOf"
          }

          testPropertyWithConfig fscheckConfig "duration never leaks into the reference (property)"
          <| fun (r: CommandRecord) (d: int64) ->
              let slower = { r with Duration = SensedDuration d }
              EvidenceCapture.referenceOf r = EvidenceCapture.referenceOf slower ]
