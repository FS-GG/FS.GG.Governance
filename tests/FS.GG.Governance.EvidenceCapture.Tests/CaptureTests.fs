module FS.GG.Governance.EvidenceCapture.Tests.CaptureTests

open Expecto
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.FreshnessKey
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.EvidenceCapture
open FS.GG.Governance.EvidenceCapture.Tests.Support

// US1 (SC-001/SC-005, FR-004/FR-005/FR-008): `capture` folds an already-executed gate's DERIVED reference into
// the store against its resolved freshness world, so the captured world becomes reusable and serves exactly
// that derived reference — close-the-loop. US3 (SC-004, FR-006) below: capture is purely additive and
// recompute-safe — no clobber of a prior entry, no spurious match for any other world. Drives REAL F030 stores
// and F032 records (Support builders); no I/O.

[<Tests>]
let tests =
    testList
        "Capture"
        [
          // ════════════════════ US1 — a real execution becomes a reusable store entry ════════════════════

          // ── (1) Close-the-loop into the empty store (SC-001, FR-005, US1 acceptance 1) ──
          test "decide inputs (capture inputs record empty) = Reuse (referenceOf record)" {
              let world = inputs "build:tests"
              let grown = EvidenceCapture.capture world baseRecord EvidenceReuse.empty

              Expect.equal
                  (EvidenceReuse.decide world grown)
                  (Reuse(EvidenceCapture.referenceOf baseRecord))
                  "the captured world is reusable and serves exactly the derived reference"
          }

          test "capturing into the empty store yields a one-entry store" {
              let world = inputs "build:tests"
              let grown = EvidenceCapture.capture world baseRecord EvidenceReuse.empty

              match EvidenceReuse.entries grown with
              | [ e ] ->
                  Expect.equal e.Inputs world "the single entry carries the captured world"
                  Expect.equal e.Evidence (EvidenceCapture.referenceOf baseRecord) "the single entry carries the derived reference"
              | other -> failtestf "expected a one-entry store, got %A" other
          }

          // ── (2) Determinism / byte-stability (SC-005, FR-008, US1 acceptance 2) ──
          test "capture is deterministic into the empty store" {
              let world = inputs "build:tests"

              Expect.equal
                  (EvidenceCapture.capture world baseRecord EvidenceReuse.empty)
                  (EvidenceCapture.capture world baseRecord EvidenceReuse.empty)
                  "identical input yields the byte-identical store"
          }

          testPropertyWithConfig fscheckConfig "capture is deterministic over an arbitrary prior store" (fun (world: FreshnessInputs) (r: CommandRecord) (store: ReuseStore) ->
              EvidenceCapture.capture world r store = EvidenceCapture.capture world r store)

          // ── (3) No spurious match for a different world (US1 acceptance 3) ──
          test "capturing one world adds no match for an unrelated world" {
              let world = inputs "build:tests"
              let grown = EvidenceCapture.capture world baseRecord EvidenceReuse.empty

              match EvidenceReuse.decide differentInputs grown with
              | Recompute _ -> ()
              | Reuse r -> failtestf "capture fabricated a match for an unrelated world: Reuse %A" r
          }

          // ════════════════════ US3 — capture is purely additive: no policy, no clobber ════════════════════

          // ── (1) Prior entries preserved + recompute-safe (SC-004, FR-006, US3 acceptance 1) ──
          test "capture into a non-empty store is the verbatim F030 record fold of the derived reference" {
              let world = inputs "build:tests"

              let prior =
                  storeOf
                      [ inputs "fmt", syntheticRef "fmt" // SYNTHETIC: prior entry; real refs need gate execution
                        inputs "lint", syntheticRef "lint" ]

              Expect.equal
                  (EvidenceCapture.capture world baseRecord prior)
                  (EvidenceReuse.record world (EvidenceCapture.referenceOf baseRecord) prior)
                  "capture is EXACTLY EvidenceReuse.record inputs (referenceOf record) store — no new policy"
          }

          test "capturing a brand-new world preserves every prior entry byte-for-byte (newest-first)" {
              let world = inputs "build:tests"

              let prior =
                  storeOf
                      [ inputs "fmt", syntheticRef "fmt"
                        inputs "lint", syntheticRef "lint" ]

              let grown = EvidenceCapture.capture world baseRecord prior

              // The new entry is most-recent; every prior entry follows, unchanged and in order.
              match EvidenceReuse.entries grown with
              | newest :: rest ->
                  Expect.equal newest.Inputs world "newest entry is the captured world"
                  Expect.equal newest.Evidence (EvidenceCapture.referenceOf baseRecord) "newest entry is the derived reference"
                  Expect.equal rest (EvidenceReuse.entries prior) "every prior entry preserved byte-for-byte, in order"
              | [] -> failtest "capture into a non-empty store must not yield an empty store"
          }

          // Recompute-safety has two universally-true facets (FR-006, SC-004). F030 `decide` derives a Recompute
          // CAUSE positionally — from the most-recent entry sharing the candidate's GateId (Check AND Domain) —
          // so capturing a world that SHARES a non-matching candidate's gate legitimately makes the cause diff
          // against the now-most-recent same-gate world (more precise; never a weakening, never a spurious
          // Reuse). The two facets below pin exactly the safety the spec requires without over-asserting that
          // positional cause:
          //   (a) for EVERY non-captured candidate, the reuse/recompute CLASSIFICATION and any Reuse reference
          //       are preserved — capture never fabricates a match and never removes a match for another world;
          //   (b) for a candidate that does NOT share the captured world's gate, the FULL decision (cause
          //       included) is preserved byte-for-byte.
          testPropertyWithConfig fscheckConfig "recompute-safe (a): classification + Reuse reference preserved for every non-captured candidate" (fun (world: FreshnessInputs) (r: CommandRecord) (store: ReuseStore) (candidate: FreshnessInputs) ->
              let grown = EvidenceCapture.capture world r store

              if FreshnessKey.matches candidate world then
                  true // the captured world itself legitimately becomes/stays reusable
              else
                  match EvidenceReuse.decide candidate store, EvidenceReuse.decide candidate grown with
                  | Reuse a, Reuse b -> a = b // no spurious reference change for an unrelated world
                  | Recompute _, Recompute _ -> true // still recompute; cause may sharpen, never weaken
                  | _ -> false) // a flipped classification would be a regression — fail

          testPropertyWithConfig fscheckConfig "recompute-safe (b): full decision preserved for a candidate not sharing the captured world's gate" (fun (world: FreshnessInputs) (r: CommandRecord) (store: ReuseStore) (candidate: FreshnessInputs) ->
              let grown = EvidenceCapture.capture world r store

              if candidate.Check = world.Check && candidate.Domain = world.Domain then
                  true // shares the gate ⇒ covered by facet (a); the positional cause may sharpen
              else
                  EvidenceReuse.decide candidate grown = EvidenceReuse.decide candidate store)

          // ── (2) Newest-first duplicate capture (US3 acceptance 2, Edge "Duplicate capture") ──
          test "re-capturing the same world with a new execution serves the most-recently-captured reference" {
              let world = inputs "build:tests"

              // A second execution differing in a reproducible fact ⇒ a DIFFERENT derived reference.
              let later = Build.record (executable = "clang")

              let grown =
                  EvidenceReuse.empty
                  |> EvidenceCapture.capture world baseRecord
                  |> EvidenceCapture.capture world later

              Expect.notEqual
                  (EvidenceCapture.referenceOf later)
                  (EvidenceCapture.referenceOf baseRecord)
                  "the two executions must derive distinct references (precondition)"

              Expect.equal
                  (EvidenceReuse.decide world grown)
                  (Reuse(EvidenceCapture.referenceOf later))
                  "decide serves the most-recently-captured reference (F030 newest-first; no new dedup policy)"
          } ]
