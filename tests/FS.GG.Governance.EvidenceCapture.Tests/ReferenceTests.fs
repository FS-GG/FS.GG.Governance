module FS.GG.Governance.EvidenceCapture.Tests.ReferenceTests

open Expecto
open FS.GG.Governance.CommandRecord
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.EvidenceCapture
open FS.GG.Governance.EvidenceCapture.Tests.Support

// US2 (SC-002/SC-003/SC-005, FR-002/FR-003/FR-007/FR-008): `referenceOf` derives a reproducible, byte-stable,
// duration-invariant, injective `EvidenceRef` from an already-executed gate's `CommandRecord` — the sensed
// duration NEVER leaks, any reproducible-fact change DOES. Drives REAL F032 records (Support builders); no I/O.

/// The wrapped reference string (for byte-for-byte comparison + as the F032 identity proof).
let private refString (r: CommandRecord) =
    match EvidenceCapture.referenceOf r with
    | EvidenceRef s -> s

[<Tests>]
let tests =
    testList
        "Reference"
        [
          // ── (1) Duration-invariance (SC-002, FR-002, US2 acceptance 1) ──
          test "two records differing ONLY in SensedDuration yield the byte-identical reference" {
              Expect.equal
                  (EvidenceCapture.referenceOf baseRecord)
                  (EvidenceCapture.referenceOf slowerRecord)
                  "the sensed duration must never leak into the derived reference"
          }

          testPropertyWithConfig fscheckConfig "duration-invariance over arbitrary records × two durations" (fun (r: CommandRecord) (d1: int64) (d2: int64) ->
              // Same reproducible facts, two different sensed durations ⇒ one and the same reference.
              let a = { r with Duration = SensedDuration d1 }
              let b = { r with Duration = SensedDuration d2 }
              EvidenceCapture.referenceOf a = EvidenceCapture.referenceOf b)

          // ── (2) Reproducible-fact sensitivity / injectivity (SC-003, FR-003, US2 acceptance 2) ──
          test "each single reproducible-fact perturbation yields a DIFFERENT reference" {
              let baseRef = EvidenceCapture.referenceOf baseRecord

              for label, variant in reproducibleVariants do
                  Expect.notEqual
                      (EvidenceCapture.referenceOf variant)
                      baseRef
                      (sprintf "perturbing '%s' must change the reference" label)
          }

          test "all reproducible-fact perturbations are pairwise-distinct references (injective)" {
              let refs =
                  baseRecord :: (reproducibleVariants |> List.map snd)
                  |> List.map refString

              Expect.equal (List.length (List.distinct refs)) (List.length refs) "every distinct reproducible record must map to a distinct reference"
          }

          test "the three captured-output outcomes yield pairwise-distinct references (F032 FR-011)" {
              let refs = capturedOutputRecords |> List.map refString
              Expect.equal (List.length (List.distinct refs)) 3 "NoCapturedOutput, CapturedAt \"\", CapturedAt \"x\" must be three distinct references"
          }

          // ── (3) Reuse-the-identity: the reference IS the F032 canonical identity, wrapped (FR-001) ──
          test "the reference string equals the F032 canonical identity verbatim" {
              Expect.equal
                  (refString baseRecord)
                  (CommandRecord.identityValue (CommandRecord.canonicalId baseRecord))
                  "referenceOf wraps identityValue (canonicalId record) — no second identity scheme"
          }

          // ── (4) Totality + determinism (FR-007, SC-005, SC-008) ──
          test "referenceOf is total over the edge records (empty digests, non-zero exit, all captured outcomes)" {
              for label, r in edgeRecords do
                  // Forcing the wrapped string proves it neither threw nor produced a non-value.
                  Expect.isGreaterThanOrEqual
                      ((refString r).Length)
                      0
                      (sprintf "referenceOf must be total (never throw) on edge record '%s'" label)
          }

          testPropertyWithConfig fscheckConfig "determinism: same record ⇒ byte-identical reference, no clock/GUID/locale leakage" (fun (r: CommandRecord) ->
              EvidenceCapture.referenceOf r = EvidenceCapture.referenceOf r) ]
